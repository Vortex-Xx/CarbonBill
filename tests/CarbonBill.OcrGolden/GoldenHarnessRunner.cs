using System.Globalization;
using System.Text;
using System.Text.Json;
using CarbonBill.Modules.Extraction.Services.Classification;
using CarbonBill.Modules.Extraction.Services.Normalization;

namespace CarbonBill.OcrGolden;

public static class GoldenHarnessRunner
{
    public static GoldenHarnessReport EvaluateDataset(
        IReadOnlyList<GoldenBillAnnotation> dataset,
        Func<GoldenBillAnnotation, (string Vendor, string BillNo, string Period, decimal Qty, string Unit, decimal Amount, float Conf, int Tier)> extractorFunc)
    {
        var docEvals = new List<GoldenDocumentEvaluation>();
        var fieldMatches = new Dictionary<string, (int Matched, int Total)>
        {
            ["Vendor"] = (0, 0),
            ["BillNumber"] = (0, 0),
            ["BillingPeriod"] = (0, 0),
            ["Quantity"] = (0, 0),
            ["Unit"] = (0, 0),
            ["AmountBdt"] = (0, 0)
        };

        int tier1 = 0, tier2 = 0, tier3 = 0;
        int needsReviewCount = 0;

        foreach (var item in dataset)
        {
            var actual = extractorFunc(item);

            if (actual.Tier == 1) tier1++;
            else if (actual.Tier == 2) tier2++;
            else tier3++;

            var fieldEvals = new List<GoldenFieldEvaluation>();

            // 1. Vendor
            bool vMatch = string.Equals(actual.Vendor.Trim(), item.ExpectedVendor.Trim(), StringComparison.OrdinalIgnoreCase);
            RecordField("Vendor", item.ExpectedVendor, actual.Vendor, vMatch, actual.Conf, fieldEvals, fieldMatches);

            // 2. BillNumber
            bool bMatch = string.Equals(actual.BillNo.Trim(), item.ExpectedBillNumber.Trim(), StringComparison.OrdinalIgnoreCase);
            RecordField("BillNumber", item.ExpectedBillNumber, actual.BillNo, bMatch, actual.Conf, fieldEvals, fieldMatches);

            // 3. BillingPeriod
            bool pMatch = string.Equals(actual.Period.Trim(), item.ExpectedBillingPeriod.Trim(), StringComparison.OrdinalIgnoreCase);
            RecordField("BillingPeriod", item.ExpectedBillingPeriod, actual.Period, pMatch, actual.Conf, fieldEvals, fieldMatches);

            // 4. Quantity (allow +/- 0.01 tolerance)
            bool qMatch = Math.Abs(actual.Qty - item.ExpectedQuantity) < 0.01m;
            RecordField("Quantity", item.ExpectedQuantity.ToString("F2", CultureInfo.InvariantCulture), actual.Qty.ToString("F2", CultureInfo.InvariantCulture), qMatch, actual.Conf, fieldEvals, fieldMatches);

            // 5. Unit
            var normExpectedUnit = BanglaNormalizer.NormalizeUnit(item.ExpectedUnit);
            var normActualUnit = BanglaNormalizer.NormalizeUnit(actual.Unit);
            bool uMatch = string.Equals(normActualUnit, normExpectedUnit, StringComparison.OrdinalIgnoreCase);
            RecordField("Unit", normExpectedUnit, normActualUnit, uMatch, actual.Conf, fieldEvals, fieldMatches);

            // 6. AmountBdt (allow +/- 0.50 tolerance)
            bool aMatch = Math.Abs(actual.Amount - item.ExpectedAmountBdt) < 0.50m;
            RecordField("AmountBdt", item.ExpectedAmountBdt.ToString("F2", CultureInfo.InvariantCulture), actual.Amount.ToString("F2", CultureInfo.InvariantCulture), aMatch, actual.Conf, fieldEvals, fieldMatches);

            bool needsReview = actual.Conf < 0.85f || fieldEvals.Any(f => !f.IsMatch);
            if (needsReview) needsReviewCount++;

            docEvals.Add(new GoldenDocumentEvaluation(
                item.Id,
                item.FileName,
                item.Category,
                actual.Tier,
                needsReview,
                actual.Conf,
                fieldEvals));
        }

        int totalFields = fieldMatches.Values.Sum(v => v.Total);
        int matchedFields = fieldMatches.Values.Sum(v => v.Matched);
        double overallAcc = totalFields > 0 ? (double)matchedFields / totalFields * 100.0 : 0.0;
        double reviewPct = dataset.Count > 0 ? (double)needsReviewCount / dataset.Count * 100.0 : 0.0;

        var perField = fieldMatches.ToDictionary(
            k => k.Key,
            v => v.Value.Total > 0 ? (double)v.Value.Matched / v.Value.Total * 100.0 : 0.0);

        return new GoldenHarnessReport(
            dataset.Count,
            tier1,
            tier2,
            tier3,
            overallAcc,
            reviewPct,
            perField,
            docEvals,
            DateTime.UtcNow);
    }

    public static string GenerateMarkdownReport(GoldenHarnessReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# CarbonBill Golden OCR Accuracy Benchmark Report");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Generated At**: {report.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Total Test Documents**: {report.TotalDocuments}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Overall Field Extraction Accuracy**: {report.OverallAccuracyPercentage.ToString("F1", CultureInfo.InvariantCulture)}%");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- **Documents Needing Human Review**: {report.NeedsCorrectionPercentage.ToString("F1", CultureInfo.InvariantCulture)}%");
        sb.AppendLine();
        sb.AppendLine("## Tier Usage Distribution");
        sb.AppendLine("| Tier | Engine / Provider | Count | Percentage |");
        sb.AppendLine("|---|---|---|---|");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Tier 1 | Tesseract 5 / Rule Engine | {report.Tier1Count} | {(report.TotalDocuments > 0 ? (report.Tier1Count * 100.0 / report.TotalDocuments).ToString("F1", CultureInfo.InvariantCulture) : "0")}% |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Tier 2 | PaddleOCR High-Contrast Mobile | {report.Tier2Count} | {(report.TotalDocuments > 0 ? (report.Tier2Count * 100.0 / report.TotalDocuments).ToString("F1", CultureInfo.InvariantCulture) : "0")}% |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Tier 3 | Groq LLM (openai/gpt-oss-120b) | {report.Tier3Count} | {(report.TotalDocuments > 0 ? (report.Tier3Count * 100.0 / report.TotalDocuments).ToString("F1", CultureInfo.InvariantCulture) : "0")}% |");
        sb.AppendLine();
        sb.AppendLine("## Per-Field Accuracy");
        sb.AppendLine("| Field | Accuracy % | Status |");
        sb.AppendLine("|---|---|---|");
        foreach (var kvp in report.AccuracyPerField)
        {
            var badge = kvp.Value >= 90.0 ? "PASS" : "WARN";
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {kvp.Key} | {kvp.Value.ToString("F1", CultureInfo.InvariantCulture)}% | {badge} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Document Level Evaluations");
        sb.AppendLine("| ID | Category | File Name | Tier | Confidence | Review Needed |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var d in report.DocumentEvaluations)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {d.Id} | {d.Category} | {d.FileName} | {d.TierUsed} | {d.OverallConfidence.ToString("F2", CultureInfo.InvariantCulture)} | {(d.NeedsHumanReview ? "Yes" : "No")} |");
        }

        return sb.ToString();
    }

    public static IReadOnlyList<GoldenBillAnnotation> Generate150Dataset()
    {
        var list = new List<GoldenBillAnnotation>();
        int idCounter = 1;

        // 1. 50 Electricity bills
        string[] elecVendors = ["DESCO", "DPDC", "BREB", "WZPDCL", "NESCO"];
        for (int i = 0; i < 50; i++)
        {
            var vendor = elecVendors[i % elecVendors.Length];
            var month = (i % 12 + 1).ToString("D2", CultureInfo.InvariantCulture);
            var year = 2024 + (i / 24);
            var period = $"{year}-{month}";
            var billNo = $"{vendor}-{year}-{10000 + i}";
            var qty = 15000m + (i * 850m);
            var amount = qty * 10.5m;
            var isBangla = i % 2 == 0;
            var rawText = isBangla
                ? $"{vendor} বিদ্যুৎ বিল নম্বর: {billNo}\nবিলিং মাস: {period}\nমোট ব্যবহার: {BanglaNormalizer.NormalizeDigits(qty.ToString(CultureInfo.InvariantCulture))} kWh\nমোট প্রদেয়: {amount} BDT"
                : $"{vendor} Electricity Bill No: {billNo}\nPeriod: {period}\nConsumption: {qty} kWh\nAmount: {amount} BDT";

            list.Add(new GoldenBillAnnotation(
                $"ELEC-{idCounter++:D3}",
                $"{vendor.ToLowerInvariant()}_bill_{period}_{i:D2}.pdf",
                "Electricity",
                rawText,
                vendor,
                billNo,
                period,
                qty,
                "kWh",
                amount,
                $"MTR-{900000 + i}",
                i % 5 == 0 ? 1250.00m : 0m));
        }

        // 2. 40 Gas bills
        string[] gasVendors = ["Titas Gas", "Bakhrabad Gas", "Jalalabad Gas", "Karnaphuli Gas"];
        for (int i = 0; i < 40; i++)
        {
            var vendor = gasVendors[i % gasVendors.Length];
            var month = (i % 12 + 1).ToString("D2", CultureInfo.InvariantCulture);
            var period = $"2024-{month}";
            var billNo = $"GAS-{vendor.Replace(" ", "").ToUpperInvariant()}-{20000 + i}";
            var qty = 5000m + (i * 320m);
            var amount = qty * 30.25m;
            var isBangla = i % 3 == 0;
            var rawText = isBangla
                ? $"{vendor} আরএমএস গ্যাস বিল: {billNo}\nমাস: {period}\nব্যবহার: {qty} ঘনমিটার (m3)\nবিল টাকা: {amount} BDT"
                : $"{vendor} RMS Gas Bill: {billNo}\nPeriod: {period}\nConsumption: {qty} m3\nTotal: {amount} BDT";

            list.Add(new GoldenBillAnnotation(
                $"GAS-{idCounter++:D3}",
                $"gas_{period}_{i:D2}.pdf",
                "Gas",
                rawText,
                vendor,
                billNo,
                period,
                qty,
                "m3",
                amount,
                $"RMS-{5000 + i}",
                0m));
        }

        // 3. 35 Diesel fuel receipts
        string[] dieselVendors = ["Padma Oil", "Meghna Petroleum", "Jamuna Oil"];
        for (int i = 0; i < 35; i++)
        {
            var vendor = dieselVendors[i % dieselVendors.Length];
            var month = (i % 12 + 1).ToString("D2", CultureInfo.InvariantCulture);
            var period = $"2024-{month}";
            var billNo = $"DSL-{vendor.Replace(" ", "").Substring(0, 3).ToUpperInvariant()}-{30000 + i}";
            var qty = 800m + (i * 150m);
            var amount = qty * 108.0m;
            var isCrumpled = i % 2 == 1;
            var rawText = isCrumpled
                ? $"{vendor} ফিলিং স্টেশন চালান নং: {billNo}\nতারিখ: {period}-12\nডিজেল পরিমাণ: {qty} লিটার\nটাকা: {amount} BDT"
                : $"{vendor} Fuel Slip: {billNo}\nPeriod: {period}\nDiesel Volume: {qty} litre\nPayable: {amount} BDT";

            list.Add(new GoldenBillAnnotation(
                $"DSL-{idCounter++:D3}",
                $"diesel_{period}_{i:D2}.jpg",
                "Diesel",
                rawText,
                vendor,
                billNo,
                period,
                qty,
                "litre",
                amount));
        }

        // 4. 25 Shipping & Freight Challans
        string[] transportVendors = ["Apex Logistics", "Bengal Cargo", "Savar Transport", "Chittagong Freight"];
        for (int i = 0; i < 25; i++)
        {
            var vendor = transportVendors[i % transportVendors.Length];
            var month = (i % 12 + 1).ToString("D2", CultureInfo.InvariantCulture);
            var period = $"2024-{month}";
            var billNo = $"CHLN-{vendor.Replace(" ", "").Substring(0, 3).ToUpperInvariant()}-{40000 + i}";
            var qty = 2500m + (i * 400m);
            var amount = qty * 4.5m;
            var rawText = $"{vendor} পরিবহন চালান: {billNo}\nতারিখ: {period}-20\nওজন: {qty} কেজি (kg)\nভাড়া: {amount} BDT";

            list.Add(new GoldenBillAnnotation(
                $"CHLN-{idCounter++:D3}",
                $"challan_{period}_{i:D2}.webp",
                "Transport",
                rawText,
                vendor,
                billNo,
                period,
                qty,
                "kg",
                amount));
        }

        return list;
    }

    private static void RecordField(
        string fieldName,
        string? expected,
        string? actual,
        bool isMatch,
        float conf,
        List<GoldenFieldEvaluation> evals,
        Dictionary<string, (int Matched, int Total)> map)
    {
        evals.Add(new GoldenFieldEvaluation(fieldName, expected, actual, isMatch, conf));
        var current = map[fieldName];
        map[fieldName] = (current.Matched + (isMatch ? 1 : 0), current.Total + 1);
    }
}
