using CarbonBill.Modules.Calculation.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Calculation.Persistence;

public class CalculationDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public CalculationDbContext(
        DbContextOptions<CalculationDbContext> options,
        ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<EmissionResult> EmissionResults => Set<EmissionResult>();
    public DbSet<RecalculationProposal> RecalculationProposals => Set<RecalculationProposal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EmissionResult>(entity =>
        {
            entity.ToTable("EmissionResults");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrgId, e.Period });
            entity.HasIndex(e => e.ActivityRecordId);
            entity.HasIndex(e => e.DocumentId);

            entity.Property(e => e.QuantityStandard).HasPrecision(18, 6);
            entity.Property(e => e.EmissionFactorUsed).HasPrecision(18, 6);
            entity.Property(e => e.KgCo2e).HasPrecision(18, 6);

            entity.HasQueryFilter(e =>
                _tenantContext == null ||
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });

        modelBuilder.Entity<RecalculationProposal>(entity =>
        {
            entity.ToTable("RecalculationProposals");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrgId, e.Status });

            entity.Property(e => e.OldTotalKgCo2e).HasPrecision(18, 6);
            entity.Property(e => e.NewTotalKgCo2e).HasPrecision(18, 6);
            entity.Property(e => e.DeltaKgCo2e).HasPrecision(18, 6);

            entity.HasQueryFilter(e =>
                _tenantContext == null ||
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });
    }
}
