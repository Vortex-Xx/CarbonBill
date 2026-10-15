using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.GapDetection.Domain;

public class MissingAlert : AggregateRoot, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid SiteId { get; set; }
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty; // e.g. "2026-09"
    public DateTime DueDate { get; set; }
    public Guid? ResponsibleUserId { get; set; }

    public string Status { get; set; } = AlertStatuses.Open;
    public string EscalationState { get; set; } = EscalationStates.None;
    public string Severity { get; set; } = AlertSeverities.Amber;

    // Plain request messages & i18n keys for floor staff & compliance
    public string RequestMessageKey { get; set; } = string.Empty;
    public string PlainRequestMessageBn { get; set; } = string.Empty;
    public string PlainRequestMessageEn { get; set; } = string.Empty;

    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? ResolvedDocumentId { get; set; }
    public DateTime? LastNudgedAtUtc { get; set; }
    public int NudgeCount { get; set; }

    public void EvaluateEscalation(DateTime currentDate)
    {
        if (Status == AlertStatuses.Resolved)
        {
            return;
        }

        // Days until due date
        var daysUntilDue = (DueDate.Date - currentDate.Date).TotalDays;

        if (daysUntilDue <= 0)
        {
            // Day 0 and overdue: Escalated to Compliance Officer / Factory Owner, Red severity
            EscalationState = EscalationStates.DayZero;
            Severity = AlertSeverities.Red;
            Status = AlertStatuses.Escalated;
        }
        else if (daysUntilDue <= 3)
        {
            // Day -3: Push to responsible user, Amber severity
            EscalationState = EscalationStates.DayMinus3;
            Severity = AlertSeverities.Amber;
            Status = AlertStatuses.Reminded;
        }
        else if (daysUntilDue <= 7)
        {
            // Day -7: Calendar reminder, Amber severity
            EscalationState = EscalationStates.DayMinus7;
            Severity = AlertSeverities.Amber;
            Status = AlertStatuses.Reminded;
        }
        else
        {
            // Normal state before Day -7
            EscalationState = EscalationStates.None;
            Severity = AlertSeverities.Amber;
            Status = AlertStatuses.Open;
        }

        UpdatedAtUtc = currentDate;
    }

    public void Resolve(Guid documentId, DateTime resolvedAtUtc)
    {
        Status = AlertStatuses.Resolved;
        ResolvedDocumentId = documentId;
        ResolvedAtUtc = resolvedAtUtc;
        UpdatedAtUtc = resolvedAtUtc;
    }

    public void Nudge(DateTime nudgedAtUtc)
    {
        NudgeCount++;
        LastNudgedAtUtc = nudgedAtUtc;
        UpdatedAtUtc = nudgedAtUtc;
    }

    public static (string Bn, string En, string Key) FormatRequestMessage(string docType, string assetName)
    {
        var (docBn, docEn, keySuffix) = docType.ToLowerInvariant() switch
        {
            "dieselslip" or "diesel" => ("ডিজেল স্লিপ", "diesel slip", "diesel"),
            "electricitybill" or "electricity" => ("বিদ্যুৎ বিল", "electricity bill", "electricity"),
            "gasbill" or "gas" => ("গ্যাস বিল", "gas bill", "gas"),
            "lpgbill" or "lpg" => ("এলপিজি চালান", "LPG invoice", "lpg"),
            "freightchallan" or "freight" => ("পরিবহন চালান", "transport challan", "freight"),
            _ => ($"{docType} চালান", $"{docType} slip", "generic")
        };

        var assetBn = ToBanglaAssetName(assetName);
        var bn = $"অনুগ্রহ করে {assetBn}-এর {docBn} পাঠান";
        var en = $"Please send {docEn} for {assetName}";
        var key = $"gap.request.{keySuffix}";

        return (bn, en, key);
    }

    public static string ToBanglaAssetName(string name)
    {
        var result = name
            .Replace("Generator", "জেনারেটর")
            .Replace("Boiler", "বয়লার")
            .Replace("Main Grid Meter", "মূল গ্রিড মিটার")
            .Replace("Meter", "মিটার");

        return result
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
