using CarbonBill.Modules.Calculation.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CarbonBill.Modules.Calculation.Endpoints;

public static class CalculationEndpoints
{
    public static IEndpointRouteBuilder MapCalculationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/emissions").WithTags("Emissions");

        group.MapGet("/summary", async (
            [FromQuery] string? period,
            ITenantContext tenantContext,
            IEmissionReadModel readModel,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var summary = await readModel.GetSummaryAsync(tenantContext.CurrentOrgId.Value, period, ct);
            return Results.Ok(summary);
        });

        group.MapGet("/breakdown", async (
            [FromQuery] string? period,
            ITenantContext tenantContext,
            IEmissionReadModel readModel,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var breakdown = await readModel.GetScopeBreakdownAsync(tenantContext.CurrentOrgId.Value, period, ct);
            return Results.Ok(breakdown);
        });

        group.MapGet("/trend", async (
            [FromQuery] int months,
            ITenantContext tenantContext,
            IEmissionReadModel readModel,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var trend = await readModel.GetMonthlyTrendAsync(tenantContext.CurrentOrgId.Value, months > 0 ? months : 12, ct);
            return Results.Ok(trend);
        });

        return endpoints;
    }
}
