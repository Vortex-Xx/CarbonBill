using CarbonBill.Modules.Flags.Domain;

namespace CarbonBill.Modules.Flags.Engine;

public record EvaluationContext(
    Guid OrgId,
    string Period,
    Guid? SiteId = null,
    Guid? DocumentId = null);

public record EvaluationResult(
    bool ShouldFlag,
    string? SeverityOverride = null,
    string? EvidenceJson = null,
    string? ExplanationBn = null,
    string? ExplanationEn = null);

public interface IFlagRuleEvaluator
{
    string RuleCode { get; }
    Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default);
}
