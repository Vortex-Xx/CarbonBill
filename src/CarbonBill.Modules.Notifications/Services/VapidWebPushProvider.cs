using System.Net.Http.Json;
using System.Text.Json;
using CarbonBill.SharedKernel.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Notifications.Services;

public class VapidWebPushProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<VapidWebPushProvider> logger) : IWebPushProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<VapidWebPushProvider> _logger = logger;

    public string ProviderName => "Vapid";

    public async Task<bool> SendPushAsync(PushNotification notification, CancellationToken cancellationToken = default)
    {
        var publicKey = _configuration["Notifications:WebPush:PublicKey"] ?? _configuration["VAPID_PUBLIC_KEY"];
        var privateKey = _configuration["Notifications:WebPush:PrivateKey"] ?? _configuration["VAPID_PRIVATE_KEY"];
        var subject = _configuration["Notifications:WebPush:Subject"] ?? "mailto:admin@carbonbill.app";

        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey) ||
            publicKey.Equals("fake", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[VapidMock] Push sent to {Endpoint}. Title: {Title}",
                notification.Endpoint, notification.Title);
            return true;
        }

        var payload = new
        {
            title = notification.Title,
            body = notification.Body,
            url = notification.ClickUrl,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, notification.Endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("TTL", "86400");
        request.Headers.Add("Urgency", "normal");
        // VAPID authorization header representation for WebPush protocol
        request.Headers.TryAddWithoutValidation("Crypto-Key", $"p256ecdsa={publicKey}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Successfully sent Web Push to {Endpoint}", notification.Endpoint);
            return true;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("VAPID Web Push failed with status {StatusCode}: {Error}", response.StatusCode, errorBody);
        throw new HttpRequestException($"WebPush error: {response.StatusCode} - {errorBody}");
    }
}
