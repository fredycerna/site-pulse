namespace SitePulse.Api.Baseline;

public static class EventTypes
{
    public const string All = "all";
    public static readonly IReadOnlyList<string> Recorded = ["call_received", "lead_created", "appointment_set"];
    public static readonly IReadOnlyList<string> Accepted = [All, .. Recorded];
}

// Response of GET /api/accounts/{id}/activity-baseline (PLAN.md §6, "API contract").
// Dates are local to the account; ranges are half-open, so End is the Monday after the last day.

public sealed record ActivityBaselineResponse(
    AccountSummary Account,
    DateRange Week,
    BaselineRange Baseline,
    string EventType,
    DateTime NowUtc,
    IReadOnlyList<DateOnly> AvailableWeeks,
    IReadOnlyList<MetricBaseline> Metrics,
    IReadOnlyList<LocationBaseline> Locations,
    DataNotes DataNotes);

public sealed record DateRange(DateOnly Start, DateOnly End);

public sealed record BaselineRange(DateOnly Start, DateOnly End, int Weeks);

public sealed record WeekCount(DateOnly WeekStart, int Count);

/// <summary>
/// Median and limits are null when Status is insufficient_history or no_data. HistoryWeeks (0–8) is how many
/// baseline weeks counted as history; fewer than 4 is insufficient_history.
/// </summary>
public sealed record MetricBaseline(
    string EventType,
    int Current,
    double? Median,
    int? TypicalLow,
    int? TypicalHigh,
    BaselineStatus Status,
    int HistoryWeeks,
    IReadOnlyList<WeekCount> History,
    IReadOnlyList<WeekCount> UnusualBaselineWeeks);

public sealed record LocationBaseline(
    string Location,
    int Current,
    double? Median,
    int? TypicalLow,
    int? TypicalHigh,
    BaselineStatus Status,
    int HistoryWeeks,
    double Deviation,
    IReadOnlyList<WeekCount> History);

public sealed record DataNotes(int DuplicateRowsIgnored);

public abstract record BaselineOutcome
{
    public sealed record Found(ActivityBaselineResponse Response) : BaselineOutcome;
    public sealed record AccountNotFound : BaselineOutcome;
    public sealed record InvalidWeek(string Reason) : BaselineOutcome;
    public sealed record UnusableTimezone(string Timezone) : BaselineOutcome;
}
