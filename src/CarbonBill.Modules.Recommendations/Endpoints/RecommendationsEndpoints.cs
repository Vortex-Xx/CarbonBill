using CarbonBill.Modules.Recommendations.Pipeline.Steps;
using CarbonBill.Modules.Recommendations.Services;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CarbonBill.Modules.Recommendations.Endpoints;

public static class RecommendationsEndpoints
{
    public static IEndpointRouteBuilder MapRecommendationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Recommendations");

        // GET /api/v1/recommendations
        group.MapGet("/recommendations", async (
            [FromQuery] Guid? siteId,
            ITenantContext tenantContext,
            RecommendationsService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await service.GetRecommendationsResponseAsync(tenantContext.CurrentOrgId.Value, siteId, ct);
            return Results.Ok(result);
        })
        .WithName("GetRecommendations")
        .WithSummary("Get ranked energy-efficiency and carbon reduction recommendations (top 5 and full abatement list).");

        // PUT /api/v1/recommendations/{id}/status
        group.MapPut("/recommendations/{id:guid}/status", async (
            [FromRoute] Guid id,
            [FromBody] StatusUpdateRequest request,
            ITenantContext tenantContext,
            RecommendationsService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            try
            {
                var updated = await service.UpdateStatusAsync(tenantContext.CurrentOrgId.Value, id, request, ct);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Error");
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Argument");
            }
        })
        .WithName("UpdateRecommendationStatus")
        .WithSummary("Update recommendation status to Planned, Done, or NotFeasible (with mandatory reason).");

        // POST /api/v1/profile/facility (Shared with Onboarding, agreed via handoff)
        group.MapPost("/profile/facility", async (
            [FromBody] UpdateFacilityProfileRequest request,
            ITenantContext tenantContext,
            RecommendationsService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var profile = await service.SaveFacilityProfileAsync(tenantContext.CurrentOrgId.Value, request, ct);
            return Results.Ok(new
            {
                status = "ProfileUpdated",
                message = "Facility profile saved and recommendations recalculated.",
                profile
            });
        })
        .WithName("SaveFacilityProfile")
        .WithSummary("Save facility profile questionnaire and baseline data, triggering recommendation recalculation.");

        // POST /api/v1/recommendations/recalculate
        group.MapPost("/recommendations/recalculate", async (
            [FromQuery] Guid? siteId,
            ITenantContext tenantContext,
            RecommendationsService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var recommendations = await service.GenerateAndSaveRecommendationsAsync(tenantContext.CurrentOrgId.Value, siteId, ct);
            return Results.Ok(new
            {
                status = "Recalculated",
                count = recommendations.Count
            });
        })
        .WithName("RecalculateRecommendations")
        .WithSummary("Trigger immediate re-execution of the 8-step recommendation pipeline.");

        return endpoints;
    }
}
