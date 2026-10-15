using CarbonBill.Modules.Insights.Domain;
using CarbonBill.Modules.Insights.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Insights.Seeds;

public interface IInsightsSeedLoader
{
    Task SeedDefaultBenchmarksAsync(CancellationToken ct = default);
}

public class InsightsSeedLoader(
    InsightsDbContext dbContext,
    ILogger<InsightsSeedLoader> logger) : IInsightsSeedLoader
{
    public async Task SeedDefaultBenchmarksAsync(CancellationToken ct = default)
    {
        if (await dbContext.BenchmarkSets.AnyAsync(ct))
        {
            return;
        }

        var defaultSets = new List<BenchmarkSet>
        {
            new()
            {
                Sector = "RMG",
                SizeBand = "Medium",
                Metric = "kg_co2e_per_piece",
                P25 = 0.310000m,
                P50 = 0.415000m,
                P75 = 0.520000m,
                P90 = 0.650000m,
                N = 32,
                Source = "IFC PaCT Bangladesh Textile Benchmark Study 2022",
                Year = 2022,
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Sector = "RMG",
                SizeBand = "Large",
                Metric = "kg_co2e_per_piece",
                P25 = 0.280000m,
                P50 = 0.380000m,
                P75 = 0.490000m,
                P90 = 0.610000m,
                N = 24,
                Source = "IFC PaCT Bangladesh Textile Benchmark Study 2022",
                Year = 2022,
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Sector = "RMG",
                SizeBand = "Small",
                Metric = "kg_co2e_per_piece",
                P25 = 0.350000m,
                P50 = 0.460000m,
                P75 = 0.580000m,
                P90 = 0.720000m,
                N = 18,
                Source = "IFC PaCT Bangladesh Textile Benchmark Study 2022",
                Year = 2022,
                CreatedAtUtc = DateTime.UtcNow
            },
            new()
            {
                Sector = "TextileDyeing",
                SizeBand = "Small",
                Metric = "kg_co2e_per_kg_fabric",
                P25 = 1.250000m,
                P50 = 1.850000m,
                P75 = 2.450000m,
                P90 = 3.100000m,
                N = 6, // Insufficient sample (N < 10) for testing benchmark honesty gating
                Source = "SREDA Pilot Preliminary Survey 2023",
                Year = 2023,
                CreatedAtUtc = DateTime.UtcNow
            }
        };

        dbContext.BenchmarkSets.AddRange(defaultSets);
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} benchmark sets into database.", defaultSets.Count);
    }
}
