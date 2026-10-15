using CarbonBill.Modules.Notifications.Domain;
using CarbonBill.Modules.Notifications.Persistence;
using CarbonBill.Modules.Notifications.Services;
using CarbonBill.SharedKernel.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Notifications.Jobs;

public interface IWeeklyDigestJob
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

public class WeeklyDigestJob(
    NotificationsDbContext dbContext,
    INotificationTemplateRenderer templateRenderer,
    INotifier notifier,
    ILogger<WeeklyDigestJob> logger) : IWeeklyDigestJob
{
    private readonly NotificationsDbContext _dbContext = dbContext;
    private readonly INotificationTemplateRenderer _templateRenderer = templateRenderer;
    private readonly INotifier _notifier = notifier;
    private readonly ILogger<WeeklyDigestJob> _logger = logger;

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("[Hangfire] Starting Weekly Digest Job at {StartTime}", startTime);

        try
        {
            var now = DateTime.UtcNow;
            var currentPeriod = now.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

            // Query active unread notifications across all users
            var unreadNotifications = await _dbContext.Notifications
                .Where(n => !n.IsRead && (n.SnoozedUntilUtc == null || n.SnoozedUntilUtc <= now))
                .ToListAsync(cancellationToken);

            var userGroups = unreadNotifications.GroupBy(n => n.UserId);

            int digestsSent = 0;
            foreach (var group in userGroups)
            {
                var userId = group.Key;
                var prefs = await _dbContext.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

                // If user has disabled email, skip sending weekly email digest
                if (prefs != null && !prefs.EmailEnabled)
                {
                    continue;
                }

                var language = prefs?.PreferredLanguage ?? "en";

                // Severity ranking: Red (1) > Amber (2) > Info/other (3)
                int SeverityWeight(string s) => s.ToUpperInvariant() switch
                {
                    "RED" => 1,
                    "AMBER" => 2,
                    _ => 3
                };

                var sortedAlerts = group
                    .OrderBy(n => SeverityWeight(n.Severity))
                    .ThenByDescending(n => n.CreatedAtUtc)
                    .ToList();

                // Flag fatigue rule: Strictly select the Top 5 priorities
                var top5 = sortedAlerts
                    .Take(5)
                    .Select(n => new WeeklyDigestItem(
                        Title: n.Title,
                        Details: n.Body,
                        Severity: n.Severity,
                        OccurredAtUtc: n.CreatedAtUtc,
                        Url: n.LinkUrl))
                    .ToList();

                var orgId = group.First().OrgId;
                var orgName = $"Factory ({orgId.ToString()[..8]})";

                var digestModel = new WeeklyDigestModel(
                    OrganizationName: orgName,
                    Period: currentPeriod,
                    Top5Items: top5,
                    TotalActiveAlertsCount: sortedAlerts.Count);

                var rendered = _templateRenderer.RenderWeeklyDigest(digestModel, language);

                // Send to user email (fallback to notifications email)
                var recipientEmail = $"user-{userId.ToString()[..8]}@carbonbill.local";

                await _notifier.SendEmailAsync(new EmailNotification(
                    RecipientEmail: recipientEmail,
                    Subject: rendered.Subject,
                    BodyHtml: rendered.HtmlBody,
                    BodyPlainText: rendered.PlainTextBody), cancellationToken);

                digestsSent++;
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("[Hangfire] Weekly Digest Job completed in {Duration}ms. Sent {Count} digests.",
                duration.TotalMilliseconds, digestsSent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Hangfire] Weekly Digest Job encountered an error");
            throw;
        }
    }
}
