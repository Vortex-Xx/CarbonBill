using CarbonBill.Modules.Recommendations.Domain;

namespace CarbonBill.Modules.Recommendations.Pipeline.Steps;

public class CarbonImpactStep
{
    private const decimal GridFactorKgCo2ePerKwh = 0.620000m; // BD Grid average factor
    private const decimal DieselFactorKgCo2ePerLitre = 2.680000m; // DEFRA / IPCC Stationary Diesel
    private const decimal GasFactorKgCo2ePerM3 = 2.020000m; // Natural Gas Stationary Combustion

    public SavingsRange Calculate(Measure measure, EnergyActivityBaseline baseline)
    {
        decimal baselineActivity;
        decimal factor;

        if (measure.Category.Equals("Boilers", StringComparison.OrdinalIgnoreCase) ||
            measure.Category.Equals("Boiler", StringComparison.OrdinalIgnoreCase))
        {
            if (baseline.AnnualGasM3 > 0)
            {
                baselineActivity = baseline.AnnualGasM3;
                factor = GasFactorKgCo2ePerM3;
            }
            else
            {
                baselineActivity = baseline.AnnualDieselLitres;
                factor = DieselFactorKgCo2ePerLitre;
            }
        }
        else if (measure.Category.Equals("Generators", StringComparison.OrdinalIgnoreCase) ||
                 measure.Category.Equals("Diesel", StringComparison.OrdinalIgnoreCase))
        {
            baselineActivity = baseline.AnnualDieselLitres;
            factor = DieselFactorKgCo2ePerLitre;
        }
        else
        {
            // Default to Electricity (Motors, Lighting, CompressedAir, SolarPV, PowerFactor)
            baselineActivity = baseline.AnnualKwh;
            factor = GridFactorKgCo2ePerKwh;
        }

        decimal low = Math.Round((baselineActivity * measure.SavingLow * factor) / 1000.0m, 3);
        decimal typical = Math.Round((baselineActivity * measure.SavingTypical * factor) / 1000.0m, 3);
        decimal high = Math.Round((baselineActivity * measure.SavingHigh * factor) / 1000.0m, 3);

        // Ensure range integrity
        if (typical < low) typical = low;
        if (high < typical) high = typical;

        return new SavingsRange(low, typical, high);
    }
}
