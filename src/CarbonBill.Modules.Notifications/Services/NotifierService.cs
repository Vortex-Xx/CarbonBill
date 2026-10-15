using CarbonBill.SharedKernel.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace CarbonBill.Modules.Notifications.Services;

public class NotifierService : INotifier
{
    private readonly IEnumerable<IEmailProvider> _emailProviders;
    private readonly IEnumerable<IWebPushProvider> _pushProviders;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotifierService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public NotifierService(
        IEnumerable<IEmailProvider> emailProviders,
        IEnumerable<IWebPushProvider> pushProviders,
        IConfiguration configuration,
        ILogger<NotifierService> logger)
    {
        _emailProviders = emailProviders;
        _pushProviders = pushProviders;
        _configuration = configuration;
        _logger = logger;

        // Exponential backoff retry policy: 3 attempts (e.g. 100ms, 200ms, 400ms)
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt - 1)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "Notifier retry {RetryCount} after {DelayMs}ms due to: {Message}",
                        retryCount, timeSpan.TotalMilliseconds, exception.Message);
                });
    }

    public async Task<bool> SendEmailAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            var chosenProviderName = _configuration["Notifications:Email:Provider"] ?? "Logging";
            var provider = _emailProviders.FirstOrDefault(p => p.ProviderName.Equals(chosenProviderName, StringComparison.OrdinalIgnoreCase))
                           ?? _emailProviders.FirstOrDefault()
                           ?? new LoggingEmailProvider(LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LoggingEmailProvider>());

            return await _retryPolicy.ExecuteAsync(async ct =>
            {
                return await provider.SendEmailAsync(notification, ct);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // Provider failure must NEVER break the caller
            _logger.LogError(ex, "Failed to send email to {Recipient} after retries. Caller is protected.", notification.RecipientEmail);
            return false;
        }
    }

    public async Task<bool> SendPushAsync(PushNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            var chosenProviderName = _configuration["Notifications:WebPush:Provider"] ?? "Logging";
            var provider = _pushProviders.FirstOrDefault(p => p.ProviderName.Equals(chosenProviderName, StringComparison.OrdinalIgnoreCase))
                           ?? _pushProviders.FirstOrDefault()
                           ?? new LoggingWebPushProvider(LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LoggingWebPushProvider>());

            return await _retryPolicy.ExecuteAsync(async ct =>
            {
                return await provider.SendPushAsync(notification, ct);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // Provider failure must NEVER break the caller
            _logger.LogError(ex, "Failed to send push to {Endpoint} after retries. Caller is protected.", notification.Endpoint);
            return false;
        }
    }
}
