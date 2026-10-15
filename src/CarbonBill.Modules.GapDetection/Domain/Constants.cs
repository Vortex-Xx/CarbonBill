namespace CarbonBill.Modules.GapDetection.Domain;

public static class EscalationStates
{
    public const string None = "None";
    public const string DayMinus7 = "DayMinus7";
    public const string DayMinus3 = "DayMinus3";
    public const string DayZero = "DayZero";

    public static readonly IReadOnlyList<string> All = [None, DayMinus7, DayMinus3, DayZero];
}

public static class AlertSeverities
{
    public const string Amber = "Amber";
    public const string Red = "Red";

    public static readonly IReadOnlyList<string> All = [Amber, Red];
}

public static class AlertStatuses
{
    public const string Open = "Open";
    public const string Reminded = "Reminded";
    public const string Escalated = "Escalated";
    public const string Resolved = "Resolved";

    public static readonly IReadOnlyList<string> All = [Open, Reminded, Escalated, Resolved];
}
