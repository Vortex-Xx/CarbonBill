using System.Globalization;
using System.Text.Json;
using CarbonBill.Modules.Flags.Contracts;
using CarbonBill.Modules.Flags.Domain;
using CarbonBill.SharedKernel.Contracts;

namespace CarbonBill.Modules.Flags.Engine;

// 1. SPIKE_MOM: Month-on-Month Spike Evaluator
public class SpikeMomRuleEvaluator(IEmissionReadModel emissionReadModel) : IFlagRuleEvaluator
{
    private readonly IEmissionReadModel _emissionReadModel = emissionReadModel;

    public string RuleCode => "SPIKE_MOM";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        // 1. Read tunable parameters from FlagRule
        decimal amberMultiplier = 1.30m;
        decimal redMultiplier = 1.60m;
        int minHistoryMonths = 3;

        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("amber_median_multiplier", out var aProp))
                    amberMultiplier = (decimal)aProp.GetDouble();
                if (doc.RootElement.TryGetProperty("red_median_multiplier", out var rProp))
                    redMultiplier = (decimal)rProp.GetDouble();
                if (doc.RootElement.TryGetProperty("min_history_months", out var mProp))
                    minHistoryMonths = mProp.GetInt32();
            }
            catch (JsonException) { }
        }

        // 2. Fetch monthly trend history
        var trend = await _emissionReadModel.GetMonthlyTrendAsync(context.OrgId, 12, ct);
        var history = trend
            .Where(t => string.Compare(t.Period, context.Period, StringComparison.OrdinalIgnoreCase) < 0)
            .OrderBy(t => t.Period)
            .ToList();

        // If fewer than minHistoryMonths (3), no flag is raised
        if (history.Count < minHistoryMonths)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        // Calculate trailing median
        var trailing = history.TakeLast(minHistoryMonths).Select(t => t.TotalKgCo2e).OrderBy(v => v).ToList();
        decimal median = trailing.Count % 2 == 1
            ? trailing[trailing.Count / 2]
            : (trailing[(trailing.Count / 2) - 1] + trailing[trailing.Count / 2]) / 2.0m;

        if (median <= 0)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        // Current month emissions
        var currentSummary = await _emissionReadModel.GetSummaryAsync(context.OrgId, context.Period, ct);
        decimal currentTotal = currentSummary.TotalKgCo2e;

        if (currentTotal > redMultiplier * median)
        {
            var pct = Math.Round((currentTotal / median - 1.0m) * 100.0m, 1);
            var evidence = JsonSerializer.Serialize(new
            {
                currentMonthValue = currentTotal,
                trailingMedian = median,
                ratio = Math.Round(currentTotal / median, 2),
                trailingMonths = history.TakeLast(minHistoryMonths).Select(h => h.Period).ToList()
            });

            return new EvaluationResult(
                ShouldFlag: true,
                SeverityOverride: "Red",
                EvidenceJson: evidence,
                ExplanationBn: $"বর্তমান মাসের কার্বন নির্গমন ({FlagFormatters.ToBanglaDigits(currentTotal.ToString("N1", CultureInfo.InvariantCulture))} kg CO2e) বিগত ৩ মাসের মধ্যকের চেয়ে {FlagFormatters.ToBanglaDigits(pct.ToString("N0", CultureInfo.InvariantCulture))}% বৃদ্ধি পেয়েছে।",
                ExplanationEn: $"Emissions for period {context.Period} ({currentTotal:N1} kg CO2e) surged by {pct:N0}% compared to the trailing 3-month median ({median:N1} kg CO2e).");
        }

        if (currentTotal > amberMultiplier * median)
        {
            var pct = Math.Round((currentTotal / median - 1.0m) * 100.0m, 1);
            var evidence = JsonSerializer.Serialize(new
            {
                currentMonthValue = currentTotal,
                trailingMedian = median,
                ratio = Math.Round(currentTotal / median, 2),
                trailingMonths = history.TakeLast(minHistoryMonths).Select(h => h.Period).ToList()
            });

            return new EvaluationResult(
                ShouldFlag: true,
                SeverityOverride: "Amber",
                EvidenceJson: evidence,
                ExplanationBn: $"বর্তমান মাসের কার্বন নির্গমন ({FlagFormatters.ToBanglaDigits(currentTotal.ToString("N1", CultureInfo.InvariantCulture))} kg CO2e) বিগত ৩ মাসের মধ্যকের চেয়ে {FlagFormatters.ToBanglaDigits(pct.ToString("N0", CultureInfo.InvariantCulture))}% বৃদ্ধি পেয়েছে।",
                ExplanationEn: $"Emissions for period {context.Period} ({currentTotal:N1} kg CO2e) increased by {pct:N0}% compared to trailing 3-month median ({median:N1} kg CO2e).");
        }

        return new EvaluationResult(ShouldFlag: false);
    }
}

// 2. HOTSPOT_DETECTED: Single Source > 50%
public class HotspotDetectedRuleEvaluator(IEmissionReadModel emissionReadModel) : IFlagRuleEvaluator
{
    private readonly IEmissionReadModel _emissionReadModel = emissionReadModel;

    public string RuleCode => "HOTSPOT_DETECTED";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        decimal threshold = 50.0m;
        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("percentage_of_total_threshold", out var prop))
                    threshold = (decimal)prop.GetDouble();
            }
            catch (JsonException) { }
        }

        var breakdown = await _emissionReadModel.GetScopeBreakdownAsync(context.OrgId, context.Period, ct);
        if (breakdown.Count == 0)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        var hotspot = breakdown.FirstOrDefault(b => b.Percentage > threshold);
        if (hotspot == null)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        var evidence = JsonSerializer.Serialize(new
        {
            category = hotspot.Category,
            percentage = hotspot.Percentage,
            kgCo2e = hotspot.KgCo2e,
            threshold
        });

        return new EvaluationResult(
            ShouldFlag: true,
            SeverityOverride: "Info",
            EvidenceJson: evidence,
            ExplanationBn: $"কার্বন ফুটপ্রিন্টের {FlagFormatters.ToBanglaDigits(hotspot.Percentage.ToString("F1", CultureInfo.InvariantCulture))}% আসছে {hotspot.Category} খাত থেকে।",
            ExplanationEn: $"{hotspot.Category} accounts for {hotspot.Percentage:F1}% of the factory's total carbon footprint.");
    }
}

// 3. GENSET_RELIANCE: Generator electricity > 20%
public class GensetRelianceRuleEvaluator(
    IFlagDocumentReadModel documentReadModel,
    IEmissionReadModel emissionReadModel) : IFlagRuleEvaluator
{
    private readonly IFlagDocumentReadModel _documentReadModel = documentReadModel;
    private readonly IEmissionReadModel _emissionReadModel = emissionReadModel;

    public string RuleCode => "GENSET_RELIANCE";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        decimal threshold = 20.0m;
        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("genset_kwh_percentage_threshold", out var prop))
                    threshold = (decimal)prop.GetDouble();
            }
            catch (JsonException) { }
        }

        var docs = await _documentReadModel.GetExtractedDocumentsAsync(context.OrgId, context.Period, ct);
        decimal gridKwh = docs.Where(d => string.Equals(d.DocType, "electricity", StringComparison.OrdinalIgnoreCase)).Sum(d => d.Quantity);
        decimal gensetKwh = docs.Where(d => string.Equals(d.DocType, "diesel", StringComparison.OrdinalIgnoreCase)).Sum(d => d.Quantity * 3.5m);

        if (gridKwh <= 0 && gensetKwh <= 0)
        {
            var breakdown = await _emissionReadModel.GetScopeBreakdownAsync(context.OrgId, context.Period, ct);
            gridKwh = breakdown.Where(b => b.Scope == 2).Sum(b => b.QuantityStandard);
            gensetKwh = breakdown.Where(b => b.Scope == 1 && b.Category.Contains("Diesel", StringComparison.OrdinalIgnoreCase)).Sum(b => b.QuantityStandard * 3.5m);
        }

        var totalElectricity = gridKwh + gensetKwh;
        if (totalElectricity <= 0)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        var gensetPct = Math.Round((gensetKwh / totalElectricity) * 100.0m, 1);
        if (gensetPct > threshold)
        {
            var evidence = JsonSerializer.Serialize(new
            {
                gensetKwh,
                gridKwh,
                totalElectricityKwh = totalElectricity,
                gensetPercentage = gensetPct,
                threshold
            });

            return new EvaluationResult(
                ShouldFlag: true,
                SeverityOverride: "Amber",
                EvidenceJson: evidence,
                ExplanationBn: $"ডিজেল জেনারেটর নির্ভরতা {FlagFormatters.ToBanglaDigits(gensetPct.ToString("F1", CultureInfo.InvariantCulture))}% যা ২০% সীমা অতিক্রম করেছে।",
                ExplanationEn: $"Diesel generator electricity accounts for {gensetPct:F1}% of total monthly electricity consumption, exceeding the {threshold}% threshold.");
        }

        return new EvaluationResult(ShouldFlag: false);
    }
}

// 4. INTENSITY_ABOVE_PEERS: Intensity > P75 (Amber) or P90 (Red), Gated by N >= 10
public class IntensityAbovePeersRuleEvaluator(
    IProductionMetricReader productionMetricReader,
    IBenchmarkReader benchmarkReader,
    IEmissionReadModel emissionReadModel) : IFlagRuleEvaluator
{
    private readonly IProductionMetricReader _productionMetricReader = productionMetricReader;
    private readonly IBenchmarkReader _benchmarkReader = benchmarkReader;
    private readonly IEmissionReadModel _emissionReadModel = emissionReadModel;

    public string RuleCode => "INTENSITY_ABOVE_PEERS";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        int minPeerCount = 10;
        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("min_peer_count", out var pProp))
                    minPeerCount = pProp.GetInt32();
            }
            catch (JsonException) { }
        }

        var metric = await _productionMetricReader.GetMetricAsync(context.OrgId, context.Period, context.SiteId, ct);
        if (metric == null || metric.Quantity <= 0)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        var summary = await _emissionReadModel.GetSummaryAsync(context.OrgId, context.Period, ct);
        if (summary.TotalKgCo2e <= 0)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        decimal intensity = Math.Round(summary.TotalKgCo2e / metric.Quantity, 6);

        // Fetch benchmark for peer comparison
        var benchmark = await _benchmarkReader.GetBenchmarkAsync("RMG", "Medium", $"kg_co2e_per_{metric.Unit}", ct)
            ?? await _benchmarkReader.GetBenchmarkAsync("RMG", "Medium", "kg_co2e_per_piece", ct);

        // BENCHMARK HONESTY GATING: No flag if N < minPeerCount
        if (benchmark == null || benchmark.N < minPeerCount)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        if (intensity > benchmark.P90)
        {
            var evidence = JsonSerializer.Serialize(new
            {
                intensity,
                benchmark.P75,
                benchmark.P90,
                peerCount = benchmark.N,
                metricUnit = metric.Unit
            });

            return new EvaluationResult(
                ShouldFlag: true,
                SeverityOverride: "Red",
                EvidenceJson: evidence,
                ExplanationBn: $"নির্গমন তীব্রতা ({FlagFormatters.ToBanglaDigits(intensity.ToString("F3", CultureInfo.InvariantCulture))} kg CO2e/{metric.Unit}) সহকর্মীদের ৯০তম পার্সেন্টাইল ({FlagFormatters.ToBanglaDigits(benchmark.P90.ToString("F3", CultureInfo.InvariantCulture))}) অতিক্রম করেছে।",
                ExplanationEn: $"Emission intensity ({intensity:F3} kg CO2e/{metric.Unit}) exceeds peer 90th percentile ({benchmark.P90:F3}).");
        }

        if (intensity > benchmark.P75)
        {
            var evidence = JsonSerializer.Serialize(new
            {
                intensity,
                benchmark.P75,
                benchmark.P90,
                peerCount = benchmark.N,
                metricUnit = metric.Unit
            });

            return new EvaluationResult(
                ShouldFlag: true,
                SeverityOverride: "Amber",
                EvidenceJson: evidence,
                ExplanationBn: $"নির্গমন তীব্রতা ({FlagFormatters.ToBanglaDigits(intensity.ToString("F3", CultureInfo.InvariantCulture))} kg CO2e/{metric.Unit}) সহকর্মীদের ৭৫তম পার্সেন্টাইল ({FlagFormatters.ToBanglaDigits(benchmark.P75.ToString("F3", CultureInfo.InvariantCulture))}) অতিক্রম করেছে।",
                ExplanationEn: $"Emission intensity ({intensity:F3} kg CO2e/{metric.Unit}) exceeds peer 75th percentile ({benchmark.P75:F3}).");
        }

        return new EvaluationResult(ShouldFlag: false);
    }
}

// 5. TARGET_DRIFT: Trajectory Exceeds Target Reduction
public class TargetDriftRuleEvaluator(
    IEmissionReadModel emissionReadModel,
    ITargetReadModel targetReadModel) : IFlagRuleEvaluator
{
    private readonly IEmissionReadModel _emissionReadModel = emissionReadModel;
    private readonly ITargetReadModel _targetReadModel = targetReadModel;

    public string RuleCode => "TARGET_DRIFT";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        decimal maxDriftPct = 5.0m;
        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("max_allowed_drift_percentage", out var prop))
                    maxDriftPct = (decimal)prop.GetDouble();
            }
            catch (JsonException) { }
        }

        var target = await _targetReadModel.GetTargetFootprintAsync(context.OrgId, context.Period, ct);
        if (!target.HasValue || target.Value <= 0)
        {
            return new EvaluationResult(ShouldFlag: false);
        }

        var summary = await _emissionReadModel.GetSummaryAsync(context.OrgId, context.Period, ct);
        decimal actual = summary.TotalKgCo2e;

        decimal allowedLimit = target.Value * (1.0m + maxDriftPct / 100.0m);
        if (actual > allowedLimit)
        {
            decimal driftPct = Math.Round((actual / target.Value - 1.0m) * 100.0m, 1);
            var evidence = JsonSerializer.Serialize(new
            {
                actualKgCo2e = actual,
                targetKgCo2e = target.Value,
                driftPercentage = driftPct,
                maxAllowedDriftPercentage = maxDriftPct
            });

            return new EvaluationResult(
                ShouldFlag: true,
                SeverityOverride: "Amber",
                EvidenceJson: evidence,
                ExplanationBn: $"মাসিক নির্গমন লক্ষ্যমাত্রার চেয়ে {FlagFormatters.ToBanglaDigits(driftPct.ToString("F1", CultureInfo.InvariantCulture))}% বেশি হয়েছে।",
                ExplanationEn: $"Emissions for {context.Period} drift {driftPct:F1}% above the reduction trajectory target.");
        }

        return new EvaluationResult(ShouldFlag: false);
    }
}

// 6. REPORT_NOT_READY: Unconfirmed docs, missing calendar months, or DQ < 80%
public class ReportNotReadyRuleEvaluator(
    IExpectedDocRuleReader expectedDocRuleReader,
    IDocumentReadModel documentReadModel,
    IEmissionReadModel emissionReadModel) : IFlagRuleEvaluator
{
    private readonly IExpectedDocRuleReader _expectedDocRuleReader = expectedDocRuleReader;
    private readonly IDocumentReadModel _documentReadModel = documentReadModel;
    private readonly IEmissionReadModel _emissionReadModel = emissionReadModel;

    public string RuleCode => "REPORT_NOT_READY";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        decimal minScore = 80.0m;
        bool requireZeroMissing = true;

        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("min_data_quality_score", out var sProp))
                    minScore = (decimal)sProp.GetDouble();
                if (doc.RootElement.TryGetProperty("require_zero_missing_documents", out var mProp))
                    requireZeroMissing = mProp.GetBoolean();
            }
            catch (JsonException) { }
        }

        // 1. Missing documents check
        var expectedRules = await _expectedDocRuleReader.GetRulesForOrgAsync(context.OrgId, ct);
        int missingCount = 0;
        foreach (var r in expectedRules)
        {
            var hasDoc = await _documentReadModel.HasDocumentAsync(context.OrgId, r.AssetId, r.DocType, context.Period, ct);
            if (!hasDoc) missingCount++;
        }

        // 2. Unconfirmed documents check
        var docs = await _documentReadModel.GetDocumentsAsync(context.OrgId, context.Period, ct);
        int unconfirmedCount = docs.Count(d =>
            !string.Equals(d.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(d.Status, "Calculated", StringComparison.OrdinalIgnoreCase));

        // 3. Data quality score check
        var summary = await _emissionReadModel.GetSummaryAsync(context.OrgId, context.Period, ct);
        bool lowQuality = summary.DataQualityScore < minScore;

        if ((requireZeroMissing && missingCount > 0) || unconfirmedCount > 0 || lowQuality)
        {
            var evidence = JsonSerializer.Serialize(new
            {
                missingDocuments = missingCount,
                unconfirmedDocuments = unconfirmedCount,
                dataQualityScore = summary.DataQualityScore,
                minRequiredScore = minScore
            });

            return new EvaluationResult(
                ShouldFlag: true,
                SeverityOverride: "Red",
                EvidenceJson: evidence,
                ExplanationBn: $"রিপোর্ট অডিট-উপযোগী নয় (অমীমাংসিত চালান: {FlagFormatters.ToBanglaDigits(unconfirmedCount.ToString(CultureInfo.InvariantCulture))}, অনুপস্থিত: {FlagFormatters.ToBanglaDigits(missingCount.ToString(CultureInfo.InvariantCulture))}, ডেটা স্কোর: {FlagFormatters.ToBanglaDigits(summary.DataQualityScore.ToString("F0", CultureInfo.InvariantCulture))}%)।",
                ExplanationEn: $"Report is not audit-ready ({unconfirmedCount} unconfirmed slips, {missingCount} missing docs, DQ score {summary.DataQualityScore:F0}%).");
        }

        return new EvaluationResult(ShouldFlag: false);
    }
}

// 7. FACTOR_OUTDATED: Active Factor Set year older than reporting period
public class FactorOutdatedRuleEvaluator(IFactorRegistryReadModel factorRegistryReadModel) : IFlagRuleEvaluator
{
    private readonly IFactorRegistryReadModel _factorRegistryReadModel = factorRegistryReadModel;

    public string RuleCode => "FACTOR_OUTDATED";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        int maxAgeYears = 1;
        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("max_age_years", out var prop))
                    maxAgeYears = prop.GetInt32();
            }
            catch (JsonException) { }
        }

        int periodYear = DateTime.UtcNow.Year;
        if (context.Period.Length >= 4 && int.TryParse(context.Period.AsSpan(0, 4), out var py))
        {
            periodYear = py;
        }

        int factorYear = await _factorRegistryReadModel.GetActiveFactorSetYearAsync(ct);
        int ageYears = periodYear - factorYear;

        if (ageYears > maxAgeYears)
        {
            var evidence = JsonSerializer.Serialize(new
            {
                periodYear,
                factorYear,
                ageYears,
                maxAgeYears
            });

            return new EvaluationResult(
                ShouldFlag: true,
                SeverityOverride: "Amber",
                EvidenceJson: evidence,
                ExplanationBn: $"নির্গমন ফ্যাক্টর সেট ({FlagFormatters.ToBanglaDigits(factorYear.ToString(CultureInfo.InvariantCulture))}) পুরোনো; বর্তমান রিপোর্টিং বছর {FlagFormatters.ToBanglaDigits(periodYear.ToString(CultureInfo.InvariantCulture))}-এর জন্য হালনাগাদ করা প্রয়োজন।",
                ExplanationEn: $"Active factor set reference year ({factorYear}) is outdated for reporting year {periodYear}.");
        }

        return new EvaluationResult(ShouldFlag: false);
    }
}

// 8. OVERRIDE_UNAPPROVED: Custom Factor Override Lacks Approved Sign-off or Justification
public class OverrideUnapprovedRuleEvaluator(IFactorRegistryReadModel factorRegistryReadModel) : IFlagRuleEvaluator
{
    private readonly IFactorRegistryReadModel _factorRegistryReadModel = factorRegistryReadModel;

    public string RuleCode => "OVERRIDE_UNAPPROVED";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        bool requireJustification = true;
        bool requireApprover = true;

        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("require_justification", out var jProp))
                    requireJustification = jProp.GetBoolean();
                if (doc.RootElement.TryGetProperty("require_approver", out var aProp))
                    requireApprover = aProp.GetBoolean();
            }
            catch (JsonException) { }
        }

        var overrides = await _factorRegistryReadModel.GetActiveOverridesAsync(context.OrgId, ct);
        var unapproved = overrides.Where(o => o.IsActive && (
            (requireJustification && string.IsNullOrWhiteSpace(o.Justification)) ||
            (requireApprover && o.ApprovedByUserId == Guid.Empty)
        )).ToList();

        if (unapproved.Count > 0)
        {
            var evidence = JsonSerializer.Serialize(new
            {
                unapprovedOverridesCount = unapproved.Count,
                overrideIds = unapproved.Select(u => u.Id).ToList()
            });

            return new EvaluationResult(
                ShouldFlag: true,
                SeverityOverride: "Red",
                EvidenceJson: evidence,
                ExplanationBn: $"কাস্টম ফ্যাক্টরে অনুমোদন বা যৌক্তিক ব্যাখ্যা নেই ({FlagFormatters.ToBanglaDigits(unapproved.Count.ToString(CultureInfo.InvariantCulture))}টি ফ্যাক্টর); অনুমোদন সম্পন্ন করুন।",
                ExplanationEn: $"Factor override lacks justification or authorized approver ({unapproved.Count} unapproved overrides).");
        }

        return new EvaluationResult(ShouldFlag: false);
    }
}

// 9. POWER_FACTOR_PENALTY: Power factor < 0.90 or maximum demand charge penalty line detected
public class PowerFactorPenaltyRuleEvaluator(IFlagDocumentReadModel documentReadModel) : IFlagRuleEvaluator
{
    private readonly IFlagDocumentReadModel _documentReadModel = documentReadModel;

    public string RuleCode => "POWER_FACTOR_PENALTY";

    public async Task<EvaluationResult> EvaluateAsync(FlagRule rule, EvaluationContext context, CancellationToken ct = default)
    {
        decimal minPf = 0.90m;
        if (!string.IsNullOrWhiteSpace(rule.TriggerParametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(rule.TriggerParametersJson);
                if (doc.RootElement.TryGetProperty("min_power_factor", out var prop))
                    minPf = (decimal)prop.GetDouble();
            }
            catch (JsonException) { }
        }

        var documents = await _documentReadModel.GetExtractedDocumentsAsync(context.OrgId, context.Period, ct);
        foreach (var doc in documents)
        {
            foreach (var field in doc.Fields)
            {
                var lowerName = field.FieldName.ToLowerInvariant();

                // Check power factor field
                if (lowerName.Contains("power_factor", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(lowerName, "pf", StringComparison.OrdinalIgnoreCase))
                {
                    if (decimal.TryParse(field.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var pfVal))
                    {
                        if (pfVal > 0 && pfVal < minPf)
                        {
                            var evidence = JsonSerializer.Serialize(new
                            {
                                documentId = doc.DocumentId,
                                fieldName = field.FieldName,
                                powerFactor = pfVal,
                                minPowerFactor = minPf
                            });

                            return new EvaluationResult(
                                ShouldFlag: true,
                                SeverityOverride: "Info",
                                EvidenceJson: evidence,
                                ExplanationBn: $"বিদ্যুৎ বিলে পাওয়ার ফ্যাক্টর পেনাল্টি সনাক্ত হয়েছে ({FlagFormatters.ToBanglaDigits(pfVal.ToString("F2", CultureInfo.InvariantCulture))} < {FlagFormatters.ToBanglaDigits(minPf.ToString("F2", CultureInfo.InvariantCulture))})।",
                                ExplanationEn: $"Power factor penalty detected on electricity bill ({pfVal:F2} < {minPf:F2}).");
                        }
                    }
                }

                // Check penalty or demand charge line
                if (lowerName.Contains("penalty", StringComparison.OrdinalIgnoreCase) ||
                    lowerName.Contains("demand_charge_penalty", StringComparison.OrdinalIgnoreCase))
                {
                    if (decimal.TryParse(field.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var penVal) && penVal > 0)
                    {
                        var evidence = JsonSerializer.Serialize(new
                        {
                            documentId = doc.DocumentId,
                            fieldName = field.FieldName,
                            penaltyAmount = penVal
                        });

                        return new EvaluationResult(
                            ShouldFlag: true,
                            SeverityOverride: "Info",
                            EvidenceJson: evidence,
                            ExplanationBn: $"বিদ্যুৎ বিলে পেনাল্টি চার্জ সনাক্ত হয়েছে ({FlagFormatters.ToBanglaDigits(penVal.ToString("N2", CultureInfo.InvariantCulture))} BDT)।",
                            ExplanationEn: $"Penalty charge detected on utility bill ({penVal:N2} BDT).");
                    }
                }
            }
        }

        return new EvaluationResult(ShouldFlag: false);
    }
}
