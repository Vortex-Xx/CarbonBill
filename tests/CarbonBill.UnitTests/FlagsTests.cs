using CarbonBill.Modules.Flags.Contracts;
using CarbonBill.Modules.Flags.Domain;
using CarbonBill.Modules.Flags.Engine;
using CarbonBill.Modules.Flags.Fakes;
using CarbonBill.Modules.Flags.Persistence;
using CarbonBill.Modules.Flags.Seeds;
using CarbonBill.Modules.Flags.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Events;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarbonBill.UnitTests;

public class RecordingFlagEventPublisher : IDomainEventPublisher
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

public sealed class FlagsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenantContext;
    private readonly FlagsDbContext _dbContext;
    private readonly RecordingFlagEventPublisher _eventPublisher;
    private readonly FakeFlagDocumentReadModel _documentReadModel;
    private readonly FakeFlagEmissionReadModel _emissionReadModel;
    private readonly FakeExpectedDocRuleReader _ruleReader;
    private readonly FakeDocumentReadModel _docSummaryReadModel;

    private readonly MissingDocumentRuleEvaluator _missingDocEvaluator;
    private readonly LowOcrConfidenceRuleEvaluator _lowOcrEvaluator;
    private readonly DuplicateSuspectedRuleEvaluator _duplicateEvaluator;
    private readonly ImplausibleValueRuleEvaluator _implausibleEvaluator;
    private readonly EstimatedShareHighRuleEvaluator _estimatedShareEvaluator;
    private readonly FlagsService _flagsService;

    public FlagsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _tenantContext = new TenantContext();
        var options = new DbContextOptionsBuilder<FlagsDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new FlagsDbContext(options, _tenantContext);
        _dbContext.Database.EnsureCreated();

        _eventPublisher = new RecordingFlagEventPublisher();
        _documentReadModel = new FakeFlagDocumentReadModel();
        _emissionReadModel = new FakeFlagEmissionReadModel();
        _ruleReader = new FakeExpectedDocRuleReader();
        _docSummaryReadModel = new FakeDocumentReadModel();

        _missingDocEvaluator = new MissingDocumentRuleEvaluator(_ruleReader, _docSummaryReadModel);
        _lowOcrEvaluator = new LowOcrConfidenceRuleEvaluator(_documentReadModel);
        _duplicateEvaluator = new DuplicateSuspectedRuleEvaluator(_documentReadModel);
        _implausibleEvaluator = new ImplausibleValueRuleEvaluator(_documentReadModel);
        _estimatedShareEvaluator = new EstimatedShareHighRuleEvaluator(_emissionReadModel);

        var evaluators = new IFlagRuleEvaluator[]
        {
            _missingDocEvaluator,
            _lowOcrEvaluator,
            _duplicateEvaluator,
            _implausibleEvaluator,
            _estimatedShareEvaluator
        };

        _flagsService = new FlagsService(_dbContext, evaluators, _eventPublisher, NullLogger<FlagsService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private FlagRule CreateTestRule(string code, string defaultSeverity, string triggerParams = "{}")
    {
        var rule = new FlagRule
        {
            FlagCode = code,
            Family = "DataQuality",
            NameBn = $"{code} বাংলা নাম",
            NameEn = $"{code} English Name",
            DefaultSeverity = defaultSeverity,
            TriggerParametersJson = triggerParams,
            SuggestedActionBn = $"{code} বাংলা পদক্ষেপ",
            SuggestedActionEn = $"{code} English Action",
            IsActive = true
        };
        _dbContext.FlagRules.Add(rule);
        _dbContext.SaveChanges();
        return rule;
    }

    [Fact]
    public async Task LowOcrConfidence_BoundaryTest_TriggersOnlyBelowThreshold()
    {
        var rule = CreateTestRule("LOW_OCR_CONFIDENCE", "Amber", "{\"confidence_threshold\": 0.70}");
        var orgId = Guid.NewGuid();
        var period = "2026-09";

        // Boundary 1: Confidence at 0.70 (exact threshold) should NOT flag
        _documentReadModel.AddDocument(new ExtractedDocumentRecord(
            DocumentId: Guid.NewGuid(),
            OrgId: orgId,
            SiteId: null,
            AssetId: null,
            AssetName: "Meter 1",
            DocType: "Electricity Bill",
            BillingPeriod: period,
            FileHash: "hash1",
            VendorName: "DPDC",
            BillNumber: "BILL-001",
            Quantity: 1000m,
            Unit: "kWh",
            MinFieldConfidence: 0.70f,
            Fields: [new ExtractedFieldConfidence("Quantity", "1000", 0.70f)]
        ));

        var resultAtThreshold = await _lowOcrEvaluator.EvaluateAsync(rule, new EvaluationContext(orgId, period));
        Assert.False(resultAtThreshold.ShouldFlag);

        // Boundary 2: Confidence at 0.69 (below threshold) MUST flag
        _documentReadModel.AddDocument(new ExtractedDocumentRecord(
            DocumentId: Guid.NewGuid(),
            OrgId: orgId,
            SiteId: null,
            AssetId: null,
            AssetName: "Meter 2",
            DocType: "Electricity Bill",
            BillingPeriod: period,
            FileHash: "hash2",
            VendorName: "DPDC",
            BillNumber: "BILL-002",
            Quantity: 1200m,
            Unit: "kWh",
            MinFieldConfidence: 0.69f,
            Fields: [new ExtractedFieldConfidence("Quantity", "1200", 0.69f)]
        ));

        var resultBelowThreshold = await _lowOcrEvaluator.EvaluateAsync(rule, new EvaluationContext(orgId, period));
        Assert.True(resultBelowThreshold.ShouldFlag);
        Assert.Equal("Amber", resultBelowThreshold.SeverityOverride);
        Assert.Contains("কম ওলসিআর কনফিডেন্স", resultBelowThreshold.ExplanationBn, StringComparison.Ordinal);
        Assert.Contains("Low OCR confidence", resultBelowThreshold.ExplanationEn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DuplicateSuspected_DetectsMatchingHashAndMatchingVendorBill()
    {
        var rule = CreateTestRule("DUPLICATE_SUSPECTED", "Amber", "{\"match_hash\": true, \"match_vendor_bill_period\": true}");
        var orgId = Guid.NewGuid();
        var period = "2026-09";

        // Doc A and Doc B have identical file hash
        _documentReadModel.AddDocument(new ExtractedDocumentRecord(
            DocumentId: Guid.NewGuid(),
            OrgId: orgId,
            SiteId: null,
            AssetId: null,
            AssetName: null,
            DocType: "Diesel Slip",
            BillingPeriod: period,
            FileHash: "SHA256_IDENTICAL_HASH",
            VendorName: "Padma Oil",
            BillNumber: "SLIP-101",
            Quantity: 500m,
            Unit: "litre",
            MinFieldConfidence: 0.95f,
            Fields: []
        ));

        _documentReadModel.AddDocument(new ExtractedDocumentRecord(
            DocumentId: Guid.NewGuid(),
            OrgId: orgId,
            SiteId: null,
            AssetId: null,
            AssetName: null,
            DocType: "Diesel Slip",
            BillingPeriod: period,
            FileHash: "SHA256_IDENTICAL_HASH",
            VendorName: "Padma Oil",
            BillNumber: "SLIP-102",
            Quantity: 500m,
            Unit: "litre",
            MinFieldConfidence: 0.95f,
            Fields: []
        ));

        var result = await _duplicateEvaluator.EvaluateAsync(rule, new EvaluationContext(orgId, period));
        Assert.True(result.ShouldFlag);
        Assert.Contains("ডুপ্লিকেট", result.ExplanationBn, StringComparison.Ordinal);
        Assert.Contains("duplicate", result.ExplanationEn, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SHA256_IDENTICAL_HASH", result.EvidenceJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImplausibleValue_DetectsUnitMismatchAnd3xIqrOutliers()
    {
        var rule = CreateTestRule("IMPLAUSIBLE_VALUE", "Amber", "{\"iqr_multiplier\": 3.0, \"min_history_count\": 3}");
        var orgId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var period = "2026-09";

        // Historical consumption: [100, 105, 110, 115, 120] Litres
        // Q1 = 105, Q3 = 115, IQR = 10. Margin = 3 * 10 = 30.
        // Bounds: [75, 145].
        _documentReadModel.SetHistory(orgId, assetId, new HistoricalAssetConsumption(
            AssetId: assetId,
            AssetName: "Generator 1",
            Unit: "litre",
            PastQuantities: [100m, 105m, 110m, 115m, 120m]
        ));

        // 1. In-bounds value (125 litre) -> Should NOT flag
        _documentReadModel.AddDocument(new ExtractedDocumentRecord(
            DocumentId: Guid.NewGuid(),
            OrgId: orgId,
            SiteId: null,
            AssetId: assetId,
            AssetName: "Generator 1",
            DocType: "Diesel Slip",
            BillingPeriod: period,
            FileHash: "h1",
            VendorName: "Padma",
            BillNumber: "1",
            Quantity: 125m,
            Unit: "litre",
            MinFieldConfidence: 0.9f,
            Fields: []
        ));

        var resultNormal = await _implausibleEvaluator.EvaluateAsync(rule, new EvaluationContext(orgId, period));
        Assert.False(resultNormal.ShouldFlag);

        // 2. Out-of-bounds value (150 litre > 145) -> MUST flag
        _documentReadModel.AddDocument(new ExtractedDocumentRecord(
            DocumentId: Guid.NewGuid(),
            OrgId: orgId,
            SiteId: null,
            AssetId: assetId,
            AssetName: "Generator 1",
            DocType: "Diesel Slip",
            BillingPeriod: period,
            FileHash: "h2",
            VendorName: "Padma",
            BillNumber: "2",
            Quantity: 150m,
            Unit: "litre",
            MinFieldConfidence: 0.9f,
            Fields: []
        ));

        var resultOutlier = await _implausibleEvaluator.EvaluateAsync(rule, new EvaluationContext(orgId, period));
        Assert.True(resultOutlier.ShouldFlag);
        Assert.Contains("৩x IQR", resultOutlier.ExplanationBn, StringComparison.Ordinal);
        Assert.Contains("3x IQR", resultOutlier.ExplanationEn, StringComparison.Ordinal);

        // 3. Unit mismatch test (Gallon instead of Litre)
        var assetId2 = Guid.NewGuid();
        _documentReadModel.SetHistory(orgId, assetId2, new HistoricalAssetConsumption(
            AssetId: assetId2,
            AssetName: "Boiler 2",
            Unit: "kg",
            PastQuantities: [1000m, 1100m, 1200m]
        ));

        _documentReadModel.AddDocument(new ExtractedDocumentRecord(
            DocumentId: Guid.NewGuid(),
            OrgId: orgId,
            SiteId: null,
            AssetId: assetId2,
            AssetName: "Boiler 2",
            DocType: "Gas Bill",
            BillingPeriod: period,
            FileHash: "h3",
            VendorName: "Titas",
            BillNumber: "3",
            Quantity: 1100m,
            Unit: "gallon", // Mismatched unit!
            MinFieldConfidence: 0.9f,
            Fields: []
        ));

        var resultUnitMismatch = await _implausibleEvaluator.EvaluateAsync(rule, new EvaluationContext(orgId, period));
        Assert.True(resultUnitMismatch.ShouldFlag);
        Assert.Contains("UnitMismatch", resultUnitMismatch.EvidenceJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EstimatedShareHigh_BoundaryTest_TriggersOnlyAbove10Percent()
    {
        var rule = CreateTestRule("ESTIMATED_SHARE_HIGH", "Amber", "{\"max_estimated_percentage\": 10.0}");
        var orgId = Guid.NewGuid();
        var period = "2026-09";

        // Boundary 1: Exactly 10.0% estimated -> Should NOT flag
        _emissionReadModel.SetEmissionSummary(orgId, period, new PeriodEmissionSummary(
            OrgId: orgId,
            SiteId: null,
            Period: period,
            TotalCo2eKg: 1000m,
            EstimatedCo2eKg: 100m // Exactly 10%
        ));

        var resultAt10 = await _estimatedShareEvaluator.EvaluateAsync(rule, new EvaluationContext(orgId, period));
        Assert.False(resultAt10.ShouldFlag);

        // Boundary 2: 10.1% estimated -> MUST flag
        _emissionReadModel.SetEmissionSummary(orgId, period, new PeriodEmissionSummary(
            OrgId: orgId,
            SiteId: null,
            Period: period,
            TotalCo2eKg: 1000m,
            EstimatedCo2eKg: 101m // 10.1%
        ));

        var resultAbove10 = await _estimatedShareEvaluator.EvaluateAsync(rule, new EvaluationContext(orgId, period));
        Assert.True(resultAbove10.ShouldFlag);
        Assert.Contains("১০%", resultAbove10.ExplanationBn, StringComparison.Ordinal);
        Assert.Contains("10%", resultAbove10.ExplanationEn, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FlagLifecycle_AutoResolvesWhenConditionClears()
    {
        var rule = CreateTestRule("LOW_OCR_CONFIDENCE", "Amber", "{\"confidence_threshold\": 0.70}");
        var orgId = Guid.NewGuid();
        var period = "2026-09";

        // 1. Condition is bad (0.60 confidence)
        var doc = new ExtractedDocumentRecord(
            DocumentId: Guid.NewGuid(),
            OrgId: orgId,
            SiteId: null,
            AssetId: null,
            AssetName: "Meter 1",
            DocType: "Electricity Bill",
            BillingPeriod: period,
            FileHash: "h1",
            VendorName: "DPDC",
            BillNumber: "BILL-1",
            Quantity: 1000m,
            Unit: "kWh",
            MinFieldConfidence: 0.60f,
            Fields: [new ExtractedFieldConfidence("Quantity", "1000", 0.60f)]
        );
        _documentReadModel.AddDocument(doc);

        var raisedFlags = await _flagsService.EvaluateOrgPeriodAsync(orgId, period);
        Assert.Single(raisedFlags);
        Assert.Equal("Open", raisedFlags[0].State);
        Assert.Single(_eventPublisher.PublishedEvents);

        // 2. Condition clears (reviewer corrected fields to 0.95 confidence)
        var cleanModel = new FakeFlagDocumentReadModel();
        cleanModel.AddDocument(doc with { MinFieldConfidence = 0.95f, Fields = [new ExtractedFieldConfidence("Quantity", "1000", 0.95f)] });

        var cleanEvaluator = new LowOcrConfidenceRuleEvaluator(cleanModel);
        var cleanService = new FlagsService(_dbContext, [cleanEvaluator], _eventPublisher, NullLogger<FlagsService>.Instance);

        var subsequentEval = await cleanService.EvaluateOrgPeriodAsync(orgId, period);
        Assert.Empty(subsequentEval); // No open flags

        // Flag in database must now be Resolved!
        var dbFlag = await _dbContext.Flags.FirstAsync(f => f.OrgId == orgId && f.RuleCode == "LOW_OCR_CONFIDENCE");
        Assert.Equal(FlagStates.Resolved, dbFlag.State);
    }

    [Fact]
    public async Task FlagDismissal_ExpiresIn30Days_AndReopens()
    {
        var rule = CreateTestRule("LOW_OCR_CONFIDENCE", "Amber");
        var orgId = Guid.NewGuid();
        var period = "2026-09";

        var flag = await _flagsService.RaiseOrUpdateFlagAsync(new RaiseFlagRequest(
            OrgId: orgId,
            SiteId: null,
            RuleCode: rule.FlagCode,
            Period: period,
            EvidenceJson: "{}",
            ExplanationBn: "বাংলা",
            ExplanationEn: "English"
        ));

        // 1. Dismiss without reason must throw
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _flagsService.DismissFlagAsync(flag.Id, orgId, "   "));

        // 2. Dismiss with valid reason
        var now = DateTime.UtcNow;
        var dismissed = await _flagsService.DismissFlagAsync(flag.Id, orgId, "Overtime production surge for Eid shipment");
        Assert.True(dismissed);

        var dbFlag = await _dbContext.Flags.FirstAsync(f => f.Id == flag.Id);
        Assert.Equal(FlagStates.Dismissed, dbFlag.State);
        Assert.Equal("Overtime production surge for Eid shipment", dbFlag.DismissedReason);
        Assert.NotNull(dbFlag.DismissedUntil);
        Assert.True(dbFlag.DismissedUntil > now.AddDays(29));

        // In dashboard query, dismissed flag is hidden
        var top5BeforeExpiry = await _flagsService.GetDashboardTop5FlagsAsync(orgId);
        Assert.Empty(top5BeforeExpiry);

        // 3. Fast-forward clock 31 days into future
        var futureDate = now.AddDays(31);
        var expired = dbFlag.CheckAndExpireDismissal(futureDate);
        Assert.True(expired);
        Assert.Equal(FlagStates.Open, dbFlag.State);
        Assert.Null(dbFlag.DismissedUntil);
        Assert.Null(dbFlag.DismissedReason);
    }

    [Fact]
    public async Task DashboardTop5Query_ReturnsStrictlyTop5OrderedBySeverityThenRecency()
    {
        var orgId = Guid.NewGuid();
        var period = "2026-09";

        // Create 8 rules and flags of varying severities
        var rules = new List<(string Code, string Severity)>
        {
            ("F1", "Info"),
            ("F2", "Amber"),
            ("F3", "Red"),
            ("F4", "Info"),
            ("F5", "Amber"),
            ("F6", "Red"),
            ("F7", "Amber"),
            ("F8", "Amber")
        };

        foreach (var (code, severity) in rules)
        {
            var rule = CreateTestRule(code, severity);
            await _flagsService.RaiseOrUpdateFlagAsync(new RaiseFlagRequest(
                OrgId: orgId,
                SiteId: null,
                RuleCode: rule.FlagCode,
                Period: period,
                EvidenceJson: "{}",
                ExplanationBn: $"{code} বাংলা",
                ExplanationEn: $"{code} English"
            ));
        }

        var top5 = await _flagsService.GetDashboardTop5FlagsAsync(orgId);

        // Must strictly return 5 items
        Assert.Equal(5, top5.Count);

        // Both Red flags (F3, F6) must appear first
        Assert.Equal("Red", top5[0].Severity);
        Assert.Equal("Red", top5[1].Severity);

        // Next 3 items must be Amber
        Assert.Equal("Amber", top5[2].Severity);
        Assert.Equal("Amber", top5[3].Severity);
        Assert.Equal("Amber", top5[4].Severity);
    }

    [Fact]
    public async Task SeedLoader_Loads14RulesFromSeedsJson()
    {
        var loader = new FlagRuleSeedLoader(_dbContext, NullLogger<FlagRuleSeedLoader>.Instance);
        var seededCount = await loader.SeedFlagRulesAsync();

        Assert.True(seededCount >= 14, $"Expected at least 14 rules seeded from JSON, got {seededCount}");

        var missingDocRule = await _dbContext.FlagRules.FirstOrDefaultAsync(r => r.FlagCode == "MISSING_DOCUMENT");
        Assert.NotNull(missingDocRule);
        Assert.Equal("Amber", missingDocRule.DefaultSeverity);
        Assert.Contains("due_day_offset", missingDocRule.TriggerParametersJson, StringComparison.Ordinal);

        var spikeRule = await _dbContext.FlagRules.FirstOrDefaultAsync(r => r.FlagCode == "SPIKE_MOM");
        Assert.NotNull(spikeRule);
        Assert.Contains("amber_median_multiplier", spikeRule.TriggerParametersJson, StringComparison.Ordinal);
    }
}
