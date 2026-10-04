namespace SitePulse.Api.Baseline;

public enum BaselineStatus { Below, Typical, Above, InsufficientHistory, NoData }

/// <param name="Median">Center of the baseline; null without enough history.</param>
/// <param name="TypicalLow">Lowest whole count that is typical (inclusive).</param>
/// <param name="TypicalHigh">Highest whole count that is typical (inclusive).</param>
/// <param name="Deviation">(current − median) / σ; signed; 0 without enough history.</param>
/// <param name="UnusualBaselineIndices">Positions in the baseline whose count lies outside the range.</param>
/// <param name="HistoryWeeks">Baseline weeks that counted as history; the same number that decides InsufficientHistory.</param>
public sealed record BaselineResult(
    BaselineStatus Status,
    int HistoryWeeks,
    double? Median,
    int? TypicalLow,
    int? TypicalHigh,
    double Deviation,
    IReadOnlyList<int> UnusualBaselineIndices);

/// <summary>
/// "Normal" for one series (PLAN.md §5): median ± 2σ with σ = max(1.4826·MAD, √median, 1).
/// Median and MAD ignore how large an outlier is; the √median floor is the noise expected
/// from a small count, so a flat series does not get a zero-width range. The floor of 1
/// (added after the end-to-end pass) covers a median of 0, where √median is 0 too: the range
/// is never narrower than ±2 events. It only changes series whose median is below 1.
/// </summary>
public static class BaselineStats
{
    public const int MinimumHistoryWeeks = 4;
    private const double MadToSigma = 1.4826;
    private const double BandWidthInSigmas = 2;
    private const double MinimumSigma = 1;

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) throw new ArgumentException("Median of an empty series.", nameof(values));
        var sorted = values.Order().ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    /// <param name="baseline">Counts of the baseline weeks with history only (weeks before the first event are left out).</param>
    public static BaselineResult Evaluate(IReadOnlyList<int> baseline, int current)
    {
        // One number decides insufficient history and is reported as HistoryWeeks, so the two cannot disagree.
        var historyWeeks = baseline.Count;
        if (historyWeeks < MinimumHistoryWeeks)
            return new BaselineResult(BaselineStatus.InsufficientHistory, historyWeeks, null, null, null, 0, []);

        var values = baseline.Select(c => (double)c).ToArray();
        var median = Median(values);
        var mad = Median(values.Select(v => Math.Abs(v - median)).ToArray());
        var sigma = Math.Max(Math.Max(MadToSigma * mad, Math.Sqrt(median)), MinimumSigma);
        var low = median - BandWidthInSigmas * sigma;
        var high = median + BandWidthInSigmas * sigma;

        // Counts are whole numbers, so ceil/floor of the band holds exactly the counts the band calls typical.
        var typicalLow = Math.Max(0, (int)Math.Ceiling(low));
        var typicalHigh = (int)Math.Floor(high);

        var unusual = Enumerable.Range(0, baseline.Count)
            .Where(i => Classify(baseline[i], low, high) != BaselineStatus.Typical)
            .ToList();

        return new BaselineResult(Classify(current, low, high), historyWeeks, median, typicalLow, typicalHigh,
            (current - median) / sigma, unusual);
    }

    private static BaselineStatus Classify(int count, double low, double high) =>
        count < low ? BaselineStatus.Below : count > high ? BaselineStatus.Above : BaselineStatus.Typical;
}
