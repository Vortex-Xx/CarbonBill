using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarbonBill.Modules.Extraction.Services.Normalization;
using CarbonBill.Modules.Extraction.Services.Tracing;
using CarbonBill.SharedKernel.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Extraction.Services.Groq;

public record GroqBillExtractionResult(
    [property: JsonPropertyName("vendor")] string? Vendor,
    [property: JsonPropertyName("billNumber")] string? BillNumber,
    [property: JsonPropertyName("billingPeriod")] string? BillingPeriod,
    [property: JsonPropertyName("docType")] string? DocType,
    [property: JsonPropertyName("quantity")] decimal Quantity,
    [property: JsonPropertyName("unit")] string? Unit,
    [property: JsonPropertyName("amountBdt")] decimal AmountBdt,
    [property: JsonPropertyName("meterNumber")] string? MeterNumber,
    [property: JsonPropertyName("powerFactorPenaltyBdt")] decimal? PowerFactorPenaltyBdt,
    [property: JsonPropertyName("demandChargePenaltyBdt")] decimal? DemandChargePenaltyBdt,
    [property: JsonPropertyName("confidence")] float Confidence,
    [property: JsonPropertyName("reasoning")] string? Reasoning);

public interface IGroqLlmExtractor
{
    Task<OcrExtractionResult> ExtractAsync(
        string rawOcrText,
        string fileName,
        string contentType,
        bool hasConsent = true,
        CancellationToken ct = default);
}

public class GroqLlmExtractor(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GroqLlmExtractor> logger,
    ILangSmithTracer? langSmithTracer = null) : IGroqLlmExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _baseUrl = configuration["VisionLlm:BaseUrl"] ?? "https://api.groq.com/openai/v1";
    private readonly string _apiKey = configuration["VisionLlm:ApiKey"] ?? string.Empty;
    private readonly string _model = configuration["VisionLlm:Model"] ?? "openai/gpt-oss-120b";

    public async Task<OcrExtractionResult> ExtractAsync(
        string rawOcrText,
        string fileName,
        string contentType,
        bool hasConsent = true,
        CancellationToken ct = default)
    {
        if (!hasConsent)
        {
            logger.LogInformation("Tenant consent for Tier 3 AI processing not granted. Using rule-based fallback.");
            return FallbackRuleBasedExtraction(rawOcrText);
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            logger.LogWarning("Groq API key not configured. Using rule-based semantic parser fallback.");
            return FallbackRuleBasedExtraction(rawOcrText);
        }

        var startTimeUtc = DateTime.UtcNow;

        try
        {
            var systemPrompt = """
            You are a specialized carbon-accounting data extraction assistant for Bangladeshi factories.
            Given OCR text from a utility bill, diesel delivery slip, gas invoice, or transport challan:
            1. Identify the vendor (DESCO, DPDC, BPDB, Padma Oil, Meghna, Jamuna, Titas Gas, etc.).
            2. Extract bill/challan number and billing period (YYYY-MM).
            3. Normalize all Bangla numerals (১২৩৪৫৬৭৮৯০) to Latin digits.
            4. Extract the consumed quantity and canonical unit (kWh for electricity, litre for diesel/fuel, m3 for natural gas, kg or tonne-km for cargo).
            5. Extract total net/payable amount in Bangladeshi Taka (BDT).
            6. Identify any power factor penalty or demand charge surcharges if present.
            7. Return ONLY valid JSON matching this schema:
            {
              "vendor": "string",
              "billNumber": "string",
              "billingPeriod": "YYYY-MM",
              "docType": "ElectricityBill | DieselSlip | GasBill | ShippingChallan",
              "quantity": 0.00,
              "unit": "kWh | litre | m3 | tonne-km",
              "amountBdt": 0.00,
              "meterNumber": "string or null",
              "powerFactorPenaltyBdt": 0.00 or null,
              "demandChargePenaltyBdt": 0.00 or null,
              "confidence": 0.95,
              "reasoning": "brief explanation"
            }
            """;

            var userPrompt = $"File: {fileName}\n\nOCR Extracted Text:\n{rawOcrText}";

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.1,
                response_format = new { type = "json_object" }
            };

            var requestJson = JsonSerializer.Serialize(requestBody);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/chat/completions");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await httpClient.SendAsync(httpRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(ct);
                logger.LogError("Groq API returned HTTP {StatusCode}: {Error}", response.StatusCode, errorText);

                if (langSmithTracer != null)
                {
                    await langSmithTracer.TraceRunAsync(
                        "GroqLlmExtractor",
                        "llm",
                        new { prompt = userPrompt, file = fileName },
                        null,
                        startTimeUtc,
                        DateTime.UtcNow,
                        new Dictionary<string, object> { ["model"] = _model, ["provider"] = "Groq", ["fileName"] = fileName },
                        $"HTTP {response.StatusCode}: {errorText}",
                        ct);
                }

                return FallbackRuleBasedExtraction(rawOcrText);
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);
            var contentString = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(contentString))
            {
                return FallbackRuleBasedExtraction(rawOcrText);
            }

            var parsed = JsonSerializer.Deserialize<GroqBillExtractionResult>(contentString, JsonOptions);

            if (parsed == null)
            {
                return FallbackRuleBasedExtraction(rawOcrText);
            }

            if (langSmithTracer != null)
            {
                await langSmithTracer.TraceRunAsync(
                    "GroqLlmExtractor",
                    "llm",
                    new { prompt = userPrompt, file = fileName },
                    new
                    {
                        rawResponse = contentString,
                        vendor = parsed.Vendor,
                        billNumber = parsed.BillNumber,
                        docType = parsed.DocType,
                        quantity = parsed.Quantity,
                        unit = parsed.Unit,
                        amountBdt = parsed.AmountBdt,
                        confidence = parsed.Confidence,
                        reasoning = parsed.Reasoning
                    },
                    startTimeUtc,
                    DateTime.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["model"] = _model,
                        ["provider"] = "Groq",
                        ["docType"] = parsed.DocType ?? "Unknown",
                        ["fileName"] = fileName
                    },
                    null,
                    ct);
            }

            var fields = new List<ExtractedFieldResult>
            {
                new("Vendor", parsed.Vendor ?? "Unknown", parsed.Vendor, parsed.Confidence, 3),
                new("BillNumber", parsed.BillNumber ?? "N/A", parsed.BillNumber, parsed.Confidence, 3),
                new("BillingPeriod", parsed.BillingPeriod ?? DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture), parsed.BillingPeriod, parsed.Confidence, 3),
                new("Quantity", parsed.Quantity.ToString("F2", CultureInfo.InvariantCulture), parsed.Quantity.ToString("F2", CultureInfo.InvariantCulture), parsed.Confidence, 3),
                new("Unit", parsed.Unit ?? "unit", parsed.Unit, parsed.Confidence, 3),
                new("AmountBdt", parsed.AmountBdt.ToString("F2", CultureInfo.InvariantCulture), parsed.AmountBdt.ToString("F2", CultureInfo.InvariantCulture), parsed.Confidence, 3)
            };

            if (!string.IsNullOrWhiteSpace(parsed.MeterNumber))
            {
                fields.Add(new("MeterNumber", parsed.MeterNumber, parsed.MeterNumber, parsed.Confidence, 3));
            }

            if (parsed.PowerFactorPenaltyBdt.HasValue && parsed.PowerFactorPenaltyBdt.Value > 0)
            {
                fields.Add(new("PowerFactorPenaltyBdt", parsed.PowerFactorPenaltyBdt.Value.ToString("F2", CultureInfo.InvariantCulture), parsed.PowerFactorPenaltyBdt.Value.ToString("F2", CultureInfo.InvariantCulture), parsed.Confidence, 3));
            }

            if (parsed.DemandChargePenaltyBdt.HasValue && parsed.DemandChargePenaltyBdt.Value > 0)
            {
                fields.Add(new("DemandChargePenaltyBdt", parsed.DemandChargePenaltyBdt.Value.ToString("F2", CultureInfo.InvariantCulture), parsed.DemandChargePenaltyBdt.Value.ToString("F2", CultureInfo.InvariantCulture), parsed.Confidence, 3));
            }

            return new OcrExtractionResult(
                Success: true,
                DetectedDocumentType: parsed.DocType,
                Fields: fields,
                ErrorMessage: null,
                TierUsed: 3);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception invoking Groq LLM extraction.");

            if (langSmithTracer != null)
            {
                await langSmithTracer.TraceRunAsync(
                    "GroqLlmExtractor",
                    "llm",
                    new { file = fileName, ocrLength = rawOcrText.Length },
                    null,
                    startTimeUtc,
                    DateTime.UtcNow,
                    new Dictionary<string, object> { ["model"] = _model, ["provider"] = "Groq", ["fileName"] = fileName },
                    ex.Message,
                    ct);
            }

            return FallbackRuleBasedExtraction(rawOcrText);
        }
    }

    private static OcrExtractionResult FallbackRuleBasedExtraction(string rawText)
    {
        var normalized = BanglaNormalizer.NormalizeDigits(rawText);
        var fields = new List<ExtractedFieldResult>();

        // Heuristic fallback
        fields.Add(new("Vendor", "Detected via OCR", "Detected via OCR", 0.70f, 1));
        fields.Add(new("ExtractedText", normalized, normalized, 0.75f, 1));

        return new OcrExtractionResult(
            Success: true,
            DetectedDocumentType: "GeneralDocument",
            Fields: fields,
            ErrorMessage: null,
            TierUsed: 1);
    }
}
