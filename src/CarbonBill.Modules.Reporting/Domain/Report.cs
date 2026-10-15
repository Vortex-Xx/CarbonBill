using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.Reporting.Domain;

public class Report : AggregateRoot, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid? SiteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ReportingPeriod { get; set; } = "2026-Q1";
    public string Status { get; set; } = ReportStatuses.Draft;

    // GHG Protocol scope totals in kg CO2e
    public decimal TotalKgCo2e { get; set; }
    public decimal Scope1KgCo2e { get; set; }
    public decimal Scope2KgCo2e { get; set; }
    public decimal Scope3KgCo2e { get; set; }

    // Data Quality Score (0 to 100)
    public decimal DataQualityScore { get; set; } = 90.0m;

    public string MethodologyStatement { get; set; } =
        "Estimate aligned with GHG Protocol methodology, not audited or certified.";

    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public List<ReportSnapshot> Snapshots { get; set; } = [];
    public List<ShareLink> ShareLinks { get; set; } = [];
}
