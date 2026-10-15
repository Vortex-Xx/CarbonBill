using CarbonBill.SharedKernel.Providers;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Notifications.Services;

public class LoggingEmailProvider(ILogger<LoggingEmailProvider> logger) : IEmailProvider
{
    private readonly ILogger<LoggingEmailProvider> _logger = logger;

    public string ProviderName => "Logging";

    public Task<bool> SendEmailAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Email Sent] To: {Recipient}, Subject: {Subject}, BodyLength: {Length}",
            notification.RecipientEmail, notification.Subject, notification.BodyHtml.Length);
        return Task.FromResult(true);
    }
}
