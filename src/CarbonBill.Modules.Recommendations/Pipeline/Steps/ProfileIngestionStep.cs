using CarbonBill.Modules.Recommendations.Domain;

namespace CarbonBill.Modules.Recommendations.Pipeline.Steps;

public class ProfileIngestionStep
{
    public EnergyActivityBaseline Execute(
        FacilityProfile profile,
        decimal? billTotalElectricityCostBdt = null,
        decimal? billTotalKwh = null,
        decimal? billTotalDieselCostBdt = null,
        decimal? billTotalDieselLitres = null)
    {
        decimal annualKwh = profile.BaselineMonthlyKwh * 12.0m;
        decimal annualDiesel = profile.BaselineMonthlyDieselLitres * 12.0m;
        decimal annualGas = profile.BaselineMonthlyGasM3 * 12.0m;

        // Derive electricity tariff directly from bills if present, fallback to profile/default
        decimal elecTariff = (billTotalKwh.HasValue && billTotalKwh.Value > 0 && billTotalElectricityCostBdt.HasValue && billTotalElectricityCostBdt.Value > 0)
            ? Math.Round(billTotalElectricityCostBdt.Value / billTotalKwh.Value, 2)
            : (profile.ElectricityTariffBdtPerKwh > 0 ? profile.ElectricityTariffBdtPerKwh : 10.50m);

        // Derive diesel tariff directly from bills if present, fallback to profile/default
        decimal dieselTariff = (billTotalDieselLitres.HasValue && billTotalDieselLitres.Value > 0 && billTotalDieselCostBdt.HasValue && billTotalDieselCostBdt.Value > 0)
            ? Math.Round(billTotalDieselCostBdt.Value / billTotalDieselLitres.Value, 2)
            : (profile.DieselTariffBdtPerLitre > 0 ? profile.DieselTariffBdtPerLitre : 108.00m);

        decimal gasTariff = profile.GasTariffBdtPerM3 > 0 ? profile.GasTariffBdtPerM3 : 30.00m;

        return new EnergyActivityBaseline(
            AnnualKwh: annualKwh,
            AnnualDieselLitres: annualDiesel,
            AnnualGasM3: annualGas,
            ElectricityTariffBdtPerKwh: elecTariff,
            DieselTariffBdtPerLitre: dieselTariff,
            GasTariffBdtPerM3: gasTariff);
    }
}
