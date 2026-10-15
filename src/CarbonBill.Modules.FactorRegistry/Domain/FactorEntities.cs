using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.FactorRegistry.Domain;

public class FactorSet : BaseEntity
{
    public string Name { get; set; } = string.Empty; // e.g. "IGES Bangladesh Grid 2025", "DEFRA 2026", "IPCC AR6 Stationary"
    public string GwpBasis { get; set; } = "AR6"; // AR5, AR6
    public string SourceCitation { get; set; } = string.Empty;
    public int Year { get; set; } = 2024;
    public string Region { get; set; } = "BD";
    public int Version { get; set; } = 1;
    public DateTime ValidFrom { get; set; } = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public DateTime? ValidTo { get; set; }
    public bool IsPublished { get; set; } = true;
    public List<EmissionFactor> Factors { get; set; } = [];
}

public class EmissionFactor : BaseEntity
{
    public Guid FactorSetId { get; set; }
    public FactorSet FactorSet { get; set; } = null!;
    public string ActivityType { get; set; } = string.Empty; // Electricity, Diesel, NaturalGas, Freight, LPG
    public string FuelOrMode { get; set; } = string.Empty;
    public string Unit { get; set; } = "kWh";
    public decimal Co2eFactor { get; set; } // kg CO2e per unit
    public int Scope { get; set; } = 1; // 1, 2, 3
    public string? Tier { get; set; } = "Tier 1";
    public string? Notes { get; set; }
}

public class FactorOverride : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid EmissionFactorId { get; set; }
    public EmissionFactor EmissionFactor { get; set; } = null!;
    public decimal OverrideValue { get; set; }
    public string Justification { get; set; } = string.Empty; // Mandatory verification rationale
    public Guid ApprovedByUserId { get; set; }
    public DateTime ApprovedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
