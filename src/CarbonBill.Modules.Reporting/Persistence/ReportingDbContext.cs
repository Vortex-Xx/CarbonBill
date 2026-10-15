using CarbonBill.Modules.Reporting.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Reporting.Persistence;

public class ReportingDbContext(
    DbContextOptions<ReportingDbContext> options,
    ITenantContext? tenantContext = null) : DbContext(options)
{
    private readonly ITenantContext? _tenantContext = tenantContext;

    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportSnapshot> ReportSnapshots => Set<ReportSnapshot>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Report>(entity =>
        {
            entity.ToTable("Reports");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ReportingPeriod).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.MethodologyStatement).HasMaxLength(500).IsRequired();

            entity.Property(e => e.TotalKgCo2e).HasPrecision(18, 6);
            entity.Property(e => e.Scope1KgCo2e).HasPrecision(18, 6);
            entity.Property(e => e.Scope2KgCo2e).HasPrecision(18, 6);
            entity.Property(e => e.Scope3KgCo2e).HasPrecision(18, 6);
            entity.Property(e => e.DataQualityScore).HasPrecision(5, 2);

            entity.HasIndex(e => new { e.OrgId, e.ReportingPeriod });
            entity.HasIndex(e => new { e.OrgId, e.Status });

            if (_tenantContext != null)
            {
                entity.HasQueryFilter(e => _tenantContext.CurrentOrgId == null || e.OrgId == _tenantContext.CurrentOrgId);
            }
        });

        modelBuilder.Entity<ReportSnapshot>(entity =>
        {
            entity.ToTable("ReportSnapshots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ContentHashSha256).HasMaxLength(64).IsRequired();
            entity.Property(e => e.SnapshotDataJson).IsRequired();

            entity.HasIndex(e => new { e.ReportId, e.Version });
            entity.HasIndex(e => e.ContentHashSha256);

            entity.HasOne(e => e.Report)
                  .WithMany(r => r.Snapshots)
                  .HasForeignKey(e => e.ReportId)
                  .OnDelete(DeleteBehavior.Cascade);

            if (_tenantContext != null)
            {
                entity.HasQueryFilter(e => _tenantContext.CurrentOrgId == null || e.OrgId == _tenantContext.CurrentOrgId);
            }
        });

        modelBuilder.Entity<ShareLink>(entity =>
        {
            entity.ToTable("ShareLinks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).HasMaxLength(128).IsRequired();

            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => new { e.ReportId, e.ExpiresAtUtc });

            entity.HasOne(e => e.Report)
                  .WithMany(r => r.ShareLinks)
                  .HasForeignKey(e => e.ReportId)
                  .OnDelete(DeleteBehavior.Cascade);

            if (_tenantContext != null)
            {
                entity.HasQueryFilter(e => _tenantContext.CurrentOrgId == null || e.OrgId == _tenantContext.CurrentOrgId);
            }
        });
    }
}
