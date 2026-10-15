using CarbonBill.Modules.Notifications.Domain;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Notifications.Persistence;

public class NotificationsDbContext(
    DbContextOptions<NotificationsDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<UserNotificationPreference> NotificationPreferences => Set<UserNotificationPreference>();
    public DbSet<NotificationSnooze> NotificationSnoozes => Set<NotificationSnooze>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title).IsRequired().HasMaxLength(250);
            entity.Property(e => e.Body).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LinkUrl).HasMaxLength(500);
            entity.Property(e => e.DeduplicationKey).HasMaxLength(200);
            entity.Property(e => e.SnoozeReason).HasMaxLength(500);

            entity.HasIndex(e => new { e.OrgId, e.UserId, e.IsRead });
            entity.HasIndex(e => new { e.UserId, e.DeduplicationKey });

            // Global Query Filter for Tenancy Isolation
            entity.HasQueryFilter(e =>
                _tenantContext.CurrentOrgId == null ||
                e.OrgId == _tenantContext.CurrentOrgId);
        });

        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.ToTable("PushSubscriptions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Endpoint).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.P256DhKey).IsRequired().HasMaxLength(256);
            entity.Property(e => e.AuthKey).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.HasIndex(e => new { e.UserId, e.Endpoint }).IsUnique();
        });

        modelBuilder.Entity<UserNotificationPreference>(entity =>
        {
            entity.ToTable("NotificationPreferences");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PreferredLanguage).IsRequired().HasMaxLength(10);
            entity.HasIndex(e => e.UserId).IsUnique();
        });

        modelBuilder.Entity<NotificationSnooze>(entity =>
        {
            entity.ToTable("NotificationSnoozes");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.AlertOrFlagKey).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);

            entity.HasIndex(e => new { e.UserId, e.AlertOrFlagKey });
        });
    }
}
