using CarbonBill.SharedKernel.Domain;

namespace CarbonBill.Modules.Recommendations.Domain;

public class MeasureSource : BaseEntity
{
    public string SourceCode { get; set; } = string.Empty; // e.g. IFC_PACT_2018, SREDA_2022
    public string Title { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty; // IFC, SREDA, IDCOL
    public int Year { get; set; }
    public string LicenseTerms { get; set; } = string.Empty;
    public string? CandidateStatus { get; set; }
}
