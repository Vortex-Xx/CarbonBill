using System.Net.Http.Json;
using System.Text.Json;
using CarbonBill.SharedKernel.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Notifications.Services;

public class BrevoEmailProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<BrevoEmailProvider> logger) : IEmailProvider
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<BrevoEmailProvider> _logger = logger;

    public string ProviderName => "Brevo";

    public async Task<bool> SendEmailAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Notifications:Email:Brevo:ApiKey"] ?? _configuration["BREVO_API_KEY"];
        var senderEmail = _configuration["Notifications:Email:SenderEmail"] ?? "notifications@carbonbill.app";
        var senderName = _configuration["Notifications:Email:SenderName"] ?? "CarbonBill";

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Equals("fake", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[BrevoMock] Email sent to {Recipient}. Subject: {Subject}",
                notification.RecipientEmail, notification.Subject);
            return true;
        }

        var payload = new
        {
            sender = new { name = senderName, email = senderEmail },
            to = new[] { new { email = notification.RecipientEmail } },
            subject = notification.Subject,
            htmlContent = notification.BodyHtml,
            textContent = notification.BodyPlainText ?? notification.BodyHtml
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("api-key", apiKey);
        request.Headers.Add("accept", "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Successfully sent email via Brevo to {Recipient}", notification.RecipientEmail);
            return true;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("Brevo API call failed with status {StatusCode}: {Error}", response.StatusCode, errorBody);
        throw new HttpRequestException($"Brevo API error: {response.StatusCode} - {errorBody}");
    }
}
