using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Tenancy;

namespace CarbonBill.Modules.Flags.Domain;

public static class FlagFamilies
{
    public const string DataQuality = "DataQuality";
    public const string Footprint = "Footprint";
    public const string BuyerReadiness = "BuyerReadiness";
    public const string BillSavings = "BillSavings";

    public static readonly IReadOnlyList<string> All = [DataQuality, Footprint, BuyerReadiness, BillSavings];
}

public static class FlagSeverities
{
    public const string Info = "Info";
    public const string Amber = "Amber";
    public const string Red = "Red";

    public static readonly IReadOnlyList<string> All = [Info, Amber, Red];
}

public static class FlagStates
{
    public const string Open = "Open";
    public const string Acknowledged = "Acknowledged";
    public const string Resolved = "Resolved";
    public const string Dismissed = "Dismissed";

    public static readonly IReadOnlyList<string> All = [Open, Acknowledged, Resolved, Dismissed];
}

public class FlagRule : BaseEntity
{
    public string FlagCode { get; set; } = string.Empty;
    public string Family { get; set; } = FlagFamilies.DataQuality;
    public string NameBn { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string DefaultSeverity { get; set; } = FlagSeverities.Amber;
    public string TriggerParametersJson { get; set; } = "{}";
    public string SuggestedActionBn { get; set; } = string.Empty;
    public string SuggestedActionEn { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Flag : AggregateRoot, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid? SiteId { get; set; }
    public Guid RuleId { get; set; }
    public FlagRule? Rule { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Family { get; set; } = FlagFamilies.DataQuality;
    public string Severity { get; set; } = FlagSeverities.Amber;
    public string Period { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = "{}";
    public string ExplanationBn { get; set; } = string.Empty;
    public string ExplanationEn { get; set; } = string.Empty;
    public string SuggestedActionBn { get; set; } = string.Empty;
    public string SuggestedActionEn { get; set; } = string.Empty;
    public string State { get; set; } = FlagStates.Open;
    public string? DismissedReason { get; set; }
    public DateTime? DismissedUntil { get; set; }

    public void Acknowledge(DateTime now)
    {
        if (State == FlagStates.Open)
        {
            State = FlagStates.Acknowledged;
            UpdatedAtUtc = now;
        }
    }

    public void Dismiss(string reason, DateTime now, int expirationDays = 30)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A mandatory non-empty justification string is required to dismiss a flag.", nameof(reason));
        }

        State = FlagStates.Dismissed;
        DismissedReason = reason.Trim();
        DismissedUntil = now.AddDays(expirationDays);
        UpdatedAtUtc = now;
    }

    public void Resolve(DateTime now)
    {
        State = FlagStates.Resolved;
        UpdatedAtUtc = now;
    }

    public void Reopen(DateTime now)
    {
        State = FlagStates.Open;
        DismissedReason = null;
        DismissedUntil = null;
        UpdatedAtUtc = now;
    }

    public bool CheckAndExpireDismissal(DateTime now)
    {
        if (State == FlagStates.Dismissed && DismissedUntil.HasValue && now >= DismissedUntil.Value)
        {
            Reopen(now);
            return true;
        }

        return false;
    }
}
