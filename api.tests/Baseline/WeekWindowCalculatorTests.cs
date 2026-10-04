using SitePulse.Api.Baseline;

namespace SitePulse.Api.Tests.Baseline;

public class WeekWindowCalculatorTests
{
    private static readonly TimeZoneInfo Chicago = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
    private static readonly TimeZoneInfo Phoenix = TimeZoneInfo.FindSystemTimeZoneById("America/Phoenix");
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.FindSystemTimeZoneById("UTC");

    private static DateTime UtcAt(int y, int m, int d, int h = 0, int min = 0) => new(y, m, d, h, min, 0, DateTimeKind.Utc);

    [Fact]
    public void Chicago_week_starts_at_local_monday_midnight()
    {
        var w = WeekWindowCalculator.WindowFor(Chicago, new DateOnly(2026, 7, 20));

        // CDT is UTC-5.
        Assert.Equal(UtcAt(2026, 7, 20, 5), w.StartUtc);
        Assert.Equal(UtcAt(2026, 7, 27, 5), w.EndUtc);
    }

    [Fact]
    public void Utc_week_starts_at_utc_monday_midnight()
    {
        var w = WeekWindowCalculator.WindowFor(Utc, new DateOnly(2026, 7, 20));

        Assert.Equal(UtcAt(2026, 7, 20), w.StartUtc);
        Assert.Equal(UtcAt(2026, 7, 27), w.EndUtc);
    }

    [Fact]
    public void Dst_start_week_is_167_hours_in_chicago_and_168_in_phoenix()
    {
        // US DST starts Sunday 2026-03-08; Phoenix does not observe it.
        var chicago = WeekWindowCalculator.WindowFor(Chicago, new DateOnly(2026, 3, 2));
        var phoenix = WeekWindowCalculator.WindowFor(Phoenix, new DateOnly(2026, 3, 2));

        Assert.Equal(TimeSpan.FromHours(167), chicago.EndUtc - chicago.StartUtc);
        Assert.Equal(TimeSpan.FromHours(168), phoenix.EndUtc - phoenix.StartUtc);
    }

    [Fact]
    public void Monday_of_any_day_is_the_preceding_or_same_monday()
    {
        Assert.Equal(new DateOnly(2026, 7, 20), WeekWindowCalculator.MondayOf(new DateOnly(2026, 7, 20)));
        Assert.Equal(new DateOnly(2026, 7, 20), WeekWindowCalculator.MondayOf(new DateOnly(2026, 7, 26)));
    }

    [Fact]
    public void Mid_week_now_gives_the_previous_full_week()
    {
        // Wed 2026-07-29 10:00 Chicago.
        var last = WeekWindowCalculator.LastCompleteWeek(Chicago, UtcAt(2026, 7, 29, 15));

        Assert.Equal(new DateOnly(2026, 7, 20), last);
    }

    [Fact]
    public void Utc_sunday_evening_is_already_monday_elsewhere_but_not_in_chicago()
    {
        // Mon 2026-07-27 03:00 UTC is still Sun 22:00 in Chicago: the week of Jul 20 has not ended there.
        Assert.Equal(new DateOnly(2026, 7, 13), WeekWindowCalculator.LastCompleteWeek(Chicago, UtcAt(2026, 7, 27, 3)));
        Assert.Equal(new DateOnly(2026, 7, 20), WeekWindowCalculator.LastCompleteWeek(Utc, UtcAt(2026, 7, 27, 3)));
    }

    [Fact]
    public void Now_exactly_at_local_monday_midnight_counts_the_week_just_ended()
    {
        var last = WeekWindowCalculator.LastCompleteWeek(Chicago, UtcAt(2026, 7, 27, 5));

        Assert.Equal(new DateOnly(2026, 7, 20), last);
    }

    [Fact]
    public void Compute_returns_eight_baseline_weeks_then_the_current_week_contiguous()
    {
        var windows = WeekWindowCalculator.Compute(Chicago, UtcAt(2026, 7, 27, 14), requestedWeek: null);

        Assert.Equal(9, windows.Count);
        Assert.Equal(new DateOnly(2026, 5, 25), windows[0].WeekStart);
        Assert.Equal(new DateOnly(2026, 7, 20), windows[8].WeekStart);
        for (var i = 1; i < windows.Count; i++)
        {
            Assert.Equal(windows[i - 1].EndUtc, windows[i].StartUtc);
        }
    }

    [Fact]
    public void Compute_honours_a_requested_past_week()
    {
        var windows = WeekWindowCalculator.Compute(Chicago, UtcAt(2026, 7, 27, 14), new DateOnly(2026, 6, 1));

        Assert.Equal(new DateOnly(2026, 6, 1), windows[8].WeekStart);
        Assert.Equal(new DateOnly(2026, 4, 6), windows[0].WeekStart);
    }

    [Fact]
    public void Baseline_spanning_dst_keeps_local_midnight_on_both_sides()
    {
        var windows = WeekWindowCalculator.Compute(Chicago, UtcAt(2026, 7, 27, 14), new DateOnly(2026, 3, 16));

        // Jan 19 is CST (UTC-6); Mar 16 is CDT (UTC-5).
        Assert.Equal(UtcAt(2026, 1, 19, 6), windows[0].StartUtc);
        Assert.Equal(UtcAt(2026, 3, 16, 5), windows[8].StartUtc);
    }

    [Fact]
    public void A_week_that_is_not_a_monday_is_rejected()
    {
        var now = UtcAt(2026, 7, 27, 14);

        Assert.NotNull(WeekWindowCalculator.ValidateRequestedWeek(Chicago, now, new DateOnly(2026, 7, 21)));
        Assert.Throws<ArgumentException>(() => WeekWindowCalculator.Compute(Chicago, now, new DateOnly(2026, 7, 21)));
    }

    [Fact]
    public void A_week_that_has_not_ended_is_rejected()
    {
        var now = UtcAt(2026, 7, 27, 14);

        Assert.NotNull(WeekWindowCalculator.ValidateRequestedWeek(Chicago, now, new DateOnly(2026, 7, 27)));
        Assert.Null(WeekWindowCalculator.ValidateRequestedWeek(Chicago, now, new DateOnly(2026, 7, 20)));
        Assert.Throws<ArgumentException>(() => WeekWindowCalculator.Compute(Chicago, now, new DateOnly(2026, 7, 27)));
    }
}
