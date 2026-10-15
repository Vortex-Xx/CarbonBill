using CarbonBill.Modules.Audit.Persistence;
using CarbonBill.Modules.Audit.Services;
using CarbonBill.Modules.Documents.Domain;
using CarbonBill.Modules.Documents.Persistence;
using CarbonBill.Modules.Documents.Services;
using CarbonBill.Modules.Extraction;
using CarbonBill.Modules.Extraction.Persistence;
using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.Modules.Review.Persistence;
using CarbonBill.Modules.Review.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Events;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Providers;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarbonBill.IntegrationTests;

public sealed class EndToEndPipelineIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenantContext;
    private readonly SqlitePragmaInterceptor _pragmaInterceptor;
    private readonly TenantSaveChangesInterceptor _tenantInterceptor;

    private readonly DocumentsDbContext _documentsDb;
    private readonly ExtractionDbContext _extractionDb;
    private readonly ReviewDbContext _reviewDb;
    private readonly AuditDbContext _auditDb;
    private readonly IdentityTenancyDbContext _identityDb;

    private readonly FakeMemoryFileStore _fileStore;
    private readonly DocumentService _documentService;
    private readonly ReviewService _reviewService;
    private readonly FakeRecordedActivityWriter _activityWriter;

    private readonly Guid _testOrgId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();

    public EndToEndPipelineIntegrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();

        _tenantContext = new TenantContext();
        _tenantContext.SetContext(_testOrgId, _testUserId);

        _pragmaInterceptor = new SqlitePragmaInterceptor();
        _tenantInterceptor = new TenantSaveChangesInterceptor(_tenantContext);

        var docsOptions = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;
        var extractionOptions = new DbContextOptionsBuilder<ExtractionDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;
        var reviewOptions = new DbContextOptionsBuilder<ReviewDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;
        var auditOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;
        var identityOptions = new DbContextOptionsBuilder<IdentityTenancyDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;

        _documentsDb = new DocumentsDbContext(docsOptions, _tenantContext);
        _extractionDb = new ExtractionDbContext(extractionOptions, _tenantContext);
        _reviewDb = new ReviewDbContext(reviewOptions, _tenantContext);
        _auditDb = new AuditDbContext(auditOptions, _tenantContext);
        _identityDb = new IdentityTenancyDbContext(identityOptions, _tenantContext);

        // Ensure database tables created across all DbContexts
        _identityDb.Database.EnsureCreated();
        _identityDb.Database.ExecuteSqlRaw(_documentsDb.Database.GenerateCreateScript());
        _identityDb.Database.ExecuteSqlRaw(_extractionDb.Database.GenerateCreateScript());
        _identityDb.Database.ExecuteSqlRaw(_reviewDb.Database.GenerateCreateScript());
        _identityDb.Database.ExecuteSqlRaw(_auditDb.Database.GenerateCreateScript());

        // Seed basic org
        _identityDb.Organizations.Add(new Organization
        {
            Id = _testOrgId,
            Name = "Apex Textile & Garments Ltd.",
            Slug = "apex-textiles",
            Sector = "RMG",
            IsActive = true
        });
        _identityDb.SaveChanges();

        _fileStore = new FakeMemoryFileStore();
        var nullPublisher = new FakeDomainEventPublisher();

        _documentService = new DocumentService(
            _documentsDb,
            _fileStore,
            _tenantContext,
            nullPublisher,
            NullLogger<DocumentService>.Instance);

        _activityWriter = new FakeRecordedActivityWriter();
        var auditService = new AuditLogService(_auditDb, _tenantContext, NullLogger<AuditLogService>.Instance);

        _reviewService = new ReviewService(
            _reviewDb,
            _documentsDb,
            _extractionDb,
            _activityWriter,
            auditService,
            _tenantContext,
            NullLogger<ReviewService>.Instance);
    }

    [Fact]
    public async Task EndToEndPipeline_DieselSlip_UploadToOcrToReviewToCalculation_Succeeds()
    {
        // 1. INGESTION (Floor staff captures a diesel slip)
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
        using var stream = new MemoryStream(imageBytes);
        var idempotencyKey = Guid.NewGuid().ToString();

        var uploadResult = await _documentService.UploadDocumentAsync(
            stream,
            "diesel_slip_nov2024.jpg",
            "image/jpeg",
            _testUserId,
            idempotencyKey,
            source: "phone");

        Assert.True(uploadResult.IsSuccess);
        var docReceipt = uploadResult.Value;
        Assert.Equal(DocumentStatuses.Uploaded, docReceipt.Status);
        Assert.False(docReceipt.IsDuplicate);

        // 2. EXTRACTION (OCR pipeline processes the document)
        var extractionRunId = Guid.NewGuid();
        var extractionRun = new ExtractionRun
        {
            Id = extractionRunId,
            OrgId = _testOrgId,
            DocumentId = docReceipt.Id,
            TierUsed = 1,
            Status = "Completed",
            OverallConfidence = 0.92f,
            ProcessedAtUtc = DateTime.UtcNow,
            Fields =
            [
                new ExtractedField
                {
                    Id = Guid.NewGuid(),
                    OrgId = _testOrgId,
                    ExtractionRunId = extractionRunId,
                    DocumentId = docReceipt.Id,
                    FieldName = "quantity",
                    RawValue = "১,২০০",
                    NormalizedValue = "1200",
                    Confidence = 0.88f,
                    SourceTier = 1
                },
                new ExtractedField
                {
                    Id = Guid.NewGuid(),
                    OrgId = _testOrgId,
                    ExtractionRunId = extractionRunId,
                    DocumentId = docReceipt.Id,
                    FieldName = "unit",
                    RawValue = "লিটার",
                    NormalizedValue = "litre",
                    Confidence = 0.95f,
                    SourceTier = 1
                },
                new ExtractedField
                {
                    Id = Guid.NewGuid(),
                    OrgId = _testOrgId,
                    ExtractionRunId = extractionRunId,
                    DocumentId = docReceipt.Id,
                    FieldName = "fuel_type",
                    RawValue = "ডিজেল",
                    NormalizedValue = "diesel",
                    Confidence = 0.96f,
                    SourceTier = 1
                }
            ]
        };
        _extractionDb.ExtractionRuns.Add(extractionRun);
        await _extractionDb.SaveChangesAsync();

        // Advance document status to NeedsReview
        var doc = await _documentsDb.Documents.FindAsync(docReceipt.Id);
        Assert.NotNull(doc);
        doc.Status = DocumentStatuses.NeedsReview;
        doc.DocType = "DieselSlip";
        await _documentsDb.SaveChangesAsync();

        // 3. REVIEW QUEUE
        _tenantContext.SetContext(_testOrgId, _testUserId);
        var queue = await _reviewService.GetReviewQueueAsync(docType: null, siteId: null);
        Assert.Contains(queue, q => q.DocumentId == docReceipt.Id);

        // 4. FIELD CORRECTION
        var quantityField = extractionRun.Fields.First(f => f.FieldName == "quantity");
        var updateCorrectionResult = await _reviewService.UpdateDocumentFieldsAsync(
            docReceipt.Id,
            new UpdateFieldsRequest(
                new Dictionary<string, string> { [quantityField.FieldName] = "1250" },
                Notes: "Verified against physical meter counter: 1,250 Litres"),
            _testUserId);

        Assert.True(updateCorrectionResult.IsSuccess);

        // 5. CONFIRMATION TRANSACTION (Commits to ActivityRecord)
        var confirmResult = await _reviewService.ConfirmDocumentAsync(
            docReceipt.Id,
            new ConfirmDocumentRequest(
                ActivityType: "diesel",
                OverrideQuantity: 1250m,
                OverrideUnit: "litre",
                OverridePeriod: "2024-11",
                Notes: "Approved by Member Rahim"),
            _testUserId);

        Assert.True(confirmResult.IsSuccess);

        // 6. VERIFY ACTIVITY RECORD COMMITMENT
        Assert.Single(_activityWriter.RecordedRequests);
        var activity = _activityWriter.RecordedRequests[0];
        Assert.Equal(1250m, activity.Quantity);
        Assert.Equal("litre", activity.Unit.ToLowerInvariant());
        Assert.Equal("diesel", activity.ActivityType.ToLowerInvariant());

        // Document state updated to Calculated
        var confirmedDoc = await _documentsDb.Documents.FindAsync(docReceipt.Id);
        Assert.NotNull(confirmedDoc);
        Assert.Equal(DocumentStatuses.Calculated, confirmedDoc.Status);

        // Audit log recorded
        var auditLogs = await _auditDb.AuditLogs.ToListAsync();
        Assert.Contains(auditLogs, a => a.Action == "DocumentConfirmed" && a.EntityId == docReceipt.Id.ToString());
    }

    [Fact]
    public async Task DuplicateUpload_DetectsDuplicate_BySha256_WithoutEmissionsDoubleCounting()
    {
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0xAA, 0xBB };

        // Upload first time
        using var stream1 = new MemoryStream(imageBytes);
        var firstUpload = await _documentService.UploadDocumentAsync(
            stream1, "first_bill.jpg", "image/jpeg", _testUserId);
        Assert.True(firstUpload.IsSuccess);
        Assert.False(firstUpload.Value.IsDuplicate);

        // Upload exact same file second time
        using var stream2 = new MemoryStream(imageBytes);
        var secondUpload = await _documentService.UploadDocumentAsync(
            stream2, "duplicate_bill.jpg", "image/jpeg", _testUserId);

        Assert.True(secondUpload.IsSuccess);
        Assert.True(secondUpload.Value.IsDuplicate);
        Assert.Equal(DocumentStatuses.Duplicate, secondUpload.Value.Status);

        // Only one document record is active
        var totalDocs = await _documentsDb.Documents.CountAsync();
        Assert.Equal(1, totalDocs);
    }

    [Fact]
    public async Task ManualFallbackEntry_DamagedSlip_ProcessesDirectlyToReviewQueue()
    {
        var manualRequest = new ManualEntryRequest(
            AssetId: null,
            DocType: "DieselSlip",
            SlipNumber: "TORN-SLIP-404",
            Quantity: 500m,
            Unit: "litre",
            AmountBdt: 54000m,
            Date: new DateTime(2024, 10, 15, 0, 0, 0, DateTimeKind.Utc));

        var result = await _documentService.CreateManualEntryAsync(manualRequest, _testUserId);
        Assert.True(result.IsSuccess);

        var doc = await _documentsDb.Documents.FindAsync(result.Value.Id);
        Assert.NotNull(doc);
        Assert.Equal("manual", doc.Source);
        Assert.Equal(DocumentStatuses.NeedsReview, doc.Status);

        // Direct confirmation works
        _tenantContext.SetContext(_testOrgId, _testUserId);
        var confirmResult = await _reviewService.ConfirmDocumentAsync(
            doc.Id,
            new ConfirmDocumentRequest(
                ActivityType: "diesel",
                OverrideQuantity: 500m,
                OverrideUnit: "litre",
                OverridePeriod: "2024-10",
                Notes: "Manual fallback approved"),
            _testUserId);

        Assert.True(confirmResult.IsSuccess);

        Assert.Single(_activityWriter.RecordedRequests);
        var activity = _activityWriter.RecordedRequests[0];
        Assert.Equal(500m, activity.Quantity);
    }

    public void Dispose()
    {
        _documentsDb.Dispose();
        _extractionDb.Dispose();
        _reviewDb.Dispose();
        _auditDb.Dispose();
        _identityDb.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal sealed class FakeRecordedActivityWriter : IActivityWriter
{
    public List<ConfirmedActivityRequest> RecordedRequests { get; } = [];

    public Task<Result<Guid>> RecordConfirmedActivityAsync(ConfirmedActivityRequest request, CancellationToken ct = default)
    {
        RecordedRequests.Add(request);
        return Task.FromResult(Result.Success(Guid.NewGuid()));
    }
}

internal sealed class FakeMemoryFileStore : IFileStore
{
    private readonly Dictionary<string, byte[]> _storage = new();

    public Task<string> UploadAsync(Stream contentStream, string destinationPath, string contentType, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        contentStream.CopyTo(ms);
        _storage[destinationPath] = ms.ToArray();
        return Task.FromResult(destinationPath);
    }

    public Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (_storage.TryGetValue(storagePath, out var bytes))
        {
            return Task.FromResult<Stream>(new MemoryStream(bytes));
        }
        return Task.FromResult<Stream>(new MemoryStream());
    }

    public Task<bool> DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_storage.Remove(storagePath));
    }

    public Task<string> GetPreSignedUrlAsync(string storagePath, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"https://storage.fake.local/{storagePath}?sig=test");
    }

    public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_storage.ContainsKey(storagePath));
    }
}

internal sealed class FakeDomainEventPublisher : IDomainEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent
    {
        return Task.CompletedTask;
    }

    public Task PublishAllAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
