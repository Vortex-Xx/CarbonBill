using CarbonBill.SharedKernel.Providers;

namespace CarbonBill.Modules.Notifications.Services;

public interface IWebPushProvider
{
    string ProviderName { get; }
    Task<bool> SendPushAsync(PushNotification notification, CancellationToken cancellationToken = default);
}
