using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CarbonBill.Modules.Extraction.Services.Classification;
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
    [property: JsonPropertyName("consumerNumber")] string? ConsumerNumber,
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
        byte[]? imageBytes = null,
        CancellationToken ct = default);
}

public class GroqLlmExtractor(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GroqLlmExtractor> logger,
    CarbonBill.SharedKernel.Providers.ILangSmithTracer? langSmithTracer = null) : IGroqLlmExtractor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _baseUrl = configuration["VisionLlm:BaseUrl"] ?? "https://api.groq.com/openai/v1";
    private readonly string _apiKey = ResolveApiKey(configuration);
    private readonly string _model = configuration["VisionLlm:Model"] ?? "openai/gpt-oss-120b";
    private readonly string _visionModel = configuration["VisionLlm:VisionModel"] ?? "qwen/qwen3.8-27b";

    private static string ResolveApiKey(IConfiguration configuration)
    {
        var key = configuration["VisionLlm:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key)) return key;
        key = configuration["GROQ_API_KEY"];
        if (!string.IsNullOrWhiteSpace(key)) return key;
        key = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (!string.IsNullOrWhiteSpace(key)) return key;

        var dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            var envPath = Path.Combine(dir, ".env");
            if (File.Exists(envPath))
            {
                foreach (var line in File.ReadAllLines(envPath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("GROQ_API_KEY=", StringComparison.OrdinalIgnoreCase))
                    {
                        return trimmed["GROQ_API_KEY=".Length..].Trim();
                    }
                }
            }
            var parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }

        return string.Empty;
    }

    private static bool IsSupportedImage(string fileName, string contentType)
    {
        if (contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("jpg", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("webp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp";
    }

    private static string GetMimeType(string fileName, string contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return contentType;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }

    public async Task<OcrExtractionResult> ExtractAsync(
        string rawOcrText,
        string fileName,
        string contentType,
        bool hasConsent = true,
        byte[]? imageBytes = null,
        CancellationToken ct = default)
    {
        var startTimeUtc = DateTime.UtcNow;

        if (!hasConsent)
        {
            logger.LogInformation("Tenant consent for Tier 3 AI processing not granted. Using rule-based fallback.");
            return await FallbackRuleBasedExtractionWithTraceAsync(rawOcrText, fileName, "TenantConsentNotGranted", startTimeUtc, ct);
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            logger.LogWarning("Groq API key not configured. Using rule-based semantic parser fallback.");
            return await FallbackRuleBasedExtractionWithTraceAsync(rawOcrText, fileName, "GroqApiKeyMissing", startTimeUtc, ct);
        }

        // 1. Try Groq Vision model if image bytes are available
        if (imageBytes != null && imageBytes.Length > 0 && IsSupportedImage(fileName, contentType))
        {
            try
            {
                logger.LogInformation("Attempting Groq Vision extraction with model {VisionModel} for {FileName}...", _visionModel, fileName);
                var base64 = Convert.ToBase64String(imageBytes);
                var mime = GetMimeType(fileName, contentType);
                var dataUrl = $"data:{mime};base64,{base64}";

                var visionPrompt = """
                You are an expert utility bill and carbon accounting extraction engine specialized in Bangladeshi utility bills (BPDB, DESCO, DPDC, BREB, NESCO, WZPDCL, Titas Gas, Bakhrabad Gas, Karnaphuli Gas, Jalalabad Gas, Padma Oil, Meghna Petroleum, Jamuna Oil, etc.).
                Extract all relevant fields from the bill image into valid JSON matching this schema:
                {
                  "vendor": "string",
                  "billNumber": "string",
                  "billingPeriod": "YYYY-MM",
                  "docType": "ElectricityBill | DieselSlip | GasBill | ShippingChallan",
                  "quantity": 0.00,
                  "unit": "kWh | litre | m3 | tonne-km",
                  "amountBdt": 0.00,
                  "meterNumber": "string or null",
                  "consumerNumber": "string or null",
                  "powerFactorPenaltyBdt": 0.00,
                  "demandChargePenaltyBdt": 0.00,
                  "confidence": 0.95,
                  "reasoning": "brief explanation"
                }

                Guidelines:
                1. Identify vendor (BPDB, DESCO, DPDC, BREB, NESCO, WZPDCL, Titas Gas, Bakhrabad Gas, Karnaphuli Gas, Jalalabad Gas, Padma Oil, etc.).
                2. Extract the electricity consumed quantity (e.g. Consumed Unit / Used Units / present reading minus previous reading) and canonical unit (kWh for electricity, m3 for natural gas, litre for diesel).
                3. Extract total amount to be paid / net payable in Bangladeshi Taka (BDT) (look for Amount to be Paid, Bill Month Total, Balance, Net Payable, Total Amount).
                4. Extract bill number, meter number, consumer number, and billing period in YYYY-MM format.
                5. Return ONLY a single raw JSON object matching the schema. No markdown formatting, no code fences.
                """;

                var visionRequestBody = new
                {
                    model = _visionModel,
                    messages = new object[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = visionPrompt },
                                new { type = "image_url", image_url = new { url = dataUrl } }
                            }
                        }
                    },
                    temperature = 0.1,
                    response_format = new { type = "json_object" }
                };

                var visionRequestJson = JsonSerializer.Serialize(visionRequestBody);
                using var visionHttpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/chat/completions");
                visionHttpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                visionHttpRequest.Headers.UserAgent.ParseAdd("CarbonBill-Extractor/1.0");
                visionHttpRequest.Content = new StringContent(visionRequestJson, Encoding.UTF8, "application/json");

                var visionStartUtc = DateTime.UtcNow;
                var visionResponse = await httpClient.SendAsync(visionHttpRequest, ct);
                if (visionResponse.IsSuccessStatusCode)
                {
                    var visionResponseJson = await visionResponse.Content.ReadAsStringAsync(ct);
                    using var vDoc = JsonDocument.Parse(visionResponseJson);
                    var vContent = vDoc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    if (!string.IsNullOrWhiteSpace(vContent))
                    {
                        var parsedVision = JsonSerializer.Deserialize<GroqBillExtractionResult>(vContent, JsonOptions);
                        if (parsedVision != null && (parsedVision.Quantity > 0 || parsedVision.AmountBdt > 0 || !string.IsNullOrWhiteSpace(parsedVision.Vendor)))
                        {
                            logger.LogInformation("Groq Vision succeeded for {FileName}: Vendor={Vendor}, Qty={Qty} {Unit}, Amt={Amt} BDT",
                                fileName, parsedVision.Vendor, parsedVision.Quantity, parsedVision.Unit, parsedVision.AmountBdt);

                            if (langSmithTracer != null)
                            {
                                await langSmithTracer.TraceRunAsync(
                                    "GroqVisionBillExtraction",
                                    "llm",
                                    new
                                    {
                                        model = _visionModel,
                                        fileName,
                                        mimeType = mime,
                                        fileSizeBytes = imageBytes.Length,
                                        prompt = visionPrompt
                                    },
                                    parsedVision,
                                    visionStartUtc,
                                    DateTime.UtcNow,
                                    new Dictionary<string, object>
                                    {
                                        ["model"] = _visionModel,
                                        ["provider"] = "Groq",
                                        ["tier"] = 3,
                                        ["fileName"] = fileName,
                                        ["extractedVendor"] = parsedVision.Vendor ?? "Unknown",
                                        ["extractedQuantity"] = parsedVision.Quantity,
                                        ["extractedUnit"] = parsedVision.Unit ?? "",
                                        ["extractedAmountBdt"] = parsedVision.AmountBdt
                                    },
                                    null,
                                    ct);
                            }

                            return BuildResult(parsedVision, rawOcrText, 3);
                        }
                    }
                }
                else
                {
                    var vErr = await visionResponse.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("Groq Vision API returned HTTP {StatusCode}: {Error}. Falling back to text LLM.", visionResponse.StatusCode, vErr);

                    if (langSmithTracer != null)
                    {
                        await langSmithTracer.TraceRunAsync(
                            "GroqVisionBillExtraction",
                            "llm",
                            new { model = _visionModel, fileName, prompt = visionPrompt },
                            null,
                            visionStartUtc,
                            DateTime.UtcNow,
                            new Dictionary<string, object> { ["model"] = _visionModel, ["provider"] = "Groq", ["fileName"] = fileName },
                            $"HTTP {visionResponse.StatusCode}: {vErr}",
                            ct);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Groq Vision extraction failed for {FileName}. Continuing to text LLM.", fileName);
            }
        }

        // 2. Groq LLM Semantic Intelligence with OCR Text
        try
        {
            var systemPrompt = """
            You are a specialized carbon-accounting data extraction assistant for Bangladeshi factories.
            Given OCR text from a utility bill, diesel delivery slip, gas invoice, or transport challan:
            1. Identify the vendor (Bangladesh Power Development Board (BPDB), DESCO, DPDC, BREB, NESCO, WZPDCL, Padma Oil, Meghna Petroleum, Jamuna Oil, Titas Gas, Bakhrabad Gas, Karnaphuli Gas, Jalalabad Gas, etc.).
            2. Extract bill/challan/customer number and billing period (YYYY-MM).
            3. Normalize all Bangla numerals (১২৩৪৫৬৭৮৯০) to Latin digits.
            4. Extract the consumed quantity (energy or fuel consumption, e.g. Used Units / Consumed Unit / ব্যবহৃত ইউনিট / বর্তমান রিডিং - পূর্ববর্তী রিডিং) and canonical unit (kWh for electricity, litre for diesel/fuel, m3 for natural gas, kg or tonne-km for cargo).
            5. Extract total net/payable amount in Bangladeshi Taka (BDT) (e.g. Amount to be Paid, Bill Month Total, Net Payable, Balance, সর্বমোট প্রদেয় টাকা, মোট বিল).
            6. Identify meter number and consumer number if present.
            7. Identify any power factor penalty or demand charge surcharges if present.
            8. Return ONLY valid JSON matching this schema:
            {
              "vendor": "string",
              "billNumber": "string",
              "billingPeriod": "YYYY-MM",
              "docType": "ElectricityBill | DieselSlip | GasBill | ShippingChallan",
              "quantity": 0.00,
              "unit": "kWh | litre | m3 | tonne-km",
              "amountBdt": 0.00,
              "meterNumber": "string or null",
              "consumerNumber": "string or null",
              "powerFactorPenaltyBdt": 0.00,
              "demandChargePenaltyBdt": 0.00,
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
            httpRequest.Headers.UserAgent.ParseAdd("CarbonBill-Extractor/1.0");
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

                return await FallbackRuleBasedExtractionWithTraceAsync(rawOcrText, fileName, $"GroqHttpError_{(int)response.StatusCode}", startTimeUtc, ct);
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
                return await FallbackRuleBasedExtractionWithTraceAsync(rawOcrText, fileName, "EmptyLlmResponse", startTimeUtc, ct);
            }

            var parsed = JsonSerializer.Deserialize<GroqBillExtractionResult>(contentString, JsonOptions);
            if (parsed == null)
            {
                return await FallbackRuleBasedExtractionWithTraceAsync(rawOcrText, fileName, "JsonDeserializationFailed", startTimeUtc, ct);
            }

            // If quantity was missing from OCR text but amount exists, estimate reasonably
            if (parsed.Quantity <= 0 && parsed.AmountBdt > 0)
            {
                var estimatedQty = parsed.DocType switch
                {
                    DocumentTypes.GasBill => Math.Round(parsed.AmountBdt / 30.0m, 2),
                    DocumentTypes.ElectricityBill => Math.Round(parsed.AmountBdt / 5.5m, 2),
                    DocumentTypes.DieselSlip => Math.Round(parsed.AmountBdt / 110.0m, 2),
                    _ => 0.0m
                };
                parsed = parsed with { Quantity = estimatedQty };
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

            return BuildResult(parsed, rawOcrText, 3);
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

            return await FallbackRuleBasedExtractionWithTraceAsync(rawOcrText, fileName, $"Exception_{ex.GetType().Name}", startTimeUtc, ct);
        }
    }

    private async Task<OcrExtractionResult> FallbackRuleBasedExtractionWithTraceAsync(
        string rawText,
        string? fileName,
        string reason,
        DateTime startTimeUtc,
        CancellationToken ct)
    {
        var result = FallbackRuleBasedExtraction(rawText, fileName);
        if (langSmithTracer != null)
        {
            var vendor = result.Fields.FirstOrDefault(f => f.FieldName == "Vendor")?.RawValue;
            var qty = result.Fields.FirstOrDefault(f => f.FieldName == "Quantity")?.RawValue;
            var unit = result.Fields.FirstOrDefault(f => f.FieldName == "Unit")?.RawValue;
            var amount = result.Fields.FirstOrDefault(f => f.FieldName == "AmountBdt")?.RawValue;

            await langSmithTracer.TraceRunAsync(
                "RuleBasedSemanticParser",
                "chain",
                new { fileName, rawOcrLength = rawText.Length, reason },
                new
                {
                    detectedDocType = result.DetectedDocumentType,
                    vendor,
                    quantity = qty,
                    unit,
                    amountBdt = amount,
                    fieldsCount = result.Fields.Count
                },
                startTimeUtc,
                DateTime.UtcNow,
                new Dictionary<string, object>
                {
                    ["extractor"] = "RuleBasedSemanticParser",
                    ["reason"] = reason,
                    ["fileName"] = fileName ?? "unknown",
                    ["detectedDocType"] = result.DetectedDocumentType ?? "GeneralDocument"
                },
                null,
                ct);
        }
        return result;
    }

    private static OcrExtractionResult BuildResult(GroqBillExtractionResult parsed, string rawOcrText, int tierUsed)
    {
        var fields = new List<ExtractedFieldResult>
        {
            new("Vendor", parsed.Vendor ?? "Unknown", parsed.Vendor, parsed.Confidence, tierUsed),
            new("BillNumber", parsed.BillNumber ?? "N/A", parsed.BillNumber, parsed.Confidence, tierUsed),
            new("BillingPeriod", parsed.BillingPeriod ?? DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture), parsed.BillingPeriod, parsed.Confidence, tierUsed),
            new("Quantity", parsed.Quantity.ToString("F2", CultureInfo.InvariantCulture), parsed.Quantity.ToString("F2", CultureInfo.InvariantCulture), parsed.Confidence, tierUsed),
            new("Unit", parsed.Unit ?? "kWh", parsed.Unit, parsed.Confidence, tierUsed),
            new("AmountBdt", parsed.AmountBdt.ToString("F2", CultureInfo.InvariantCulture), parsed.AmountBdt.ToString("F2", CultureInfo.InvariantCulture), parsed.Confidence, tierUsed)
        };

        if (!string.IsNullOrWhiteSpace(parsed.MeterNumber))
        {
            fields.Add(new("MeterNumber", parsed.MeterNumber, parsed.MeterNumber, parsed.Confidence, tierUsed));
        }

        if (!string.IsNullOrWhiteSpace(parsed.ConsumerNumber))
        {
            fields.Add(new("ConsumerNumber", parsed.ConsumerNumber, parsed.ConsumerNumber, parsed.Confidence, tierUsed));
        }

        if (parsed.PowerFactorPenaltyBdt.HasValue && parsed.PowerFactorPenaltyBdt.Value > 0)
        {
            fields.Add(new("PowerFactorPenaltyBdt", parsed.PowerFactorPenaltyBdt.Value.ToString("F2", CultureInfo.InvariantCulture), parsed.PowerFactorPenaltyBdt.Value.ToString("F2", CultureInfo.InvariantCulture), parsed.Confidence, tierUsed));
        }

        if (parsed.DemandChargePenaltyBdt.HasValue && parsed.DemandChargePenaltyBdt.Value > 0)
        {
            fields.Add(new("DemandChargePenaltyBdt", parsed.DemandChargePenaltyBdt.Value.ToString("F2", CultureInfo.InvariantCulture), parsed.DemandChargePenaltyBdt.Value.ToString("F2", CultureInfo.InvariantCulture), parsed.Confidence, tierUsed));
        }

        if (!string.IsNullOrWhiteSpace(rawOcrText))
        {
            fields.Add(new("ExtractedText", rawOcrText, rawOcrText, parsed.Confidence, tierUsed));
        }

        return new OcrExtractionResult(
            Success: true,
            DetectedDocumentType: parsed.DocType,
            Fields: fields,
            ErrorMessage: null,
            TierUsed: tierUsed);
    }

    public static OcrExtractionResult FallbackRuleBasedExtraction(string rawText, string? fileName = null)
    {
        var normalized = BanglaNormalizer.NormalizeDigits(rawText);
        var classification = DocumentClassifier.Classify(normalized, fileName);
        var fields = new List<ExtractedFieldResult>();

        var docType = classification.DocumentType != DocumentTypes.Unknown
            ? classification.DocumentType
            : "GeneralDocument";

        // 1. Extract Vendor Name across all Bangladeshi utility providers
        string? extractedVendor = null;
        if (normalized.Contains("POWER DEVELOPMENT BOARD", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("BPDB", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("পিডিবি", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Bangladesh Power Development Board (BPDB)";
            docType = DocumentTypes.ElectricityBill;
        }
        else if (normalized.Contains("RURAL ELECTRIFICATION", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("BREB", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("PALLI BIDYUT", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("পল্লী বিদ্যুৎ", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Bangladesh Rural Electrification Board (BREB)";
            docType = DocumentTypes.ElectricityBill;
        }
        else if (normalized.Contains("DESCO", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("ডেসকো", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Dhaka Electric Supply Company (DESCO)";
            docType = DocumentTypes.ElectricityBill;
        }
        else if (normalized.Contains("DPDC", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("ডিপিডিসি", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Dhaka Power Distribution Company (DPDC)";
            docType = DocumentTypes.ElectricityBill;
        }
        else if (normalized.Contains("NESCO", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("নেসকো", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Northern Electricity Supply Company (NESCO)";
            docType = DocumentTypes.ElectricityBill;
        }
        else if (normalized.Contains("WZPDCL", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("WEST ZONE POWER", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "West Zone Power Distribution Company (WZPDCL)";
            docType = DocumentTypes.ElectricityBill;
        }
        else if (normalized.Contains("TITAS", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("তিতাস", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Titas Gas Transmission and Distribution Company Ltd";
            docType = DocumentTypes.GasBill;
        }
        else if (normalized.Contains("BAKHRABAD", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("বাখরাবাদ", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Bakhrabad Gas Distribution Company Ltd";
            docType = DocumentTypes.GasBill;
        }
        else if (normalized.Contains("KARNAPHULI", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("KGDCL", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("কর্ণফুলী", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Karnaphuli Gas Distribution Company Ltd";
            docType = DocumentTypes.GasBill;
        }
        else if (normalized.Contains("JALALABAD", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("জালালাবাদ", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Jalalabad Gas Transmission and Distribution System Ltd";
            docType = DocumentTypes.GasBill;
        }
        else if (normalized.Contains("PASCHIMANCHAL", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("PGCL", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Paschimanchal Gas Company Ltd";
            docType = DocumentTypes.GasBill;
        }
        else if (normalized.Contains("SUNDARBAN", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("SGCL", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Sundarban Gas Company Ltd";
            docType = DocumentTypes.GasBill;
        }
        else if (normalized.Contains("PADMA OIL", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("PADMA", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("পদ্মা", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Padma Oil Company Ltd";
            docType = DocumentTypes.DieselSlip;
        }
        else if (normalized.Contains("MEGHNA", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("মেঘনা", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Meghna Petroleum Ltd";
            docType = DocumentTypes.DieselSlip;
        }
        else if (normalized.Contains("JAMUNA", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("যমুনা", StringComparison.OrdinalIgnoreCase))
        {
            extractedVendor = "Jamuna Oil Company Ltd";
            docType = DocumentTypes.DieselSlip;
        }

        // 2. Extract Amount in BDT
        decimal? extractedAmount = null;
        var amountPatterns = new[]
        {
            @"(?:AMOUNT\s*TO\s*BE\s*PAID|TOTAL\s*AMOUNT\s*TO\s*BE\s*PA(?:ID|Y)|NET\s*PAYABLE|TOTAL\s*PAYABLE|সর্বমোট\s*প্রদেয়|মোট\s*প্রদেয়|প্রদেয়\s*টাকা)[:\s]*(?:BDT|Tk|TAKA)?[:\s]*([0-9,]+(?:\.[0-9]{1,2})?)",
            @"(?:BILL\s*MONTH\s*TOTAL|MONTH\s*TOTAL|মাসিক\s*বিল)[:\s]*(?:BDT|Tk|TAKA)?[:\s]*([0-9,]+(?:\.[0-9]{1,2})?)",
            @"(?:Balance|বকেয়া|জের)[:\s]*(?:BDT|Tk|TAKA)?[:\s]*([0-9,]+(?:\.[0-9]{1,2})?)",
            @"(?:PRINCIPAL\s*AMOUNT|মূল\s*টাকা)[:\s]*(?:BDT|Tk|TAKA)?[:\s]*([0-9,]+(?:\.[0-9]{1,2})?)",
            @"(?:CURRENT\s*CHARGES|ELECTRICITY\s*CHARGE|বিদ্যুৎ\s*চার্জ|গ্যাস\s*বিল)[:\s]*(?:BDT|Tk|TAKA)?[:\s]*([0-9,]+(?:\.[0-9]{1,2})?)",
            @"(?:Paid to Organization|Payment Received|Bill Amount|Net Payable|Amount|প্রদেয়|টাকা)[:\s]*(?:BDT|Tk|TAKA)?[:\s]*([0-9,]+(?:\.[0-9]{1,2})?)",
            @"(?:BDT|TAKA|TK)\.?\s*([0-9,]+(?:\.[0-9]{1,2})?)"
        };

        foreach (var pattern in amountPatterns)
        {
            var match = Regex.Match(normalized, pattern, RegexOptions.IgnoreCase);
            if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var amt))
            {
                // Discard numbers that are actually 8-digit bill/meter numbers or current years
                if (amt > 0 && amt != 2024 && amt != 2025 && amt != 2026 && amt < 50000000m)
                {
                    extractedAmount = amt;
                    break;
                }
            }
        }

        // 3. Extract Billing Period / Month
        string? extractedPeriod = null;
        var monthYearRegex = new Regex(
            @"(?:(?:Bill Month|Month|Period|মাস)[:\s]*)?\b(January|February|March|April|May|June|July|August|September|October|November|December),?\s*([0-9]{4})\b",
            RegexOptions.IgnoreCase);
        var monthMatch = monthYearRegex.Match(normalized);
        if (monthMatch.Success)
        {
            var monthName = monthMatch.Groups[1].Value;
            var yearStr = monthMatch.Groups[2].Value;
            if (DateTime.TryParseExact($"{monthName} 1, {yearStr}", "MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                extractedPeriod = dt.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            }
        }

        if (extractedPeriod == null)
        {
            // Format: May-24 or Mar24 or May/2024
            var shortMonthRegex = new Regex(@"\b(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*[-/\s]*([0-9]{2,4})\b", RegexOptions.IgnoreCase);
            var smMatch = shortMonthRegex.Match(normalized);
            if (smMatch.Success)
            {
                var mStr = smMatch.Groups[1].Value;
                var yStr = smMatch.Groups[2].Value;
                if (yStr.Length == 2) yStr = "20" + yStr;
                if (DateTime.TryParseExact($"{mStr} 1, {yStr}", "MMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    extractedPeriod = dt.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                }
            }
        }

        if (extractedPeriod == null)
        {
            var isoPeriod = new Regex(@"\b(202[0-9])[-/](0[1-9]|1[0-2])\b").Match(normalized);
            if (isoPeriod.Success) extractedPeriod = $"{isoPeriod.Groups[1].Value}-{isoPeriod.Groups[2].Value}";
        }

        // 4. Extract Bill / Account / Transaction Number
        string? extractedBillNo = null;
        var accMatch = new Regex(
            @"(?:Bill Account Number|Account Number|Transaction ID|Bill No|Challan No)[:\s]*([A-Za-z0-9-]+)",
            RegexOptions.IgnoreCase).Match(normalized);
        if (accMatch.Success)
        {
            extractedBillNo = accMatch.Groups[1].Value;
        }
        if (string.IsNullOrWhiteSpace(extractedBillNo) || extractedBillNo.Length < 4)
        {
            var rawAcc = new Regex(@"\b1\s*(7[0-9]{7})\b").Match(normalized);
            if (rawAcc.Success)
            {
                extractedBillNo = "1" + rawAcc.Groups[1].Value;
            }
            else
            {
                var txnMatch = new Regex(@"\b([0-9]{8,10})\b").Match(normalized);
                if (txnMatch.Success)
                {
                    extractedBillNo = txnMatch.Groups[1].Value;
                }
            }
        }

        // Extract Consumer Number and Meter Number
        string? extractedConsumerNo = null;
        var consumerMatch = new Regex(@"(?:Consumer No|Consumer Number|গ্রাহক নং)[:\s]*([0-9A-Za-z-]+)", RegexOptions.IgnoreCase).Match(normalized);
        if (consumerMatch.Success) extractedConsumerNo = consumerMatch.Groups[1].Value;

        string? extractedMeterNo = null;
        var meterMatch = new Regex(@"(?:Meter No|Meter Number|মিটার নং)[:\s]*([0-9A-Za-z-]+)", RegexOptions.IgnoreCase).Match(normalized);
        if (meterMatch.Success) extractedMeterNo = meterMatch.Groups[1].Value;

        // 5. Quantity & Canonical Unit
        string unit = docType == DocumentTypes.GasBill ? "m3" : (docType == DocumentTypes.ElectricityBill ? "kWh" : (docType == DocumentTypes.DieselSlip ? "litre" : "unit"));
        decimal? extractedQty = null;

        // Try direct regex with unit suffix
        var qtyMatch = new Regex(@"([0-9,]+(?:\.[0-9]+)?)\s*(?:kWh|k\.w\.h|kwh|m3|ঘনমিটার|Litre|লিটার|Units?|ইউনিট)\b", RegexOptions.IgnoreCase).Match(normalized);
        if (qtyMatch.Success && decimal.TryParse(qtyMatch.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var q))
        {
            extractedQty = q;
        }

        // Try "Consumed Unit" or "Used Units" pattern
        if (!extractedQty.HasValue)
        {
            var consumedMatch = new Regex(@"(?:CONSUMED\s*UNIT|USED\s*UNITS?|UNITS?\s*CONSUMED|ব্যবহৃত\s*ইউনিট)[:\s]*([0-9,]+(?:\.[0-9]+)?)", RegexOptions.IgnoreCase).Match(normalized);
            if (consumedMatch.Success && decimal.TryParse(consumedMatch.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var cUnits))
            {
                extractedQty = cUnits;
            }
        }

        // Try meter reading difference: PRESENT RDG minus PREVIOUS RDG
        if (!extractedQty.HasValue)
        {
            var presMatch = new Regex(@"PRESENT\s*R(?:EA?D(?:IN)?G|DG|OG)?[:\s]*([0-9]+)", RegexOptions.IgnoreCase).Match(normalized);
            var prevMatch = new Regex(@"PREVIOUS\s*R(?:EA?D(?:IN)?G|DG|OG)?[:\s]*([0-9]+)", RegexOptions.IgnoreCase).Match(normalized);
            if (presMatch.Success && prevMatch.Success &&
                long.TryParse(presMatch.Groups[1].Value, out var pres) &&
                long.TryParse(prevMatch.Groups[1].Value, out var prev) &&
                pres >= prev)
            {
                var diff = pres - prev;
                if (diff > 0 && diff < 500000)
                {
                    extractedQty = diff;
                }
            }
        }

        // If quantity is still not found, derive based on the actual extracted bill amount and standard utility tariffs
        if (!extractedQty.HasValue && extractedAmount.HasValue && extractedAmount.Value > 0)
        {
            if (docType == DocumentTypes.GasBill) extractedQty = Math.Round(extractedAmount.Value / 30.0m, 2);
            else if (docType == DocumentTypes.ElectricityBill) extractedQty = Math.Round(extractedAmount.Value / 5.5m, 2);
            else if (docType == DocumentTypes.DieselSlip) extractedQty = Math.Round(extractedAmount.Value / 110.0m, 2);
        }

        // Populate fields based on extracted or fallback values
        var finalVendor = extractedVendor ?? (docType == DocumentTypes.ElectricityBill ? "Bangladesh Power Development Board (BPDB)" : (docType == DocumentTypes.DieselSlip ? "Padma Oil Company Ltd" : (docType == DocumentTypes.GasBill ? "Titas Gas" : "Detected via OCR")));
        var finalAmount = extractedAmount ?? 0.00m;
        var finalPeriod = extractedPeriod ?? DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var finalQty = extractedQty ?? 0.00m;
        var finalBillNo = extractedBillNo ?? "N/A";

        fields.Add(new("Vendor", finalVendor, finalVendor, 0.95f, 1));
        fields.Add(new("BillNumber", finalBillNo, finalBillNo, 0.92f, 1));
        fields.Add(new("BillingPeriod", finalPeriod, finalPeriod, 0.94f, 1));
        fields.Add(new("Quantity", finalQty.ToString("F2", CultureInfo.InvariantCulture), finalQty.ToString("F2", CultureInfo.InvariantCulture), 0.91f, 1));
        fields.Add(new("Unit", unit, unit, 0.96f, 1));
        fields.Add(new("AmountBdt", finalAmount.ToString("F2", CultureInfo.InvariantCulture), finalAmount.ToString("F2", CultureInfo.InvariantCulture), 0.95f, 1));

        if (!string.IsNullOrWhiteSpace(extractedMeterNo))
        {
            fields.Add(new("MeterNumber", extractedMeterNo, extractedMeterNo, 0.90f, 1));
        }

        if (!string.IsNullOrWhiteSpace(extractedConsumerNo))
        {
            fields.Add(new("ConsumerNumber", extractedConsumerNo, extractedConsumerNo, 0.90f, 1));
        }

        fields.Add(new("ExtractedText", normalized, normalized, 0.90f, 1));

        return new OcrExtractionResult(
            Success: true,
            DetectedDocumentType: docType,
            Fields: fields,
            ErrorMessage: null,
            TierUsed: 1);
    }
}
