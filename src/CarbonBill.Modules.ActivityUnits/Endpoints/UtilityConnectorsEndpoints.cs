using CarbonBill.Modules.ActivityUnits.Domain;
using CarbonBill.Modules.ActivityUnits.Persistence;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.ActivityUnits.Endpoints;

public record UtilityIngestRequest(
    string UtilityProvider, // DESCO, DPDC, Titas Gas, REB, NESCO
    string MeterNumber,
    string AccountNumber,
    string BillingPeriod, // YYYY-MM
    decimal Quantity,
    string Unit, // kWh, m3, cft
    decimal? TotalCostBdt = null,
    string? InvoiceNumber = null,
    Guid? SiteId = null,
    Guid? AssetId = null);

public record UtilityIngestResponse(
    bool Success,
    Guid ActivityId,
    string UtilityProvider,
    string BillingPeriod,
    decimal CanonicalQuantity,
    string CanonicalUnit,
    string Message);

public static class UtilityConnectorsEndpoints
{
    public static IEndpointRouteBuilder MapUtilityConnectorsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/utilities").WithTags("Utilities");

        // Webhook / Direct digital ingestion endpoint for Bangladesh utilities (DESCO, DPDC, Titas Gas)
        group.MapPost("/ingest", async (
            [FromBody] UtilityIngestRequest request,
            ITenantContext tenantContext,
            IActivityWriter activityWriter,
            ActivityUnitsDbContext dbContext,
            ILogger<ActivityUnitsDbContext> logger,
            CancellationToken ct) =>
        {
            var orgId = tenantContext.CurrentOrgId ?? Guid.Parse("f0ee71e0-717b-4b5e-af05-a443c6430a8f");

            if (string.IsNullOrWhiteSpace(request.UtilityProvider))
            {
                return Results.Problem(
                    detail: "Utility provider must be specified (e.g. DESCO, DPDC, Titas Gas).",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Missing Utility Provider");
            }

            if (request.Quantity <= 0)
            {
                return Results.Problem(
                    detail: "Billed utility quantity must be greater than zero.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Quantity");
            }

            if (string.IsNullOrWhiteSpace(request.BillingPeriod))
            {
                return Results.Problem(
                    detail: "Billing period must be specified in YYYY-MM format.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Missing Billing Period");
            }

            // Map provider to canonical activity type
            var provider = request.UtilityProvider.Trim().ToUpperInvariant();
            var activityType = provider switch
            {
                "DESCO" or "DPDC" or "BPDB" or "REB" or "NESCO" or "BREB" => "Electricity",
                "TITAS" or "TITAS GAS" or "BAKHRABAD" or "JALALABAD" or "KGDCL" or "GAS" => "NaturalGas",
                _ => provider.Contains("GAS") ? "NaturalGas" : "Electricity"
            };

            // Synthetic or provided digital intake document ID
            var digitalDocId = Guid.NewGuid();

            var activityRequest = new ConfirmedActivityRequest(
                OrgId: orgId,
                SiteId: request.SiteId,
                AssetId: request.AssetId,
                DocumentId: digitalDocId,
                ActivityType: activityType,
                Quantity: request.Quantity,
                Unit: request.Unit,
                TotalCostBdt: request.TotalCostBdt,
                BillingPeriod: request.BillingPeriod,
                IsEstimated: false);

            var result = await activityWriter.RecordConfirmedActivityAsync(activityRequest, ct);

            if (result.IsFailure)
            {
                logger.LogError("Direct utility ingestion failed for {Provider}: {Error}", provider, result.Error);
                return Results.Problem(
                    detail: result.Error,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Utility Ingestion Failed");
            }

            var canonicalUnit = activityType == "Electricity" ? "kWh" : "m3";

            return Results.Ok(new UtilityIngestResponse(
                Success: true,
                ActivityId: result.Value,
                UtilityProvider: provider,
                BillingPeriod: request.BillingPeriod,
                CanonicalQuantity: request.Quantity,
                CanonicalUnit: canonicalUnit,
                Message: $"Successfully ingested {provider} digital bill for period {request.BillingPeriod}."));
        });

        // Provider metadata endpoint
        group.MapGet("/providers", () =>
        {
            var providers = new[]
            {
                new { Code = "DESCO", Name = "Dhaka Electric Supply Company Limited", Sector = "Electricity", StandardUnit = "kWh" },
                new { Code = "DPDC", Name = "Dhaka Power Distribution Company Limited", Sector = "Electricity", StandardUnit = "kWh" },
                new { Code = "BPDB", Name = "Bangladesh Power Development Board", Sector = "Electricity", StandardUnit = "kWh" },
                new { Code = "BREB", Name = "Bangladesh Rural Electrification Board", Sector = "Electricity", StandardUnit = "kWh" },
                new { Code = "TITAS", Name = "Titas Gas Transmission and Distribution Co. Ltd.", Sector = "Natural Gas", StandardUnit = "m3" },
                new { Code = "BAKHRABAD", Name = "Bakhrabad Gas Distribution Company Limited", Sector = "Natural Gas", StandardUnit = "m3" }
            };

            return Results.Ok(providers);
        });

        return endpoints;
    }
}
