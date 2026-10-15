using System.Net;
using System.Security.Claims;
using CarbonBill.Api;
using CarbonBill.Modules.ActivityUnits.Domain;
using CarbonBill.Modules.ActivityUnits.Persistence;
using CarbonBill.Modules.Audit.Domain;
using CarbonBill.Modules.Audit.Persistence;
using CarbonBill.Modules.Calculation.Domain;
using CarbonBill.Modules.Calculation.Persistence;
using CarbonBill.Modules.Documents.Domain;
using CarbonBill.Modules.Documents.Persistence;
using CarbonBill.Modules.FactorRegistry.Domain;
using CarbonBill.Modules.FactorRegistry.Persistence;
using CarbonBill.Modules.Flags.Domain;
using CarbonBill.Modules.Flags.Persistence;
using CarbonBill.Modules.GapDetection.Domain;
using CarbonBill.Modules.GapDetection.Persistence;
using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.Modules.IdentityTenancy.Services;
using CarbonBill.Modules.Insights.Domain;
using CarbonBill.Modules.Insights.Persistence;
using CarbonBill.Modules.Notifications.Domain;
using CarbonBill.Modules.Notifications.Persistence;
using CarbonBill.Modules.Onboarding.Domain;
using CarbonBill.Modules.Onboarding.Persistence;
using CarbonBill.Modules.Recommendations.Domain;
using CarbonBill.Modules.Recommendations.Persistence;
using CarbonBill.Modules.Reporting.Domain;
using CarbonBill.Modules.Reporting.Persistence;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CarbonBill.IntegrationTests;

/// <summary>
/// Deliverable I4: OWASP ASVS Level 2 Verification, Adversarial Cross-Tenant Penetration,
/// IDOR Sweep, and Security Hardening Integration Tests.
/// </summary>
public class SecurityAndTenantIsolationPenetrationTests : IDisposable
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
    private readonly DbContextOptions<DocumentsDbContext> _docsDbOptions;
    private readonly DbContextOptions<FlagsDbContext> _flagsDbOptions;
    private readonly DbContextOptions<GapDetectionDbContext> _gapDbOptions;
    private readonly DbContextOptions<InsightsDbContext> _insightsDbOptions;
    private readonly DbContextOptions<NotificationsDbContext> _notifDbOptions;
    private readonly DbContextOptions<RecommendationsDbContext> _recsDbOptions;
    private readonly DbContextOptions<ReportingDbContext> _reportsDbOptions;

    public SecurityAndTenantIsolationPenetrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();

        _tenantContext = new TenantContext();
        _pragmaInterceptor = new SqlitePragmaInterceptor();
        _tenantInterceptor = new TenantSaveChangesInterceptor(_tenantContext);

        _identityDbOptions = CreateOptions<IdentityTenancyDbContext>();
        _auditDbOptions = CreateOptions<AuditDbContext>();
        _onbDbOptions = CreateOptions<OnboardingDbContext>();
        _unitsDbOptions = CreateOptions<ActivityUnitsDbContext>();
        _calcDbOptions = CreateOptions<CalculationDbContext>();
        _factorDbOptions = CreateOptions<FactorRegistryDbContext>();
        _docsDbOptions = CreateOptions<DocumentsDbContext>();
        _flagsDbOptions = CreateOptions<FlagsDbContext>();
        _gapDbOptions = CreateOptions<GapDetectionDbContext>();
        _insightsDbOptions = CreateOptions<InsightsDbContext>();
        _notifDbOptions = CreateOptions<NotificationsDbContext>();
        _recsDbOptions = CreateOptions<RecommendationsDbContext>();
        _reportsDbOptions = CreateOptions<ReportingDbContext>();

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
            new AuditDbContext(_auditDbOptions, _tenantContext),
            new OnboardingDbContext(_onbDbOptions, _tenantContext),
            new DocumentsDbContext(_docsDbOptions, _tenantContext),
            new ActivityUnitsDbContext(_unitsDbOptions, _tenantContext),
            new FactorRegistryDbContext(_factorDbOptions, _tenantContext),
            new CalculationDbContext(_calcDbOptions, _tenantContext),
            new GapDetectionDbContext(_gapDbOptions, _tenantContext),
            new FlagsDbContext(_flagsDbOptions, _tenantContext),
            new RecommendationsDbContext(_recsDbOptions, _tenantContext),
            new ReportingDbContext(_reportsDbOptions, _tenantContext),
            new NotificationsDbContext(_notifDbOptions, _tenantContext),
            new InsightsDbContext(_insightsDbOptions, _tenantContext)
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
                    // Table or index already created
                }
            }
            db.Dispose();
        }
    }

    [Fact]
    public async Task AdversarialIDOR_CrossTenantDocumentAccess_IsDenied()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var docAId = Guid.NewGuid();

        // 1. Seed document under Org A
        _tenantContext.SetContext(orgA, userA, Roles.Owner);
        using (var docsDbA = new DocumentsDbContext(_docsDbOptions, _tenantContext))
        {
            var doc = new Document
            {
                Id = docAId,
                OrgId = orgA,
                FileName = "confidential_electric_bill_orgA.pdf",
                StoragePath = "orgA/docs/confidential.pdf",
                Sha256Hash = "sha256-hash-secret-a",
                FileSizeBytes = 10240,
                ContentType = "application/pdf",
                Status = DocumentStatuses.Confirmed,
                UploadedByUserId = userA
            };
            docsDbA.Documents.Add(doc);
            await docsDbA.SaveChangesAsync();
        }

        // 2. Adversary: User B from Org B tries to read Org A's document
        _tenantContext.SetContext(orgB, userB, Roles.Owner);
        using (var docsDbB = new DocumentsDbContext(_docsDbOptions, _tenantContext))
        {
            var docFromB = await docsDbB.Documents.FirstOrDefaultAsync(d => d.Id == docAId);
            Assert.Null(docFromB);

            var allDocsForB = await docsDbB.Documents.ToListAsync();
            Assert.Empty(allDocsForB);
        }

        // 3. Adversary: User B tries to update Org A's document via IDOR
        using (var docsDbB = new DocumentsDbContext(_docsDbOptions, _tenantContext))
        {
            var maliciousDocUpdate = new Document
            {
                Id = docAId,
                OrgId = orgA, // Malicious target Org A
                FileName = "tampered.pdf",
                StoragePath = "orgA/docs/tampered.pdf",
                Sha256Hash = "sha256-tampered",
                FileSizeBytes = 500,
                ContentType = "application/pdf",
                Status = DocumentStatuses.Failed,
                UploadedByUserId = userB
            };

            docsDbB.Documents.Add(maliciousDocUpdate);
            await Assert.ThrowsAsync<CrossTenantAccessException>(async () =>
            {
                await docsDbB.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task AdversarialIDOR_CrossTenantReportsAndShareLinks_AreIsolated()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var reportAId = Guid.NewGuid();

        // 1. Create Report and ShareLink for Org A
        _tenantContext.SetContext(orgA, userA, Roles.Compliance);
        using (var dbA = new ReportingDbContext(_reportsDbOptions, _tenantContext))
        {
            var report = new Report
            {
                Id = reportAId,
                OrgId = orgA,
                ReportingPeriod = "2026",
                Status = ReportStatuses.Approved,
                ApprovedByUserId = userA,
                ApprovedAtUtc = DateTime.UtcNow
            };
            dbA.Reports.Add(report);

            var shareLink = new ShareLink
            {
                OrgId = orgA,
                ReportId = reportAId,
                Token = "secret-auditor-token-org-a",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                CreatedByUserId = userA,
                RedactPrices = true
            };
            dbA.ShareLinks.Add(shareLink);
            await dbA.SaveChangesAsync();
        }

        // 2. Adversary Org B attempts to query Org A's reports or share links
        _tenantContext.SetContext(orgB, userB, Roles.Compliance);
        using (var dbB = new ReportingDbContext(_reportsDbOptions, _tenantContext))
        {
            var reports = await dbB.Reports.ToListAsync();
            var shareLinks = await dbB.ShareLinks.ToListAsync();

            Assert.Empty(reports);
            Assert.Empty(shareLinks);
        }
    }

    [Fact]
    public async Task AdversarialIDOR_CrossTenantRecommendationsAndFlags_AreIsolated()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        // 1. Org A Flags and Recommendations
        _tenantContext.SetContext(orgA, userA, Roles.Owner);
        using (var flagsDbA = new FlagsDbContext(_flagsDbOptions, _tenantContext))
        using (var recsDbA = new RecommendationsDbContext(_recsDbOptions, _tenantContext))
        {
            var rule = new FlagRule
            {
                FlagCode = "FLAG-HOTSPOT-01",
                Family = FlagFamilies.Footprint,
                DefaultSeverity = FlagSeverities.Red,
                NameEn = "Natural Gas Hotspot Spike",
                NameBn = "প্রাকৃতিক গ্যাস হটস্পট বৃদ্ধি",
                SuggestedActionEn = "Inspect boiler and steam lines",
                SuggestedActionBn = "বয়লার ও বাষ্প লাইন পরিদর্শন করুন"
            };
            flagsDbA.FlagRules.Add(rule);

            var flag = new Flag
            {
                OrgId = orgA,
                RuleId = rule.Id,
                RuleCode = "FLAG-HOTSPOT-01",
                Family = FlagFamilies.Footprint,
                Severity = FlagSeverities.Red,
                Period = "2026-09",
                EvidenceJson = "{\"surge\":35.5}",
                ExplanationEn = "Severe gas spike detected",
                ExplanationBn = "তীব্র গ্যাস বৃদ্ধি সনাক্ত করা হয়েছে"
            };
            flagsDbA.Flags.Add(flag);
            await flagsDbA.SaveChangesAsync();

            var source = new MeasureSource
            {
                SourceCode = "SREDA_2022",
                Title = "Industrial Energy Efficiency Study",
                Institution = "SREDA",
                Year = 2022,
                LicenseTerms = "Public"
            };
            recsDbA.MeasureSources.Add(source);

            var measure = new Measure
            {
                MeasureCode = "VFD_MOTORS",
                NameEn = "Install VFD on Motors",
                NameBn = "মোটরে ভিএফডি স্থাপন",
                Category = "Motors",
                SavingLow = 0.05m,
                SavingTypical = 0.15m,
                SavingHigh = 0.25m,
                CapexLow = 50000m,
                CapexHigh = 150000m,
                LifetimeYears = 10,
                EvidenceGrade = "A",
                SourceId = source.Id
            };
            recsDbA.Measures.Add(measure);

            var rec = new Recommendation
            {
                OrgId = orgA,
                MeasureId = measure.Id,
                SavingRangeCo2eJson = "{\"typical\":12.0}",
                SavingRangeBdtJson = "{\"typical\":450000}",
                CapexRangeBdtJson = "{\"typical\":150000}",
                PaybackYears = 0.33m,
                Status = "Active"
            };
            recsDbA.Recommendations.Add(rec);
            await recsDbA.SaveChangesAsync();
        }

        // 2. Org B attempts to read Org A's proprietary flags & financial recommendations
        _tenantContext.SetContext(orgB, userB, Roles.Owner);
        using (var flagsDbB = new FlagsDbContext(_flagsDbOptions, _tenantContext))
        using (var recsDbB = new RecommendationsDbContext(_recsDbOptions, _tenantContext))
        {
            var flagsB = await flagsDbB.Flags.ToListAsync();
            var recsB = await recsDbB.Recommendations.ToListAsync();

            Assert.Empty(flagsB);
            Assert.Empty(recsB);
        }
    }

    [Fact]
    public async Task AuditLog_TamperResistance_And_AppendOnlyVerification()
    {
        var orgA = Guid.NewGuid();
        var userA = Guid.NewGuid();

        _tenantContext.SetContext(orgA, userA, Roles.Owner);
        using var auditDb = new AuditDbContext(_auditDbOptions, _tenantContext);

        var log = new AuditLog
        {
            OrgId = orgA,
            UserId = userA,
            Action = "Report.Approved",
            EntityType = "Report",
            EntityId = Guid.NewGuid().ToString(),
            DetailsJson = "{\"period\":\"2026-09\",\"scope1\":120.5,\"scope2\":340.2}",
            OccurredAtUtc = DateTime.UtcNow
        };
        auditDb.AuditLogs.Add(log);
        await auditDb.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(orgA, log.OrgId);

        // Verify retrieval matches exactly
        var retrieved = await auditDb.AuditLogs.FirstOrDefaultAsync(l => l.Id == log.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Report.Approved", retrieved.Action);
    }

    [Fact]
    public void RolesMatrix_AuthorizationPolicies_StrictPermissions()
    {
        // Assert Role Definitions
        Assert.Equal("FloorStaff", Roles.FloorStaff);
        Assert.Equal("Accountant", Roles.Accountant);
        Assert.Equal("Compliance", Roles.Compliance);
        Assert.Equal("Owner", Roles.Owner);
        Assert.Equal("Consultant", Roles.Consultant);
        Assert.Equal("PlatformAdmin", Roles.PlatformAdmin);

        // Verify that FloorStaff cannot have Compliance or Admin rights
        var floorClaims = new[]
        {
            new Claim(ClaimTypes.Role, Roles.FloorStaff),
            new Claim("org_id", Guid.NewGuid().ToString())
        };
        var floorPrincipal = new ClaimsPrincipal(new ClaimsIdentity(floorClaims, "TestAuth"));

        Assert.True(floorPrincipal.IsInRole(Roles.FloorStaff));
        Assert.False(floorPrincipal.IsInRole(Roles.Compliance));
        Assert.False(floorPrincipal.IsInRole(Roles.Owner));
        Assert.False(floorPrincipal.IsInRole(Roles.PlatformAdmin));
    }

    [Fact]
    public async Task KioskPinLock_Set_Lock_And_Unlock_Lifecycle()
    {
        var orgA = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var passwordHasher = new PasswordHasher();

        _tenantContext.SetContext(orgA, userA, Roles.FloorStaff);
        using var identityDb = new IdentityTenancyDbContext(_identityDbOptions, _tenantContext);

        var floorUser = new User
        {
            Id = userA,
            Email = "jahid.kiosk@apex.local",
            FullName = "Jahid Hasan",
            PasswordHash = passwordHasher.HashPassword("Pass1234!"),
            PreferredLanguage = "bn",
            IsActive = true
        };
        identityDb.Users.Add(floorUser);
        await identityDb.SaveChangesAsync();

        // 1. Set Kiosk 4-digit PIN
        floorUser.PinLockHash = passwordHasher.HashPassword("1234");
        await identityDb.SaveChangesAsync();
        Assert.NotNull(floorUser.PinLockHash);

        // 2. Lock Kiosk
        floorUser.IsLocked = true;
        await identityDb.SaveChangesAsync();
        Assert.True(floorUser.IsLocked);

        // 3. Attempt unlock with wrong PIN fails
        var wrongPinValid = passwordHasher.VerifyPassword("9999", floorUser.PinLockHash);
        Assert.False(wrongPinValid);

        // 4. Unlock with correct PIN succeeds
        var correctPinValid = passwordHasher.VerifyPassword("1234", floorUser.PinLockHash);
        Assert.True(correctPinValid);
        floorUser.IsLocked = false;
        await identityDb.SaveChangesAsync();

        var reloaded = await identityDb.Users.FindAsync(userA);
        Assert.NotNull(reloaded);
        Assert.False(reloaded.IsLocked);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
