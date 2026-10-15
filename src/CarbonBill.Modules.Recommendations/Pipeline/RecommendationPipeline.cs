using CarbonBill.Modules.Recommendations.Domain;
using CarbonBill.Modules.Recommendations.Pipeline.Steps;

namespace CarbonBill.Modules.Recommendations.Pipeline;

public interface IRecommendationPipeline
{
    ProfileIngestionStep ProfileIngestion { get; }
    EligibilityFilterStep EligibilityFilter { get; }
    CarbonImpactStep CarbonImpact { get; }
    FinancialModelingStep FinancialModeling { get; }
    RealismFilterStep RealismFilter { get; }
    RankingStep Ranking { get; }
    ExplanationStep Explanation { get; }
    CloseTheLoopStep CloseTheLoop { get; }

    List<CandidateMeasureEvaluation> Execute(
        FacilityProfile profile,
        IReadOnlyList<Measure> libraryMeasures,
        decimal? billElectricityCost = null,
        decimal? billKwh = null,
        decimal? billDieselCost = null,
        decimal? billDieselLitres = null);
}

public class RecommendationPipeline(
    ProfileIngestionStep profileIngestion,
    EligibilityFilterStep eligibilityFilter,
    CarbonImpactStep carbonImpact,
    FinancialModelingStep financialModeling,
    RealismFilterStep realismFilter,
    RankingStep ranking,
    ExplanationStep explanation,
    CloseTheLoopStep closeTheLoop) : IRecommendationPipeline
{
    public ProfileIngestionStep ProfileIngestion { get; } = profileIngestion;
    public EligibilityFilterStep EligibilityFilter { get; } = eligibilityFilter;
    public CarbonImpactStep CarbonImpact { get; } = carbonImpact;
    public FinancialModelingStep FinancialModeling { get; } = financialModeling;
    public RealismFilterStep RealismFilter { get; } = realismFilter;
    public RankingStep Ranking { get; } = ranking;
    public ExplanationStep Explanation { get; } = explanation;
    public CloseTheLoopStep CloseTheLoop { get; } = closeTheLoop;

    public List<CandidateMeasureEvaluation> Execute(
        FacilityProfile profile,
        IReadOnlyList<Measure> libraryMeasures,
        decimal? billElectricityCost = null,
        decimal? billKwh = null,
        decimal? billDieselCost = null,
        decimal? billDieselLitres = null)
    {
        // 1. Profile Ingestion
        var baseline = ProfileIngestion.Execute(profile, billElectricityCost, billKwh, billDieselCost, billDieselLitres);

        var evaluations = new List<CandidateMeasureEvaluation>();

        foreach (var measure in libraryMeasures)
        {
            var candidate = new CandidateMeasureEvaluation
            {
                Measure = measure
            };

            // 2. Eligibility Filter
            candidate.IsEligible = EligibilityFilter.Evaluate(measure, profile, out var ineligibilityReason);
            candidate.IneligibilityReason = ineligibilityReason;

            if (!candidate.IsEligible)
            {
                continue;
            }

            // 3. Carbon Impact
            candidate.AvoidedTco2e = CarbonImpact.Calculate(measure, baseline);

            // 4. Financial Modeling
            var financial = FinancialModeling.Calculate(measure, baseline, candidate.AvoidedTco2e);
            candidate.AnnualSavingsBdt = financial.AnnualSavingsBdt;
            candidate.CapexBdt = financial.CapexBdt;
            candidate.PaybackYears = financial.PaybackYears;
            candidate.CostPerTco2e = financial.CostPerTco2e;
            candidate.AnnualisedCapexBdt = financial.AnnualisedCapexBdt;

            // 5. Realism Filter
            var realism = RealismFilter.Evaluate(measure, profile, financial);
            candidate.IsRealistic = realism.IsRealistic;
            candidate.NeedsEnergyAudit = realism.NeedsEnergyAudit;

            // 7. Explanation Cards
            Explanation.GenerateCards(candidate, profile, baseline);

            evaluations.Add(candidate);
        }

        // 6. Ranking (Top 5 highlighted, all ranked)
        Ranking.Rank(evaluations);

        return evaluations;
    }
}
