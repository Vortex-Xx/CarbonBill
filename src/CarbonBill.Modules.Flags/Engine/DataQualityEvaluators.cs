using System.Globalization;
using System.Text.Json;
using CarbonBill.Modules.Flags.Contracts;
using CarbonBill.Modules.Flags.Domain;
using CarbonBill.SharedKernel.Contracts;

namespace CarbonBill.Modules.Flags.Engine;

public static class FlagFormatters
{
    public static string ToBanglaDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace('0', '০')
            .Replace('1', '১')
            .Replace('2', '২')
            .Replace('3', '৩')
            .Replace('4', '৪')
            .Replace('5', '৫')
            .Replace('6', '৬')
            .Replace('7', '৭')
            .Replace('8', '৮')
            .Replace('9', '৯');
    }
}

public class MissingDocumentRuleEvaluator(
    IExpectedDocRuleReader expectedDocRuleReader,
    IDocumentReadModel documentReadModel) : IFlagRuleEvaluator
{
    private readonly IExpectedDocRuleReader _expectedDocRuleReader = expectedDocRuleReader;
    private readonly IDocumentReadModel _documentReadModel = documentReadModel;

    public string RuleCode => "MISSING_DOCUMENT";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        var expectedRules = await _expectedDocRuleReader.GetRulesForOrgAsync(context.OrgId, ct);
        if (expectedRules.Count == 0)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        // Parse trigger parameters
        int escalateRedAfterDays = 5;
        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("escalate_red_after_days", out var prop))
                {
                    escalateRedAfterDays = prop.GetInt32();
                }
            }
            catch (JsonException) { }
        }

        var missingDocs = new List<object>();
        var now = DateTime.UtcNow;
        bool hasRedSeverity = false;
        string? firstAssetName = null;
        string? firstDocType = null;

        foreach (var expected in expectedRules)
        {
            var hasDoc = await _documentReadModel.HasDocumentAsync(context.OrgId, expected.AssetId, expected.DocType, context.Period, ct);
            if (!hasDoc)
            {
                // Calculate due date (standard 10th of following month)
                var dueDay = expected.DueDayOfMonth > 0 ? expected.DueDayOfMonth : 10;
                var dueDate = DateTime.UtcNow.Date; // Baseline
                if (DateTime.TryParseExact(context.Period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var pDate))
                {
                    dueDate = pDate.AddMonths(1).Date.AddDays(dueDay - 1);
                }

                if (now >= dueDate)
                {
                    var daysOverdue = (now - dueDate).TotalDays;
                    var isRed = daysOverdue >= escalateRedAfterDays;
                    if (isRed) hasRedSeverity = true;

                    firstAssetName ??= expected.AssetName;
                    firstDocType ??= expected.DocType;

                    missingDocs.Add(new
                    {
                        assetId = expected.AssetId,
                        assetName = expected.AssetName,
                        docType = expected.DocType,
                        dueDate = dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        daysOverdue = (int)daysOverdue,
                        isRed
                    });
                }
            }
        }

        if (missingDocs.Count == 0)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        var severity = hasRedSeverity ? FlagSeverities.Red : FlagSeverities.Amber;
        var evidenceJson = JsonSerializer.Serialize(new { missingDocuments = missingDocs });

        var countStrBn = FlagFormatters.ToBanglaDigits(missingDocs.Count.ToString(CultureInfo.InvariantCulture));
        var explanationBn = $"{firstAssetName ?? "অ্যাসেট"}-এর {firstDocType ?? "চালান"} জমা দেওয়া হয়নি ({context.Period} মেয়াদের জন্য)। মোট {countStrBn}টি অনুপস্থিত চালান সনাক্ত হয়েছে।";
        var explanationEn = $"Missing {firstDocType ?? "document"} for {firstAssetName ?? "asset"} in period {context.Period}. Total {missingDocs.Count} expected slips are unsubmitted.";

        return new EvaluationResult(
            ShouldFlag: true,
            SeverityOverride: severity,
            EvidenceJson: evidenceJson,
            ExplanationBn: explanationBn,
            ExplanationEn: explanationEn);
    }
}

public class LowOcrConfidenceRuleEvaluator(IFlagDocumentReadModel documentReadModel) : IFlagRuleEvaluator
{
    private readonly IFlagDocumentReadModel _documentReadModel = documentReadModel;

    public string RuleCode => "LOW_OCR_CONFIDENCE";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        float threshold = 0.70f;
        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("confidence_threshold", out var prop))
                {
                    threshold = prop.GetSingle();
                }
            }
            catch (JsonException) { }
        }

        var docs = await _documentReadModel.GetExtractedDocumentsAsync(context.OrgId, context.Period, ct);
        var lowConfidenceItems = new List<object>();

        foreach (var d in docs)
        {
            if (d.MinFieldConfidence < threshold)
            {
                var suspiciousFields = d.Fields.Where(f => f.Confidence < threshold).Select(f => new
                {
                    fieldName = f.FieldName,
                    value = f.Value,
                    confidence = f.Confidence
                }).ToList();

                lowConfidenceItems.Add(new
                {
                    documentId = d.DocumentId,
                    docType = d.DocType,
                    minConfidence = d.MinFieldConfidence,
                    threshold,
                    suspiciousFields
                });
            }
        }

        if (lowConfidenceItems.Count == 0)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        var evidenceJson = JsonSerializer.Serialize(new { lowConfidenceDocuments = lowConfidenceItems });
        var threshStrBn = FlagFormatters.ToBanglaDigits((threshold * 100).ToString("F0", CultureInfo.InvariantCulture));
        var countStrBn = FlagFormatters.ToBanglaDigits(lowConfidenceItems.Count.ToString(CultureInfo.InvariantCulture));
        var explanationBn = $"{countStrBn}টি নথিতে কম ওলসিআর কনফিডেন্স পাওয়া গেছে ({threshStrBn}%-এর নিচে)। ফিল্ডগুলো ম্যানুয়াল রিভিউ প্রয়োজন।";
        var explanationEn = $"Low OCR confidence (<{threshold * 100:F0}%) detected on {lowConfidenceItems.Count} document(s). Fields require manual verification.";

        return new EvaluationResult(
            ShouldFlag: true,
            SeverityOverride: FlagSeverities.Amber,
            EvidenceJson: evidenceJson,
            ExplanationBn: explanationBn,
            ExplanationEn: explanationEn);
    }
}

public class DuplicateSuspectedRuleEvaluator(IFlagDocumentReadModel documentReadModel) : IFlagRuleEvaluator
{
    private readonly IFlagDocumentReadModel _documentReadModel = documentReadModel;

    public string RuleCode => "DUPLICATE_SUSPECTED";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        bool matchHash = true;
        bool matchVendorBillPeriod = true;

        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("match_hash", out var propHash))
                    matchHash = propHash.GetBoolean();
                if (doc.RootElement.TryGetProperty("match_vendor_bill_period", out var propBill))
                    matchVendorBillPeriod = propBill.GetBoolean();
            }
            catch (JsonException) { }
        }

        var docs = await _documentReadModel.GetExtractedDocumentsAsync(context.OrgId, context.Period, ct);
        var duplicatePairs = new List<object>();

        for (int i = 0; i < docs.Count; i++)
        {
            for (int j = i + 1; j < docs.Count; j++)
            {
                var a = docs[i];
                var b = docs[j];

                bool isDuplicate = false;
                string matchType = "";

                if (matchHash && !string.IsNullOrEmpty(a.FileHash) && a.FileHash.Equals(b.FileHash, StringComparison.OrdinalIgnoreCase))
                {
                    isDuplicate = true;
                    matchType = "Matching File Hash";
                }
                else if (matchVendorBillPeriod &&
                         !string.IsNullOrWhiteSpace(a.VendorName) &&
                         !string.IsNullOrWhiteSpace(a.BillNumber) &&
                         a.VendorName.Equals(b.VendorName, StringComparison.OrdinalIgnoreCase) &&
                         a.BillNumber.Equals(b.BillNumber, StringComparison.OrdinalIgnoreCase) &&
                         a.BillingPeriod.Equals(b.BillingPeriod, StringComparison.OrdinalIgnoreCase))
                {
                    isDuplicate = true;
                    matchType = $"Matching Vendor ({a.VendorName}) and Bill Number ({a.BillNumber})";
                }

                if (isDuplicate)
                {
                    duplicatePairs.Add(new
                    {
                        documentA = a.DocumentId,
                        documentB = b.DocumentId,
                        matchType,
                        fileHash = a.FileHash,
                        vendor = a.VendorName,
                        billNumber = a.BillNumber
                    });
                }
            }
        }

        if (duplicatePairs.Count == 0)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        var evidenceJson = JsonSerializer.Serialize(new { duplicates = duplicatePairs });
        var countStrBn = FlagFormatters.ToBanglaDigits(duplicatePairs.Count.ToString(CultureInfo.InvariantCulture));
        var explanationBn = $"একই সময়ে একাধিক অভিন্ন চালান বা ডুপ্লিকেট ফাইল পাওয়া গেছে ({countStrBn}টি সম্ভাব্য ডুপ্লিকেট সনাক্ত)।";
        var explanationEn = $"Suspected duplicate document(s) detected ({duplicatePairs.Count} pair(s) with identical hash or vendor bill details).";

        return new EvaluationResult(
            ShouldFlag: true,
            SeverityOverride: FlagSeverities.Amber,
            EvidenceJson: evidenceJson,
            ExplanationBn: explanationBn,
            ExplanationEn: explanationEn);
    }
}

public class ImplausibleValueRuleEvaluator(IFlagDocumentReadModel documentReadModel) : IFlagRuleEvaluator
{
    private readonly IFlagDocumentReadModel _documentReadModel = documentReadModel;

    public string RuleCode => "IMPLAUSIBLE_VALUE";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        double iqrMultiplier = 3.0;
        int minHistoryCount = 3;

        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("iqr_multiplier", out var propIqr))
                    iqrMultiplier = propIqr.GetDouble();
                if (doc.RootElement.TryGetProperty("min_history_count", out var propMin))
                    minHistoryCount = propMin.GetInt32();
            }
            catch (JsonException) { }
        }

        var docs = await _documentReadModel.GetExtractedDocumentsAsync(context.OrgId, context.Period, ct);
        var implausibleItems = new List<object>();

        foreach (var doc in docs)
        {
            if (!doc.AssetId.HasValue) continue;

            var history = await _documentReadModel.GetHistoricalConsumptionAsync(context.OrgId, doc.AssetId.Value, ct);
            if (history == null) continue;

            // 1. Check unit mismatch
            if (!string.IsNullOrEmpty(history.Unit) && !doc.Unit.Equals(history.Unit, StringComparison.OrdinalIgnoreCase))
            {
                implausibleItems.Add(new
                {
                    documentId = doc.DocumentId,
                    assetId = doc.AssetId.Value,
                    assetName = doc.AssetName ?? history.AssetName,
                    reason = "UnitMismatch",
                    docUnit = doc.Unit,
                    expectedUnit = history.Unit,
                    docQuantity = doc.Quantity
                });
                continue;
            }

            // 2. Check 3 x IQR outlier
            if (history.PastQuantities.Count >= minHistoryCount)
            {
                var sorted = history.PastQuantities.OrderBy(x => x).ToList();
                var (q1, q3) = CalculateQuartiles(sorted);
                var iqr = q3 - q1;
                var margin = (decimal)iqrMultiplier * iqr;
                var lowerBound = Math.Max(0m, q1 - margin);
                var upperBound = q3 + margin;

                if (doc.Quantity < lowerBound || doc.Quantity > upperBound)
                {
                    implausibleItems.Add(new
                    {
                        documentId = doc.DocumentId,
                        assetId = doc.AssetId.Value,
                        assetName = doc.AssetName ?? history.AssetName,
                        reason = "Outside3xIQR",
                        quantity = doc.Quantity,
                        unit = doc.Unit,
                        q1,
                        q3,
                        iqr,
                        lowerBound,
                        upperBound,
                        iqrMultiplier
                    });
                }
            }
        }

        if (implausibleItems.Count == 0)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        var evidenceJson = JsonSerializer.Serialize(new { implausibleValues = implausibleItems });
        var countStrBn = FlagFormatters.ToBanglaDigits(implausibleItems.Count.ToString(CultureInfo.InvariantCulture));
        var explanationBn = $"{countStrBn}টি চালানে অস্বাভাবিক পরিমাপ বা একক অমিল পাওয়া গেছে (ঐতিহাসিক ৩x IQR সীমার বাইরে)।";
        var explanationEn = $"Implausible consumption quantity or unit mismatch detected on {implausibleItems.Count} document(s) outside historical 3x IQR bounds.";

        return new EvaluationResult(
            ShouldFlag: true,
            SeverityOverride: FlagSeverities.Amber,
            EvidenceJson: evidenceJson,
            ExplanationBn: explanationBn,
            ExplanationEn: explanationEn);
    }

    private static (decimal Q1, decimal Q3) CalculateQuartiles(List<decimal> sorted)
    {
        int n = sorted.Count;
        if (n == 1) return (sorted[0], sorted[0]);

        decimal GetPercentile(double percentile)
        {
            double pos = percentile * (n - 1);
            int baseIdx = (int)pos;
            decimal frac = (decimal)(pos - baseIdx);

            if (baseIdx + 1 < n)
            {
                return sorted[baseIdx] + frac * (sorted[baseIdx + 1] - sorted[baseIdx]);
            }
            return sorted[baseIdx];
        }

        return (GetPercentile(0.25), GetPercentile(0.75));
    }
}

public class EstimatedShareHighRuleEvaluator(IFlagEmissionReadModel emissionReadModel) : IFlagRuleEvaluator
{
    private readonly IFlagEmissionReadModel _emissionReadModel = emissionReadModel;

    public string RuleCode => "ESTIMATED_SHARE_HIGH";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        decimal maxPercentage = 10.0m;
        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("max_estimated_percentage", out var prop))
                {
                    maxPercentage = prop.GetDecimal();
                }
            }
            catch (JsonException) { }
        }

        var summary = await _emissionReadModel.GetPeriodEmissionSummaryAsync(context.OrgId, context.Period, ct);
        if (summary == null || summary.TotalCo2eKg <= 0m)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        var estimatedShare = (summary.EstimatedCo2eKg / summary.TotalCo2eKg) * 100m;
        if (estimatedShare <= maxPercentage)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        var evidenceJson = JsonSerializer.Serialize(new
        {
            totalCo2eKg = summary.TotalCo2eKg,
            estimatedCo2eKg = summary.EstimatedCo2eKg,
            estimatedSharePercentage = Math.Round(estimatedShare, 2),
            thresholdPercentage = maxPercentage
        });

        var shareStrBn = FlagFormatters.ToBanglaDigits(estimatedShare.ToString("F1", CultureInfo.InvariantCulture));
        var maxStrBn = FlagFormatters.ToBanglaDigits(maxPercentage.ToString("F0", CultureInfo.InvariantCulture));
        var explanationBn = $"{context.Period} মেয়াদে মোট কার্বনের {shareStrBn}% অনুমাননির্ভর, যা নির্ধারিত সর্বোচ্চ {maxStrBn}%-এর বেশি।";
        var explanationEn = $"Estimated emissions account for {estimatedShare:F1}% of carbon footprint in {context.Period}, exceeding allowable {maxPercentage:F0}% threshold.";

        return new EvaluationResult(
            ShouldFlag: true,
            SeverityOverride: FlagSeverities.Amber,
            EvidenceJson: evidenceJson,
            ExplanationBn: explanationBn,
            ExplanationEn: explanationEn);
    }
}
