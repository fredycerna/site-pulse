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
