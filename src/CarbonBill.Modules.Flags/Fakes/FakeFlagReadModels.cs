using System.Globalization;
using CarbonBill.Modules.Flags.Contracts;
using CarbonBill.SharedKernel.Contracts;

namespace CarbonBill.Modules.Flags.Fakes;

public class FakeFlagDocumentReadModel : IFlagDocumentReadModel
{
    private readonly List<ExtractedDocumentRecord> _documents = [];
    private readonly Dictionary<(Guid OrgId, Guid AssetId), HistoricalAssetConsumption> _history = [];

    public void AddDocument(ExtractedDocumentRecord doc) => _documents.Add(doc);

    public void SetHistory(Guid orgId, Guid assetId, HistoricalAssetConsumption consumption) =>
        _history[(orgId, assetId)] = consumption;

    public Task<IReadOnlyList<ExtractedDocumentRecord>> GetExtractedDocumentsAsync(Guid orgId, string period, CancellationToken ct = default)
    {
        var docs = _documents
            .Where(d => d.OrgId == orgId && d.BillingPeriod.Equals(period, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<ExtractedDocumentRecord>>(docs);
    }

    public Task<HistoricalAssetConsumption?> GetHistoricalConsumptionAsync(Guid orgId, Guid assetId, CancellationToken ct = default)
    {
        _history.TryGetValue((orgId, assetId), out var history);
        return Task.FromResult(history);
    }
}

public class FakeFlagEmissionReadModel : IFlagEmissionReadModel
{
    private readonly Dictionary<(Guid OrgId, string Period), PeriodEmissionSummary> _summaries = [];

    public void SetEmissionSummary(Guid orgId, string period, PeriodEmissionSummary summary) =>
        _summaries[(orgId, period)] = summary;

    public Task<PeriodEmissionSummary?> GetPeriodEmissionSummaryAsync(Guid orgId, string period, CancellationToken ct = default)
    {
        _summaries.TryGetValue((orgId, period), out var summary);
        return Task.FromResult(summary);
    }
}

public class FakeEmissionReadModel : IEmissionReadModel
{
    private readonly Dictionary<(Guid OrgId, string Period), EmissionSummaryDto> _summaries = [];
    private readonly Dictionary<(Guid OrgId, string Period), List<ScopeBreakdownItem>> _breakdowns = [];
    private readonly Dictionary<Guid, List<MonthlyTrendItem>> _trends = [];

    public void SetSummary(Guid orgId, string period, EmissionSummaryDto summary) =>
        _summaries[(orgId, period)] = summary;

    public void SetBreakdown(Guid orgId, string period, List<ScopeBreakdownItem> items) =>
        _breakdowns[(orgId, period)] = items;

    public void SetMonthlyTrend(Guid orgId, List<MonthlyTrendItem> items) =>
        _trends[orgId] = items;

    public Task<EmissionSummaryDto> GetSummaryAsync(Guid orgId, string? period = null, CancellationToken ct = default)
    {
        var p = period ?? DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        if (_summaries.TryGetValue((orgId, p), out var s))
        {
            return Task.FromResult(s);
        }

        return Task.FromResult(new EmissionSummaryDto(
            Period: p,
            TotalKgCo2e: 0m,
            TotalTonnesCo2e: 0m,
            VerifiedKgCo2e: 0m,
            EstimatedKgCo2e: 0m,
            Scope1KgCo2e: 0m,
            Scope2KgCo2e: 0m,
            Scope3KgCo2e: 0m,
            DataQualityScore: 100m,
            RecordsCount: 0));
    }

    public Task<IReadOnlyList<ScopeBreakdownItem>> GetScopeBreakdownAsync(Guid orgId, string? period = null, CancellationToken ct = default)
    {
        var p = period ?? DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        if (_breakdowns.TryGetValue((orgId, p), out var items))
        {
            return Task.FromResult<IReadOnlyList<ScopeBreakdownItem>>(items);
        }

        return Task.FromResult<IReadOnlyList<ScopeBreakdownItem>>([]);
    }

    public Task<IReadOnlyList<MonthlyTrendItem>> GetMonthlyTrendAsync(Guid orgId, int months = 12, CancellationToken ct = default)
    {
        if (_trends.TryGetValue(orgId, out var items))
        {
            return Task.FromResult<IReadOnlyList<MonthlyTrendItem>>(items.TakeLast(months).ToList());
        }

        return Task.FromResult<IReadOnlyList<MonthlyTrendItem>>([]);
    }
}

public class FakeProductionMetricReader : IProductionMetricReader
{
    private readonly List<ProductionMetricDto> _metrics = [];

    public void AddMetric(ProductionMetricDto metric) => _metrics.Add(metric);

    public Task<ProductionMetricDto?> GetMetricAsync(Guid orgId, string period, Guid? siteId = null, CancellationToken ct = default)
    {
        var m = _metrics.FirstOrDefault(x => x.OrgId == orgId && x.Period.Equals(period, StringComparison.OrdinalIgnoreCase) && (!siteId.HasValue || x.SiteId == siteId));
        return Task.FromResult(m);
    }

    public Task<IReadOnlyList<ProductionMetricDto>> GetMetricsAsync(Guid orgId, Guid? siteId = null, CancellationToken ct = default)
    {
        var list = _metrics.Where(x => x.OrgId == orgId && (!siteId.HasValue || x.SiteId == siteId)).ToList();
        return Task.FromResult<IReadOnlyList<ProductionMetricDto>>(list);
    }
}

public class FakeBenchmarkReader : IBenchmarkReader
{
    private readonly List<BenchmarkSetDto> _benchmarks = [];

    public void AddBenchmark(BenchmarkSetDto b) => _benchmarks.Add(b);

    public Task<BenchmarkSetDto?> GetBenchmarkAsync(string sector, string sizeBand, string metric, CancellationToken ct = default)
    {
        var b = _benchmarks.FirstOrDefault(x =>
            string.Equals(x.Sector, sector, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.SizeBand, sizeBand, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Metric, metric, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(b);
    }
}

public class FakeFactorRegistryReadModel : IFactorRegistryReadModel
{
    private int _year = 2026;
    private readonly List<FactorOverrideRecord> _overrides = [];

    public void SetActiveYear(int year) => _year = year;
    public void AddOverride(FactorOverrideRecord r) => _overrides.Add(r);

    public Task<int> GetActiveFactorSetYearAsync(CancellationToken ct = default) => Task.FromResult(_year);

    public Task<IReadOnlyList<FactorOverrideRecord>> GetActiveOverridesAsync(Guid orgId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FactorOverrideRecord>>(_overrides);
}

public class FakeTargetReadModel : ITargetReadModel
{
    private readonly Dictionary<(Guid OrgId, string Period), decimal> _targets = [];

    public void SetTarget(Guid orgId, string period, decimal targetKg) =>
        _targets[(orgId, period)] = targetKg;

    public Task<decimal?> GetTargetFootprintAsync(Guid orgId, string period, CancellationToken ct = default)
    {
        _targets.TryGetValue((orgId, period), out var target);
        return Task.FromResult(target > 0 ? (decimal?)target : null);
    }
}
