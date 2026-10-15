using CarbonBill.Modules.Documents.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Documents.Persistence;

public class DocumentsDbContext(
    DbContextOptions<DocumentsDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentPage> DocumentPages => Set<DocumentPage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(260);
            entity.Property(e => e.StoragePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Sha256Hash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ClientIdempotencyKey).HasMaxLength(100);
            entity.Property(e => e.DocType).HasMaxLength(50);
            entity.Property(e => e.FailureReason).HasMaxLength(500);

            // Indexes for high-frequency queries
            entity.HasIndex(e => new { e.OrgId, e.Status });
            entity.HasIndex(e => new { e.OrgId, e.Sha256Hash });
            entity.HasIndex(e => new { e.OrgId, e.CapturedAtUtc });
            entity.HasIndex(e => new { e.OrgId, e.UploadedByUserId });

            entity.HasMany(e => e.Pages)
                .WithOne(p => p.Document)
                .HasForeignKey(p => p.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Strict Global Query Filter for Tenancy Isolation
            entity.HasQueryFilter(e =>
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });

        modelBuilder.Entity<DocumentPage>(entity =>
        {
            entity.ToTable("DocumentPages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StoragePath).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.DocumentId);

            entity.HasQueryFilter(e =>
                _tenantContext.CurrentOrgId == null ||
                e.Document.OrgId == _tenantContext.CurrentOrgId);
        });
    }
}
