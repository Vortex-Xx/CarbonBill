using System.Text.Json;
using CarbonBill.Modules.Extraction.Persistence;
using CarbonBill.Modules.Extraction.Services.Classification;
using CarbonBill.Modules.Extraction.Services.Groq;
using CarbonBill.Modules.Extraction.Services.Providers;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Events;
using CarbonBill.SharedKernel.Providers;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Extraction.Services;

public interface IExtractionService
{
    Task<Result<ExtractionRun>> ProcessExtractionAsync(
        Guid documentId,
        Stream documentStream,
        string fileName,
        string contentType,
        bool hasTier3Consent = true,
        CancellationToken ct = default);

    Task<ExtractionRun?> GetExtractionRunForDocumentAsync(
        Guid documentId,
        CancellationToken ct = default);
}

public class ExtractionService(
    ExtractionDbContext dbContext,
    IDualOcrEngine dualOcrEngine,
    IGroqLlmExtractor groqExtractor,
    ITenantContext tenantContext,
    IDomainEventPublisher eventPublisher,
    ILogger<ExtractionService> logger) : IExtractionService
{
    public async Task<Result<ExtractionRun>> ProcessExtractionAsync(
        Guid documentId,
        Stream documentStream,
        string fileName,
        string contentType,
        bool hasTier3Consent = true,
        CancellationToken ct = default)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue)
        {
            return Result.Failure<ExtractionRun>("Active Organization ID is required.");
        }

        logger.LogInformation("Processing extraction pipeline for Document {DocumentId}...", documentId);

        // 1. Dual OCR (Option C: Tesseract / PaddleOCR)
        var rawOcrText = await dualOcrEngine.ExtractRawTextAsync(documentStream, fileName, contentType, ct);

        // 2. Classify Document Type
        var classification = DocumentClassifier.Classify(rawOcrText, fileName);

        // 3. Groq LLM Semantic Intelligence (openai/gpt-oss-120b) with consent gating
        var groqResult = await groqExtractor.ExtractAsync(rawOcrText, fileName, contentType, hasTier3Consent, ct);

        // 4. Save ExtractionRun and ExtractedFields
        var extractionRunId = Guid.NewGuid();
        var extractionRun = new ExtractionRun
        {
            Id = extractionRunId,
            OrgId = orgId.Value,
            DocumentId = documentId,
            TierUsed = groqResult.TierUsed,
            Status = "Completed",
            OverallConfidence = 0.95f,
            ProcessedAtUtc = DateTime.UtcNow,
            RawOutputJson = JsonSerializer.Serialize(new
            {
                classification = classification.DocumentType,
                rawOcrText
            })
        };

        foreach (var field in groqResult.Fields)
        {
            extractionRun.Fields.Add(new ExtractedField
            {
                Id = Guid.NewGuid(),
                OrgId = orgId.Value,
                ExtractionRunId = extractionRunId,
                DocumentId = documentId,
                FieldName = field.FieldName,
                RawValue = field.RawValue,
                NormalizedValue = field.NormalizedValue,
                Confidence = field.Confidence,
                SourceTier = field.SourceTier,
                BoundingBoxJson = field.BoundingBox != null ? JsonSerializer.Serialize(field.BoundingBox) : null
            });
        }

        dbContext.ExtractionRuns.Add(extractionRun);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Extraction completed for Document {DocumentId}. Extracted {Count} fields.", documentId, extractionRun.Fields.Count);

        var finalDocType = (groqResult.DetectedDocumentType != null && groqResult.DetectedDocumentType != "GeneralDocument")
            ? groqResult.DetectedDocumentType
            : (classification.DocumentType != DocumentTypes.Unknown ? classification.DocumentType : (groqResult.DetectedDocumentType ?? "GeneralDocument"));

        // Publish DocumentExtracted domain event
        await eventPublisher.PublishAsync(new DocumentExtractedEvent(
            documentId,
            orgId.Value,
            groqResult.TierUsed,
            finalDocType,
            extractionRun.Fields.Count,
            DateTime.UtcNow), ct);

        return Result.Success(extractionRun);
    }

    public async Task<ExtractionRun?> GetExtractionRunForDocumentAsync(
        Guid documentId,
        CancellationToken ct = default)
    {
        return await dbContext.ExtractionRuns
            .Include(r => r.Fields)
            .AsNoTracking()
            .OrderByDescending(r => r.ProcessedAtUtc)
            .FirstOrDefaultAsync(r => r.DocumentId == documentId, ct);
    }
}
