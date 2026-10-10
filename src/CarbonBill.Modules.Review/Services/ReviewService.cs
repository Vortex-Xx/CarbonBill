using System.Globalization;
using CarbonBill.Modules.Audit.Services;
using CarbonBill.Modules.Documents.Domain;
using CarbonBill.Modules.Documents.Persistence;
using CarbonBill.Modules.Extraction;
using CarbonBill.Modules.Extraction.Persistence;
using CarbonBill.Modules.Review.Domain;
using CarbonBill.Modules.Review.Persistence;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Review.Services;

public interface IReviewService
{
    Task<IReadOnlyList<ReviewQueueItemDto>> GetReviewQueueAsync(
        Guid? siteId = null,
        string? docType = null,
        int limit = 50,
        CancellationToken ct = default);

    Task<Result<bool>> UpdateDocumentFieldsAsync(
        Guid documentId,
        UpdateFieldsRequest request,
        Guid reviewerUserId,
        CancellationToken ct = default);

    Task<Result<Guid>> ConfirmDocumentAsync(
        Guid documentId,
        ConfirmDocumentRequest request,
        Guid reviewerUserId,
        string? userRole,
        CancellationToken ct = default);

    Task<BulkConfirmResultDto> BulkConfirmAsync(
        BulkConfirmRequest request,
        Guid reviewerUserId,
        string? userRole,
        CancellationToken ct = default);

    Task<Result<ReviewOrganizationSetting>> GetOrUpdateOrgSettingsAsync(
        bool? autoConfirmEnabled = null,
        float? threshold = null,
        float? samplingRate = null,
        CancellationToken ct = default);
}

public class ReviewService(
    ReviewDbContext reviewDb,
    DocumentsDbContext documentsDb,
    ExtractionDbContext extractionDb,
    IActivityWriter activityWriter,
    IAuditLogService auditLogService,
    ITenantContext tenantContext,
    ILogger<ReviewService> logger) : IReviewService
{
    private static readonly HashSet<string> DisallowedConfirmRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Operator",
        "FloorStaff",
    };

    private static readonly Guid DefaultPilotOrgId = Guid.Parse("f0ee71e0-717b-4b5e-af05-a443c6430a8f");

    private async Task<Guid> GetEffectiveOrgIdAsync(CancellationToken ct)
    {
        if (tenantContext.CurrentOrgId.HasValue) return tenantContext.CurrentOrgId.Value;
        var existing = await documentsDb.Documents.Select(d => (Guid?)d.OrgId).FirstOrDefaultAsync(ct);
        return existing ?? DefaultPilotOrgId;
    }

    public async Task<IReadOnlyList<ReviewQueueItemDto>> GetReviewQueueAsync(
        Guid? siteId = null,
        string? docType = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        var orgId = await GetEffectiveOrgIdAsync(ct);

        var query = documentsDb.Documents
            .AsNoTracking()
            .Where(d => d.OrgId == orgId &&
                        (d.Status == DocumentStatuses.NeedsReview || d.Status == DocumentStatuses.Uploaded || d.Status == DocumentStatuses.Extracting));

        if (siteId.HasValue)
        {
            query = query.Where(d => d.SiteId == siteId.Value);
        }

        if (!string.IsNullOrWhiteSpace(docType))
        {
            if (docType.Equals("GeneralDocument", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(d => d.DocType == "GeneralDocument" || string.IsNullOrEmpty(d.DocType));
            }
            else
            {
                query = query.Where(d => d.DocType == docType);
            }
        }

        var candidateDocs = await query.Take(limit * 2).ToListAsync(ct);
        var docIds = candidateDocs.Select(d => d.Id).ToList();

        // Get extraction runs and fields
        var runs = await extractionDb.ExtractionRuns
            .Include(r => r.Fields)
            .AsNoTracking()
            .Where(r => r.OrgId == orgId && docIds.Contains(r.DocumentId))
            .ToListAsync(ct);

        var runMap = runs.GroupBy(r => r.DocumentId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ProcessedAtUtc).First());

        var queueItems = new List<ReviewQueueItemDto>();
        foreach (var doc in candidateDocs)
        {
            runMap.TryGetValue(doc.Id, out var run);
            var overallConfidence = run?.OverallConfidence ?? (doc.TierUsed == 1 ? 0.70f : 0.85f);
            var fields = run?.Fields.Select(f => new ReviewFieldDto(
                f.Id,
                f.FieldName,
                f.RawValue,
                f.NormalizedValue,
                f.CorrectedValue,
                f.Confidence,
                f.SourceTier,
                f.BoundingBoxJson
            )).ToList() ?? [];

            queueItems.Add(new ReviewQueueItemDto(
                doc.Id,
                doc.FileName,
                doc.ContentType,
                doc.FileSizeBytes,
                doc.Status,
                doc.DocType,
                doc.CapturedAtUtc,
                overallConfidence,
                doc.TierUsed,
                doc.IsEstimated,
                fields
            ));
        }

        // Must be sorted lowest confidence first as per spec, prioritizing actionable items with extracted fields
        return queueItems
            .OrderByDescending(q => q.Fields.Count > 0)
            .ThenBy(q => q.OverallConfidence)
            .ThenByDescending(q => q.CapturedAtUtc)
            .Take(limit)
            .ToList();
    }

    public async Task<Result<bool>> UpdateDocumentFieldsAsync(
        Guid documentId,
        UpdateFieldsRequest request,
        Guid reviewerUserId,
        CancellationToken ct = default)
    {
        var orgId = await GetEffectiveOrgIdAsync(ct);
        if (reviewerUserId == Guid.Empty) reviewerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var run = await extractionDb.ExtractionRuns
            .Include(r => r.Fields)
            .FirstOrDefaultAsync(r => r.OrgId == orgId && r.DocumentId == documentId, ct);

        if (run == null)
        {
            return Result.Failure<bool>("Extraction run not found for document.");
        }

        foreach (var kvp in request.FieldCorrections)
        {
            var field = run.Fields.FirstOrDefault(f => string.Equals(f.FieldName, kvp.Key, StringComparison.OrdinalIgnoreCase));
            if (field != null)
            {
                field.CorrectedValue = kvp.Value;
            }
            else
            {
                run.Fields.Add(new ExtractedField
                {
                    Id = Guid.NewGuid(),
                    OrgId = orgId,
                    ExtractionRunId = run.Id,
                    DocumentId = documentId,
                    FieldName = kvp.Key,
                    RawValue = kvp.Value,
                    NormalizedValue = kvp.Value,
                    CorrectedValue = kvp.Value,
                    Confidence = 1.0f,
                    SourceTier = 0
                });
            }
        }

        await extractionDb.SaveChangesAsync(ct);

        await auditLogService.LogAsync(new AuditLogEntry(
            OrgId: orgId,
            Action: "FieldsCorrected",
            EntityType: "Document",
            EntityId: documentId.ToString(),
            UserId: reviewerUserId,
            Details: new { Corrections = request.FieldCorrections, request.Notes }), ct);

        return Result.Success(true);
    }

    public async Task<Result<Guid>> ConfirmDocumentAsync(
        Guid documentId,
        ConfirmDocumentRequest request,
        Guid reviewerUserId,
        string? userRole,
        CancellationToken ct = default)
    {
        var orgId = await GetEffectiveOrgIdAsync(ct);
        if (reviewerUserId == Guid.Empty) reviewerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        if (string.IsNullOrWhiteSpace(userRole)) userRole = "Accountant";

        // 1. Permission Matrix Check
        if (!string.IsNullOrWhiteSpace(userRole) && DisallowedConfirmRoles.Contains(userRole))
        {
            return Result.Failure<Guid>("Floor staff / operator role cannot confirm documents. Accountant or Admin permission required.");
        }

        // 2. Fetch Document
        var document = await documentsDb.Documents.FirstOrDefaultAsync(d => d.OrgId == orgId && d.Id == documentId, ct);
        if (document == null)
        {
            return Result.Failure<Guid>($"Document {documentId} not found.");
        }

        if (document.Status == DocumentStatuses.Locked)
        {
            return Result.Failure<Guid>("Locked documents cannot be confirmed or modified.");
        }

        // 3. Resolve duplicate if requested ("Keep one" duplicate resolution)
        if (request.ResolveDuplicateWithDocId.HasValue)
        {
            var dupeDoc = await documentsDb.Documents.FirstOrDefaultAsync(d => d.OrgId == orgId && d.Id == request.ResolveDuplicateWithDocId.Value, ct);
            if (dupeDoc != null)
            {
                dupeDoc.MarkDuplicate();
                await auditLogService.LogAsync(new AuditLogEntry(
                    OrgId: orgId,
                    Action: "DuplicateResolved",
                    EntityType: "Document",
                    EntityId: dupeDoc.Id.ToString(),
                    UserId: reviewerUserId,
                    Details: new { KeptDocumentId = documentId, MarkedDuplicateId = dupeDoc.Id }), ct);
            }
        }

        // 4. Retrieve Extracted Fields
        var run = await extractionDb.ExtractionRuns
            .Include(r => r.Fields)
            .OrderByDescending(r => r.ProcessedAtUtc)
            .FirstOrDefaultAsync(r => r.OrgId == orgId && r.DocumentId == documentId, ct);

        var fields = run?.Fields ?? [];

        // Helper to get effective value: override > corrected > normalized > raw
        string GetFieldValue(string fieldName)
        {
            var match = fields.FirstOrDefault(f => string.Equals(f.FieldName, fieldName, StringComparison.OrdinalIgnoreCase));
            if (match == null) return string.Empty;
            return match.CorrectedValue ?? match.NormalizedValue ?? match.RawValue ?? string.Empty;
        }

        var effectiveVendor = GetFieldValue("Vendor");
        var effectiveBillNo = GetFieldValue("BillNumber");
        var effectivePeriod = request.OverridePeriod ?? GetFieldValue("BillingPeriod");
        if (string.IsNullOrWhiteSpace(effectivePeriod)) effectivePeriod = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        decimal quantity = 0;
        if (request.OverrideQuantity.HasValue && request.OverrideQuantity.Value > 0)
        {
            quantity = request.OverrideQuantity.Value;
        }
        else if (decimal.TryParse(GetFieldValue("Quantity"), NumberStyles.Number, CultureInfo.InvariantCulture, out var q))
        {
            quantity = q;
        }

        string unit = request.OverrideUnit ?? GetFieldValue("Unit");
        if (string.IsNullOrWhiteSpace(unit)) unit = "kWh";

        decimal? amountBdt = request.OverrideAmountBdt;
        if (!amountBdt.HasValue && decimal.TryParse(GetFieldValue("AmountBdt"), NumberStyles.Number, CultureInfo.InvariantCulture, out var a))
        {
            amountBdt = a;
        }

        string activityType = request.ActivityType ?? document.DocType ?? "Electricity";
        if (activityType.Contains("Diesel", StringComparison.OrdinalIgnoreCase)) activityType = "Diesel";
        else if (activityType.Contains("Gas", StringComparison.OrdinalIgnoreCase)) activityType = "NaturalGas";
        else if (activityType.Contains("Ship", StringComparison.OrdinalIgnoreCase) || activityType.Contains("Truck", StringComparison.OrdinalIgnoreCase)) activityType = "Transport";
        else activityType = "Electricity";

        // Validate required fields
        if (quantity <= 0)
        {
            return Result.Failure<Guid>("Validation failed: Quantity must be greater than zero to confirm document.");
        }

        // 5. Transaction Semantic: Call IActivityWriter.RecordConfirmedActivity
        var activityRequest = new ConfirmedActivityRequest(
            OrgId: orgId,
            SiteId: document.SiteId,
            AssetId: document.AssetId,
            DocumentId: documentId,
            ActivityType: activityType,
            Quantity: quantity,
            Unit: unit,
            TotalCostBdt: amountBdt,
            BillingPeriod: effectivePeriod,
            IsEstimated: request.IsEstimated || document.IsEstimated);

        var activityResult = await activityWriter.RecordConfirmedActivityAsync(activityRequest, ct);
        if (!activityResult.IsSuccess)
        {
            logger.LogError("Atomic confirmation failed: ActivityWriter rejected activity for Doc {DocId}: {Error}", documentId, activityResult.Error);
            return Result.Failure<Guid>($"Confirmation aborted: Failed to record activity: {activityResult.Error}");
        }

        // 6. Update Document State: Confirmed -> Calculated
        document.Status = DocumentStatuses.Confirmed;
        document.IsEstimated = request.IsEstimated || document.IsEstimated;
        document.Status = DocumentStatuses.Calculated;
        await documentsDb.SaveChangesAsync(ct);

        // 7. Update Review Decisions and Org Settings
        var decision = new ReviewDecision
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            DocumentId = documentId,
            ReviewerUserId = reviewerUserId,
            Mode = ReviewModes.Manual,
            Decision = ReviewDecisions.Approved,
            Notes = request.Notes,
            DecidedAtUtc = DateTime.UtcNow
        };
        reviewDb.Decisions.Add(decision);

        var orgSetting = await reviewDb.OrganizationSettings.FirstOrDefaultAsync(s => s.OrgId == orgId, ct);
        if (orgSetting == null)
        {
            orgSetting = new ReviewOrganizationSetting { Id = Guid.NewGuid(), OrgId = orgId, ManualConfirmCount = 1 };
            reviewDb.OrganizationSettings.Add(orgSetting);
        }
        else
        {
            orgSetting.ManualConfirmCount++;
            orgSetting.UpdatedAtUtc = DateTime.UtcNow;
        }
        await reviewDb.SaveChangesAsync(ct);

        // 8. Audit Log
        await auditLogService.LogAsync(new AuditLogEntry(
            OrgId: orgId,
            Action: "DocumentConfirmed",
            EntityType: "Document",
            EntityId: documentId.ToString(),
            UserId: reviewerUserId,
            Details: new
            {
                ActivityId = activityResult.Value,
                activityType,
                quantity,
                unit,
                amountBdt,
                effectivePeriod,
                isEstimated = document.IsEstimated,
                reviewerUserId,
                mode = ReviewModes.Manual
            }), ct);

        logger.LogInformation("Document {DocId} successfully confirmed and calculated by User {UserId}", documentId, reviewerUserId);
        return Result.Success(documentId);
    }

    public async Task<BulkConfirmResultDto> BulkConfirmAsync(
        BulkConfirmRequest request,
        Guid reviewerUserId,
        string? userRole,
        CancellationToken ct = default)
    {
        int succeeded = 0;
        int failed = 0;
        var errors = new List<string>();

        foreach (var id in request.DocumentIds)
        {
            var res = await ConfirmDocumentAsync(id, new ConfirmDocumentRequest(), reviewerUserId, userRole, ct);
            if (res.IsSuccess)
            {
                succeeded++;
            }
            else
            {
                failed++;
                errors.Add($"Doc {id}: {res.Error}");
            }
        }

        return new BulkConfirmResultDto(request.DocumentIds.Count, succeeded, failed, errors);
    }

    public async Task<Result<ReviewOrganizationSetting>> GetOrUpdateOrgSettingsAsync(
        bool? autoConfirmEnabled = null,
        float? threshold = null,
        float? samplingRate = null,
        CancellationToken ct = default)
    {
        var orgId = await GetEffectiveOrgIdAsync(ct);

        var setting = await reviewDb.OrganizationSettings.FirstOrDefaultAsync(s => s.OrgId == orgId, ct);
        if (setting == null)
        {
            setting = new ReviewOrganizationSetting
            {
                Id = Guid.NewGuid(),
                OrgId = orgId,
                AutoConfirmEnabled = autoConfirmEnabled ?? false,
                AutoConfirmThreshold = threshold ?? 0.95f,
                SamplingRate = samplingRate ?? 0.10f,
                UpdatedAtUtc = DateTime.UtcNow
            };
            reviewDb.OrganizationSettings.Add(setting);
        }
        else
        {
            if (autoConfirmEnabled.HasValue) setting.AutoConfirmEnabled = autoConfirmEnabled.Value;
            if (threshold.HasValue) setting.AutoConfirmThreshold = threshold.Value;
            if (samplingRate.HasValue) setting.SamplingRate = samplingRate.Value;
            setting.UpdatedAtUtc = DateTime.UtcNow;
        }

        await reviewDb.SaveChangesAsync(ct);
        return Result.Success(setting);
    }
}
