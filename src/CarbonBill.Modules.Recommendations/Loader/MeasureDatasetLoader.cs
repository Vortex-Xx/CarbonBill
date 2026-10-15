using System.Text.Json;
using System.Text.Json.Serialization;
using CarbonBill.Modules.Recommendations.Domain;
using CarbonBill.Modules.Recommendations.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Recommendations.Loader;

public class MeasureSourceJsonModel
{
    [JsonPropertyName("source_code")]
    public string SourceCode { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("institution")]
    public string Institution { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("license_terms")]
    public string LicenseTerms { get; set; } = string.Empty;

    [JsonPropertyName("candidate_status")]
    public string? CandidateStatus { get; set; }
}

public class MeasureJsonModel
{
    [JsonPropertyName("measure_code")]
    public string MeasureCode { get; set; } = string.Empty;

    [JsonPropertyName("name_bn")]
    public string NameBn { get; set; } = string.Empty;

    [JsonPropertyName("name_en")]
    public string NameEn { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("applicable_sectors")]
    public List<string>? ApplicableSectors { get; set; }

    [JsonPropertyName("applicability_rules")]
    public JsonElement? ApplicabilityRules { get; set; }

    [JsonPropertyName("saving_low")]
    public decimal? SavingLow { get; set; }

    [JsonPropertyName("saving_typical")]
    public decimal? SavingTypical { get; set; }

    [JsonPropertyName("saving_high")]
    public decimal? SavingHigh { get; set; }

    [JsonPropertyName("capex_low")]
    public decimal? CapexLow { get; set; }

    [JsonPropertyName("capex_high")]
    public decimal? CapexHigh { get; set; }

    [JsonPropertyName("capex_unit")]
    public string? CapexUnit { get; set; }

    [JsonPropertyName("lifetime_years")]
    public int? LifetimeYears { get; set; }

    [JsonPropertyName("evidence_grade")]
    public string? EvidenceGrade { get; set; }

    [JsonPropertyName("source_code")]
    public string? SourceCode { get; set; }

    [JsonPropertyName("local_notes")]
    public string? LocalNotes { get; set; }
}

public class MeasureDatasetLoader(
    RecommendationsDbContext dbContext,
    ILogger<MeasureDatasetLoader> logger) : IDatasetLoader<Measure>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<int> LoadSourcesAsync(Stream jsonStream, CancellationToken ct = default)
    {
        var sourceModels = await JsonSerializer.DeserializeAsync<List<MeasureSourceJsonModel>>(jsonStream, JsonOpts, ct)
            ?? [];

        int addedCount = 0;
        foreach (var sm in sourceModels)
        {
            if (string.IsNullOrWhiteSpace(sm.SourceCode)) continue;

            var existing = await dbContext.MeasureSources
                .FirstOrDefaultAsync(s => s.SourceCode == sm.SourceCode, ct);

            if (existing == null)
            {
                var newSource = new MeasureSource
                {
                    SourceCode = sm.SourceCode,
                    Title = sm.Title,
                    Institution = sm.Institution,
                    Year = sm.Year,
                    LicenseTerms = sm.LicenseTerms,
                    CandidateStatus = sm.CandidateStatus
                };
                dbContext.MeasureSources.Add(newSource);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Loaded {Count} measure sources into database.", addedCount);
        }

        return addedCount;
    }

    public async Task<int> LoadSourcesFromFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Source file not found: {filePath}", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        return await LoadSourcesAsync(stream, ct);
    }

    public async Task<int> LoadAsync(Stream jsonStream, CancellationToken ct = default)
    {
        var measureModels = await JsonSerializer.DeserializeAsync<List<MeasureJsonModel>>(jsonStream, JsonOpts, ct)
            ?? [];

        int loadedCount = 0;
        var existingSources = await dbContext.MeasureSources.ToDictionaryAsync(s => s.SourceCode, ct);

        foreach (var m in measureModels)
        {
            if (string.IsNullOrWhiteSpace(m.MeasureCode)) continue;

            // 1. Mandatory source validation:
            // Every number must have a verifiable source row. If any numeric field is present, source_code must resolve!
            bool hasNumericFields = m.SavingLow.HasValue || m.SavingTypical.HasValue || m.SavingHigh.HasValue ||
                                    m.CapexLow.HasValue || m.CapexHigh.HasValue || m.LifetimeYears.HasValue;

            MeasureSource? source = null;
            if (!string.IsNullOrWhiteSpace(m.SourceCode))
            {
                existingSources.TryGetValue(m.SourceCode, out source);
            }

            if (hasNumericFields && source == null)
            {
                throw new InvalidOperationException(
                    $"Validation failed for Measure '{m.MeasureCode}': Numeric values present but source '{m.SourceCode}' is missing or invalid. Every number must have a verifiable source row.");
            }

            // 2. If it's a template record with all null values, skip or store as uncalibrated template
            if (!hasNumericFields)
            {
                continue;
            }

            // 3. Validate range ordering: low <= typical <= high and capex_low <= capex_high
            if (m.SavingLow > m.SavingTypical || m.SavingTypical > m.SavingHigh)
            {
                throw new InvalidOperationException(
                    $"Validation failed for Measure '{m.MeasureCode}': Saving ranges must satisfy Low ({m.SavingLow}) <= Typical ({m.SavingTypical}) <= High ({m.SavingHigh}).");
            }

            if (m.CapexLow > m.CapexHigh)
            {
                throw new InvalidOperationException(
                    $"Validation failed for Measure '{m.MeasureCode}': Capex ranges must satisfy Low ({m.CapexLow}) <= High ({m.CapexHigh}).");
            }

            // 4. Validate evidence grade
            var grade = (m.EvidenceGrade ?? "B").Trim().ToUpperInvariant();
            if (grade is not ("A" or "B" or "C"))
            {
                throw new InvalidOperationException(
                    $"Validation failed for Measure '{m.MeasureCode}': Evidence grade '{m.EvidenceGrade}' is invalid. Allowed grades are 'A', 'B', or 'C'.");
            }

            // 5. Upsert Measure
            var existing = await dbContext.Measures
                .FirstOrDefaultAsync(x => x.MeasureCode == m.MeasureCode, ct);

            string sectorsJson = m.ApplicableSectors != null
                ? JsonSerializer.Serialize(m.ApplicableSectors)
                : "[\"RMG\",\"TextileDyeing\"]";

            string rulesJson = m.ApplicabilityRules.HasValue
                ? m.ApplicabilityRules.Value.GetRawText()
                : "{}";

            if (existing != null)
            {
                existing.NameBn = m.NameBn;
                existing.NameEn = m.NameEn;
                existing.Category = m.Category;
                existing.ApplicableSectorsJson = sectorsJson;
                existing.ApplicabilityRulesJson = rulesJson;
                existing.SavingLow = m.SavingLow ?? 0m;
                existing.SavingTypical = m.SavingTypical ?? 0m;
                existing.SavingHigh = m.SavingHigh ?? 0m;
                existing.CapexLow = m.CapexLow ?? 0m;
                existing.CapexHigh = m.CapexHigh ?? 0m;
                existing.CapexUnit = m.CapexUnit ?? "BDT/unit";
                existing.LifetimeYears = m.LifetimeYears ?? 5;
                existing.EvidenceGrade = grade;
                existing.SourceId = source!.Id;
                existing.LocalNotes = m.LocalNotes;
                existing.MarkUpdated();
            }
            else
            {
                var newMeasure = new Measure
                {
                    MeasureCode = m.MeasureCode,
                    NameBn = m.NameBn,
                    NameEn = m.NameEn,
                    Category = m.Category,
                    ApplicableSectorsJson = sectorsJson,
                    ApplicabilityRulesJson = rulesJson,
                    SavingLow = m.SavingLow ?? 0m,
                    SavingTypical = m.SavingTypical ?? 0m,
                    SavingHigh = m.SavingHigh ?? 0m,
                    CapexLow = m.CapexLow ?? 0m,
                    CapexHigh = m.CapexHigh ?? 0m,
                    CapexUnit = m.CapexUnit ?? "BDT/unit",
                    LifetimeYears = m.LifetimeYears ?? 5,
                    EvidenceGrade = grade,
                    SourceId = source!.Id,
                    LocalNotes = m.LocalNotes
                };
                dbContext.Measures.Add(newMeasure);
            }

            loadedCount++;
        }

        if (loadedCount > 0)
        {
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Loaded and validated {Count} measures into library.", loadedCount);
        }

        return loadedCount;
    }

    public async Task<int> LoadFromFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Measure file not found: {filePath}", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        return await LoadAsync(stream, ct);
    }
}
