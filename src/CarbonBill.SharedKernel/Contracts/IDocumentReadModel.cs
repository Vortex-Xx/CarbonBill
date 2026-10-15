namespace CarbonBill.SharedKernel.Contracts;

public record DocumentSummaryDto(
    Guid Id,
    Guid OrgId,
    Guid? SiteId,
    Guid? AssetId,
    string? DocType,
    string? BillingPeriod,
    DateTime UploadedAtUtc,
    string Status);

public interface IDocumentReadModel
{
    Task<IReadOnlyList<DocumentSummaryDto>> GetDocumentsAsync(Guid orgId, string? billingPeriod = null, CancellationToken ct = default);
    Task<bool> HasDocumentAsync(Guid orgId, Guid? assetId, string? docType, string billingPeriod, CancellationToken ct = default);
}
