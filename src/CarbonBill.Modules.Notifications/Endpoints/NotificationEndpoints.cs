using CarbonBill.Modules.Notifications.Domain;
using CarbonBill.Modules.Notifications.Persistence;
using CarbonBill.Modules.Notifications.Services;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.Notifications.Endpoints;

public record SnoozeRequest(string AlertOrFlagKey, int Days, string Reason);

public record PushSubscriptionRequest(string Endpoint, string P256Dh, string Auth, string? UserAgent = null);

public record UpdatePreferencesRequest(
    string PreferredLanguage,
    bool InAppEnabled,
    bool WebPushEnabled,
    bool EmailEnabled,
    bool QuietHoursEnabled,
    int QuietHoursStartMinutes,
    int QuietHoursEndMinutes,
    int TimeZoneOffsetMinutes);

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/notifications")
            .WithTags("Notifications");

        // GET /api/v1/notifications
        group.MapGet("/", async (
            bool? unreadOnly,
            int? page,
            int? pageSize,
            NotificationsDbContext dbContext,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var userId = tenantContext.CurrentUserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var query = dbContext.Notifications
                .Where(n => n.UserId == userId);

            if (unreadOnly == true)
            {
                query = query.Where(n => !n.IsRead);
            }

            var p = Math.Max(1, page ?? 1);
            var size = Math.Clamp(pageSize ?? 20, 1, 100);

            var totalCount = await query.CountAsync(ct);
            var unreadCount = await dbContext.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync(ct);

            var items = await query
                .OrderByDescending(n => n.CreatedAtUtc)
                .Skip((p - 1) * size)
                .Take(size)
                .Select(n => new
                {
                    n.Id,
                    n.OrgId,
                    n.UserId,
                    n.Title,
                    n.Body,
                    n.Channel,
                    n.Category,
                    n.Severity,
                    n.LinkUrl,
                    n.IsRead,
                    n.ReadAtUtc,
                    n.DeduplicationKey,
                    n.CollapseCount,
                    n.SnoozedUntilUtc,
                    n.SnoozeReason,
                    n.CreatedAtUtc
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                totalCount,
                unreadCount,
                page = p,
                pageSize = size,
                items
            });
        });

        // POST /api/v1/notifications/{id}/read
        group.MapPost("/{id:guid}/read", async (
            Guid id,
            INotificationService notificationService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var userId = tenantContext.CurrentUserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var success = await notificationService.MarkAsReadAsync(id, userId, ct);
            return success ? Results.Ok() : Results.NotFound();
        });

        // POST /api/v1/notifications/read-all
        group.MapPost("/read-all", async (
            INotificationService notificationService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var userId = tenantContext.CurrentUserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var count = await notificationService.MarkAllAsReadAsync(userId, ct);
            return Results.Ok(new { markedReadCount = count });
        });

        // POST /api/v1/notifications/snooze
        group.MapPost("/snooze", async (
            SnoozeRequest request,
            INotificationService notificationService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var userId = tenantContext.CurrentUserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "Snooze reason is mandatory to prevent hidden defects." });
            }

            if (request.Days <= 0)
            {
                return Results.BadRequest(new { error = "Snooze days must be greater than zero." });
            }

            await notificationService.SnoozeAlertAsync(
                userId,
                tenantContext.CurrentOrgId,
                request.AlertOrFlagKey,
                request.Days,
                request.Reason,
                ct);

            return Results.Ok(new { message = $"Alert snoozed for {request.Days} days." });
        });

        // GET /api/v1/notifications/preferences
        group.MapGet("/preferences", async (
            INotificationService notificationService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var userId = tenantContext.CurrentUserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var prefs = await notificationService.GetOrCreatePreferencesAsync(userId, tenantContext.CurrentOrgId, ct);
            return Results.Ok(prefs);
        });

        // PUT /api/v1/notifications/preferences
        group.MapPut("/preferences", async (
            UpdatePreferencesRequest request,
            INotificationService notificationService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var userId = tenantContext.CurrentUserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var updated = new UserNotificationPreference
            {
                UserId = userId,
                OrgId = tenantContext.CurrentOrgId,
                PreferredLanguage = string.IsNullOrWhiteSpace(request.PreferredLanguage) ? "en" : request.PreferredLanguage,
                InAppEnabled = request.InAppEnabled,
                WebPushEnabled = request.WebPushEnabled,
                EmailEnabled = request.EmailEnabled,
                QuietHoursEnabled = request.QuietHoursEnabled,
                QuietHoursStartMinutes = request.QuietHoursStartMinutes,
                QuietHoursEndMinutes = request.QuietHoursEndMinutes,
                TimeZoneOffsetMinutes = request.TimeZoneOffsetMinutes
            };

            var saved = await notificationService.UpdatePreferencesAsync(userId, updated, ct);
            return Results.Ok(saved);
        });

        // POST /api/v1/notifications/push-subscription
        group.MapPost("/push-subscription", async (
            PushSubscriptionRequest request,
            INotificationService notificationService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var userId = tenantContext.CurrentUserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var sub = await notificationService.RegisterPushSubscriptionAsync(
                userId,
                tenantContext.CurrentOrgId,
                request.Endpoint,
                request.P256Dh,
                request.Auth,
                request.UserAgent,
                ct);

            return Results.Ok(sub);
        });

        // DELETE /api/v1/notifications/push-subscription
        group.MapDelete("/push-subscription", async (
            string endpoint,
            INotificationService notificationService,
            ITenantContext tenantContext,
            CancellationToken ct) =>
        {
            var userId = tenantContext.CurrentUserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var success = await notificationService.UnregisterPushSubscriptionAsync(userId, endpoint, ct);
            return success ? Results.Ok() : Results.NotFound();
        });

        return endpoints;
    }
}
