namespace CarbonBill.SharedKernel.Contracts;

public record EmissionSummaryDto(
    string Period,
    decimal TotalKgCo2e,
    decimal TotalTonnesCo2e,
    decimal VerifiedKgCo2e,
    decimal EstimatedKgCo2e,
    decimal Scope1KgCo2e,
    decimal Scope2KgCo2e,
    decimal Scope3KgCo2e,
    decimal DataQualityScore,
    int RecordsCount);

public record ScopeBreakdownItem(
    int Scope,
    string ScopeName,
    string Category,
    decimal KgCo2e,
    decimal Percentage,
    decimal QuantityStandard,
    string StandardUnit);

public record MonthlyTrendItem(
    string Period,
    decimal TotalKgCo2e,
    decimal Scope1KgCo2e,
    decimal Scope2KgCo2e,
    decimal Scope3KgCo2e,
    decimal DataQualityScore);

public interface IEmissionReadModel
{
    Task<EmissionSummaryDto> GetSummaryAsync(Guid orgId, string? period = null, CancellationToken ct = default);
    Task<IReadOnlyList<ScopeBreakdownItem>> GetScopeBreakdownAsync(Guid orgId, string? period = null, CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyTrendItem>> GetMonthlyTrendAsync(Guid orgId, int months = 12, CancellationToken ct = default);
}
