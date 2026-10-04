namespace SitePulse.Api.Baseline;

/// <summary>One local week, Monday 00:00 to the next Monday 00:00 in the account's timezone, as a half-open UTC range.</summary>
public sealed record WeekWindow(DateOnly WeekStart, DateTime StartUtc, DateTime EndUtc);

/// <summary>
/// Week boundaries live here, not in SQL: T-SQL does not understand IANA zone ids, and passing UTC
/// ranges keeps the predicate on occurred_at a plain index seek. TimeZoneInfo handles DST, so a
/// week can be 167 or 169 hours long.
/// </summary>
public static class WeekWindowCalculator
{
    public const int DefaultBaselineWeeks = 8;

    public static DateOnly MondayOf(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    /// <summary>The Monday of the latest local week that has fully ended at <paramref name="nowUtc"/>.</summary>
    public static DateOnly LastCompleteWeek(TimeZoneInfo tz, DateTime nowUtc)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(nowUtc), tz);
        return MondayOf(DateOnly.FromDateTime(localNow)).AddDays(-7);
    }

    public static WeekWindow WindowFor(TimeZoneInfo tz, DateOnly monday) =>
        new(monday, LocalMidnightToUtc(tz, monday), LocalMidnightToUtc(tz, monday.AddDays(7)));

    /// <returns>null when the week can be shown; otherwise the reason it cannot.</returns>
    public static string? ValidateRequestedWeek(TimeZoneInfo tz, DateTime nowUtc, DateOnly week)
    {
        if (week.DayOfWeek != DayOfWeek.Monday)
            return $"week must be a Monday (local week start); {week:yyyy-MM-dd} is a {week.DayOfWeek}.";
        if (week > LastCompleteWeek(tz, nowUtc))
            return $"week {week:yyyy-MM-dd} has not ended yet; only complete weeks can be compared.";
        return null;
    }

    /// <summary>
    /// The baseline weeks (oldest first) followed by the week being judged: baselineWeeks + 1
    /// contiguous windows. Defaults to the last complete week.
    /// </summary>
    public static IReadOnlyList<WeekWindow> Compute(TimeZoneInfo tz, DateTime nowUtc, DateOnly? requestedWeek,
        int baselineWeeks = DefaultBaselineWeeks)
    {
        if (requestedWeek is { } requested && ValidateRequestedWeek(tz, nowUtc, requested) is { } error)
            throw new ArgumentException(error, nameof(requestedWeek));

        var current = requestedWeek ?? LastCompleteWeek(tz, nowUtc);
        return Enumerable.Range(0, baselineWeeks + 1)
            .Select(i => WindowFor(tz, current.AddDays(7 * (i - baselineWeeks))))
            .ToList();
    }

    private static DateTime LocalMidnightToUtc(TimeZoneInfo tz, DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // Zones that jump at midnight (none of the seed's) have no 00:00 on that day; the day starts at the first valid instant.
        while (tz.IsInvalidTime(local)) local = local.AddMinutes(15);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc), // database values are UTC
        _ => value,
    };
}
