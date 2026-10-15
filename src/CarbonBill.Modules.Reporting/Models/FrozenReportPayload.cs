namespace CarbonBill.Modules.Reporting.Models;

public record MonthlyActivitySnapshot(
    string Period,
    decimal Scope1KgCo2e,
    decimal Scope2KgCo2e,
    decimal Scope3KgCo2e,
    decimal TotalKgCo2e,
    decimal ElectricityKwh,
    decimal DieselLitres,
    decimal GasM3,
    bool IsEstimated);

public record AppliedFactorSnapshot(
    string ActivityOrFuel,
    decimal FactorValue,
    string Unit,
    string Source,
    int PublicationYear,
    string GwpBasis);

public record DocumentAuditSnapshot(
    Guid DocumentId,
    string DocumentType,
    string Period,
    decimal Quantity,
    string Unit,
    decimal? AmountBdt,
    float OcrConfidence,
    string ReviewerEmail,
    DateTime ConfirmedAtUtc);

public class FrozenReportPayload
{
    public Guid ReportId { get; set; }
    public Guid OrgId { get; set; }
    public string OrgName { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string Location { get; set; } = "Dhaka, Bangladesh";
    public string ReportingPeriod { get; set; } = "2026-Q1";
    public string BoundaryDescription { get; set; } = "Operational Control (GHG Protocol Corporate Standard)";

    public decimal DataQualityScore { get; set; } // 0 - 100
    public decimal TotalKgCo2e { get; set; }
    public decimal Scope1KgCo2e { get; set; }
    public decimal Scope2KgCo2e { get; set; }
    public decimal Scope3KgCo2e { get; set; }

    public decimal TotalTco2e => Math.Round(TotalKgCo2e / 1000m, 3);
    public decimal Scope1Tco2e => Math.Round(Scope1KgCo2e / 1000m, 3);
    public decimal Scope2Tco2e => Math.Round(Scope2KgCo2e / 1000m, 3);
    public decimal Scope3Tco2e => Math.Round(Scope3KgCo2e / 1000m, 3);

    public decimal IntensityKgCo2ePerUnit { get; set; }
    public string ProductionUnit { get; set; } = "piece";
    public decimal ProductionVolume { get; set; }

    public decimal VerifiedTco2e { get; set; }
    public decimal EstimatedTco2e { get; set; }
    public decimal VerifiedSharePercent { get; set; }
    public decimal EstimatedSharePercent { get; set; }

    public List<MonthlyActivitySnapshot> MonthlyActivities { get; set; } = [];
    public List<AppliedFactorSnapshot> AppliedFactors { get; set; } = [];
    public List<DocumentAuditSnapshot> DocumentAuditTrail { get; set; } = [];

    public string MethodologyStatement { get; set; } =
        "Estimate aligned with GHG Protocol methodology, not audited or certified.";

    public string LimitationsStatement { get; set; } =
        "Outputs are engineering estimates based on utility billing evidence. Scope 3 purchased capital goods and raw materials are not yet monitored under this inventory boundary.";

    public bool RedactPrices { get; set; }
}

public record AuditorTraceResult(
    Guid ReportId,
    string ReportingPeriod,
    string TargetMetric,
    DocumentAuditSnapshot Document,
    AppliedFactorSnapshot Factor,
    string VerificationStatement);
