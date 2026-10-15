using CarbonBill.SharedKernel.Domain;

namespace CarbonBill.Modules.Insights.Domain;

public class BenchmarkSet : BaseEntity
{
    public string Sector { get; set; } = string.Empty; // RMG, TextileDyeing, etc.
    public string SizeBand { get; set; } = string.Empty; // Small, Medium, Large
    public string Metric { get; set; } = string.Empty; // e.g. kg_co2e_per_piece
    public decimal P25 { get; set; } // numeric(18,6)
    public decimal P50 { get; set; } // numeric(18,6)
    public decimal P75 { get; set; } // numeric(18,6)
    public decimal P90 { get; set; } // numeric(18,6)
    public int N { get; set; } // sample size (minimum peer count gate)
    public string Source { get; set; } = string.Empty; // Citation
    public int Year { get; set; } // Publication year
}
