using System.Security.Claims;
using CarbonBill.Modules.Documents.Domain;
using CarbonBill.Modules.Documents.Services;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CarbonBill.Modules.Documents.Endpoints;

public static class DocumentsEndpoints
{
    public static IEndpointRouteBuilder MapDocumentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/documents")
            .WithTags("Documents")
            .RequireAuthorization();

        // POST /api/v1/documents (Multipart Upload)
        group.MapPost("/", async (
            HttpRequest httpRequest,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            IDocumentService documentService,
            ITenantContext tenantContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (!httpRequest.HasFormContentType)
            {
                return Results.Problem(
                    detail: "Expected multipart/form-data content.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Content Type");
            }

            var form = await httpRequest.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? (form.Files.Count > 0 ? form.Files[0] : null);
            if (file == null || file.Length == 0)
            {
                return Results.Problem(
                    detail: "No file was provided in the upload request.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Missing File");
            }

            var userId = tenantContext.CurrentUserId ?? Guid.Empty;
            var source = form["source"].ToString();
            if (string.IsNullOrWhiteSpace(source))
            {
                source = "phone";
            }

            var docType = form["docType"].ToString();
            var category = form["category"].ToString();
            if (string.IsNullOrWhiteSpace(docType) && !string.IsNullOrWhiteSpace(category))
            {
                docType = category.ToLowerInvariant() switch
                {
                    "gas" => "GasBill",
                    "electricity" => "ElectricityBill",
                    "diesel" => "DieselSlip",
                    "shipment" => "ShippingChallan",
                    _ => null
                };
            }

            await using var stream = file.OpenReadStream();
            var result = await documentService.UploadDocumentAsync(
                stream,
                file.FileName,
                file.ContentType,
                userId,
                idempotencyKey,
                source,
                docType,
                ct);

            if (!result.IsSuccess)
            {
                return Results.Problem(
                    detail: result.Error,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Upload Failed");
            }

            var receipt = result.Value;
            var statusCode = receipt.IsDuplicate ? StatusCodes.Status200OK : StatusCodes.Status202Accepted;

            return Results.Json(new
            {
                id = receipt.Id,
                status = receipt.Status,
                isDuplicate = receipt.IsDuplicate,
                receipt = new
                {
                    received = receipt.Received,
                    timestamp = receipt.CapturedAtUtc,
                    fileName = receipt.FileName,
                    fileSizeBytes = receipt.FileSizeBytes
                }
            }, statusCode: statusCode);
        })
        .AllowAnonymous()
        .DisableAntiforgery();

        // GET /api/v1/documents
        group.MapGet("/", async (
            [FromQuery] string? status,
            [FromQuery] int? limit,
            IDocumentService documentService,
            CancellationToken ct) =>
        {
            var items = await documentService.GetDocumentsAsync(status, limit ?? 20, ct);
            return Results.Ok(new { items, count = items.Count });
        })
        .AllowAnonymous();

        // GET /api/v1/documents/{id:guid}/file (Serve Document Image / PDF)
        group.MapGet("/{id:guid}/file", async (
            Guid id,
            IDocumentService documentService,
            CancellationToken ct) =>
        {
            var result = await documentService.GetDocumentFileAsync(id, ct);
            if (!result.IsSuccess)
            {
                return Results.NotFound(new { error = result.Error });
            }

            var (fileStream, contentType, fileName) = result.Value;
            return Results.File(fileStream, contentType, fileName, enableRangeProcessing: true);
        })
        .AllowAnonymous();

        // GET /api/v1/documents/{id}
        group.MapGet("/{id:guid}", async (
            Guid id,
            IDocumentService documentService,
            CancellationToken ct) =>
        {
            var result = await documentService.GetDocumentByIdAsync(id, ct);
            if (!result.IsSuccess)
            {
                return Results.Problem(
                    detail: result.Error,
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Document Not Found");
            }

            return Results.Ok(result.Value);
        })
        .AllowAnonymous();

        // POST /api/v1/documents/manual-entry
        group.MapPost("/manual-entry", async (
            [FromBody] ManualEntryRequest request,
            IDocumentService documentService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            if (request.Quantity <= 0)
            {
                return Results.Problem(
                    detail: "Quantity must be greater than zero.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Quantity");
            }

            var userId = tenantContext.CurrentUserId ?? Guid.Empty;
            var result = await documentService.CreateManualEntryAsync(request, userId, ct);

            if (!result.IsSuccess)
            {
                return Results.Problem(
                    detail: result.Error,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Manual Entry Failed");
            }

            return Results.Created($"/api/v1/documents/{result.Value.Id}", new
            {
                id = result.Value.Id,
                status = result.Value.Status,
                isManualFallback = true
            });
        })
        .AllowAnonymous();

        // GET /api/v1/documents/my-submissions
        group.MapGet("/my-submissions", async (
            [FromQuery] int? limit,
            IDocumentService documentService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var userId = tenantContext.CurrentUserId ?? Guid.Empty;
            var items = await documentService.GetMySubmissionsAsync(userId, limit ?? 20, ct);
            return Results.Ok(items);
        })
        .AllowAnonymous();

        return endpoints;
    }
}
