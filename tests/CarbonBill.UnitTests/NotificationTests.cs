using System.Globalization;
using CarbonBill.Modules.Notifications.Domain;
using CarbonBill.Modules.Notifications.Jobs;
using CarbonBill.Modules.Notifications.Persistence;
using CarbonBill.Modules.Notifications.Services;
using CarbonBill.SharedKernel.Events;
using CarbonBill.SharedKernel.Providers;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarbonBill.UnitTests;

public class FakeFailingEmailProvider : IEmailProvider
{
    public string ProviderName => "Failing";
    public int AttemptCount { get; private set; }

    public Task<bool> SendEmailAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        AttemptCount++;
        throw new HttpRequestException("Simulated 503 Service Unavailable");
    }
}

public class FakeSuccessfulEmailProvider : IEmailProvider
{
    public string ProviderName => "Success";
    public int AttemptCount { get; private set; }

    public Task<bool> SendEmailAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        AttemptCount++;
        return Task.FromResult(true);
    }
}

public class MockNotifier : INotifier
{
    public List<EmailNotification> SentEmails { get; } = [];
    public List<PushNotification> SentPushes { get; } = [];

    public Task<bool> SendEmailAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        SentEmails.Add(notification);
        return Task.FromResult(true);
    }

    public Task<bool> SendPushAsync(PushNotification notification, CancellationToken cancellationToken = default)
    {
        SentPushes.Add(notification);
        return Task.FromResult(true);
    }
}

public sealed class NotificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenantContext;
    private readonly NotificationsDbContext _dbContext;
    private readonly NotificationTemplateRenderer _renderer;
    private readonly MockNotifier _mockNotifier;
    private readonly NotificationService _notificationService;

    public NotificationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _tenantContext = new TenantContext();
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new NotificationsDbContext(options, _tenantContext);
        _dbContext.Database.EnsureCreated();

        _renderer = new NotificationTemplateRenderer();
        _mockNotifier = new MockNotifier();
        _notificationService = new NotificationService(_dbContext, _mockNotifier, NullLogger<NotificationService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void TemplateRenderer_RendersBanglaNumerals_Correctly()
    {
        var banglaDigits = _renderer.FormatNumber(1234567890, "bn");
        Assert.Equal("১২৩৪৫৬৭৮৯০", banglaDigits);

        var englishDigits = _renderer.FormatNumber(1234567890, "en");
        Assert.Equal("1234567890", englishDigits);

        var defaultDigits = _renderer.FormatNumber(1234567890);
        Assert.Equal("1234567890", defaultDigits);
    }

    [Fact]
    public void TemplateRenderer_RendersMissingAlert_Bilingual()
    {
        var alert = new MissingAlertRaisedEvent(
            AlertId: Guid.NewGuid(),
            OrgId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            AssetId: Guid.NewGuid(),
            AssetName: "Generator 2",
            DocType: "Diesel Slip",
            Period: "2026-09",
            DueDate: new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc),
            EscalationState: "FloorReminder",
            Severity: "Amber",
            ResponsibleUserId: Guid.NewGuid(),
            OccurredOnUtc: DateTime.UtcNow
        );

        // Bangla
        var bn = _renderer.RenderMissingAlert(alert, "bn");
        Assert.Contains("অনুপস্থিত নথি সতৰ্কবার্তা", bn.Title, StringComparison.Ordinal);
        Assert.Contains("Generator 2-এর জন্য Diesel Slip জমা দিন", bn.Body, StringComparison.Ordinal);
        Assert.Contains("২০২৬-১০-১০", bn.Body, StringComparison.Ordinal); // Formatted with Bangla digits

        // English
        var en = _renderer.RenderMissingAlert(alert, "en");
        Assert.Contains("Missing Document", en.Title, StringComparison.Ordinal);
        Assert.Contains("Please upload Diesel Slip for Generator 2", en.Body, StringComparison.Ordinal);
        Assert.Contains("2026-10-10", en.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateRenderer_RendersFlagRaised_Bilingual()
    {
        var flag = new FlagRaisedEvent(
            FlagId: Guid.NewGuid(),
            OrgId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            RuleCode: "DQ-SPIKE-001",
            Family: "DataQuality",
            Severity: "Red",
            Period: "2026-09",
            ExplanationBn: "ডিজেল ব্যবহারে অস্বাভাবিক বৃদ্ধি (১.৬ গুণের বেশি)",
            ExplanationEn: "Abnormal surge in diesel consumption (>1.6x)",
            SuggestedActionBn: "লগশিট ও মিটার রিডিং যাচাই করুন",
            SuggestedActionEn: "Verify logbook and meter readings",
            OccurredOnUtc: DateTime.UtcNow
        );

        // Bangla
        var bn = _renderer.RenderFlagRaised(flag, "bn");
        Assert.Contains("জরুরি", bn.Title, StringComparison.Ordinal);
        Assert.Contains("ডিজেল ব্যবহারে অস্বাভাবিক বৃদ্ধি", bn.Body, StringComparison.Ordinal);
        Assert.Contains("লগশিট ও মিটার রিডিং যাচাই করুন", bn.Body, StringComparison.Ordinal);

        // English
        var en = _renderer.RenderFlagRaised(flag, "en");
        Assert.Contains("CRITICAL", en.Title, StringComparison.Ordinal);
        Assert.Contains("Abnormal surge in diesel consumption", en.Body, StringComparison.Ordinal);
        Assert.Contains("Verify logbook and meter readings", en.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void TemplateRenderer_RendersDocumentRetakeAndFailed_Bilingual()
    {
        var retake = new DocumentRetakeRequestedEvent(
            DocumentId: Guid.NewGuid(),
            OrgId: Guid.NewGuid(),
            UploaderUserId: Guid.NewGuid(),
            FileName: "generator_diesel_slip.jpg",
            Reason: "Image is blurry; meter serial number cannot be recognized",
            OccurredOnUtc: DateTime.UtcNow
        );

        var bnRetake = _renderer.RenderDocumentRetake(retake, "bn");
        Assert.Contains("নথি পুনরায় ছবি তোলা প্রয়োজন", bnRetake.Title, StringComparison.Ordinal);
        Assert.Contains("generator_diesel_slip.jpg", bnRetake.Body, StringComparison.Ordinal);

        var enRetake = _renderer.RenderDocumentRetake(retake, "en");
        Assert.Contains("Document Retake Required", enRetake.Title, StringComparison.Ordinal);
        Assert.Contains("Please capture and re-upload", enRetake.Body, StringComparison.Ordinal);

        var failed = new DocumentFailedEvent(
            DocumentId: Guid.NewGuid(),
            OrgId: Guid.NewGuid(),
            UploaderUserId: Guid.NewGuid(),
            FileName: "corrupt_file.pdf",
            FailureReason: "File format damaged or unreadable",
            OccurredOnUtc: DateTime.UtcNow
        );

        var bnFailed = _renderer.RenderDocumentFailed(failed, "bn");
        Assert.Contains("নথি প্রক্রিয়াকরণ ব্যর্থ হয়েছে", bnFailed.Title, StringComparison.Ordinal);

        var enFailed = _renderer.RenderDocumentFailed(failed, "en");
        Assert.Contains("Document Processing Failed", enFailed.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void WeeklyDigest_IncludesTop5_AndGhgProtocolDisclaimer()
    {
        var items = new List<WeeklyDigestItem>
        {
            new("High Genset Reliance", "Genset provided >20% power", "Red", DateTime.UtcNow),
            new("Missing Gas Bill", "Titgas bill for Line 1 missing", "Red", DateTime.UtcNow),
            new("Low OCR Confidence", "Bill total read at 62% confidence", "Amber", DateTime.UtcNow),
            new("Factor Override Pending", "Grid factor awaiting approval", "Amber", DateTime.UtcNow),
            new("Outdated Peer Baseline", "Peer group baseline older than 180d", "Info", DateTime.UtcNow),
            new("Sixth Alert Excluded", "This should not appear in top 5", "Info", DateTime.UtcNow)
        };

        var model = new WeeklyDigestModel(
            OrganizationName: "Apex Spinning Mills",
            Period: "2026-09",
            Top5Items: items.Take(5).ToList(),
            TotalActiveAlertsCount: 15
        );

        var renderedBn = _renderer.RenderWeeklyDigest(model, "bn");
        Assert.Contains("শীর্ষ ৫টি", renderedBn.Subject, StringComparison.Ordinal);
        Assert.Contains("মোট সক্রিয় বিষয়: <strong>১৫টি</strong>", renderedBn.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("কার্বনবিল ফলাফল GHG প্রোটোকল পদ্ধতির সাথে সামঞ্জস্যপূর্ণ আনুমানিক হিসাব", renderedBn.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Sixth Alert Excluded", renderedBn.HtmlBody, StringComparison.Ordinal);

        var renderedEn = _renderer.RenderWeeklyDigest(model, "en");
        Assert.Contains("Top 5 Alerts", renderedEn.Subject, StringComparison.Ordinal);
        Assert.Contains("Total Active Issues: <strong>15</strong>", renderedEn.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("aligned with GHG Protocol methodology", renderedEn.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Sixth Alert Excluded", renderedEn.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotificationService_CollapseRepeatedAlerts_IncrementsCollapseCount()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var dedupKey = "GAP:site1:asset1:2026-09:diesel";

        // First notification
        var first = await _notificationService.NotifyUserAsync(new CreateNotificationCommand(
            OrgId: orgId,
            UserId: userId,
            Title: "Missing Diesel Slip",
            Body: "Please submit diesel slip",
            Category: NotificationCategories.Gap,
            Severity: "Amber",
            DeduplicationKey: dedupKey
        ));

        Assert.NotNull(first);
        Assert.Equal(1, first.CollapseCount);

        // Second notification with same key while unread
        var second = await _notificationService.NotifyUserAsync(new CreateNotificationCommand(
            OrgId: orgId,
            UserId: userId,
            Title: "Missing Diesel Slip (Updated)",
            Body: "Please submit diesel slip urgently",
            Category: NotificationCategories.Gap,
            Severity: "Amber",
            DeduplicationKey: dedupKey
        ));

        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, second.CollapseCount);

        var allNotifications = await _dbContext.Notifications.Where(n => n.UserId == userId).ToListAsync();
        Assert.Single(allNotifications);
        Assert.Equal(2, allNotifications[0].CollapseCount);
    }

    [Fact]
    public async Task NotificationService_Snooze_RequiresMandatoryReason()
    {
        var userId = Guid.NewGuid();
        var key = "FLAG:SPIKE-001";

        // Empty reason must throw ArgumentException (anti-hiding rule)
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _notificationService.SnoozeAlertAsync(userId, null, key, 7, "   "));
    }

    [Fact]
    public async Task NotificationService_Snooze_SuppressesAlertsWhileActive()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var key = "FLAG:SPIKE-001";

        // Snooze for 14 days with reason
        await _notificationService.SnoozeAlertAsync(userId, orgId, key, 14, "Overtime production surge for Eid shipment");

        // Attempt to notify with this key
        var result = await _notificationService.NotifyUserAsync(new CreateNotificationCommand(
            OrgId: orgId,
            UserId: userId,
            Title: "Consumption Spike",
            Body: "High spike detected",
            Category: NotificationCategories.Flag,
            Severity: "Amber",
            DeduplicationKey: key
        ));

        // Result should be null because notification is suppressed by active snooze
        Assert.Null(result);

        var notifications = await _dbContext.Notifications.Where(n => n.UserId == userId).ToListAsync();
        Assert.Empty(notifications);
    }

    [Fact]
    public async Task NotificationService_QuietHours_FiltersNonCriticalRealTimeAlerts()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Configure user with quiet hours active right now
        var prefs = await _notificationService.GetOrCreatePreferencesAsync(userId, orgId);
        prefs.QuietHoursEnabled = true;
        prefs.QuietHoursStartMinutes = 0;
        prefs.QuietHoursEndMinutes = 24 * 60; // Quiet hours all day for test
        await _notificationService.UpdatePreferencesAsync(userId, prefs);

        // Register push subscription
        await _notificationService.RegisterPushSubscriptionAsync(
            userId, orgId, "https://push.example.com/test", "key", "auth", "TestBrowser");

        // Send non-critical Amber notification
        await _notificationService.NotifyUserAsync(new CreateNotificationCommand(
            OrgId: orgId,
            UserId: userId,
            Title: "Amber Alert",
            Body: "Non-critical alert",
            Category: NotificationCategories.Gap,
            Severity: "Amber",
            RecipientEmail: "user@example.com"
        ));

        // In-app should be saved, but Push and Email suppressed due to quiet hours
        var savedInApp = await _dbContext.Notifications.Where(n => n.UserId == userId).ToListAsync();
        Assert.Single(savedInApp);
        Assert.Empty(_mockNotifier.SentPushes);
        Assert.Empty(_mockNotifier.SentEmails);

        // Now send critical Red notification during quiet hours
        await _notificationService.NotifyUserAsync(new CreateNotificationCommand(
            OrgId: orgId,
            UserId: userId,
            Title: "Critical Red Alert",
            Body: "Emergency outage",
            Category: NotificationCategories.Flag,
            Severity: "Red",
            RecipientEmail: "user@example.com"
        ));

        // Critical Red bypasses quiet hours
        Assert.Single(_mockNotifier.SentPushes);
        Assert.Single(_mockNotifier.SentEmails);
    }

    [Fact]
    public async Task NotifierService_ProviderFailure_RetriesAndDoesNotBreakCaller()
    {
        var failingProvider = new FakeFailingEmailProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Email:Provider"] = "Failing"
            })
            .Build();

        var notifierService = new NotifierService(
            emailProviders: [failingProvider],
            pushProviders: [new LoggingWebPushProvider(NullLogger<LoggingWebPushProvider>.Instance)],
            configuration: configuration,
            logger: NullLogger<NotifierService>.Instance
        );

        var notification = new EmailNotification("test@carbonbill.app", "Test Subject", "<p>Body</p>");

        // Should NOT throw exception to caller, should return false safely
        var result = await notifierService.SendEmailAsync(notification);

        Assert.False(result);
        // Assert that retry was attempted (1 initial + 3 retries = 4 attempts)
        Assert.Equal(4, failingProvider.AttemptCount);
    }

    [Fact]
    public async Task NotifierService_ProviderSuccess_ReturnsTrue()
    {
        var successProvider = new FakeSuccessfulEmailProvider();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Email:Provider"] = "Success"
            })
            .Build();

        var notifierService = new NotifierService(
            emailProviders: [successProvider],
            pushProviders: [new LoggingWebPushProvider(NullLogger<LoggingWebPushProvider>.Instance)],
            configuration: configuration,
            logger: NullLogger<NotifierService>.Instance
        );

        var notification = new EmailNotification("test@carbonbill.app", "Test Subject", "<p>Body</p>");

        var result = await notifierService.SendEmailAsync(notification);

        Assert.True(result);
        Assert.Equal(1, successProvider.AttemptCount);
    }
}
