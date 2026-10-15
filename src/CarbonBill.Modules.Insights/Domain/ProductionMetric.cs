using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.Insights.Domain;

public class ProductionMetric : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid? SiteId { get; set; }
    public string Period { get; set; } = string.Empty; // YYYY-MM
    public string Unit { get; set; } = string.Empty; // pieces, kg_fabric, tonnes
    public decimal Quantity { get; set; } // numeric(18,2)
}
