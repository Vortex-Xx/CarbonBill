using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.Notifications.Domain;

public static class NotificationChannels
{
    public const string InApp = "InApp";
    public const string WebPush = "WebPush";
    public const string Email = "Email";

    public static readonly IReadOnlyList<string> All = [InApp, WebPush, Email];
}

public static class NotificationCategories
{
    public const string Gap = "Gap";
    public const string Flag = "Flag";
    public const string DocumentRetake = "DocumentRetake";
    public const string Digest = "Digest";
    public const string System = "System";

    public static readonly IReadOnlyList<string> All = [Gap, Flag, DocumentRetake, Digest, System];
}

public class Notification : AggregateRoot, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Channel { get; set; } = NotificationChannels.InApp;
    public string? LinkUrl { get; set; }
    public string Category { get; set; } = NotificationCategories.System;
    public string Severity { get; set; } = "Info"; // Info, Amber, Red
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    // Flag Fatigue & Deduplication Controls
    public string? DeduplicationKey { get; set; }
    public int CollapseCount { get; set; } = 1;
    public DateTime? SnoozedUntilUtc { get; set; }
    public string? SnoozeReason { get; set; }

    public void MarkAsRead(DateTime readAtUtc)
    {
        IsRead = true;
        ReadAtUtc = readAtUtc;
        UpdatedAtUtc = readAtUtc;
    }

    public void Snooze(DateTime untilUtc, string reason, DateTime now)
    {
        SnoozedUntilUtc = untilUtc;
        SnoozeReason = reason;
        UpdatedAtUtc = now;
    }

    public void IncrementCollapse(DateTime now)
    {
        CollapseCount++;
        UpdatedAtUtc = now;
    }

    public bool IsSnoozed(DateTime now) => SnoozedUntilUtc.HasValue && now < SnoozedUntilUtc.Value;
}

public class PushSubscription : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrgId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256DhKey { get; set; } = string.Empty;
    public string AuthKey { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserNotificationPreference : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrgId { get; set; }
    public string PreferredLanguage { get; set; } = "en"; // English by default
    public bool InAppEnabled { get; set; } = true;
    public bool WebPushEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;

    // Quiet Hours (Default: Off, 22:00 to 08:00 Asia/Dhaka UTC+6)
    public bool QuietHoursEnabled { get; set; }
    public int QuietHoursStartMinutes { get; set; } = 22 * 60; // 22:00 (1320)
    public int QuietHoursEndMinutes { get; set; } = 8 * 60;    // 08:00 (480)
    public int TimeZoneOffsetMinutes { get; set; } = 360;      // +6 hours (Dhaka standard time)

    public bool IsInQuietHours(DateTime utcNow)
    {
        if (!QuietHoursEnabled)
        {
            return false;
        }

        // Convert UTC to local minutes
        var localDateTime = utcNow.AddMinutes(TimeZoneOffsetMinutes);
        var currentMinutes = localDateTime.Hour * 60 + localDateTime.Minute;

        if (QuietHoursStartMinutes > QuietHoursEndMinutes)
        {
            // Overnight window (e.g. 22:00 to 08:00)
            return currentMinutes >= QuietHoursStartMinutes || currentMinutes < QuietHoursEndMinutes;
        }

        // Intra-day window (e.g. 13:00 to 15:00)
        return currentMinutes >= QuietHoursStartMinutes && currentMinutes < QuietHoursEndMinutes;
    }
}

public class NotificationSnooze : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrgId { get; set; }
    public string AlertOrFlagKey { get; set; } = string.Empty;
    public DateTime SnoozedUntilUtc { get; set; }
    public string Reason { get; set; } = string.Empty;

    public bool IsActive(DateTime now) => now < SnoozedUntilUtc;
}
