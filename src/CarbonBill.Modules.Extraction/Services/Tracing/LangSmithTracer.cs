using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Extraction.Services.Tracing;

public sealed class LangSmithTracer : CarbonBill.SharedKernel.Providers.ILangSmithTracer
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

        string? ep = configuration["LANGSMITH_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(ep)) ep = configuration["LangSmith:Endpoint"];
        if (string.IsNullOrWhiteSpace(ep)) ep = Environment.GetEnvironmentVariable("LANGSMITH_ENDPOINT");
        _endpoint = !string.IsNullOrWhiteSpace(ep) ? ep : "https://api.smith.langchain.com";

        string? key = configuration["LANGSMITH_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) key = configuration["LangSmith:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("LANGSMITH_API_KEY");
        if (string.IsNullOrWhiteSpace(key) && configuration["LANGSMITH_API_KEY"] == null)
        {
            key = ReadFromDotEnv("LANGSMITH_API_KEY");
        }
        _apiKey = key ?? string.Empty;

        string? proj = configuration["LANGSMITH_PROJECT"];
        if (string.IsNullOrWhiteSpace(proj)) proj = configuration["LangSmith:Project"];
        if (string.IsNullOrWhiteSpace(proj)) proj = Environment.GetEnvironmentVariable("LANGSMITH_PROJECT");
        _project = !string.IsNullOrWhiteSpace(proj) ? proj : "CarbonBill";

        if (IsEnabled)
        {
            _logger.LogInformation("LangSmith tracking active. Traces will be emitted to project '{Project}' at {Endpoint}.", _project, _endpoint);
        }
        else
        {
            _logger.LogWarning("LangSmith tracking disabled or API key missing.");
        }
    }

    private static string? ReadFromDotEnv(string key)
    {
        try
        {
            var dir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(dir))
            {
                var envPath = Path.Combine(dir, ".env");
                if (File.Exists(envPath))
                {
                    foreach (var line in File.ReadAllLines(envPath))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase))
                        {
                            return trimmed[(key.Length + 1)..].Trim();
                        }
                    }
                }
                var parent = Directory.GetParent(dir);
                dir = parent?.FullName;
            }
        }
        catch
        {
            // Ignore file read exceptions
        }
        return null;
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
