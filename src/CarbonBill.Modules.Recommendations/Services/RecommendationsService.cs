using System.Text.Json;
using CarbonBill.Modules.Recommendations.Domain;
using CarbonBill.Modules.Recommendations.Persistence;
using CarbonBill.Modules.Recommendations.Pipeline;
using CarbonBill.Modules.Recommendations.Pipeline.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CarbonBill.Modules.Recommendations.Services;

public record UpdateFacilityProfileRequest(
    string? Sector = null,
    bool? HasBoiler = null,
    bool? HasGenset = null,
    bool? HasProductionFloor = null,
    bool? GridElectricity = null,
    string? BuildingOwnership = null,
    decimal? RoofAreaSqft = null,
    string? BudgetBand = null,
    decimal? MaxBudgetBdt = null,
    decimal? BaselineMonthlyKwh = null,
    decimal? BaselineMonthlyDieselLitres = null,
    decimal? BaselineMonthlyGasM3 = null,
    decimal? ElectricityTariffBdtPerKwh = null,
    decimal? DieselTariffBdtPerLitre = null,
    decimal? GasTariffBdtPerM3 = null);

public record RecommendationDto(
    Guid Id,
    Guid MeasureId,
    string MeasureCode,
    string NameEn,
    string NameBn,
    string Category,
    string EvidenceGrade,
    string? SourceCitation,
    int Rank,
    string Status,
    string? StatusReason,
    bool NeedsEnergyAudit,
    decimal PaybackYears,
    decimal CostPerTco2e,
    decimal CompositeScore,
    decimal Tco2eAvoidedLow,
    decimal Tco2eAvoidedTypical,
    decimal Tco2eAvoidedHigh,
    decimal SavingsBdtLow,
    decimal SavingsBdtTypical,
    decimal SavingsBdtHigh,
    decimal CapexLow,
    decimal CapexHigh,
    string ExplanationEn,
    string ExplanationBn,
    string FinancingNoteEn,
    string FinancingNoteBn,
    string AssumptionsEn,
    string AssumptionsBn,
    string NextStepEn,
    string NextStepBn,
    decimal? RealisedSavingBdt,
    decimal? RealisedTco2eAvoided,
    DateTime? CompletedAtUtc,
    bool EvidenceUpgradeRecorded,
    string? EvidenceNotes);

public record RecommendationsSummaryDto(
    int TotalCount,
    decimal TotalTypicalTco2eAvoided,
    decimal TotalTypicalSavingsBdt,
    int PlannedCount,
    int DoneCount);

public record RecommendationsResponse(
    List<RecommendationDto> Top5,
    List<RecommendationDto> All,
    RecommendationsSummaryDto Summary);

public class RecommendationsService(
    RecommendationsDbContext dbContext,
    IRecommendationPipeline pipeline,
    ILogger<RecommendationsService> logger,
    CarbonBill.SharedKernel.Providers.ILangSmithTracer? langSmithTracer = null)
{
    public async Task<FacilityProfile> GetOrCreateProfileAsync(
        Guid orgId,
        Guid? siteId = null,
        CancellationToken ct = default)
    {
        var profile = await dbContext.FacilityProfiles
            .FirstOrDefaultAsync(p => p.OrgId == orgId, ct);

        if (profile == null)
        {
            profile = new FacilityProfile
            {
                OrgId = orgId,
                SiteId = siteId,
                Sector = "RMG",
                HasBoiler = true,
                HasGenset = true,
                HasProductionFloor = true,
                GridElectricity = true,
                BuildingOwnership = "Owned",
                RoofAreaSqft = 25000m,
                BudgetBand = "Medium",
                MaxBudgetBdt = 3000000m,
                BaselineMonthlyKwh = 30000m,
                BaselineMonthlyDieselLitres = 1200m,
                BaselineMonthlyGasM3 = 500m,
                ElectricityTariffBdtPerKwh = 10.50m,
                DieselTariffBdtPerLitre = 108.00m,
                GasTariffBdtPerM3 = 30.00m
            };

            dbContext.FacilityProfiles.Add(profile);
            await dbContext.SaveChangesAsync(ct);
        }

        return profile;
    }

    public async Task<FacilityProfile> SaveFacilityProfileAsync(
        Guid orgId,
        UpdateFacilityProfileRequest request,
        CancellationToken ct = default)
    {
        var profile = await GetOrCreateProfileAsync(orgId, null, ct);

        if (!string.IsNullOrWhiteSpace(request.Sector)) profile.Sector = request.Sector.Trim();
        if (request.HasBoiler.HasValue) profile.HasBoiler = request.HasBoiler.Value;
        if (request.HasGenset.HasValue) profile.HasGenset = request.HasGenset.Value;
        if (request.HasProductionFloor.HasValue) profile.HasProductionFloor = request.HasProductionFloor.Value;
        if (request.GridElectricity.HasValue) profile.GridElectricity = request.GridElectricity.Value;
        if (!string.IsNullOrWhiteSpace(request.BuildingOwnership)) profile.BuildingOwnership = request.BuildingOwnership.Trim();
        if (request.RoofAreaSqft.HasValue) profile.RoofAreaSqft = request.RoofAreaSqft.Value;
        if (!string.IsNullOrWhiteSpace(request.BudgetBand)) profile.BudgetBand = request.BudgetBand.Trim();
        if (request.MaxBudgetBdt.HasValue) profile.MaxBudgetBdt = request.MaxBudgetBdt.Value;
        if (request.BaselineMonthlyKwh.HasValue) profile.BaselineMonthlyKwh = request.BaselineMonthlyKwh.Value;
        if (request.BaselineMonthlyDieselLitres.HasValue) profile.BaselineMonthlyDieselLitres = request.BaselineMonthlyDieselLitres.Value;
        if (request.BaselineMonthlyGasM3.HasValue) profile.BaselineMonthlyGasM3 = request.BaselineMonthlyGasM3.Value;
        if (request.ElectricityTariffBdtPerKwh.HasValue) profile.ElectricityTariffBdtPerKwh = request.ElectricityTariffBdtPerKwh.Value;
        if (request.DieselTariffBdtPerLitre.HasValue) profile.DieselTariffBdtPerLitre = request.DieselTariffBdtPerLitre.Value;
        if (request.GasTariffBdtPerM3.HasValue) profile.GasTariffBdtPerM3 = request.GasTariffBdtPerM3.Value;

        profile.MarkUpdated();
        await dbContext.SaveChangesAsync(ct);

        // Recalculate recommendations immediately on profile change
        await GenerateAndSaveRecommendationsAsync(orgId, null, ct);

        return profile;
    }

    public async Task<List<Recommendation>> GenerateAndSaveRecommendationsAsync(
        Guid orgId,
        Guid? siteId = null,
        CancellationToken ct = default)
    {
        var profile = await GetOrCreateProfileAsync(orgId, siteId, ct);

        var measures = await dbContext.Measures
            .Include(m => m.Source)
            .ToListAsync(ct);

        if (measures.Count == 0)
        {
            logger.LogWarning("No measures found in library to generate recommendations for org {OrgId}.", orgId);
            return [];
        }

        var startTimeUtc = DateTime.UtcNow;

        // Run the 8-step pipeline
        var candidateEvaluations = pipeline.Execute(profile, measures);

        // Load existing recommendations for this org to preserve user statuses (Planned, Done, NotFeasible)
        var existing = await dbContext.Recommendations
            .Where(r => r.OrgId == orgId)
            .ToDictionaryAsync(r => r.MeasureId, ct);

        var savedEntities = new List<Recommendation>();

        foreach (var eval in candidateEvaluations)
        {
            if (existing.TryGetValue(eval.Measure.Id, out var rec))
            {
                // Update calculations while preserving user state if already modified
                rec.SavingRangeCo2eJson = JsonSerializer.Serialize(new { low = eval.AvoidedTco2e.Low, typical = eval.AvoidedTco2e.Typical, high = eval.AvoidedTco2e.High });
                rec.SavingRangeBdtJson = JsonSerializer.Serialize(new { low = eval.AnnualSavingsBdt.Low, typical = eval.AnnualSavingsBdt.Typical, high = eval.AnnualSavingsBdt.High });
                rec.CapexRangeBdtJson = JsonSerializer.Serialize(new { low = eval.CapexBdt.Low, high = eval.CapexBdt.High });
                rec.PaybackYears = eval.PaybackYears;
                rec.CostPerTco2e = eval.CostPerTco2e;
                rec.CompositeScore = eval.CompositeScore;
                rec.Rank = eval.Rank;
                rec.NeedsEnergyAudit = eval.NeedsEnergyAudit;
                rec.ExplanationBn = eval.ExplanationBn;
                rec.ExplanationEn = eval.ExplanationEn;
                rec.FinancingNoteBn = eval.FinancingNoteBn;
                rec.FinancingNoteEn = eval.FinancingNoteEn;
                rec.AssumptionsBn = eval.AssumptionsBn;
                rec.AssumptionsEn = eval.AssumptionsEn;
                rec.NextStepBn = eval.NextStepBn;
                rec.NextStepEn = eval.NextStepEn;
                rec.MarkUpdated();
                savedEntities.Add(rec);
            }
            else
            {
                var newRec = new Recommendation
                {
                    OrgId = orgId,
                    SiteId = siteId,
                    MeasureId = eval.Measure.Id,
                    SavingRangeCo2eJson = JsonSerializer.Serialize(new { low = eval.AvoidedTco2e.Low, typical = eval.AvoidedTco2e.Typical, high = eval.AvoidedTco2e.High }),
                    SavingRangeBdtJson = JsonSerializer.Serialize(new { low = eval.AnnualSavingsBdt.Low, typical = eval.AnnualSavingsBdt.Typical, high = eval.AnnualSavingsBdt.High }),
                    CapexRangeBdtJson = JsonSerializer.Serialize(new { low = eval.CapexBdt.Low, high = eval.CapexBdt.High }),
                    PaybackYears = eval.PaybackYears,
                    CostPerTco2e = eval.CostPerTco2e,
                    CompositeScore = eval.CompositeScore,
                    Rank = eval.Rank,
                    Status = RecommendationStatuses.Suggested,
                    NeedsEnergyAudit = eval.NeedsEnergyAudit,
                    ExplanationBn = eval.ExplanationBn,
                    ExplanationEn = eval.ExplanationEn,
                    FinancingNoteBn = eval.FinancingNoteBn,
                    FinancingNoteEn = eval.FinancingNoteEn,
                    AssumptionsBn = eval.AssumptionsBn,
                    AssumptionsEn = eval.AssumptionsEn,
                    NextStepBn = eval.NextStepBn,
                    NextStepEn = eval.NextStepEn
                };

                dbContext.Recommendations.Add(newRec);
                savedEntities.Add(newRec);
            }
        }

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Saved {Count} recommendations for org {OrgId}.", savedEntities.Count, orgId);

        if (langSmithTracer != null)
        {
            try
            {
                var topMeasures = candidateEvaluations.Take(5).Select(e => new
                {
                    code = e.Measure.MeasureCode,
                    name = e.Measure.NameEn,
                    rank = e.Rank,
                    compositeScore = e.CompositeScore,
                    paybackYears = e.PaybackYears,
                    avoidedTco2e = e.AvoidedTco2e.Typical,
                    savingsBdt = e.AnnualSavingsBdt.Typical,
                    capexLow = e.CapexBdt.Low,
                    capexHigh = e.CapexBdt.High
                }).ToList();

                await langSmithTracer.TraceRunAsync(
                    "MaccDecarbonizationPipeline",
                    "chain",
                    new
                    {
                        orgId,
                        siteId,
                        profile = new
                        {
                            sector = profile.Sector,
                            budgetBand = profile.BudgetBand,
                            maxBudgetBdt = profile.MaxBudgetBdt,
                            baselineMonthlyKwh = profile.BaselineMonthlyKwh,
                            baselineMonthlyDieselLitres = profile.BaselineMonthlyDieselLitres,
                            baselineMonthlyGasM3 = profile.BaselineMonthlyGasM3,
                            hasBoiler = profile.HasBoiler,
                            hasGenset = profile.HasGenset
                        },
                        availableMeasuresCount = measures.Count
                    },
                    new
                    {
                        totalRecommendationsGenerated = candidateEvaluations.Count,
                        totalSavedEntities = savedEntities.Count,
                        topRecommendations = topMeasures,
                        totalAvoidedTco2eTypical = candidateEvaluations.Sum(e => e.AvoidedTco2e.Typical),
                        totalSavingsBdtTypical = candidateEvaluations.Sum(e => e.AnnualSavingsBdt.Typical)
                    },
                    startTimeUtc,
                    DateTime.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["module"] = "Recommendations",
                        ["pipeline"] = "MaccDecarbonization8Step",
                        ["orgId"] = orgId.ToString()
                    },
                    null,
                    ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to emit LangSmith trace for MACC recommendations.");
            }
        }

        return savedEntities;
    }

    public async Task<RecommendationsResponse> GetRecommendationsResponseAsync(
        Guid orgId,
        Guid? siteId = null,
        CancellationToken ct = default)
    {
        var recommendations = await dbContext.Recommendations
            .Include(r => r.Measure)
            .ThenInclude(m => m.Source)
            .Where(r => r.OrgId == orgId)
            .OrderBy(r => r.Rank)
            .ToListAsync(ct);

        if (recommendations.Count == 0)
        {
            // Auto-generate if empty
            await GenerateAndSaveRecommendationsAsync(orgId, siteId, ct);

            recommendations = await dbContext.Recommendations
                .Include(r => r.Measure)
                .ThenInclude(m => m.Source)
                .Where(r => r.OrgId == orgId)
                .OrderBy(r => r.Rank)
                .ToListAsync(ct);
        }

        var dtoList = recommendations.Select(MapToDto).ToList();
        var top5 = dtoList.Take(5).ToList();

        decimal totalTypicalTco2e = 0m;
        decimal totalTypicalBdt = 0m;
        int plannedCount = 0;
        int doneCount = 0;

        foreach (var r in dtoList)
        {
            totalTypicalTco2e += r.Tco2eAvoidedTypical;
            totalTypicalBdt += r.SavingsBdtTypical;
            if (r.Status == RecommendationStatuses.Planned) plannedCount++;
            if (r.Status == RecommendationStatuses.Done) doneCount++;
        }

        var summary = new RecommendationsSummaryDto(
            TotalCount: dtoList.Count,
            TotalTypicalTco2eAvoided: Math.Round(totalTypicalTco2e, 2),
            TotalTypicalSavingsBdt: Math.Round(totalTypicalBdt, 2),
            PlannedCount: plannedCount,
            DoneCount: doneCount);

        return new RecommendationsResponse(top5, dtoList, summary);
    }

    public async Task<RecommendationDto> UpdateStatusAsync(
        Guid orgId,
        Guid recommendationId,
        StatusUpdateRequest request,
        CancellationToken ct = default)
    {
        var rec = await dbContext.Recommendations
            .Include(r => r.Measure)
            .ThenInclude(m => m.Source)
            .FirstOrDefaultAsync(r => r.Id == recommendationId && r.OrgId == orgId, ct);

        if (rec == null)
        {
            throw new KeyNotFoundException($"Recommendation '{recommendationId}' not found for org '{orgId}'.");
        }

        var profile = await GetOrCreateProfileAsync(orgId, rec.SiteId, ct);

        pipeline.CloseTheLoop.UpdateStatus(rec, request, profile);

        await dbContext.SaveChangesAsync(ct);

        return MapToDto(rec);
    }

    private static RecommendationDto MapToDto(Recommendation r)
    {
        var m = r.Measure;

        // Parse JSON ranges
        decimal co2eLow = 0, co2eTyp = 0, co2eHigh = 0;
        if (!string.IsNullOrWhiteSpace(r.SavingRangeCo2eJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(r.SavingRangeCo2eJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("low", out var l)) co2eLow = l.GetDecimal();
                if (root.TryGetProperty("typical", out var t)) co2eTyp = t.GetDecimal();
                if (root.TryGetProperty("high", out var h)) co2eHigh = h.GetDecimal();
            }
            catch (JsonException) { }
        }

        decimal bdtLow = 0, bdtTyp = 0, bdtHigh = 0;
        if (!string.IsNullOrWhiteSpace(r.SavingRangeBdtJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(r.SavingRangeBdtJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("low", out var l)) bdtLow = l.GetDecimal();
                if (root.TryGetProperty("typical", out var t)) bdtTyp = t.GetDecimal();
                if (root.TryGetProperty("high", out var h)) bdtHigh = h.GetDecimal();
            }
            catch (JsonException) { }
        }

        decimal capexLow = 0, capexHigh = 0;
        if (!string.IsNullOrWhiteSpace(r.CapexRangeBdtJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(r.CapexRangeBdtJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("low", out var l)) capexLow = l.GetDecimal();
                if (root.TryGetProperty("high", out var h)) capexHigh = h.GetDecimal();
            }
            catch (JsonException) { }
        }

        return new RecommendationDto(
            Id: r.Id,
            MeasureId: r.MeasureId,
            MeasureCode: m?.MeasureCode ?? string.Empty,
            NameEn: m?.NameEn ?? string.Empty,
            NameBn: m?.NameBn ?? string.Empty,
            Category: m?.Category ?? string.Empty,
            EvidenceGrade: m?.EvidenceGrade ?? "B",
            SourceCitation: m?.Source?.Title,
            Rank: r.Rank,
            Status: r.Status,
            StatusReason: r.StatusReason,
            NeedsEnergyAudit: r.NeedsEnergyAudit,
            PaybackYears: r.PaybackYears,
            CostPerTco2e: r.CostPerTco2e,
            CompositeScore: r.CompositeScore,
            Tco2eAvoidedLow: co2eLow,
            Tco2eAvoidedTypical: co2eTyp,
            Tco2eAvoidedHigh: co2eHigh,
            SavingsBdtLow: bdtLow,
            SavingsBdtTypical: bdtTyp,
            SavingsBdtHigh: bdtHigh,
            CapexLow: capexLow,
            CapexHigh: capexHigh,
            ExplanationEn: r.ExplanationEn,
            ExplanationBn: r.ExplanationBn,
            FinancingNoteEn: r.FinancingNoteEn,
            FinancingNoteBn: r.FinancingNoteBn,
            AssumptionsEn: r.AssumptionsEn,
            AssumptionsBn: r.AssumptionsBn,
            NextStepEn: r.NextStepEn,
            NextStepBn: r.NextStepBn,
            RealisedSavingBdt: r.RealisedSavingBdt,
            RealisedTco2eAvoided: r.RealisedTco2eAvoided,
            CompletedAtUtc: r.CompletedAtUtc,
            EvidenceUpgradeRecorded: r.EvidenceUpgradeRecorded,
            EvidenceNotes: r.EvidenceNotes);
    }
}
