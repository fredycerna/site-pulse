namespace SitePulse.Api.Baseline;

public sealed record AccountRecord(int Id, string Name, string Timezone, DateTime? FirstEventUtc);

public sealed record WeekTypeCount(string EventType, int WeekIndex, int Events);

/// <param name="FirstEventUtc">The location's first event ever (any type), before the end of the last window.</param>
/// <param name="Counts">Only non-zero cells; WeekIndex points into the windows passed to the query.</param>
public sealed record LocationActivity(string Location, DateTime FirstEventUtc, IReadOnlyList<WeekTypeCount> Counts);

/// <param name="DuplicateRowsIgnored">Raw rows minus deduplicated rows inside the windows.</param>
public sealed record WeeklyActivity(IReadOnlyList<LocationActivity> Locations, int DuplicateRowsIgnored);

/// <summary>The only place with SQL for the dashboard. Every method is scoped by account.</summary>
public interface IActivityQueries
{
    Task<AccountRecord?> FindAccountAsync(int accountId, CancellationToken ct = default);

    Task<WeeklyActivity> GetWeeklyActivityAsync(int accountId, IReadOnlyList<WeekWindow> windows, CancellationToken ct = default);
}
