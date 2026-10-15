using System.Net.Http.Headers;
using System.Net.Http.Json;
using CarbonBill.SharedKernel.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Notifications.Services;

public class ResendEmailProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<ResendEmailProvider> logger) : IEmailProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<ResendEmailProvider> _logger = logger;

    public string ProviderName => "Resend";

    public async Task<bool> SendEmailAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Notifications:Email:Resend:ApiKey"] ?? _configuration["RESEND_API_KEY"];
        var senderEmail = _configuration["Notifications:Email:SenderEmail"] ?? "onboarding@resend.dev";
        var senderName = _configuration["Notifications:Email:SenderName"] ?? "CarbonBill";

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Equals("fake", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[ResendMock] Email sent to {Recipient}. Subject: {Subject}",
                notification.RecipientEmail, notification.Subject);
            return true;
        }

        var payload = new
        {
            from = $"{senderName} <{senderEmail}>",
            to = new[] { notification.RecipientEmail },
            subject = notification.Subject,
            html = notification.BodyHtml,
            text = notification.BodyPlainText ?? notification.BodyHtml
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Successfully sent email via Resend to {Recipient}", notification.RecipientEmail);
            return true;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("Resend API call failed with status {StatusCode}: {Error}", response.StatusCode, errorBody);
        throw new HttpRequestException($"Resend API error: {response.StatusCode} - {errorBody}");
    }
}
