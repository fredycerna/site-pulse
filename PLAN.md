# PLAN — DASH-247: "Is this normal for us?"

Written before any code, on Saturday 2026-10-03. This file is frozen after its first commit. Anything that changes on the way goes to the README under "Deviations from the plan".

## 1. The ticket and how I read it

DASH-247 asks for a dashboard that lets a customer answer "is this normal for us?" at a glance, without exporting data or calling their account manager. Product notes: probably a baseline comparison, multi-location customers matter, and a customer admin should be able to look at it on Monday morning and act. Alerting and ML/forecasting are out of scope.

My reading: the screen has to answer one question.

> "Was last week normal for us, and which location stepped outside its own normal?"

That gives me three commitments:

- **"Last week"** means the last complete week, Monday to Sunday, **in the account's timezone**. A Monday-morning admin is looking back at a finished week, not at a half-week.
- **"For us"** means each location is compared with **its own history**: its 8 previous complete weeks. Not with other locations, not with other accounts. A 2-location shop and a 15-location group both get a fair comparison.
- **"Act on it"** means the screen ranks locations by how far outside their range they are, and always says what it compared against. A status without its baseline is not actionable.

The metric for this slice is **event volume per week**, with a filter by event type (all / calls / leads / appointments). Quality signals (missed-call rate, no-shows) are a real follow-up, but they are a different question and I am not building them now.

## 2. What the seed data actually looks like

I loaded `schema.sql` and `seed.sql` into a scratch database and measured before deciding anything. 12,626 events across 20 accounts.

- **Date range**: 2026-02-01 to 2026-07-27 UTC. 25 complete weeks (Feb 2 to Jul 26) plus a partial Monday, Jul 27. The real clock (October) is useless here: nothing happened "last week". "Now" has to be pinned to the snapshot.
- **A spike**: account 6 (Metro Collision, 15 locations) logged 882 events on 2026-06-03, against roughly 80 on a normal day. That week shows 881 against ~70. Any average-based baseline gets poisoned by it for two months; a median does not move.
- **Small volumes**: per location, 2 to 15 events per week. Percent changes lie at this size ("+100 %" is 3 → 6).
- **12 exact duplicate rows**: identical in every column except `id`, with consecutive ids (11266/11267, 5856/5857, 9603/9604, 1538/1539, 6966/6967, 1523/1524, 5851/5852, 8738/8739, 2236/2237, 7524/7525, 6325/6326, 9595/9596). Looks like double ingestion.
- **NULLs**: `outcome` is NULL on 398 events (240 calls, 100 leads, 58 appointments); `duration_seconds` is NULL on 313 calls; some `missed` calls have a duration. None of this changes a volume count, so they all count.
- **An empty account**: account 20 (Quiet Harbor Spa) has zero events.
- **Timezones**: Chicago, New_York, Denver, Los_Angeles, Phoenix (no DST), and one account on plain `UTC` (account 18).
- **Zero weeks**: a few location-weeks have no events at all (account 6 Site G twice, Site N once; account 18 Site C once). They count as 0, they are not skipped.
- **Clean otherwise**: no orphans, no events before the account's `created_at`, no location name variants, one date format.
- With the rule below, the last complete week (Jul 20–26) flags 3 locations as `above` (12/Site F, 15/Site C, 19/Site A) and none `below`. The spike week (Jun 1–7) comes out `above` for account 6. Good demo cases.

## 3. Questions and assumptions

The prompt allows documenting assumptions instead of waiting for answers. It is the weekend, so I am assuming and writing it down:

1. Weeks run Monday 00:00 to Monday 00:00 in the account's local time.
2. The 12 duplicate rows are double ingestion. I keep them in the table and ignore them when reading.
3. Events with NULL `outcome` or `duration_seconds` are still events and count toward volume.
4. "Now" is pinned to the seed snapshot (Mon 2026-07-27 14:00 UTC), and the UI says so.
5. The Jun 3 spike is kept. It is real data as far as I know; the median absorbs it, and the UI lists it as an unusual baseline week instead of hiding it.
6. "Normal" means weekly volume in this slice, not quality.

If any of these is wrong, the answer changes in one place (the view, the window calculator or the stats function), not across the codebase.

## 4. The slice: what is in and what is not

**In:**
- One API endpoint that returns, for an account and a week: the four account-level metrics (all, calls, leads, appointments) with their normal range and status, plus one row per location for the selected event type, already sorted by attention.
- One Angular page: account picker, verdict sentence, four metric cards (the selected card is the type filter), location rows with a range bar, a click-through detail per location with the 9-week history.
- Filters (account, type, week) in the URL, so a reload or a pasted link restores the view.
- Edge states: loading, error, account with no activity, location without enough history.
- Tests for the parts that can be wrong in interesting ways, and aggregates checked against the seed by an independent path.

**Out, named on purpose:**
- Missed-call rate and no-show rate (quality, not volume).
- Comparing locations against each other.
- The current partial week.
- Auth, export, alerts, ML/forecasting.

A smaller, correct slice is worth more than a broad, half-working one. If I run out of time, I cut from the bottom of the frontend list, not from correctness.

## 5. What "normal" means

For each location (and for the account as a whole), take the counts of the 8 previous complete weeks:

- **center** = median
- **spread** σ = max(1.4826 · MAD, √median)
- **normal range** = median ± 2σ, shown as whole numbers ("normal 2–12")
- **status** = `below` / `typical` / `above`, or `insufficient_history` if the location has fewer than 4 complete weeks since its first event. A value equal to a range limit is `typical`.

Why these pieces:
- Median and MAD are robust: one 881-event week does not drag the range for two months.
- The √median floor is the noise you expect from a small count anyway. Without it, a location with 5, 5, 5, 5, 5, 5, 5, 5 gets a zero-width range and 6 becomes "above normal".
- I compare against a range, not a percentage, because volumes are small.

**Back-test against the seed** (1,173 location-weeks, each judged against its own previous 8 weeks): how many would be flagged as outside normal?

| Rule | Flagged |
|---|---|
| median ± 2σ, σ = max(1.4826·MAD, √median) (chosen) | **6 %** |
| "the range you saw in 6 of your 8 weeks" | 36 % |
| average of last 4 weeks ± 30 % | 51 % |
| interquartile range p25–p75 | 55 % |

A screen that flags half the locations every Monday trains people to ignore it. 6 % means a 15-location group sees about one location to look at in a typical week, which matches "act on it".

## 6. Architecture

**Stack**: .NET 8 Web API, EF Core 8, SQL Server 2022 in Docker, xUnit, Angular (standalone components, signals). The API targets `net8.0` with `RollForward=Major` so it runs on whatever runtime is installed. `dotnet-ef` is in a local tool manifest.

**Layout**: `api/`, `api.tests/`, `web/`, `db/seed/` (`schema.sql` and `seed.sql` byte-identical to the starter), `docker-compose.yml`, `ai-log/`.

**Schema**: an EF Core migration with explicit snake_case names (`ToTable`, `HasColumnName`) matching `schema.sql`, ids not generated by the database, `datetime2(0)`, `varchar` columns, and an index on `(account_id, occurred_at) INCLUDE (location, event_type)`.

**Seed loading**: an idempotent command (`dotnet run --project api -- seed`, and automatic in Development when `accounts` is empty) that runs `db/seed/seed.sql` as-is, in batches, inside one transaction. The data is never edited or cleaned.

**Duplicates**: a second migration creates the view `activity_events_deduped` (`SELECT DISTINCT` over every column except `id`). One named place where the duplicate rule lives. The dashboard reads from the view. No summary table, no weekly view: the week depends on each account's timezone, and at 12.6k rows there is nothing to optimize.

**Layers**: thin REST endpoint → `BaselineService` (orchestrates) → `IActivityQueries` (the only class with SQL) + pure functions (`WeekWindowCalculator`, `BaselineStats`). Dependency injection throughout. No generic repository: one query class behind an interface is enough, and lets me test the service with a fake.

**Data access**: EF Core, because the prompt asks for migrations with the stack's standard tool. The aggregation is raw SQL through `Database.SqlQuery<T>` with interpolation (parameterized), so EF works as a micro-ORM there. One dependency covers migrations and queries; Dapper would mean a second migration tool.

**The aggregation**: one parameterized query that reads the deduped view, filters by `account_id` and a UTC range, joins against the 9 week windows (a `VALUES` list), and groups by location, event type and window. At most 15 × 3 × 9 rows. The four account metrics and the location rows all come from that one round trip. The statistics (median, MAD, range) run in C# on at most 15 × 9 numbers, in a pure function.

### Why the week boundaries are computed in C#

T-SQL does not understand IANA timezone names, and the accounts store IANA names. So C# computes the 10 boundaries of the 9 weeks with `TimeZoneInfo` (which handles DST), converts them to UTC, and passes them to SQL. This is not a workaround:

- The predicate stays a plain range on `occurred_at`, with no function on the column, so it uses the index. Converting every row with `AT TIME ZONE` inside the query would not.
- Counting, deduplicating, filtering and grouping still happen in SQL. C# only contributes 10 timestamps.
- To check it, the independent verification computes the same numbers in pure T-SQL with `AT TIME ZONE` and Windows zone names. Both paths have to agree.

### Why no cache

The query touches at most ~2,600 rows per account, through an index. I will measure it and write the time in the log. At scale, closed weeks never change, so the move would be caching by (account, week, type) or materializing a weekly summary per account-local week. Today that is complexity with no measurable gain.

### Clock

`TimeProvider` is injected. In Development, `Dashboard:NowOverrideUtc = 2026-07-27T14:00:00Z`. The UI shows "Demo clock pinned to Mon Jul 27, 2026 (seed snapshot)".

### API contract

`GET /api/accounts` → `[{ id, name, timezone }]`

`GET /api/accounts/{id}/activity-baseline?eventType=all|call_received|lead_created|appointment_set&week=YYYY-MM-DD`

- `week` is a local Monday, optional; default is the last complete week. `eventType` defaults to `all`.
- Response:
  - `account`, `week { start, end }`, `baseline { start, end, weeks: 8 }` (what we compare against), `eventType`, `nowUtc`
  - `availableWeeks[]`: local Mondays from the first week with data to the last complete week. The current week is not offered.
  - `metrics[4]`, always all four, account level: `{ eventType, current, median, typicalLow, typicalHigh, status, history[9] { weekStart, count }, unusualBaselineWeeks[] { weekStart, count } }`
  - `locations[]` for the requested `eventType`, already sorted: `{ location, current, median, typicalLow, typicalHigh, status, deviation, history[9] { weekStart, count } }`
  - `dataNotes { duplicateRowsIgnored }`
- The response carries everything the view needs to explain why. There is no second endpoint for the detail.

**Edges**: unknown account → 404. Invalid `eventType`, a `week` that is not a Monday, or a week not finished yet → 400 ProblemDetails. Account with no events → 200 with `locations: []` and `status: "no_data"`. Unknown timezone → 422.

**Security, sized to the slice** (auth is out of scope per the prompt): GET only; all SQL parameterized; every query filtered by `account_id`; whitelist for `eventType`; errors as ProblemDetails without stack traces; no open CORS (Angular dev proxy instead); no `innerHTML`; the SA password is local-dev only, labeled, overridable by environment variable. In production the account would come from the token, not from the route.

**State that survives a reload**: URL query params (`account`, `type`, `week`) are the single source of truth. No state library.

## 7. Test plan

Unit tests, written first and watched fail:

- `WeekWindowCalculator`: correct local Monday for America/Chicago and UTC; the week of Mar 2–8, 2026 is 167 h in Chicago (DST starts) and 168 h in Phoenix; "now" mid-week returns the previous full week; "now" exactly at Monday 00:00 local; a `week` that is not a Monday or has not ended is rejected.
- `BaselineStats`: even n; MAD = 0 falls back to the √median floor; account 6's real series `53, 881, 102, 59, 76, 68, 80, 50` with current 87 → median 72, `typical`; the spike week → `above`; a baseline that contains the spike reports it in `unusualBaselineWeeks` and the range does not move; all-zero baseline; integer limits consistent with the classification; fewer than 4 weeks of history → `insufficient_history`.

Integration tests against the seeded database (no Testcontainers, no separate database; the API is read-only, so the tests read the same seeded compose database):

- For 3 cases (account 6 week 2026-07-20; account 6 week 2026-06-01; account 1 week 2026-07-06, which contains the 11266/11267 duplicate in Site C), compute the expected numbers by hand in `sqlcmd` with `AT TIME ZONE`, then pin them in tests and compare with the API.
- Account 20 → `no_data`; account 999 → 404; `eventType=foo` → 400; per-type counts add up to the total.
- Time cap: 30 minutes to get these green. If not, the manual verification goes in the README and I move on.

Frontend tests: restoring filters from the URL (mount with `?account=6&type=call_received` and check what is requested from the API); ordering and labeling of out-of-range locations; the empty state.

Manual pass against the real API: accounts 6, 20, 18 and a single-location account; reload with filters set.

## 8. The view

Principle: **one view, visual, details only on request, and always say what it is compared against.** A static mockup with real seed numbers is the spec (`ai-log/design/`).

Top to bottom:
1. Account picker ("Demo: viewing as") and the pinned-clock notice.
2. **Verdict**: the week and the account's timezone; a sentence with numbers ("Calls is within your normal range (51, normal 29–66). 1 location is outside its own range."); below it, what we compared against ("Compared with your own 8 previous full weeks (May 25 – Jul 19).").
3. **Four metric cards** (All activity, Calls, Leads, Appointments), each with its number, range bar and status. The selected card is the filter; there is no separate type control. That way a deviation in one type shows even when the total is normal.
4. **One row per location** (max 15): name, this week's number, range bar (band = normal range, tick = median, dot = this week), status. Out of range first. No sparkline and no range text in the row; that goes in the detail.
5. **Detail on click**: a 9-week chart with the shaded band and the median, and an explanatory sentence built from the API data.
6. States: loading, error, "No activity recorded yet", and "No baseline yet: we need at least 4 full weeks to know what is normal for you. N weeks to go."
7. If time allows: a week picker from `availableWeeks` (default labeled "last complete"), and two footer cards, "How we decide what is normal" and "Notes about your data" (duplicates excluded, unusual baseline weeks that do not move the range, current week not compared).

Color: out of range is marked with color **and** a label. Above in blue, below in amber, never red/green. The screen says "different from your usual", not "good" or "bad": we do not know whether more calls is good news.

Charts are hand-written SVG (`RangeBar`, `WeekHistoryChart`), no chart or UI library. The view never recomputes status, ranges or order; that logic lives in the API.

## 9. Risks

- **SQL Server on Apple Silicon**: the image is amd64 only. I give it 20 minutes; if it does not come up healthy, I switch to PostgreSQL 16 and say so.
- **Timezone math**: DST weeks are 167 or 169 hours. Covered by unit tests and by the independent T-SQL check.
- **Seed loading**: a large script can time out. If so, smaller batches; the SQL itself is not rewritten.
- **Scope creep in the view**: the frontend list is ordered core first; the "if time allows" items get cut first.
- **Angular CLI vs Node version**: if the latest CLI does not work with the installed Node, I pin the previous major.
- **Overrunning the time cap**: I stop and document what is missing rather than rushing it in.

## 10. Time budget

The prompt asks for 4 to 6 hours of effort. My budget:

| Block | When | Hours |
|---|---|---|
| Planning (seed profiling, rule back-test, product questions, this plan) | Sat morning | 1.5 |
| Walking skeleton + docs | Sat early afternoon | inside the 1.5 above and the 4 below |
| Implementation: backend, aggregation, tests, view, README | Sat evening | 4 |
| Polish: README, AI log, publish | Sun | 0.5 |
| **Total** | | **6** |

The README will report the real time per block. If the evening block goes past 4 hours, I cut and write down what is missing.

## 11. Work order and how the agents split it

One planning session wrote this plan and one closed task list per terminal. Three execution terminals share one working tree, each owning its own files, and commit by explicit path only.

- **Docs terminal**: this PLAN.md (first commit of the repo), `CLAUDE.md`, `ai-log/AGENT_WORKFLOW.md`, `CHECKLIST.md`, the planning record and design references.
- **Backend terminal**: database compose and seed files, schema migration, seed loader, accounts endpoint and test project (afternoon); then `WeekWindowCalculator`, `BaselineStats`, the baseline endpoint, the independent verification, and the README (evening).
- **Frontend terminal** (evening): contract types and API client (against a sample JSON until the endpoint exists), the dashboard view, URL state, tests, manual pass.

Order: plan first in git → database and walking skeleton → pure functions with tests → endpoint → verification → view → README. The API contract above is fixed before backend and frontend run in parallel; if it has to change, it is recorded as a deviation.

Every terminal keeps `ai-log/notes-<terminal>.md` during the work: every time I correct or redirect the agent, and every mistake a test or cross-check catches.
