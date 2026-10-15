using CarbonBill.SharedKernel.Providers;

namespace CarbonBill.Modules.Notifications.Services;

public interface IEmailProvider
{
    string ProviderName { get; }
    Task<bool> SendEmailAsync(EmailNotification notification, CancellationToken cancellationToken = default);
}
