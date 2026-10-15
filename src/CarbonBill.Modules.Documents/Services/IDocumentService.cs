using CarbonBill.Modules.Documents.Domain;
using CarbonBill.SharedKernel.Domain;

namespace CarbonBill.Modules.Documents.Services;

public record UploadDocumentResult(
    bool Success,
    Document? Document,
    string? ErrorMessage,
    bool IsDuplicate = false);

public record DocumentReceiptDto(
    Guid Id,
    string Status,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTime CapturedAtUtc,
    bool Received,
    bool IsDuplicate = false);

public record DocumentDetailDto(
    Guid Id,
    Guid OrgId,
    Guid? SiteId,
    Guid? AssetId,
    string FileName,
    string StoragePath,
    string ContentType,
    long FileSizeBytes,
    string Sha256Hash,
    string Status,
    string Source,
    DateTime CapturedAtUtc,
    int? TierUsed,
    bool IsEstimated,
    string? DocType,
    string? PreSignedUrl,
    IReadOnlyList<DocumentPageDto> Pages);

public record DocumentPageDto(Guid Id, int PageNumber, string StoragePath);

public interface IDocumentService
{
    Task<Result<DocumentReceiptDto>> UploadDocumentAsync(
        Stream fileStream,
        string fileName,
        string claimedContentType,
        Guid uploadedByUserId,
        string? idempotencyKey = null,
        string source = "phone",
        string? docType = null,
        CancellationToken ct = default);

    Task<Result<DocumentReceiptDto>> CreateManualEntryAsync(
        ManualEntryRequest request,
        Guid uploadedByUserId,
        CancellationToken ct = default);

    Task<IReadOnlyList<DocumentReceiptDto>> GetDocumentsAsync(
        string? status = null,
        int limit = 20,
        CancellationToken ct = default);

    Task<Result<DocumentDetailDto>> GetDocumentByIdAsync(
        Guid documentId,
        CancellationToken ct = default);

    Task<Result<(Stream Stream, string ContentType, string FileName)>> GetDocumentFileAsync(
        Guid documentId,
        CancellationToken ct = default);

    Task<IReadOnlyList<DocumentReceiptDto>> GetMySubmissionsAsync(
        Guid userId,
        int limit = 20,
        CancellationToken ct = default);

    Task<Guid?> GetDefaultOrgIdAsync(CancellationToken ct = default);
}
