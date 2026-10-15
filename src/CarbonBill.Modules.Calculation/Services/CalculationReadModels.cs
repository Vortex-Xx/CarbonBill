using CarbonBill.Modules.Calculation.Persistence;
using CarbonBill.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Calculation.Services;

public class EmissionReadModel(CalculationDbContext dbContext) : IEmissionReadModel
{
    public async Task<EmissionSummaryDto> GetSummaryAsync(Guid orgId, string? period = null, CancellationToken ct = default)
    {
        var query = dbContext.EmissionResults
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.OrgId == orgId);

        if (!string.IsNullOrWhiteSpace(period))
        {
            query = query.Where(e => e.Period == period);
        }

        var results = await query.ToListAsync(ct);

        if (results.Count == 0)
        {
            return new EmissionSummaryDto(
                Period: period ?? DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
                TotalKgCo2e: 0m,
                TotalTonnesCo2e: 0m,
                VerifiedKgCo2e: 0m,
                EstimatedKgCo2e: 0m,
                Scope1KgCo2e: 0m,
                Scope2KgCo2e: 0m,
                Scope3KgCo2e: 0m,
                DataQualityScore: 100m,
                RecordsCount: 0);
        }

        var totalKg = results.Sum(e => e.KgCo2e);
        var verifiedKg = results.Where(e => !e.IsEstimated).Sum(e => e.KgCo2e);
        var estimatedKg = results.Where(e => e.IsEstimated).Sum(e => e.KgCo2e);

        var scope1 = results.Where(e => e.Scope == 1).Sum(e => e.KgCo2e);
        var scope2 = results.Where(e => e.Scope == 2).Sum(e => e.KgCo2e);
        var scope3 = results.Where(e => e.Scope == 3).Sum(e => e.KgCo2e);

        // Data Quality Score formula: 100 * (Verified / Total)
        var dqScore = totalKg > 0
            ? Math.Round(100m * (verifiedKg / totalKg), 2)
            : 100m;

        return new EmissionSummaryDto(
            Period: period ?? results.Max(e => e.Period) ?? DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            TotalKgCo2e: totalKg,
            TotalTonnesCo2e: Math.Round(totalKg / 1000m, 3),
            VerifiedKgCo2e: verifiedKg,
            EstimatedKgCo2e: estimatedKg,
            Scope1KgCo2e: scope1,
            Scope2KgCo2e: scope2,
            Scope3KgCo2e: scope3,
            DataQualityScore: dqScore,
            RecordsCount: results.Count);
    }

    public async Task<IReadOnlyList<ScopeBreakdownItem>> GetScopeBreakdownAsync(Guid orgId, string? period = null, CancellationToken ct = default)
    {
        var query = dbContext.EmissionResults
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.OrgId == orgId);

        if (!string.IsNullOrWhiteSpace(period))
        {
            query = query.Where(e => e.Period == period);
        }

        var results = await query.ToListAsync(ct);
        var totalKg = results.Sum(e => e.KgCo2e);

        var groups = results
            .GroupBy(e => new { e.Scope, e.Category, e.StandardUnit })
            .Select(g =>
            {
                var catKg = g.Sum(x => x.KgCo2e);
                var pct = totalKg > 0 ? Math.Round(100m * (catKg / totalKg), 2) : 0m;
                var scopeName = g.Key.Scope switch
                {
                    1 => "Scope 1 (Direct Fuel & Combustion)",
                    2 => "Scope 2 (Purchased Electricity)",
                    3 => "Scope 3 (Upstream Transport)",
                    _ => $"Scope {g.Key.Scope}"
                };

                return new ScopeBreakdownItem(
                    Scope: g.Key.Scope,
                    ScopeName: scopeName,
                    Category: g.Key.Category,
                    KgCo2e: catKg,
                    Percentage: pct,
                    QuantityStandard: g.Sum(x => x.QuantityStandard),
                    StandardUnit: g.Key.StandardUnit);
            })
            .OrderBy(b => b.Scope)
            .ThenByDescending(b => b.KgCo2e)
            .ToList();

        return groups;
    }

    public async Task<IReadOnlyList<MonthlyTrendItem>> GetMonthlyTrendAsync(Guid orgId, int months = 12, CancellationToken ct = default)
    {
        var results = await dbContext.EmissionResults
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.OrgId == orgId)
            .ToListAsync(ct);

        var grouped = results
            .GroupBy(e => e.Period)
            .OrderBy(g => g.Key)
            .TakeLast(months)
            .Select(g =>
            {
                var total = g.Sum(x => x.KgCo2e);
                var verified = g.Where(x => !x.IsEstimated).Sum(x => x.KgCo2e);
                var dqScore = total > 0 ? Math.Round(100m * (verified / total), 2) : 100m;

                return new MonthlyTrendItem(
                    Period: g.Key,
                    TotalKgCo2e: total,
                    Scope1KgCo2e: g.Where(x => x.Scope == 1).Sum(x => x.KgCo2e),
                    Scope2KgCo2e: g.Where(x => x.Scope == 2).Sum(x => x.KgCo2e),
                    Scope3KgCo2e: g.Where(x => x.Scope == 3).Sum(x => x.KgCo2e),
                    DataQualityScore: dqScore);
            })
            .ToList();

        return grouped;
    }
}
