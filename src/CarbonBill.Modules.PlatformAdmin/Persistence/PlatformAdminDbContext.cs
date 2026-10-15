using CarbonBill.Modules.PlatformAdmin.Domain;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.PlatformAdmin.Persistence;

public class PlatformAdminDbContext : DbContext
{
    public PlatformAdminDbContext(DbContextOptions<PlatformAdminDbContext> options)
        : base(options)
    {
    }

    public DbSet<DatasetVersion> DatasetVersions => Set<DatasetVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DatasetVersion>(entity =>
        {
            entity.ToTable("DatasetVersions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Kind, e.Version });
        });
    }
}
