using CarbonBill.Modules.Extraction.Services;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Providers;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CarbonBill.Modules.Extraction.Endpoints;

public static class ExtractionEndpoints
{
    public static IEndpointRouteBuilder MapExtractionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/extraction")
            .WithTags("Extraction")
            .RequireAuthorization();

        // GET /api/v1/extraction/documents/{documentId:guid}
        group.MapGet("/documents/{documentId:guid}", async (
            Guid documentId,
            IExtractionService extractionService,
            CancellationToken ct) =>
        {
            var run = await extractionService.GetExtractionRunForDocumentAsync(documentId, ct);
            if (run == null)
            {
                return Results.Problem(
                    detail: "Extraction result not found for this document.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found");
            }

            return Results.Ok(new
            {
                id = run.Id,
                documentId = run.DocumentId,
                tierUsed = run.TierUsed,
                status = run.Status,
                confidence = run.OverallConfidence,
                processedAtUtc = run.ProcessedAtUtc,
                fields = run.Fields.Select(f => new
                {
                    id = f.Id,
                    fieldName = f.FieldName,
                    rawValue = f.RawValue,
                    normalizedValue = f.NormalizedValue,
                    correctedValue = f.CorrectedValue,
                    confidence = f.Confidence,
                    sourceTier = f.SourceTier,
                    boundingBoxJson = f.BoundingBoxJson
                })
            });
        }).AllowAnonymous();

        // POST /api/v1/extraction/test-ocr (Multipart Direct Test for utility bills & challans)
        group.MapPost("/test-ocr", async (
            HttpRequest httpRequest,
            IExtractionService extractionService,
            ITenantContext tenantContext,
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
                    detail: "No file was provided for extraction testing.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Missing File");
            }

            await using var stream = file.OpenReadStream();
            var syntheticDocId = Guid.NewGuid();
            var result = await extractionService.ProcessExtractionAsync(
                syntheticDocId,
                stream,
                file.FileName,
                file.ContentType,
                hasTier3Consent: true,
                ct: ct);

            if (!result.IsSuccess)
            {
                return Results.Problem(
                    detail: result.Error,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Extraction Failed");
            }

            var run = result.Value;
            return Results.Ok(new
            {
                success = true,
                documentId = run.DocumentId,
                tierUsed = run.TierUsed,
                confidence = run.OverallConfidence,
                rawOutput = run.RawOutputJson,
                fields = run.Fields.Select(f => new
                {
                    fieldName = f.FieldName,
                    rawValue = f.RawValue,
                    normalizedValue = f.NormalizedValue,
                    confidence = f.Confidence,
                    sourceTier = f.SourceTier
                })
            });
        })
        .AllowAnonymous()
        .DisableAntiforgery();

        return endpoints;
    }
}
