using System.Globalization;
using CarbonBill.Modules.Flags.Domain;
using CarbonBill.Modules.Flags.Engine;
using CarbonBill.Modules.Flags.Persistence;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Flags.Services;

public interface IFlagEngine
{
    Task<IReadOnlyList<FlagDto>> EvaluateOrgPeriodAsync(Guid orgId, string period, Guid? siteId = null, CancellationToken ct = default);
    Task<int> CheckAndExpireDismissalsAsync(CancellationToken ct = default);
}

public class FlagsService(
    FlagsDbContext dbContext,
    IEnumerable<IFlagRuleEvaluator> evaluators,
    IDomainEventPublisher eventPublisher,
    ILogger<FlagsService> logger,
    CarbonBill.SharedKernel.Providers.ILangSmithTracer? langSmithTracer = null) : IFlagRaiser, IFlagReader, IFlagEngine
{
    private readonly FlagsDbContext _dbContext = dbContext;
    private readonly IEnumerable<IFlagRuleEvaluator> _evaluators = evaluators;
    private readonly IDomainEventPublisher _eventPublisher = eventPublisher;
    private readonly ILogger<FlagsService> _logger = logger;
    private readonly CarbonBill.SharedKernel.Providers.ILangSmithTracer? _langSmithTracer = langSmithTracer;

    // --- IFlagRaiser ---

    public async Task<FlagDto> RaiseOrUpdateFlagAsync(RaiseFlagRequest request, CancellationToken ct = default)
    {
        var rule = await _dbContext.FlagRules
            .FirstOrDefaultAsync(r => r.FlagCode == request.RuleCode, ct);

        if (rule == null)
        {
            throw new InvalidOperationException($"FlagRule with code '{request.RuleCode}' was not found in database.");
        }

        var now = DateTime.UtcNow;
        var existingFlag = await _dbContext.Flags
            .FirstOrDefaultAsync(f => f.OrgId == request.OrgId &&
                                      f.RuleCode == request.RuleCode &&
                                      f.Period == request.Period, ct);

        var severity = request.SeverityOverride ?? rule.DefaultSeverity;

        if (existingFlag == null)
        {
            var newFlag = new Flag
            {
                OrgId = request.OrgId,
                SiteId = request.SiteId,
                RuleId = rule.Id,
                RuleCode = rule.FlagCode,
                Family = rule.Family,
                Severity = severity,
                Period = request.Period,
                EvidenceJson = request.EvidenceJson,
                ExplanationBn = request.ExplanationBn,
                ExplanationEn = request.ExplanationEn,
                SuggestedActionBn = rule.SuggestedActionBn,
                SuggestedActionEn = rule.SuggestedActionEn,
                State = FlagStates.Open,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _dbContext.Flags.Add(newFlag);
            await _dbContext.SaveChangesAsync(ct);

            // Publish FlagRaisedEvent for Notifications module
            await _eventPublisher.PublishAsync(new FlagRaisedEvent(
                FlagId: newFlag.Id,
                OrgId: newFlag.OrgId,
                SiteId: newFlag.SiteId,
                RuleCode: newFlag.RuleCode,
                Family: newFlag.Family,
                Severity: newFlag.Severity,
                Period: newFlag.Period,
                ExplanationBn: newFlag.ExplanationBn,
                ExplanationEn: newFlag.ExplanationEn,
                SuggestedActionBn: newFlag.SuggestedActionBn,
                SuggestedActionEn: newFlag.SuggestedActionEn,
                OccurredOnUtc: now
            ), ct);

            _logger.LogInformation("Raised new Flag {FlagId} for Rule {RuleCode}, Org {OrgId}, Period {Period}",
                newFlag.Id, newFlag.RuleCode, newFlag.OrgId, newFlag.Period);

            return MapToDto(newFlag);
        }

        // Check if existing flag was dismissed and if dismissal expired
        existingFlag.CheckAndExpireDismissal(now);

        // Update evidence and severity if condition worsened
        existingFlag.EvidenceJson = request.EvidenceJson;
        existingFlag.ExplanationBn = request.ExplanationBn;
        existingFlag.ExplanationEn = request.ExplanationEn;
        existingFlag.Severity = severity;
        existingFlag.UpdatedAtUtc = now;

        // If it was resolved but problem recurred, reopen
        if (existingFlag.State == FlagStates.Resolved)
        {
            existingFlag.Reopen(now);
            await _eventPublisher.PublishAsync(new FlagRaisedEvent(
                FlagId: existingFlag.Id,
                OrgId: existingFlag.OrgId,
                SiteId: existingFlag.SiteId,
                RuleCode: existingFlag.RuleCode,
                Family: existingFlag.Family,
                Severity: existingFlag.Severity,
                Period: existingFlag.Period,
                ExplanationBn: existingFlag.ExplanationBn,
                ExplanationEn: existingFlag.ExplanationEn,
                SuggestedActionBn: existingFlag.SuggestedActionBn,
                SuggestedActionEn: existingFlag.SuggestedActionEn,
                OccurredOnUtc: now
            ), ct);
        }

        await _dbContext.SaveChangesAsync(ct);
        return MapToDto(existingFlag);
    }

    public async Task<bool> AutoResolveFlagAsync(Guid orgId, string ruleCode, string period, Guid? siteId = null, CancellationToken ct = default)
    {
        var existingFlag = await _dbContext.Flags
            .FirstOrDefaultAsync(f => f.OrgId == orgId &&
                                      f.RuleCode == ruleCode &&
                                      f.Period == period &&
                                      (f.State == FlagStates.Open || f.State == FlagStates.Acknowledged), ct);

        if (existingFlag == null)
        {
            return false;
        }

        existingFlag.Resolve(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Auto-resolved Flag {FlagId} for Rule {RuleCode}, Period {Period} as condition cleared",
            existingFlag.Id, ruleCode, period);

        return true;
    }

    // --- IFlagReader ---

    public async Task<IReadOnlyList<FlagDto>> GetFlagsAsync(
        Guid orgId,
        string? period = null,
        string? state = null,
        string? severity = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = _dbContext.Flags.Where(f => f.OrgId == orgId);

        if (!string.IsNullOrWhiteSpace(period))
            query = query.Where(f => f.Period == period);

        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(f => f.State == state);

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(f => f.Severity == severity);

        var p = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 100);

        var list = await query
            .OrderByDescending(f => f.CreatedAtUtc)
            .Skip((p - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return list.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<FlagDto>> GetDashboardTop5FlagsAsync(Guid orgId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Fetch open or acknowledged flags (excluding resolved)
        var flags = await _dbContext.Flags
            .Where(f => f.OrgId == orgId && f.State != FlagStates.Resolved)
            .ToListAsync(ct);

        // Check and expire dismissals
        foreach (var f in flags)
        {
            f.CheckAndExpireDismissal(now);
        }

        // Active items: Open or Acknowledged (Dismissed flags that haven't expired are suppressed)
        int SeverityWeight(string s) => s.ToUpperInvariant() switch
        {
            "RED" => 1,
            "AMBER" => 2,
            _ => 3
        };

        var top5 = flags
            .Where(f => f.State == FlagStates.Open || f.State == FlagStates.Acknowledged)
            .OrderBy(f => SeverityWeight(f.Severity))
            .ThenByDescending(f => f.CreatedAtUtc)
            .Take(5)
            .Select(MapToDto)
            .ToList();

        return top5;
    }

    // --- Actions: Acknowledge & Dismiss ---

    public async Task<bool> AcknowledgeFlagAsync(Guid flagId, Guid orgId, CancellationToken ct = default)
    {
        var flag = await _dbContext.Flags.FirstOrDefaultAsync(f => f.Id == flagId && f.OrgId == orgId, ct);
        if (flag == null) return false;

        flag.Acknowledge(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DismissFlagAsync(Guid flagId, Guid orgId, string reason, CancellationToken ct = default)
    {
        var flag = await _dbContext.Flags.FirstOrDefaultAsync(f => f.Id == flagId && f.OrgId == orgId, ct);
        if (flag == null) return false;

        flag.Dismiss(reason, DateTime.UtcNow, expirationDays: 30);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    // --- IFlagEngine ---

    public async Task<IReadOnlyList<FlagDto>> EvaluateOrgPeriodAsync(Guid orgId, string period, Guid? siteId = null, CancellationToken ct = default)
    {
        var startTimeUtc = DateTime.UtcNow;
        var activeRules = await _dbContext.FlagRules.Where(r => r.IsActive).ToListAsync(ct);
        var results = new List<FlagDto>();
        var evalContext = new EvaluationContext(orgId, period, siteId);
        var evaluatedRules = new List<string>();

        foreach (var rule in activeRules)
        {
            evaluatedRules.Add(rule.FlagCode);
            var evaluator = _evaluators.FirstOrDefault(e => e.RuleCode.Equals(rule.FlagCode, StringComparison.OrdinalIgnoreCase));
            if (evaluator == null) continue;

            var result = await evaluator.EvaluateAsync(rule, evalContext, ct);
            if (result.ShouldFlag)
            {
                var flagDto = await RaiseOrUpdateFlagAsync(new RaiseFlagRequest(
                    OrgId: orgId,
                    SiteId: siteId,
                    RuleCode: rule.FlagCode,
                    Period: period,
                    EvidenceJson: result.EvidenceJson ?? "{}",
                    ExplanationBn: result.ExplanationBn ?? rule.NameBn,
                    ExplanationEn: result.ExplanationEn ?? rule.NameEn,
                    SeverityOverride: result.SeverityOverride
                ), ct);

                results.Add(flagDto);
            }
            else
            {
                // Condition is clear -> Auto-resolve if currently open
                await AutoResolveFlagAsync(orgId, rule.FlagCode, period, siteId, ct);
            }
        }

        if (_langSmithTracer != null)
        {
            try
            {
                await _langSmithTracer.TraceRunAsync(
                    "CarbonFlagAnomalyDetector",
                    "chain",
                    new
                    {
                        orgId,
                        period,
                        siteId,
                        activeRulesCount = activeRules.Count,
                        evaluatedRuleCodes = evaluatedRules
                    },
                    new
                    {
                        flagsRaisedCount = results.Count,
                        raisedFlags = results.Select(f => new
                        {
                            ruleCode = f.RuleCode,
                            severity = f.Severity,
                            family = f.Family,
                            explanationEn = f.ExplanationEn
                        }).ToList()
                    },
                    startTimeUtc,
                    DateTime.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["module"] = "Flags",
                        ["engine"] = "CarbonFlagAnomalyDetector",
                        ["orgId"] = orgId.ToString(),
                        ["period"] = period
                    },
                    null,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to emit LangSmith trace for Carbon Flag Anomaly Detector.");
            }
        }

        return results;
    }

    public async Task<int> CheckAndExpireDismissalsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var dismissedFlags = await _dbContext.Flags
            .Where(f => f.State == FlagStates.Dismissed && f.DismissedUntil != null && f.DismissedUntil <= now)
            .ToListAsync(ct);

        foreach (var f in dismissedFlags)
        {
            f.Reopen(now);
            _logger.LogInformation("Reopened dismissed Flag {FlagId} ({RuleCode}) as 30-day dismissal window expired",
                f.Id, f.RuleCode);
        }

        await _dbContext.SaveChangesAsync(ct);
        return dismissedFlags.Count;
    }

    private static FlagDto MapToDto(Flag f) => new(
        f.Id,
        f.OrgId,
        f.SiteId,
        f.RuleId,
        f.RuleCode,
        f.Family,
        f.Severity,
        f.Period,
        f.EvidenceJson,
        f.ExplanationBn,
        f.ExplanationEn,
        f.SuggestedActionBn,
        f.SuggestedActionEn,
        f.State,
        f.DismissedReason,
        f.DismissedUntil,
        f.CreatedAtUtc,
        f.UpdatedAtUtc
    );
}
