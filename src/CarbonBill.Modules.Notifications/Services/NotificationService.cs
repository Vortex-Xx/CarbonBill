using CarbonBill.Modules.Notifications.Domain;
using CarbonBill.Modules.Notifications.Persistence;
using CarbonBill.SharedKernel.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Notifications.Services;

public class NotificationService(
    NotificationsDbContext dbContext,
    INotifier notifier,
    ILogger<NotificationService> logger) : INotificationService
{
    private readonly NotificationsDbContext _dbContext = dbContext;
    private readonly INotifier _notifier = notifier;
    private readonly ILogger<NotificationService> _logger = logger;

    public async Task<Notification?> NotifyUserAsync(CreateNotificationCommand command, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // 1. Check if alert is snoozed
        if (!string.IsNullOrWhiteSpace(command.DeduplicationKey))
        {
            var isSnoozed = await _dbContext.NotificationSnoozes
                .AnyAsync(s => s.UserId == command.UserId &&
                               s.AlertOrFlagKey == command.DeduplicationKey &&
                               s.SnoozedUntilUtc > now, cancellationToken);

            if (isSnoozed)
            {
                _logger.LogInformation("Notification suppressed for User {UserId} due to active snooze on {Key}",
                    command.UserId, command.DeduplicationKey);
                return null;
            }
        }

        // 2. Collapse repeated alerts (Flag fatigue mitigation)
        if (!string.IsNullOrWhiteSpace(command.DeduplicationKey))
        {
            var existingUnread = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.UserId == command.UserId &&
                                          n.DeduplicationKey == command.DeduplicationKey &&
                                          !n.IsRead, cancellationToken);

            if (existingUnread != null)
            {
                existingUnread.IncrementCollapse(now);
                existingUnread.Title = command.Title;
                existingUnread.Body = command.Body;
                existingUnread.Severity = command.Severity;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Collapsed duplicate notification {Id} for User {UserId}. Total count: {Count}",
                    existingUnread.Id, command.UserId, existingUnread.CollapseCount);
                return existingUnread;
            }
        }

        // 3. User Preferences (Quiet Hours, Channels)
        var prefs = await GetOrCreatePreferencesAsync(command.UserId, command.OrgId, cancellationToken);
        var inQuietHours = prefs.IsInQuietHours(now);
        var isCriticalRed = command.Severity.Equals("Red", StringComparison.OrdinalIgnoreCase);

        // 4. Create in-app notification
        var notification = new Notification
        {
            OrgId = command.OrgId,
            UserId = command.UserId,
            Title = command.Title,
            Body = command.Body,
            Category = command.Category,
            Severity = command.Severity,
            LinkUrl = command.LinkUrl,
            DeduplicationKey = command.DeduplicationKey,
            Channel = NotificationChannels.InApp,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        if (prefs.InAppEnabled)
        {
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 5. Send Web Push if enabled and allowed
        if (prefs.WebPushEnabled && (!inQuietHours || isCriticalRed))
        {
            var pushSubscriptions = await _dbContext.PushSubscriptions
                .Where(p => p.UserId == command.UserId && p.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var sub in pushSubscriptions)
            {
                await _notifier.SendPushAsync(new PushNotification(
                    sub.Endpoint,
                    sub.P256DhKey,
                    sub.AuthKey,
                    command.Title,
                    command.Body,
                    command.LinkUrl), cancellationToken);
            }
        }

        // 6. Send Email if enabled and allowed
        if (prefs.EmailEnabled && (!inQuietHours || isCriticalRed) && !string.IsNullOrWhiteSpace(command.RecipientEmail))
        {
            await _notifier.SendEmailAsync(new EmailNotification(
                command.RecipientEmail,
                command.Title,
                $"<p>{command.Body}</p>",
                command.Body), cancellationToken);
        }

        return notification;
    }

    public async Task<bool> SnoozeAlertAsync(
        Guid userId,
        Guid? orgId,
        string alertOrFlagKey,
        int days,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A mandatory non-empty reason is required to snooze an alert.", nameof(reason));
        }

        if (days <= 0)
        {
            throw new ArgumentException("Snooze duration must be greater than zero days.", nameof(days));
        }

        var now = DateTime.UtcNow;
        var until = now.AddDays(days);

        var existingSnooze = await _dbContext.NotificationSnoozes
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AlertOrFlagKey == alertOrFlagKey, cancellationToken);

        if (existingSnooze != null)
        {
            existingSnooze.SnoozedUntilUtc = until;
            existingSnooze.Reason = reason;
            existingSnooze.UpdatedAtUtc = now;
        }
        else
        {
            _dbContext.NotificationSnoozes.Add(new NotificationSnooze
            {
                UserId = userId,
                OrgId = orgId,
                AlertOrFlagKey = alertOrFlagKey,
                SnoozedUntilUtc = until,
                Reason = reason,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        // Also update matching in-app notifications
        var matchingNotifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId && n.DeduplicationKey == alertOrFlagKey)
            .ToListAsync(cancellationToken);

        foreach (var n in matchingNotifications)
        {
            n.Snooze(until, reason, now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Alert {Key} snoozed by User {UserId} for {Days} days until {Until}",
            alertOrFlagKey, userId, days, until);

        return true;
    }

    public async Task<UserNotificationPreference> GetOrCreatePreferencesAsync(
        Guid userId,
        Guid? orgId = null,
        CancellationToken cancellationToken = default)
    {
        var prefs = await _dbContext.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (prefs == null)
        {
            prefs = new UserNotificationPreference
            {
                UserId = userId,
                OrgId = orgId,
                PreferredLanguage = "en", // English by default
                InAppEnabled = true,
                WebPushEnabled = true,
                EmailEnabled = true,
                QuietHoursEnabled = false,
                QuietHoursStartMinutes = 22 * 60,
                QuietHoursEndMinutes = 8 * 60,
                TimeZoneOffsetMinutes = 360,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _dbContext.NotificationPreferences.Add(prefs);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return prefs;
    }

    public async Task<UserNotificationPreference> UpdatePreferencesAsync(
        Guid userId,
        UserNotificationPreference updated,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetOrCreatePreferencesAsync(userId, updated.OrgId, cancellationToken);

        existing.PreferredLanguage = updated.PreferredLanguage;
        existing.InAppEnabled = updated.InAppEnabled;
        existing.WebPushEnabled = updated.WebPushEnabled;
        existing.EmailEnabled = updated.EmailEnabled;
        existing.QuietHoursEnabled = updated.QuietHoursEnabled;
        existing.QuietHoursStartMinutes = updated.QuietHoursStartMinutes;
        existing.QuietHoursEndMinutes = updated.QuietHoursEndMinutes;
        existing.TimeZoneOffsetMinutes = updated.TimeZoneOffsetMinutes;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<PushSubscription> RegisterPushSubscriptionAsync(
        Guid userId,
        Guid? orgId,
        string endpoint,
        string p256dh,
        string auth,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.PushSubscriptions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Endpoint == endpoint, cancellationToken);

        var now = DateTime.UtcNow;
        if (existing != null)
        {
            existing.P256DhKey = p256dh;
            existing.AuthKey = auth;
            existing.UserAgent = userAgent;
            existing.IsActive = true;
            existing.UpdatedAtUtc = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var newSub = new PushSubscription
        {
            UserId = userId,
            OrgId = orgId,
            Endpoint = endpoint,
            P256DhKey = p256dh,
            AuthKey = auth,
            UserAgent = userAgent,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.PushSubscriptions.Add(newSub);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return newSub;
    }

    public async Task<bool> UnregisterPushSubscriptionAsync(Guid userId, string endpoint, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.PushSubscriptions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Endpoint == endpoint, cancellationToken);

        if (existing == null)
        {
            return false;
        }

        existing.IsActive = false;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (notification == null)
        {
            return false;
        }

        notification.MarkAsRead(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unread = await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var item in unread)
        {
            item.MarkAsRead(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }
}
