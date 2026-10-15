using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.Onboarding.Domain;

public class Site : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public bool IsPrimary { get; set; } = true;
    public List<Asset> Assets { get; set; } = [];
}

public class Asset : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid SiteId { get; set; }
    public Site Site { get; set; } = null!;
    public string Type { get; set; } = "meter"; // meter, genset, boiler, vehicle
    public string Name { get; set; } = string.Empty;
    public string? IdentifierOrMeterNumber { get; set; }
    public string? FuelOrEnergyType { get; set; } // electricity, diesel, gas, lpg
    public List<ExpectedDocRule> ExpectedDocRules { get; set; } = [];
}

public class ExpectedDocRule : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public string DocType { get; set; } = "ElectricityBill"; // ElectricityBill, diesel_slip, gas_bill
    public string Frequency { get; set; } = "Monthly"; // Monthly, Quarterly, PerDelivery
    public int DueDayOfMonth { get; set; } = 15;
    public Guid? ResponsibleUserId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FacilityProfile : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public bool HasBoiler { get; set; }
    public bool HasGenset { get; set; }
    public decimal RoofAreaSqFt { get; set; }
    public string BuildingOwnership { get; set; } = "Owned"; // Owned, Leased
    public string BudgetBandBdt { get; set; } = "500000-2000000";
    public decimal? AnnualProductionVolume { get; set; }
    public string? ProductionUnit { get; set; } = "piece"; // piece, kg, metre
}
