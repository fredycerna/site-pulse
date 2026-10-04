# Work checklist

Living list. A box is ticked only when it was verified for real, with the evidence named next to it.

## Deliverables from the prompt

- [ ] Public repo of my own (not a fork, distinct name, no starter `docs/`) with backend, frontend and migrations
- [x] Migrations with the stack's standard tool (EF Core), not hand-run DDL — `InitialSchema` + `AddDedupedEventsView`
- [x] Seed loaded as-is (`db/seed/` byte-identical to the starter, checked with `shasum`) — block A, Q1 hand-off
- [x] Endpoint with a real aggregation — `ActivityQueries.GetWeeklyActivityAsync`, one parameterized SQL query
- [x] Angular view with an input whose state survives a reload — Q2: URL params, frontend test, manual reload in Chrome
- [x] Tests that run (one line in the README on how) — `dotnet test` 60/60, `npm test` 41/41 on the final tree and from a clean clone (Q5, 2026-10-03 23:19); 54/54 and 29/29 before the end-to-end fixes
- [x] `PLAN.md` committed before any implementation and never edited (first commit of the repo) — `b82d0d8` is the root commit and the only commit touching PLAN.md
- [x] `README.md`: how to run, interpretation, assumptions, what was deferred and why, what I would do with another day, how to run the tests, deviations from the plan, real time per block — read end to end against the final state (Q5, `74854fd`); the time table closes with block D
- [x] AI log in `ai-log/`: raw prompts and responses, where I rejected or corrected the agent, short reflection, one line on tools and models — `ai-log/README.md` (index, tools and models, reflection), `notes-q1.md` … `notes-q5.md`, exports `01` … `05` (`7b4ceba`); the planning session's own export is still pending and is added by that session
- [x] `CLAUDE.md` for the repo (optional, adds) — `383e344`

## Evaluation criteria (and the evidence that covers each)

- [x] Spec interpretation: the question the screen answers, written in `PLAN.md` §1 and shown as the verdict sentence in the UI (Q2: verdict with numbers and baseline)
- [x] Planning before code: `PLAN.md` is the first commit; git log shows the order
- [x] Data judgment: seed findings in `PLAN.md` §2; duplicates handled in the `activity_events_deduped` view; spike kept and listed as an unusual baseline week — plus the errata in the README where the plan's numbers were wrong
- [x] Choice of "normal" backed by evidence: back-test table in `PLAN.md` §5 (6 % vs 36 % / 51 % / 55 %); remeasured by the app in local weeks: 6.2 % (README, errata 2)
- [x] Correct aggregates: integration tests pinned to numbers computed by independent hand-written SQL — 3 cases, `db/verify/verify-aggregates.sql`; two deliberate breakages of the code each made them fail
- [x] Timezone correctness: DST unit tests (167 h Chicago, 168 h Phoenix); integration cases in New York and Chicago
- [x] Multi-location usefulness: locations ranked by deviation, out-of-range first — `OrderByAttention` service test; Q2 test that the view does not re-sort
- [x] Actionable on Monday morning: default is the last complete week; every status shows what it is compared against — `week` and `baseline` in every response; verdict line in the view (Q2)
- [x] Code structure: thin endpoint → service → one query class + pure functions, DI throughout
- [x] State survives a reload: `account`, `type`, `week` in the URL; frontend test restores them (Q2)
- [x] Edge states in the UI: loading, error, no activity, insufficient history (Q2 manual pass: account 20, week of Feb 2); "N weeks to go" from the API's `historyWeeks` (Q2 `f2c0d8b`)
- [x] End-to-end pass in a real browser, view diffed against the API and the API against hand SQL; findings fixed with failing-first tests; second pass clean except F11 (exceptional weeks per location, needs a contract change) — `ai-log/e2e-report.md` (Q4)
- [x] AI-first workflow, honestly logged: `ai-log/notes-*.md`, exports, `AGENT_WORKFLOW.md` (now with the end-to-end and publishing terminals and the validation loop), reflection in `ai-log/README.md`
- [x] Scope discipline: deferred items named in the README with the reason

## Data checks

- [x] Duplicates: 12 exact duplicate rows ignored at read time (the view returns 12,614 of 12,626); `dataNotes.duplicateRowsIgnored` counts the duplicates dropped **inside the 9 weeks shown** (not the whole account), checked against the independent SQL in 3 cases (1, 3 and 1); account 1 week 2026-07-06 Site C counts the 11266/11267 pair once (4, raw table 5)
- [x] Timezones: weeks bucketed in each account's IANA zone; account 18 (`UTC`) and a Phoenix account (no DST) behave correctly — account 7 (Phoenix) week of Mar 16 matches the independent SQL; account 18 by API and Q2 manual pass
- [x] Zero weeks count as 0, not skipped (account 6 Site G and Site N, account 18 Site C) — via API: Site G Apr 13 and Jun 29, Site N Feb 23, 18/Site C Mar 23 come back with 0 and a status
- [x] Empty account: account 20 returns `no_data` and the UI shows "No activity recorded yet" — integration test + Q2 manual pass
- [x] Spike: account 6 week 2026-06-01 is `above`; the following 8 weeks list it in `unusualBaselineWeeks` and their range does not move — integration tests (Jun 1 and Jul 20) + unit test (a bigger spike gives the same range)
- [x] NULL `outcome` / `duration_seconds` events are counted — the view's DISTINCT keeps them, and the independent SQL (which counts every row) matches
- [x] Last complete week (2026-07-20) flags 12/Site F, 15/Site C, 19/Site A as `above`, none `below` — measured through the API over all 20 accounts
- [x] Per-type counts add up to the `all` count — integration test
- [x] "Now" pinned to 2026-07-27T14:00:00Z in Development and shown in the UI — `appsettings.Development.json`, `nowUtc` in the response, notice in the view (Q2)

## Security checks

- [x] API is GET only — only `MapGet` in `api/`
- [x] All SQL parameterized (interpolated `SqlQuery<T>`, no concatenation), checked at the exact lines — `ActivityQueries.cs`
- [x] Every activity query filtered by `account_id` — the 4 predicates of the aggregation and the LINQ lookup
- [x] `eventType` validated against a whitelist — `BaselineEndpoint.ParseQuery` + tests
- [x] Errors are ProblemDetails with the right status (400 / 404 / 422), no stack traces — 400/404 checked with curl, 422 by unit test, `UseExceptionHandler` configured; a real 500 triggered on 2026-10-03 by stopping the database container (Development environment): `application/problem+json`, title "An error occurred while processing your request.", status 500, no trace; 200 again once the database was back
- [x] No open CORS; the Angular dev proxy is used instead — no CORS in `api/`
- [x] No `innerHTML` in the view — grep over `web/src`
- [x] SA password is local-dev only, labeled, overridable by environment variable
- [x] README says that in production the account comes from the token, not the route

## Before publishing

- [x] Review every export in `ai-log/` before it goes public (nothing private, nothing outside this exercise) — done by the planning session at 22:58, not by a terminal (a terminal that read them would copy them into its own export); what was changed is listed in `ai-log/README.md`
- [x] Clean clone runs with the README steps; tests green — 2026-10-03 22:03, commit `77b7460`, fresh database `SitePulse_CleanClone`: migrations + seed, `dotnet test` 54/54, `npm install` + `npm test` 27/27, `ng build` clean. Found and fixed: a `container_name` conflict when another copy runs on the same machine (README note). `npm start` not run from the clone (ports in use); Q2's manual pass covers the view. Repeated end to end on 2026-10-03 23:19 (Q5) at `74854fd`, fresh database `SitePulse_Q5Clone`: migrations + seed, `dotnet build` 0 warnings, `dotnet test` 60/60, `npm install` + `npm test` 41/41, API and `npm start` from the clone, page checked in Playwright (default view; account 6, week of 2026-06-01, equal to the API; console clean). Found and fixed: a clone in a folder with the same name silently reuses the running container (README note)
- [x] No starter `docs/` in the repo; repo name is my own — `git ls-files` has no `docs/`, `bin/`, `obj/`, `node_modules/` or `dist/`; no absolute local paths outside the exports; the only password is the labeled local-dev SA default; `.playwright-mcp/` ignored; name `site-pulse`
- [ ] Create the public repo and push (done by me, not by an agent)
- [ ] Reply to the prompt email with the repo link (draft first)
