using CarbonBill.Modules.GapDetection.Services;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarbonBill.Modules.GapDetection.Endpoints;

public static class GapEndpoints
{
    public static IEndpointRouteBuilder MapGapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/gaps")
            .WithTags("GapDetection");

        // GET /api/v1/gaps?period=2026-09&status=Open
        group.MapGet("/", async (
            string? period,
            string? status,
            IGapDetector gapDetector,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated && !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var orgId = tenantContext.CurrentOrgId ?? Guid.Empty;
            if (orgId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Active organization context is required." });
            }

            var alerts = await gapDetector.GetAlertsAsync(orgId, period, status, ct);
            return Results.Ok(alerts);
        });

        // POST /api/v1/gaps/{id}/nudge
        group.MapPost("/{id:guid}/nudge", async (
            Guid id,
            IGapDetector gapDetector,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated && !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var orgId = tenantContext.CurrentOrgId ?? Guid.Empty;
            if (orgId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Active organization context is required." });
            }

            var result = await gapDetector.NudgeAlertAsync(id, orgId, ct);
            if (result.IsFailure)
            {
                return Results.Problem(
                    detail: result.Error,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Nudge Failed");
            }

            var alert = result.Value;
            return Results.Ok(new
            {
                alert.Id,
                alert.AssetName,
                alert.DocType,
                alert.NudgeCount,
                alert.LastNudgedAtUtc,
                Message = $"Responsible staff nudged successfully: '{alert.PlainRequestMessageBn}'"
            });
        });

        // POST /api/v1/gaps/evaluate?period=2026-09
        group.MapPost("/evaluate", async (
            string? period,
            IGapDetector gapDetector,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated && !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var orgId = tenantContext.CurrentOrgId ?? Guid.Empty;
            if (orgId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Active organization context is required." });
            }

            var targetPeriod = !string.IsNullOrWhiteSpace(period)
                ? period
                : DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

            var alerts = await gapDetector.CheckMissingDocumentsAsync(orgId, targetPeriod, ct);
            return Results.Ok(new
            {
                Period = targetPeriod,
                EvaluatedAlertsCount = alerts.Count,
                Alerts = alerts.Select(a => new
                {
                    a.Id,
                    a.AssetName,
                    a.DocType,
                    a.Status,
                    a.Severity,
                    a.EscalationState,
                    a.PlainRequestMessageBn,
                    a.PlainRequestMessageEn
                })
            });
        });

        return endpoints;
    }
}
