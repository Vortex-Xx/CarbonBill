using CarbonBill.Modules.GapDetection.Domain;
using CarbonBill.Modules.GapDetection.Persistence;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.GapDetection.Services;

public class GapDetectionService(
    GapDetectionDbContext dbContext,
    IExpectedDocRuleReader ruleReader,
    IDocumentReadModel documentReadModel,
    IDomainEventPublisher eventPublisher,
    TimeProvider timeProvider,
    ILogger<GapDetectionService> logger,
    CarbonBill.SharedKernel.Providers.ILangSmithTracer? langSmithTracer = null) : IGapDetector
{
    public async Task<IReadOnlyList<MissingAlert>> CheckMissingDocumentsAsync(
        Guid orgId,
        string period,
        CancellationToken cancellationToken = default)
    {
        var startTimeUtc = DateTime.UtcNow;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rules = await ruleReader.GetRulesForOrgAsync(orgId, cancellationToken);
        var createdOrUpdatedAlerts = new List<MissingAlert>();

        logger.LogInformation("Evaluating gaps for Org {OrgId}, Period {Period} against {RuleCount} expected rules",
            orgId, period, rules.Count);

        foreach (var rule in rules)
        {
            var dueDate = CalculateDueDate(period, rule.DueDayOfMonth);
            var hasDoc = await documentReadModel.HasDocumentAsync(orgId, rule.AssetId, rule.DocType, period, cancellationToken);

            var existingAlert = await dbContext.MissingAlerts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a =>
                    a.OrgId == orgId &&
                    a.AssetId == rule.AssetId &&
                    a.DocType == rule.DocType &&
                    a.Period == period, cancellationToken);

            if (hasDoc)
            {
                // Document is present
                if (existingAlert != null && existingAlert.Status != AlertStatuses.Resolved)
                {
                    logger.LogInformation("Resolving gap alert {AlertId} for Asset {AssetName} as document was received",
                        existingAlert.Id, rule.AssetName);

                    existingAlert.Resolve(Guid.Empty, now);
                    await eventPublisher.PublishAsync(new MissingAlertResolvedEvent(
                        existingAlert.Id,
                        existingAlert.OrgId,
                        existingAlert.AssetId,
                        existingAlert.Period,
                        Guid.Empty,
                        now), cancellationToken);

                    createdOrUpdatedAlerts.Add(existingAlert);
                }
            }
            else
            {
                // Document is missing
                if (existingAlert == null)
                {
                    var (bn, en, key) = MissingAlert.FormatRequestMessage(rule.DocType, rule.AssetName);
                    var newAlert = new MissingAlert
                    {
                        OrgId = orgId,
                        SiteId = rule.SiteId ?? Guid.Empty,
                        AssetId = rule.AssetId,
                        AssetName = rule.AssetName,
                        AssetType = rule.AssetType,
                        DocType = rule.DocType,
                        Period = period,
                        DueDate = dueDate,
                        ResponsibleUserId = rule.ResponsibleUserId,
                        RequestMessageKey = key,
                        PlainRequestMessageBn = bn,
                        PlainRequestMessageEn = en,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };

                    newAlert.EvaluateEscalation(now);
                    dbContext.MissingAlerts.Add(newAlert);
                    createdOrUpdatedAlerts.Add(newAlert);

                    logger.LogInformation("Raised missing alert for Asset {AssetName} ({DocType}) for period {Period}, Severity: {Severity}, State: {State}",
                        rule.AssetName, rule.DocType, period, newAlert.Severity, newAlert.EscalationState);

                    await eventPublisher.PublishAsync(new MissingAlertRaisedEvent(
                        newAlert.Id,
                        newAlert.OrgId,
                        newAlert.SiteId,
                        newAlert.AssetId,
                        newAlert.AssetName,
                        newAlert.DocType,
                        newAlert.Period,
                        newAlert.DueDate,
                        newAlert.EscalationState,
                        newAlert.Severity,
                        newAlert.ResponsibleUserId,
                        now), cancellationToken);
                }
                else if (existingAlert.Status != AlertStatuses.Resolved)
                {
                    var previousState = existingAlert.EscalationState;
                    var previousSeverity = existingAlert.Severity;

                    existingAlert.EvaluateEscalation(now);

                    if (previousState != existingAlert.EscalationState || previousSeverity != existingAlert.Severity)
                    {
                        logger.LogInformation("Updated escalation for alert {AlertId}: {OldState}/{OldSev} -> {NewState}/{NewSev}",
                            existingAlert.Id, previousState, previousSeverity, existingAlert.EscalationState, existingAlert.Severity);
                    }

                    createdOrUpdatedAlerts.Add(existingAlert);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (langSmithTracer != null)
        {
            try
            {
                await langSmithTracer.TraceRunAsync(
                    "MissingDocumentGapAnalyzer",
                    "chain",
                    new
                    {
                        orgId,
                        period,
                        expectedRulesCount = rules.Count,
                        assetsChecked = rules.Select(r => new { r.AssetName, r.DocType }).ToList()
                    },
                    new
                    {
                        alertsCreatedOrUpdated = createdOrUpdatedAlerts.Count,
                        missingAlerts = createdOrUpdatedAlerts.Select(a => new
                        {
                            a.AssetName,
                            a.DocType,
                            a.Period,
                            a.Severity,
                            a.EscalationState,
                            a.Status
                        }).ToList()
                    },
                    startTimeUtc,
                    DateTime.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["module"] = "GapDetection",
                        ["engine"] = "MissingDocumentGapAnalyzer",
                        ["orgId"] = orgId.ToString(),
                        ["period"] = period
                    },
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to emit LangSmith trace for Gap Detection.");
            }
        }

        return createdOrUpdatedAlerts;
    }

    public async Task<IReadOnlyList<MissingAlert>> EvaluateAllActiveOrgsAsync(
        string? period = null,
        CancellationToken cancellationToken = default)
    {
        var targetPeriod = !string.IsNullOrWhiteSpace(period) 
            ? period 
            : timeProvider.GetUtcNow().UtcDateTime.AddMonths(-1).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

        var allRules = await ruleReader.GetAllActiveRulesAsync(cancellationToken);
        var orgIds = allRules.Select(r => r.OrgId).Distinct().ToList();

        logger.LogInformation("Running nightly gap evaluation for {OrgCount} organizations in period {Period}",
            orgIds.Count, targetPeriod);

        var results = new List<MissingAlert>();
        foreach (var orgId in orgIds)
        {
            var alerts = await CheckMissingDocumentsAsync(orgId, targetPeriod, cancellationToken);
            results.AddRange(alerts);
        }

        return results;
    }

    public async Task<Result<MissingAlert>> NudgeAlertAsync(
        Guid alertId,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var alert = await dbContext.MissingAlerts
            .FirstOrDefaultAsync(a => a.Id == alertId && a.OrgId == orgId, cancellationToken);

        if (alert == null)
        {
            return Result.Failure<MissingAlert>("Alert not found or cross-tenant access denied.");
        }

        if (alert.Status == AlertStatuses.Resolved)
        {
            return Result.Failure<MissingAlert>("Cannot nudge a resolved alert.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        alert.Nudge(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Nudged alert {AlertId} for Asset {AssetName} (Nudge Count: {NudgeCount})",
            alert.Id, alert.AssetName, alert.NudgeCount);

        return Result.Success(alert);
    }

    public async Task<int> ResolveAlertForDocumentAsync(
        Guid orgId,
        Guid? assetId,
        string? docType,
        string? period,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var query = dbContext.MissingAlerts
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && a.Status != AlertStatuses.Resolved);

        if (assetId.HasValue && assetId.Value != Guid.Empty)
        {
            query = query.Where(a => a.AssetId == assetId.Value);
        }

        if (!string.IsNullOrWhiteSpace(docType))
        {
            query = query.Where(a => a.DocType == docType);
        }

        if (!string.IsNullOrWhiteSpace(period))
        {
            query = query.Where(a => a.Period == period);
        }

        var matchingAlerts = await query.ToListAsync(cancellationToken);

        foreach (var alert in matchingAlerts)
        {
            alert.Resolve(documentId, now);

            logger.LogInformation("Auto-resolved gap alert {AlertId} with Document {DocumentId}",
                alert.Id, documentId);

            await eventPublisher.PublishAsync(new MissingAlertResolvedEvent(
                alert.Id,
                alert.OrgId,
                alert.AssetId,
                alert.Period,
                documentId,
                now), cancellationToken);
        }

        if (matchingAlerts.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return matchingAlerts.Count;
    }

    public async Task<IReadOnlyList<MissingAlertDto>> GetAlertsAsync(
        Guid orgId,
        string? period = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.MissingAlerts.AsNoTracking().Where(a => a.OrgId == orgId);

        if (!string.IsNullOrWhiteSpace(period))
        {
            query = query.Where(a => a.Period == period);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        return await query
            .OrderByDescending(a => a.Severity == AlertSeverities.Red)
            .ThenBy(a => a.DueDate)
            .Select(a => new MissingAlertDto(
                a.Id,
                a.OrgId,
                a.SiteId,
                a.AssetId,
                a.AssetName,
                a.AssetType,
                a.DocType,
                a.Period,
                a.DueDate,
                a.ResponsibleUserId,
                a.Status,
                a.EscalationState,
                a.Severity,
                a.RequestMessageKey,
                a.PlainRequestMessageBn,
                a.PlainRequestMessageEn,
                a.ResolvedAtUtc,
                a.ResolvedDocumentId,
                a.LastNudgedAtUtc,
                a.NudgeCount,
                a.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public static DateTime CalculateDueDate(string period, int dueDayOfMonth)
    {
        // Period format: "YYYY-MM" (e.g. "2026-09")
        // The due date for month M is day `dueDayOfMonth` of month M+1
        if (DateTime.TryParseExact(period, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsedMonth))
        {
            var nextMonth = parsedMonth.AddMonths(1);
            var clampedDay = Math.Min(dueDayOfMonth, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
            return new DateTime(nextMonth.Year, nextMonth.Month, clampedDay, 23, 59, 59, DateTimeKind.Utc);
        }

        // Fallback: 10 days from now
        return DateTime.UtcNow.Date.AddDays(dueDayOfMonth > 0 ? dueDayOfMonth : 10);
    }
}
