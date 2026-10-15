namespace CarbonBill.SharedKernel.Providers;

public record BoundingBox(float Left, float Top, float Width, float Height);

public record ExtractedFieldResult(
    string FieldName,
    string RawValue,
    string? NormalizedValue,
    float Confidence,
    int SourceTier,
    BoundingBox? BoundingBox = null);

public record OcrExtractionResult(
    bool Success,
    string? DetectedDocumentType,
    IReadOnlyList<ExtractedFieldResult> Fields,
    string? ErrorMessage,
    int TierUsed);

public interface IOcrProvider
{
    int Tier { get; }
    string ProviderName { get; }
    Task<OcrExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}

public interface IFileStore
{
    Task<string> UploadAsync(
        Stream contentStream,
        string destinationPath,
        string contentType,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(
        string storagePath,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken = default);

    Task<string> GetPreSignedUrlAsync(
        string storagePath,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string storagePath,
        CancellationToken cancellationToken = default);
}

public record EmailNotification(
    string RecipientEmail,
    string Subject,
    string BodyHtml,
    string? BodyPlainText = null);

public record PushNotification(
    string Endpoint,
    string P256DhKey,
    string AuthKey,
    string Title,
    string Body,
    string? ClickUrl = null);

public interface INotifier
{
    Task<bool> SendEmailAsync(EmailNotification notification, CancellationToken cancellationToken = default);
    Task<bool> SendPushAsync(PushNotification notification, CancellationToken cancellationToken = default);
}

public record ReportRenderRequest(
    Guid ReportId,
    string TemplateName,
    string Language, // "bn" or "en"
    object ModelData);

public interface IReportRenderer
{
    string OutputFormat { get; } // "pdf" or "xlsx"
    Task<byte[]> RenderAsync(ReportRenderRequest request, CancellationToken cancellationToken = default);
}

public interface ILangSmithTracer
{
    bool IsEnabled { get; }
    Task<string?> TraceRunAsync(
        string name,
        string runType,
        object inputs,
        object? outputs,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        Dictionary<string, object>? metadata = null,
        string? errorMessage = null,
        CancellationToken ct = default);
}

