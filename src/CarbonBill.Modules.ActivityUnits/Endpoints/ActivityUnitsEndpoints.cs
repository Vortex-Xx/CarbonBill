using CarbonBill.Modules.ActivityUnits.Persistence;
using CarbonBill.Modules.ActivityUnits.Services;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.ActivityUnits.Endpoints;

public record ConvertUnitRequest(decimal Quantity, string FromUnit, string ActivityType);

public static class ActivityUnitsEndpoints
{
    public static IEndpointRouteBuilder MapActivityUnitsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/units").WithTags("Units");

        group.MapGet("/conversions", async (
            ActivityUnitsDbContext dbContext,
            CancellationToken ct) =>
        {
            var conversions = await dbContext.UnitConversions.AsNoTracking().ToListAsync(ct);
            return Results.Ok(conversions.Select(c => new
            {
                c.FromUnit,
                c.ToUnit,
                c.ConversionFactor,
                c.Version
            }));
        });

        group.MapPost("/convert", async (
            [FromBody] ConvertUnitRequest request,
            IUnitConverter unitConverter,
            CancellationToken ct) =>
        {
            var result = await unitConverter.ConvertToCanonicalAsync(
                request.Quantity,
                request.FromUnit,
                request.ActivityType,
                ct);

            return Results.Ok(new
            {
                rawQuantity = request.Quantity,
                rawUnit = request.FromUnit,
                canonicalQuantity = result.CanonicalQuantity,
                canonicalUnit = result.CanonicalUnit,
                conversionFactor = result.ConversionFactor,
                conversionVersion = result.ConversionVersion
            });
        });

        return endpoints;
    }
}
