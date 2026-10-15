using CarbonBill.SharedKernel.Domain;

namespace CarbonBill.SharedKernel.Events;

public record DocumentConfirmedEvent(
    Guid DocumentId,
    Guid OrgId,
    Guid? SiteId,
    Guid? AssetId,
    string? DocType,
    string? Period,
    DateTime OccurredOnUtc) : IDomainEvent;

public record EmissionCalculatedEvent(
    Guid OrgId,
    Guid? SiteId,
    string Period,
    decimal TotalCo2eKg,
    decimal EstimatedCo2eKg,
    DateTime OccurredOnUtc) : IDomainEvent;
