# AI log notes — Terminal Q1 (backend and walking skeleton)

Raw notes written as things happen. Each entry: time, what the agent proposed or did, what was decided, why.
Also the agent's own mistakes when a test or a cross-check caught them.

## 2026-10-03 — Block A

- 13:22 — Session start (Opus 5.5). Repo folder empty: Q3 has not run `git init` yet, so no commits from Q1 until the PLAN.md commit exists. Started Docker Desktop and the SQL Server 2022 image pull (`linux/amd64`) in the background; 20-minute cap for the database runs from the pull.
- 13:24 — Cloned the starter to a scratch folder. `schema.sql` and `seed.sql` sit at the starter root (not under a `db/` folder); the plan's target is `db/seed/`. Seed is one `INSERT` per line (20 accounts + 12,626 events), which makes line-based batching safe for the loader.
- 13:25 — `dotnet tool install` failed: a private NuGet feed in the user-level NuGet config needs credentials. Added a repo-level `nuget.config` that clears sources and uses only nuget.org. Right for a clean clone anyway: the repo must not depend on the machine's feeds.
- 13:29 — Image pulled; container `healthy` and `SELECT 1` answers 6 minutes after the pull started. SQL Server on arm64 via `linux/amd64` emulation works, so no Postgres fallback.
- 13:30 — `dotnet run --project api -- seed`: both migrations applied, 12,646 statements loaded in ~1 s (batches of 500, one transaction). Counts: 20 accounts, 12,626 events; `activity_events_deduped` returns 12,614, so DISTINCT removes exactly the 12 known duplicates and nothing else.
- 13:31 — Anti-vacuity check on the idempotency test: disabled the "accounts not empty" guard and the test failed with a PK violation (the transaction rolled back). Restored. With the container stopped, `/health` returns 503 ProblemDetails and the tests fail with "The seeded database is not reachable...".
- 13:31 — PLAN.md commit from Q3 not there yet (`main` has no commits). Holding all commits; moving on to the Angular shell.
- 13:32 — Angular CLI 22.2.1 requires Node ^22.22.3; this machine has 22.18. Used `@angular/cli@21` as the plan says. `ng new` then failed inside `npm install` with an npm 10.9 internal error ("Cannot read properties of null (reading 'edgesOut')") while resolving vitest's peer deps. Installed once with npm 11 to produce the lockfile; with the lockfile, plain `npm install` on npm 10.9 works. Recorded for the README.
- 13:33 — Shell verified: `ng test` 2/2 green (lists accounts from `/api/accounts`, shows an error on failure), `ng build` clean, and the browser at :4200 lists the 20 real accounts through the dev proxy (no CORS on the API).
- 13:34 — PLAN.md commit `b82d0d8` found; committed tasks 4, 5 and 6 with explicit paths only.

## 2026-10-03 — Block B (evening)

- 21:43 — Session start (Opus 5.5). Read the block A hand-off and the repo: head `6a1e682`, block A complete, so task 1 of block B does not apply. Docker Desktop was off; started it and `docker compose up -d`.
- 21:45 — Reading of "fewer than 4 complete weeks since its first event" (PLAN §5): a baseline week counts as history only if it starts at or after the location's first event. The partial week that contains the first event is left out, so a half week does not pull the median down. The same rule applies at account level.
- 21:58 — My own mistake, caught before running it: the first draft of the "whole-number limits agree with the classification" test checked `low <= x <= high ⇔ typical` using the same limits the code classifies with, which is a tautology. Rewrote it to recompute the band independently in the test and compare against the real-valued band. Mutation check: changing `Ceiling` to `Floor` on the lower limit makes 2 tests fail; restored, green.
- 21:59 — All-zero baseline: by the plan's formula σ = 0, the range is 0–0 and a single event is `above`. I'm following the plan, and this is pinned in a test so it is a visible choice, not an accident. It only happens per event type (for example a location that never had appointments).
