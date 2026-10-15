using System.Globalization;
using CarbonBill.Modules.Flags.Contracts;
using CarbonBill.Modules.Flags.Domain;
using CarbonBill.Modules.Flags.Engine;
using CarbonBill.Modules.Flags.Fakes;
using CarbonBill.Modules.Insights.Domain;
using CarbonBill.Modules.Insights.Persistence;
using CarbonBill.Modules.Insights.Services;
using CarbonBill.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarbonBill.UnitTests;

public class InsightsAndFootprintFlagTests
{
    private readonly Guid _testOrgId = Guid.NewGuid();

    // -------------------------------------------------------------
    // 1. SPIKE_MOM EVALUATOR TESTS
    // -------------------------------------------------------------

    [Fact]
    public async Task SpikeMom_FewerThanThreeMonthsHistory_ProducesNoFlag()
    {
        var emissionFake = new FakeEmissionReadModel();
        // Only 2 months of historical data
        emissionFake.SetMonthlyTrend(_testOrgId,
        [
            new MonthlyTrendItem("2026-06", 1000m, 500m, 500m, 0m, 100m),
            new MonthlyTrendItem("2026-07", 1000m, 500m, 500m, 0m, 100m)
        ]);
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 3000m, 3.0m, 3000m, 0m, 1500m, 1500m, 0m, 100m, 10));

        var evaluator = new SpikeMomRuleEvaluator(emissionFake);
        var rule = CreateFlagRule("SPIKE_MOM", "Amber",
            """{"amber_median_multiplier": 1.30, "red_median_multiplier": 1.60, "min_history_months": 3}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));

        Assert.False(result.ShouldFlag, "Spike evaluator must not flag when fewer than 3 months of history exist.");
    }

    [Fact]
    public async Task SpikeMom_EvaluatorBoundaries_TriggersAmberAndRedAppropriately()
    {
        var emissionFake = new FakeEmissionReadModel();
        // 3 trailing months with median 1000 (1000, 1000, 1000)
        emissionFake.SetMonthlyTrend(_testOrgId,
        [
            new MonthlyTrendItem("2026-05", 1000m, 500m, 500m, 0m, 100m),
            new MonthlyTrendItem("2026-06", 1000m, 500m, 500m, 0m, 100m),
            new MonthlyTrendItem("2026-07", 1000m, 500m, 500m, 0m, 100m)
        ]);

        var evaluator = new SpikeMomRuleEvaluator(emissionFake);
        var rule = CreateFlagRule("SPIKE_MOM", "Amber",
            """{"amber_median_multiplier": 1.30, "red_median_multiplier": 1.60, "min_history_months": 3}""");

        // 1. Below 1.3x (1200 <= 1300) -> No flag
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 1200m, 1.2m, 1200m, 0m, 600m, 600m, 0m, 100m, 5));
        var res1 = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.False(res1.ShouldFlag);

        // 2. Between 1.3x and 1.6x (1350 > 1300, <= 1600) -> Amber
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 1350m, 1.35m, 1350m, 0m, 650m, 700m, 0m, 100m, 5));
        var res2 = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.True(res2.ShouldFlag);
        Assert.Equal("Amber", res2.SeverityOverride);

        // 3. Above 1.6x (1650 > 1600) -> Red
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 1650m, 1.65m, 1650m, 0m, 800m, 850m, 0m, 100m, 5));
        var res3 = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.True(res3.ShouldFlag);
        Assert.Equal("Red", res3.SeverityOverride);
    }

    // -------------------------------------------------------------
    // 2. HOTSPOT_DETECTED EVALUATOR TESTS
    // -------------------------------------------------------------

    [Theory]
    [InlineData(40.0, false, null)]
    [InlineData(50.0, false, null)]
    [InlineData(55.0, true, "Info")]
    public async Task HotspotDetected_BoundaryConditions(decimal percentage, bool expectedFlag, string? expectedSeverity)
    {
        var emissionFake = new FakeEmissionReadModel();
        // 3 items to avoid complementary > 50%
        var remainder = (100m - percentage) / 2m;
        emissionFake.SetBreakdown(_testOrgId, "2026-08",
        [
            new ScopeBreakdownItem(2, "Scope 2", "Purchased Electricity", 1000m * (percentage / 100m), percentage, 10000m, "kWh"),
            new ScopeBreakdownItem(1, "Scope 1", "Diesel Generator", 1000m * (remainder / 100m), remainder, 200m, "litre"),
            new ScopeBreakdownItem(1, "Scope 1", "Natural Gas", 1000m * (remainder / 100m), remainder, 100m, "m3")
        ]);

        var evaluator = new HotspotDetectedRuleEvaluator(emissionFake);
        var rule = CreateFlagRule("HOTSPOT_DETECTED", "Info", """{"percentage_of_total_threshold": 50.0}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));

        Assert.Equal(expectedFlag, result.ShouldFlag);
        if (expectedFlag)
        {
            Assert.Equal(expectedSeverity, result.SeverityOverride);
        }
    }

    // -------------------------------------------------------------
    // 3. GENSET_RELIANCE EVALUATOR TESTS
    // -------------------------------------------------------------

    [Theory]
    [InlineData(50.0, 1000.0, false)] // 50L * 3.5 = 175 kWh genset / (1000 + 175) = 14.8% <= 20% -> false
    [InlineData(30.0, 1000.0, false)] // 30L * 3.5 = 105 kWh genset / (1000 + 105) = 9.5% <= 20% -> false
    [InlineData(100.0, 1000.0, true)] // 100L * 3.5 = 350 kWh genset / 1350 = 25.9% > 20% -> true
    public async Task GensetReliance_BoundaryConditions(decimal dieselLitres, decimal gridKwh, bool expectedFlag)
    {
        var docFake = new FakeFlagDocumentReadModel();
        docFake.AddDocument(new ExtractedDocumentRecord(
            Guid.NewGuid(), _testOrgId, null, null, "Generator 1", "diesel", "2026-08",
            "hash1", "Padma Oil", "B-101", dieselLitres, "litre", 0.95f, []));
        docFake.AddDocument(new ExtractedDocumentRecord(
            Guid.NewGuid(), _testOrgId, null, null, "Main Meter", "electricity", "2026-08",
            "hash2", "BREB", "B-102", gridKwh, "kWh", 0.95f, []));

        var emissionFake = new FakeEmissionReadModel();
        var evaluator = new GensetRelianceRuleEvaluator(docFake, emissionFake);
        var rule = CreateFlagRule("GENSET_RELIANCE", "Amber", """{"genset_kwh_percentage_threshold": 20.0}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.Equal(expectedFlag, result.ShouldFlag);
    }

    // -------------------------------------------------------------
    // 4. INTENSITY_ABOVE_PEERS EVALUATOR & BENCHMARK GATING TESTS
    // -------------------------------------------------------------

    [Fact]
    public async Task IntensityAbovePeers_InsufficientPeerCount_HonestGatingSuppressesFlag()
    {
        var pMetricReader = new FakeProductionMetricReader();
        pMetricReader.AddMetric(new ProductionMetricDto(Guid.NewGuid(), _testOrgId, null, "2026-08", "piece", 10000m, DateTime.UtcNow));

        var bReader = new FakeBenchmarkReader();
        // N = 5 (< 10 threshold)
        bReader.AddBenchmark(new BenchmarkSetDto(
            Guid.NewGuid(), "RMG", "Medium", "kg_co2e_per_piece", 0.30m, 0.40m, 0.50m, 0.60m, 5, "Survey", 2024));

        var emissionFake = new FakeEmissionReadModel();
        // High intensity: 10000 kg / 10000 pieces = 1.0 > P90 (0.60), but N < 10
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 10000m, 10.0m, 10000m, 0m, 5000m, 5000m, 0m, 100m, 5));

        var evaluator = new IntensityAbovePeersRuleEvaluator(pMetricReader, bReader, emissionFake);
        var rule = CreateFlagRule("INTENSITY_ABOVE_PEERS", "Amber",
            """{"amber_percentile": 75, "red_percentile": 90, "min_peer_count": 10}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));

        Assert.False(result.ShouldFlag, "Evaluator must never flag when peer count N is below minimum threshold.");
    }

    [Fact]
    public async Task IntensityAbovePeers_SufficientPeers_TriggersAmberOrRed()
    {
        var pMetricReader = new FakeProductionMetricReader();
        pMetricReader.AddMetric(new ProductionMetricDto(Guid.NewGuid(), _testOrgId, null, "2026-08", "piece", 1000m, DateTime.UtcNow));

        var bReader = new FakeBenchmarkReader();
        // N = 25 (>= 10)
        bReader.AddBenchmark(new BenchmarkSetDto(
            Guid.NewGuid(), "RMG", "Medium", "kg_co2e_per_piece", 0.30m, 0.40m, 0.50m, 0.60m, 25, "IFC PaCT", 2024));

        var emissionFake = new FakeEmissionReadModel();
        var evaluator = new IntensityAbovePeersRuleEvaluator(pMetricReader, bReader, emissionFake);
        var rule = CreateFlagRule("INTENSITY_ABOVE_PEERS", "Amber",
            """{"amber_percentile": 75, "red_percentile": 90, "min_peer_count": 10}""");

        // 1. Intensity 0.45 (between P50 0.40 and P75 0.50) -> No flag
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 450m, 0.45m, 450m, 0m, 200m, 250m, 0m, 100m, 5));
        var res1 = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.False(res1.ShouldFlag);

        // 2. Intensity 0.55 (between P75 0.50 and P90 0.60) -> Amber
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 550m, 0.55m, 550m, 0m, 250m, 300m, 0m, 100m, 5));
        var res2 = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.True(res2.ShouldFlag);
        Assert.Equal("Amber", res2.SeverityOverride);

        // 3. Intensity 0.70 (> P90 0.60) -> Red
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 700m, 0.70m, 700m, 0m, 350m, 350m, 0m, 100m, 5));
        var res3 = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.True(res3.ShouldFlag);
        Assert.Equal("Red", res3.SeverityOverride);
    }

    // -------------------------------------------------------------
    // 5. TARGET_DRIFT EVALUATOR TESTS
    // -------------------------------------------------------------

    [Theory]
    [InlineData(1030.0, 1000.0, false)] // 3% drift <= 5% -> no flag
    [InlineData(1080.0, 1000.0, true)]  // 8% drift > 5% -> Amber
    public async Task TargetDrift_BoundaryConditions(decimal actualEmissions, decimal targetEmissions, bool expectedFlag)
    {
        var targetFake = new FakeTargetReadModel();
        targetFake.SetTarget(_testOrgId, "2026-08", targetEmissions);

        var emissionFake = new FakeEmissionReadModel();
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", actualEmissions, actualEmissions / 1000m, actualEmissions, 0m, 500m, 500m, 0m, 100m, 5));

        var evaluator = new TargetDriftRuleEvaluator(emissionFake, targetFake);
        var rule = CreateFlagRule("TARGET_DRIFT", "Amber", """{"max_allowed_drift_percentage": 5.0}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.Equal(expectedFlag, result.ShouldFlag);
        if (expectedFlag)
        {
            Assert.Equal("Amber", result.SeverityOverride);
        }
    }

    // -------------------------------------------------------------
    // 6. REPORT_NOT_READY EVALUATOR TESTS
    // -------------------------------------------------------------

    [Fact]
    public async Task ReportNotReady_AllConfirmedAndHighQuality_ProducesNoFlag()
    {
        var expectedReader = new FakeExpectedDocRuleReader();
        var docReader = new FakeDocumentReadModel();
        var emissionFake = new FakeEmissionReadModel();

        // High quality score 95%, no unconfirmed docs
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 1000m, 1.0m, 950m, 50m, 500m, 500m, 0m, 95.0m, 5));

        var evaluator = new ReportNotReadyRuleEvaluator(expectedReader, docReader, emissionFake);
        var rule = CreateFlagRule("REPORT_NOT_READY", "Red",
            """{"min_data_quality_score": 80.0, "require_zero_missing_documents": true}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.False(result.ShouldFlag);
    }

    [Fact]
    public async Task ReportNotReady_LowDataQualityScore_TriggersRedFlag()
    {
        var expectedReader = new FakeExpectedDocRuleReader();
        var docReader = new FakeDocumentReadModel();
        var emissionFake = new FakeEmissionReadModel();

        // Low quality score 75% (< 80%)
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 1000m, 1.0m, 750m, 250m, 500m, 500m, 0m, 75.0m, 5));

        var evaluator = new ReportNotReadyRuleEvaluator(expectedReader, docReader, emissionFake);
        var rule = CreateFlagRule("REPORT_NOT_READY", "Red",
            """{"min_data_quality_score": 80.0, "require_zero_missing_documents": true}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.True(result.ShouldFlag);
        Assert.Equal("Red", result.SeverityOverride);
    }

    // -------------------------------------------------------------
    // 7. FACTOR_OUTDATED EVALUATOR TESTS
    // -------------------------------------------------------------

    [Theory]
    [InlineData(2025, 2026, false)] // Age = 1 <= 1 -> no flag
    [InlineData(2024, 2026, true)]  // Age = 2 > 1 -> Amber
    public async Task FactorOutdated_BoundaryConditions(int factorYear, int periodYear, bool expectedFlag)
    {
        var factorFake = new FakeFactorRegistryReadModel();
        factorFake.SetActiveYear(factorYear);

        var evaluator = new FactorOutdatedRuleEvaluator(factorFake);
        var rule = CreateFlagRule("FACTOR_OUTDATED", "Amber", """{"max_age_years": 1}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, $"{periodYear}-09"));
        Assert.Equal(expectedFlag, result.ShouldFlag);
        if (expectedFlag)
        {
            Assert.Equal("Amber", result.SeverityOverride);
        }
    }

    // -------------------------------------------------------------
    // 8. OVERRIDE_UNAPPROVED EVALUATOR TESTS
    // -------------------------------------------------------------

    [Fact]
    public async Task OverrideUnapproved_ApprovedWithJustification_ProducesNoFlag()
    {
        var factorFake = new FakeFactorRegistryReadModel();
        factorFake.AddOverride(new FactorOverrideRecord(
            Guid.NewGuid(), Guid.NewGuid(), 0.55m, "Verified by SGS third-party audit", Guid.NewGuid(), true));

        var evaluator = new OverrideUnapprovedRuleEvaluator(factorFake);
        var rule = CreateFlagRule("OVERRIDE_UNAPPROVED", "Red",
            """{"require_justification": true, "require_approver": true}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.False(result.ShouldFlag);
    }

    [Fact]
    public async Task OverrideUnapproved_MissingApproverOrJustification_TriggersRedFlag()
    {
        var factorFake = new FakeFactorRegistryReadModel();
        factorFake.AddOverride(new FactorOverrideRecord(
            Guid.NewGuid(), Guid.NewGuid(), 0.55m, "", Guid.Empty, true));

        var evaluator = new OverrideUnapprovedRuleEvaluator(factorFake);
        var rule = CreateFlagRule("OVERRIDE_UNAPPROVED", "Red",
            """{"require_justification": true, "require_approver": true}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.True(result.ShouldFlag);
        Assert.Equal("Red", result.SeverityOverride);
    }

    // -------------------------------------------------------------
    // 9. POWER_FACTOR_PENALTY EVALUATOR TESTS
    // -------------------------------------------------------------

    [Fact]
    public async Task PowerFactorPenalty_LowPowerFactorOrPenaltyFee_TriggersInfoFlag()
    {
        var docFake = new FakeFlagDocumentReadModel();
        // Power factor 0.82 < 0.90 threshold
        docFake.AddDocument(new ExtractedDocumentRecord(
            Guid.NewGuid(), _testOrgId, null, null, "Main Electricity", "electricity", "2026-08",
            "hash1", "BREB", "B-999", 50000m, "kWh", 0.95f,
            [
                new ExtractedFieldConfidence("power_factor", "0.82", 0.95f),
                new ExtractedFieldConfidence("power_factor_penalty", "4500.00", 0.92f)
            ]));

        var evaluator = new PowerFactorPenaltyRuleEvaluator(docFake);
        var rule = CreateFlagRule("POWER_FACTOR_PENALTY", "Info", """{"min_power_factor": 0.90}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.True(result.ShouldFlag);
        Assert.Equal("Info", result.SeverityOverride);
    }

    [Fact]
    public async Task PowerFactorPenalty_NormalPowerFactor_ProducesNoFlag()
    {
        var docFake = new FakeFlagDocumentReadModel();
        // Power factor 0.95 >= 0.90
        docFake.AddDocument(new ExtractedDocumentRecord(
            Guid.NewGuid(), _testOrgId, null, null, "Main Electricity", "electricity", "2026-08",
            "hash1", "BREB", "B-999", 50000m, "kWh", 0.95f,
            [
                new ExtractedFieldConfidence("power_factor", "0.95", 0.95f)
            ]));

        var evaluator = new PowerFactorPenaltyRuleEvaluator(docFake);
        var rule = CreateFlagRule("POWER_FACTOR_PENALTY", "Info", """{"min_power_factor": 0.90}""");

        var result = await evaluator.EvaluateAsync(rule, new EvaluationContext(_testOrgId, "2026-08"));
        Assert.False(result.ShouldFlag);
    }

    // -------------------------------------------------------------
    // 10. INSIGHTS SERVICE & DASHBOARD INTENSITY TESTS
    // -------------------------------------------------------------

    [Fact]
    public async Task InsightsService_ComputesVerifiedAndIncludingEstimateIntensityVariants()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<InsightsDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new InsightsDbContext(options);
        await db.Database.EnsureCreatedAsync();

        // Add benchmark with N = 32
        db.BenchmarkSets.Add(new BenchmarkSet
        {
            Sector = "RMG",
            SizeBand = "Medium",
            Metric = "kg_co2e_per_piece",
            P25 = 0.310m,
            P50 = 0.415m,
            P75 = 0.520m,
            P90 = 0.650m,
            N = 32,
            Source = "IFC PaCT 2022",
            Year = 2022
        });
        await db.SaveChangesAsync();

        var emissionFake = new FakeEmissionReadModel();
        // Total = 4500 kg, Verified = 3600 kg, Estimated = 900 kg
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 4500m, 4.5m, 3600m, 900m, 2000m, 2500m, 0m, 80m, 8));

        var service = new InsightsService(db, emissionFake, NullLogger<InsightsService>.Instance);

        // Capture production metric: 10,000 pieces
        var captured = await service.CaptureMetricAsync(_testOrgId, null, "2026-08", "piece", 10000m);
        Assert.NotNull(captured);
        Assert.Equal(10000m, captured.Quantity);

        // Calculate dashboard intensity
        var dashboard = await service.GetIntensityDashboardAsync(_testOrgId, "2026-08");

        // FactoryValue = 4500 / 10000 = 0.45 kg CO2e / piece (incl-estimate variant)
        Assert.Equal(0.450000m, dashboard.FactoryValue);

        // VerifiedFactoryValue = 3600 / 10000 = 0.36 kg CO2e / piece (verified-only variant)
        Assert.Equal(0.360000m, dashboard.VerifiedFactoryValue);

        // Benchmark gating: N = 32 >= 10 -> HasBenchmark is true
        Assert.True(dashboard.PeerBenchmark.HasBenchmark);
        Assert.Null(dashboard.PeerBenchmark.Message);
        Assert.Equal(0.310m, dashboard.PeerBenchmark.P25);
        Assert.Equal(0.415m, dashboard.PeerBenchmark.P50);
        Assert.Equal(0.520m, dashboard.PeerBenchmark.P75);
        Assert.Equal(0.650m, dashboard.PeerBenchmark.P90);
        Assert.Equal(32, dashboard.PeerBenchmark.N);
    }

    [Fact]
    public async Task InsightsService_BenchmarkGating_ReturnsNoBenchmarkYetWhenSampleBelowTen()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<InsightsDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new InsightsDbContext(options);
        await db.Database.EnsureCreatedAsync();

        // Add benchmark with N = 7 (< 10 threshold)
        db.BenchmarkSets.Add(new BenchmarkSet
        {
            Sector = "TextileDyeing",
            SizeBand = "Small",
            Metric = "kg_co2e_per_kg_fabric",
            P25 = 1.25m,
            P50 = 1.85m,
            P75 = 2.45m,
            P90 = 3.10m,
            N = 7,
            Source = "Small Pilot",
            Year = 2023
        });
        await db.SaveChangesAsync();

        var emissionFake = new FakeEmissionReadModel();
        emissionFake.SetSummary(_testOrgId, "2026-08", new EmissionSummaryDto(
            "2026-08", 2000m, 2.0m, 2000m, 0m, 1000m, 1000m, 0m, 100m, 4));

        var service = new InsightsService(db, emissionFake, NullLogger<InsightsService>.Instance);
        await service.CaptureMetricAsync(_testOrgId, null, "2026-08", "kg_fabric", 1000m);

        var dashboard = await service.GetIntensityDashboardAsync(_testOrgId, "2026-08", sector: "TextileDyeing", sizeBand: "Small");

        // Benchmark gating must be enforced:
        Assert.False(dashboard.PeerBenchmark.HasBenchmark);
        Assert.Equal("no benchmark yet", dashboard.PeerBenchmark.Message);
        Assert.Equal("প্রতুল তথ্য এখনও পাওয়া যায়নি", dashboard.PeerBenchmark.MessageBn);
        Assert.Null(dashboard.PeerBenchmark.P25);
        Assert.Null(dashboard.PeerBenchmark.P50);
        Assert.Null(dashboard.PeerBenchmark.P75);
        Assert.Null(dashboard.PeerBenchmark.P90);
    }

    private static FlagRule CreateFlagRule(string code, string defaultSeverity, string triggerParametersJson) =>
        new()
        {
            Id = Guid.NewGuid(),
            FlagCode = code,
            Family = "Test",
            NameBn = "টেস্ট ফ্ল্যাগ",
            NameEn = "Test Flag",
            DefaultSeverity = defaultSeverity,
            TriggerParametersJson = triggerParametersJson,
            SuggestedActionBn = "পদক্ষেপ নিন",
            SuggestedActionEn = "Take action",
            IsActive = true
        };
}
