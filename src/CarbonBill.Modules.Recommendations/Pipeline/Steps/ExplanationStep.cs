using System.Globalization;
using CarbonBill.Modules.Recommendations.Domain;

namespace CarbonBill.Modules.Recommendations.Pipeline.Steps;

public class ExplanationStep
{
    public void GenerateCards(CandidateMeasureEvaluation candidate, FacilityProfile profile, EnergyActivityBaseline baseline)
    {
        var m = candidate.Measure;
        string sourceCitation = m.Source?.Title ?? "Industrial Energy Efficiency Dataset";

        // 1. English Card
        string whyEn = $"Based on your facility's baseline annual consumption ({baseline.AnnualKwh:N0} kWh electricity, {baseline.AnnualDieselLitres:N0} L diesel) and actual tariff ({baseline.ElectricityTariffBdtPerKwh:F2} BDT/kWh). Expected energy reduction of {m.SavingTypical * 100m:F0}% ({m.SavingLow * 100m:F0}%-{m.SavingHigh * 100m:F0}%).";
        if (candidate.NeedsEnergyAudit)
        {
            whyEn += " Detailed site audit recommended before capital commitment.";
        }

        string financingEn = candidate.CapexBdt.High > 500000m
            ? "Eligible for IDCOL Green Transformation Fund or Bangladesh Bank concessionary refinancing (5-6% interest rate)."
            : "Direct operational budget financing recommended (short payback under 2 years).";

        string assumptionsEn = $"Assumes continuous facility operating profile, verified baseline tariffs ({baseline.ElectricityTariffBdtPerKwh:F2} BDT/kWh electricity, {baseline.DieselTariffBdtPerLitre:F2} BDT/L diesel), and standard vendor equipment lifespan of {m.LifetimeYears} years.";

        string nextStepEn = candidate.NeedsEnergyAudit
            ? "Conduct detailed investment-grade energy audit before equipment procurement and apply for IDCOL/BB green finance."
            : "Obtain quotations from qualified equipment vendors, review motor/power specifications, and prepare installation schedule.";

        // 2. Bangla Card
        string whyBn = $"আপনার কারখানার বাৎসরিক শক্তি ব্যবহার ({ToBnDigits(baseline.AnnualKwh.ToString("N0", CultureInfo.InvariantCulture))} kWh বিদ্যুৎ, {ToBnDigits(baseline.AnnualDieselLitres.ToString("N0", CultureInfo.InvariantCulture))} লিটার ডিজেল) এবং বিল ট্যারিফের উপর ভিত্তি করে। সম্ভাব্য জ্বালানি সাশ্রয় {ToBnDigits((m.SavingTypical * 100m).ToString("F0", CultureInfo.InvariantCulture))}% ({ToBnDigits((m.SavingLow * 100m).ToString("F0", CultureInfo.InvariantCulture))}%-{ToBnDigits((m.SavingHigh * 100m).ToString("F0", CultureInfo.InvariantCulture))}%)।";
        if (candidate.NeedsEnergyAudit)
        {
            whyBn += " মূলধন বিনিয়োগের পূর্বে বিস্তারিত সাইট এনার্জি অডিট সুপারিশ করা হচ্ছে।";
        }

        string financingBn = candidate.CapexBdt.High > 500000m
            ? "ইডকল (IDCOL) গ্রিন ফাইন্যান্সিং বা বাংলাদেশ ব্যাংক গ্রিন ট্রান্সফরমেশন ফান্ডের সহজ শর্তের ঋণের উপযোগী (সুদহার ৫-৬%)।"
            : "স্বল্পমেয়াদী অপারেটিং বাজেট থেকে বাস্তবায়নযোগ্য (২ বছরের কম পে-ব্যাক)।";

        string assumptionsBn = $"কারখানার স্বাভাবিক পরিচালন সময়, যাচাইকৃত ইউটিলিটি ট্যারিফ ({ToBnDigits(baseline.ElectricityTariffBdtPerKwh.ToString("F2", CultureInfo.InvariantCulture))} টাকা/kWh বিদ্যুৎ, {ToBnDigits(baseline.DieselTariffBdtPerLitre.ToString("F2", CultureInfo.InvariantCulture))} টাকা/লিটার ডিজেল) এবং সরবরাহকারীর স্ট্যান্ডার্ড {ToBnDigits(m.LifetimeYears.ToString(CultureInfo.InvariantCulture))} বছরের ওয়ারেন্টির উপর ভিত্তি করে প্রাক্কলন।";

        string nextStepBn = candidate.NeedsEnergyAudit
            ? "যন্ত্রপাতি সংগ্রহের পূর্বে বিস্তারিত ইনভেস্টমেন্ট-গ্রেড এনার্জি অডিট সম্পন্ন করুন এবং গ্রিন ফাইন্যান্সিং আবেদন প্রস্তুত করুন।"
            : "যোগ্য ভেন্ডরদের কাছ থেকে কোটেশন সংগ্রহ করুন, টেকনিক্যাল স্পেসিফিকেশন যাচাই করুন এবং বাস্তবায়ন পরিকল্পনা চূড়ান্ত করুন।";

        candidate.ExplanationEn = whyEn;
        candidate.ExplanationBn = whyBn;
        candidate.FinancingNoteEn = financingEn;
        candidate.FinancingNoteBn = financingBn;
        candidate.AssumptionsEn = assumptionsEn;
        candidate.AssumptionsBn = assumptionsBn;
        candidate.NextStepEn = nextStepEn;
        candidate.NextStepBn = nextStepBn;
        candidate.SourceCitation = sourceCitation;
        candidate.EvidenceGrade = m.EvidenceGrade;
    }

    private static string ToBnDigits(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace('0', '০')
            .Replace('1', '১')
            .Replace('2', '২')
            .Replace('3', '৩')
            .Replace('4', '৪')
            .Replace('5', '৫')
            .Replace('6', '৬')
            .Replace('7', '৭')
            .Replace('8', '৮')
            .Replace('9', '৯');
    }
}
