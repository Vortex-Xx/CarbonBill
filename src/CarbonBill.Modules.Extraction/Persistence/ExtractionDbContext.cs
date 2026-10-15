using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Extraction.Persistence;

public class ExtractionDbContext(
    DbContextOptions<ExtractionDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<ExtractionRun> ExtractionRuns => Set<ExtractionRun>();
    public DbSet<ExtractedField> ExtractedFields => Set<ExtractedField>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ExtractionRun>(entity =>
        {
            entity.ToTable("ExtractionRuns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);

            entity.HasIndex(e => new { e.OrgId, e.DocumentId });
            entity.HasIndex(e => e.ProcessedAtUtc);

            entity.HasMany(e => e.Fields)
                .WithOne(f => f.ExtractionRun)
                .HasForeignKey(f => f.ExtractionRunId)
                .OnDelete(DeleteBehavior.Cascade);

            // Global Query Filter for Tenancy Isolation
            entity.HasQueryFilter(e =>
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });

        modelBuilder.Entity<ExtractedField>(entity =>
        {
            entity.ToTable("ExtractedFields");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FieldName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RawValue).IsRequired().HasMaxLength(500);
            entity.Property(e => e.NormalizedValue).HasMaxLength(500);
            entity.Property(e => e.CorrectedValue).HasMaxLength(500);

            entity.HasIndex(e => new { e.OrgId, e.DocumentId });

            entity.HasQueryFilter(e =>
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });
    }
}
