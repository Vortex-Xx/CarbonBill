using CarbonBill.Modules.Recommendations.Domain;

namespace CarbonBill.Modules.Recommendations.Pipeline.Steps;

public record FinancialResult(
    SavingsRange AnnualSavingsBdt,
    CapexRange CapexBdt,
    decimal PaybackYears,
    decimal CostPerTco2e,
    decimal AnnualisedCapexBdt);

public class FinancialModelingStep
{
    public FinancialResult Calculate(Measure measure, EnergyActivityBaseline baseline, SavingsRange avoidedTco2e)
    {
        decimal baselineActivity;
        decimal tariff;

        if (measure.Category.Equals("Boilers", StringComparison.OrdinalIgnoreCase) ||
            measure.Category.Equals("Boiler", StringComparison.OrdinalIgnoreCase))
        {
            if (baseline.AnnualGasM3 > 0)
            {
                baselineActivity = baseline.AnnualGasM3;
                tariff = baseline.GasTariffBdtPerM3;
            }
            else
            {
                baselineActivity = baseline.AnnualDieselLitres;
                tariff = baseline.DieselTariffBdtPerLitre;
            }
        }
        else if (measure.Category.Equals("Generators", StringComparison.OrdinalIgnoreCase) ||
                 measure.Category.Equals("Diesel", StringComparison.OrdinalIgnoreCase))
        {
            baselineActivity = baseline.AnnualDieselLitres;
            tariff = baseline.DieselTariffBdtPerLitre;
        }
        else
        {
            baselineActivity = baseline.AnnualKwh;
            tariff = baseline.ElectricityTariffBdtPerKwh;
        }

        // 1. Annual Savings in BDT
        decimal savingsLowBdt = Math.Round(baselineActivity * measure.SavingLow * tariff, 2);
        decimal savingsTypicalBdt = Math.Round(baselineActivity * measure.SavingTypical * tariff, 2);
        decimal savingsHighBdt = Math.Round(baselineActivity * measure.SavingHigh * tariff, 2);

        if (savingsTypicalBdt < savingsLowBdt) savingsTypicalBdt = savingsLowBdt;
        if (savingsHighBdt < savingsTypicalBdt) savingsHighBdt = savingsTypicalBdt;

        var savingsRange = new SavingsRange(savingsLowBdt, savingsTypicalBdt, savingsHighBdt);

        // 2. Capex
        decimal capexLow = measure.CapexLow;
        decimal capexHigh = measure.CapexHigh;
        if (capexHigh < capexLow) capexHigh = capexLow;
        var capexRange = new CapexRange(capexLow, capexHigh);

        decimal avgCapex = (capexLow + capexHigh) / 2.0m;

        // 3. Simple Payback Years (Capex / Annual Savings)
        decimal paybackYears = savingsTypicalBdt > 0
            ? Math.Round(avgCapex / savingsTypicalBdt, 2)
            : 0m;

        // 4. Annualised Capex (Capex / Lifetime)
        int lifetime = measure.LifetimeYears > 0 ? measure.LifetimeYears : 5;
        decimal annualisedCapex = Math.Round(avgCapex / lifetime, 2);

        // 5. Cost per Avoided tCO2e = (Annualised Capex - Annual Savings) / Annual Avoided tCO2e
        // Note: A NEGATIVE value indicates that the annual savings exceed annualized capex (net financial profit per tonne).
        decimal costPerTco2e = avoidedTco2e.Typical > 0
            ? Math.Round((annualisedCapex - savingsTypicalBdt) / avoidedTco2e.Typical, 2)
            : 0m;

        return new FinancialResult(
            AnnualSavingsBdt: savingsRange,
            CapexBdt: capexRange,
            PaybackYears: paybackYears,
            CostPerTco2e: costPerTco2e,
            AnnualisedCapexBdt: annualisedCapex);
    }
}
