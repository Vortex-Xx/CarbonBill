using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.Reporting.Domain;

public class ShareLink : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public bool RedactPrices { get; set; }
    public bool IsRevoked { get; set; }
    public int ViewCount { get; set; }
    public DateTime? LastViewedAtUtc { get; set; }
    public Guid CreatedByUserId { get; set; }
}
