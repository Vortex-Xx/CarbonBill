using CarbonBill.SharedKernel.Providers;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Notifications.Services;

public class LoggingWebPushProvider(ILogger<LoggingWebPushProvider> logger) : IWebPushProvider
{
    private readonly ILogger<LoggingWebPushProvider> _logger = logger;

    public string ProviderName => "Logging";

    public Task<bool> SendPushAsync(PushNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[WebPush Sent] Endpoint: {Endpoint}, Title: {Title}, Body: {Body}",
            notification.Endpoint, notification.Title, notification.Body);
        return Task.FromResult(true);
    }
}
