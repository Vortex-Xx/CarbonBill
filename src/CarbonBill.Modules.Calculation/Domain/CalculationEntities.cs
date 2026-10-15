using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.Calculation.Domain;

public class EmissionResult : AggregateRoot, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid ActivityRecordId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid FactorId { get; set; }
    public int FactorVersion { get; set; } = 1;
    public int ConversionVersion { get; set; } = 1;
    public int Scope { get; set; } = 1; // 1, 2, 3
    public string Category { get; set; } = string.Empty; // Grid Electricity, Stationary Combustion, Mobile Combustion
    public decimal QuantityStandard { get; set; }
    public string StandardUnit { get; set; } = string.Empty;
    public decimal EmissionFactorUsed { get; set; }
    public decimal KgCo2e { get; set; } // Formula: QuantityStandard * EmissionFactorUsed
    public decimal TonnesCo2e => Math.Round(KgCo2e / 1000m, 6);
    public string Period { get; set; } = "2026-09";
    public bool IsEstimated { get; set; }
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class RecalculationProposal : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid OldFactorId { get; set; }
    public Guid NewFactorId { get; set; }
    public decimal OldTotalKgCo2e { get; set; }
    public decimal NewTotalKgCo2e { get; set; }
    public decimal DeltaKgCo2e { get; set; }
    public int AffectedRecordsCount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Applied, Rejected
    public DateTime? AppliedAtUtc { get; set; }
}
