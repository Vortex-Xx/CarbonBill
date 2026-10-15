using System.Text.Json;
using CarbonBill.Modules.Recommendations.Domain;

namespace CarbonBill.Modules.Recommendations.Pipeline.Steps;

public class EligibilityFilterStep
{
    public bool Evaluate(Measure measure, FacilityProfile profile, out string? ineligibilityReason)
    {
        ineligibilityReason = null;

        // 1. Sector check
        if (!string.IsNullOrWhiteSpace(measure.ApplicableSectorsJson))
        {
            try
            {
                var sectors = JsonSerializer.Deserialize<List<string>>(measure.ApplicableSectorsJson) ?? [];
                if (sectors.Count > 0 &&
                    !sectors.Any(s => s.Equals(profile.Sector, StringComparison.OrdinalIgnoreCase) ||
                                      s.Equals("General", StringComparison.OrdinalIgnoreCase)))
                {
                    ineligibilityReason = $"Not applicable to sector '{profile.Sector}'.";
                    return false;
                }
            }
            catch (JsonException) { }
        }

        // 2. Solar PV specific check: Must have known roof area > 0 and owned building
        if (measure.Category.Equals("SolarPV", StringComparison.OrdinalIgnoreCase) ||
            measure.MeasureCode.Contains("SOLAR", StringComparison.OrdinalIgnoreCase))
        {
            if (!profile.RoofAreaSqft.HasValue || profile.RoofAreaSqft.Value <= 0)
            {
                ineligibilityReason = "Rooftop solar requires verified roof area (unknown or 0 sqft).";
                return false;
            }

            if (!profile.BuildingOwnership.Equals("Owned", StringComparison.OrdinalIgnoreCase))
            {
                ineligibilityReason = "Rooftop solar requires building ownership ('Owned'). Rented properties ineligible.";
                return false;
            }
        }

        // 3. Applicability rules json evaluation
        if (!string.IsNullOrWhiteSpace(measure.ApplicabilityRulesJson) && measure.ApplicabilityRulesJson != "{}")
        {
            try
            {
                using var doc = JsonDocument.Parse(measure.ApplicabilityRulesJson);
                var root = doc.RootElement;

                // has_boiler
                if (root.TryGetProperty("has_boiler", out var bProp) && bProp.GetBoolean() && !profile.HasBoiler)
                {
                    ineligibilityReason = "Facility has no boiler.";
                    return false;
                }

                // has_genset
                if (root.TryGetProperty("has_genset", out var gProp) && gProp.GetBoolean() && !profile.HasGenset)
                {
                    ineligibilityReason = "Facility has no generator.";
                    return false;
                }

                // has_production_floor
                if (root.TryGetProperty("has_production_floor", out var fProp) && fProp.GetBoolean() && !profile.HasProductionFloor)
                {
                    ineligibilityReason = "Facility has no production floor.";
                    return false;
                }

                // grid_electricity
                if (root.TryGetProperty("grid_electricity", out var geProp) && geProp.GetBoolean() && !profile.GridElectricity)
                {
                    ineligibilityReason = "Facility is not connected to grid electricity.";
                    return false;
                }

                // monthly_kwh_gt
                if (root.TryGetProperty("monthly_kwh_gt", out var kwhProp) && profile.BaselineMonthlyKwh <= (decimal)kwhProp.GetDouble())
                {
                    ineligibilityReason = $"Monthly electricity ({profile.BaselineMonthlyKwh} kWh) is below rule threshold ({kwhProp.GetDouble()} kWh).";
                    return false;
                }

                // roof_area_sqft_gt
                if (root.TryGetProperty("roof_area_sqft_gt", out var roofProp))
                {
                    decimal minRoof = (decimal)roofProp.GetDouble();
                    if (!profile.RoofAreaSqft.HasValue || profile.RoofAreaSqft.Value <= minRoof)
                    {
                        ineligibilityReason = $"Roof area ({profile.RoofAreaSqft ?? 0} sqft) is below required {minRoof} sqft.";
                        return false;
                    }
                }

                // building_ownership
                if (root.TryGetProperty("building_ownership", out var ownProp))
                {
                    var reqOwn = ownProp.GetString();
                    if (!string.IsNullOrWhiteSpace(reqOwn) && !profile.BuildingOwnership.Equals(reqOwn, StringComparison.OrdinalIgnoreCase))
                    {
                        ineligibilityReason = $"Requires '{reqOwn}' building ownership; current facility is '{profile.BuildingOwnership}'.";
                        return false;
                    }
                }
            }
            catch (JsonException) { }
        }

        return true;
    }
}
