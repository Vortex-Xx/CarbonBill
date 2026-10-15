using CarbonBill.Modules.ActivityUnits.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.ActivityUnits.Persistence;

public class ActivityUnitsDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public ActivityUnitsDbContext(
        DbContextOptions<ActivityUnitsDbContext> options,
        ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<UnitConversion> UnitConversions => Set<UnitConversion>();
    public DbSet<ActivityRecord> ActivityRecords => Set<ActivityRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.ToTable("Units");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<UnitConversion>(entity =>
        {
            entity.ToTable("UnitConversions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FromUnit, e.ToUnit });
            entity.Property(e => e.ConversionFactor).HasPrecision(18, 6);
        });

        modelBuilder.Entity<ActivityRecord>(entity =>
        {
            entity.ToTable("ActivityRecords");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrgId, e.Period });
            entity.HasIndex(e => e.DocumentId);
            entity.Property(e => e.QuantityStandard).HasPrecision(18, 6);
            entity.Property(e => e.RawQuantity).HasPrecision(18, 6);
            entity.Property(e => e.TotalCostBdt).HasPrecision(18, 2);
            entity.Property(e => e.UnitCostBdt).HasPrecision(18, 2);

            entity.HasQueryFilter(e =>
                _tenantContext == null ||
                !_tenantContext.CurrentOrgId.HasValue ||
                e.OrgId == _tenantContext.CurrentOrgId.Value);
        });
    }
}
