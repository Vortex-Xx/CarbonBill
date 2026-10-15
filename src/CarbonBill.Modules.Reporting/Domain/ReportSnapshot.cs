using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.Reporting.Domain;

public class ReportSnapshot : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public int Version { get; set; } = 1;
    public string ContentHashSha256 { get; set; } = string.Empty;
    public string SnapshotDataJson { get; set; } = "{}";

    public Guid FrozenByUserId { get; set; }
    public DateTime FrozenAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsSuperseded { get; set; }
}
