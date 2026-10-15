using CarbonBill.Modules.GapDetection.Services;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.GapDetection.Jobs;

public interface INightlyGapDetectionJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

public class NightlyGapDetectionJob(
    IGapDetector gapDetector,
    ILogger<NightlyGapDetectionJob> logger) : INightlyGapDetectionJob
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        logger.LogInformation("[Hangfire] Starting nightly gap detection job at {StartTime}", startTime);

        try
        {
            var alerts = await gapDetector.EvaluateAllActiveOrgsAsync(cancellationToken: cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            logger.LogInformation("[Hangfire] Nightly gap detection job completed in {Duration}ms. Evaluated/Updated {Count} alerts",
                duration.TotalMilliseconds, alerts.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Hangfire] Nightly gap detection job failed");
            throw;
        }
    }
}
