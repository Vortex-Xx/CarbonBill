using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.Recommendations.Domain;

public class Recommendation : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid? SiteId { get; set; }

    public Guid MeasureId { get; set; }
    public Measure Measure { get; set; } = null!;

    // Ranges in JSON format: { "low": ..., "typical": ..., "high": ... }
    public string SavingRangeCo2eJson { get; set; } = "{}"; // tCO2e/year
    public string SavingRangeBdtJson { get; set; } = "{}"; // BDT/year
    public string CapexRangeBdtJson { get; set; } = "{}"; // BDT { "low": ..., "high": ... }

    public decimal PaybackYears { get; set; } // numeric(5,2)
    public decimal CostPerTco2e { get; set; } // numeric(18,2) - can be negative for net-positive investments!
    public decimal CompositeScore { get; set; } // numeric(18,4)
    public int Rank { get; set; }

    public string Status { get; set; } = RecommendationStatuses.Suggested;
    public string? StatusReason { get; set; } // Required if NotFeasible

    public bool NeedsEnergyAudit { get; set; } // True if Capex > threshold (1M BDT)

    public string ExplanationBn { get; set; } = string.Empty;
    public string ExplanationEn { get; set; } = string.Empty;
    public string FinancingNoteBn { get; set; } = string.Empty;
    public string FinancingNoteEn { get; set; } = string.Empty;
    public string AssumptionsBn { get; set; } = string.Empty;
    public string AssumptionsEn { get; set; } = string.Empty;
    public string NextStepBn { get; set; } = string.Empty;
    public string NextStepEn { get; set; } = string.Empty;

    // Closed-loop outcome tracking
    public decimal? RealisedSavingBdt { get; set; }
    public decimal? RealisedTco2eAvoided { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool EvidenceUpgradeRecorded { get; set; }
    public string? EvidenceNotes { get; set; }
}
