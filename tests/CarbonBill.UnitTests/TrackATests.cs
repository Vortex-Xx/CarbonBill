using CarbonBill.Modules.ActivityUnits.Persistence;
using CarbonBill.Modules.ActivityUnits.Services;
using CarbonBill.Modules.Audit.Persistence;
using CarbonBill.Modules.Audit.Services;
using CarbonBill.Modules.Calculation.Persistence;
using CarbonBill.Modules.Calculation.Services;
using CarbonBill.Modules.FactorRegistry.Domain;
using CarbonBill.Modules.FactorRegistry.Persistence;
using CarbonBill.Modules.FactorRegistry.Services;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.Modules.Onboarding.Domain;
using CarbonBill.Modules.Onboarding.Persistence;
using CarbonBill.Modules.Onboarding.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarbonBill.UnitTests;

public sealed class TrackATests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenantContext;
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private readonly ActivityUnitsDbContext _unitsDb;
    private readonly FactorRegistryDbContext _factorDb;
    private readonly CalculationDbContext _calcDb;
    private readonly AuditDbContext _auditDb;
    private readonly OnboardingDbContext _onbDb;
    private readonly IdentityTenancyDbContext _identDb;

    public TrackATests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _tenantContext = new TenantContext();
        _tenantContext.SetContext(_orgId, _userId);

        var unitsOptions = new DbContextOptionsBuilder<ActivityUnitsDbContext>().UseSqlite(_connection).Options;
        var factorOptions = new DbContextOptionsBuilder<FactorRegistryDbContext>().UseSqlite(_connection).Options;
        var calcOptions = new DbContextOptionsBuilder<CalculationDbContext>().UseSqlite(_connection).Options;
        var auditOptions = new DbContextOptionsBuilder<AuditDbContext>().UseSqlite(_connection).Options;
        var onbOptions = new DbContextOptionsBuilder<OnboardingDbContext>().UseSqlite(_connection).Options;
        var identOptions = new DbContextOptionsBuilder<IdentityTenancyDbContext>().UseSqlite(_connection).Options;

        _unitsDb = new ActivityUnitsDbContext(unitsOptions, _tenantContext);
        _factorDb = new FactorRegistryDbContext(factorOptions, _tenantContext);
        _calcDb = new CalculationDbContext(calcOptions, _tenantContext);
        _auditDb = new AuditDbContext(auditOptions, _tenantContext);
        _onbDb = new OnboardingDbContext(onbOptions, _tenantContext);
        _identDb = new IdentityTenancyDbContext(identOptions, _tenantContext);

        _unitsDb.Database.EnsureCreated();
        _unitsDb.Database.ExecuteSqlRaw(_factorDb.Database.GenerateCreateScript());
        _unitsDb.Database.ExecuteSqlRaw(_calcDb.Database.GenerateCreateScript());
        _unitsDb.Database.ExecuteSqlRaw(_auditDb.Database.GenerateCreateScript());
        _unitsDb.Database.ExecuteSqlRaw(_onbDb.Database.GenerateCreateScript());
        _unitsDb.Database.ExecuteSqlRaw(_identDb.Database.GenerateCreateScript());
    }

    public void Dispose()
    {
        _unitsDb.Dispose();
        _factorDb.Dispose();
        _calcDb.Dispose();
        _auditDb.Dispose();
        _onbDb.Dispose();
        _identDb.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task UnitConverter_ConvertsStandardAndCustomUnitsAccurately()
    {
        var converter = new UnitConverter(_unitsDb);

        // 1. MWh -> kWh (* 1000)
        var mwhResult = await converter.ConvertToCanonicalAsync(5.5m, "MWh", "grid_electricity");
        Assert.Equal(5500.0m, mwhResult.CanonicalQuantity);
        Assert.Equal("kWh", mwhResult.CanonicalUnit);
        Assert.Equal(1000m, mwhResult.ConversionFactor);

        // 2. Gallons -> Litres (* 3.78541)
        var galResult = await converter.ConvertToCanonicalAsync(100m, "gallon", "diesel");
        Assert.Equal(378.541m, galResult.CanonicalQuantity);
        Assert.Equal("litre", galResult.CanonicalUnit);

        // 3. Cft -> m3 (* 0.0283168)
        var cftResult = await converter.ConvertToCanonicalAsync(10000m, "cft", "gas");
        Assert.Equal(283.168m, cftResult.CanonicalQuantity);
        Assert.Equal("m3", cftResult.CanonicalUnit);

        // 4. LPG Cylinders -> kg (* 12)
        var cylResult = await converter.ConvertToCanonicalAsync(10m, "cylinder_12kg", "lpg");
        Assert.Equal(120m, cylResult.CanonicalQuantity);
        Assert.Equal("kg", cylResult.CanonicalUnit);
    }

    [Fact]
    public async Task FactorLookup_RespectsTenantOverrideHierarchy()
    {
        var factorSet = new FactorSet
        {
            Name = "IGES Bangladesh 2024",
            Year = 2024,
            IsPublished = true
        };
        _factorDb.FactorSets.Add(factorSet);
        await _factorDb.SaveChangesAsync();

        var defaultFactor = new EmissionFactor
        {
            FactorSetId = factorSet.Id,
            ActivityType = "grid_electricity",
            FuelOrMode = "grid",
            Unit = "kWh",
            Co2eFactor = 0.621000m,
            Scope = 2
        };
        _factorDb.EmissionFactors.Add(defaultFactor);
        await _factorDb.SaveChangesAsync();

        var lookup = new FactorLookupService(_factorDb);

        // 1. Default lookup before override
        var resolvedDefault = await lookup.ResolveFactorAsync(_orgId, "grid_electricity");
        Assert.NotNull(resolvedDefault);
        Assert.Equal(0.621000m, resolvedDefault.Co2eFactor);
        Assert.False(resolvedDefault.IsOverridden);

        // 2. Add tenant override
        _factorDb.FactorOverrides.Add(new FactorOverride
        {
            OrgId = _orgId,
            EmissionFactorId = defaultFactor.Id,
            OverrideValue = 0.585000m,
            Justification = "Verified captive solar co-generation mix",
            ApprovedByUserId = _userId,
            IsActive = true
        });
        await _factorDb.SaveChangesAsync();

        // 3. Lookup after override -> should return overridden factor
        var resolvedOverride = await lookup.ResolveFactorAsync(_orgId, "grid_electricity");
        Assert.NotNull(resolvedOverride);
        Assert.Equal(0.585000m, resolvedOverride.Co2eFactor);
        Assert.True(resolvedOverride.IsOverridden);
        Assert.Equal("Verified captive solar co-generation mix", resolvedOverride.Justification);
    }

    [Fact]
    public async Task ActivityWriter_RecordsActivityCalculatesEmissionsAndAuditsAtomically()
    {
        var docId = Guid.NewGuid();

        // Seed Grid Factor
        var factorSet = new FactorSet { Name = "IGES 2024", Year = 2024, IsPublished = true };
        _factorDb.FactorSets.Add(factorSet);
        await _factorDb.SaveChangesAsync();

        _factorDb.EmissionFactors.Add(new EmissionFactor
        {
            FactorSetId = factorSet.Id,
            ActivityType = "electricity",
            Unit = "kWh",
            Co2eFactor = 0.621000m,
            Scope = 2
        });
        await _factorDb.SaveChangesAsync();

        var unitConverter = new UnitConverter(_unitsDb);
        var factorLookup = new FactorLookupService(_factorDb);
        var auditService = new AuditLogService(_auditDb, _tenantContext, NullLogger<AuditLogService>.Instance);

        var writer = new ActivityWriter(
            _unitsDb,
            _calcDb,
            unitConverter,
            factorLookup,
            auditService,
            NullLogger<ActivityWriter>.Instance);

        var request = new ConfirmedActivityRequest(
            OrgId: _orgId,
            SiteId: Guid.NewGuid(),
            AssetId: Guid.NewGuid(),
            DocumentId: docId,
            ActivityType: "electricity",
            Quantity: 10m, // 10 MWh
            Unit: "MWh",
            TotalCostBdt: 85000.00m,
            BillingPeriod: "2026-09",
            IsEstimated: false);

        var result = await writer.RecordConfirmedActivityAsync(request);
        Assert.True(result.IsSuccess);

        // Verify ActivityRecord created with canonical units
        var activity = await _unitsDb.ActivityRecords.FirstOrDefaultAsync(a => a.DocumentId == docId);
        Assert.NotNull(activity);
        Assert.Equal(10000m, activity.QuantityStandard); // 10 MWh = 10,000 kWh
        Assert.Equal("kWh", activity.StandardUnit);
        Assert.Equal(85000.00m, activity.TotalCostBdt);
        Assert.Equal(8.50m, activity.UnitCostBdt); // 85000 / 10000 = 8.50 BDT/kWh

        // Verify EmissionResult computed with exact decimal precision
        var emission = await _calcDb.EmissionResults.FirstOrDefaultAsync(e => e.DocumentId == docId);
        Assert.NotNull(emission);
        // 10,000 kWh * 0.621 kg CO2e/kWh = 6210.000000 kg CO2e
        Assert.Equal(6210.000000m, emission.KgCo2e);
        Assert.Equal(6.210000m, emission.TonnesCo2e);
        Assert.Equal(2, emission.Scope);

        // Verify ReadModel Summary
        var readModel = new EmissionReadModel(_calcDb);
        var summary = await readModel.GetSummaryAsync(_orgId, "2026-09");
        Assert.Equal(6210.000000m, summary.TotalKgCo2e);
        Assert.Equal(6.210m, summary.TotalTonnesCo2e);
        Assert.Equal(100m, summary.DataQualityScore); // 100% verified
        Assert.Equal(1, summary.RecordsCount);

        // Verify Audit Log persisted
        var logs = await auditService.GetLogsAsync();
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.Action == "ActivityCalculated");
    }

    [Fact]
    public async Task ExpectedDocRuleReader_RetrievesOrgAndGlobalRules()
    {
        var site = new Site { OrgId = _orgId, Name = "DEPZ Unit 1" };
        _onbDb.Sites.Add(site);
        await _onbDb.SaveChangesAsync();

        var asset = new Asset { OrgId = _orgId, SiteId = site.Id, Name = "Generator 1", Type = "genset" };
        _onbDb.Assets.Add(asset);
        await _onbDb.SaveChangesAsync();

        _onbDb.ExpectedDocRules.Add(new ExpectedDocRule
        {
            OrgId = _orgId,
            AssetId = asset.Id,
            DocType = "diesel_slip",
            Frequency = "Monthly",
            DueDayOfMonth = 5,
            IsActive = true
        });
        await _onbDb.SaveChangesAsync();

        var reader = new ExpectedDocRuleReader(_onbDb);
        var orgRules = await reader.GetRulesForOrgAsync(_orgId);

        Assert.Single(orgRules);
        Assert.Equal("Generator 1", orgRules[0].AssetName);
        Assert.Equal("diesel_slip", orgRules[0].DocType);
        Assert.Equal(5, orgRules[0].DueDayOfMonth);
    }
}
