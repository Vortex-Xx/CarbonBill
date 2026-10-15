using CarbonBill.Modules.Onboarding.Domain;
using CarbonBill.Modules.Onboarding.Persistence;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Onboarding.Endpoints;

public record CreateSiteRequest(string Name, string? Location, string? Address = null, string? City = null, bool IsPrimary = true);
public record CreateAssetRequest(Guid SiteId, string Type, string Name, string? IdentifierOrMeterNumber = null, string? FuelOrEnergyType = null);
public record CreateRuleRequest(Guid AssetId, string DocType, string Frequency, int DueDay, Guid? ResponsibleUserId = null);
public record FacilityProfileRequest(
    bool HasBoiler,
    bool HasGenset,
    decimal RoofAreaSqFt,
    string BuildingOwnership = "Owned",
    string BudgetBandBdt = "500000-2000000",
    decimal? AnnualProductionVolume = null,
    string? ProductionUnit = "piece");

public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/onboarding").WithTags("Onboarding");

        // Sites
        group.MapPost("/sites", async (
            [FromBody] CreateSiteRequest request,
            ITenantContext tenantContext,
            OnboardingDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var site = new Site
            {
                OrgId = tenantContext.CurrentOrgId.Value,
                Name = request.Name,
                Address = request.Address ?? request.Location,
                City = request.City,
                IsPrimary = request.IsPrimary
            };

            dbContext.Sites.Add(site);
            await dbContext.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/onboarding/sites/{site.Id}", new
            {
                id = site.Id,
                name = site.Name,
                site.Address,
                site.City,
                site.IsPrimary
            });
        });

        group.MapGet("/sites", async (
            ITenantContext tenantContext,
            OnboardingDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var sites = await dbContext.Sites
                .Include(s => s.Assets)
                .ThenInclude(a => a.ExpectedDocRules)
                .ToListAsync(ct);

            return Results.Ok(sites.Select(s => new
            {
                s.Id,
                s.Name,
                s.Address,
                s.City,
                s.IsPrimary,
                s.CreatedAtUtc,
                AssetsCount = s.Assets.Count
            }));
        });

        // Assets
        group.MapPost("/assets", async (
            [FromBody] CreateAssetRequest request,
            ITenantContext tenantContext,
            OnboardingDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var siteExists = await dbContext.Sites.AnyAsync(s => s.Id == request.SiteId, ct);
            if (!siteExists)
            {
                return Results.Problem(
                    detail: "Specified site was not found.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Site");
            }

            var asset = new Asset
            {
                OrgId = tenantContext.CurrentOrgId.Value,
                SiteId = request.SiteId,
                Type = request.Type,
                Name = request.Name,
                IdentifierOrMeterNumber = request.IdentifierOrMeterNumber,
                FuelOrEnergyType = request.FuelOrEnergyType
            };

            dbContext.Assets.Add(asset);
            await dbContext.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/onboarding/assets/{asset.Id}", new
            {
                id = asset.Id,
                siteId = asset.SiteId,
                type = asset.Type,
                name = asset.Name,
                identifierOrMeterNumber = asset.IdentifierOrMeterNumber,
                fuelOrEnergyType = asset.FuelOrEnergyType
            });
        });

        group.MapGet("/assets", async (
            [FromQuery] Guid? siteId,
            ITenantContext tenantContext,
            OnboardingDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var query = dbContext.Assets.AsQueryable();
            if (siteId.HasValue)
            {
                query = query.Where(a => a.SiteId == siteId.Value);
            }

            var assets = await query
                .Include(a => a.ExpectedDocRules)
                .ToListAsync(ct);

            return Results.Ok(assets.Select(a => new
            {
                a.Id,
                a.SiteId,
                a.Type,
                a.Name,
                a.IdentifierOrMeterNumber,
                a.FuelOrEnergyType,
                RulesCount = a.ExpectedDocRules.Count
            }));
        });

        // Rules
        group.MapPost("/rules", async (
            [FromBody] CreateRuleRequest request,
            ITenantContext tenantContext,
            OnboardingDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var assetExists = await dbContext.Assets.AnyAsync(a => a.Id == request.AssetId, ct);
            if (!assetExists)
            {
                return Results.Problem(
                    detail: "Specified asset was not found.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Asset");
            }

            var rule = new ExpectedDocRule
            {
                OrgId = tenantContext.CurrentOrgId.Value,
                AssetId = request.AssetId,
                DocType = request.DocType,
                Frequency = request.Frequency,
                DueDayOfMonth = request.DueDay,
                ResponsibleUserId = request.ResponsibleUserId,
                IsActive = true
            };

            dbContext.ExpectedDocRules.Add(rule);
            await dbContext.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/onboarding/rules/{rule.Id}", new
            {
                id = rule.Id,
                status = "Active",
                docType = rule.DocType,
                frequency = rule.Frequency,
                dueDay = rule.DueDayOfMonth
            });
        });

        group.MapGet("/rules", async (
            ITenantContext tenantContext,
            OnboardingDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var rules = await dbContext.ExpectedDocRules
                .Include(r => r.Asset)
                .ToListAsync(ct);

            return Results.Ok(rules.Select(r => new
            {
                r.Id,
                r.AssetId,
                AssetName = r.Asset?.Name ?? "Unknown",
                r.DocType,
                r.Frequency,
                dueDay = r.DueDayOfMonth,
                r.ResponsibleUserId,
                r.IsActive
            }));
        });

        // Facility Profile
        group.MapPost("/profile/facility", async (
            [FromBody] FacilityProfileRequest request,
            ITenantContext tenantContext,
            OnboardingDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var orgId = tenantContext.CurrentOrgId.Value;
            var profile = await dbContext.FacilityProfiles.FirstOrDefaultAsync(p => p.OrgId == orgId, ct);

            if (profile == null)
            {
                profile = new FacilityProfile
                {
                    OrgId = orgId,
                    HasBoiler = request.HasBoiler,
                    HasGenset = request.HasGenset,
                    RoofAreaSqFt = request.RoofAreaSqFt,
                    BuildingOwnership = request.BuildingOwnership,
                    BudgetBandBdt = request.BudgetBandBdt,
                    AnnualProductionVolume = request.AnnualProductionVolume,
                    ProductionUnit = request.ProductionUnit ?? "piece",
                    UpdatedAtUtc = DateTime.UtcNow
                };
                dbContext.FacilityProfiles.Add(profile);
            }
            else
            {
                profile.HasBoiler = request.HasBoiler;
                profile.HasGenset = request.HasGenset;
                profile.RoofAreaSqFt = request.RoofAreaSqFt;
                profile.BuildingOwnership = request.BuildingOwnership;
                profile.BudgetBandBdt = request.BudgetBandBdt;
                profile.AnnualProductionVolume = request.AnnualProductionVolume;
                profile.ProductionUnit = request.ProductionUnit ?? "piece";
                profile.UpdatedAtUtc = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(ct);
            return Results.Ok(new { status = "ProfileSaved", profile.UpdatedAtUtc });
        });

        group.MapGet("/profile/facility", async (
            ITenantContext tenantContext,
            OnboardingDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var orgId = tenantContext.CurrentOrgId.Value;
            var profile = await dbContext.FacilityProfiles.FirstOrDefaultAsync(p => p.OrgId == orgId, ct);
            if (profile == null)
            {
                return Results.NotFound(new { message = "No facility profile recorded for this organization." });
            }

            return Results.Ok(new
            {
                profile.OrgId,
                profile.HasBoiler,
                profile.HasGenset,
                profile.RoofAreaSqFt,
                profile.BuildingOwnership,
                profile.BudgetBandBdt,
                profile.AnnualProductionVolume,
                profile.ProductionUnit,
                profile.UpdatedAtUtc
            });
        });

        // RMG Template Onboarding
        group.MapPost("/templates/rmg", async (
            ITenantContext tenantContext,
            OnboardingDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var orgId = tenantContext.CurrentOrgId.Value;

            // Check if site already exists
            var site = await dbContext.Sites.FirstOrDefaultAsync(s => s.OrgId == orgId, ct);
            if (site == null)
            {
                site = new Site
                {
                    OrgId = orgId,
                    Name = "Main Factory Site",
                    Address = "Plot 42, Export Processing Zone, Savar",
                    City = "Dhaka",
                    IsPrimary = true
                };
                dbContext.Sites.Add(site);
                await dbContext.SaveChangesAsync(ct);
            }

            // Create standard RMG assets if none exist
            var existingAssets = await dbContext.Assets.Where(a => a.SiteId == site.Id).ToListAsync(ct);
            if (existingAssets.Count == 0)
            {
                var gridMeter = new Asset
                {
                    OrgId = orgId,
                    SiteId = site.Id,
                    Type = "meter",
                    Name = "BREB / DESCO Main Substation Meter",
                    IdentifierOrMeterNumber = "MTR-DESCO-001",
                    FuelOrEnergyType = "electricity"
                };

                var dieselGenset = new Asset
                {
                    OrgId = orgId,
                    SiteId = site.Id,
                    Type = "genset",
                    Name = "Cummins 500 kVA Backup Generator",
                    IdentifierOrMeterNumber = "GEN-CUMM-01",
                    FuelOrEnergyType = "diesel"
                };

                var gasBoiler = new Asset
                {
                    OrgId = orgId,
                    SiteId = site.Id,
                    Type = "boiler",
                    Name = "Thermex Gas Steam Boiler 4T",
                    IdentifierOrMeterNumber = "BLR-THX-01",
                    FuelOrEnergyType = "gas"
                };

                dbContext.Assets.AddRange(gridMeter, dieselGenset, gasBoiler);
                await dbContext.SaveChangesAsync(ct);

                // Add default monthly rules
                var rules = new List<ExpectedDocRule>
                {
                    new()
                    {
                        OrgId = orgId,
                        AssetId = gridMeter.Id,
                        DocType = "ElectricityBill",
                        Frequency = "Monthly",
                        DueDayOfMonth = 10,
                        IsActive = true
                    },
                    new()
                    {
                        OrgId = orgId,
                        AssetId = dieselGenset.Id,
                        DocType = "diesel_slip",
                        Frequency = "Monthly",
                        DueDayOfMonth = 5,
                        IsActive = true
                    },
                    new()
                    {
                        OrgId = orgId,
                        AssetId = gasBoiler.Id,
                        DocType = "gas_bill",
                        Frequency = "Monthly",
                        DueDayOfMonth = 15,
                        IsActive = true
                    }
                };

                dbContext.ExpectedDocRules.AddRange(rules);
                await dbContext.SaveChangesAsync(ct);
            }

            return Results.Ok(new
            {
                status = "RmgTemplateApplied",
                siteId = site.Id,
                message = "Standard RMG setup created with Grid Electricity, Diesel Generator, and Steam Boiler assets."
            });
        });

        return endpoints;
    }
}
