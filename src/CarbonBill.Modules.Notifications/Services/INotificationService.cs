using CarbonBill.Modules.Notifications.Domain;
using CarbonBill.SharedKernel.Events;

namespace CarbonBill.Modules.Notifications.Services;

public record CreateNotificationCommand(
    Guid OrgId,
    Guid UserId,
    string Title,
    string Body,
    string Category,
    string Severity,
    string? LinkUrl = null,
    string? DeduplicationKey = null,
    string? RecipientEmail = null);

public interface INotificationService
{
    Task<Notification?> NotifyUserAsync(CreateNotificationCommand command, CancellationToken cancellationToken = default);
    Task<bool> SnoozeAlertAsync(Guid userId, Guid? orgId, string alertOrFlagKey, int days, string reason, CancellationToken cancellationToken = default);
    Task<UserNotificationPreference> GetOrCreatePreferencesAsync(Guid userId, Guid? orgId = null, CancellationToken cancellationToken = default);
    Task<UserNotificationPreference> UpdatePreferencesAsync(Guid userId, UserNotificationPreference updated, CancellationToken cancellationToken = default);
    Task<PushSubscription> RegisterPushSubscriptionAsync(Guid userId, Guid? orgId, string endpoint, string p256dh, string auth, string? userAgent, CancellationToken cancellationToken = default);
    Task<bool> UnregisterPushSubscriptionAsync(Guid userId, string endpoint, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
