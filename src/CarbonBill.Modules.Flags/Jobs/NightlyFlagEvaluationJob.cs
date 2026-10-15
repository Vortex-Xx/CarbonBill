using System.Globalization;
using CarbonBill.Modules.Flags.Persistence;
using CarbonBill.Modules.Flags.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Flags.Jobs;

public interface INightlyFlagEvaluationJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

public class NightlyFlagEvaluationJob(
    FlagsDbContext dbContext,
    IFlagEngine flagEngine,
    ILogger<NightlyFlagEvaluationJob> logger) : INightlyFlagEvaluationJob
{
    private readonly FlagsDbContext _dbContext = dbContext;
    private readonly IFlagEngine _flagEngine = flagEngine;
    private readonly ILogger<NightlyFlagEvaluationJob> _logger = logger;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("[Hangfire] Starting Nightly Flag Evaluation Job at {StartTime}", startTime);

        try
        {
            // 1. Check and reopen any flags whose 30-day dismissal window has expired
            var reopenedCount = await _flagEngine.CheckAndExpireDismissalsAsync(cancellationToken);
            if (reopenedCount > 0)
            {
                _logger.LogInformation("Reopened {Count} flags with expired dismissals", reopenedCount);
            }

            // 2. Query distinct orgs that have existing flags or activity
            var orgIds = await _dbContext.Flags
                .Select(f => f.OrgId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var currentPeriod = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var previousPeriod = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture);

            int evaluatedCount = 0;
            foreach (var orgId in orgIds)
            {
                await _flagEngine.EvaluateOrgPeriodAsync(orgId, currentPeriod, null, cancellationToken);
                await _flagEngine.EvaluateOrgPeriodAsync(orgId, previousPeriod, null, cancellationToken);
                evaluatedCount++;
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("[Hangfire] Nightly Flag Evaluation Job completed in {Duration}ms. Evaluated {Count} orgs.",
                duration.TotalMilliseconds, evaluatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Hangfire] Nightly Flag Evaluation Job encountered an error");
            throw;
        }
    }
}
