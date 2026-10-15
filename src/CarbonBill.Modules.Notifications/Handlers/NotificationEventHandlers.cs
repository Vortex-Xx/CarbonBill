using CarbonBill.Modules.Notifications.Domain;
using CarbonBill.Modules.Notifications.Services;
using CarbonBill.SharedKernel.Events;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Notifications.Handlers;

public class MissingAlertRaisedEventHandler(
    INotificationService notificationService,
    INotificationTemplateRenderer templateRenderer,
    ILogger<MissingAlertRaisedEventHandler> logger) : IDomainEventHandler<MissingAlertRaisedEvent>
{
    private readonly INotificationService _notificationService = notificationService;
    private readonly INotificationTemplateRenderer _templateRenderer = templateRenderer;
    private readonly ILogger<MissingAlertRaisedEventHandler> _logger = logger;

    public async Task HandleAsync(MissingAlertRaisedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling MissingAlertRaisedEvent for AlertId {AlertId}, Asset {AssetName}",
            domainEvent.AlertId, domainEvent.AssetName);

        var targetUserId = domainEvent.ResponsibleUserId ?? Guid.Empty;
        var prefs = await _notificationService.GetOrCreatePreferencesAsync(targetUserId, domainEvent.OrgId, cancellationToken);
        var rendered = _templateRenderer.RenderMissingAlert(domainEvent, prefs.PreferredLanguage);

        var deduplicationKey = $"GAP:{domainEvent.OrgId}:{domainEvent.AssetId}:{domainEvent.Period}:{domainEvent.DocType}";

        await _notificationService.NotifyUserAsync(new CreateNotificationCommand(
            OrgId: domainEvent.OrgId,
            UserId: targetUserId,
            Title: rendered.Title,
            Body: rendered.Body,
            Category: NotificationCategories.Gap,
            Severity: domainEvent.Severity,
            LinkUrl: rendered.ActionUrl,
            DeduplicationKey: deduplicationKey
        ), cancellationToken);
    }
}

public class FlagRaisedEventHandler(
    INotificationService notificationService,
    INotificationTemplateRenderer templateRenderer,
    ILogger<FlagRaisedEventHandler> logger) : IDomainEventHandler<FlagRaisedEvent>
{
    private readonly INotificationService _notificationService = notificationService;
    private readonly INotificationTemplateRenderer _templateRenderer = templateRenderer;
    private readonly ILogger<FlagRaisedEventHandler> _logger = logger;

    public async Task HandleAsync(FlagRaisedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling FlagRaisedEvent for Rule {RuleCode}, Org {OrgId}",
            domainEvent.RuleCode, domainEvent.OrgId);

        // Fallback target: org-wide notification (Guid.Empty / Compliance)
        var targetUserId = Guid.Empty;
        var prefs = await _notificationService.GetOrCreatePreferencesAsync(targetUserId, domainEvent.OrgId, cancellationToken);
        var rendered = _templateRenderer.RenderFlagRaised(domainEvent, prefs.PreferredLanguage);

        var deduplicationKey = $"FLAG:{domainEvent.OrgId}:{domainEvent.RuleCode}:{domainEvent.Period}";

        await _notificationService.NotifyUserAsync(new CreateNotificationCommand(
            OrgId: domainEvent.OrgId,
            UserId: targetUserId,
            Title: rendered.Title,
            Body: rendered.Body,
            Category: NotificationCategories.Flag,
            Severity: domainEvent.Severity,
            LinkUrl: rendered.ActionUrl,
            DeduplicationKey: deduplicationKey
        ), cancellationToken);
    }
}

public class DocumentRetakeRequestedEventHandler(
    INotificationService notificationService,
    INotificationTemplateRenderer templateRenderer,
    ILogger<DocumentRetakeRequestedEventHandler> logger) : IDomainEventHandler<DocumentRetakeRequestedEvent>
{
    private readonly INotificationService _notificationService = notificationService;
    private readonly INotificationTemplateRenderer _templateRenderer = templateRenderer;
    private readonly ILogger<DocumentRetakeRequestedEventHandler> _logger = logger;

    public async Task HandleAsync(DocumentRetakeRequestedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling DocumentRetakeRequestedEvent for Doc {DocId}, Uploader {UserId}",
            domainEvent.DocumentId, domainEvent.UploaderUserId);

        var targetUserId = domainEvent.UploaderUserId ?? Guid.Empty;
        var prefs = await _notificationService.GetOrCreatePreferencesAsync(targetUserId, domainEvent.OrgId, cancellationToken);
        var rendered = _templateRenderer.RenderDocumentRetake(domainEvent, prefs.PreferredLanguage);

        var deduplicationKey = $"RETAKE:{domainEvent.DocumentId}";

        await _notificationService.NotifyUserAsync(new CreateNotificationCommand(
            OrgId: domainEvent.OrgId,
            UserId: targetUserId,
            Title: rendered.Title,
            Body: rendered.Body,
            Category: NotificationCategories.DocumentRetake,
            Severity: "Amber",
            LinkUrl: rendered.ActionUrl,
            DeduplicationKey: deduplicationKey
        ), cancellationToken);
    }
}

public class DocumentFailedEventHandler(
    INotificationService notificationService,
    INotificationTemplateRenderer templateRenderer,
    ILogger<DocumentFailedEventHandler> logger) : IDomainEventHandler<DocumentFailedEvent>
{
    private readonly INotificationService _notificationService = notificationService;
    private readonly INotificationTemplateRenderer _templateRenderer = templateRenderer;
    private readonly ILogger<DocumentFailedEventHandler> _logger = logger;

    public async Task HandleAsync(DocumentFailedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling DocumentFailedEvent for Doc {DocId}, Uploader {UserId}",
            domainEvent.DocumentId, domainEvent.UploaderUserId);

        var targetUserId = domainEvent.UploaderUserId ?? Guid.Empty;
        var prefs = await _notificationService.GetOrCreatePreferencesAsync(targetUserId, domainEvent.OrgId, cancellationToken);
        var rendered = _templateRenderer.RenderDocumentFailed(domainEvent, prefs.PreferredLanguage);

        var deduplicationKey = $"DOC_FAILED:{domainEvent.DocumentId}";

        await _notificationService.NotifyUserAsync(new CreateNotificationCommand(
            OrgId: domainEvent.OrgId,
            UserId: targetUserId,
            Title: rendered.Title,
            Body: rendered.Body,
            Category: NotificationCategories.System,
            Severity: "Red",
            LinkUrl: rendered.ActionUrl,
            DeduplicationKey: deduplicationKey
        ), cancellationToken);
    }
}
