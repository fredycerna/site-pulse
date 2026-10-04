namespace SitePulse.Api.Baseline;

/// <summary>
/// Orchestrates one dashboard read: account and timezone, the 9 week windows, one aggregation
/// query, then the statistics per series. Holds no SQL and no math of its own.
/// </summary>
public sealed class BaselineService(IActivityQueries queries, TimeProvider clock)
{
    public async Task<BaselineOutcome> GetAsync(int accountId, string eventType, DateOnly? week, CancellationToken ct = default)
    {
        var account = await queries.FindAccountAsync(accountId, ct);
        if (account is null) return new BaselineOutcome.AccountNotFound();
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(account.Timezone, out var tz))
            return new BaselineOutcome.UnusableTimezone(account.Timezone);

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        if (week is { } requested && WeekWindowCalculator.ValidateRequestedWeek(tz, nowUtc, requested) is { } reason)
            return new BaselineOutcome.InvalidWeek(reason);

        // Same rule as the week picker: an account with data cannot be judged before its first complete week.
        // An account without events has no such week and keeps answering no_data.
        var availableWeeks = AvailableWeeks(account.FirstEventUtc, tz, nowUtc);
        if (week is { } early && availableWeeks.Count > 0 && early < availableWeeks[0])
            return new BaselineOutcome.InvalidWeek(
                $"week {early:yyyy-MM-dd} is before this account's first complete week with data, {availableWeeks[0]:yyyy-MM-dd}.");

        var windows = WeekWindowCalculator.Compute(tz, nowUtc, week);
        var activity = await queries.GetWeeklyActivityAsync(account.Id, windows, ct);

        var accountFirstEvent = activity.Locations.Count == 0 ? (DateTime?)null : activity.Locations.Min(l => l.FirstEventUtc);
        var metrics = EventTypes.Accepted
            .Select(type => accountFirstEvent is { } first
                ? Metric(type, Series(activity.Locations, type, windows.Count), first, windows)
                : NoDataMetric(type, windows))
            .ToList();
        var locations = OrderByAttention(activity.Locations
            .Select(l => Location(l.Location, Series([l], eventType, windows.Count), l.FirstEventUtc, windows)));

        var current = windows[^1];
        return new BaselineOutcome.Found(new ActivityBaselineResponse(
            new AccountSummary(account.Id, account.Name, account.Timezone),
            new DateRange(current.WeekStart, current.WeekStart.AddDays(7)),
            new BaselineRange(windows[0].WeekStart, current.WeekStart, windows.Count - 1),
            eventType,
            nowUtc,
            availableWeeks,
            metrics,
            locations,
            new DataNotes(activity.DuplicateRowsIgnored)));
    }

    /// <summary>Out of range first, largest deviation first; then typical; then insufficient history. Name breaks ties.</summary>
    public static IReadOnlyList<LocationBaseline> OrderByAttention(IEnumerable<LocationBaseline> locations) =>
        locations
            .OrderBy(l => l.Status switch
            {
                BaselineStatus.Above or BaselineStatus.Below => 0,
                BaselineStatus.Typical => 1,
                _ => 2,
            })
            .ThenByDescending(l => l.Status is BaselineStatus.Above or BaselineStatus.Below ? Math.Abs(l.Deviation) : 0)
            .ThenBy(l => l.Location, StringComparer.Ordinal)
            .ToList();

    /// <summary>Counts per window, zero where the query returned nothing.</summary>
    private static int[] Series(IEnumerable<LocationActivity> locations, string eventType, int windowCount)
    {
        var series = new int[windowCount];
        foreach (var cell in locations.SelectMany(l => l.Counts))
        {
            if (eventType == EventTypes.All || cell.EventType == eventType) series[cell.WeekIndex] += cell.Events;
        }
        return series;
    }

    /// <summary>
    /// Judges the last window against the earlier ones. A baseline week is history only if it
    /// starts at or after the first event: the partial week of the first event is left out.
    /// </summary>
    private static (BaselineResult Result, IReadOnlyList<int> BaselineIndices) Judge(
        int[] series, DateTime firstEventUtc, IReadOnlyList<WeekWindow> windows)
    {
        var indices = Enumerable.Range(0, windows.Count - 1).Where(i => windows[i].StartUtc >= firstEventUtc).ToList();
        return (BaselineStats.Evaluate(indices.Select(i => series[i]).ToList(), series[^1]), indices);
    }

    private static MetricBaseline Metric(string eventType, int[] series, DateTime firstEventUtc, IReadOnlyList<WeekWindow> windows)
    {
        var (r, indices) = Judge(series, firstEventUtc, windows);
        var unusual = r.UnusualBaselineIndices.Select(i => indices[i])
            .Select(i => new WeekCount(windows[i].WeekStart, series[i])).ToList();
        return new MetricBaseline(eventType, series[^1], r.Median, r.TypicalLow, r.TypicalHigh, r.Status, r.HistoryWeeks,
            History(series, windows, indices), unusual);
    }

    private static MetricBaseline NoDataMetric(string eventType, IReadOnlyList<WeekWindow> windows) =>
        new(eventType, 0, null, null, null, BaselineStatus.NoData, 0, History(new int[windows.Count], windows, []), []);

    private static LocationBaseline Location(string name, int[] series, DateTime firstEventUtc, IReadOnlyList<WeekWindow> windows)
    {
        var (r, indices) = Judge(series, firstEventUtc, windows);
        return new LocationBaseline(name, series[^1], r.Median, r.TypicalLow, r.TypicalHigh, r.Status, r.HistoryWeeks,
            Math.Round(r.Deviation, 2), History(series, windows, indices));
    }

    /// <param name="baselineIndices">The windows Judge counted as history, so InBaseline and HistoryWeeks cannot disagree.</param>
    private static IReadOnlyList<HistoryWeek> History(int[] series, IReadOnlyList<WeekWindow> windows, IReadOnlyList<int> baselineIndices) =>
        windows.Select((w, i) => new HistoryWeek(w.WeekStart, series[i], baselineIndices.Contains(i))).ToList();

    /// <summary>
    /// Local Mondays from the first complete week to the last complete week. Same rule as history: a week
    /// counts if it starts at or after the first event, so the partial week of the first event is not offered,
    /// and neither is the current week.
    /// </summary>
    private static IReadOnlyList<DateOnly> AvailableWeeks(DateTime? firstEventUtc, TimeZoneInfo tz, DateTime nowUtc)
    {
        if (firstEventUtc is not { } first) return [];
        var from = WeekWindowCalculator.MondayOf(DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(first, DateTimeKind.Utc), tz)));
        if (WeekWindowCalculator.WindowFor(tz, from).StartUtc < first) from = from.AddDays(7);
        var to = WeekWindowCalculator.LastCompleteWeek(tz, nowUtc);
        var weeks = new List<DateOnly>();
        for (var monday = from; monday <= to; monday = monday.AddDays(7)) weeks.Add(monday);
        return weeks;
    }
}
