using CarbonBill.Modules.ActivityUnits.Domain;
using CarbonBill.Modules.ActivityUnits.Persistence;
using CarbonBill.Modules.Audit.Domain;
using CarbonBill.Modules.Audit.Persistence;
using CarbonBill.Modules.Calculation.Domain;
using CarbonBill.Modules.Calculation.Persistence;
using CarbonBill.Modules.FactorRegistry.Domain;
using CarbonBill.Modules.FactorRegistry.Persistence;
using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.Modules.Onboarding.Domain;
using CarbonBill.Modules.Onboarding.Persistence;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CarbonBill.IntegrationTests;

public class TenantIsolationIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenantContext;
    private readonly SqlitePragmaInterceptor _pragmaInterceptor;
    private readonly TenantSaveChangesInterceptor _tenantInterceptor;

    private readonly DbContextOptions<IdentityTenancyDbContext> _identityDbOptions;
    private readonly DbContextOptions<AuditDbContext> _auditDbOptions;
    private readonly DbContextOptions<OnboardingDbContext> _onbDbOptions;
    private readonly DbContextOptions<ActivityUnitsDbContext> _unitsDbOptions;
    private readonly DbContextOptions<CalculationDbContext> _calcDbOptions;
    private readonly DbContextOptions<FactorRegistryDbContext> _factorDbOptions;

    public TenantIsolationIntegrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();

        _tenantContext = new TenantContext();
        _pragmaInterceptor = new SqlitePragmaInterceptor();
        _tenantInterceptor = new TenantSaveChangesInterceptor(_tenantContext);

        _identityDbOptions = new DbContextOptionsBuilder<IdentityTenancyDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;

        _auditDbOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;

        _onbDbOptions = new DbContextOptionsBuilder<OnboardingDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;

        _unitsDbOptions = new DbContextOptionsBuilder<ActivityUnitsDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;

        _calcDbOptions = new DbContextOptionsBuilder<CalculationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;

        _factorDbOptions = new DbContextOptionsBuilder<FactorRegistryDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_pragmaInterceptor, _tenantInterceptor)
            .Options;

        // Create tables for all contexts using their database creators
        using var identityDb = new IdentityTenancyDbContext(_identityDbOptions, _tenantContext);
        identityDb.GetService<IRelationalDatabaseCreator>().CreateTables();

        using var auditDb = new AuditDbContext(_auditDbOptions, _tenantContext);
        auditDb.GetService<IRelationalDatabaseCreator>().CreateTables();

        using var onbDb = new OnboardingDbContext(_onbDbOptions, _tenantContext);
        onbDb.GetService<IRelationalDatabaseCreator>().CreateTables();

        using var unitsDb = new ActivityUnitsDbContext(_unitsDbOptions, _tenantContext);
        unitsDb.GetService<IRelationalDatabaseCreator>().CreateTables();

        using var calcDb = new CalculationDbContext(_calcDbOptions, _tenantContext);
        calcDb.GetService<IRelationalDatabaseCreator>().CreateTables();

        using var factorDb = new FactorRegistryDbContext(_factorDbOptions, _tenantContext);
        factorDb.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    [Fact]
    public async Task ReadIsolation_OrgB_CannotRead_OrgA_Data()
    {
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();
        var userAId = Guid.NewGuid();

        // 1. Act as Org A: Seed Organization and Invitation
        _tenantContext.SetContext(orgAId, userAId);
        using (var dbA = new IdentityTenancyDbContext(_identityDbOptions, _tenantContext))
        {
            var orgA = new Organization { Id = orgAId, Name = "Org A Textiles", Slug = "org-a", Sector = "RMG" };
            var orgB = new Organization { Id = orgBId, Name = "Org B RMG", Slug = "org-b", Sector = "RMG" };
            dbA.Organizations.AddRange(orgA, orgB);

            var invA = new Invitation
            {
                OrgId = orgAId,
                Token = "token-org-a",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                CreatedByUserId = userAId
            };
            dbA.Invitations.Add(invA);
            await dbA.SaveChangesAsync();
        }

        // 2. Switch Context to Org B User
        var userBId = Guid.NewGuid();
        _tenantContext.SetContext(orgBId, userBId);

        // 3. Query Invitations as Org B -> Org A invitations must NOT be visible
        using (var dbB = new IdentityTenancyDbContext(_identityDbOptions, _tenantContext))
        {
            var invitationsVisibleToB = await dbB.Invitations.ToListAsync();
            Assert.Empty(invitationsVisibleToB);
        }
    }

    [Fact]
    public async Task WriteIsolation_OrgB_CannotModifyOrWrite_OrgA_Data()
    {
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();

        // 1. Act as Org A: Create Invitation
        _tenantContext.SetContext(orgAId, userAId);
        using (var dbA = new IdentityTenancyDbContext(_identityDbOptions, _tenantContext))
        {
            var orgA = new Organization { Id = orgAId, Name = "Org A Textiles", Slug = "org-a-2", Sector = "RMG" };
            dbA.Organizations.Add(orgA);

            var invA = new Invitation
            {
                OrgId = orgAId,
                Token = "token-org-a-write",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                CreatedByUserId = userAId
            };
            dbA.Invitations.Add(invA);
            await dbA.SaveChangesAsync();
        }

        // 2. Switch Context to Org B User
        _tenantContext.SetContext(orgBId, userBId);

        // 3. Attempt to insert entity with Org A's ID while active context is Org B -> Throws CrossTenantAccessException
        using (var dbB = new IdentityTenancyDbContext(_identityDbOptions, _tenantContext))
        {
            var maliciousInv = new Invitation
            {
                OrgId = orgAId, // Mismatched OrgId!
                Token = "malicious-token",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                CreatedByUserId = userBId
            };
            dbB.Invitations.Add(maliciousInv);

            await Assert.ThrowsAsync<CrossTenantAccessException>(async () =>
            {
                await dbB.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task MultiTenantIsolation_Onboarding_SitesAndAssets_Isolated()
    {
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();

        // Act as Org A
        _tenantContext.SetContext(orgAId, userAId);
        using (var onbDbA = new OnboardingDbContext(_onbDbOptions, _tenantContext))
        {
            var siteA = new Site { OrgId = orgAId, Name = "Site A Savar" };
            onbDbA.Sites.Add(siteA);
            await onbDbA.SaveChangesAsync();

            var assetA = new Asset { OrgId = orgAId, SiteId = siteA.Id, Name = "Meter A", Type = "meter" };
            onbDbA.Assets.Add(assetA);
            await onbDbA.SaveChangesAsync();
        }

        // Switch to Org B
        _tenantContext.SetContext(orgBId, userBId);
        using (var onbDbB = new OnboardingDbContext(_onbDbOptions, _tenantContext))
        {
            var sitesB = await onbDbB.Sites.ToListAsync();
            var assetsB = await onbDbB.Assets.ToListAsync();

            Assert.Empty(sitesB);
            Assert.Empty(assetsB);
        }
    }

    [Fact]
    public async Task MultiTenantIsolation_EmissionsAndActivity_Isolated()
    {
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();

        // Act as Org A
        _tenantContext.SetContext(orgAId, userAId);
        using (var unitsDbA = new ActivityUnitsDbContext(_unitsDbOptions, _tenantContext))
        using (var calcDbA = new CalculationDbContext(_calcDbOptions, _tenantContext))
        {
            var actA = new ActivityRecord
            {
                OrgId = orgAId,
                DocumentId = Guid.NewGuid(),
                ActivityType = "electricity",
                QuantityStandard = 5000,
                StandardUnit = "kWh",
                RawQuantity = 5000,
                RawUnit = "kWh",
                Period = "2026-09"
            };
            unitsDbA.ActivityRecords.Add(actA);
            await unitsDbA.SaveChangesAsync();

            var emA = new EmissionResult
            {
                OrgId = orgAId,
                ActivityRecordId = actA.Id,
                DocumentId = actA.DocumentId,
                FactorId = Guid.NewGuid(),
                Scope = 2,
                Category = "Grid Electricity",
                QuantityStandard = 5000,
                StandardUnit = "kWh",
                EmissionFactorUsed = 0.621000m,
                KgCo2e = 3105.000000m,
                Period = "2026-09"
            };
            calcDbA.EmissionResults.Add(emA);
            await calcDbA.SaveChangesAsync();
        }

        // Switch to Org B
        _tenantContext.SetContext(orgBId, userBId);
        using (var unitsDbB = new ActivityUnitsDbContext(_unitsDbOptions, _tenantContext))
        using (var calcDbB = new CalculationDbContext(_calcDbOptions, _tenantContext))
        {
            var actsB = await unitsDbB.ActivityRecords.ToListAsync();
            var emsB = await calcDbB.EmissionResults.ToListAsync();

            Assert.Empty(actsB);
            Assert.Empty(emsB);
        }
    }

    [Fact]
    public async Task AuditLog_TenantIsolation_AndStamping_Works()
    {
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();
        var userAId = Guid.NewGuid();

        // Act as Org A
        _tenantContext.SetContext(orgAId, userAId);
        using (var auditDbA = new AuditDbContext(_auditDbOptions, _tenantContext))
        {
            var log = new AuditLog
            {
                Action = "Document.Confirmed",
                EntityType = "Document",
                EntityId = Guid.NewGuid().ToString(),
                UserEmail = "user@orgA.local"
            };
            auditDbA.AuditLogs.Add(log);
            await auditDbA.SaveChangesAsync();

            // Check that OrgId was automatically stamped by interceptor
            Assert.Equal(orgAId, log.OrgId);
        }

        // Switch to Org B
        var userBId = Guid.NewGuid();
        _tenantContext.SetContext(orgBId, userBId);

        using (var auditDbB = new AuditDbContext(_auditDbOptions, _tenantContext))
        {
            var logsB = await auditDbB.AuditLogs.ToListAsync();
            Assert.Empty(logsB);
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
