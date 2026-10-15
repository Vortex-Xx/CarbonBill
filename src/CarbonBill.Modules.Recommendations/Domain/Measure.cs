using CarbonBill.SharedKernel.Domain;

namespace CarbonBill.Modules.Recommendations.Domain;

public class Measure : BaseEntity
{
    public string MeasureCode { get; set; } = string.Empty; // e.g. VFD_MOTORS
    public string NameBn { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Motors, Boilers, SolarPV, Lighting, CompressedAir, PowerFactor
    public string ApplicableSectorsJson { get; set; } = "[\"RMG\",\"TextileDyeing\"]";
    public string ApplicabilityRulesJson { get; set; } = "{}";

    public decimal SavingLow { get; set; } // numeric(5,4)
    public decimal SavingTypical { get; set; } // numeric(5,4)
    public decimal SavingHigh { get; set; } // numeric(5,4)

    public decimal CapexLow { get; set; } // numeric(18,2)
    public decimal CapexHigh { get; set; } // numeric(18,2)
    public string CapexUnit { get; set; } = "BDT/unit";

    public int LifetimeYears { get; set; }
    public string EvidenceGrade { get; set; } = "B"; // A, B, C

    public Guid SourceId { get; set; } // Required research source
    public MeasureSource? Source { get; set; }

    public string? LocalNotes { get; set; }
}
