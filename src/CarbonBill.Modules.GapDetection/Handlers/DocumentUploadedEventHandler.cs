using CarbonBill.Modules.GapDetection.Services;
using CarbonBill.SharedKernel.Events;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.GapDetection.Handlers;

public class DocumentUploadedEventHandler(
    IGapDetector gapDetector,
    ILogger<DocumentUploadedEventHandler> logger) : IDomainEventHandler<DocumentUploadedEvent>
{
    public async Task HandleAsync(DocumentUploadedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("DocumentUploadedEvent received in GapDetection: DocId={DocId}, OrgId={OrgId}, AssetId={AssetId}, DocType={DocType}, Period={Period}",
            domainEvent.DocumentId, domainEvent.OrgId, domainEvent.AssetId, domainEvent.DocType, domainEvent.BillingPeriod);

        // Auto-resolve any pending alert matching this document
        var resolvedCount = await gapDetector.ResolveAlertForDocumentAsync(
            domainEvent.OrgId,
            domainEvent.AssetId,
            domainEvent.DocType,
            domainEvent.BillingPeriod,
            domainEvent.DocumentId,
            cancellationToken);

        if (resolvedCount > 0)
        {
            logger.LogInformation("Successfully auto-resolved {Count} gap alerts upon document arrival", resolvedCount);
        }
        else if (!string.IsNullOrWhiteSpace(domainEvent.BillingPeriod))
        {
            // Re-run gap check to ensure state reconciliation
            await gapDetector.CheckMissingDocumentsAsync(
                domainEvent.OrgId,
                domainEvent.BillingPeriod,
                cancellationToken);
        }
    }
}
