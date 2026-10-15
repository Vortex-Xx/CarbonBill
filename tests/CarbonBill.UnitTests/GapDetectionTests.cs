using CarbonBill.Modules.GapDetection.Domain;
using CarbonBill.Modules.GapDetection.Fakes;
using CarbonBill.Modules.GapDetection.Handlers;
using CarbonBill.Modules.GapDetection.Persistence;
using CarbonBill.Modules.GapDetection.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Events;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarbonBill.UnitTests;

public class TestTimeProvider(DateTime initialUtc) : TimeProvider
{
    private DateTimeOffset _utcNow = new DateTimeOffset(initialUtc, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTime newUtc)
    {
        _utcNow = new DateTimeOffset(newUtc, TimeSpan.Zero);
    }

    public void Advance(TimeSpan delta)
    {
        _utcNow = _utcNow.Add(delta);
    }
}

public class RecordingEventPublisher : IDomainEventPublisher
{
    public List<IDomainEvent> PublishedEvents { get; } = [];

    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent
    {
        PublishedEvents.Add(domainEvent);
        return Task.CompletedTask;
    }

    public Task PublishAllAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        PublishedEvents.AddRange(domainEvents);
        return Task.CompletedTask;
    }
}

public sealed class GapDetectionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenantContext;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private readonly GapDetectionDbContext _dbContext;
    private readonly FakeExpectedDocRuleReader _ruleReader;
    private readonly FakeDocumentReadModel _documentReadModel;
    private readonly RecordingEventPublisher _eventPublisher;
    private readonly TestTimeProvider _timeProvider;
    private readonly GapDetectionService _gapService;

    public GapDetectionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _tenantContext = new TenantContext();
        _tenantContext.SetContext(_orgId, _userId);

        var dbOptions = new DbContextOptionsBuilder<GapDetectionDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new GapDetectionDbContext(dbOptions, _tenantContext);
        _dbContext.Database.EnsureCreated();

        _ruleReader = new FakeExpectedDocRuleReader();
        _documentReadModel = new FakeDocumentReadModel();
        _eventPublisher = new RecordingEventPublisher();
        
        // Start clock on 2026-09-25
        _timeProvider = new TestTimeProvider(new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc));

        _gapService = new GapDetectionService(
            _dbContext,
            _ruleReader,
            _documentReadModel,
            _eventPublisher,
            _timeProvider,
            NullLogger<GapDetectionService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task EscalationTimeline_WithFakeClock_TransitionsProperlyFromNoneToDayMinus7ToDayMinus3ToDayZero()
    {
        // Arrange
        // For period "2026-09" with DueDayOfMonth = 10, Due Date is 2026-10-10 23:59:59 UTC
        var assetId = Guid.NewGuid();
        var rule = new ExpectedDocRuleDto(
            Id: Guid.NewGuid(),
            OrgId: _orgId,
            AssetId: assetId,
            AssetName: "Generator 2",
            AssetType: "Genset",
            DocType: "DieselSlip",
            Frequency: "Monthly",
            DueDayOfMonth: 10,
            ResponsibleUserId: Guid.NewGuid());

        _ruleReader.AddRule(rule);

        // 1. Day -15 (2026-09-25): Well before due date
        _timeProvider.SetUtcNow(new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc));
        var alerts = await _gapService.CheckMissingDocumentsAsync(_orgId, "2026-09");
        Assert.Single(alerts);
        var alert = alerts[0];
        Assert.Equal(EscalationStates.None, alert.EscalationState);
        Assert.Equal(AlertSeverities.Amber, alert.Severity);
        Assert.Equal(AlertStatuses.Open, alert.Status);

        // 2. Day -7 (2026-10-03): Reminder threshold
        _timeProvider.SetUtcNow(new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc));
        alerts = await _gapService.CheckMissingDocumentsAsync(_orgId, "2026-09");
        alert = alerts[0];
        Assert.Equal(EscalationStates.DayMinus7, alert.EscalationState);
        Assert.Equal(AlertSeverities.Amber, alert.Severity);
        Assert.Equal(AlertStatuses.Reminded, alert.Status);

        // 3. Day -3 (2026-10-07): Push to responsible user
        _timeProvider.SetUtcNow(new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc));
        alerts = await _gapService.CheckMissingDocumentsAsync(_orgId, "2026-09");
        alert = alerts[0];
        Assert.Equal(EscalationStates.DayMinus3, alert.EscalationState);
        Assert.Equal(AlertSeverities.Amber, alert.Severity);
        Assert.Equal(AlertStatuses.Reminded, alert.Status);

        // 4. Day 0 (2026-10-10): Due date - escalate to Compliance with RED severity
        _timeProvider.SetUtcNow(new DateTime(2026, 10, 10, 23, 59, 59, DateTimeKind.Utc));
        alerts = await _gapService.CheckMissingDocumentsAsync(_orgId, "2026-09");
        alert = alerts[0];
        Assert.Equal(EscalationStates.DayZero, alert.EscalationState);
        Assert.Equal(AlertSeverities.Red, alert.Severity);
        Assert.Equal(AlertStatuses.Escalated, alert.Status);

        // 5. Day +5 (2026-10-15): Overdue - stays DayZero and RED
        _timeProvider.SetUtcNow(new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc));
        alerts = await _gapService.CheckMissingDocumentsAsync(_orgId, "2026-09");
        alert = alerts[0];
        Assert.Equal(EscalationStates.DayZero, alert.EscalationState);
        Assert.Equal(AlertSeverities.Red, alert.Severity);
        Assert.Equal(AlertStatuses.Escalated, alert.Status);

        // Assert MissingAlertRaisedEvent was published
        Assert.Contains(_eventPublisher.PublishedEvents, e => e is MissingAlertRaisedEvent);
    }

    [Fact]
    public async Task AutoResolve_WhenDocumentArrives_MarksAlertResolved()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var rule = new ExpectedDocRuleDto(
            Id: Guid.NewGuid(),
            OrgId: _orgId,
            AssetId: assetId,
            AssetName: "Main Meter",
            AssetType: "GridMeter",
            DocType: "ElectricityBill",
            Frequency: "Monthly",
            DueDayOfMonth: 15,
            ResponsibleUserId: _userId);

        _ruleReader.AddRule(rule);

        // Initially document is missing
        var alerts = await _gapService.CheckMissingDocumentsAsync(_orgId, "2026-09");
        var alert = Assert.Single(alerts);
        Assert.Equal(AlertStatuses.Open, alert.Status);

        // Act: Document arrives and is registered in read model
        var docId = Guid.NewGuid();
        _documentReadModel.AddDocument(new DocumentSummaryDto(
            Id: docId,
            OrgId: _orgId,
            SiteId: Guid.NewGuid(),
            AssetId: assetId,
            DocType: "ElectricityBill",
            BillingPeriod: "2026-09",
            UploadedAtUtc: _timeProvider.GetUtcNow().UtcDateTime,
            Status: "Confirmed"));

        // Re-run gap evaluation
        var updatedAlerts = await _gapService.CheckMissingDocumentsAsync(_orgId, "2026-09");
        var resolvedAlert = Assert.Single(updatedAlerts);

        // Assert
        Assert.Equal(AlertStatuses.Resolved, resolvedAlert.Status);
        Assert.NotNull(resolvedAlert.ResolvedAtUtc);

        // Assert MissingAlertResolvedEvent was published
        Assert.Contains(_eventPublisher.PublishedEvents, e => e is MissingAlertResolvedEvent res && res.AlertId == alert.Id);
    }

    [Fact]
    public async Task EventDriven_DocumentUploadedHandler_ResolvesMatchingGaps()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var rule = new ExpectedDocRuleDto(
            Id: Guid.NewGuid(),
            OrgId: _orgId,
            AssetId: assetId,
            AssetName: "Boiler 1",
            AssetType: "Boiler",
            DocType: "GasBill",
            Frequency: "Monthly",
            DueDayOfMonth: 12,
            ResponsibleUserId: _userId);

        _ruleReader.AddRule(rule);
        await _gapService.CheckMissingDocumentsAsync(_orgId, "2026-09");

        var existingAlert = await _dbContext.MissingAlerts.FirstOrDefaultAsync(a => a.AssetId == assetId);
        Assert.NotNull(existingAlert);
        Assert.Equal(AlertStatuses.Open, existingAlert.Status);

        // Act: Fire DocumentUploadedEvent via handler
        var handler = new DocumentUploadedEventHandler(_gapService, NullLogger<DocumentUploadedEventHandler>.Instance);
        var docId = Guid.NewGuid();
        var uploadEvent = new DocumentUploadedEvent(
            DocumentId: docId,
            OrgId: _orgId,
            StoragePath: "uploads/2026/09/bill.pdf",
            ContentType: "application/pdf",
            Source: "web",
            OccurredOnUtc: _timeProvider.GetUtcNow().UtcDateTime,
            SiteId: Guid.NewGuid(),
            AssetId: assetId,
            DocType: "GasBill",
            BillingPeriod: "2026-09");

        await handler.HandleAsync(uploadEvent);

        // Assert
        var resolvedAlert = await _dbContext.MissingAlerts.FirstOrDefaultAsync(a => a.AssetId == assetId);
        Assert.NotNull(resolvedAlert);
        Assert.Equal(AlertStatuses.Resolved, resolvedAlert.Status);
        Assert.Equal(docId, resolvedAlert.ResolvedDocumentId);
    }

    [Fact]
    public async Task MultiAssetOrganization_TracksMultipleAssets_ResolvesOnlySubmittedAsset()
    {
        // Arrange: Organization with 4 distinct assets
        var meterId = Guid.NewGuid();
        var gen1Id = Guid.NewGuid();
        var gen2Id = Guid.NewGuid();
        var boilerId = Guid.NewGuid();

        _ruleReader.AddRules([
            new ExpectedDocRuleDto(Guid.NewGuid(), _orgId, meterId, "Main Grid Meter", "GridMeter", "ElectricityBill", "Monthly", 10, _userId),
            new ExpectedDocRuleDto(Guid.NewGuid(), _orgId, gen1Id, "Generator 1", "Genset", "DieselSlip", "Monthly", 10, _userId),
            new ExpectedDocRuleDto(Guid.NewGuid(), _orgId, gen2Id, "Generator 2", "Genset", "DieselSlip", "Monthly", 10, _userId),
            new ExpectedDocRuleDto(Guid.NewGuid(), _orgId, boilerId, "Boiler 1", "Boiler", "GasBill", "Monthly", 10, _userId)
        ]);

        // Evaluate gaps: all 4 are missing initially
        var initialAlerts = await _gapService.CheckMissingDocumentsAsync(_orgId, "2026-09");
        Assert.Equal(4, initialAlerts.Count);

        // Verify tailored plain request strings for floor staff
        var gen2Alert = initialAlerts.First(a => a.AssetId == gen2Id);
        Assert.Equal("Please send diesel slip for Generator 2", gen2Alert.PlainRequestMessageEn);
        Assert.Equal("অনুগ্রহ করে জেনারেটর ২-এর ডিজেল স্লিপ পাঠান", gen2Alert.PlainRequestMessageBn);
        Assert.Equal("gap.request.diesel", gen2Alert.RequestMessageKey);

        var boilerAlert = initialAlerts.First(a => a.AssetId == boilerId);
        Assert.Equal("Please send gas bill for Boiler 1", boilerAlert.PlainRequestMessageEn);
        Assert.Equal("অনুগ্রহ করে বয়লার ১-এর গ্যাস বিল পাঠান", boilerAlert.PlainRequestMessageBn);

        // Act: Submit document for Generator 1 ONLY
        var docId = Guid.NewGuid();
        _documentReadModel.AddDocument(new DocumentSummaryDto(
            Id: docId,
            OrgId: _orgId,
            SiteId: Guid.NewGuid(),
            AssetId: gen1Id,
            DocType: "DieselSlip",
            BillingPeriod: "2026-09",
            UploadedAtUtc: _timeProvider.GetUtcNow().UtcDateTime,
            Status: "Confirmed"));

        var postUploadAlerts = await _gapService.CheckMissingDocumentsAsync(_orgId, "2026-09");

        // Assert: Generator 1 is resolved, while Generator 2, Main Grid Meter, and Boiler 1 remain unresolved
        var allAlertsInDb = await _dbContext.MissingAlerts.ToListAsync();
        var gen1InDb = allAlertsInDb.First(a => a.AssetId == gen1Id);
        var gen2InDb = allAlertsInDb.First(a => a.AssetId == gen2Id);
        var meterInDb = allAlertsInDb.First(a => a.AssetId == meterId);
        var boilerInDb = allAlertsInDb.First(a => a.AssetId == boilerId);

        Assert.Equal(AlertStatuses.Resolved, gen1InDb.Status);
        Assert.Equal(AlertStatuses.Open, gen2InDb.Status);
        Assert.Equal(AlertStatuses.Open, meterInDb.Status);
        Assert.Equal(AlertStatuses.Open, boilerInDb.Status);
    }

    [Fact]
    public async Task TenantIsolation_CrossTenantAccess_IsPrevented()
    {
        // Arrange
        var orgA = _orgId;
        var orgB = Guid.NewGuid();

        var assetA = Guid.NewGuid();
        var assetB = Guid.NewGuid();

        _ruleReader.AddRules([
            new ExpectedDocRuleDto(Guid.NewGuid(), orgA, assetA, "Asset Org A", "Genset", "DieselSlip", "Monthly", 10, _userId),
            new ExpectedDocRuleDto(Guid.NewGuid(), orgB, assetB, "Asset Org B", "Genset", "DieselSlip", "Monthly", 10, _userId)
        ]);

        await _gapService.CheckMissingDocumentsAsync(orgA, "2026-09");
        await _gapService.CheckMissingDocumentsAsync(orgB, "2026-09");

        // Act & Assert 1: Query as Org A
        var orgAAlerts = await _gapService.GetAlertsAsync(orgA, "2026-09");
        Assert.Single(orgAAlerts);
        Assert.Equal(assetA, orgAAlerts[0].AssetId);
        Assert.DoesNotContain(orgAAlerts, a => a.OrgId == orgB);

        // Act & Assert 2: Org A attempts to nudge Org B's alert
        var orgBAlert = await _dbContext.MissingAlerts.IgnoreQueryFilters().FirstAsync(a => a.OrgId == orgB);
        var nudgeResult = await _gapService.NudgeAlertAsync(orgBAlert.Id, orgA);

        Assert.True(nudgeResult.IsFailure);
        Assert.Contains("cross-tenant access denied", nudgeResult.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NudgeAlert_IncrementsNudgeCount_AndRejectsResolvedAlert()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        _ruleReader.AddRule(new ExpectedDocRuleDto(Guid.NewGuid(), _orgId, assetId, "Asset 1", "Genset", "DieselSlip", "Monthly", 10, _userId));
        var alerts = await _gapService.CheckMissingDocumentsAsync(_orgId, "2026-09");
        var alert = Assert.Single(alerts);

        Assert.Equal(0, alert.NudgeCount);
        Assert.Null(alert.LastNudgedAtUtc);

        // Act 1: Nudge alert
        var nudgeResult1 = await _gapService.NudgeAlertAsync(alert.Id, _orgId);
        Assert.True(nudgeResult1.IsSuccess);
        Assert.Equal(1, nudgeResult1.Value.NudgeCount);
        Assert.NotNull(nudgeResult1.Value.LastNudgedAtUtc);

        // Act 2: Nudge again
        var nudgeResult2 = await _gapService.NudgeAlertAsync(alert.Id, _orgId);
        Assert.True(nudgeResult2.IsSuccess);
        Assert.Equal(2, nudgeResult2.Value.NudgeCount);

        // Act 3: Resolve alert and attempt nudge
        alert.Resolve(Guid.NewGuid(), DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        var nudgeResult3 = await _gapService.NudgeAlertAsync(alert.Id, _orgId);
        Assert.True(nudgeResult3.IsFailure);
        Assert.Equal("Cannot nudge a resolved alert.", nudgeResult3.Error);
    }
}
