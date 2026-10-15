using CarbonBill.Modules.GapDetection.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.GapDetection.Persistence;

public class GapDetectionDbContext(
    DbContextOptions<GapDetectionDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<MissingAlert> MissingAlerts => Set<MissingAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MissingAlert>(entity =>
        {
            entity.ToTable("MissingAlerts");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.AssetName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.AssetType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DocType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Period).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EscalationState).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RequestMessageKey).HasMaxLength(100);
            entity.Property(e => e.PlainRequestMessageBn).HasMaxLength(500);
            entity.Property(e => e.PlainRequestMessageEn).HasMaxLength(500);

            entity.HasIndex(e => new { e.OrgId, e.Period });
            entity.HasIndex(e => new { e.OrgId, e.Status });
            entity.HasIndex(e => new { e.OrgId, e.AssetId, e.DocType, e.Period });

            // Global Query Filter for Tenancy Isolation
            entity.HasQueryFilter(e =>
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });
    }
}
