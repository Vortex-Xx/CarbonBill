namespace CarbonBill.SharedKernel.Contracts;

public record FlagDto(
    Guid Id,
    Guid OrgId,
    Guid? SiteId,
    Guid RuleId,
    string RuleCode,
    string Family,
    string Severity,
    string Period,
    string EvidenceJson,
    string ExplanationBn,
    string ExplanationEn,
    string SuggestedActionBn,
    string SuggestedActionEn,
    string State,
    string? DismissedReason,
    DateTime? DismissedUntil,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record RaiseFlagRequest(
    Guid OrgId,
    Guid? SiteId,
    string RuleCode,
    string Period,
    string EvidenceJson,
    string ExplanationBn,
    string ExplanationEn,
    string? SeverityOverride = null);

public interface IFlagRaiser
{
    Task<FlagDto> RaiseOrUpdateFlagAsync(RaiseFlagRequest request, CancellationToken ct = default);
    Task<bool> AutoResolveFlagAsync(Guid orgId, string ruleCode, string period, Guid? siteId = null, CancellationToken ct = default);
}

public interface IFlagReader
{
    Task<IReadOnlyList<FlagDto>> GetFlagsAsync(
        Guid orgId,
        string? period = null,
        string? state = null,
        string? severity = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<IReadOnlyList<FlagDto>> GetDashboardTop5FlagsAsync(Guid orgId, CancellationToken ct = default);
}
