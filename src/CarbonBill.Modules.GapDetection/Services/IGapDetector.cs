using CarbonBill.Modules.GapDetection.Domain;
using CarbonBill.SharedKernel.Domain;

namespace CarbonBill.Modules.GapDetection.Services;

public record MissingAlertDto(
    Guid Id,
    Guid OrgId,
    Guid SiteId,
    Guid AssetId,
    string AssetName,
    string AssetType,
    string DocType,
    string Period,
    DateTime DueDate,
    Guid? ResponsibleUserId,
    string Status,
    string EscalationState,
    string Severity,
    string RequestMessageKey,
    string PlainRequestMessageBn,
    string PlainRequestMessageEn,
    DateTime? ResolvedAtUtc,
    Guid? ResolvedDocumentId,
    DateTime? LastNudgedAtUtc,
    int NudgeCount,
    DateTime CreatedAtUtc);

public interface IGapDetector
{
    Task<IReadOnlyList<MissingAlert>> CheckMissingDocumentsAsync(Guid orgId, string period, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MissingAlert>> EvaluateAllActiveOrgsAsync(string? period = null, CancellationToken cancellationToken = default);
    Task<Result<MissingAlert>> NudgeAlertAsync(Guid alertId, Guid orgId, CancellationToken cancellationToken = default);
    Task<int> ResolveAlertForDocumentAsync(Guid orgId, Guid? assetId, string? docType, string? period, Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MissingAlertDto>> GetAlertsAsync(Guid orgId, string? period = null, string? status = null, CancellationToken cancellationToken = default);
}
