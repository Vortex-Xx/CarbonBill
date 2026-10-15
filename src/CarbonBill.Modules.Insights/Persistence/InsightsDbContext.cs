using CarbonBill.Modules.Insights.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Insights.Persistence;

public class InsightsDbContext(
    DbContextOptions<InsightsDbContext> options,
    ITenantContext? tenantContext = null) : DbContext(options)
{
    private readonly ITenantContext? _tenantContext = tenantContext;

    public DbSet<ProductionMetric> ProductionMetrics => Set<ProductionMetric>();
    public DbSet<BenchmarkSet> BenchmarkSets => Set<BenchmarkSet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProductionMetric>(entity =>
        {
            entity.ToTable("ProductionMetrics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Period).HasMaxLength(7).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(18, 2).IsRequired();

            entity.HasIndex(e => new { e.OrgId, e.Period });

            if (_tenantContext != null)
            {
                entity.HasQueryFilter(e => _tenantContext.CurrentOrgId == null || e.OrgId == _tenantContext.CurrentOrgId);
            }
        });

        modelBuilder.Entity<BenchmarkSet>(entity =>
        {
            entity.ToTable("BenchmarkSets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Sector).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SizeBand).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Metric).HasMaxLength(100).IsRequired();
            entity.Property(e => e.P25).HasPrecision(18, 6).IsRequired();
            entity.Property(e => e.P50).HasPrecision(18, 6).IsRequired();
            entity.Property(e => e.P75).HasPrecision(18, 6).IsRequired();
            entity.Property(e => e.P90).HasPrecision(18, 6).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(255).IsRequired();

            entity.HasIndex(e => new { e.Sector, e.SizeBand, e.Metric });
        });
    }
}
