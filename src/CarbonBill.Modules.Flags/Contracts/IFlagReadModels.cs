namespace CarbonBill.Modules.Flags.Contracts;

public record ExtractedFieldConfidence(
    string FieldName,
    string Value,
    float Confidence);

public record ExtractedDocumentRecord(
    Guid DocumentId,
    Guid OrgId,
    Guid? SiteId,
    Guid? AssetId,
    string? AssetName,
    string DocType,
    string BillingPeriod,
    string? FileHash,
    string? VendorName,
    string? BillNumber,
    decimal Quantity,
    string Unit,
    float MinFieldConfidence,
    IReadOnlyList<ExtractedFieldConfidence> Fields);

public record HistoricalAssetConsumption(
    Guid AssetId,
    string AssetName,
    string Unit,
    IReadOnlyList<decimal> PastQuantities);

public record PeriodEmissionSummary(
    Guid OrgId,
    Guid? SiteId,
    string Period,
    decimal TotalCo2eKg,
    decimal EstimatedCo2eKg);

public record FactorOverrideRecord(
    Guid Id,
    Guid FactorId,
    decimal OverrideValue,
    string Justification,
    Guid ApprovedByUserId,
    bool IsActive);

public interface IFlagDocumentReadModel
{
    Task<IReadOnlyList<ExtractedDocumentRecord>> GetExtractedDocumentsAsync(Guid orgId, string period, CancellationToken ct = default);
    Task<HistoricalAssetConsumption?> GetHistoricalConsumptionAsync(Guid orgId, Guid assetId, CancellationToken ct = default);
}

public interface IFlagEmissionReadModel
{
    Task<PeriodEmissionSummary?> GetPeriodEmissionSummaryAsync(Guid orgId, string period, CancellationToken ct = default);
}

public interface IFactorRegistryReadModel
{
    Task<int> GetActiveFactorSetYearAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FactorOverrideRecord>> GetActiveOverridesAsync(Guid orgId, CancellationToken ct = default);
}

public interface ITargetReadModel
{
    Task<decimal?> GetTargetFootprintAsync(Guid orgId, string period, CancellationToken ct = default);
}
