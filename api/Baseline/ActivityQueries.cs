using Microsoft.EntityFrameworkCore;
using SitePulse.Api.Data;

namespace SitePulse.Api.Baseline;

/// <summary>
/// EF Core for the simple read, raw SQL for the aggregation. All SQL is interpolated
/// <see cref="RelationalDatabaseFacadeExtensions.SqlQuery{T}(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade, FormattableString)"/>,
/// so every value becomes a parameter. Every predicate carries account_id.
/// </summary>
public sealed class ActivityQueries(SitePulseDbContext db) : IActivityQueries
{
    public Task<AccountRecord?> FindAccountAsync(int accountId, CancellationToken ct = default) =>
        db.Accounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => new AccountRecord(a.Id, a.Name, a.Timezone,
                db.ActivityEvents.Where(e => e.AccountId == a.Id).Min(e => (DateTime?)e.OccurredAt)))
            .SingleOrDefaultAsync(ct);

    /// <summary>
    /// One round trip: counts per (location, event type, window) from the deduplicated view, each
    /// location's first event, and how many duplicate rows the view dropped inside the windows.
    /// The range predicate is a plain comparison on occurred_at, so it seeks the
    /// (account_id, occurred_at) index.
    /// </summary>
    public async Task<WeeklyActivity> GetWeeklyActivityAsync(int accountId, IReadOnlyList<WeekWindow> windows, CancellationToken ct = default)
    {
        if (windows.Count != 9)
            throw new ArgumentException("The aggregation is written for 9 contiguous windows (8 baseline + 1).", nameof(windows));

        var b = windows.Select(w => w.StartUtc).Append(windows[^1].EndUtc).ToArray();

        var rows = await db.Database.SqlQuery<WeeklyCountRow>($"""
            WITH weeks AS (
                SELECT idx, start_utc, end_utc
                FROM (VALUES (0, {b[0]}, {b[1]}), (1, {b[1]}, {b[2]}), (2, {b[2]}, {b[3]}),
                             (3, {b[3]}, {b[4]}), (4, {b[4]}, {b[5]}), (5, {b[5]}, {b[6]}),
                             (6, {b[6]}, {b[7]}), (7, {b[7]}, {b[8]}), (8, {b[8]}, {b[9]})) AS w (idx, start_utc, end_utc)
            ),
            firsts AS (
                SELECT location, MIN(occurred_at) AS first_at
                FROM activity_events_deduped
                WHERE account_id = {accountId} AND occurred_at < {b[9]}
                GROUP BY location
            ),
            counts AS (
                SELECT e.location, e.event_type, w.idx, COUNT(*) AS events
                FROM activity_events_deduped AS e
                JOIN weeks AS w ON e.occurred_at >= w.start_utc AND e.occurred_at < w.end_utc
                WHERE e.account_id = {accountId} AND e.occurred_at >= {b[0]} AND e.occurred_at < {b[9]}
                GROUP BY e.location, e.event_type, w.idx
            ),
            duplicates AS (
                SELECT (SELECT COUNT(*) FROM activity_events
                        WHERE account_id = {accountId} AND occurred_at >= {b[0]} AND occurred_at < {b[9]})
                     - (SELECT COUNT(*) FROM activity_events_deduped
                        WHERE account_id = {accountId} AND occurred_at >= {b[0]} AND occurred_at < {b[9]}) AS n
            )
            SELECT f.location AS Location, f.first_at AS FirstEventUtc,
                   c.event_type AS EventType, c.idx AS WeekIndex, c.events AS Events,
                   d.n AS DuplicateRowsIgnored
            FROM firsts AS f
            CROSS JOIN duplicates AS d
            LEFT JOIN counts AS c ON c.location = f.location
            """).ToListAsync(ct);

        var locations = rows
            .GroupBy(r => (r.Location, r.FirstEventUtc))
            .Select(g => new LocationActivity(g.Key.Location, DateTime.SpecifyKind(g.Key.FirstEventUtc, DateTimeKind.Utc),
                g.Where(r => r.EventType is not null)
                    .Select(r => new WeekTypeCount(r.EventType!, r.WeekIndex!.Value, r.Events!.Value))
                    .ToList()))
            .ToList();

        return new WeeklyActivity(locations, rows.Count == 0 ? 0 : rows[0].DuplicateRowsIgnored);
    }

    private sealed record WeeklyCountRow(
        string Location, DateTime FirstEventUtc, string? EventType, int? WeekIndex, int? Events, int DuplicateRowsIgnored);
}
