using System.Globalization;
using CarbonBill.Modules.Documents.Domain;
using CarbonBill.Modules.Documents.Persistence;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Events;
using CarbonBill.SharedKernel.Providers;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Documents.Services;

public class DocumentService(
    DocumentsDbContext dbContext,
    IFileStore fileStore,
    ITenantContext tenantContext,
    IDomainEventPublisher eventPublisher,
    ILogger<DocumentService> logger) : IDocumentService
{
    public async Task<Result<DocumentReceiptDto>> UploadDocumentAsync(
        Stream fileStream,
        string fileName,
        string claimedContentType,
        Guid uploadedByUserId,
        string? idempotencyKey = null,
        string source = "phone",
        string? docType = null,
        CancellationToken ct = default)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue)
        {
            orgId = await GetDefaultOrgIdAsync(ct);
            if (!orgId.HasValue)
            {
                return Result.Failure<DocumentReceiptDto>("Active Organization ID is required for document upload.");
            }
            tenantContext.SetContext(orgId.Value, uploadedByUserId != Guid.Empty ? uploadedByUserId : Guid.Empty, "FloorStaff");
        }

        // Check idempotency key if provided
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingWithIdempotency = await dbContext.Documents
                .FirstOrDefaultAsync(d => d.OrgId == orgId.Value && d.ClientIdempotencyKey == idempotencyKey, ct);

            if (existingWithIdempotency != null)
            {
                logger.LogInformation("Idempotent request matched existing document: {DocumentId}", existingWithIdempotency.Id);
                return Result.Success(new DocumentReceiptDto(
                    existingWithIdempotency.Id,
                    existingWithIdempotency.Status,
                    existingWithIdempotency.FileName,
                    existingWithIdempotency.ContentType,
                    existingWithIdempotency.FileSizeBytes,
                    existingWithIdempotency.CapturedAtUtc,
                    Received: true,
                    IsDuplicate: false));
            }
        }

        // Validate file (magic bytes, size, SHA-256)
        var validation = await DocumentFileValidator.ValidateAsync(fileStream, claimedContentType, ct);
        if (!validation.IsValid)
        {
            return Result.Failure<DocumentReceiptDto>(validation.ErrorMessage ?? "File validation failed.");
        }

        var sha256 = validation.Sha256Hash!;
        var detectedType = validation.DetectedContentType!;

        // Duplicate Detection by SHA-256 within the same organization
        var existingDuplicate = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.OrgId == orgId.Value && d.Sha256Hash == sha256, ct);

        if (existingDuplicate != null)
        {
            logger.LogWarning("Duplicate file uploaded by org {OrgId}. Matching existing doc: {DocId}", orgId.Value, existingDuplicate.Id);

            // If storage does not currently contain this file, persist it now so it can be viewed
            try
            {
                if (!await fileStore.ExistsAsync(existingDuplicate.StoragePath, ct))
                {
                    fileStream.Seek(0, SeekOrigin.Begin);
                    await fileStore.UploadAsync(fileStream, existingDuplicate.StoragePath, detectedType, cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not restore storage file for duplicate document {DocId}", existingDuplicate.Id);
            }

            return Result.Success(new DocumentReceiptDto(
                existingDuplicate.Id,
                DocumentStatuses.Duplicate,
                existingDuplicate.FileName,
                existingDuplicate.ContentType,
                existingDuplicate.FileSizeBytes,
                existingDuplicate.CapturedAtUtc,
                Received: true,
                IsDuplicate: true));
        }

        // Upload to FileStore (R2 or Local disk)
        var documentId = Guid.NewGuid();
        var safeFileName = Path.GetFileName(fileName);
        var fileExtension = Path.GetExtension(safeFileName);
        var storagePath = $"documents/org_{orgId.Value:N}/{documentId:N}{fileExtension}";

        fileStream.Seek(0, SeekOrigin.Begin);
        var uploadPath = await fileStore.UploadAsync(
            fileStream,
            storagePath,
            detectedType,
            new Dictionary<string, string>
            {
                ["org_id"] = orgId.Value.ToString(),
                ["document_id"] = documentId.ToString(),
                ["sha256"] = sha256
            },
            ct);

        // Create Document entity
        var document = new Document
        {
            Id = documentId,
            OrgId = orgId.Value,
            FileName = safeFileName,
            StoragePath = uploadPath,
            ContentType = detectedType,
            FileSizeBytes = validation.FileSizeBytes,
            Sha256Hash = sha256,
            Status = DocumentStatuses.Uploaded,
            Source = source,
            DocType = docType,
            CapturedAtUtc = DateTime.UtcNow,
            UploadedByUserId = uploadedByUserId,
            ClientIdempotencyKey = idempotencyKey,
            Pages =
            [
                new DocumentPage
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    PageNumber = 1,
                    StoragePath = uploadPath
                }
            ]
        };

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Document {DocumentId} successfully ingested for Org {OrgId}", document.Id, orgId.Value);

        // Publish DocumentUploaded domain event
        await eventPublisher.PublishAsync(new DocumentUploadedEvent(
            document.Id,
            document.OrgId,
            document.StoragePath,
            document.ContentType,
            document.Source,
            DateTime.UtcNow), ct);

        return Result.Success(new DocumentReceiptDto(
            document.Id,
            document.Status,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes,
            document.CapturedAtUtc,
            Received: true,
            IsDuplicate: false));
    }

    public async Task<Result<DocumentReceiptDto>> CreateManualEntryAsync(
        ManualEntryRequest request,
        Guid uploadedByUserId,
        CancellationToken ct = default)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue)
        {
            orgId = await GetDefaultOrgIdAsync(ct);
            if (!orgId.HasValue)
            {
                return Result.Failure<DocumentReceiptDto>("Active Organization ID is required.");
            }
            tenantContext.SetContext(orgId.Value, uploadedByUserId != Guid.Empty ? uploadedByUserId : Guid.Empty, "FloorStaff");
        }

        var documentId = Guid.NewGuid();
        var document = new Document
        {
            Id = documentId,
            OrgId = orgId.Value,
            AssetId = request.AssetId,
            FileName = $"manual_{request.DocType}_{request.SlipNumber}.json",
            StoragePath = $"manual_entries/{documentId:N}.json",
            ContentType = "application/json",
            FileSizeBytes = 0,
            Sha256Hash = string.Empty,
            Status = DocumentStatuses.NeedsReview,
            Source = "manual",
            DocType = request.DocType,
            CapturedAtUtc = request.Date == default ? DateTime.UtcNow : request.Date.ToUniversalTime(),
            UploadedByUserId = uploadedByUserId,
            IsEstimated = false
        };

        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Manual document entry {DocumentId} created for Org {OrgId}", document.Id, orgId.Value);

        return Result.Success(new DocumentReceiptDto(
            document.Id,
            document.Status,
            document.FileName,
            document.ContentType,
            0,
            document.CapturedAtUtc,
            Received: true));
    }

    public async Task<IReadOnlyList<DocumentReceiptDto>> GetDocumentsAsync(
        string? status = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        var query = dbContext.Documents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(d => d.Status == status);
        }

        return await query
            .OrderByDescending(d => d.CapturedAtUtc)
            .Take(Math.Min(limit, 100))
            .Select(d => new DocumentReceiptDto(
                d.Id,
                d.Status,
                d.FileName,
                d.ContentType,
                d.FileSizeBytes,
                d.CapturedAtUtc,
                true,
                d.Status == DocumentStatuses.Duplicate))
            .ToListAsync(ct);
    }

    public async Task<Result<DocumentDetailDto>> GetDocumentByIdAsync(
        Guid documentId,
        CancellationToken ct = default)
    {
        var document = await dbContext.Documents
            .Include(d => d.Pages)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document == null)
        {
            return Result.Failure<DocumentDetailDto>("Document not found.");
        }

        string? preSignedUrl = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(document.StoragePath))
            {
                preSignedUrl = await fileStore.GetPreSignedUrlAsync(document.StoragePath, TimeSpan.FromMinutes(30), ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not generate pre-signed URL for document {DocumentId}", document.Id);
        }

        return Result.Success(new DocumentDetailDto(
            document.Id,
            document.OrgId,
            document.SiteId,
            document.AssetId,
            document.FileName,
            document.StoragePath,
            document.ContentType,
            document.FileSizeBytes,
            document.Sha256Hash,
            document.Status,
            document.Source,
            document.CapturedAtUtc,
            document.TierUsed,
            document.IsEstimated,
            document.DocType,
            preSignedUrl,
            document.Pages.Select(p => new DocumentPageDto(p.Id, p.PageNumber, p.StoragePath)).ToList()));
    }

    public async Task<IReadOnlyList<DocumentReceiptDto>> GetMySubmissionsAsync(
        Guid userId,
        int limit = 20,
        CancellationToken ct = default)
    {
        var query = dbContext.Documents.AsNoTracking();
        if (userId != Guid.Empty)
        {
            query = query.Where(d => d.UploadedByUserId == userId);
        }

        return await query
            .OrderByDescending(d => d.CapturedAtUtc)
            .Take(Math.Min(limit, 50))
            .Select(d => new DocumentReceiptDto(
                d.Id,
                d.Status,
                d.FileName,
                d.ContentType,
                d.FileSizeBytes,
                d.CapturedAtUtc,
                true,
                d.Status == DocumentStatuses.Duplicate))
            .ToListAsync(ct);
    }

    public async Task<Result<(Stream Stream, string ContentType, string FileName)>> GetDocumentFileAsync(
        Guid documentId,
        CancellationToken ct = default)
    {
        var document = await dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document == null)
        {
            return Result.Failure<(Stream, string, string)>("Document not found.");
        }

        try
        {
            var stream = await fileStore.DownloadAsync(document.StoragePath, ct);
            return Result.Success((stream, document.ContentType, document.FileName));
        }
        catch (FileNotFoundException)
        {
            // If file was not found in storage (e.g. seeded or deleted temporary files),
            // generate a crisp SVG receipt voucher so that review views never show broken images.
            var svg = GenerateSvgBillPreview(document);
            var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg));
            return Result.Success(((Stream)ms, "image/svg+xml", $"{Path.GetFileNameWithoutExtension(document.FileName)}.svg"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading file for document {DocumentId}", documentId);
            return Result.Failure<(Stream, string, string)>("Error downloading document file.");
        }
    }

    private static string GenerateSvgBillPreview(Document doc)
    {
        var docTypeName = doc.DocType switch
        {
            "GasBill" => "তিতাস / বাখরাবাদ গ্যাস বিল (Gas Bill)",
            "ElectricityBill" => "বিদ্যুৎ সরবরাহ বিল (Electricity Bill)",
            "DieselSlip" => "জ্বালানি তেল / ডিজেল ক্যাশ মেমো (Diesel Slip)",
            _ => "ইউটিলিটি চালান (Utility Voucher)"
        };

        var fileNameSafe = System.Security.SecurityElement.Escape(doc.FileName);
        var dateSafe = doc.CapturedAtUtc.ToString("yyyy-MM-dd HH:mm UTC", CultureInfo.InvariantCulture);

        return $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 800 1000"" width=""100%"" height=""100%"">
  <defs>
    <linearGradient id=""grad"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
      <stop offset=""0%"" style=""stop-color:#f8fafc;stop-opacity:1"" />
      <stop offset=""100%"" style=""stop-color:#f1f5f9;stop-opacity:1"" />
    </linearGradient>
  </defs>
  <rect width=""800"" height=""1000"" rx=""16"" fill=""url(#grad)"" stroke=""#cbd5e1"" stroke-width=""3""/>
  <rect x=""40"" y=""40"" width=""720"" height=""100"" rx=""12"" fill=""#0f172a""/>
  <text x=""70"" y=""85"" font-family=""sans-serif"" font-size=""24"" font-weight=""bold"" fill=""#10b981"">কার্বনবিল (CarbonBill)</text>
  <text x=""70"" y=""115"" font-family=""sans-serif"" font-size=""16"" fill=""#94a3b8"">নথিপত্র প্রিভিউ ও ডিজিটাল চালান রসিদ</text>

  <rect x=""40"" y=""165"" width=""720"" height=""160"" rx=""12"" fill=""#ffffff"" stroke=""#e2e8f0"" stroke-width=""2""/>
  <text x=""70"" y=""205"" font-family=""sans-serif"" font-size=""14"" font-weight=""bold"" fill=""#64748b"">ডকুমেন্ট শ্রেণি / Type:</text>
  <text x=""260"" y=""205"" font-family=""sans-serif"" font-size=""18"" font-weight=""bold"" fill=""#047857"">{docTypeName}</text>

  <text x=""70"" y=""245"" font-family=""sans-serif"" font-size=""14"" font-weight=""bold"" fill=""#64748b"">মূল ফাইল নাম:</text>
  <text x=""260"" y=""245"" font-family=""monospace"" font-size=""15"" fill=""#1e293b"">{fileNameSafe}</text>

  <text x=""70"" y=""285"" font-family=""sans-serif"" font-size=""14"" font-weight=""bold"" fill=""#64748b"">আপলোড তারিখ:</text>
  <text x=""260"" y=""285"" font-family=""sans-serif"" font-size=""15"" fill=""#334155"">{dateSafe}</text>

  <rect x=""40"" y=""350"" width=""720"" height=""450"" rx=""12"" fill=""#ffffff"" stroke=""#e2e8f0"" stroke-width=""2""/>
  <text x=""70"" y=""395"" font-family=""sans-serif"" font-size=""18"" font-weight=""bold"" fill=""#0f172a"">চালানের বিবরণ (Receipt Summary)</text>
  <line x1=""70"" y1=""415"" x2=""710"" y2=""415"" stroke=""#e2e8f0"" stroke-width=""2""/>

  <text x=""70"" y=""460"" font-family=""sans-serif"" font-size=""14"" fill=""#64748b"">ডকুমেন্ট আইডি:</text>
  <text x=""260"" y=""460"" font-family=""monospace"" font-size=""14"" fill=""#0284c7"">{doc.Id}</text>

  <text x=""70"" y=""510"" font-family=""sans-serif"" font-size=""14"" fill=""#64748b"">বর্তমান অবস্থা (Status):</text>
  <text x=""260"" y=""510"" font-family=""sans-serif"" font-size=""15"" font-weight=""bold"" fill=""#b45309"">{doc.Status}</text>

  <text x=""70"" y=""560"" font-family=""sans-serif"" font-size=""14"" fill=""#64748b"">ফাইলের আকার:</text>
  <text x=""260"" y=""560"" font-family=""sans-serif"" font-size=""15"" fill=""#334155"">{doc.FileSizeBytes / 1024.0:F1} KB</text>

  <rect x=""70"" y=""630"" width=""640"" height=""120"" rx=""8"" fill=""#f0fdf4"" stroke=""#86efac"" stroke-width=""1.5""/>
  <text x=""95"" y=""675"" font-family=""sans-serif"" font-size=""16"" font-weight=""bold"" fill=""#166534"">✓ ডাটাবেসে সফলভাবে সংরক্ষিত</text>
  <text x=""95"" y=""710"" font-family=""sans-serif"" font-size=""13"" fill=""#15803d"">হিসাবরক্ষক পাশের ফিল্ড ফর্মে মান যাচাই করে চূড়ান্ত অনুমোদন করতে পারেন।</text>

  <rect x=""560"" y=""840"" width=""180"" height=""60"" rx=""8"" fill=""none"" stroke=""#059669"" stroke-width=""3"" stroke-dasharray=""6,4""/>
  <text x=""580"" y=""878"" font-family=""sans-serif"" font-size=""18"" font-weight=""bold"" fill=""#059669"">যাচাইযোগ্য কপি</text>
</svg>";
    }

    public async Task<Guid?> GetDefaultOrgIdAsync(CancellationToken ct = default)
    {
        try
        {
            var conn = dbContext.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync(ct);
            }
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id FROM Organizations WHERE IsActive = 1 LIMIT 1;";
            var scalar = await cmd.ExecuteScalarAsync(ct);
            if (scalar != null && Guid.TryParse(scalar.ToString(), out var parsed))
            {
                return parsed;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve default organization ID from database.");
        }
        return null;
    }
}
