using System.Text.RegularExpressions;
using CarbonBill.Modules.Insights.Services;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CarbonBill.Modules.Insights.Endpoints;

public record CreateProductionMetricRequest(
    Guid? SiteId,
    string Period,
    string Unit,
    decimal Quantity);

public static class InsightsEndpoints
{
    private static readonly Regex PeriodRegex = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    public static IEndpointRouteBuilder MapInsightsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // 1. Dashboard Intensity Endpoint
        endpoints.MapGet("/api/v1/dashboard/intensity", async (
            [FromQuery] string? period,
            [FromQuery] string? sector,
            [FromQuery] string? sizeBand,
            ITenantContext tenantContext,
            IInsightsService insightsService,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await insightsService.GetIntensityDashboardAsync(
                orgId: tenantContext.CurrentOrgId.Value,
                period: period,
                sector: string.IsNullOrWhiteSpace(sector) ? "RMG" : sector,
                sizeBand: string.IsNullOrWhiteSpace(sizeBand) ? "Medium" : sizeBand,
                minPeerCount: 10,
                ct: ct);

            return Results.Ok(result);
        })
        .WithName("GetDashboardIntensity")
        .WithTags("Dashboard");

        // 2. Production Metric Capture Endpoints
        var metricsGroup = endpoints.MapGroup("/api/v1/insights/production-metrics").WithTags("ProductionMetrics");

        metricsGroup.MapPost("/", async (
            [FromBody] CreateProductionMetricRequest request,
            ITenantContext tenantContext,
            IInsightsService insightsService,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            if (request.Quantity <= 0)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Quantity",
                    detail: "Production quantity must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(request.Period) || !PeriodRegex.IsMatch(request.Period))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Period",
                    detail: "Period must be in YYYY-MM format.");
            }

            if (string.IsNullOrWhiteSpace(request.Unit))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Unit",
                    detail: "Production output unit cannot be empty.");
            }

            var metric = await insightsService.CaptureMetricAsync(
                orgId: tenantContext.CurrentOrgId.Value,
                siteId: request.SiteId,
                period: request.Period,
                unit: request.Unit.Trim().ToLowerInvariant(),
                quantity: request.Quantity,
                ct: ct);

            return Results.Created($"/api/v1/insights/production-metrics/{metric.Id}", metric);
        })
        .WithName("CaptureProductionMetric");

        metricsGroup.MapGet("/", async (
            [FromQuery] Guid? siteId,
            ITenantContext tenantContext,
            IInsightsService insightsService,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var list = await insightsService.GetMetricsAsync(tenantContext.CurrentOrgId.Value, siteId, ct);
            return Results.Ok(list);
        })
        .WithName("GetProductionMetrics");

        return endpoints;
    }
}
