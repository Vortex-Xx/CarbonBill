using CarbonBill.Modules.ActivityUnits.Domain;
using CarbonBill.Modules.ActivityUnits.Persistence;
using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CarbonBill.IntegrationTests;

/// <summary>
/// Deliverable I8: Automated SQLite Online Vacuum Backup and Disaster Recovery Drill Tests.
/// </summary>
public class DatabaseBackupRestoreIntegrationTests : IDisposable
{
    private readonly string _tempDbDir;
    private readonly string _mainDbPath;
    private readonly string _backupDbPath;

    public DatabaseBackupRestoreIntegrationTests()
    {
        _tempDbDir = Path.Combine(Path.GetTempPath(), "carbonbill_backup_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDbDir);
        _mainDbPath = Path.Combine(_tempDbDir, "carbonbill.db");
        _backupDbPath = Path.Combine(_tempDbDir, "carbonbill_backup.db");
    }

    [Fact]
    public async Task SQLite_VacuumIntoBackup_AndRestoreDrill_PreservesAllData()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetContext(orgId, userId);

        var pragmaInterceptor = new SqlitePragmaInterceptor();
        var tenantInterceptor = new TenantSaveChangesInterceptor(tenantContext);

        var identityOptions = new DbContextOptionsBuilder<IdentityTenancyDbContext>()
            .UseSqlite($"Data Source={_mainDbPath}")
            .AddInterceptors(pragmaInterceptor, tenantInterceptor)
            .Options;

        var unitsOptions = new DbContextOptionsBuilder<ActivityUnitsDbContext>()
            .UseSqlite($"Data Source={_mainDbPath}")
            .AddInterceptors(pragmaInterceptor, tenantInterceptor)
            .Options;

        // 1. Initialize and Seed Main Database
        using (var identityDb = new IdentityTenancyDbContext(identityOptions, tenantContext))
        using (var unitsDb = new ActivityUnitsDbContext(unitsOptions, tenantContext))
        {
            identityDb.GetService<IRelationalDatabaseCreator>().CreateTables();
            unitsDb.GetService<IRelationalDatabaseCreator>().CreateTables();

            var org = new Organization
            {
                Id = orgId,
                Name = "Apex Textile Mills Ltd",
                Slug = "apex-textiles",
                Sector = "RMG"
            };
            identityDb.Organizations.Add(org);
            await identityDb.SaveChangesAsync();

            var record = new ActivityRecord
            {
                OrgId = orgId,
                ActivityType = "Electricity",
                QuantityStandard = 25000m,
                StandardUnit = "kWh",
                RawQuantity = 25000m,
                RawUnit = "kWh",
                TotalCostBdt = 212500m,
                Period = "2026-09",
                IsEstimated = false
            };
            unitsDb.ActivityRecords.Add(record);
            await unitsDb.SaveChangesAsync();
        }

        // 2. Perform Atomic Online Backup via VACUUM INTO
        using (var conn = new SqliteConnection($"Data Source={_mainDbPath}"))
        {
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"VACUUM INTO '{_backupDbPath}';";
            await cmd.ExecuteNonQueryAsync();
        }

        Assert.True(File.Exists(_backupDbPath));
        var backupFileInfo = new FileInfo(_backupDbPath);
        Assert.True(backupFileInfo.Length > 0);

        // 3. Verify Backup Integrity Check
        using (var backupConn = new SqliteConnection($"Data Source={_backupDbPath}"))
        {
            await backupConn.OpenAsync();
            using var cmd = backupConn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var integrity = (string?)await cmd.ExecuteScalarAsync();
            Assert.Equal("ok", integrity);
        }

        // 4. Mutate / Corrupt Main Database (Add unexpected mutation)
        using (var identityDb = new IdentityTenancyDbContext(identityOptions, tenantContext))
        {
            var rogueOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Rogue Mutated Org",
                Slug = "rogue-mutated",
                Sector = "Unknown"
            };
            identityDb.Organizations.Add(rogueOrg);
            await identityDb.SaveChangesAsync();

            var count = await identityDb.Organizations.CountAsync();
            Assert.Equal(2, count);
        }

        // Clear SQLite connection pool to release main DB file lock
        SqliteConnection.ClearAllPools();

        // 5. Disaster Recovery Restore: Replace Main DB with Backup
        File.Copy(_backupDbPath, _mainDbPath, overwrite: true);

        // 6. Verify Restored Database has pristine original state
        using (var identityDb = new IdentityTenancyDbContext(identityOptions, tenantContext))
        using (var unitsDb = new ActivityUnitsDbContext(unitsOptions, tenantContext))
        {
            var orgs = await identityDb.Organizations.ToListAsync();
            Assert.Single(orgs);
            Assert.Equal("Apex Textile Mills Ltd", orgs[0].Name);

            var records = await unitsDb.ActivityRecords.ToListAsync();
            Assert.Single(records);
            Assert.Equal(25000m, records[0].QuantityStandard);
            Assert.Equal(212500m, records[0].TotalCostBdt);
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDbDir))
        {
            try
            {
                Directory.Delete(_tempDbDir, recursive: true);
            }
            catch
            {
            }
        }
        GC.SuppressFinalize(this);
    }
}
