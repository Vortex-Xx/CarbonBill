using CarbonBill.SharedKernel.Events;

namespace CarbonBill.Modules.Notifications.Services;

public record RenderedNotification(string Title, string Body, string? ActionUrl = null);

public record WeeklyDigestItem(
    string Title,
    string Details,
    string Severity, // "Red", "Amber", "Info"
    DateTime OccurredAtUtc,
    string? Url = null);

public record WeeklyDigestModel(
    string OrganizationName,
    string Period,
    IReadOnlyList<WeeklyDigestItem> Top5Items,
    int TotalActiveAlertsCount);

public record RenderedDigest(string Subject, string HtmlBody, string PlainTextBody);

public interface INotificationTemplateRenderer
{
    RenderedNotification RenderMissingAlert(MissingAlertRaisedEvent alert, string language = "en");
    RenderedNotification RenderFlagRaised(FlagRaisedEvent flag, string language = "en");
    RenderedNotification RenderDocumentRetake(DocumentRetakeRequestedEvent retake, string language = "en");
    RenderedNotification RenderDocumentFailed(DocumentFailedEvent failed, string language = "en");
    RenderedDigest RenderWeeklyDigest(WeeklyDigestModel model, string language = "en");
    string FormatNumber(long number, string language = "en");
}
