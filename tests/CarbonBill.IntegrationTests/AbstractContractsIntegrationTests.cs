using CarbonBill.Modules.ActivityUnits;
using CarbonBill.Modules.ActivityUnits.Domain;
using CarbonBill.Modules.ActivityUnits.Persistence;
using CarbonBill.Modules.ActivityUnits.Services;
using CarbonBill.Modules.Audit;
using CarbonBill.Modules.Audit.Domain;
using CarbonBill.Modules.Audit.Persistence;
using CarbonBill.Modules.Audit.Services;
using CarbonBill.Modules.Calculation;
using CarbonBill.Modules.Calculation.Persistence;
using CarbonBill.Modules.Calculation.Services;
using CarbonBill.Modules.Documents;
using CarbonBill.Modules.Documents.Persistence;
using CarbonBill.Modules.Extraction;
using CarbonBill.Modules.FactorRegistry;
using CarbonBill.Modules.FactorRegistry.Domain;
using CarbonBill.Modules.FactorRegistry.Persistence;
using CarbonBill.Modules.FactorRegistry.Services;
using CarbonBill.Modules.Flags;
using CarbonBill.Modules.Flags.Domain;
using CarbonBill.Modules.Flags.Persistence;
using CarbonBill.Modules.Flags.Services;
using CarbonBill.Modules.GapDetection;
using CarbonBill.Modules.GapDetection.Persistence;
using CarbonBill.Modules.IdentityTenancy;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.Modules.Insights;
using CarbonBill.Modules.Insights.Domain;
using CarbonBill.Modules.Insights.Persistence;
using CarbonBill.Modules.Insights.Services;
using CarbonBill.Modules.Notifications;
using CarbonBill.Modules.Notifications.Persistence;
using CarbonBill.Modules.Notifications.Services;
using CarbonBill.Modules.Onboarding;
using CarbonBill.Modules.Onboarding.Persistence;
using CarbonBill.Modules.Onboarding.Services;
using CarbonBill.Modules.PlatformAdmin;
using CarbonBill.Modules.PlatformAdmin.Persistence;
using CarbonBill.Modules.Recommendations;
using CarbonBill.Modules.Recommendations.Persistence;
using CarbonBill.Modules.Reporting;
using CarbonBill.Modules.Reporting.Persistence;
using CarbonBill.Modules.Reporting.Renderers;
using CarbonBill.Modules.Reporting.Services;
using CarbonBill.Modules.Review;
using CarbonBill.Modules.Review.Persistence;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Events;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Providers;
using CarbonBill.SharedKernel.Providers.Stubs;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CarbonBill.IntegrationTests;

public class AbstractContractsIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public AbstractContractsIntegrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _connection.Open();
    }

    private ServiceProvider BuildServiceProvider(bool useFakes)
    {
        var services = new ServiceCollection();

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:;Foreign Keys=True",
            ["ConnectionStrings:HangfireConnection"] = "Data Source=:memory:",
            ["Jwt:Key"] = "CarbonBill-SuperSecret-Development-Key-2026-VeryLongKeyNeeded-AtLeast32Bytes",
            ["Jwt:Issuer"] = "CarbonBill",
            ["Jwt:Audience"] = "CarbonBillClients",
            ["UseFakes"] = useFakes ? "true" : "false"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        services.AddSingleton(configuration);
        services.AddLogging();

        // Cross-cutting infrastructure
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IDomainEventPublisher, InMemoryDomainEventPublisher>();
        services.AddSingleton<SqlitePragmaInterceptor>();
        services.AddScoped<TenantSaveChangesInterceptor>();

        // Register all modules
        services.AddIdentityTenancyModule(configuration);
        services.AddAuditModule(configuration);
        services.AddOnboardingModule(configuration);
        services.AddDocumentsModule(configuration);
        services.AddExtractionModule(configuration);
        services.AddReviewModule(configuration);
        services.AddActivityUnitsModule(configuration);
        services.AddFactorRegistryModule(configuration);
        services.AddCalculationModule(configuration);
        services.AddGapDetectionModule(configuration);
        services.AddFlagsModule(configuration);
        services.AddRecommendationsModule(configuration);
        services.AddReportingModule(configuration);
        services.AddNotificationsModule(configuration);
        services.AddPlatformAdminModule(configuration);
        services.AddInsightsModule(configuration);

        // Provider configuration (override with stubs if useFakes=true)
        if (useFakes)
        {
            services.RemoveAll<IOcrProvider>();
            services.RemoveAll<IFileStore>();
            services.RemoveAll<INotifier>();
            services.RemoveAll<IReportRenderer>();

            services.AddSingleton<IOcrProvider, FakeTesseractOcrProvider>();
            services.AddSingleton<IFileStore, LocalOrR2FileStoreStub>();
            services.AddSingleton<INotifier, LoggingNotifierStub>();
            services.AddSingleton<IReportRenderer, FakeQuestPdfReportRenderer>();
        }
        else
        {
            services.AddSingleton<IOcrProvider, FakeTesseractOcrProvider>();
            services.AddSingleton<IFileStore, LocalOrR2FileStoreStub>();
        }

        // Override DbContexts to share the open in-memory connection
        OverrideDbContexts(services, _connection);

        return services.BuildServiceProvider();
    }

    private static void OverrideDbContexts(IServiceCollection services, SqliteConnection connection)
    {
        ReplaceDbContext<IdentityTenancyDbContext>(services, connection);
        ReplaceDbContext<AuditDbContext>(services, connection);
        ReplaceDbContext<OnboardingDbContext>(services, connection);
        ReplaceDbContext<DocumentsDbContext>(services, connection);
        ReplaceDbContext<ActivityUnitsDbContext>(services, connection);
        ReplaceDbContext<FactorRegistryDbContext>(services, connection);
        ReplaceDbContext<CalculationDbContext>(services, connection);
        ReplaceDbContext<GapDetectionDbContext>(services, connection);
        ReplaceDbContext<FlagsDbContext>(services, connection);
        ReplaceDbContext<RecommendationsDbContext>(services, connection);
        ReplaceDbContext<ReportingDbContext>(services, connection);
        ReplaceDbContext<NotificationsDbContext>(services, connection);
        ReplaceDbContext<PlatformAdminDbContext>(services, connection);
        ReplaceDbContext<InsightsDbContext>(services, connection);
    }

    private static void ReplaceDbContext<TContext>(IServiceCollection services, SqliteConnection connection)
        where TContext : DbContext
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(DbContextOptions<TContext>) || d.ServiceType == typeof(TContext)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }

        services.AddDbContext<TContext>((sp, options) =>
        {
            options.UseSqlite(connection);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });
    }

    private static void InitializeDatabaseTables(IServiceProvider sp, SqliteConnection connection)
    {
        using var scope = sp.CreateScope();
        var contexts = new DbContext[]
        {
            scope.ServiceProvider.GetRequiredService<IdentityTenancyDbContext>(),
            scope.ServiceProvider.GetRequiredService<AuditDbContext>(),
            scope.ServiceProvider.GetRequiredService<OnboardingDbContext>(),
            scope.ServiceProvider.GetRequiredService<DocumentsDbContext>(),
            scope.ServiceProvider.GetRequiredService<ActivityUnitsDbContext>(),
            scope.ServiceProvider.GetRequiredService<FactorRegistryDbContext>(),
            scope.ServiceProvider.GetRequiredService<CalculationDbContext>(),
            scope.ServiceProvider.GetRequiredService<GapDetectionDbContext>(),
            scope.ServiceProvider.GetRequiredService<FlagsDbContext>(),
            scope.ServiceProvider.GetRequiredService<RecommendationsDbContext>(),
            scope.ServiceProvider.GetRequiredService<ReportingDbContext>(),
            scope.ServiceProvider.GetRequiredService<NotificationsDbContext>(),
            scope.ServiceProvider.GetRequiredService<PlatformAdminDbContext>(),
            scope.ServiceProvider.GetRequiredService<InsightsDbContext>()
        };

        foreach (var db in contexts)
        {
            var script = db.GetService<IRelationalDatabaseCreator>().GenerateCreateScript();
            var statements = script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            using var cmd = connection.CreateCommand();
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
        }
    }

    [Fact]
    public void HostDependencyInjection_UseFakesFalse_ResolvesRealModuleImplementations()
    {
        var sp = BuildServiceProvider(useFakes: false);
        using var scope = sp.CreateScope();

        // 1. Activity & Calculation
        var activityWriter = scope.ServiceProvider.GetRequiredService<IActivityWriter>();
        Assert.IsType<ActivityWriter>(activityWriter);

        var emissionReadModel = scope.ServiceProvider.GetRequiredService<IEmissionReadModel>();
        Assert.IsType<EmissionReadModel>(emissionReadModel);

        // 2. Onboarding & Expected Docs
        var docRuleReader = scope.ServiceProvider.GetRequiredService<IExpectedDocRuleReader>();
        Assert.IsType<ExpectedDocRuleReader>(docRuleReader);

        // 3. Units & Factors
        var unitConverter = scope.ServiceProvider.GetRequiredService<IUnitConverter>();
        Assert.IsType<UnitConverter>(unitConverter);

        var factorLookup = scope.ServiceProvider.GetRequiredService<IFactorLookup>();
        Assert.IsType<FactorLookupService>(factorLookup);

        // 4. Audit
        var auditLogger = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        Assert.IsType<AuditLogService>(auditLogger);

        // 5. Insights & Benchmarks
        var metricReader = scope.ServiceProvider.GetRequiredService<IProductionMetricReader>();
        Assert.IsType<InsightsService>(metricReader);

        var benchmarkReader = scope.ServiceProvider.GetRequiredService<IBenchmarkReader>();
        Assert.IsType<InsightsService>(benchmarkReader);

        // 6. Carbon Flags
        var flagRaiser = scope.ServiceProvider.GetRequiredService<IFlagRaiser>();
        Assert.IsType<FlagsService>(flagRaiser);

        var flagReader = scope.ServiceProvider.GetRequiredService<IFlagReader>();
        Assert.IsType<FlagsService>(flagReader);

        // 7. Notifications
        var notifier = scope.ServiceProvider.GetRequiredService<INotifier>();
        Assert.IsType<NotifierService>(notifier);

        // 8. Reporting
        var reportRenderer = scope.ServiceProvider.GetRequiredService<IReportRenderer>();
        Assert.IsType<QuestPdfReportRenderer>(reportRenderer);
    }

    [Fact]
    public void HostDependencyInjection_UseFakesTrue_ResolvesStubs()
    {
        var sp = BuildServiceProvider(useFakes: true);
        using var scope = sp.CreateScope();

        var notifier = scope.ServiceProvider.GetRequiredService<INotifier>();
        Assert.IsType<LoggingNotifierStub>(notifier);

        var reportRenderer = scope.ServiceProvider.GetRequiredService<IReportRenderer>();
        Assert.IsType<FakeQuestPdfReportRenderer>(reportRenderer);

        var ocrProvider = scope.ServiceProvider.GetRequiredService<IOcrProvider>();
        Assert.IsType<FakeTesseractOcrProvider>(ocrProvider);

        var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();
        Assert.IsType<LocalOrR2FileStoreStub>(fileStore);
    }

    [Fact]
    public async Task AbstractContracts_RealPipeline_UnitConversion_Emissions_Audit()
    {
        var sp = BuildServiceProvider(useFakes: false);
        InitializeDatabaseTables(sp, _connection);

        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using (var scope = sp.CreateScope())
        {
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.SetContext(orgId, userId, "Owner");

            // 1. Seed Factor and Unit Conversion in real DB
            var unitsDb = scope.ServiceProvider.GetRequiredService<ActivityUnitsDbContext>();
            unitsDb.Units.Add(new Unit { Code = "kWh", NameEn = "Kilowatt Hour", NameBn = "কিলোওয়াট ঘন্টা", PhysicalQuantity = "Energy" });
            unitsDb.UnitConversions.Add(new UnitConversion
            {
                FromUnit = "kWh",
                ToUnit = "kWh",
                ConversionFactor = 1.0m,
                Version = 1
            });
            await unitsDb.SaveChangesAsync();

            var factorDb = scope.ServiceProvider.GetRequiredService<FactorRegistryDbContext>();
            var factorSet = new FactorSet
            {
                Name = "IGES Bangladesh Grid 2025",
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

            // 2. Execute Real ActivityWriter Pipeline
            var activityWriter = scope.ServiceProvider.GetRequiredService<IActivityWriter>();
            var docId = Guid.NewGuid();

            var recordResult = await activityWriter.RecordConfirmedActivityAsync(new ConfirmedActivityRequest(
                OrgId: orgId,
                SiteId: null,
                AssetId: null,
                DocumentId: docId,
                ActivityType: "Electricity",
                Quantity: 1000m,
                Unit: "kWh",
                TotalCostBdt: 8500m,
                BillingPeriod: "2026-09",
                IsEstimated: false
            ));

            Assert.True(recordResult.IsSuccess);

            // 3. Query via Real EmissionReadModel
            var emissionReadModel = scope.ServiceProvider.GetRequiredService<IEmissionReadModel>();
            var summary = await emissionReadModel.GetSummaryAsync(orgId, "2026-09");

            Assert.NotNull(summary);
            Assert.Equal(621.000000m, summary.TotalKgCo2e);
            Assert.Equal(621.000000m, summary.Scope2KgCo2e);

            // 4. Verify Scope Breakdown
            var breakdown = await emissionReadModel.GetScopeBreakdownAsync(orgId, "2026-09");
            Assert.NotEmpty(breakdown);
            Assert.Contains(breakdown, b => b.Scope == 2 && b.KgCo2e == 621.000000m);

            // 5. Verify Audit Log was recorded
            var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var auditLogs = await auditDb.AuditLogs.ToListAsync();
            Assert.NotEmpty(auditLogs);
            Assert.Contains(auditLogs, a => a.OrgId == orgId);
        }
    }

    [Fact]
    public async Task AbstractContracts_RealFlags_And_InsightsService()
    {
        var sp = BuildServiceProvider(useFakes: false);
        InitializeDatabaseTables(sp, _connection);

        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var scope = sp.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetContext(orgId, userId, "Owner");

        // Seed FlagRule
        var flagsDb = scope.ServiceProvider.GetRequiredService<FlagsDbContext>();
        var flagRule = new FlagRule
        {
            FlagCode = "FLAG-DQ-01",
            Family = FlagFamilies.DataQuality,
            DefaultSeverity = FlagSeverities.Amber,
            NameEn = "Missing Electricity Bill",
            NameBn = "বিদ্যুৎ বিল অনুপস্থিত",
            SuggestedActionEn = "Upload the missing bill",
            SuggestedActionBn = "অনুপস্থিত বিল আপলোড করুন",
            IsActive = true
        };
        flagsDb.FlagRules.Add(flagRule);
        await flagsDb.SaveChangesAsync();

        // 1. Test InsightsService as IProductionMetricReader
        var insightsDb = scope.ServiceProvider.GetRequiredService<InsightsDbContext>();
        insightsDb.ProductionMetrics.Add(new ProductionMetric
        {
            OrgId = orgId,
            Period = "2026-09",
            Unit = "pieces",
            Quantity = 50000m
        });
        await insightsDb.SaveChangesAsync();

        var metricReader = scope.ServiceProvider.GetRequiredService<IProductionMetricReader>();
        var metric = await metricReader.GetMetricAsync(orgId, "2026-09");
        Assert.NotNull(metric);
        Assert.Equal(50000m, metric.Quantity);

        // 2. Test FlagsService as IFlagRaiser & IFlagReader
        var flagRaiser = scope.ServiceProvider.GetRequiredService<IFlagRaiser>();
        var flagDto = await flagRaiser.RaiseOrUpdateFlagAsync(new RaiseFlagRequest(
            OrgId: orgId,
            SiteId: null,
            RuleCode: "FLAG-DQ-01",
            Period: "2026-09",
            EvidenceJson: "{}",
            ExplanationBn: "সেপ্টেম্বর ২০২৬ এর বিদ্যুৎ বিল পাওয়া যায়নি",
            ExplanationEn: "Electricity bill for September 2026 is missing"
        ));

        Assert.NotNull(flagDto);
        Assert.Equal("FLAG-DQ-01", flagDto.RuleCode);
        Assert.Equal("Open", flagDto.State);

        var flagReader = scope.ServiceProvider.GetRequiredService<IFlagReader>();
        var flags = await flagReader.GetFlagsAsync(orgId, period: "2026-09");
        Assert.Single(flags);
        Assert.Equal("FLAG-DQ-01", flags[0].RuleCode);
        Assert.Equal("Open", flags[0].State);
    }

    [Fact]
    public async Task DirectUtilityConnector_IngestDESCO_And_TitasGas_DigitalBills()
    {
        var sp = BuildServiceProvider(useFakes: false);
        InitializeDatabaseTables(sp, _connection);

        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var scope = sp.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetContext(orgId, userId, "Owner");

        var activityWriter = scope.ServiceProvider.GetRequiredService<IActivityWriter>();
        var activityDb = scope.ServiceProvider.GetRequiredService<ActivityUnitsDbContext>();
        var calcDb = scope.ServiceProvider.GetRequiredService<CalculationDbContext>();

        // 1. Ingest DESCO digital electricity bill (5000 kWh, 48000 BDT)
        var descoRequest = new ConfirmedActivityRequest(
            OrgId: orgId,
            SiteId: null,
            AssetId: null,
            DocumentId: Guid.NewGuid(),
            ActivityType: "Electricity",
            Quantity: 5000m,
            Unit: "kWh",
            TotalCostBdt: 48000m,
            BillingPeriod: "2026-09",
            IsEstimated: false);

        var descoResult = await activityWriter.RecordConfirmedActivityAsync(descoRequest);
        Assert.True(descoResult.IsSuccess);

        // 2. Ingest Titas Gas digital gas invoice (1200 m3, 36000 BDT)
        var titasRequest = new ConfirmedActivityRequest(
            OrgId: orgId,
            SiteId: null,
            AssetId: null,
            DocumentId: Guid.NewGuid(),
            ActivityType: "NaturalGas",
            Quantity: 1200m,
            Unit: "m3",
            TotalCostBdt: 36000m,
            BillingPeriod: "2026-09",
            IsEstimated: false);

        var titasResult = await activityWriter.RecordConfirmedActivityAsync(titasRequest);
        Assert.True(titasResult.IsSuccess);

        // Verify emissions computed for both
        var emissions = await calcDb.EmissionResults
            .Where(e => e.OrgId == orgId)
            .ToListAsync();

        Assert.Equal(2, emissions.Count);
        var elec = emissions.First(e => e.Scope == 2);
        Assert.Equal(5000m * 0.621000m, elec.KgCo2e);

        var gas = emissions.First(e => e.Scope == 1);
        Assert.Equal(1200m * 1.930000m, gas.KgCo2e);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
