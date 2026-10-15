using CarbonBill.Modules.FactorRegistry.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.FactorRegistry.Persistence;

public class FactorRegistryDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public FactorRegistryDbContext(
        DbContextOptions<FactorRegistryDbContext> options,
        ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<FactorSet> FactorSets => Set<FactorSet>();
    public DbSet<EmissionFactor> EmissionFactors => Set<EmissionFactor>();
    public DbSet<FactorOverride> FactorOverrides => Set<FactorOverride>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FactorSet>(entity =>
        {
            entity.ToTable("FactorSets");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Name, e.Version });

            entity.HasMany(e => e.Factors)
                  .WithOne(f => f.FactorSet)
                  .HasForeignKey(f => f.FactorSetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmissionFactor>(entity =>
        {
            entity.ToTable("EmissionFactors");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ActivityType, e.FuelOrMode });
            entity.Property(e => e.Co2eFactor).HasPrecision(18, 6);
        });

        modelBuilder.Entity<FactorOverride>(entity =>
        {
            entity.ToTable("FactorOverrides");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrgId, e.EmissionFactorId });
            entity.Property(e => e.OverrideValue).HasPrecision(18, 6);

            entity.HasOne(e => e.EmissionFactor)
                  .WithMany()
                  .HasForeignKey(e => e.EmissionFactorId);

            entity.HasQueryFilter(e =>
                _tenantContext == null ||
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });
    }
}
