using CarbonBill.Modules.Recommendations.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Recommendations.Persistence;

public class RecommendationsDbContext(
    DbContextOptions<RecommendationsDbContext> options,
    ITenantContext? tenantContext = null) : DbContext(options)
{
    private readonly ITenantContext? _tenantContext = tenantContext;

    public DbSet<MeasureSource> MeasureSources => Set<MeasureSource>();
    public DbSet<Measure> Measures => Set<Measure>();
    public DbSet<FacilityProfile> FacilityProfiles => Set<FacilityProfile>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MeasureSource>(entity =>
        {
            entity.ToTable("MeasureSources");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Institution).HasMaxLength(150).IsRequired();

            entity.HasIndex(e => e.SourceCode).IsUnique();
        });

        modelBuilder.Entity<Measure>(entity =>
        {
            entity.ToTable("Measures");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MeasureCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.NameBn).HasMaxLength(200).IsRequired();
            entity.Property(e => e.NameEn).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EvidenceGrade).HasMaxLength(1).IsRequired();
            entity.Property(e => e.CapexUnit).HasMaxLength(50).IsRequired();

            entity.Property(e => e.SavingLow).HasPrecision(5, 4);
            entity.Property(e => e.SavingTypical).HasPrecision(5, 4);
            entity.Property(e => e.SavingHigh).HasPrecision(5, 4);
            entity.Property(e => e.CapexLow).HasPrecision(18, 2);
            entity.Property(e => e.CapexHigh).HasPrecision(18, 2);

            entity.HasIndex(e => e.MeasureCode).IsUnique();

            entity.HasOne(e => e.Source)
                  .WithMany()
                  .HasForeignKey(e => e.SourceId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FacilityProfile>(entity =>
        {
            entity.ToTable("RecommendationFacilityProfiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Sector).HasMaxLength(50).IsRequired();
            entity.Property(e => e.BuildingOwnership).HasMaxLength(50).IsRequired();
            entity.Property(e => e.BudgetBand).HasMaxLength(50).IsRequired();

            entity.Property(e => e.RoofAreaSqft).HasPrecision(18, 2);
            entity.Property(e => e.MaxBudgetBdt).HasPrecision(18, 2);
            entity.Property(e => e.BaselineMonthlyKwh).HasPrecision(18, 2);
            entity.Property(e => e.BaselineMonthlyDieselLitres).HasPrecision(18, 2);
            entity.Property(e => e.BaselineMonthlyGasM3).HasPrecision(18, 2);
            entity.Property(e => e.ElectricityTariffBdtPerKwh).HasPrecision(18, 2);
            entity.Property(e => e.DieselTariffBdtPerLitre).HasPrecision(18, 2);
            entity.Property(e => e.GasTariffBdtPerM3).HasPrecision(18, 2);

            entity.HasIndex(e => e.OrgId);

            if (_tenantContext != null)
            {
                entity.HasQueryFilter(e => _tenantContext.CurrentOrgId == null || e.OrgId == _tenantContext.CurrentOrgId);
            }
        });

        modelBuilder.Entity<Recommendation>(entity =>
        {
            entity.ToTable("Recommendations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.PaybackYears).HasPrecision(5, 2);
            entity.Property(e => e.CostPerTco2e).HasPrecision(18, 2);
            entity.Property(e => e.CompositeScore).HasPrecision(18, 4);
            entity.Property(e => e.RealisedSavingBdt).HasPrecision(18, 2);
            entity.Property(e => e.RealisedTco2eAvoided).HasPrecision(18, 6);

            entity.HasIndex(e => new { e.OrgId, e.Status });

            entity.HasOne(e => e.Measure)
                  .WithMany()
                  .HasForeignKey(e => e.MeasureId)
                  .OnDelete(DeleteBehavior.Restrict);

            if (_tenantContext != null)
            {
                entity.HasQueryFilter(e => _tenantContext.CurrentOrgId == null || e.OrgId == _tenantContext.CurrentOrgId);
            }
        });
    }
}
