using SitePulse.Api.Baseline;

namespace SitePulse.Api.Tests.Baseline;

/// <summary>Service assembly over a fake query class: zero-filling, history since first event, ordering, edge states.</summary>
public class BaselineServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 27, 14, 0, 0, DateTimeKind.Utc);

    // UTC account, so window i starts at May 25 + 7i, 00:00 UTC; index 8 is the week of Jul 20.
    private static DateTime WeekStart(int index) => new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc).AddDays(7 * index);

    private sealed class FakeQueries(AccountRecord? account, WeeklyActivity activity) : IActivityQueries
    {
        public Task<AccountRecord?> FindAccountAsync(int accountId, CancellationToken ct = default) =>
            Task.FromResult(account?.Id == accountId ? account : null);

        public Task<WeeklyActivity> GetWeeklyActivityAsync(int accountId, IReadOnlyList<WeekWindow> windows, CancellationToken ct = default) =>
            Task.FromResult(activity);
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    private static LocationActivity Steady(string name, int perWeek, int current, string type = "call_received", int firstWeek = 0) =>
        new(name, WeekStart(firstWeek),
            Enumerable.Range(firstWeek, 8 - firstWeek).Select(i => new WeekTypeCount(type, i, perWeek))
                .Append(new WeekTypeCount(type, 8, current)).ToList());

    private static async Task<ActivityBaselineResponse> Found(WeeklyActivity activity, string eventType = "all",
        string timezone = "UTC", DateOnly? week = null)
    {
        var account = new AccountRecord(1, "Test", timezone,
            activity.Locations.Count == 0 ? null : activity.Locations.Min(l => l.FirstEventUtc));
        var service = new BaselineService(new FakeQueries(account, activity), new FixedClock(Now));
        var outcome = await service.GetAsync(1, eventType, week);
        return Assert.IsType<BaselineOutcome.Found>(outcome).Response;
    }

    [Fact]
    public async Task Unknown_account_is_not_found()
    {
        var service = new BaselineService(new FakeQueries(null, new([], 0)), new FixedClock(Now));

        Assert.IsType<BaselineOutcome.AccountNotFound>(await service.GetAsync(999, "all", null));
    }

    [Fact]
    public async Task Unknown_timezone_is_unusable_data()
    {
        var account = new AccountRecord(1, "Test", "Mars/Olympus_Mons", null);
        var service = new BaselineService(new FakeQueries(account, new([], 0)), new FixedClock(Now));

        var outcome = Assert.IsType<BaselineOutcome.UnusableTimezone>(await service.GetAsync(1, "all", null));
        Assert.Equal("Mars/Olympus_Mons", outcome.Timezone);
    }

    [Fact]
    public async Task Week_not_ended_yet_is_invalid()
    {
        var account = new AccountRecord(1, "Test", "UTC", WeekStart(0));
        var service = new BaselineService(new FakeQueries(account, new([], 0)), new FixedClock(Now));

        Assert.IsType<BaselineOutcome.InvalidWeek>(await service.GetAsync(1, "all", new DateOnly(2026, 7, 27)));
    }

    [Fact]
    public async Task Account_without_events_is_no_data_with_all_four_metrics()
    {
        var r = await Found(new([], 0));

        Assert.Empty(r.Locations);
        Assert.Empty(r.AvailableWeeks);
        Assert.Equal(EventTypes.Accepted, r.Metrics.Select(m => m.EventType));
        Assert.All(r.Metrics, m =>
        {
            Assert.Equal(BaselineStatus.NoData, m.Status);
            Assert.Equal(0, m.HistoryWeeks);
            Assert.Null(m.Median);
            Assert.Equal(9, m.History.Count);
        });
    }

    [Fact]
    public async Task Weeks_without_events_are_filled_with_zero()
    {
        var sparse = new LocationActivity("Site A", WeekStart(0),
            [new("call_received", 0, 4), new("call_received", 3, 6), new("call_received", 8, 5)]);

        var site = Assert.Single((await Found(new([sparse], 0))).Locations);

        Assert.Equal([4, 0, 0, 6, 0, 0, 0, 0, 5], site.History.Select(h => h.Count));
        Assert.Equal(new DateOnly(2026, 5, 25), site.History[0].WeekStart);
        Assert.Equal(0, site.Median); // median of 4,0,0,6,0,0,0,0
        Assert.Equal(5, site.Current);
    }

    [Fact]
    public async Task Baseline_weeks_before_the_first_event_are_not_history()
    {
        // First event mid-week 5: weeks 6 and 7 are the only complete ones → insufficient.
        var young = new LocationActivity("Young", WeekStart(5).AddHours(30), [new("call_received", 7, 3)]);
        // First event exactly at the start of week 4: weeks 4..7 count → evaluated.
        var fourWeeks = new LocationActivity("Four weeks", WeekStart(4),
            [new("call_received", 4, 10), new("call_received", 5, 10), new("call_received", 6, 10), new("call_received", 7, 10), new("call_received", 8, 10)]);

        var r = await Found(new([young, fourWeeks], 0));

        var youngRow = r.Locations.Single(l => l.Location == "Young");
        Assert.Equal(BaselineStatus.InsufficientHistory, youngRow.Status);
        Assert.Equal(2, youngRow.HistoryWeeks);
        var evaluated = r.Locations.Single(l => l.Location == "Four weeks");
        Assert.Equal(BaselineStatus.Typical, evaluated.Status);
        Assert.Equal(4, evaluated.HistoryWeeks);
        Assert.Equal(10, evaluated.Median); // zeros in weeks 0..3 were left out
    }

    [Fact]
    public async Task Locations_use_the_requested_type_and_metrics_split_the_total()
    {
        var a = new LocationActivity("Site A", WeekStart(0),
            [new("call_received", 8, 7), new("lead_created", 8, 2), new("appointment_set", 8, 1)]);
        var b = new LocationActivity("Site B", WeekStart(0), [new("call_received", 8, 3)]);

        var r = await Found(new([a, b], 0), eventType: "lead_created");

        Assert.Equal("lead_created", r.EventType);
        Assert.Equal(2, r.Locations.Single(l => l.Location == "Site A").Current);
        Assert.Equal(0, r.Locations.Single(l => l.Location == "Site B").Current);
        var current = r.Metrics.ToDictionary(m => m.EventType, m => m.Current);
        Assert.Equal(13, current["all"]);
        Assert.Equal(current["all"], current["call_received"] + current["lead_created"] + current["appointment_set"]);
    }

    [Fact]
    public async Task Out_of_range_first_by_size_then_typical_then_insufficient()
    {
        var r = await Found(new([
            Steady("Typical B", 10, 10),
            Steady("Slightly above", 10, 20),
            Steady("Typical A", 10, 11),
            Steady("Far below", 30, 0),
            Steady("New", 10, 50, firstWeek: 6),
        ], 0));

        Assert.Equal(["Far below", "Slightly above", "Typical A", "Typical B", "New"], r.Locations.Select(l => l.Location));
    }

    [Fact]
    public async Task Response_says_what_it_compares_against()
    {
        var r = await Found(new([Steady("Site A", 10, 10)], 3));

        Assert.Equal(new DateRange(new(2026, 7, 20), new(2026, 7, 27)), r.Week);
        Assert.Equal(new BaselineRange(new(2026, 5, 25), new(2026, 7, 20), 8), r.Baseline);
        Assert.Equal(3, r.DataNotes.DuplicateRowsIgnored);
        Assert.Equal(new DateOnly(2026, 5, 25), r.AvailableWeeks[0]);
        Assert.Equal(new DateOnly(2026, 7, 20), r.AvailableWeeks[^1]);
    }

    [Fact]
    public async Task Unusual_baseline_weeks_are_named_by_their_week()
    {
        var spiky = new LocationActivity("Site A", WeekStart(0),
            Enumerable.Range(0, 9).Select(i => new WeekTypeCount("call_received", i, i == 2 ? 300 : 20)).ToList());

        var all = (await Found(new([spiky], 0))).Metrics.Single(m => m.EventType == "all");

        Assert.Equal([new WeekCount(new DateOnly(2026, 6, 8), 300)], all.UnusualBaselineWeeks);
    }
}
