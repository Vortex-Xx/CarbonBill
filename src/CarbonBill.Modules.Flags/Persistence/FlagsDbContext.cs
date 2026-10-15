using CarbonBill.Modules.Flags.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Flags.Persistence;

public class FlagsDbContext(
    DbContextOptions<FlagsDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<FlagRule> FlagRules => Set<FlagRule>();
    public DbSet<Flag> Flags => Set<Flag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FlagRule>(entity =>
        {
            entity.ToTable("FlagRules");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FlagCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Family).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DefaultSeverity).IsRequired().HasMaxLength(20);
            entity.Property(e => e.NameBn).IsRequired().HasMaxLength(200);
            entity.Property(e => e.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SuggestedActionBn).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.SuggestedActionEn).IsRequired().HasMaxLength(2000);

            entity.HasIndex(e => e.FlagCode).IsUnique();
        });

        modelBuilder.Entity<Flag>(entity =>
        {
            entity.ToTable("Flags");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RuleCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Family).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Period).IsRequired().HasMaxLength(7);
            entity.Property(e => e.State).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ExplanationBn).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.ExplanationEn).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.SuggestedActionBn).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.SuggestedActionEn).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.DismissedReason).HasMaxLength(500);

            entity.HasOne(e => e.Rule)
                .WithMany()
                .HasForeignKey(e => e.RuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.OrgId, e.State, e.Severity });
            entity.HasIndex(e => new { e.OrgId, e.RuleCode, e.Period });

            // Global Query Filter for Tenancy Isolation
            entity.HasQueryFilter(e =>
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });
    }
}
