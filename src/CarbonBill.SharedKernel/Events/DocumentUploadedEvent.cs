using CarbonBill.SharedKernel.Domain;

namespace CarbonBill.SharedKernel.Events;

public record DocumentUploadedEvent(
    Guid DocumentId,
    Guid OrgId,
    string StoragePath,
    string ContentType,
    string Source,
    DateTime OccurredOnUtc,
    Guid? SiteId = null,
    Guid? AssetId = null,
    string? DocType = null,
    string? BillingPeriod = null,
    string? FileName = null) : IDomainEvent;
