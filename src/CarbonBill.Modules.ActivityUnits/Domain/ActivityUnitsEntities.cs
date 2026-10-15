using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.ActivityUnits.Domain;

public class Unit : BaseEntity
{
    public string Code { get; set; } = string.Empty; // kWh, litre, m3, kg, tonne_km
    public string NameEn { get; set; } = string.Empty;
    public string NameBn { get; set; } = string.Empty;
    public string PhysicalQuantity { get; set; } = string.Empty; // Energy, Volume, Mass, Distance
}

public class UnitConversion : BaseEntity
{
    public string FromUnit { get; set; } = string.Empty;
    public string ToUnit { get; set; } = string.Empty;
    public decimal ConversionFactor { get; set; }
    public int Version { get; set; } = 1;
}

public class ActivityRecord : AggregateRoot, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid DocumentId { get; set; }
    public string ActivityType { get; set; } = "Electricity"; // Electricity, Diesel, NaturalGas, Freight, LPG
    public decimal QuantityStandard { get; set; } // in canonical unit (kWh, litre, m3, kg)
    public string StandardUnit { get; set; } = "kWh";
    public decimal RawQuantity { get; set; }
    public string RawUnit { get; set; } = "kWh";
    public decimal? TotalCostBdt { get; set; }
    public decimal? UnitCostBdt { get; set; }
    public string Period { get; set; } = "2026-09"; // YYYY-MM
    public bool IsEstimated { get; set; }
}
