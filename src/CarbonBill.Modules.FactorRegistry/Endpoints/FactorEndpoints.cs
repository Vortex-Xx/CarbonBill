using CarbonBill.Modules.FactorRegistry.Domain;
using CarbonBill.Modules.FactorRegistry.Persistence;
using CarbonBill.Modules.FactorRegistry.Services;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.FactorRegistry.Endpoints;

public record CreateFactorOverrideRequest(Guid FactorId, decimal OverrideValue, string Justification);

public static class FactorEndpoints
{
    public static IEndpointRouteBuilder MapFactorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Factors");

        group.MapGet("/factors", async (
            ITenantContext tenantContext,
            IFactorLookup factorLookup,
            CancellationToken ct) =>
        {
            var factors = await factorLookup.GetAllCurrentFactorsAsync(tenantContext.CurrentOrgId, ct);

            return Results.Ok(factors.Select(f => new
            {
                id = f.FactorId,
                factorSetName = f.FactorSetName,
                activityType = f.ActivityType,
                factorValue = f.Co2eFactor,
                unit = f.Unit,
                scope = f.Scope,
                source = f.SourceCitation,
                gwpBasis = f.GwpBasis,
                isOverridden = f.IsOverridden,
                justification = f.Justification
            }));
        });

        group.MapPost("/factor-overrides", async (
            [FromBody] CreateFactorOverrideRequest request,
            ITenantContext tenantContext,
            FactorRegistryDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Justification))
            {
                return Results.Problem(
                    detail: "Mandatory justification is required for factor override.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Missing Justification");
            }

            var factor = await dbContext.EmissionFactors.FindAsync([request.FactorId], ct);
            if (factor == null)
            {
                return Results.Problem(
                    detail: "Specified emission factor was not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Factor Not Found");
            }

            var orgId = tenantContext.CurrentOrgId.Value;
            var existingOverride = await dbContext.FactorOverrides
                .FirstOrDefaultAsync(o => o.OrgId == orgId && o.EmissionFactorId == request.FactorId, ct);

            if (existingOverride != null)
            {
                existingOverride.OverrideValue = request.OverrideValue;
                existingOverride.Justification = request.Justification;
                existingOverride.ApprovedByUserId = tenantContext.CurrentUserId ?? Guid.Empty;
                existingOverride.ApprovedAtUtc = DateTime.UtcNow;
                existingOverride.IsActive = true;
            }
            else
            {
                var newOverride = new FactorOverride
                {
                    OrgId = orgId,
                    EmissionFactorId = request.FactorId,
                    OverrideValue = request.OverrideValue,
                    Justification = request.Justification,
                    ApprovedByUserId = tenantContext.CurrentUserId ?? Guid.Empty,
                    ApprovedAtUtc = DateTime.UtcNow,
                    IsActive = true
                };
                dbContext.FactorOverrides.Add(newOverride);
            }

            await dbContext.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/factor-overrides/{request.FactorId}", new
            {
                id = request.FactorId,
                overrideValue = request.OverrideValue,
                status = "Approved",
                justification = request.Justification
            });
        });

        group.MapGet("/factor-overrides", async (
            ITenantContext tenantContext,
            FactorRegistryDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var overrides = await dbContext.FactorOverrides
                .Include(o => o.EmissionFactor)
                .ToListAsync(ct);

            return Results.Ok(overrides.Select(o => new
            {
                o.Id,
                o.EmissionFactorId,
                ActivityType = o.EmissionFactor?.ActivityType ?? "Unknown",
                Unit = o.EmissionFactor?.Unit ?? "",
                DefaultFactor = o.EmissionFactor?.Co2eFactor ?? 0,
                o.OverrideValue,
                o.Justification,
                o.ApprovedAtUtc,
                o.IsActive
            }));
        });

        return endpoints;
    }
}
