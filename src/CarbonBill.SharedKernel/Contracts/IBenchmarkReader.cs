namespace CarbonBill.SharedKernel.Contracts;

public record BenchmarkSetDto(
    Guid Id,
    string Sector,
    string SizeBand,
    string Metric,
    decimal P25,
    decimal P50,
    decimal P75,
    decimal P90,
    int N,
    string Source,
    int Year);

public interface IBenchmarkReader
{
    Task<BenchmarkSetDto?> GetBenchmarkAsync(string sector, string sizeBand, string metric, CancellationToken ct = default);
}
