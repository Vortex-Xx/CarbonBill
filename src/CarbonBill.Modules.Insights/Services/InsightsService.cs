using System.Globalization;
using CarbonBill.Modules.Insights.Domain;
using CarbonBill.Modules.Insights.Persistence;
using CarbonBill.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Insights.Services;

public record PeerBenchmarkResult(
    bool HasBenchmark,
    string? Message,
    string? MessageBn,
    decimal? P25,
    decimal? P50,
    decimal? P75,
    decimal? P90,
    int N,
    string? Source,
    int? Year);

public record MonthlyIntensityTrendItem(
    string Period,
    decimal TotalIntensity,
    decimal VerifiedIntensity,
    decimal TotalKgCo2e,
    decimal VerifiedKgCo2e,
    decimal ProductionQuantity,
    decimal DataQualityScore);

public record IntensityDashboardResult(
    string Metric,
    string Unit,
    decimal FactoryValue,
    decimal VerifiedFactoryValue,
    decimal TotalKgCo2e,
    decimal VerifiedKgCo2e,
    decimal ProductionQuantity,
    PeerBenchmarkResult PeerBenchmark,
    IReadOnlyList<MonthlyIntensityTrendItem> Trends);

public interface IInsightsService : IProductionMetricReader, IBenchmarkReader
{
    Task<ProductionMetricDto> CaptureMetricAsync(
        Guid orgId,
        Guid? siteId,
        string period,
        string unit,
        decimal quantity,
        CancellationToken ct = default);

    Task<IntensityDashboardResult> GetIntensityDashboardAsync(
        Guid orgId,
        string? period = null,
        string sector = "RMG",
        string sizeBand = "Medium",
        int minPeerCount = 10,
        CancellationToken ct = default);
}

public class InsightsService(
    InsightsDbContext dbContext,
    IEmissionReadModel emissionReadModel,
    ILogger<InsightsService> logger) : IInsightsService
{
    private readonly InsightsDbContext _dbContext = dbContext;
    private readonly IEmissionReadModel _emissionReadModel = emissionReadModel;
    private readonly ILogger<InsightsService> _logger = logger;

    public async Task<ProductionMetricDto?> GetMetricAsync(Guid orgId, string period, Guid? siteId = null, CancellationToken ct = default)
    {
        var query = _dbContext.ProductionMetrics
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.OrgId == orgId && m.Period == period);

        if (siteId.HasValue)
        {
            query = query.Where(m => m.SiteId == siteId.Value);
        }

        var metric = await query.OrderByDescending(m => m.CreatedAtUtc).FirstOrDefaultAsync(ct);
        return metric == null ? null : MapToDto(metric);
    }

    public async Task<IReadOnlyList<ProductionMetricDto>> GetMetricsAsync(Guid orgId, Guid? siteId = null, CancellationToken ct = default)
    {
        var query = _dbContext.ProductionMetrics
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.OrgId == orgId);

        if (siteId.HasValue)
        {
            query = query.Where(m => m.SiteId == siteId.Value);
        }

        var list = await query.OrderByDescending(m => m.Period).ToListAsync(ct);
        return list.Select(MapToDto).ToList();
    }

    public async Task<BenchmarkSetDto?> GetBenchmarkAsync(string sector, string sizeBand, string metric, CancellationToken ct = default)
    {
        var allBenchmarks = await _dbContext.BenchmarkSets
            .AsNoTracking()
            .ToListAsync(ct);

        var benchmark = allBenchmarks.FirstOrDefault(b =>
            string.Equals(b.Sector, sector, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(b.SizeBand, sizeBand, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(b.Metric, metric, StringComparison.OrdinalIgnoreCase));

        return benchmark == null ? null : new BenchmarkSetDto(
            benchmark.Id,
            benchmark.Sector,
            benchmark.SizeBand,
            benchmark.Metric,
            benchmark.P25,
            benchmark.P50,
            benchmark.P75,
            benchmark.P90,
            benchmark.N,
            benchmark.Source,
            benchmark.Year);
    }

    public async Task<ProductionMetricDto> CaptureMetricAsync(
        Guid orgId,
        Guid? siteId,
        string period,
        string unit,
        decimal quantity,
        CancellationToken ct = default)
    {
        var existing = await _dbContext.ProductionMetrics
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.OrgId == orgId && m.SiteId == siteId && m.Period == period && m.Unit == unit, ct);

        if (existing != null)
        {
            existing.Quantity = quantity;
            existing.MarkUpdated();
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Updated production metric {Id} for org {OrgId}, period {Period}: {Quantity} {Unit}",
                existing.Id, orgId, period, quantity, unit);
            return MapToDto(existing);
        }

        var newMetric = new ProductionMetric
        {
            OrgId = orgId,
            SiteId = siteId,
            Period = period,
            Unit = unit,
            Quantity = quantity
        };

        _dbContext.ProductionMetrics.Add(newMetric);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Captured new production metric {Id} for org {OrgId}, period {Period}: {Quantity} {Unit}",
            newMetric.Id, orgId, period, quantity, unit);

        return MapToDto(newMetric);
    }

    public async Task<IntensityDashboardResult> GetIntensityDashboardAsync(
        Guid orgId,
        string? period = null,
        string sector = "RMG",
        string sizeBand = "Medium",
        int minPeerCount = 10,
        CancellationToken ct = default)
    {
        // 1. Get emission summary for current period
        var emissionSummary = await _emissionReadModel.GetSummaryAsync(orgId, period, ct);
        var activePeriod = string.IsNullOrWhiteSpace(period) ? emissionSummary.Period : period;

        // 2. Fetch production metric for this period
        var productionMetrics = await _dbContext.ProductionMetrics
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.OrgId == orgId)
            .ToListAsync(ct);

        var currentMetric = productionMetrics
            .Where(m => string.Equals(m.Period, activePeriod, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.CreatedAtUtc)
            .FirstOrDefault()
            ?? productionMetrics.OrderByDescending(m => m.Period).FirstOrDefault();

        var unit = currentMetric?.Unit ?? "piece";
        var quantity = currentMetric?.Quantity ?? 0m;
        var metricCode = $"kg_co2e_per_{unit}";

        // 3. Compute intensities (both verified-only and including-estimate variants)
        decimal totalKg = emissionSummary.TotalKgCo2e;
        decimal verifiedKg = emissionSummary.VerifiedKgCo2e;
        decimal factoryValue = quantity > 0 ? Math.Round(totalKg / quantity, 6) : 0m;
        decimal verifiedFactoryValue = quantity > 0 ? Math.Round(verifiedKg / quantity, 6) : 0m;

        // 4. Retrieve peer benchmark and enforce honest benchmark gating
        var benchmark = await GetBenchmarkAsync(sector, sizeBand, metricCode, ct)
            ?? await GetBenchmarkAsync(sector, sizeBand, "kg_co2e_per_piece", ct);

        PeerBenchmarkResult peerBenchmark;
        if (benchmark == null || benchmark.N < minPeerCount)
        {
            // Honest gating: insufficient sample size (< minPeerCount)
            peerBenchmark = new PeerBenchmarkResult(
                HasBenchmark: false,
                Message: "no benchmark yet",
                MessageBn: "প্রতুল তথ্য এখনও পাওয়া যায়নি",
                P25: null,
                P50: null,
                P75: null,
                P90: null,
                N: benchmark?.N ?? 0,
                Source: benchmark?.Source,
                Year: benchmark?.Year);
        }
        else
        {
            peerBenchmark = new PeerBenchmarkResult(
                HasBenchmark: true,
                Message: null,
                MessageBn: null,
                P25: benchmark.P25,
                P50: benchmark.P50,
                P75: benchmark.P75,
                P90: benchmark.P90,
                N: benchmark.N,
                Source: benchmark.Source,
                Year: benchmark.Year);
        }

        // 5. Build historical monthly trends
        var emissionTrends = await _emissionReadModel.GetMonthlyTrendAsync(orgId, 12, ct);
        var trends = new List<MonthlyIntensityTrendItem>();

        foreach (var trend in emissionTrends)
        {
            var pMetric = productionMetrics.FirstOrDefault(m => string.Equals(m.Period, trend.Period, StringComparison.OrdinalIgnoreCase));
            var pQty = pMetric?.Quantity ?? 0m;
            var tVerifiedKg = trend.TotalKgCo2e * (trend.DataQualityScore / 100m);

            var tTotalIntensity = pQty > 0 ? Math.Round(trend.TotalKgCo2e / pQty, 6) : 0m;
            var tVerifiedIntensity = pQty > 0 ? Math.Round(tVerifiedKg / pQty, 6) : 0m;

            trends.Add(new MonthlyIntensityTrendItem(
                Period: trend.Period,
                TotalIntensity: tTotalIntensity,
                VerifiedIntensity: tVerifiedIntensity,
                TotalKgCo2e: trend.TotalKgCo2e,
                VerifiedKgCo2e: tVerifiedKg,
                ProductionQuantity: pQty,
                DataQualityScore: trend.DataQualityScore));
        }

        return new IntensityDashboardResult(
            Metric: metricCode,
            Unit: unit,
            FactoryValue: factoryValue,
            VerifiedFactoryValue: verifiedFactoryValue,
            TotalKgCo2e: totalKg,
            VerifiedKgCo2e: verifiedKg,
            ProductionQuantity: quantity,
            PeerBenchmark: peerBenchmark,
            Trends: trends);
    }

    private static ProductionMetricDto MapToDto(ProductionMetric m) =>
        new(m.Id, m.OrgId, m.SiteId, m.Period, m.Unit, m.Quantity, m.CreatedAtUtc);
}
