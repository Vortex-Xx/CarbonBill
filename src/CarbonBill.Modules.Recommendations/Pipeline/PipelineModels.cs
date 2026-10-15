using CarbonBill.Modules.Recommendations.Domain;

namespace CarbonBill.Modules.Recommendations.Pipeline;

public record EnergyActivityBaseline(
    decimal AnnualKwh,
    decimal AnnualDieselLitres,
    decimal AnnualGasM3,
    decimal ElectricityTariffBdtPerKwh,
    decimal DieselTariffBdtPerLitre,
    decimal GasTariffBdtPerM3);

public record SavingsRange(decimal Low, decimal Typical, decimal High);
public record CapexRange(decimal Low, decimal High);

public class CandidateMeasureEvaluation
{
    public Measure Measure { get; set; } = null!;
    public bool IsEligible { get; set; } = true;
    public string? IneligibilityReason { get; set; }

    // Step 3 Carbon Impact (tCO2e/year)
    public SavingsRange AvoidedTco2e { get; set; } = new(0, 0, 0);

    // Step 4 Financial Modeling
    public SavingsRange AnnualSavingsBdt { get; set; } = new(0, 0, 0);
    public CapexRange CapexBdt { get; set; } = new(0, 0);
    public decimal PaybackYears { get; set; }
    public decimal CostPerTco2e { get; set; } // Can be negative for net-positive investments!
    public decimal AnnualisedCapexBdt { get; set; }

    // Step 5 Realism Filters
    public bool IsRealistic { get; set; } = true;
    public bool NeedsEnergyAudit { get; set; }

    // Step 6 Multi-Factor Ranking
    public decimal CompositeScore { get; set; }
    public int Rank { get; set; }

    // Step 7 Explanation Cards
    public string ExplanationBn { get; set; } = string.Empty;
    public string ExplanationEn { get; set; } = string.Empty;
    public string FinancingNoteBn { get; set; } = string.Empty;
    public string FinancingNoteEn { get; set; } = string.Empty;
    public string AssumptionsBn { get; set; } = string.Empty;
    public string AssumptionsEn { get; set; } = string.Empty;
    public string NextStepBn { get; set; } = string.Empty;
    public string NextStepEn { get; set; } = string.Empty;
    public string SourceCitation { get; set; } = string.Empty;
    public string EvidenceGrade { get; set; } = string.Empty;
}

public class PipelineContext
{
    public Guid OrgId { get; set; }
    public Guid? SiteId { get; set; }
    public FacilityProfile Profile { get; set; } = null!;
    public EnergyActivityBaseline Baseline { get; set; } = null!;
    public List<CandidateMeasureEvaluation> Candidates { get; set; } = [];
}
