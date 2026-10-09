using CarbonBill.Modules.Audit.Persistence;
using CarbonBill.Modules.Documents.Persistence;
using CarbonBill.Modules.Extraction.Persistence;
using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.Modules.IdentityTenancy.Services;
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
        var reviewDb = scope.ServiceProvider.GetRequiredService<CarbonBill.Modules.Review.Persistence.ReviewDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        logger.LogInformation("Ensuring SQLite database and tables are created...");
        await identityDb.Database.EnsureCreatedAsync();

        var otherContexts = new DbContext[] { auditDb, documentsDb, extractionDb, reviewDb };
        foreach (var ctx in otherContexts)
        {
            try
            {
                var script = ctx.Database.GenerateCreateScript();
                if (!string.IsNullOrWhiteSpace(script))
                {
                    var statements = script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var stmt in statements)
                    {
                        var safeStmt = stmt.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.OrdinalIgnoreCase);
                        await ctx.Database.ExecuteSqlRawAsync(safeStmt + ";");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Table ensure creation warning for context {Context}", ctx.GetType().Name);
            }
        }

        if (await identityDb.Organizations.AnyAsync())
        {
            logger.LogInformation("Database already contains seed data. Skipping seeding.");
            return;
        }

        logger.LogInformation("Seeding development organizations, users, memberships, and demo invitation...");

        // Create Demo Organization
        var apexOrg = new Organization
        {
            Name = "Apex Textile & Garments Ltd.",
            Slug = "apex-textiles",
            Sector = "RMG",
            IsActive = true
        };
        identityDb.Organizations.Add(apexOrg);

        // Platform Admin
        var adminUser = new User
        {
            Email = "admin@carbonbill.local",
            FullName = "Platform Administrator",
            PasswordHash = passwordHasher.HashPassword("Admin1234!"),
            PreferredLanguage = "en",
            IsPlatformAdmin = true
        };
        identityDb.Users.Add(adminUser);

        // Factory Owner (Kabir / Nusrat)
        var ownerUser = new User
        {
            Email = "owner@apex.local",
            FullName = "Kabir Ahmed",
            PasswordHash = passwordHasher.HashPassword("Pass1234!"),
            PreferredLanguage = "bn"
        };
        identityDb.Users.Add(ownerUser);

        // Accountant (Rahim)
        var accountantUser = new User
        {
            Email = "accountant@apex.local",
            FullName = "Rahim Mia",
            PasswordHash = passwordHasher.HashPassword("Pass1234!"),
            PreferredLanguage = "bn"
        };
        identityDb.Users.Add(accountantUser);

        // Compliance Officer
        var complianceUser = new User
        {
            Email = "compliance@apex.local",
            FullName = "Nusrat Jahan",
            PasswordHash = passwordHasher.HashPassword("Pass1234!"),
            PreferredLanguage = "bn"
        };
        identityDb.Users.Add(complianceUser);

        // Floor Staff (Jahid)
        var floorUser = new User
        {
            Email = "floor@apex.local",
            FullName = "Jahid Hasan",
            PhoneNumber = "01700000000",
            PasswordHash = passwordHasher.HashPassword("Pass1234!"),
            PreferredLanguage = "bn"
        };
        identityDb.Users.Add(floorUser);

        // Sustainability Consultant (Farhana)
        var consultantUser = new User
        {
            Email = "consultant@greenadvisory.local",
            FullName = "Farhana Rahman",
            PasswordHash = passwordHasher.HashPassword("Pass1234!"),
            PreferredLanguage = "en"
        };
        identityDb.Users.Add(consultantUser);

        await identityDb.SaveChangesAsync();

        // Assign Memberships
        identityDb.Memberships.AddRange([
            new Membership { UserId = ownerUser.Id, OrgId = apexOrg.Id, Role = Roles.Owner },
            new Membership { UserId = accountantUser.Id, OrgId = apexOrg.Id, Role = Roles.Accountant },
            new Membership { UserId = complianceUser.Id, OrgId = apexOrg.Id, Role = Roles.Compliance },
            new Membership { UserId = floorUser.Id, OrgId = apexOrg.Id, Role = Roles.FloorStaff },
            new Membership { UserId = consultantUser.Id, OrgId = apexOrg.Id, Role = Roles.Consultant },
            new Membership { UserId = adminUser.Id, OrgId = apexOrg.Id, Role = Roles.PlatformAdmin }
        ]);

        // Create Demo QR + PIN invitation for Floor Staff
        var invitation = new Invitation
        {
            OrgId = apexOrg.Id,
            Role = Roles.FloorStaff,
            Token = "apex-floor-demo",
            PinHash = passwordHasher.HashPassword("1234"),
            ExpiresAtUtc = DateTime.UtcNow.AddYears(1),
            CreatedByUserId = ownerUser.Id,
            MaxUses = 100
        };
        identityDb.Invitations.Add(invitation);

        await identityDb.SaveChangesAsync();
        logger.LogInformation("Database seeding completed successfully!");
    }
}
