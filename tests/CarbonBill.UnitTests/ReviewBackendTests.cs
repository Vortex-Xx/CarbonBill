using CarbonBill.Modules.Audit.Persistence;
using CarbonBill.Modules.Audit.Services;
using CarbonBill.Modules.Documents.Domain;
using CarbonBill.Modules.Documents.Persistence;
using CarbonBill.Modules.Extraction;
using CarbonBill.Modules.Extraction.Persistence;
using CarbonBill.Modules.Review.Domain;
using CarbonBill.Modules.Review.Persistence;
using CarbonBill.Modules.Review.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarbonBill.UnitTests;

public sealed class ReviewBackendTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DocumentsDbContext> _docOptions;
    private readonly DbContextOptions<ExtractionDbContext> _extOptions;
    private readonly DbContextOptions<ReviewDbContext> _revOptions;
    private readonly DbContextOptions<AuditDbContext> _audOptions;
    private readonly TenantContext _tenantContext;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public ReviewBackendTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _docOptions = new DbContextOptionsBuilder<DocumentsDbContext>().UseSqlite(_connection).Options;
        _extOptions = new DbContextOptionsBuilder<ExtractionDbContext>().UseSqlite(_connection).Options;
        _revOptions = new DbContextOptionsBuilder<ReviewDbContext>().UseSqlite(_connection).Options;
        _audOptions = new DbContextOptionsBuilder<AuditDbContext>().UseSqlite(_connection).Options;

        _tenantContext = new TenantContext();
        _tenantContext.SetContext(_orgId, _userId);

        using var docDb = new DocumentsDbContext(_docOptions, _tenantContext);
        docDb.Database.EnsureCreated();
        using var extDb = new ExtractionDbContext(_extOptions, _tenantContext);
        docDb.Database.ExecuteSqlRaw(extDb.Database.GenerateCreateScript());
        using var revDb = new ReviewDbContext(_revOptions, _tenantContext);
        docDb.Database.ExecuteSqlRaw(revDb.Database.GenerateCreateScript());
        using var audDb = new AuditDbContext(_audOptions, _tenantContext);
        docDb.Database.ExecuteSqlRaw(audDb.Database.GenerateCreateScript());
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ConfirmDocument_AnyAuthenticatedMember_CanAttemptConfirmation()
    {
        // Arrange
        using var docDb = new DocumentsDbContext(_docOptions, _tenantContext);
        using var extDb = new ExtractionDbContext(_extOptions, _tenantContext);
        using var revDb = new ReviewDbContext(_revOptions, _tenantContext);
        using var audDb = new AuditDbContext(_audOptions, _tenantContext);

        var auditService = new AuditLogService(audDb, _tenantContext, NullLogger<AuditLogService>.Instance);
        var activityWriter = new FakeSuccessActivityWriter();
        var reviewService = new ReviewService(revDb, docDb, extDb, activityWriter, auditService, _tenantContext, NullLogger<ReviewService>.Instance);

        var docId = Guid.NewGuid();

        // Act - Any authenticated member can attempt confirmation (returns not found if doc doesn't exist)
        var result = await reviewService.ConfirmDocumentAsync(
            docId,
            new ConfirmDocumentRequest(),
            _userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task GetReviewQueue_SortsItemsLowestConfidenceFirst()
    {
        // Arrange
        using var docDb = new DocumentsDbContext(_docOptions, _tenantContext);
        using var extDb = new ExtractionDbContext(_extOptions, _tenantContext);
        using var revDb = new ReviewDbContext(_revOptions, _tenantContext);
        using var audDb = new AuditDbContext(_audOptions, _tenantContext);

        var docHigh = new Document
        {
            Id = Guid.NewGuid(), OrgId = _orgId, FileName = "high.pdf",
            StoragePath = "docs/high.pdf", ContentType = "application/pdf",
            Sha256Hash = "hash1", Status = DocumentStatuses.NeedsReview,
            UploadedByUserId = _userId, DocType = "Electricity"
        };
        var docLow = new Document
        {
            Id = Guid.NewGuid(), OrgId = _orgId, FileName = "low.pdf",
            StoragePath = "docs/low.pdf", ContentType = "application/pdf",
            Sha256Hash = "hash2", Status = DocumentStatuses.NeedsReview,
            UploadedByUserId = _userId, DocType = "Electricity"
        };
        docDb.Documents.AddRange(docHigh, docLow);
        await docDb.SaveChangesAsync();

        var runHigh = new ExtractionRun
        {
            Id = Guid.NewGuid(), OrgId = _orgId, DocumentId = docHigh.Id,
            OverallConfidence = 0.95f, Status = "Completed", TierUsed = 3
        };
        var runLow = new ExtractionRun
        {
            Id = Guid.NewGuid(), OrgId = _orgId, DocumentId = docLow.Id,
            OverallConfidence = 0.65f, Status = "Completed", TierUsed = 1
        };
        extDb.ExtractionRuns.AddRange(runHigh, runLow);
        await extDb.SaveChangesAsync();

        var auditService = new AuditLogService(audDb, _tenantContext, NullLogger<AuditLogService>.Instance);
        var reviewService = new ReviewService(revDb, docDb, extDb, new FakeSuccessActivityWriter(), auditService, _tenantContext, NullLogger<ReviewService>.Instance);

        // Act
        var queue = await reviewService.GetReviewQueueAsync();

        // Assert
        Assert.Equal(2, queue.Count);
        Assert.Equal(docLow.Id, queue[0].DocumentId); // 0.65 confidence first!
        Assert.Equal(docHigh.Id, queue[1].DocumentId); // 0.95 confidence second!
    }

    [Fact]
    public async Task ConfirmDocument_AtomicTransaction_WhenActivityWriterFails_RollsBackConfirmation()
    {
        // Arrange
        using var docDb = new DocumentsDbContext(_docOptions, _tenantContext);
        using var extDb = new ExtractionDbContext(_extOptions, _tenantContext);
        using var revDb = new ReviewDbContext(_revOptions, _tenantContext);
        using var audDb = new AuditDbContext(_audOptions, _tenantContext);

        var doc = new Document
        {
            Id = Guid.NewGuid(), OrgId = _orgId, FileName = "bill.pdf",
            StoragePath = "docs/bill.pdf", ContentType = "application/pdf",
            Sha256Hash = "hash-failing", Status = DocumentStatuses.NeedsReview,
            UploadedByUserId = _userId, DocType = "Electricity"
        };
        docDb.Documents.Add(doc);
        await docDb.SaveChangesAsync();

        var run = new ExtractionRun
        {
            Id = Guid.NewGuid(), OrgId = _orgId, DocumentId = doc.Id,
            OverallConfidence = 0.90f, Status = "Completed", TierUsed = 3,
            Fields =
            [
                new() { Id = Guid.NewGuid(), OrgId = _orgId, DocumentId = doc.Id, FieldName = "Quantity", RawValue = "1500", NormalizedValue = "1500", Confidence = 0.95f }
            ]
        };
        extDb.ExtractionRuns.Add(run);
        await extDb.SaveChangesAsync();

        var failingActivityWriter = new FailingActivityWriter();
        var auditService = new AuditLogService(audDb, _tenantContext, NullLogger<AuditLogService>.Instance);
        var reviewService = new ReviewService(revDb, docDb, extDb, failingActivityWriter, auditService, _tenantContext, NullLogger<ReviewService>.Instance);

        // Act
        var result = await reviewService.ConfirmDocumentAsync(doc.Id, new ConfirmDocumentRequest(), _userId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Failed to record activity", result.Error);

        // Document state must NOT be Confirmed or Calculated
        var unchangedDoc = await docDb.Documents.FirstAsync(d => d.Id == doc.Id);
        Assert.Equal(DocumentStatuses.NeedsReview, unchangedDoc.Status);
    }

    [Fact]
    public async Task ConfirmDocument_Success_MarksConfirmedAndCalculatedAndResolvesDuplicate()
    {
        // Arrange
        using var docDb = new DocumentsDbContext(_docOptions, _tenantContext);
        using var extDb = new ExtractionDbContext(_extOptions, _tenantContext);
        using var revDb = new ReviewDbContext(_revOptions, _tenantContext);
        using var audDb = new AuditDbContext(_audOptions, _tenantContext);

        var primaryDoc = new Document
        {
            Id = Guid.NewGuid(), OrgId = _orgId, FileName = "primary.pdf",
            StoragePath = "docs/primary.pdf", ContentType = "application/pdf",
            Sha256Hash = "primary-hash", Status = DocumentStatuses.NeedsReview,
            UploadedByUserId = _userId, DocType = "Electricity"
        };
        var dupeDoc = new Document
        {
            Id = Guid.NewGuid(), OrgId = _orgId, FileName = "dupe.pdf",
            StoragePath = "docs/dupe.pdf", ContentType = "application/pdf",
            Sha256Hash = "dupe-hash", Status = DocumentStatuses.NeedsReview,
            UploadedByUserId = _userId, DocType = "Electricity"
        };
        docDb.Documents.AddRange(primaryDoc, dupeDoc);
        await docDb.SaveChangesAsync();

        var run = new ExtractionRun
        {
            Id = Guid.NewGuid(), OrgId = _orgId, DocumentId = primaryDoc.Id,
            OverallConfidence = 0.92f, Status = "Completed", TierUsed = 3,
            Fields =
            [
                new() { Id = Guid.NewGuid(), OrgId = _orgId, DocumentId = primaryDoc.Id, FieldName = "Vendor", RawValue = "DESCO", NormalizedValue = "DESCO", Confidence = 0.95f },
                new() { Id = Guid.NewGuid(), OrgId = _orgId, DocumentId = primaryDoc.Id, FieldName = "Quantity", RawValue = "5000", NormalizedValue = "5000", Confidence = 0.95f },
                new() { Id = Guid.NewGuid(), OrgId = _orgId, DocumentId = primaryDoc.Id, FieldName = "Unit", RawValue = "kWh", NormalizedValue = "kWh", Confidence = 0.95f },
                new() { Id = Guid.NewGuid(), OrgId = _orgId, DocumentId = primaryDoc.Id, FieldName = "AmountBdt", RawValue = "50000", NormalizedValue = "50000", Confidence = 0.95f }
            ]
        };
        extDb.ExtractionRuns.Add(run);
        await extDb.SaveChangesAsync();

        var successWriter = new FakeSuccessActivityWriter();
        var auditService = new AuditLogService(audDb, _tenantContext, NullLogger<AuditLogService>.Instance);
        var reviewService = new ReviewService(revDb, docDb, extDb, successWriter, auditService, _tenantContext, NullLogger<ReviewService>.Instance);

        // Act - confirm primary and resolve duplicate
        var result = await reviewService.ConfirmDocumentAsync(
            primaryDoc.Id,
            new ConfirmDocumentRequest(ResolveDuplicateWithDocId: dupeDoc.Id),
            _userId);

        // Assert
        Assert.True(result.IsSuccess);
        var confirmedDoc = await docDb.Documents.FirstAsync(d => d.Id == primaryDoc.Id);
        Assert.Equal(DocumentStatuses.Calculated, confirmedDoc.Status);

        var markedDupe = await docDb.Documents.FirstAsync(d => d.Id == dupeDoc.Id);
        Assert.Equal(DocumentStatuses.Duplicate, markedDupe.Status);

        // Check ReviewDecision recorded
        var decision = await revDb.Decisions.FirstOrDefaultAsync(d => d.DocumentId == primaryDoc.Id);
        Assert.NotNull(decision);
        Assert.Equal(ReviewModes.Manual, decision!.Mode);
        Assert.Equal(ReviewDecisions.Approved, decision.Decision);

        // Check audit log recorded
        var logs = await audDb.AuditLogs.Where(l => l.OrgId == _orgId).ToListAsync();
        Assert.Contains(logs, l => l.Action == "DocumentConfirmed");
        Assert.Contains(logs, l => l.Action == "DuplicateResolved");
    }

    private sealed class FakeSuccessActivityWriter : IActivityWriter
    {
        public Task<Result<Guid>> RecordConfirmedActivityAsync(ConfirmedActivityRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(Result.Success(Guid.NewGuid()));
        }
    }

    private sealed class FailingActivityWriter : IActivityWriter
    {
        public Task<Result<Guid>> RecordConfirmedActivityAsync(ConfirmedActivityRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(Result.Failure<Guid>("Factor not found for electricity"));
        }
    }
}
