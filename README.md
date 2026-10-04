# Site Pulse — DASH-247

A dashboard that answers one question for a multi-location customer on Monday morning:

> "Was last week normal for us, and which location stepped outside its own normal?"

Each location's last complete week (Monday to Sunday in the account's timezone) is compared with its own 8 previous complete weeks. The screen says what it compared against, ranks the locations that are outside their range, and shows the history behind every verdict.

`PLAN.md` is the plan as written before the code, frozen at its first commit. This README covers what was built, where reality differed, and the errata to the plan.

## Run it

Requirements: Docker, .NET SDK 8 or later (the projects target `net8.0` with `RollForward=Major`), Node 22.

```bash
docker compose up -d                 # SQL Server 2022 on :1433 (amd64 image; runs under emulation on Apple Silicon)
dotnet tool restore                  # local dotnet-ef
dotnet run --project api             # API on http://localhost:5290; in Development it migrates and loads the seed on first run
cd web && npm install && npm start   # Angular on http://localhost:4200, proxies /api to :5290
```

`dotnet run --project api -- seed` applies the migrations, loads `db/seed/seed.sql` if the database is empty, and exits.

The compose file names its container `site-pulse-sql`. If a container with that name already exists (for example, another copy of this repository on the same machine), `docker compose up -d` fails with a name conflict. Reuse the running server and point this copy at its own database, so migrations and the seed run from scratch:

```bash
export ConnectionStrings__SitePulse='Server=localhost,1433;Database=SitePulse_Copy;User Id=sa;Password=SitePulse_Dev_Only_2026!;TrustServerCertificate=True;Encrypt=True'
export SITEPULSE_TEST_CONNECTION="$ConnectionStrings__SitePulse"
dotnet run --project api -- seed && dotnet test
```

Configuration:

| Setting | Default | Notes |
|---|---|---|
| `MSSQL_SA_PASSWORD` (compose) | `SitePulse_Dev_Only_2026!` | Local development only. Override in your shell or a `.env` file. |
| `ConnectionStrings__SitePulse` (API) | points at the compose database | Same password as above. |
| `Dashboard__NowOverrideUtc` (API) | `2026-07-27T14:00:00Z` in Development | Pins "now" to the seed snapshot; see "Pinned clock". |
| `SITEPULSE_TEST_CONNECTION` (tests) | the compose database | Integration tests read the seeded database. |

**Tests**: `dotnet test` from the repository root (needs the database up and seeded), and `cd web && npm test`.

## What I built

**API** (`api/`): two read-only endpoints.

- `GET /api/accounts` → `[{ id, name, timezone }]`
- `GET /api/accounts/{id}/activity-baseline?eventType=all|call_received|lead_created|appointment_set&week=YYYY-MM-DD`

The second one returns everything the view needs in one response: the account, the week judged and the baseline range it is compared with, the four account-level metrics (all, calls, leads, appointments) with their normal range, status, 9-week history and unusual baseline weeks, one row per location for the selected type already sorted by attention, the weeks that can be picked, and data notes. Errors are `ProblemDetails`: 400 for invalid input (unknown `eventType`, a `week` that is not an ISO date, not a Monday, or not finished), 404 for an unknown account, 422 for an account whose timezone cannot be resolved.

Layers, as planned: thin endpoint (`BaselineEndpoint`) → `BaselineService` (orchestration) → `IActivityQueries` (`ActivityQueries`, the only class with SQL) plus two pure functions, `WeekWindowCalculator` and `BaselineStats`.

**View** (`web/`): one Angular page, built from the mockup in `ai-log/design/`: account picker and pinned-clock notice; a verdict sentence with numbers and its baseline; four metric cards where the selected card is the type filter; one row per location with a range bar, out-of-range first; a 9-week chart on click; loading, error, no-activity and not-enough-history states (with "N weeks to go" from `historyWeeks`); a week picker; and footer cards explaining the rule and the data notes. Charts are hand-written SVG. Filters live in the URL (`account`, `type`, `week`), so a reload or a pasted link restores the view. The view renders what the API decided; it never recomputes status, ranges or order.

## How I read the ticket

"Is this normal for us?" needs a baseline, and the product notes stress multi-location customers and acting on Monday morning. So:

- **Last week** = the last complete Monday-to-Sunday week **in the account's timezone**. A half week is not comparable.
- **For us** = each location against **its own** previous 8 complete weeks, not against other locations or accounts.
- **Act on it** = rank by how far outside the range a location is, and always show what it was compared against.
- **Metric** = weekly event volume, filterable by type. Quality (missed-call rate, no-shows) is a different question and is deferred.

## What "normal" means

For each series (a location, or the account as a whole): median ± 2σ with σ = max(1.4826·MAD, √median), shown as whole numbers ("normal 24–121"). A value equal to a limit is typical. Fewer than 4 weeks of history → `insufficient_history`.

- **Median and MAD** ignore how big an outlier is. Account 6's 880-event spike week sits in the baseline of the following eight weeks and does not move their range; it is listed as an unusual baseline week instead of being hidden.
- **The √median floor** is the noise you expect from small counts (2 to 15 events per location-week). Without it, a location with the same count every week gets a zero-width range.
- **Why not percentages, quartiles or a moving average**: measured against the seed, this rule flags about 6 % of location-weeks; the alternatives flag 36 % to 55 % (PLAN.md §5). A screen that flags half the locations every Monday gets ignored.
- **History** starts at the location's first event, and only complete weeks count: a baseline week counts if it starts at or after the first event. The partial week of the first event is left out so a half week does not drag the median down. The same rule decides which weeks can be picked: `availableWeeks` starts at the first complete week (Feb 2 for every account in the seed, whose first events are on Sun Feb 1). The response carries `historyWeeks`, the exact number that decided `insufficient_history`.
- **All-zero baseline** (for example, a location that has never had an appointment): by the formula the range is 0–0, and a single event is `above`. That is pinned in a test as a visible choice.

## Decisions and trade-offs

- **EF Core for the schema, raw SQL for the aggregation.** The prompt asks for migrations with the stack's standard tool, which is EF Core migrations. The aggregation is one interpolated `SqlQuery<T>`, so every value is a parameter, and EF acts as a micro-ORM there. Dapper would have meant a second tool for migrations. The cost: the SQL is a string, so it is covered by integration tests against the seeded database rather than by the compiler.
- **One round trip for the dashboard.** One query reads the deduplicated view, joins it against the 9 week windows (a `VALUES` list of 10 UTC boundaries) and groups by location, event type and week. It also returns each location's first event and how many duplicate rows were dropped. The account lookup is a separate small LINQ query.
- **Week boundaries in C#, counting in SQL.** The accounts store IANA zone names, which T-SQL does not understand. C# computes the 10 boundaries with `TimeZoneInfo` (DST weeks are 167 or 169 hours) and passes them as UTC. The predicate stays a plain range on `occurred_at`, which can seek the `(account_id, occurred_at)` index. Converting every row with `AT TIME ZONE` inside the query would not.
- **Duplicates handled at read time.** The 12 double-ingested rows stay in the table as delivered. The view `activity_events_deduped` (`SELECT DISTINCT` over every column but `id`) is the one place the rule lives. The response reports how many were dropped from the weeks shown.
- **Pinned clock.** The seed ends on Mon 2026-07-27; with the real clock every week would be empty. `TimeProvider` is injected, and Development pins it to 2026-07-27 14:00 UTC. The UI says so.
- **State in the URL.** `account`, `type` and `week` query params are the only source of truth for filters: reload-safe, shareable, no state library.
- **No cache.** Measured: 20 requests for the largest account (15 locations) take a median of 19 ms end to end, with a max of 35 ms. At scale, closed weeks never change, so the move would be caching per (account, week, type) or a weekly summary materialized per account-local week. Today that would be complexity with no measurable gain.

## Verifying the numbers

An aggregate is not right just because the code that produced it says so. `db/verify/verify-aggregates.sql` computes the same numbers by a different path: it does not use the view (it deduplicates with `GROUP BY` over every column but `id`) or the C# boundaries (it converts each row with `AT TIME ZONE` and the Windows zone name, and buckets by local Monday). The usage is in the file header.

Three cases were checked cell by cell and pinned in `api.tests/Integration/AggregateVerificationTests.cs`:

| Case | Why | Result |
|---|---|---|
| Account 6, week of 2026-07-20 | 15 locations; the baseline contains the spike | 4 types × 9 weeks match; `all` = 87, normal 24–121, typical; the Jun 1 week (880) listed as unusual |
| Account 6, week of 2026-06-01 | The spike week itself | All counts match; all four metrics `above` |
| Account 1, week of 2026-07-06 | Holds the duplicate pair 11266/11267 (Site C) | Counts match; Site C = 4 (the raw table has 5) |

Both ways of breaking the code were tried on purpose: counting from the raw table instead of the view, and bucketing weeks in UTC instead of the account's zone. Each made these tests fail.

Also covered: account 20 (no events) → `no_data` on all four metrics and no locations; account 999 → not found; per-type location counts add up to the total; an early week (account 6, Feb 16) reports `historyWeeks` that were computed by hand.

## Security, sized to the slice

Authentication is out of scope per the prompt, so the account id comes from the route. **In production it would come from the session token, never from a client-supplied value.** Within that: the API is GET-only; every query has `account_id` in its predicate; all SQL is parameterized; `eventType` is a whitelist; errors are `ProblemDetails` without stack traces; no open CORS (the Angular dev server proxies `/api`); no `innerHTML` in the view; and the SA password is a labeled local-dev default that can be overridden.

## Deviations from the plan

1. **`nuget.config` in the repository** that clears package sources and uses nuget.org only. A private feed in the machine's user-level config broke the restore; a clean clone must not depend on the machine's feeds.
2. **Angular CLI 21 instead of 22.** CLI 22 requires Node ≥ 22.22.3 and the machine has 22.18. The plan allowed this fallback.
3. **The `web/` lockfile was generated with npm 11.** Without a lockfile, npm 10.9 fails with an internal error ("edgesOut") while resolving peer dependencies; with the lockfile, `npm install` on npm 10.9 works.
4. **`site-pulse.sln` at the root** so `dotnet test` runs from there.
5. **`historyWeeks` added to the contract** (in `metrics[]` and `locations[]`). It was a gap in PLAN.md's contract, found by the frontend terminal: "No baseline yet… N weeks to go" had no field, and deriving it from `availableWeeks` gave a wrong number (that list started on the partial week of the first event; see item 10). Fredy approved it on one condition: the number comes from the same computation that decides `insufficient_history`, so the two cannot disagree.
6. **Nullable `median`, `typicalLow` and `typicalHigh`.** The plan did not say what they hold when there is no range; they are `null` for `insufficient_history` and `no_data`.
7. **`duplicateRowsIgnored` counts the 9 weeks shown**, not the whole account. The plan did not pin the scope; the view words it as "excluded from the weeks shown".
8. **`db/verify/verify-aggregates.sql`** was added so the independent check can be repeated.
9. **Integration tests call the service directly** with the seeded database (as planned: no `WebApplicationFactory`). The HTTP mapping (404, 400, 422 as `ProblemDetails`) was checked with curl, and the query-string validation is a pure function with its own tests. A non-numeric account id (`/api/accounts/abc/...`) gets a 404 from the route constraint, not a 400.
10. **The partial week of the first event is neither history nor pickable.** The plan said "fewer than 4 complete weeks since its first event" and "`availableWeeks` from the first week with data". In the seed, the first week with data (Jan 26) holds only its Sunday. A baseline week counts as history only if it starts at or after the first event, and `availableWeeks` follows the same rule, so it starts at Feb 2. A `week=2026-01-26` passed by hand is still accepted and comes out `insufficient_history`.

## Errata to PLAN.md

`PLAN.md` is frozen at its first commit. A cross-review by the planning session after that commit found these errors. They are corrected here instead of in the plan.

1. **§2, the spike.** 882 is the total across **all accounts** on 2026-06-03, not account 6's. Account 6 alone logged 805 events that day, against about 13 on one of its normal weekdays. The 881-event week for account 6 is correct.
2. **§2 and §5, the back-test.** The flag rates (6 %, 36 %, 51 %, 55 %) and the locations flagged for the week of Jul 20 came from an exploratory run that bucketed weeks in UTC. The app buckets weeks in each account's local time, so exact counts can differ slightly. Measured by the app: the chosen rule flags **73 of the same 1,173 location-weeks (6.2 %)**, and 92 of 1,396 (6.6 %) if every location-week with at least 4 weeks of history is included. For the week of 2026-07-20 it flags the same three locations, all `above`: account 12 Site F (11, normal 1–10), account 15 Site C (12, normal 2–10), account 19 Site A (13, normal 2–12). In local weeks, account 6's spike week holds 880 events (not 881). Its baseline for Jul 20 is `53, 880, 102, 59, 76, 69, 79, 50`, which gives a median of 72.5 and a normal range of 24–121, instead of §7's UTC series with a median of 72. The unit test keeps §7's series as a pure input; the integration tests use the local numbers.
3. **§10, time budget.** Real figures: planning about 1 h (12:20 to 13:20); walking skeleton and docs about 15 min, with two terminals in parallel. The evening block stays at 4 h and Sunday at 0.5 h. Actual time per block is reported above.

## Known limitations

- **The history rule is conservative.** A baseline week counts only if it starts at or after the location's first event, so a location whose first event lands on a Monday after midnight loses that whole week. In the seed, account 4 (America/Chicago) on the week of Mar 2: Site D (first event Sun Feb 1) has its 4 weeks and a range, while Sites A, B and C (first events Mon Feb 2 at 06:17 and 11:11, and Tue Feb 3 at 02:01, local) have 3 and show "No baseline yet", although the account as a whole has a range. The cost is one extra week of waiting for a new location; the alternative would let a partial first week drag its median down.

## Deferred, and why

- **Quality metrics** (missed-call rate, no-show rate): a different question from volume, with its own baseline problems (rates on tiny denominators).
- **Comparing locations with each other**: "normal for us" means each location against itself.
- **The current partial week**: a half week is not comparable with full weeks; it is never offered.
- **Auth, export, alerts, ML/forecasting**: out of scope per the ticket.
- **Cache or summary tables**: not needed at the measured 19 ms (see above).

## With another day

- An HTTP-level test layer (`WebApplicationFactory` over the seeded database) so status codes and JSON shape are pinned in CI, not just checked with curl.
- A `historyWeeks`-style explanation for `unusualBaselineWeeks` in the location detail, not only at account level.
- Quality metrics as a second tab, starting with missed-call rate, with a minimum-volume rule before any status is shown.
- Run the integration tests in CI against the compose database, and add a seeded-database health check to `docker compose`.

## Real time per block

| Block | When (2026-10-03, local) | Evidence | Time |
|---|---|---|---|
| Planning: seed profiling, rule back-test, product questions, PLAN.md | 12:20 – 13:20 | planning session | 60 min |
| Walking skeleton (backend terminal) and docs (docs terminal), in parallel | 13:20 – 13:38 | Q1 notes start 13:22; Q3 notes start 13:26; first commit `b82d0d8` 13:32; last commit `6a1e682` 13:38 | 18 min |
| Evening, backend terminal | ~21:40 – 22:03 | Q1 notes start 21:43; commits `c3dd3e1` 21:46 … the README and checklist commit at 22:03 | 23 min |
| Evening, frontend terminal (in parallel) | ~21:40 – 21:55 | Q2 notes start 21:44; commits `441b163` 21:46 … `5cc1204` 21:54 | 15 min |
| Sunday: README polish, AI log, publish | pending | | |

The evening block came in well under its 4 h budget (about 25 minutes per terminal, in parallel). The time saved went into verification and the clean-clone test, not features.

## AI log

`ai-log/` holds the raw record: `00-planning-record.md` (the planning session), `notes-q*.md` (each terminal's notes, written as things happened, including every correction from me and every mistake a test or cross-check caught), the session exports, and `AGENT_WORKFLOW.md` (how the terminals were split and which models ran them).
