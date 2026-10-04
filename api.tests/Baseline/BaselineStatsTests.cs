using SitePulse.Api.Baseline;

namespace SitePulse.Api.Tests.Baseline;

public class BaselineStatsTests
{
    // PLAN.md's exploratory series for account 6 before Jul 20, bucketed in UTC weeks (881 = the spike week).
    // The app buckets in local weeks (53, 880, …, median 72.5; see the integration tests). Here it is only an input.
    private static readonly int[] Account6UtcSeries = [53, 881, 102, 59, 76, 68, 80, 50];

    [Fact]
    public void Median_of_an_even_count_is_the_mean_of_the_middle_pair()
    {
        Assert.Equal(2.5, BaselineStats.Median([4, 1, 3, 2]));
        Assert.Equal(2, BaselineStats.Median([3, 1, 2]));
    }

    [Fact]
    public void Zero_mad_falls_back_to_the_sqrt_median_floor()
    {
        // median 9, MAD 0 → σ = √9 = 3 → 9 ± 6.
        var r = BaselineStats.Evaluate([9, 9, 9, 9, 9, 9, 9, 9], current: 10);

        Assert.Equal(3, r.TypicalLow);
        Assert.Equal(15, r.TypicalHigh);
        Assert.Equal(BaselineStatus.Typical, r.Status);
    }

    [Fact]
    public void A_value_equal_to_a_limit_is_typical_and_one_past_it_is_not()
    {
        int[] flat = [9, 9, 9, 9, 9, 9, 9, 9];

        Assert.Equal(BaselineStatus.Typical, BaselineStats.Evaluate(flat, 15).Status);
        Assert.Equal(BaselineStatus.Typical, BaselineStats.Evaluate(flat, 3).Status);
        Assert.Equal(BaselineStatus.Above, BaselineStats.Evaluate(flat, 16).Status);
        Assert.Equal(BaselineStatus.Below, BaselineStats.Evaluate(flat, 2).Status);
    }

    [Fact]
    public void Account_6_exploratory_utc_series_with_87_is_typical_around_median_72()
    {
        var r = BaselineStats.Evaluate(Account6UtcSeries, current: 87);

        // MAD = 16 → σ = 1.4826·16 = 23.72 → 72 ± 47.44 → 24.56..119.44 → whole numbers 25..119.
        Assert.Equal(72, r.Median);
        Assert.Equal(25, r.TypicalLow);
        Assert.Equal(119, r.TypicalHigh);
        Assert.Equal(BaselineStatus.Typical, r.Status);
    }

    [Fact]
    public void The_spike_week_itself_is_above()
    {
        var r = BaselineStats.Evaluate([53, 102, 59, 76, 68, 80, 50, 87], current: 881);

        Assert.Equal(BaselineStatus.Above, r.Status);
        Assert.True(r.Deviation > 0);
    }

    [Fact]
    public void A_spike_in_the_baseline_is_reported_and_its_size_does_not_move_the_range()
    {
        var withSpike = BaselineStats.Evaluate(Account6UtcSeries, current: 87);
        var withBiggerSpike = BaselineStats.Evaluate([53, 5000, 102, 59, 76, 68, 80, 50], current: 87);

        Assert.Equal([1], withSpike.UnusualBaselineIndices);
        Assert.Equal(withSpike.TypicalLow, withBiggerSpike.TypicalLow);
        Assert.Equal(withSpike.TypicalHigh, withBiggerSpike.TypicalHigh);
    }

    [Fact]
    public void All_zero_baseline_still_has_a_range_of_two_events()
    {
        // Degenerate case of PLAN.md §5 found by the end-to-end pass: median 0 and MAD 0 used to give
        // σ = 0, a 0–0 range, and one event was "above". σ is now floored at 1, so the range is 0–2.
        int[] zeros = [0, 0, 0, 0, 0, 0, 0, 0];

        Assert.Equal((0, 2), (BaselineStats.Evaluate(zeros, 0).TypicalLow, BaselineStats.Evaluate(zeros, 0).TypicalHigh));
        Assert.Equal(BaselineStatus.Typical, BaselineStats.Evaluate(zeros, 1).Status);
        Assert.Equal(BaselineStatus.Typical, BaselineStats.Evaluate(zeros, 2).Status);
        Assert.Equal(BaselineStatus.Above, BaselineStats.Evaluate(zeros, 3).Status);
    }

    [Fact]
    public void Sparse_series_with_median_zero_gets_the_floor_of_one_sigma()
    {
        // Account 6, Site M, appointments before Jul 20 (hand SQL): median 0, MAD 0. It had 2 and 1 in the last two weeks.
        int[] siteM = [0, 6, 0, 0, 0, 0, 2, 1];

        var two = BaselineStats.Evaluate(siteM, 2);
        var three = BaselineStats.Evaluate(siteM, 3);

        Assert.Equal((0, 2), (two.TypicalLow, two.TypicalHigh));
        Assert.Equal(BaselineStatus.Typical, two.Status);
        Assert.Equal(BaselineStatus.Above, three.Status);
        Assert.Equal(3, three.Deviation);
    }

    [Fact]
    public void Lower_limit_never_goes_below_zero()
    {
        var r = BaselineStats.Evaluate([1, 0, 2, 1, 0, 3, 1, 1], current: 0);

        Assert.Equal(0, r.TypicalLow);
        Assert.Equal(BaselineStatus.Typical, r.Status);
    }

    [Fact]
    public void Fewer_than_four_weeks_of_history_is_insufficient()
    {
        var three = BaselineStats.Evaluate([10, 12, 11], current: 50);
        var four = BaselineStats.Evaluate([10, 12, 11, 10], current: 50);

        Assert.Equal(BaselineStatus.InsufficientHistory, three.Status);
        Assert.Equal(3, three.HistoryWeeks);
        Assert.Equal(4, four.HistoryWeeks);
        Assert.Null(three.Median);
        Assert.Null(three.TypicalLow);
        Assert.Null(three.TypicalHigh);
        Assert.Empty(three.UnusualBaselineIndices);
        Assert.Equal(BaselineStatus.Above, four.Status);
    }

    [Fact]
    public void Whole_number_limits_agree_with_the_real_valued_band_for_any_count()
    {
        // Independent of the implementation: recompute the band here and compare both views of it.
        static double Mid(IEnumerable<double> xs)
        {
            var s = xs.OrderBy(x => x).ToArray();
            return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2;
        }

        var random = new Random(247);
        for (var trial = 0; trial < 500; trial++)
        {
            var baseline = Enumerable.Range(0, random.Next(4, 9)).Select(_ => random.Next(0, random.Next(1, 40))).ToArray();
            var median = Mid(baseline.Select(x => (double)x));
            var sigma = Math.Max(Math.Max(1.4826 * Mid(baseline.Select(x => Math.Abs(x - median))), Math.Sqrt(median)), 1);

            for (var current = 0; current <= 80; current++)
            {
                var r = BaselineStats.Evaluate(baseline, current);
                var expected = current < median - 2 * sigma ? BaselineStatus.Below
                    : current > median + 2 * sigma ? BaselineStatus.Above
                    : BaselineStatus.Typical;

                Assert.Equal(expected, r.Status);
                Assert.Equal(current >= r.TypicalLow && current <= r.TypicalHigh, r.Status == BaselineStatus.Typical);
            }
        }
    }
}
