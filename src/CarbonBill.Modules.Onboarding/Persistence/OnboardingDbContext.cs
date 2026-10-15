using CarbonBill.Modules.Onboarding.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Onboarding.Persistence;

public class OnboardingDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public OnboardingDbContext(
        DbContextOptions<OnboardingDbContext> options,
        ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<ExpectedDocRule> ExpectedDocRules => Set<ExpectedDocRule>();
    public DbSet<FacilityProfile> FacilityProfiles => Set<FacilityProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Site>(entity =>
        {
            entity.ToTable("Sites");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrgId, e.Name });

            entity.HasMany(e => e.Assets)
                  .WithOne(a => a.Site)
                  .HasForeignKey(a => a.SiteId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e =>
                _tenantContext == null ||
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("Assets");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrgId, e.SiteId });

            entity.HasMany(e => e.ExpectedDocRules)
                  .WithOne(r => r.Asset)
                  .HasForeignKey(r => r.AssetId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e =>
                _tenantContext == null ||
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });

        modelBuilder.Entity<ExpectedDocRule>(entity =>
        {
            entity.ToTable("ExpectedDocRules");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrgId, e.AssetId });

            entity.HasQueryFilter(e =>
                _tenantContext == null ||
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });

        modelBuilder.Entity<FacilityProfile>(entity =>
        {
            entity.ToTable("FacilityProfiles");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrgId).IsUnique();

            entity.HasQueryFilter(e =>
                _tenantContext == null ||
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });
    }
}
