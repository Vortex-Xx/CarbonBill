using CarbonBill.Modules.ActivityUnits.Domain;
using CarbonBill.Modules.ActivityUnits.Persistence;
using CarbonBill.Modules.Audit.Persistence;
using CarbonBill.Modules.Calculation.Domain;
using CarbonBill.Modules.Calculation.Persistence;
using CarbonBill.Modules.Documents.Persistence;
using CarbonBill.Modules.Extraction.Persistence;
using CarbonBill.Modules.FactorRegistry.Domain;
using CarbonBill.Modules.FactorRegistry.Persistence;
using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.Modules.IdentityTenancy.Services;
using CarbonBill.Modules.Onboarding.Domain;
using CarbonBill.Modules.Onboarding.Persistence;
using CarbonBill.Modules.PlatformAdmin.Domain;
using CarbonBill.Modules.PlatformAdmin.Persistence;
using CarbonBill.Modules.Review.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Api;

public static class DataSeeder
{
    public static async Task SeedDevelopmentDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");
        var identityDb = scope.ServiceProvider.GetRequiredService<IdentityTenancyDbContext>();
        var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var documentsDb = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var extractionDb = scope.ServiceProvider.GetRequiredService<ExtractionDbContext>();
        var reviewDb = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
        var onboardingDb = scope.ServiceProvider.GetRequiredService<OnboardingDbContext>();
        var factorDb = scope.ServiceProvider.GetRequiredService<FactorRegistryDbContext>();
        var unitsDb = scope.ServiceProvider.GetRequiredService<ActivityUnitsDbContext>();
        var calcDb = scope.ServiceProvider.GetRequiredService<CalculationDbContext>();
        var adminDb = scope.ServiceProvider.GetRequiredService<PlatformAdminDbContext>();
        var gapDb = scope.ServiceProvider.GetRequiredService<CarbonBill.Modules.GapDetection.Persistence.GapDetectionDbContext>();
        var notificationsDb = scope.ServiceProvider.GetRequiredService<CarbonBill.Modules.Notifications.Persistence.NotificationsDbContext>();
        var flagsDb = scope.ServiceProvider.GetRequiredService<CarbonBill.Modules.Flags.Persistence.FlagsDbContext>();
        var flagSeedLoader = scope.ServiceProvider.GetRequiredService<CarbonBill.Modules.Flags.Seeds.IFlagRuleSeedLoader>();
        var insightsDb = scope.ServiceProvider.GetRequiredService<CarbonBill.Modules.Insights.Persistence.InsightsDbContext>();
        var insightsSeedLoader = scope.ServiceProvider.GetRequiredService<CarbonBill.Modules.Insights.Seeds.IInsightsSeedLoader>();
        var recommendationsDb = scope.ServiceProvider.GetRequiredService<CarbonBill.Modules.Recommendations.Persistence.RecommendationsDbContext>();
        var recommendationsSeedLoader = scope.ServiceProvider.GetRequiredService<CarbonBill.Modules.Recommendations.Seeds.IRecommendationsSeedLoader>();
        var reportingDb = scope.ServiceProvider.GetRequiredService<CarbonBill.Modules.Reporting.Persistence.ReportingDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        logger.LogInformation("Ensuring SQLite database and tables are created across all modules...");
        var contexts = new DbContext[]
        {
            identityDb,
            auditDb,
            documentsDb,
            extractionDb,
            reviewDb,
            onboardingDb,
            factorDb,
            unitsDb,
            calcDb,
            adminDb,
            gapDb,
            notificationsDb,
            flagsDb,
            insightsDb,
            recommendationsDb,
            reportingDb
        };

        var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var connection = identityDb.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                await connection.OpenAsync();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    existingTables.Add(reader.GetString(0));
                }
            }

            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug("Could not inspect existing tables: {Msg}", ex.Message);
        }

        foreach (var ctx in contexts)
        {
            try
            {
                var script = ctx.Database.GenerateCreateScript();
                var safeScript = System.Text.RegularExpressions.Regex.Replace(
                    script,
                    @"\bCREATE\s+TABLE\s+(?!IF\s+NOT\s+EXISTS\b)",
                    "CREATE TABLE IF NOT EXISTS ",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                safeScript = System.Text.RegularExpressions.Regex.Replace(
                    safeScript,
                    @"\bCREATE\s+(UNIQUE\s+)?INDEX\s+(?!IF\s+NOT\s+EXISTS\b)",
                    "CREATE $1INDEX IF NOT EXISTS ",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                await ctx.Database.ExecuteSqlRawAsync(safeScript);

                // Auto-migrate schema: ensure newly added entity properties exist as columns in SQLite
                var connection = ctx.Database.GetDbConnection();
                var wasOpen = connection.State == System.Data.ConnectionState.Open;
                if (!wasOpen) await connection.OpenAsync();

                foreach (var entityType in ctx.Model.GetEntityTypes())
                {
                    var tableName = entityType.GetTableName();
                    if (string.IsNullOrEmpty(tableName)) continue;

                    var tableCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            tableCols.Add(reader.GetString(1));
                        }
                    }

                    if (tableCols.Count == 0) continue;

                    // Drop obsolete NOT NULL columns that no longer exist in EF Core model
                    var modelColNames = entityType.GetProperties()
                        .Select(p => p.GetColumnName())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var obsoleteNotNullCols = new List<string>();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            var cName = reader.GetString(1);
                            var notNull = reader.GetInt32(3);
                            var isPk = reader.GetInt32(5);
                            if (isPk == 0 && notNull == 1 && !modelColNames.Contains(cName))
                            {
                                obsoleteNotNullCols.Add(cName);
                            }
                        }
                    }

                    foreach (var obsCol in obsoleteNotNullCols)
                    {
                        try
                        {
#pragma warning disable EF1002
                            await ctx.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{tableName}\" DROP COLUMN \"{obsCol}\";");
#pragma warning restore EF1002
                            tableCols.Remove(obsCol);
                            logger.LogInformation("Dropped obsolete NOT NULL column {Column} from table {Table}", obsCol, tableName);
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug("Could not drop column {Col} from {Table}: {Msg}", obsCol, tableName, ex.Message);
                        }
                    }

                    foreach (var prop in entityType.GetProperties())
                    {
                        var colName = prop.GetColumnName();
                        if (string.IsNullOrEmpty(colName) || tableCols.Contains(colName)) continue;

                        var colType = prop.GetColumnType();
                        if (string.IsNullOrWhiteSpace(colType))
                        {
                            var clr = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
                            if (clr == typeof(int) || clr == typeof(long) || clr == typeof(short) || clr == typeof(bool) || clr.IsEnum)
                                colType = "INTEGER";
                            else if (clr == typeof(decimal) || clr == typeof(double) || clr == typeof(float))
                                colType = "NUMERIC";
                            else if (clr == typeof(byte[]))
                                colType = "BLOB";
                            else
                                colType = "TEXT";
                        }

                        try
                        {
#pragma warning disable EF1002
                            var alterSql = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{colName}\" {colType} NULL;";
                            await ctx.Database.ExecuteSqlRawAsync(alterSql);
#pragma warning restore EF1002
                            logger.LogInformation("Added missing column {Column} ({Type}) to table {Table}", colName, colType, tableName);
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug("Could not add column {Col} to {Table}: {Msg}", colName, tableName, ex.Message);
                        }
                    }
                }

                if (!wasOpen) await connection.CloseAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug("Table/schema sync for {Context}: {Msg}", ctx.GetType().Name, ex.Message);
            }
        }

        // 1. Seed Factor Sets & Emission Factors if missing
        if (!await factorDb.FactorSets.AnyAsync())
        {
            logger.LogInformation("Seeding published emission factor sets...");
            var factorSet = new FactorSet
            {
                Name = "Bangladesh National Grid & IPCC AR6 Standard",
                GwpBasis = "AR6",
                SourceCitation = "IGES Grid Factor 2024 / DEFRA 2024 / IPCC AR6",
                Year = 2024,
                Region = "BD",
                Version = 1,
                ValidFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsPublished = true
            };
            factorDb.FactorSets.Add(factorSet);
            await factorDb.SaveChangesAsync();

            var factors = new List<EmissionFactor>
            {
                new()
                {
                    FactorSetId = factorSet.Id,
                    ActivityType = "grid_electricity",
                    FuelOrMode = "grid",
                    Unit = "kWh",
                    Co2eFactor = 0.621000m,
                    Scope = 2,
                    Tier = "Tier 2",
                    Notes = "IGES Bangladesh National Grid average"
                },
                new()
                {
                    FactorSetId = factorSet.Id,
                    ActivityType = "electricity",
                    FuelOrMode = "grid",
                    Unit = "kWh",
                    Co2eFactor = 0.621000m,
                    Scope = 2,
                    Tier = "Tier 2",
                    Notes = "General electricity alias"
                },
                new()
                {
                    FactorSetId = factorSet.Id,
                    ActivityType = "diesel",
                    FuelOrMode = "diesel",
                    Unit = "litre",
                    Co2eFactor = 2.680000m,
                    Scope = 1,
                    Tier = "Tier 1",
                    Notes = "DEFRA 2024 Diesel 100% mineral"
                },
                new()
                {
                    FactorSetId = factorSet.Id,
                    ActivityType = "naturalgas",
                    FuelOrMode = "gas",
                    Unit = "m3",
                    Co2eFactor = 1.930000m,
                    Scope = 1,
                    Tier = "Tier 1",
                    Notes = "IPCC AR6 Stationary Natural Gas"
                },
                new()
                {
                    FactorSetId = factorSet.Id,
                    ActivityType = "gas",
                    FuelOrMode = "gas",
                    Unit = "m3",
                    Co2eFactor = 1.930000m,
                    Scope = 1,
                    Tier = "Tier 1",
                    Notes = "Gas alias"
                },
                new()
                {
                    FactorSetId = factorSet.Id,
                    ActivityType = "lpg",
                    FuelOrMode = "lpg",
                    Unit = "kg",
                    Co2eFactor = 2.940000m,
                    Scope = 1,
                    Tier = "Tier 1",
                    Notes = "IPCC LPG combustion"
                },
                new()
                {
                    FactorSetId = factorSet.Id,
                    ActivityType = "freight",
                    FuelOrMode = "road_truck",
                    Unit = "tonne_km",
                    Co2eFactor = 0.160000m,
                    Scope = 3,
                    Tier = "Tier 1",
                    Notes = "DEFRA 2024 Rigid HGV Freight Transport"
                }
            };

            factorDb.EmissionFactors.AddRange(factors);
            await factorDb.SaveChangesAsync();
        }

        // 2. Seed Units & Unit Conversions if missing
        if (!await unitsDb.Units.AnyAsync())
        {
            logger.LogInformation("Seeding standard units and canonical conversions...");
            unitsDb.Units.AddRange([
                new Unit { Code = "kWh", NameEn = "Kilowatt Hour", NameBn = "কিলোওয়াট ঘণ্টা", PhysicalQuantity = "Energy" },
                new Unit { Code = "MWh", NameEn = "Megawatt Hour", NameBn = "মেগাওয়াট ঘণ্টা", PhysicalQuantity = "Energy" },
                new Unit { Code = "litre", NameEn = "Litre", NameBn = "লিটার", PhysicalQuantity = "Volume" },
                new Unit { Code = "gallon", NameEn = "Gallon (US)", NameBn = "গ্যালন", PhysicalQuantity = "Volume" },
                new Unit { Code = "m3", NameEn = "Cubic Metre", NameBn = "ঘনমিটার", PhysicalQuantity = "Volume" },
                new Unit { Code = "cft", NameEn = "Cubic Feet", NameBn = "ঘনফুট", PhysicalQuantity = "Volume" },
                new Unit { Code = "kg", NameEn = "Kilogram", NameBn = "কেজি", PhysicalQuantity = "Mass" },
                new Unit { Code = "tonne_km", NameEn = "Tonne-Kilometre", NameBn = "টন-কিলোমিটার", PhysicalQuantity = "Distance/Mass" }
            ]);

            unitsDb.UnitConversions.AddRange([
                new UnitConversion { FromUnit = "mwh", ToUnit = "kwh", ConversionFactor = 1000m, Version = 1 },
                new UnitConversion { FromUnit = "gallon", ToUnit = "litre", ConversionFactor = 3.78541m, Version = 1 },
                new UnitConversion { FromUnit = "cft", ToUnit = "m3", ConversionFactor = 0.0283168m, Version = 1 },
                new UnitConversion { FromUnit = "cylinder_12kg", ToUnit = "kg", ConversionFactor = 12m, Version = 1 },
                new UnitConversion { FromUnit = "cylinder_35kg", ToUnit = "kg", ConversionFactor = 35m, Version = 1 },
                new UnitConversion { FromUnit = "cylinder_45kg", ToUnit = "kg", ConversionFactor = 45m, Version = 1 }
            ]);

            await unitsDb.SaveChangesAsync();
        }

        // 3. Seed Organizations, Users, Sites & Assets if missing
        var apexOrg = await identityDb.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Slug == "apex-textiles");
        if (apexOrg == null)
        {
            apexOrg = new Organization
            {
                Name = "Apex Textile & Garments Ltd.",
                Slug = "apex-textiles",
                Sector = "RMG",
                IsActive = true
            };
            identityDb.Organizations.Add(apexOrg);
            await identityDb.SaveChangesAsync();
        }

        // Factory Team Users (All have identical full access to Apex Org)
        var demoUsers = new[]
        {
            ("owner@apex.local", "Kabir Ahmed", "01711111111"),
            ("accountant@apex.local", "Rahim Mia", "01722222222"),
            ("compliance@apex.local", "Nusrat Jahan", "01733333333"),
            ("floor@apex.local", "Jahid Hasan", "01744444444"),
            ("admin@carbonbill.local", "Platform Administrator", "01700000000")
        };

        var seededUsers = new List<User>();
        foreach (var (email, fullName, phone) in demoUsers)
        {
            var user = await identityDb.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    FullName = fullName,
                    PhoneNumber = phone,
                    PasswordHash = passwordHasher.HashPassword("Pass1234!"),
                    PreferredLanguage = "en",
                    IsActive = true
                };
                identityDb.Users.Add(user);
                await identityDb.SaveChangesAsync();
            }
            else
            {
                if (!passwordHasher.VerifyPassword("Pass1234!", user.PasswordHash))
                {
                    user.PasswordHash = passwordHasher.HashPassword("Pass1234!");
                }
                if (string.IsNullOrWhiteSpace(user.PhoneNumber) || user.PhoneNumber != phone)
                {
                    user.PhoneNumber = phone;
                }
                if (user.PreferredLanguage != "en")
                {
                    user.PreferredLanguage = "en";
                }
                await identityDb.SaveChangesAsync();
            }
            seededUsers.Add(user);

            var hasMembership = await identityDb.Memberships.IgnoreQueryFilters()
                .AnyAsync(m => m.UserId == user.Id && m.OrgId == apexOrg.Id);
            if (!hasMembership)
            {
                identityDb.Memberships.Add(new Membership
                {
                    UserId = user.Id,
                    OrgId = apexOrg.Id,
                    IsActive = true
                });
                await identityDb.SaveChangesAsync();
            }
        }

        var ownerUser = seededUsers[0];

        // Create Demo Teammate Invitation Links if missing
        var demoTokens = new[] { "apex-team-invite-demo", "apex-floor-demo" };
        foreach (var tok in demoTokens)
        {
            var hasInv = await identityDb.Invitations.IgnoreQueryFilters().AnyAsync(i => i.Token == tok);
            if (!hasInv)
            {
                identityDb.Invitations.Add(new Invitation
                {
                    OrgId = apexOrg.Id,
                    Token = tok,
                    ExpiresAtUtc = DateTime.UtcNow.AddYears(1),
                    CreatedByUserId = ownerUser.Id,
                    MaxUses = 1000
                });
                await identityDb.SaveChangesAsync();
            }
        }

        if (await onboardingDb.Sites.IgnoreQueryFilters().AnyAsync(s => s.OrgId == apexOrg.Id))
        {
            logger.LogInformation("Database already contains operational assets. Seeding complete.");
            return;
        }

        // Seed Onboarding Site & Assets for Apex
        var site = new Site
        {
            OrgId = apexOrg.Id,
            Name = "Savar Export Processing Unit",
            Address = "Plot 12-14, DEPZ, Savar",
            City = "Dhaka",
            IsPrimary = true
        };
        onboardingDb.Sites.Add(site);
        await onboardingDb.SaveChangesAsync();

        var meterAsset = new Asset
        {
            OrgId = apexOrg.Id,
            SiteId = site.Id,
            Type = "meter",
            Name = "DESCO Main HT Substation Meter",
            IdentifierOrMeterNumber = "MTR-DEPZ-0891",
            FuelOrEnergyType = "electricity"
        };
        var gensetAsset = new Asset
        {
            OrgId = apexOrg.Id,
            SiteId = site.Id,
            Type = "genset",
            Name = "Cummins 500 kVA Backup Diesel Generator",
            IdentifierOrMeterNumber = "GEN-CUMM-01",
            FuelOrEnergyType = "diesel"
        };
        var boilerAsset = new Asset
        {
            OrgId = apexOrg.Id,
            SiteId = site.Id,
            Type = "boiler",
            Name = "Thermex Gas Steam Boiler 4T",
            IdentifierOrMeterNumber = "BLR-THX-01",
            FuelOrEnergyType = "gas"
        };
        onboardingDb.Assets.AddRange(meterAsset, gensetAsset, boilerAsset);
        await onboardingDb.SaveChangesAsync();

        onboardingDb.ExpectedDocRules.AddRange([
            new ExpectedDocRule { OrgId = apexOrg.Id, AssetId = meterAsset.Id, DocType = "ElectricityBill", Frequency = "Monthly", DueDayOfMonth = 10, IsActive = true },
            new ExpectedDocRule { OrgId = apexOrg.Id, AssetId = gensetAsset.Id, DocType = "diesel_slip", Frequency = "Monthly", DueDayOfMonth = 5, IsActive = true },
            new ExpectedDocRule { OrgId = apexOrg.Id, AssetId = boilerAsset.Id, DocType = "gas_bill", Frequency = "Monthly", DueDayOfMonth = 15, IsActive = true }
        ]);

        onboardingDb.FacilityProfiles.Add(new FacilityProfile
        {
            OrgId = apexOrg.Id,
            HasBoiler = true,
            HasGenset = true,
            RoofAreaSqFt = 35000,
            BuildingOwnership = "Owned",
            BudgetBandBdt = "1000000-5000000",
            AnnualProductionVolume = 1200000,
            ProductionUnit = "piece",
            UpdatedAtUtc = DateTime.UtcNow
        });
        await onboardingDb.SaveChangesAsync();

        // Seed Dataset Version
        adminDb.DatasetVersions.Add(new DatasetVersion
        {
            Kind = "Factors",
            Version = "2024.1",
            Description = "Bangladesh Grid + IPCC AR6 Seed Factors",
            RecordCount = 7,
            ChecksumSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            UploadedByUserId = ownerUser.Id,
            UploadedAtUtc = DateTime.UtcNow,
            IsActive = true
        });
        await adminDb.SaveChangesAsync();

        // Seed Flags Rules from seeds/flags/flag_rules.json
        await flagSeedLoader.SeedFlagRulesAsync();

        // Seed Insights Benchmark Sets
        await insightsSeedLoader.SeedDefaultBenchmarksAsync();

        // Seed Recommendations Measures and Sources
        await recommendationsSeedLoader.SeedDefaultMeasuresAsync();

        logger.LogInformation("Database seeding completed successfully!");
    }
}
