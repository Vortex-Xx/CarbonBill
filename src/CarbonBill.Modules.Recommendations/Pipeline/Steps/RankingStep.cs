namespace CarbonBill.Modules.Recommendations.Pipeline.Steps;

public class RankingStep(
    decimal wPayback = 0.35m,
    decimal wImpact = 0.35m,
    decimal wGrade = 0.20m,
    decimal wCapex = 0.10m)
{
    private readonly decimal _wPayback = wPayback;
    private readonly decimal _wImpact = wImpact;
    private readonly decimal _wGrade = wGrade;
    private readonly decimal _wCapex = wCapex;

    public void Rank(List<CandidateMeasureEvaluation> candidates)
    {
        foreach (var c in candidates)
        {
            // 1. Payback score (0 to 10): 0-1 yr = 9-10, 5 yrs = 5, >=10 yrs = 0
            decimal paybackScore = Math.Clamp(10.0m - c.PaybackYears, 0.0m, 10.0m);

            // 2. Impact score (0 to 10): higher tCO2e avoided is better
            decimal impactScore = Math.Clamp(c.AvoidedTco2e.Typical / 10.0m, 0.0m, 10.0m);

            // 3. Evidence grade score: A = 10, B = 7, C = 4
            decimal gradeScore = c.Measure.EvidenceGrade.ToUpperInvariant() switch
            {
                "A" => 10.0m,
                "B" => 7.0m,
                _ => 4.0m
            };

            // 4. Capex penalty score (0 to 10)
            decimal avgCapex = (c.CapexBdt.Low + c.CapexBdt.High) / 2.0m;
            decimal capexScore = Math.Clamp(avgCapex / 200000.0m, 0.0m, 10.0m);

            decimal composite = (_wPayback * paybackScore) +
                                (_wImpact * impactScore) +
                                (_wGrade * gradeScore) -
                                (_wCapex * capexScore);

            c.CompositeScore = Math.Round(composite, 4);
        }

        // Realistic options first, then highest composite score
        var sorted = candidates
            .OrderByDescending(c => c.IsRealistic)
            .ThenByDescending(c => c.CompositeScore)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].Rank = i + 1;
        }
    }
}
