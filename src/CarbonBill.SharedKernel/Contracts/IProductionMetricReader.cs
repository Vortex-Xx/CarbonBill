namespace CarbonBill.SharedKernel.Contracts;

public record ProductionMetricDto(
    Guid Id,
    Guid OrgId,
    Guid? SiteId,
    string Period,
    string Unit,
    decimal Quantity,
    DateTime CreatedAtUtc);

public interface IProductionMetricReader
{
    Task<ProductionMetricDto?> GetMetricAsync(Guid orgId, string period, Guid? siteId = null, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionMetricDto>> GetMetricsAsync(Guid orgId, Guid? siteId = null, CancellationToken ct = default);
}
