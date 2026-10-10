using System.Text;
using Microsoft.Extensions.Logging;

namespace CarbonBill.SharedKernel.Providers.Stubs;

public class FakeTesseractOcrProvider(ILogger<FakeTesseractOcrProvider> logger) : IOcrProvider
{
    public int Tier => 1;
    public string ProviderName => "FakeTesseractOcr (Local)";

    public Task<OcrExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("FakeTesseractOcrProvider processing file: {FileName} ({ContentType})", fileName, contentType);

        var fakeFields = new List<ExtractedFieldResult>
        {
            new("Vendor", "DESCO", "DESCO", 0.98f, Tier, new BoundingBox(10, 10, 100, 30)),
            new("BillNumber", "2026-DESCO-9988", "2026-DESCO-9988", 0.95f, Tier, new BoundingBox(120, 10, 150, 30)),
            new("BillingPeriod", "2026-09", "2026-09", 0.92f, Tier, new BoundingBox(10, 50, 80, 20)),
            new("ConsumptionKwh", "4520.50", "4520.50", 0.94f, Tier, new BoundingBox(200, 100, 90, 25)),
            new("AmountBdt", "38424.25", "38424.25", 0.96f, Tier, new BoundingBox(200, 140, 90, 25))
        };

        return Task.FromResult(new OcrExtractionResult(
            Success: true,
            DetectedDocumentType: "ElectricityBill",
            Fields: fakeFields,
            ErrorMessage: null,
            TierUsed: Tier));
    }
}

public class FakeAzureDocumentIntelligenceProvider(ILogger<FakeAzureDocumentIntelligenceProvider> logger) : IOcrProvider
{
    public int Tier => 2;
    public string ProviderName => "FakeAzureDocumentIntelligence (F0)";

    public Task<OcrExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("FakeAzureDocumentIntelligenceProvider processing file: {FileName}", fileName);
        return Task.FromResult(new OcrExtractionResult(
            Success: true,
            DetectedDocumentType: "FuelSlip",
            Fields:
            [
                new("Vendor", "Padma Oil Company", "Padma Oil Company", 0.97f, Tier),
                new("QuantityLiters", "250.00", "250.00", 0.96f, Tier),
                new("AmountBdt", "27500.00", "27500.00", 0.98f, Tier)
            ],
            ErrorMessage: null,
            TierUsed: Tier));
    }
}

public class FakeGeminiVisionOcrProvider(ILogger<FakeGeminiVisionOcrProvider> logger) : IOcrProvider
{
    public int Tier => 3;
    public string ProviderName => "FakeGeminiVision (Flash LLM)";

    public Task<OcrExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("FakeGeminiVisionOcrProvider processing file: {FileName}", fileName);
        return Task.FromResult(new OcrExtractionResult(
            Success: true,
            DetectedDocumentType: "HandwrittenChallan",
            Fields:
            [
                new("ChallanNumber", "CH-4412", "CH-4412", 0.91f, Tier),
                new("FuelType", "Diesel", "Diesel", 0.89f, Tier),
                new("VolumeLiters", "120", "120", 0.88f, Tier)
            ],
            ErrorMessage: null,
            TierUsed: Tier));
    }
}

public class LocalOrR2FileStoreStub(ILogger<LocalOrR2FileStoreStub> logger) : IFileStore
{
    private readonly Dictionary<string, byte[]> _memoryStorage = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _storageDir = Path.Combine(AppContext.BaseDirectory, "filestore");

    private string GetFullPath(string storagePath)
    {
        var sanitized = storagePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        return Path.Combine(_storageDir, sanitized);
    }

    public async Task<string> UploadAsync(
        Stream contentStream,
        string destinationPath,
        string contentType,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await contentStream.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();
        _memoryStorage[destinationPath] = bytes;

        try
        {
            var fullPath = GetFullPath(destinationPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not persist file to disk storage: {Path}", destinationPath);
        }

        logger.LogInformation("Stored file at {DestinationPath} ({BytesCount} bytes, {ContentType})", destinationPath, bytes.Length, contentType);
        return destinationPath;
    }

    public Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (_memoryStorage.TryGetValue(storagePath, out var bytes))
        {
            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }

        var fullPath = GetFullPath(storagePath);
        if (File.Exists(fullPath))
        {
            try
            {
                var diskBytes = File.ReadAllBytes(fullPath);
                _memoryStorage[storagePath] = diskBytes;
                return Task.FromResult<Stream>(new MemoryStream(diskBytes));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read file from disk: {Path}", fullPath);
            }
        }

        throw new FileNotFoundException($"File not found in storage: {storagePath}");
    }

    public Task<bool> DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var removed = _memoryStorage.Remove(storagePath);
        var fullPath = GetFullPath(storagePath);
        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                removed = true;
            }
            catch { }
        }
        return Task.FromResult(removed);
    }

    public Task<string> GetPreSignedUrlAsync(string storagePath, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"/api/v1/documents/download?path={Uri.EscapeDataString(storagePath)}&expires={DateTime.UtcNow.Add(expiry):O}");
    }

    public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (_memoryStorage.ContainsKey(storagePath)) return Task.FromResult(true);
        var fullPath = GetFullPath(storagePath);
        return Task.FromResult(File.Exists(fullPath));
    }
}

public class LoggingNotifierStub(ILogger<LoggingNotifierStub> logger) : INotifier
{
    public Task<bool> SendEmailAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[Email Stub] To: {Recipient}, Subject: {Subject}", notification.RecipientEmail, notification.Subject);
        return Task.FromResult(true);
    }

    public Task<bool> SendPushAsync(PushNotification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[WebPush Stub] Title: {Title}, Body: {Body}, Endpoint: {Endpoint}", notification.Title, notification.Body, notification.Endpoint);
        return Task.FromResult(true);
    }
}

public class FakeQuestPdfReportRenderer(ILogger<FakeQuestPdfReportRenderer> logger) : IReportRenderer
{
    public string OutputFormat => "pdf";

    public Task<byte[]> RenderAsync(ReportRenderRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Rendering fake PDF report: {ReportId} in language: {Lang}", request.ReportId, request.Language);
        var dummyPdf = Encoding.UTF8.GetBytes($"%PDF-1.4\n% CarbonBill GHG Report {request.ReportId} ({request.Language})\n%%EOF");
        return Task.FromResult(dummyPdf);
    }
}

public class FakeClosedXmlReportRenderer(ILogger<FakeClosedXmlReportRenderer> logger) : IReportRenderer
{
    public string OutputFormat => "xlsx";

    public Task<byte[]> RenderAsync(ReportRenderRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Rendering fake Excel export: {ReportId}", request.ReportId);
        var dummyXlsx = Encoding.UTF8.GetBytes($"PK\x03\x04 - CarbonBill GHG Excel Sheet {request.ReportId}");
        return Task.FromResult(dummyXlsx);
    }
}
