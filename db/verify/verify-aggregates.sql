-- Independent check of the dashboard aggregates (see README, "Verifying the numbers").
-- Usage, with the compose database up:
--   docker cp db/verify/verify-aggregates.sql site-pulse-sql:/tmp/verify.sql
--   docker exec site-pulse-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
--     -d SitePulse -W -s '|' -h -1 -i /tmp/verify.sql -v account=6 tz="Eastern Standard Time" week=2026-07-20
-- `tz` is the Windows name of the account's zone (America/New_York → Eastern Standard Time,
-- America/Chicago → Central Standard Time, ...). `week` is the local Monday being judged.
--
-- Independent path: no view, no C# boundaries. Dedup by GROUP BY over every column but id,
-- local time via AT TIME ZONE with a Windows zone name, local Monday by date arithmetic.
SET NOCOUNT ON;
DECLARE @account int = $(account), @tz sysname = '$(tz)', @week date = '$(week)';
DECLARE @first date = DATEADD(day, -56, @week), @last date = DATEADD(day, 7, @week);

WITH raw AS (
    SELECT location, event_type, duration_seconds, outcome, occurred_at,
           CAST(CAST(occurred_at AS datetimeoffset) AT TIME ZONE @tz AS date) AS local_day
    FROM activity_events WHERE account_id = @account
), raw_weeks AS (
    SELECT *, DATEADD(day, -(DATEDIFF(day, '19000101', local_day) % 7), local_day) AS week_start FROM raw
), dedup AS (
    SELECT location, event_type, week_start FROM raw_weeks
    GROUP BY location, event_type, occurred_at, duration_seconds, outcome, week_start
)
SELECT 'metric' AS kind, week_start, 'all' AS t, COUNT(*) AS n FROM dedup WHERE week_start >= @first AND week_start < @last GROUP BY week_start
UNION ALL
SELECT 'metric', week_start, event_type, COUNT(*) FROM dedup WHERE week_start >= @first AND week_start < @last GROUP BY week_start, event_type
UNION ALL
SELECT 'loc:' + location, week_start, 'all', COUNT(*) FROM dedup WHERE week_start = @week GROUP BY location, week_start
UNION ALL
SELECT 'dups', NULL, NULL,
       (SELECT COUNT(*) FROM raw_weeks WHERE week_start >= @first AND week_start < @last)
     - (SELECT COUNT(*) FROM dedup WHERE week_start >= @first AND week_start < @last)
ORDER BY kind, t, week_start;
