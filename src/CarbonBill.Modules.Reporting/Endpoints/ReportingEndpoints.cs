using CarbonBill.Modules.Reporting.Services;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CarbonBill.Modules.Reporting.Endpoints;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Reporting");

        // 1. POST /api/v1/reports
        group.MapPost("/reports", async (
            [FromBody] CreateReportRequest request,
            ITenantContext tenantContext,
            ReportingService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var report = await service.CreateReportAsync(tenantContext.CurrentOrgId.Value, request, ct);
            return Results.Created($"/api/v1/reports/{report.Id}", new
            {
                id = report.Id,
                title = report.Title,
                reportingPeriod = report.ReportingPeriod,
                status = report.Status,
                dataQualityScore = report.DataQualityScore,
                totalKgCo2e = report.TotalKgCo2e
            });
        })
        .WithName("CreateReport")
        .WithSummary("Create a new greenhouse gas accounting report in Draft status.");

        // 2. POST /api/v1/reports/{id}/submit-review
        group.MapPost("/reports/{id:guid}/submit-review", async (
            [FromRoute] Guid id,
            ITenantContext tenantContext,
            ReportingService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            try
            {
                var report = await service.SubmitForReviewAsync(tenantContext.CurrentOrgId.Value, id, ct);
                return Results.Ok(new { id = report.Id, status = report.Status });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("SubmitReportForReview")
        .WithSummary("Transition report from Draft to ReadyForReview.");

        // 3. POST /api/v1/reports/{id}/approve
        group.MapPost("/reports/{id:guid}/approve", async (
            [FromRoute] Guid id,
            ITenantContext tenantContext,
            ReportingService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            try
            {
                var userId = tenantContext.CurrentUserId ?? Guid.Empty;
                var snapshot = await service.ApproveAndLockReportAsync(tenantContext.CurrentOrgId.Value, id, userId, ct);

                return Results.Ok(new
                {
                    reportId = snapshot.ReportId,
                    status = "Locked",
                    version = snapshot.Version,
                    contentHashSha256 = snapshot.ContentHashSha256,
                    frozenAtUtc = snapshot.FrozenAtUtc,
                    message = "Report approved, locked, and cryptographically frozen."
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("ApproveReport")
        .WithSummary("Approve and permanently lock report, creating an immutable SHA-256 frozen snapshot.");

        // 4. GET /api/v1/reports/{id}/pdf
        group.MapGet("/reports/{id:guid}/pdf", async (
            [FromRoute] Guid id,
            [FromQuery] string? lang,
            [FromQuery] bool? redactPrices,
            ITenantContext tenantContext,
            ReportingService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            try
            {
                string language = string.Equals(lang, "bn", StringComparison.OrdinalIgnoreCase) ? "bn" : "en";
                byte[] pdfBytes = await service.RenderReportPdfAsync(tenantContext.CurrentOrgId.Value, id, language, redactPrices, ct);

                string fileName = $"CarbonBill-GHG-Report-{id}-{language}.pdf";
                return Results.File(pdfBytes, "application/pdf", fileName);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        })
        .WithName("DownloadReportPdf")
        .WithSummary("Download generated GHG Protocol corporate inventory PDF report via QuestPDF.");

        // 5. GET /api/v1/reports/{id}/excel
        group.MapGet("/reports/{id:guid}/excel", async (
            [FromRoute] Guid id,
            [FromQuery] bool? redactPrices,
            ITenantContext tenantContext,
            ReportingService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            try
            {
                byte[] xlsxBytes = await service.RenderReportExcelAsync(tenantContext.CurrentOrgId.Value, id, redactPrices, ct);
                string fileName = $"CarbonBill-GHG-DataSheet-{id}.xlsx";
                return Results.File(xlsxBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        })
        .WithName("DownloadReportExcel")
        .WithSummary("Download structured multi-sheet Excel data workbook via ClosedXML.");

        // 6. POST /api/v1/reports/{id}/share
        group.MapPost("/reports/{id:guid}/share", async (
            [FromRoute] Guid id,
            [FromBody] CreateShareLinkRequest request,
            ITenantContext tenantContext,
            ReportingService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            try
            {
                var userId = tenantContext.CurrentUserId ?? Guid.Empty;
                var link = await service.CreateShareLinkAsync(tenantContext.CurrentOrgId.Value, id, request, userId, ct);

                return Results.Created($"/api/v1/reports/share/{link.Token}", new
                {
                    token = link.Token,
                    shareUrl = $"/auditor/verify?token={link.Token}",
                    expiresAtUtc = link.ExpiresAtUtc,
                    redactPrices = link.RedactPrices
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        })
        .WithName("CreateShareLink")
        .WithSummary("Generate an expiring read-only auditor share link with optional price redaction.");

        // 7. GET /api/v1/reports/share/{token}
        group.MapGet("/reports/share/{token}", async (
            [FromRoute] string token,
            ReportingService service,
            CancellationToken ct) =>
        {
            try
            {
                var (payload, link) = await service.GetSharedReportAsync(token, ct);
                return Results.Ok(new
                {
                    token = link.Token,
                    expiresAtUtc = link.ExpiresAtUtc,
                    redactPrices = link.RedactPrices,
                    viewCount = link.ViewCount,
                    report = payload
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized, title: "Expired Link");
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("GetSharedReport")
        .WithSummary("Read-only public auditor inspection endpoint for valid share link tokens.");

        // 8. GET /api/v1/reports/{id}/auditor/trace
        group.MapGet("/reports/{id:guid}/auditor/trace", async (
            [FromRoute] Guid id,
            [FromQuery] string? docId,
            ITenantContext tenantContext,
            ReportingService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            try
            {
                var trace = await service.GetAuditorTraceAsync(tenantContext.CurrentOrgId.Value, id, docId ?? string.Empty, ct);
                return Results.Ok(trace);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        })
        .WithName("GetAuditorTrace")
        .WithSummary("Auditor verification trace mapping a specific reported figure directly to source document and factor.");

        // 9. Dashboard Summary: GET /api/v1/dashboard/summary
        group.MapGet("/dashboard/summary", async (
            [FromQuery] string? period,
            ITenantContext tenantContext,
            ReportingService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var summary = await service.GetDashboardSummaryAsync(tenantContext.CurrentOrgId.Value, period, ct);
            return Results.Ok(summary);
        })
        .WithName("GetDashboardSummary")
        .WithSummary("Executive summary metrics for dashboards (Scope 1/2/3, DQS, verified vs estimated split).");

        // 10. Dashboard Trend: GET /api/v1/dashboard/trend
        group.MapGet("/dashboard/trend", async (
            ITenantContext tenantContext,
            ReportingService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated || !tenantContext.CurrentOrgId.HasValue)
            {
                return Results.Unauthorized();
            }

            var trend = await service.GetDashboardTrendAsync(tenantContext.CurrentOrgId.Value, ct);
            return Results.Ok(trend);
        })
        .WithName("GetDashboardTrend")
        .WithSummary("Monthly emissions trend with verified vs estimated split and hatched styling flag.");

        // 11. Consultant Clients: GET /api/v1/consultant/clients
        group.MapGet("/consultant/clients", async (
            ITenantContext tenantContext,
            ReportingService service,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var clients = await service.GetConsultantClientsAsync(ct);
            return Results.Ok(clients);
        })
        .WithName("GetConsultantClients")
        .WithSummary("Portfolio overview of client organizations for sustainability consultants.");

        // 12. OpenTelemetry Business Metrics: GET /api/v1/metrics/business
        group.MapGet("/metrics/business", () =>
        {
            var snapshot = CarbonBill.Modules.Reporting.Observability.CarbonBillMetrics.GetCurrentSnapshot();
            return Results.Ok(snapshot);
        })
        .WithName("GetBusinessMetrics")
        .WithSummary("OpenTelemetry business metrics snapshot for Prometheus/Grafana Cloud monitoring.")
        .AllowAnonymous();

        return endpoints;
    }
}
