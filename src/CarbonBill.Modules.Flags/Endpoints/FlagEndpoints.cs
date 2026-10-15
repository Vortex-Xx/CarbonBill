using CarbonBill.Modules.Flags.Domain;
using CarbonBill.Modules.Flags.Persistence;
using CarbonBill.Modules.Flags.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Flags.Endpoints;

public record DismissFlagRequest(string Reason);

public record UpdateFlagRuleRequest(
    string? TriggerParametersJson,
    string? DefaultSeverity,
    bool? IsActive);

public static class FlagEndpoints
{
    public static IEndpointRouteBuilder MapFlagEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/flags")
            .WithTags("Flags");

        // GET /api/v1/flags?state=open&period=2026-09
        group.MapGet("/", async (
            string? state,
            string? period,
            string? severity,
            int? page,
            int? pageSize,
            IFlagReader flagReader,
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

            var p = Math.Max(1, page ?? 1);
            var size = Math.Clamp(pageSize ?? 20, 1, 100);

            var flags = await flagReader.GetFlagsAsync(
                orgId: orgId,
                period: period,
                state: state,
                severity: severity,
                page: p,
                pageSize: size,
                ct: ct);

            return Results.Ok(flags);
        });

        // GET /api/v1/flags/top5 (Dashboard Query: Top 5 by severity then recency)
        group.MapGet("/top5", async (
            IFlagReader flagReader,
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

            var top5 = await flagReader.GetDashboardTop5FlagsAsync(orgId, ct);
            return Results.Ok(top5);
        });

        // POST /api/v1/flags/{id}/acknowledge
        group.MapPost("/{id:guid}/acknowledge", async (
            Guid id,
            FlagsService flagsService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated && !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var orgId = tenantContext.CurrentOrgId ?? Guid.Empty;
            var success = await flagsService.AcknowledgeFlagAsync(id, orgId, ct);
            return success ? Results.Ok() : Results.NotFound();
        });

        // POST /api/v1/flags/{id}/dismiss
        group.MapPost("/{id:guid}/dismiss", async (
            Guid id,
            DismissFlagRequest request,
            FlagsService flagsService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated && !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "A mandatory non-empty justification reason is required to dismiss a flag." });
            }

            var orgId = tenantContext.CurrentOrgId ?? Guid.Empty;
            var success = await flagsService.DismissFlagAsync(id, orgId, request.Reason, ct);
            return success ? Results.Ok(new { message = "Flag dismissed for 30 days." }) : Results.NotFound();
        });

        // POST /api/v1/flags/evaluate
        group.MapPost("/evaluate", async (
            string? period,
            IFlagEngine flagEngine,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated && !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var orgId = tenantContext.CurrentOrgId ?? Guid.Empty;
            var evalPeriod = period ?? DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

            var raised = await flagEngine.EvaluateOrgPeriodAsync(orgId, evalPeriod, null, ct);
            return Results.Ok(new { evaluatedPeriod = evalPeriod, raisedCount = raised.Count, flags = raised });
        });

        // GET /api/v1/flags/rules (Platform Admin inspects tunable rules)
        group.MapGet("/rules", async (
            FlagsDbContext dbContext,
            CancellationToken ct) =>
        {
            var rules = await dbContext.FlagRules.ToListAsync(ct);
            return Results.Ok(rules);
        });

        // PUT /api/v1/flags/rules/{code} (Platform Admin updates parameters without deployment)
        group.MapPut("/rules/{code}", async (
            string code,
            UpdateFlagRuleRequest request,
            FlagsDbContext dbContext,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var rule = await dbContext.FlagRules.FirstOrDefaultAsync(r => r.FlagCode == code, ct);
            if (rule == null)
            {
                return Results.NotFound();
            }

            if (request.TriggerParametersJson != null)
                rule.TriggerParametersJson = request.TriggerParametersJson;

            if (!string.IsNullOrWhiteSpace(request.DefaultSeverity))
                rule.DefaultSeverity = request.DefaultSeverity;

            if (request.IsActive.HasValue)
                rule.IsActive = request.IsActive.Value;

            rule.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            return Results.Ok(rule);
        });

        return endpoints;
    }
}
