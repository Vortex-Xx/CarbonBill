using CarbonBill.Modules.IdentityTenancy.Domain;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.IdentityTenancy.Persistence;

public class IdentityTenancyDbContext(
    DbContextOptions<IdentityTenancyDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Organization
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Sector).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.PreferredLanguage).IsRequired().HasMaxLength(10);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Membership (Tenant-scoped)
        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("Memberships");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => new { e.UserId, e.OrgId }).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Memberships)
                .HasForeignKey(e => e.OrgId)
                .OnDelete(DeleteBehavior.Cascade);

            // Global Query Filter for Tenancy Isolation
            entity.HasQueryFilter(e =>
                _tenantContext.IsPlatformAdmin ||
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });

        // Invitation (Tenant-scoped)
        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("Invitations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(128);
            entity.Property(e => e.PinHash).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Token).IsUnique();

            entity.HasOne(e => e.Organization)
                .WithMany()
                .HasForeignKey(e => e.OrgId)
                .OnDelete(DeleteBehavior.Cascade);

            // Global Query Filter
            entity.HasQueryFilter(e =>
                _tenantContext.IsPlatformAdmin ||
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.TokenHash).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
