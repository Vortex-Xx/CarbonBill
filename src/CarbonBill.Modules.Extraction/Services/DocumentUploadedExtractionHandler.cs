using CarbonBill.Modules.Extraction.Services;
using CarbonBill.SharedKernel.Events;
using CarbonBill.SharedKernel.Providers;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Extraction.Services;

public class DocumentUploadedExtractionHandler(
    IFileStore fileStore,
    IExtractionService extractionService,
    ITenantContext tenantContext,
    ILogger<DocumentUploadedExtractionHandler> logger) : IDomainEventHandler<DocumentUploadedEvent>
{
    public async Task HandleAsync(DocumentUploadedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Background extraction triggered for Document {DocumentId}...", domainEvent.DocumentId);
            tenantContext.SetContext(domainEvent.OrgId, Guid.Empty);

            await using var stream = await fileStore.DownloadAsync(domainEvent.StoragePath, cancellationToken);
            if (stream == null)
            {
                logger.LogWarning("Could not download file stream for {StoragePath}", domainEvent.StoragePath);
                return;
            }

            var fileName = domainEvent.FileName ?? Path.GetFileName(domainEvent.StoragePath);
            var result = await extractionService.ProcessExtractionAsync(
                domainEvent.DocumentId,
                stream,
                fileName,
                domainEvent.ContentType,
                hasTier3Consent: true,
                ct: cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation("Document {DocumentId} extraction completed successfully with Tier {Tier}", domainEvent.DocumentId, result.Value.TierUsed);
            }
            else
            {
                logger.LogWarning("Document {DocumentId} extraction failed: {Error}", domainEvent.DocumentId, result.Error);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in DocumentUploadedExtractionHandler for Document {DocumentId}", domainEvent.DocumentId);
        }
    }
}
