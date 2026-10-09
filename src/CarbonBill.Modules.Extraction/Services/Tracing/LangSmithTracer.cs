using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Extraction.Services.Tracing;

public interface ILangSmithTracer
{
    bool IsEnabled { get; }
    Task<string?> TraceRunAsync(
        string name,
        string runType,
        object inputs,
        object? outputs,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        Dictionary<string, object>? metadata = null,
        string? errorMessage = null,
        CancellationToken ct = default);
}

public sealed class LangSmithTracer : ILangSmithTracer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<LangSmithTracer> _logger;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _project;
    private readonly bool _isEnabled;

    public bool IsEnabled => _isEnabled && !string.IsNullOrWhiteSpace(_apiKey);

    public LangSmithTracer(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LangSmithTracer> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var tracingSetting = configuration["LANGSMITH_TRACING"]
            ?? configuration["LangSmith:Tracing"]
            ?? "true";

        _isEnabled = string.Equals(tracingSetting, "true", StringComparison.OrdinalIgnoreCase);

        _endpoint = configuration["LANGSMITH_ENDPOINT"]
            ?? configuration["LangSmith:Endpoint"]
            ?? "https://api.smith.langchain.com";

        _apiKey = configuration["LANGSMITH_API_KEY"]
            ?? configuration["LangSmith:ApiKey"]
            ?? string.Empty;

        _project = configuration["LANGSMITH_PROJECT"]
            ?? configuration["LangSmith:Project"]
            ?? "CarbonBill";
    }

    public async Task<string?> TraceRunAsync(
        string name,
        string runType,
        object inputs,
        object? outputs,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        Dictionary<string, object>? metadata = null,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            return null;
        }

        var runId = Guid.NewGuid().ToString();

        try
        {
            var payload = new
            {
                id = runId,
                name = name,
                run_type = runType,
                inputs = inputs,
                outputs = outputs,
                session_name = _project,
                start_time = startTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                end_time = endTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                extra = new
                {
                    metadata = metadata ?? new Dictionary<string, object>()
                },
                error = errorMessage
            };

            var jsonContent = JsonSerializer.Serialize(payload, JsonOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint.TrimEnd('/')}/runs")
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("x-api-key", _apiKey);

            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("LangSmith run trace {RunId} returned HTTP {StatusCode}: {Error}", runId, response.StatusCode, errorText);
                return null;
            }

            _logger.LogInformation("LangSmith trace successfully logged to project '{Project}' (RunId: {RunId})", _project, runId);
            return runId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send trace {RunId} to LangSmith at {Endpoint}. Extraction continues normally.", runId, _endpoint);
            return null;
        }
    }
}
