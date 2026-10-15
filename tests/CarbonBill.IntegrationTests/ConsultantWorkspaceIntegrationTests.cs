using CarbonBill.Modules.ActivityUnits.Domain;
using CarbonBill.Modules.ActivityUnits.Persistence;
using CarbonBill.Modules.ActivityUnits.Services;
using CarbonBill.Modules.Audit.Persistence;
using CarbonBill.Modules.Audit.Services;
using CarbonBill.Modules.Calculation.Persistence;
using CarbonBill.Modules.Calculation.Services;
using CarbonBill.Modules.FactorRegistry.Domain;
using CarbonBill.Modules.FactorRegistry.Persistence;
using CarbonBill.Modules.FactorRegistry.Services;
using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarbonBill.IntegrationTests;

/// <summary>
/// Deliverable I7: Consultant Multi-Organization Workspace and Factor Override Workflow Tests.
/// </summary>
public class ConsultantWorkspaceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenantContext;
    private readonly SqlitePragmaInterceptor _pragmaInterceptor;
    private readonly TenantSaveChangesInterceptor _tenantInterceptor;

    private readonly DbContextOptions<IdentityTenancyDbContext> _identityDbOptions;
    private readonly DbContextOptions<FactorRegistryDbContext> _factorDbOptions;
    private readonly DbContextOptions<ActivityUnitsDbContext> _unitsDbOptions;
    private readonly DbContextOptions<CalculationDbContext> _calcDbOptions;
    private readonly DbContextOptions<AuditDbContext> _auditDbOptions;

    public ConsultantWorkspaceIntegrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();

        _tenantContext = new TenantContext();
        _pragmaInterceptor = new SqlitePragmaInterceptor();
        _tenantInterceptor = new TenantSaveChangesInterceptor(_tenantContext);

        _identityDbOptions = CreateOptions<IdentityTenancyDbContext>();
        _factorDbOptions = CreateOptions<FactorRegistryDbContext>();
        _unitsDbOptions = CreateOptions<ActivityUnitsDbContext>();
        _calcDbOptions = CreateOptions<CalculationDbContext>();
        _auditDbOptions = CreateOptions<AuditDbContext>();

        InitializeAllSchemas();
    }

    private DbContextOptions<TContext> CreateOptions<TContext>() where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;
    }

    private void InitializeAllSchemas()
    {
        var contexts = new DbContext[]
        {
            new IdentityTenancyDbContext(_identityDbOptions, _tenantContext),
            new FactorRegistryDbContext(_factorDbOptions, _tenantContext),
            new ActivityUnitsDbContext(_unitsDbOptions, _tenantContext),
            new CalculationDbContext(_calcDbOptions, _tenantContext),
            new AuditDbContext(_auditDbOptions, _tenantContext)
        };

        foreach (var db in contexts)
        {
            var script = db.GetService<IRelationalDatabaseCreator>().GenerateCreateScript();
            var statements = script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            using var cmd = _connection.CreateCommand();
            foreach (var statement in statements)
            {
                if (string.IsNullOrWhiteSpace(statement)) continue;
                try
                {
                    cmd.CommandText = statement;
                    cmd.ExecuteNonQuery();
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 1 || ex.Message.Contains("already exists"))
                {
                }
            }
            db.Dispose();
        }
    }

    [Fact]
    public async Task Consultant_MultiOrgSwitching_And_FactorOverrideWorkflow()
    {
        var consultantUserId = Guid.NewGuid();
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();

        // 1. Setup Consultant User with memberships in Org A and Org B
        using (var identityDb = new IdentityTenancyDbContext(_identityDbOptions, _tenantContext))
        {
            var user = new User
            {
                Id = consultantUserId,
                Email = "farhana.consultant@sustainability.bd",
                FullName = "Farhana Yasmin",
                PasswordHash = "hash-123",
                PreferredLanguage = "bn"
            };
            identityDb.Users.Add(user);

            var orgA = new Organization { Id = orgAId, Name = "Savar Textiles Ltd", Slug = "savar-textiles", Sector = "RMG" };
            var orgB = new Organization { Id = orgBId, Name = "Gazipur Apparels Ltd", Slug = "gazipur-apparels", Sector = "RMG" };
            identityDb.Organizations.AddRange(orgA, orgB);

            var memberA = new Membership { UserId = consultantUserId, OrgId = orgAId };
            var memberB = new Membership { UserId = consultantUserId, OrgId = orgBId };
            identityDb.Memberships.AddRange(memberA, memberB);

            await identityDb.SaveChangesAsync();
        }

        // 2. Setup Default Factor Registry (National Grid Default: 0.621 kg CO2e/kWh)
        Guid electricityFactorId;
        using (var factorDb = new FactorRegistryDbContext(_factorDbOptions, _tenantContext))
        {
            var factorSet = new FactorSet
            {
                Name = "IGES Bangladesh Grid 2026",
                Version = 1,
                GwpBasis = "AR6",
                SourceCitation = "DoE Bangladesh 2024",
                Year = 2024,
                Region = "BD",
                IsPublished = true
            };
            factorDb.FactorSets.Add(factorSet);

            var factor = new EmissionFactor
            {
                FactorSetId = factorSet.Id,
                ActivityType = "Electricity",
                FuelOrMode = "Grid",
                Unit = "kWh",
                Co2eFactor = 0.621000m,
                Scope = 2,
                Tier = "Tier 1"
            };
            factorDb.EmissionFactors.Add(factor);
            await factorDb.SaveChangesAsync();
            electricityFactorId = factor.Id;
        }

        // 3. Seed Unit Conversion
        using (var unitsDb = new ActivityUnitsDbContext(_unitsDbOptions, _tenantContext))
        {
            unitsDb.Units.Add(new Unit { Code = "kWh", NameEn = "Kilowatt Hour", NameBn = "কিলোওয়াট ঘন্টা", PhysicalQuantity = "Energy" });
            unitsDb.UnitConversions.Add(new UnitConversion { FromUnit = "kWh", ToUnit = "kWh", ConversionFactor = 1.0m, Version = 1 });
            await unitsDb.SaveChangesAsync();
        }

        // 4. Consultant acts on Org A -> Applies custom solar-adjusted factor override (0.450 kg CO2e/kWh)
        _tenantContext.SetContext(orgAId, consultantUserId);
        using (var factorDbA = new FactorRegistryDbContext(_factorDbOptions, _tenantContext))
        {
            var factorOverride = new FactorOverride
            {
                OrgId = orgAId,
                EmissionFactorId = electricityFactorId,
                OverrideValue = 0.450000m,
                Justification = "Verified 500kW rooftop solar grid-tied self-consumption audit by SREDA 2026",
                ApprovedByUserId = consultantUserId,
                ApprovedAtUtc = DateTime.UtcNow,
                IsActive = true
            };
            factorDbA.FactorOverrides.Add(factorOverride);
            await factorDbA.SaveChangesAsync();
        }

        // 5. Test Factor Lookup for Org A vs Org B
        using (var factorDb = new FactorRegistryDbContext(_factorDbOptions, _tenantContext))
        {
            var factorLookup = new FactorLookupService(factorDb);

            var factorForOrgA = await factorLookup.ResolveFactorAsync(orgAId, "Electricity");
            Assert.NotNull(factorForOrgA);
            Assert.True(factorForOrgA.IsOverridden);
            Assert.Equal(0.450000m, factorForOrgA.Co2eFactor);
            Assert.Contains("SREDA", factorForOrgA.Justification);

            var factorForOrgB = await factorLookup.ResolveFactorAsync(orgBId, "Electricity");
            Assert.NotNull(factorForOrgB);
            Assert.False(factorForOrgB.IsOverridden);
            Assert.Equal(0.621000m, factorForOrgB.Co2eFactor);
        }

        // 6. Execute Calculation Pipeline for Org A (10,000 kWh -> 4,500 kg CO2e)
        _tenantContext.SetContext(orgAId, consultantUserId);
        using (var calcDbA = new CalculationDbContext(_calcDbOptions, _tenantContext))
        using (var unitsDbA = new ActivityUnitsDbContext(_unitsDbOptions, _tenantContext))
        using (var factorDbA = new FactorRegistryDbContext(_factorDbOptions, _tenantContext))
        using (var auditDbA = new AuditDbContext(_auditDbOptions, _tenantContext))
        {
            var factorLookup = new FactorLookupService(factorDbA);
            var unitConverter = new UnitConverter(unitsDbA);
            var auditService = new AuditLogService(auditDbA, _tenantContext, NullLogger<AuditLogService>.Instance);
            var activityWriter = new ActivityWriter(unitsDbA, calcDbA, unitConverter, factorLookup, auditService, NullLogger<ActivityWriter>.Instance);

            var docId = Guid.NewGuid();
            var result = await activityWriter.RecordConfirmedActivityAsync(new ConfirmedActivityRequest(
                OrgId: orgAId,
                SiteId: null,
                AssetId: null,
                DocumentId: docId,
                ActivityType: "Electricity",
                Quantity: 10000m,
                Unit: "kWh",
                TotalCostBdt: 85000m,
                BillingPeriod: "2026-09",
                IsEstimated: false
            ));

            Assert.True(result.IsSuccess);

            var emissionResultA = await calcDbA.EmissionResults.FirstOrDefaultAsync(e => e.OrgId == orgAId && e.DocumentId == docId);
            Assert.NotNull(emissionResultA);
            Assert.Equal(4500.000000m, emissionResultA.KgCo2e); // 10000 * 0.450
            Assert.Equal(0.450000m, emissionResultA.EmissionFactorUsed);
        }

        // 7. Execute Calculation Pipeline for Org B (10,000 kWh -> 6,210 kg CO2e)
        _tenantContext.SetContext(orgBId, consultantUserId);
        using (var calcDbB = new CalculationDbContext(_calcDbOptions, _tenantContext))
        using (var unitsDbB = new ActivityUnitsDbContext(_unitsDbOptions, _tenantContext))
        using (var factorDbB = new FactorRegistryDbContext(_factorDbOptions, _tenantContext))
        using (var auditDbB = new AuditDbContext(_auditDbOptions, _tenantContext))
        {
            var factorLookup = new FactorLookupService(factorDbB);
            var unitConverter = new UnitConverter(unitsDbB);
            var auditService = new AuditLogService(auditDbB, _tenantContext, NullLogger<AuditLogService>.Instance);
            var activityWriter = new ActivityWriter(unitsDbB, calcDbB, unitConverter, factorLookup, auditService, NullLogger<ActivityWriter>.Instance);

            var docId = Guid.NewGuid();
            var result = await activityWriter.RecordConfirmedActivityAsync(new ConfirmedActivityRequest(
                OrgId: orgBId,
                SiteId: null,
                AssetId: null,
                DocumentId: docId,
                ActivityType: "Electricity",
                Quantity: 10000m,
                Unit: "kWh",
                TotalCostBdt: 85000m,
                BillingPeriod: "2026-09",
                IsEstimated: false
            ));

            Assert.True(result.IsSuccess);

            var emissionResultB = await calcDbB.EmissionResults.FirstOrDefaultAsync(e => e.OrgId == orgBId && e.DocumentId == docId);
            Assert.NotNull(emissionResultB);
            Assert.Equal(6210.000000m, emissionResultB.KgCo2e); // 10000 * 0.621
            Assert.Equal(0.621000m, emissionResultB.EmissionFactorUsed);
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
