using CarbonBill.Modules.Recommendations.Domain;

namespace CarbonBill.Modules.Recommendations.Pipeline.Steps;

public record RealismResult(bool IsRealistic, bool NeedsEnergyAudit);

public class RealismFilterStep(decimal auditThresholdBdt = 1000000.0m)
{
    private readonly decimal _auditThresholdBdt = auditThresholdBdt;

    public RealismResult Evaluate(Measure measure, FacilityProfile profile, FinancialResult financial)
    {
        bool isRealistic = true;

        // If max budget is defined and lower bound of capex exceeds budget, mark as unrealistic for immediate cycle
        if (profile.MaxBudgetBdt.HasValue && profile.MaxBudgetBdt.Value > 0)
        {
            if (financial.CapexBdt.Low > profile.MaxBudgetBdt.Value)
            {
                isRealistic = false;
            }
        }

        // Needs detailed energy audit if capex exceeds the spend threshold (default 1,000,000 BDT)
        bool needsAudit = financial.CapexBdt.High >= _auditThresholdBdt || financial.CapexBdt.Low >= _auditThresholdBdt;

        return new RealismResult(isRealistic, needsAudit);
    }
}
