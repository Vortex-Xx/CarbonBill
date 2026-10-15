using CarbonBill.Modules.ActivityUnits.Domain;
using CarbonBill.Modules.ActivityUnits.Persistence;
using CarbonBill.Modules.ActivityUnits.Services;
using CarbonBill.Modules.Audit.Domain;
using CarbonBill.Modules.Audit.Services;
using CarbonBill.Modules.Calculation.Domain;
using CarbonBill.Modules.Calculation.Persistence;
using CarbonBill.Modules.FactorRegistry.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Domain;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Calculation.Services;

public class ActivityWriter(
    ActivityUnitsDbContext activityDbContext,
    CalculationDbContext calculationDbContext,
    IUnitConverter unitConverter,
    IFactorLookup factorLookup,
    IAuditLogService auditLogService,
    ILogger<ActivityWriter> logger) : IActivityWriter
{
    public async Task<Result<Guid>> RecordConfirmedActivityAsync(
        ConfirmedActivityRequest request,
        CancellationToken ct = default)
    {
        try
        {
            // 1. Canonical Unit Conversion
            var conversion = await unitConverter.ConvertToCanonicalAsync(
                request.Quantity,
                request.Unit,
                request.ActivityType,
                ct);

            // 2. Map standard activity metadata
            var (scope, category) = DetermineScopeAndCategory(request.ActivityType);

            // 3. Resolve Emission Factor with tenant overrides
            var factor = await factorLookup.ResolveFactorAsync(
                request.OrgId,
                request.ActivityType,
                null,
                null,
                ct);

            var factorId = factor?.FactorId ?? Guid.NewGuid();
            var factorVersion = factor?.FactorVersion ?? 1;
            var factorValue = factor?.Co2eFactor ?? GetDefaultFactorForActivity(request.ActivityType);
            var effectiveScope = factor?.Scope ?? scope;

            // 4. Record Activity
            var unitCost = request.TotalCostBdt.HasValue && conversion.CanonicalQuantity > 0
                ? Math.Round(request.TotalCostBdt.Value / conversion.CanonicalQuantity, 2)
                : (decimal?)null;

            var activityRecord = new ActivityRecord
            {
                OrgId = request.OrgId,
                SiteId = request.SiteId,
                AssetId = request.AssetId,
                DocumentId = request.DocumentId,
                ActivityType = request.ActivityType,
                QuantityStandard = conversion.CanonicalQuantity,
                StandardUnit = conversion.CanonicalUnit,
                RawQuantity = request.Quantity,
                RawUnit = request.Unit,
                TotalCostBdt = request.TotalCostBdt,
                UnitCostBdt = unitCost,
                Period = request.BillingPeriod,
                IsEstimated = request.IsEstimated,
                CreatedAtUtc = DateTime.UtcNow
            };

            activityDbContext.ActivityRecords.Add(activityRecord);
            await activityDbContext.SaveChangesAsync(ct);

            // 5. Calculate GHG Footprint (kg CO2e = quantity_standard * emission_factor)
            var kgCo2e = conversion.CanonicalQuantity * factorValue;

            var emissionResult = new EmissionResult
            {
                OrgId = request.OrgId,
                ActivityRecordId = activityRecord.Id,
                DocumentId = request.DocumentId,
                FactorId = factorId,
                FactorVersion = factorVersion,
                ConversionVersion = conversion.ConversionVersion,
                Scope = effectiveScope,
                Category = category,
                QuantityStandard = conversion.CanonicalQuantity,
                StandardUnit = conversion.CanonicalUnit,
                EmissionFactorUsed = factorValue,
                KgCo2e = kgCo2e,
                Period = request.BillingPeriod,
                IsEstimated = request.IsEstimated,
                CalculatedAtUtc = DateTime.UtcNow
            };

            calculationDbContext.EmissionResults.Add(emissionResult);
            await calculationDbContext.SaveChangesAsync(ct);

            // 6. Audit Trail
            await auditLogService.LogAsync(new AuditLogEntry(
                OrgId: request.OrgId,
                Action: "ActivityCalculated",
                EntityType: "EmissionResult",
                EntityId: emissionResult.Id.ToString(),
                UserId: null,
                UserEmail: null,
                Details: $"Confirmed {request.ActivityType}: {conversion.CanonicalQuantity} {conversion.CanonicalUnit} -> {kgCo2e:F2} kg CO2e (Scope {effectiveScope})"), ct);

            logger.LogInformation(
                "Successfully recorded activity and emission for Org {OrgId}, Doc {DocId}: {KgCo2e} kg CO2e",
                request.OrgId, request.DocumentId, kgCo2e);

            return Result.Success(activityRecord.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record confirmed activity for Document {DocId}", request.DocumentId);
            return Result.Failure<Guid>($"Calculation engine error: {ex.Message}");
        }
    }

    private static (int Scope, string Category) DetermineScopeAndCategory(string activityType)
    {
        var type = activityType.Trim().ToLowerInvariant();
        if (type.Contains("electricity") || type.Contains("grid") || type.Contains("desco") || type.Contains("reb"))
        {
            return (2, "Grid Electricity");
        }

        if (type.Contains("diesel") || type.Contains("generator") || type.Contains("genset"))
        {
            return (1, "Stationary Combustion (Diesel)");
        }

        if (type.Contains("gas") || type.Contains("titas") || type.Contains("boiler"))
        {
            return (1, "Stationary Combustion (Natural Gas)");
        }

        if (type.Contains("lpg"))
        {
            return (1, "Stationary Combustion (LPG)");
        }

        if (type.Contains("transport") || type.Contains("freight") || type.Contains("truck") || type.Contains("shipping"))
        {
            return (3, "Upstream Transportation");
        }

        return (1, "Stationary Combustion");
    }

    private static decimal GetDefaultFactorForActivity(string activityType)
    {
        var type = activityType.Trim().ToLowerInvariant();
        if (type.Contains("electricity")) return 0.621000m; // IGES Bangladesh Grid
        if (type.Contains("diesel")) return 2.680000m;      // DEFRA / IPCC Diesel
        if (type.Contains("gas")) return 1.930000m;         // IPCC Natural Gas
        if (type.Contains("lpg")) return 2.940000m;         // IPCC LPG
        if (type.Contains("freight")) return 0.160000m;     // DEFRA Rigid Truck
        return 1.000000m;
    }
}
