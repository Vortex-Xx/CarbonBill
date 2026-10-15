using CarbonBill.SharedKernel.Domain;

namespace CarbonBill.SharedKernel.Events;

public record FlagRaisedEvent(
    Guid FlagId,
    Guid OrgId,
    Guid? SiteId,
    string RuleCode,
    string Family,
    string Severity,
    string Period,
    string ExplanationBn,
    string ExplanationEn,
    string SuggestedActionBn,
    string SuggestedActionEn,
    DateTime OccurredOnUtc) : IDomainEvent;

public record DocumentRetakeRequestedEvent(
    Guid DocumentId,
    Guid OrgId,
    Guid? UploaderUserId,
    string FileName,
    string Reason,
    DateTime OccurredOnUtc) : IDomainEvent;

public record DocumentFailedEvent(
    Guid DocumentId,
    Guid OrgId,
    Guid? UploaderUserId,
    string FileName,
    string FailureReason,
    DateTime OccurredOnUtc) : IDomainEvent;
