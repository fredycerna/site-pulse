using SitePulse.Api.Baseline;

namespace SitePulse.Api.Tests.Integration;

/// <summary>
/// Aggregates pinned against numbers computed by a different path: hand-written T-SQL in sqlcmd that
/// does not use the deduped view or the C# week boundaries. It dedups with GROUP BY over every column
/// but id, converts each row with AT TIME ZONE and a Windows zone name, and buckets by local Monday.
/// The query is db/verify/verify-aggregates.sql. Reads the seeded compose database.
/// </summary>
public class AggregateVerificationTests
{
    private static readonly DateTimeOffset DemoNow = new(2026, 7, 27, 14, 0, 0, TimeSpan.Zero);

    private static async Task<BaselineOutcome> Get(int accountId, string eventType = "all", DateOnly? week = null)
    {
        await using var db = await SeededDatabase.OpenAsync();
        var service = new BaselineService(new ActivityQueries(db), new PinnedTimeProvider(DemoNow));
        return await service.GetAsync(accountId, eventType, week);
    }

    private static async Task<ActivityBaselineResponse> Found(int accountId, DateOnly week, string eventType = "all") =>
        Assert.IsType<BaselineOutcome.Found>(await Get(accountId, eventType, week)).Response;

    private static int[] HistoryOf(ActivityBaselineResponse r, string eventType) =>
        r.Metrics.Single(m => m.EventType == eventType).History.Select(h => h.Count).ToArray();

    public static TheoryData<int, string, int[], int[], int[], int[], int> Cases => new()
    {
        // Account 6 (America/New_York), last complete week. Baseline holds the spike week (Jun 1).
        {
            6, "2026-07-20",
            [53, 880, 102, 59, 76, 69, 79, 50, 87],
            [29, 528, 66, 35, 48, 41, 43, 27, 51],
            [15, 230, 20, 13, 15, 15, 21, 15, 17],
            [9, 122, 16, 11, 13, 13, 15, 8, 19],
            1
        },
        // Account 6, the spike week itself.
        {
            6, "2026-06-01",
            [76, 55, 64, 86, 68, 78, 62, 53, 880],
            [43, 33, 36, 49, 46, 46, 38, 29, 528],
            [18, 16, 16, 27, 12, 22, 17, 15, 230],
            [15, 6, 12, 10, 10, 10, 7, 9, 122],
            3
        },
        // Account 1 (America/Chicago), the week holding the 11266/11267 duplicate in Site C.
        {
            1, "2026-07-06",
            [58, 51, 46, 36, 47, 47, 39, 62, 42],
            [41, 36, 25, 23, 33, 32, 25, 37, 23],
            [10, 10, 15, 7, 10, 12, 9, 15, 11],
            [7, 5, 6, 6, 4, 3, 5, 10, 8],
            1
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Weekly_counts_match_the_independent_sql(int accountId, string week,
        int[] all, int[] calls, int[] leads, int[] appointments, int duplicates)
    {
        var r = await Found(accountId, DateOnly.Parse(week));

        Assert.Equal(all, HistoryOf(r, "all"));
        Assert.Equal(calls, HistoryOf(r, "call_received"));
        Assert.Equal(leads, HistoryOf(r, "lead_created"));
        Assert.Equal(appointments, HistoryOf(r, "appointment_set"));
        Assert.Equal(duplicates, r.DataNotes.DuplicateRowsIgnored);
    }

    [Fact]
    public async Task Location_counts_match_the_independent_sql_for_the_spike_week()
    {
        var r = await Found(6, new DateOnly(2026, 6, 1));

        Assert.Equal(
            new Dictionary<string, int>
            {
                ["Site A"] = 49, ["Site B"] = 65, ["Site C"] = 67, ["Site D"] = 70, ["Site E"] = 55,
                ["Site F"] = 65, ["Site G"] = 57, ["Site H"] = 54, ["Site I"] = 47, ["Site J"] = 56,
                ["Site K"] = 60, ["Site L"] = 67, ["Site M"] = 50, ["Site N"] = 72, ["Site O"] = 46,
            },
            r.Locations.ToDictionary(l => l.Location, l => l.Current));
        Assert.All(r.Metrics, m => Assert.Equal(BaselineStatus.Above, m.Status));
    }

    [Fact]
    public async Task The_double_ingested_row_is_counted_once()
    {
        // The raw table holds 5 Site C rows that week (11266 and 11267 are the same event).
        var r = await Found(1, new DateOnly(2026, 7, 6));

        Assert.Equal(4, r.Locations.Single(l => l.Location == "Site C").Current);
    }

    [Fact]
    public async Task Spike_is_reported_as_an_unusual_baseline_week_and_the_week_after_is_typical()
    {
        var all = (await Found(6, new DateOnly(2026, 7, 20))).Metrics.Single(m => m.EventType == "all");

        // Median 72.5, MAD 16.5 → σ 24.46 → 23.57..121.43.
        Assert.Equal(72.5, all.Median);
        Assert.Equal((24, 121), (all.TypicalLow, all.TypicalHigh));
        Assert.Equal(BaselineStatus.Typical, all.Status);
        Assert.Equal([new WeekCount(new DateOnly(2026, 6, 1), 880)], all.UnusualBaselineWeeks);
    }

    [Theory]
    [InlineData(6, "2026-07-20")]
    [InlineData(1, "2026-07-06")]
    public async Task Per_type_location_counts_add_up_to_the_total(int accountId, string week)
    {
        var monday = DateOnly.Parse(week);
        var total = (await Found(accountId, monday)).Metrics.Single(m => m.EventType == "all").Current;

        var sum = 0;
        foreach (var type in EventTypes.Recorded)
        {
            var r = await Found(accountId, monday, type);
            Assert.Equal(r.Metrics.Single(m => m.EventType == type).Current, r.Locations.Sum(l => l.Current));
            sum += r.Locations.Sum(l => l.Current);
        }
        Assert.Equal(total, sum);
    }

    [Fact]
    public async Task Early_week_reports_the_history_weeks_that_decided_insufficient_history()
    {
        // By hand in sqlcmd: first event per location in New York time, against the baseline Mondays
        // Dec 22 .. Feb 9. Only Mondays at or after the first event count (the first, partial week does not).
        // Account: first event Sun Feb 1 07:06 (Site D) → Feb 2 and Feb 9 → 2.
        var r = await Found(6, new DateOnly(2026, 2, 16));

        Assert.All(r.Metrics, m =>
        {
            Assert.Equal(2, m.HistoryWeeks);
            Assert.Equal(BaselineStatus.InsufficientHistory, m.Status);
        });
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["Site A"] = 1, ["Site B"] = 1, ["Site C"] = 2, ["Site D"] = 2, ["Site E"] = 1,
                ["Site F"] = 1, ["Site G"] = 1, ["Site H"] = 1, ["Site I"] = 1, ["Site J"] = 2,
                ["Site K"] = 1, ["Site L"] = 1, ["Site M"] = 1, ["Site N"] = 1, ["Site O"] = 1,
            },
            r.Locations.ToDictionary(l => l.Location, l => l.HistoryWeeks));
        Assert.All(r.Locations, l => Assert.Equal(BaselineStatus.InsufficientHistory, l.Status));
    }

    [Fact]
    public async Task Account_without_events_is_no_data()
    {
        var r = Assert.IsType<BaselineOutcome.Found>(await Get(20)).Response;

        Assert.Empty(r.Locations);
        Assert.All(r.Metrics, m => Assert.Equal(BaselineStatus.NoData, m.Status));
    }

    [Fact]
    public async Task Unknown_account_is_not_found()
    {
        Assert.IsType<BaselineOutcome.AccountNotFound>(await Get(999));
    }
}
