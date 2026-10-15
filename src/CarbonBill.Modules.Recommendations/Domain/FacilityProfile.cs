using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.Recommendations.Domain;

public class FacilityProfile : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid? SiteId { get; set; }
    public string Sector { get; set; } = "RMG";

    // Questionnaire fields
    public bool HasBoiler { get; set; }
    public bool HasGenset { get; set; } = true;
    public bool HasProductionFloor { get; set; } = true;
    public bool GridElectricity { get; set; } = true;
    public string BuildingOwnership { get; set; } = "Owned"; // Owned or Rented
    public decimal? RoofAreaSqft { get; set; } // Null if unknown
    public string BudgetBand { get; set; } = "Medium";
    public decimal? MaxBudgetBdt { get; set; }

    // Baseline energy activities (monthly)
    public decimal BaselineMonthlyKwh { get; set; } = 25000m;
    public decimal BaselineMonthlyDieselLitres { get; set; } = 800m;
    public decimal BaselineMonthlyGasM3 { get; set; }

    // Effective utility tariffs derived from bills or defaults
    public decimal ElectricityTariffBdtPerKwh { get; set; } = 10.50m;
    public decimal DieselTariffBdtPerLitre { get; set; } = 108.00m;
    public decimal GasTariffBdtPerM3 { get; set; } = 30.00m;
}
