using CarbonBill.Modules.Documents.Domain;
using CarbonBill.Modules.Documents.Persistence;
using CarbonBill.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Documents.Services;

public class DocumentExtractedHandler(
    DocumentsDbContext dbContext,
    ILogger<DocumentExtractedHandler> logger) : IDomainEventHandler<DocumentExtractedEvent>
{
    public async Task HandleAsync(DocumentExtractedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await dbContext.Documents
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Id == domainEvent.DocumentId, cancellationToken);

            if (document == null)
            {
                logger.LogWarning("Document {DocumentId} not found to update post-extraction status", domainEvent.DocumentId);
                return;
            }

            document.Status = DocumentStatuses.NeedsReview;
            document.TierUsed = domainEvent.TierUsed;
            if (!string.IsNullOrWhiteSpace(domainEvent.DetectedDocType))
            {
                if (domainEvent.DetectedDocType != "GeneralDocument" || string.IsNullOrWhiteSpace(document.DocType))
                {
                    document.DocType = domainEvent.DetectedDocType;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Document {DocumentId} advanced to status '{Status}' after extraction", document.Id, document.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update Document {DocumentId} on DocumentExtractedEvent", domainEvent.DocumentId);
        }
    }
}
