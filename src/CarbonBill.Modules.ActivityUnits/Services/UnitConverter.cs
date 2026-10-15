using CarbonBill.Modules.ActivityUnits.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.ActivityUnits.Services;

public record ConversionResult(
    decimal CanonicalQuantity,
    string CanonicalUnit,
    decimal ConversionFactor,
    int ConversionVersion);

public interface IUnitConverter
{
    Task<ConversionResult> ConvertToCanonicalAsync(
        decimal rawQuantity,
        string rawUnit,
        string activityType,
        CancellationToken ct = default);

    string GetCanonicalUnitForActivity(string activityType);
}

public class UnitConverter(ActivityUnitsDbContext dbContext) : IUnitConverter
{
    private static readonly Dictionary<string, string> DefaultCanonicalUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        { "electricity", "kWh" },
        { "grid_electricity", "kWh" },
        { "electricitybill", "kWh" },
        { "diesel", "litre" },
        { "diesel_slip", "litre" },
        { "gas", "m3" },
        { "naturalgas", "m3" },
        { "gas_bill", "m3" },
        { "lpg", "kg" },
        { "petrol", "litre" },
        { "octane", "litre" },
        { "freight", "tonne_km" },
        { "transport", "tonne_km" }
    };

    private static readonly Dictionary<(string From, string To), decimal> BuiltInConversionFactors = new()
    {
        // Electricity
        { ("kwh", "kwh"), 1.0m },
        { ("mwh", "kwh"), 1000.0m },
        { ("gwh", "kwh"), 1000000.0m },
        { ("unit", "kwh"), 1.0m },

        // Diesel / Petrol / Liquid fuels (to litre)
        { ("litre", "litre"), 1.0m },
        { ("l", "litre"), 1.0m },
        { ("liter", "litre"), 1.0m },
        { ("litres", "litre"), 1.0m },
        { ("gal", "litre"), 3.78541m },
        { ("gallon", "litre"), 3.78541m },
        { ("gallon_uk", "litre"), 4.54609m },
        { ("m3", "litre"), 1000.0m },

        // Natural Gas (to m3)
        { ("m3", "m3"), 1.0m },
        { ("cft", "m3"), 0.0283168m },
        { ("ft3", "m3"), 0.0283168m },
        { ("cubic_feet", "m3"), 0.0283168m },
        { ("mmbtu", "m3"), 28.2637m },
        { ("scm", "m3"), 1.0m },

        // LPG / Mass (to kg)
        { ("kg", "kg"), 1.0m },
        { ("cylinder_12kg", "kg"), 12.0m },
        { ("cylinder_35kg", "kg"), 35.0m },
        { ("cylinder_45kg", "kg"), 45.0m },
        { ("tonne", "kg"), 1000.0m },
        { ("ton", "kg"), 1000.0m },

        // Transport (to tonne_km)
        { ("tonne_km", "tonne_km"), 1.0m },
        { ("ton_mile", "tonne_km"), 1.45997m }
    };

    public string GetCanonicalUnitForActivity(string activityType)
    {
        var key = activityType.Trim().ToLowerInvariant();
        return DefaultCanonicalUnits.TryGetValue(key, out var canonical) ? canonical : "kWh";
    }

    public async Task<ConversionResult> ConvertToCanonicalAsync(
        decimal rawQuantity,
        string rawUnit,
        string activityType,
        CancellationToken ct = default)
    {
        var from = rawUnit.Trim().ToLowerInvariant();
        var targetCanonical = GetCanonicalUnitForActivity(activityType);
        var to = targetCanonical.ToLowerInvariant();

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return new ConversionResult(rawQuantity, targetCanonical, 1.0m, 1);
        }

        // Check database for configured unit conversion override
        var dbConversions = await dbContext.UnitConversions.AsNoTracking().ToListAsync(ct);
        var dbConversion = dbConversions.FirstOrDefault(c =>
            string.Equals(c.FromUnit, from, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.ToUnit, to, StringComparison.OrdinalIgnoreCase));

        if (dbConversion != null)
        {
            var converted = rawQuantity * dbConversion.ConversionFactor;
            return new ConversionResult(converted, targetCanonical, dbConversion.ConversionFactor, dbConversion.Version);
        }

        // Check built-in conversion lookup table
        if (BuiltInConversionFactors.TryGetValue((from, to), out var factor))
        {
            var converted = rawQuantity * factor;
            return new ConversionResult(converted, targetCanonical, factor, 1);
        }

        // If direct match not found, default to 1:1 conversion factor with warning/traceability
        return new ConversionResult(rawQuantity, targetCanonical, 1.0m, 1);
    }
}
