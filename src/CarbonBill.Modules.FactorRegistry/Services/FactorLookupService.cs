using System.Globalization;
using CarbonBill.Modules.FactorRegistry.Domain;
using CarbonBill.Modules.FactorRegistry.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.FactorRegistry.Services;

public record ResolvedFactor(
    Guid FactorId,
    string FactorSetName,
    int FactorVersion,
    string ActivityType,
    string Unit,
    decimal Co2eFactor,
    int Scope,
    bool IsOverridden,
    string? Justification,
    string GwpBasis,
    string SourceCitation);

public interface IFactorLookup
{
    Task<ResolvedFactor?> ResolveFactorAsync(
        Guid orgId,
        string activityType,
        string? fuelOrMode = null,
        int? targetYear = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ResolvedFactor>> GetAllCurrentFactorsAsync(
        Guid? orgId = null,
        CancellationToken ct = default);
}

public class FactorLookupService(FactorRegistryDbContext dbContext) : IFactorLookup
{
    public async Task<ResolvedFactor?> ResolveFactorAsync(
        Guid orgId,
        string activityType,
        string? fuelOrMode = null,
        int? targetYear = null,
        CancellationToken ct = default)
    {
        var normalizedType = activityType.Trim().ToLowerInvariant();

        // 1. Fetch published emission factors
        var factors = await dbContext.EmissionFactors
            .AsNoTracking()
            .Include(f => f.FactorSet)
            .Where(f => f.FactorSet.IsPublished)
            .ToListAsync(ct);

        IEnumerable<EmissionFactor> candidates = factors;

        if (!string.IsNullOrWhiteSpace(fuelOrMode))
        {
            var normalizedFuel = fuelOrMode.Trim().ToLowerInvariant();
            candidates = candidates.Where(f =>
                string.Equals(f.ActivityType, normalizedType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(f.FuelOrMode, normalizedFuel, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            candidates = candidates.Where(f =>
                string.Equals(f.ActivityType, normalizedType, StringComparison.OrdinalIgnoreCase));
        }

        if (targetYear.HasValue)
        {
            candidates = candidates.OrderByDescending(f => f.FactorSet.Year == targetYear.Value)
                                   .ThenByDescending(f => f.FactorSet.Year);
        }
        else
        {
            candidates = candidates.OrderByDescending(f => f.FactorSet.Year);
        }

        var emissionFactor = candidates.FirstOrDefault();
        if (emissionFactor == null)
        {
            // Fallback match by broad activity type substring
            emissionFactor = factors
                .Where(f => f.ActivityType.Contains(normalizedType, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.FactorSet.Year)
                .FirstOrDefault();
        }

        if (emissionFactor == null)
        {
            return null;
        }

        // 2. Check for active tenant override
        var activeOverride = await dbContext.FactorOverrides
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.OrgId == orgId &&
                                      o.EmissionFactorId == emissionFactor.Id &&
                                      o.IsActive, ct);

        if (activeOverride != null)
        {
            return new ResolvedFactor(
                FactorId: emissionFactor.Id,
                FactorSetName: emissionFactor.FactorSet?.Name ?? "Custom Override",
                FactorVersion: emissionFactor.FactorSet?.Version ?? 1,
                ActivityType: emissionFactor.ActivityType,
                Unit: emissionFactor.Unit,
                Co2eFactor: activeOverride.OverrideValue,
                Scope: emissionFactor.Scope,
                IsOverridden: true,
                Justification: activeOverride.Justification,
                GwpBasis: emissionFactor.FactorSet?.GwpBasis ?? "AR6",
                SourceCitation: $"Tenant Override: {activeOverride.Justification}");
        }

        return new ResolvedFactor(
            FactorId: emissionFactor.Id,
            FactorSetName: emissionFactor.FactorSet?.Name ?? "Default",
            FactorVersion: emissionFactor.FactorSet?.Version ?? 1,
            ActivityType: emissionFactor.ActivityType,
            Unit: emissionFactor.Unit,
            Co2eFactor: emissionFactor.Co2eFactor,
            Scope: emissionFactor.Scope,
            IsOverridden: false,
            Justification: null,
            GwpBasis: emissionFactor.FactorSet?.GwpBasis ?? "AR6",
            SourceCitation: emissionFactor.FactorSet?.SourceCitation ?? "Default Registry");
    }

    public async Task<IReadOnlyList<ResolvedFactor>> GetAllCurrentFactorsAsync(
        Guid? orgId = null,
        CancellationToken ct = default)
    {
        var factors = await dbContext.EmissionFactors
            .AsNoTracking()
            .Include(f => f.FactorSet)
            .Where(f => f.FactorSet.IsPublished)
            .ToListAsync(ct);

        Dictionary<Guid, CarbonBill.Modules.FactorRegistry.Domain.FactorOverride> overrides;

        if (orgId.HasValue)
        {
            overrides = await dbContext.FactorOverrides
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(o => o.OrgId == orgId.Value && o.IsActive)
                .ToDictionaryAsync(o => o.EmissionFactorId, ct);
        }
        else
        {
            overrides = [];
        }

        var result = new List<ResolvedFactor>();
        foreach (var f in factors)
        {
            if (overrides.TryGetValue(f.Id, out var ov))
            {
                result.Add(new ResolvedFactor(
                    FactorId: f.Id,
                    FactorSetName: f.FactorSet?.Name ?? "Custom Override",
                    FactorVersion: f.FactorSet?.Version ?? 1,
                    ActivityType: f.ActivityType,
                    Unit: f.Unit,
                    Co2eFactor: ov.OverrideValue,
                    Scope: f.Scope,
                    IsOverridden: true,
                    Justification: ov.Justification,
                    GwpBasis: f.FactorSet?.GwpBasis ?? "AR6",
                    SourceCitation: $"Tenant Override: {ov.Justification}"));
            }
            else
            {
                result.Add(new ResolvedFactor(
                    FactorId: f.Id,
                    FactorSetName: f.FactorSet?.Name ?? "Default",
                    FactorVersion: f.FactorSet?.Version ?? 1,
                    ActivityType: f.ActivityType,
                    Unit: f.Unit,
                    Co2eFactor: f.Co2eFactor,
                    Scope: f.Scope,
                    IsOverridden: false,
                    Justification: null,
                    GwpBasis: f.FactorSet?.GwpBasis ?? "AR6",
                    SourceCitation: f.FactorSet?.SourceCitation ?? "Default Registry"));
            }
        }

        return result;
    }
}
