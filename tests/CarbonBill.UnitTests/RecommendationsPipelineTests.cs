using System.Text;
using System.Text.Json;
using CarbonBill.Modules.Recommendations.Domain;
using CarbonBill.Modules.Recommendations.Loader;
using CarbonBill.Modules.Recommendations.Persistence;
using CarbonBill.Modules.Recommendations.Pipeline;
using CarbonBill.Modules.Recommendations.Pipeline.Steps;
using CarbonBill.Modules.Recommendations.Services;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarbonBill.UnitTests;

public class RecommendationsPipelineTests
{
    private static MeasureSource CreateFakeSource(string code = "FAKE_SOURCE_PACT_2024")
    {
        return new MeasureSource
        {
            Id = Guid.NewGuid(),
            SourceCode = code,
            Title = "FAKE PaCT Industrial Energy Audit Benchmark 2024",
            Institution = "International Finance Corporation (Test)",
            Year = 2024,
            LicenseTerms = "Public test license",
            CandidateStatus = "VERIFY: Unit Test Source"
        };
    }

    private static Measure CreateFakeLedMeasure(MeasureSource source)
    {
        return new Measure
        {
            Id = Guid.NewGuid(),
            MeasureCode = "FAKE_MEASURE_LED_RETROFIT",
            NameEn = "FAKE LED Retrofit and Skylights",
            NameBn = "FAKE টেস্ট এলইডি লাইটিং ব্যবস্থা",
            Category = "Lighting",
            ApplicableSectorsJson = "[\"RMG\", \"TextileDyeing\", \"General\"]",
            ApplicabilityRulesJson = "{\"grid_electricity\": true}",
            SavingLow = 0.0800m,
            SavingTypical = 0.1200m,
            SavingHigh = 0.1800m,
            CapexLow = 100000.00m,
            CapexHigh = 160000.00m,
            CapexUnit = "BDT/floor",
            LifetimeYears = 5,
            EvidenceGrade = "A",
            SourceId = source.Id,
            Source = source,
            LocalNotes = "Test LED measure"
        };
    }

    private static Measure CreateFakeSolarMeasure(MeasureSource source)
    {
        return new Measure
        {
            Id = Guid.NewGuid(),
            MeasureCode = "FAKE_MEASURE_ROOFTOP_SOLAR",
            NameEn = "FAKE Rooftop Solar PV",
            NameBn = "FAKE রুফটপ সোলার পিভি",
            Category = "SolarPV",
            ApplicableSectorsJson = "[\"RMG\", \"General\"]",
            ApplicabilityRulesJson = "{\"building_ownership\": \"Owned\", \"roof_area_sqft_gt\": 5000}",
            SavingLow = 0.1500m,
            SavingTypical = 0.2000m,
            SavingHigh = 0.2500m,
            CapexLow = 1500000.00m,
            CapexHigh = 2200000.00m,
            CapexUnit = "BDT/system",
            LifetimeYears = 20,
            EvidenceGrade = "A",
            SourceId = source.Id,
            Source = source,
            LocalNotes = "Test Solar measure"
        };
    }

    [Fact]
    public void Step1_ProfileIngestion_DerivesTariffFromBills_AndFallsBackToDefaults()
    {
        var step = new ProfileIngestionStep();
        var profile = new FacilityProfile
        {
            BaselineMonthlyKwh = 20000m,
            BaselineMonthlyDieselLitres = 1000m,
            BaselineMonthlyGasM3 = 500m,
            ElectricityTariffBdtPerKwh = 10.50m,
            DieselTariffBdtPerLitre = 108.00m,
            GasTariffBdtPerM3 = 30.00m
        };

        // 1. With actual bills: 250,000 BDT for 20,000 kWh -> 12.50 BDT/kWh
        var baselineFromBills = step.Execute(
            profile,
            billTotalElectricityCostBdt: 250000m,
            billTotalKwh: 20000m,
            billTotalDieselCostBdt: 120000m,
            billTotalDieselLitres: 1000m);

        Assert.Equal(240000m, baselineFromBills.AnnualKwh); // 20000 * 12
        Assert.Equal(12000m, baselineFromBills.AnnualDieselLitres);
        Assert.Equal(6000m, baselineFromBills.AnnualGasM3);
        Assert.Equal(12.50m, baselineFromBills.ElectricityTariffBdtPerKwh);
        Assert.Equal(120.00m, baselineFromBills.DieselTariffBdtPerLitre);

        // 2. Without bills: falls back to profile defaults
        var baselineDefaults = step.Execute(profile);
        Assert.Equal(10.50m, baselineDefaults.ElectricityTariffBdtPerKwh);
        Assert.Equal(108.00m, baselineDefaults.DieselTariffBdtPerLitre);
        Assert.Equal(30.00m, baselineDefaults.GasTariffBdtPerM3);
    }

    [Fact]
    public void Step2_Eligibility_RejectsSolar_WhenRoofAreaUnknownOrZero()
    {
        var step = new EligibilityFilterStep();
        var source = CreateFakeSource();
        var solar = CreateFakeSolarMeasure(source);

        var profileUnknownRoof = new FacilityProfile
        {
            BuildingOwnership = "Owned",
            RoofAreaSqft = null // unknown
        };

        bool isEligibleUnknown = step.Evaluate(solar, profileUnknownRoof, out var reasonUnknown);
        Assert.False(isEligibleUnknown);
        Assert.Contains("verified roof area", reasonUnknown, StringComparison.OrdinalIgnoreCase);

        var profileZeroRoof = new FacilityProfile
        {
            BuildingOwnership = "Owned",
            RoofAreaSqft = 0m
        };

        bool isEligibleZero = step.Evaluate(solar, profileZeroRoof, out var reasonZero);
        Assert.False(isEligibleZero);
        Assert.Contains("verified roof area", reasonZero, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Step2_Eligibility_RejectsSolar_WhenBuildingRented()
    {
        var step = new EligibilityFilterStep();
        var source = CreateFakeSource();
        var solar = CreateFakeSolarMeasure(source);

        var profileRented = new FacilityProfile
        {
            BuildingOwnership = "Rented",
            RoofAreaSqft = 30000m
        };

        bool isEligible = step.Evaluate(solar, profileRented, out var reason);
        Assert.False(isEligible);
        Assert.Contains("Rented properties ineligible", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Step2_Eligibility_AcceptsSolar_WhenOwnedAndRoofKnown()
    {
        var step = new EligibilityFilterStep();
        var source = CreateFakeSource();
        var solar = CreateFakeSolarMeasure(source);

        var profileValid = new FacilityProfile
        {
            BuildingOwnership = "Owned",
            RoofAreaSqft = 25000m
        };

        bool isEligible = step.Evaluate(solar, profileValid, out var reason);
        Assert.True(isEligible);
        Assert.Null(reason);
    }

    [Fact]
    public void Step3_CarbonImpact_ComputesRanges_AndEnforcesOrdering()
    {
        var step = new CarbonImpactStep();
        var source = CreateFakeSource();
        var led = CreateFakeLedMeasure(source);

        var baseline = new EnergyActivityBaseline(
            AnnualKwh: 360000m,
            AnnualDieselLitres: 12000m,
            AnnualGasM3: 0m,
            ElectricityTariffBdtPerKwh: 11.00m,
            DieselTariffBdtPerLitre: 108.00m,
            GasTariffBdtPerM3: 30.00m);

        var result = step.Calculate(led, baseline);

        // Low: 360,000 * 0.08 * 0.62 / 1000 = 17.856 tCO2e
        // Typical: 360,000 * 0.12 * 0.62 / 1000 = 26.784 tCO2e
        // High: 360,000 * 0.18 * 0.62 / 1000 = 40.176 tCO2e
        Assert.Equal(17.856m, result.Low);
        Assert.Equal(26.784m, result.Typical);
        Assert.Equal(40.176m, result.High);

        Assert.True(result.Low <= result.Typical);
        Assert.True(result.Typical <= result.High);
    }

    [Fact]
    public void Step4_FinancialModeling_PreservesNegativeCostPerTonne()
    {
        var step = new FinancialModelingStep();
        var source = CreateFakeSource();

        // High saving, modest capex -> annual savings exceed annualized capex -> NEGATIVE cost per tonne
        var measure = new Measure
        {
            Category = "Lighting",
            SavingLow = 0.10m,
            SavingTypical = 0.15m,
            SavingHigh = 0.20m,
            CapexLow = 120000m,
            CapexHigh = 180000m, // avg capex = 150,000
            LifetimeYears = 5    // annualised capex = 30,000
        };

        var baseline = new EnergyActivityBaseline(
            AnnualKwh: 100000m,
            AnnualDieselLitres: 0m,
            AnnualGasM3: 0m,
            ElectricityTariffBdtPerKwh: 10.00m,
            DieselTariffBdtPerLitre: 108.00m,
            GasTariffBdtPerM3: 30.00m);

        // Avoided typical tCO2e = 100,000 * 0.15 * 0.62 / 1000 = 9.3 tCO2e
        var avoidedTco2e = new SavingsRange(6.2m, 9.3m, 12.4m);

        var financial = step.Calculate(measure, baseline, avoidedTco2e);

        // Typical annual savings = 100,000 * 0.15 * 10 = 150,000 BDT
        // Avg Capex = 150,000 BDT
        // Annualised Capex = 150,000 / 5 = 30,000 BDT
        // Payback = 150,000 / 150,000 = 1.00 years
        // Cost per tCO2e = (30,000 - 150,000) / 9.3 = -120,000 / 9.3 = -12,903.23 BDT/tCO2e
        Assert.Equal(150000m, financial.AnnualSavingsBdt.Typical);
        Assert.Equal(1.00m, financial.PaybackYears);
        Assert.True(financial.CostPerTco2e < 0m, "Cost per tCO2e must be negative when annual savings exceed annualized capex.");
        Assert.Equal(-12903.23m, financial.CostPerTco2e);
    }

    [Fact]
    public void Step5_RealismFilter_FlagsNeedsEnergyAudit_WhenCapexExceedsSpendThreshold()
    {
        var step = new RealismFilterStep(auditThresholdBdt: 1000000m);
        var source = CreateFakeSource();

        var highCapexMeasure = CreateFakeSolarMeasure(source);
        var profile = new FacilityProfile { MaxBudgetBdt = 5000000m };
        var highCapexFinancial = new FinancialResult(
            AnnualSavingsBdt: new SavingsRange(100000, 200000, 300000),
            CapexBdt: new CapexRange(1500000, 2200000),
            PaybackYears: 9.25m,
            CostPerTco2e: 500m,
            AnnualisedCapexBdt: 92500m);

        var realismHigh = step.Evaluate(highCapexMeasure, profile, highCapexFinancial);
        Assert.True(realismHigh.IsRealistic);
        Assert.True(realismHigh.NeedsEnergyAudit);

        var modestMeasure = CreateFakeLedMeasure(source);
        var modestFinancial = new FinancialResult(
            AnnualSavingsBdt: new SavingsRange(30000, 50000, 70000),
            CapexBdt: new CapexRange(100000, 160000),
            PaybackYears: 2.6m,
            CostPerTco2e: -2000m,
            AnnualisedCapexBdt: 26000m);

        var realismModest = step.Evaluate(modestMeasure, profile, modestFinancial);
        Assert.True(realismModest.IsRealistic);
        Assert.False(realismModest.NeedsEnergyAudit);
    }

    [Fact]
    public void Step6_Ranking_AssignsRank1_ToHighestCompositeScore()
    {
        var ranking = new RankingStep();
        var source = CreateFakeSource();

        var candidate1 = new CandidateMeasureEvaluation
        {
            Measure = CreateFakeLedMeasure(source),
            PaybackYears = 1.2m, // fast payback
            AvoidedTco2e = new SavingsRange(10, 25, 40),
            CapexBdt = new CapexRange(100000, 160000),
            IsRealistic = true
        };

        var candidate2 = new CandidateMeasureEvaluation
        {
            Measure = CreateFakeSolarMeasure(source),
            PaybackYears = 8.5m, // longer payback
            AvoidedTco2e = new SavingsRange(20, 50, 70),
            CapexBdt = new CapexRange(1500000, 2200000),
            IsRealistic = true
        };

        var candidates = new List<CandidateMeasureEvaluation> { candidate2, candidate1 };
        ranking.Rank(candidates);

        // candidate1 has much faster payback, low capex -> higher composite score
        Assert.Equal(1, candidate1.Rank);
        Assert.Equal(2, candidate2.Rank);
        Assert.True(candidate1.CompositeScore > candidate2.CompositeScore);
    }

    [Fact]
    public void Step7_Explanation_GeneratesBilingualCards_WithBanglaDigits()
    {
        var step = new ExplanationStep();
        var source = CreateFakeSource();
        var measure = CreateFakeLedMeasure(source);

        var candidate = new CandidateMeasureEvaluation
        {
            Measure = measure,
            CapexBdt = new CapexRange(100000, 160000),
            NeedsEnergyAudit = false
        };

        var profile = new FacilityProfile();
        var baseline = new EnergyActivityBaseline(
            AnnualKwh: 240000m,
            AnnualDieselLitres: 12000m,
            AnnualGasM3: 0m,
            ElectricityTariffBdtPerKwh: 10.50m,
            DieselTariffBdtPerLitre: 108.00m,
            GasTariffBdtPerM3: 30.00m);

        step.GenerateCards(candidate, profile, baseline);

        Assert.False(string.IsNullOrWhiteSpace(candidate.ExplanationEn));
        Assert.False(string.IsNullOrWhiteSpace(candidate.ExplanationBn));
        Assert.False(string.IsNullOrWhiteSpace(candidate.FinancingNoteEn));
        Assert.False(string.IsNullOrWhiteSpace(candidate.FinancingNoteBn));
        Assert.False(string.IsNullOrWhiteSpace(candidate.AssumptionsEn));
        Assert.False(string.IsNullOrWhiteSpace(candidate.AssumptionsBn));
        Assert.False(string.IsNullOrWhiteSpace(candidate.NextStepEn));
        Assert.False(string.IsNullOrWhiteSpace(candidate.NextStepBn));

        // Verify Bangla digits presence (e.g. '২', '৪', '০', '%')
        Assert.Contains("বিদ্যুৎ", candidate.ExplanationBn);
        Assert.Contains("০", candidate.ExplanationBn);
        Assert.Contains("Direct operational budget financing", candidate.FinancingNoteEn);

        // Test high capex triggers IDCOL / Bangladesh Bank green finance note
        candidate.CapexBdt = new CapexRange(1500000m, 2200000m);
        candidate.NeedsEnergyAudit = true;
        step.GenerateCards(candidate, profile, baseline);
        Assert.Contains("IDCOL", candidate.FinancingNoteEn);
        Assert.Contains("ইডকল", candidate.FinancingNoteBn);
    }

    [Fact]
    public void Step8_CloseTheLoop_EnforcesReasonForNotFeasible_AndCalculatesRealisedSavingsOnDone()
    {
        var step = new CloseTheLoopStep();
        var source = CreateFakeSource();
        var measure = CreateFakeLedMeasure(source);

        var recommendation = new Recommendation
        {
            OrgId = Guid.NewGuid(),
            MeasureId = measure.Id,
            Measure = measure,
            Status = RecommendationStatuses.Suggested
        };

        var profile = new FacilityProfile
        {
            BaselineMonthlyKwh = 20000m,
            ElectricityTariffBdtPerKwh = 10.50m
        };

        // 1. NotFeasible with empty reason must throw
        Assert.Throws<InvalidOperationException>(() =>
            step.UpdateStatus(recommendation, new StatusUpdateRequest(Status: "NotFeasible", Reason: ""), profile));

        // 2. NotFeasible with valid reason succeeds
        step.UpdateStatus(recommendation, new StatusUpdateRequest(Status: "NotFeasible", Reason: "Structural limitation in building roof"), profile);
        Assert.Equal(RecommendationStatuses.NotFeasible, recommendation.Status);
        Assert.Equal("Structural limitation in building roof", recommendation.StatusReason);

        // 3. Planned status
        step.UpdateStatus(recommendation, new StatusUpdateRequest(Status: "Planned"), profile);
        Assert.Equal(RecommendationStatuses.Planned, recommendation.Status);

        // 4. Done status with post-intervention monthly energy units:
        // Baseline 20,000 kWh -> Post 17,500 kWh (monthly savings = 2,500 kWh)
        // Annual savings = 2,500 * 12 = 30,000 kWh
        // Tariff = 10.50 BDT/kWh -> Realised BDT = 30,000 * 10.50 = 315,000 BDT
        // Realised tCO2e = (30,000 * 0.62) / 1000 = 18.6 tCO2e
        step.UpdateStatus(recommendation, new StatusUpdateRequest(
            Status: "Done",
            Reason: "Installation completed by Delta Engineering Ltd.",
            PostInterventionMonthlyEnergyUnits: 17500m), profile);

        Assert.Equal(RecommendationStatuses.Done, recommendation.Status);
        Assert.NotNull(recommendation.CompletedAtUtc);
        Assert.Equal(315000.00m, recommendation.RealisedSavingBdt);
        Assert.Equal(18.600m, recommendation.RealisedTco2eAvoided);
        Assert.True(recommendation.EvidenceUpgradeRecorded);
        Assert.Contains("Candidate for library evidence grade upgrade review", recommendation.EvidenceNotes);
    }

    [Fact]
    public async Task DatasetLoader_ValidationFails_WhenNumericFieldLacksSource()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<RecommendationsDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new RecommendationsDbContext(options);
        dbContext.Database.EnsureCreated();

        var loader = new MeasureDatasetLoader(dbContext, NullLogger<MeasureDatasetLoader>.Instance);

        // JSON has numbers, but source_code does not match any existing source in the DB
        var invalidJson = @"[
            {
                ""measure_code"": ""INVALID_NO_SOURCE"",
                ""name_en"": ""Measure without source"",
                ""name_bn"": ""সোর্সবিহীন মেজার"",
                ""category"": ""Lighting"",
                ""saving_low"": 0.05,
                ""saving_typical"": 0.10,
                ""saving_high"": 0.15,
                ""capex_low"": 50000,
                ""capex_high"": 80000,
                ""lifetime_years"": 5,
                ""evidence_grade"": ""B"",
                ""source_code"": ""NON_EXISTENT_SOURCE""
            }
        ]";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidJson));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(stream));
        Assert.Contains("Every number must have a verifiable source row", ex.Message);
    }

    [Fact]
    public async Task DatasetLoader_ValidationFails_WhenRangesAreInverted()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<RecommendationsDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new RecommendationsDbContext(options);
        dbContext.Database.EnsureCreated();

        // Seed a valid source first
        dbContext.MeasureSources.Add(new MeasureSource
        {
            SourceCode = "VALID_SOURCE",
            Title = "Valid Source Document",
            Institution = "IFC",
            Year = 2024
        });
        await dbContext.SaveChangesAsync();

        var loader = new MeasureDatasetLoader(dbContext, NullLogger<MeasureDatasetLoader>.Instance);

        // Inverted savings range: Low (0.25) > Typical (0.10)
        var invertedSavingsJson = @"[
            {
                ""measure_code"": ""INVERTED_SAVINGS"",
                ""name_en"": ""Inverted Savings"",
                ""name_bn"": ""ভুল সেভিংস রেঞ্জ"",
                ""category"": ""Motors"",
                ""saving_low"": 0.25,
                ""saving_typical"": 0.10,
                ""saving_high"": 0.30,
                ""capex_low"": 50000,
                ""capex_high"": 80000,
                ""lifetime_years"": 5,
                ""evidence_grade"": ""B"",
                ""source_code"": ""VALID_SOURCE""
            }
        ]";

        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes(invertedSavingsJson));
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(stream1));
        Assert.Contains("Saving ranges must satisfy Low", ex1.Message);

        // Inverted capex range: Low (90000) > High (50000)
        var invertedCapexJson = @"[
            {
                ""measure_code"": ""INVERTED_CAPEX"",
                ""name_en"": ""Inverted Capex"",
                ""name_bn"": ""ভুল ক্যাপেক্স রেঞ্জ"",
                ""category"": ""Motors"",
                ""saving_low"": 0.05,
                ""saving_typical"": 0.10,
                ""saving_high"": 0.15,
                ""capex_low"": 90000,
                ""capex_high"": 50000,
                ""lifetime_years"": 5,
                ""evidence_grade"": ""B"",
                ""source_code"": ""VALID_SOURCE""
            }
        ]";

        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes(invertedCapexJson));
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(stream2));
        Assert.Contains("Capex ranges must satisfy Low", ex2.Message);
    }

    [Fact]
    public async Task DatasetLoader_ValidationFails_WhenEvidenceGradeIsInvalid()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<RecommendationsDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new RecommendationsDbContext(options);
        dbContext.Database.EnsureCreated();

        dbContext.MeasureSources.Add(new MeasureSource
        {
            SourceCode = "VALID_SOURCE",
            Title = "Valid Source Document",
            Institution = "IFC",
            Year = 2024
        });
        await dbContext.SaveChangesAsync();

        var loader = new MeasureDatasetLoader(dbContext, NullLogger<MeasureDatasetLoader>.Instance);

        var invalidGradeJson = @"[
            {
                ""measure_code"": ""INVALID_GRADE"",
                ""name_en"": ""Invalid Grade"",
                ""name_bn"": ""ভুল গ্রেড"",
                ""category"": ""Lighting"",
                ""saving_low"": 0.05,
                ""saving_typical"": 0.10,
                ""saving_high"": 0.15,
                ""capex_low"": 50000,
                ""capex_high"": 80000,
                ""lifetime_years"": 5,
                ""evidence_grade"": ""Z"",
                ""source_code"": ""VALID_SOURCE""
            }
        ]";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidGradeJson));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync(stream));
        Assert.Contains("Allowed grades are 'A', 'B', or 'C'", ex.Message);
    }

    [Fact]
    public async Task EndToEndPipeline_WithFakeMeasures_GeneratesAndStoresRecommendations()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var tenantId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetContext(tenantId, Guid.NewGuid());

        var options = new DbContextOptionsBuilder<RecommendationsDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new RecommendationsDbContext(options, tenantContext);
        dbContext.Database.EnsureCreated();

        // 1. Seed Source and FAKE measures
        var source = CreateFakeSource();
        dbContext.MeasureSources.Add(source);
        await dbContext.SaveChangesAsync();

        var led = CreateFakeLedMeasure(source);
        var solar = CreateFakeSolarMeasure(source);
        dbContext.Measures.AddRange(led, solar);
        await dbContext.SaveChangesAsync();

        // 2. Assemble Pipeline
        var pipeline = new RecommendationPipeline(
            new ProfileIngestionStep(),
            new EligibilityFilterStep(),
            new CarbonImpactStep(),
            new FinancialModelingStep(),
            new RealismFilterStep(1000000m),
            new RankingStep(),
            new ExplanationStep(),
            new CloseTheLoopStep());

        var service = new RecommendationsService(dbContext, pipeline, NullLogger<RecommendationsService>.Instance);

        // 3. Generate recommendations
        var response = await service.GetRecommendationsResponseAsync(tenantId);

        Assert.NotNull(response);
        Assert.True(response.Top5.Count >= 1);
        Assert.True(response.All.Count >= 1);
        Assert.True(response.Summary.TotalTypicalSavingsBdt > 0m);

        var firstRec = response.Top5[0];
        Assert.Equal(1, firstRec.Rank);
        Assert.Equal(RecommendationStatuses.Suggested, firstRec.Status);
        Assert.True(firstRec.SavingsBdtLow <= firstRec.SavingsBdtTypical);
        Assert.True(firstRec.SavingsBdtTypical <= firstRec.SavingsBdtHigh);

        // 4. Update status to Planned
        var updatedPlanned = await service.UpdateStatusAsync(tenantId, firstRec.Id, new StatusUpdateRequest(Status: "Planned"));
        Assert.Equal(RecommendationStatuses.Planned, updatedPlanned.Status);

        // 5. Update status to Done with post intervention units
        var updatedDone = await service.UpdateStatusAsync(tenantId, firstRec.Id, new StatusUpdateRequest(
            Status: "Done",
            Reason: "Installed with local team",
            PostInterventionMonthlyEnergyUnits: 25000m));
        Assert.Equal(RecommendationStatuses.Done, updatedDone.Status);
        Assert.NotNull(updatedDone.CompletedAtUtc);
        Assert.True(updatedDone.EvidenceUpgradeRecorded);
    }
}
