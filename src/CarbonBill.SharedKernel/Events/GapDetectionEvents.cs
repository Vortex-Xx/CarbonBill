using CarbonBill.SharedKernel.Domain;

namespace CarbonBill.SharedKernel.Events;

public record MissingAlertRaisedEvent(
    Guid AlertId,
    Guid OrgId,
    Guid SiteId,
    Guid AssetId,
    string AssetName,
    string DocType,
    string Period,
    DateTime DueDate,
    string EscalationState,
    string Severity,
    Guid? ResponsibleUserId,
    DateTime OccurredOnUtc) : IDomainEvent;

public record MissingAlertResolvedEvent(
    Guid AlertId,
    Guid OrgId,
    Guid AssetId,
    string Period,
    Guid DocumentId,
    DateTime OccurredOnUtc) : IDomainEvent;
