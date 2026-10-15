using CarbonBill.Modules.Recommendations.Domain;

namespace CarbonBill.Modules.Recommendations.Pipeline.Steps;

public record StatusUpdateRequest(
    string Status,
    string? Reason = null,
    decimal? PostInterventionMonthlyEnergyUnits = null,
    decimal? RealisedSavingBdt = null);

public class CloseTheLoopStep
{
    public void UpdateStatus(
        Recommendation recommendation,
        StatusUpdateRequest request,
        FacilityProfile profile,
        decimal? customRealisedBdt = null)
    {
        var newStatus = request.Status.Trim();

        if (newStatus.Equals(RecommendationStatuses.NotFeasible, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new InvalidOperationException("A non-empty reason is required when marking a recommendation as NotFeasible.");
            }

            recommendation.Status = RecommendationStatuses.NotFeasible;
            recommendation.StatusReason = request.Reason.Trim();
            recommendation.MarkUpdated();
            return;
        }

        if (newStatus.Equals(RecommendationStatuses.Planned, StringComparison.OrdinalIgnoreCase))
        {
            recommendation.Status = RecommendationStatuses.Planned;
            recommendation.StatusReason = request.Reason;
            recommendation.MarkUpdated();
            return;
        }

        if (newStatus.Equals(RecommendationStatuses.Done, StringComparison.OrdinalIgnoreCase))
        {
            recommendation.Status = RecommendationStatuses.Done;
            recommendation.StatusReason = request.Reason;
            recommendation.CompletedAtUtc = DateTime.UtcNow;

            // Closed-loop post-implementation bill comparison
            decimal? realisedBdt = customRealisedBdt ?? request.RealisedSavingBdt;

            if (realisedBdt.HasValue)
            {
                recommendation.RealisedSavingBdt = realisedBdt.Value;
                // Estimate realized tCO2e based on tariff
                if (profile.ElectricityTariffBdtPerKwh > 0)
                {
                    decimal kwhSaved = realisedBdt.Value / profile.ElectricityTariffBdtPerKwh;
                    recommendation.RealisedTco2eAvoided = Math.Round((kwhSaved * 0.620000m) / 1000.0m, 3);
                }
            }
            else if (request.PostInterventionMonthlyEnergyUnits.HasValue)
            {
                decimal baselineMonthly = profile.BaselineMonthlyKwh;
                if (recommendation.Measure != null &&
                    (recommendation.Measure.Category.Contains("Diesel", StringComparison.OrdinalIgnoreCase) ||
                     recommendation.Measure.Category.Contains("Generator", StringComparison.OrdinalIgnoreCase)))
                {
                    baselineMonthly = profile.BaselineMonthlyDieselLitres;
                }

                decimal diffMonthly = baselineMonthly - request.PostInterventionMonthlyEnergyUnits.Value;
                if (diffMonthly > 0)
                {
                    decimal annualSavedUnits = diffMonthly * 12.0m;
                    decimal tariff = profile.ElectricityTariffBdtPerKwh;
                    decimal factor = 0.620000m;

                    if (recommendation.Measure != null &&
                        recommendation.Measure.Category.Contains("Diesel", StringComparison.OrdinalIgnoreCase))
                    {
                        tariff = profile.DieselTariffBdtPerLitre;
                        factor = 2.680000m;
                    }

                    recommendation.RealisedSavingBdt = Math.Round(annualSavedUnits * tariff, 2);
                    recommendation.RealisedTco2eAvoided = Math.Round((annualSavedUnits * factor) / 1000.0m, 3);
                }
            }

            // Upgrade measure evidence over time (record only, do not auto-edit the library)
            recommendation.EvidenceUpgradeRecorded = true;
            recommendation.EvidenceNotes = $"Empirical outcome recorded: {recommendation.RealisedSavingBdt ?? 0m:N2} BDT/year, {recommendation.RealisedTco2eAvoided ?? 0m:N3} tCO2e/year. Candidate for library evidence grade upgrade review.";

            recommendation.MarkUpdated();
            return;
        }

        throw new ArgumentException($"Invalid status '{request.Status}'. Allowed statuses: Suggested, Planned, Done, NotFeasible.");
    }
}
