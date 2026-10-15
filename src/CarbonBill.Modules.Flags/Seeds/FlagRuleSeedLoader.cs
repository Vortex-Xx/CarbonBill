using System.Text.Json;
using System.Text.Json.Serialization;
using CarbonBill.Modules.Flags.Domain;
using CarbonBill.Modules.Flags.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Flags.Seeds;

public class FlagRuleSeedDto
{
    [JsonPropertyName("flag_code")]
    public string FlagCode { get; set; } = string.Empty;

    [JsonPropertyName("family")]
    public string Family { get; set; } = string.Empty;

    [JsonPropertyName("name_bn")]
    public string NameBn { get; set; } = string.Empty;

    [JsonPropertyName("name_en")]
    public string NameEn { get; set; } = string.Empty;

    [JsonPropertyName("default_severity")]
    public string DefaultSeverity { get; set; } = string.Empty;

    [JsonPropertyName("trigger_parameters")]
    public JsonElement TriggerParameters { get; set; }

    [JsonPropertyName("suggested_action_bn")]
    public string SuggestedActionBn { get; set; } = string.Empty;

    [JsonPropertyName("suggested_action_en")]
    public string SuggestedActionEn { get; set; } = string.Empty;
}

public interface IFlagRuleSeedLoader
{
    Task<int> SeedFlagRulesAsync(string? seedFilePath = null, bool overrideExisting = false, CancellationToken ct = default);
}

public class FlagRuleSeedLoader(
    FlagsDbContext dbContext,
    ILogger<FlagRuleSeedLoader> logger) : IFlagRuleSeedLoader
{
    private readonly FlagsDbContext _dbContext = dbContext;
    private readonly ILogger<FlagRuleSeedLoader> _logger = logger;

    public async Task<int> SeedFlagRulesAsync(string? seedFilePath = null, bool overrideExisting = false, CancellationToken ct = default)
    {
        var resolvedPath = seedFilePath ?? FindSeedFile();
        if (resolvedPath == null || !File.Exists(resolvedPath))
        {
            _logger.LogWarning("Flag rules seed file not found at '{Path}'. Skipping seed.", resolvedPath);
            return 0;
        }

        var jsonContent = await File.ReadAllTextAsync(resolvedPath, ct);
        var rules = JsonSerializer.Deserialize<List<FlagRuleSeedDto>>(jsonContent);
        if (rules == null || rules.Count == 0)
        {
            return 0;
        }

        int loadedCount = 0;
        var existingRules = await _dbContext.FlagRules.ToListAsync(ct);

        foreach (var dto in rules)
        {
            var existing = existingRules.FirstOrDefault(r => r.FlagCode.Equals(dto.FlagCode, StringComparison.OrdinalIgnoreCase));
            var paramJson = dto.TriggerParameters.GetRawText();

            if (existing == null)
            {
                var newRule = new FlagRule
                {
                    FlagCode = dto.FlagCode,
                    Family = dto.Family,
                    NameBn = dto.NameBn,
                    NameEn = dto.NameEn,
                    DefaultSeverity = dto.DefaultSeverity,
                    TriggerParametersJson = paramJson,
                    SuggestedActionBn = dto.SuggestedActionBn,
                    SuggestedActionEn = dto.SuggestedActionEn,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                _dbContext.FlagRules.Add(newRule);
                loadedCount++;
            }
            else if (overrideExisting)
            {
                existing.NameBn = dto.NameBn;
                existing.NameEn = dto.NameEn;
                existing.DefaultSeverity = dto.DefaultSeverity;
                existing.TriggerParametersJson = paramJson;
                existing.SuggestedActionBn = dto.SuggestedActionBn;
                existing.SuggestedActionEn = dto.SuggestedActionEn;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                loadedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Successfully seeded/updated {Count} flag rules from '{Path}'", loadedCount, resolvedPath);
        return loadedCount;
    }

    private static string? FindSeedFile()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "seeds", "flags", "flag_rules.json");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        var baseDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (baseDir != null)
        {
            var candidate = Path.Combine(baseDir.FullName, "seeds", "flags", "flag_rules.json");
            if (File.Exists(candidate)) return candidate;
            baseDir = baseDir.Parent;
        }

        return null;
    }
}
