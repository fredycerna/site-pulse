# CLAUDE.md

Context for AI coding agents working in this repository. Short on purpose: every rule here has cost me time on a real project at least once.

## What this is

A take-home slice for ticket DASH-247: a dashboard that tells a customer whether last week's activity was normal for them, per location. Read `PLAN.md` first. It is the plan as written before the code and it is **frozen**: never edit it. If reality differs, record it in `README.md` under "Deviations from the plan".

## Stack and layout

- `api/` .NET 8 Web API, EF Core (migrations + raw SQL for aggregation), SQL Server 2022 in Docker
- `api.tests/` xUnit: unit tests for pure functions, a few integration tests against the seeded database
- `web/` Angular (standalone components, signals, no state or UI library)
- `db/seed/` the provided `schema.sql` and `seed.sql`, byte-identical to the starter. **Read-only.**
- `ai-log/` raw AI interaction log. A deliverable, not an afterthought.

## Commands

```bash
docker compose up -d                 # SQL Server
dotnet tool restore                  # local dotnet-ef
dotnet run --project api             # migrates and seeds on first run (Development)
dotnet run --project api -- seed     # migrate + load the seed if empty, then exit
dotnet test                          # needs the database up
cd web && npm install && npm start   # dev server with proxy to the API
cd web && npm test
```

## Rules for data and queries

- **Every query is scoped by account.** No query reads activity without `account_id` in the predicate. There is no auth in this slice, so the account comes from the route; in production it would come from the session token, never from a client-supplied value.
- **Timestamps are UTC in the database.** Local week boundaries are computed in C# from the account's IANA timezone and passed to SQL as UTC ranges. Ranges are half-open: `>= start AND < end`. Never wrap the column in a function inside a predicate; it kills the index.
- **Raw SQL is always parameterized.** Interpolated `SqlQuery<T>` only. No string concatenation, ever.
- **Never change, regenerate or clean the seed.** Messy rows are handled at read time (see the `activity_events_deduped` view) and reported to the user, not silently fixed.
- **Verify numbers by an independent path.** An aggregate is not "right" because the code that produced it says so. Compute the expected value another way (hand-written SQL) before pinning it in a test.
- **Measure before optimizing.** No cache, no summary tables, no extra infrastructure until a measurement says so. Write down what you would do at scale instead of building it.

## Rules for code

- Thin endpoint → service → one query class behind an interface, plus pure functions for the math. Dependency injection throughout. No generic repository, no mediator, no extra layers.
- Validate input explicitly at the endpoint. Errors are `ProblemDetails` with the right status code (400 invalid input, 404 unknown account, 422 unusable data). No stack traces in responses.
- The API is read-only. Do not add write endpoints.
- Frontend: the API owns the logic. The view never recomputes status, ranges or ordering. URL query params are the single source of truth for filters. Every view handles loading, empty, error and insufficient-history states. No `innerHTML`.
- Show the user what a status is compared against. A label without its baseline is a bug.
- Smaller and correct beats broader and half-working. If time runs out, stop and document; do not rush a feature in.

## Rules for tests

- Write the test first for pure logic, and watch it fail. A new test that passes without the change proves nothing.
- Test the parts that can be wrong in interesting ways: timezone and DST boundaries, zero and low volumes, outliers, duplicates, empty accounts. No coverage farming.
- Integration tests read the already-seeded database. No extra containers, no test-only databases.

## Before every commit

1. Reread the whole diff, not just the last edit.
2. Build clean; count errors and warnings before reading test output.
3. New tests fail without the change.
4. Edge cases named out loud: empty, zero, boundary, DST week, page reload, unknown id.
5. Account scoping and parameterization checked at the exact lines that touch SQL.

## Git

- One-line commit messages, conventional prefix, English. No trailers, no tool footers.
- Stage and commit explicit paths only (`git add <paths>`, `git commit -m "..." -- <paths>`). Never `git add -A`: other terminals share this working tree.
- Never `--no-verify`. If a hook fails, fix the cause.
- No pushes, no repository creation, no publishing. The human does anything public.

## Working with the human

- The plan decides. Do not re-open decisions it already made; do not stop to ask permission for what it already covers.
- Product and scope decisions belong to the human. Ask once, with a recommendation.
- Written state can be stale. When a document and the code or data disagree, trust what you just measured and say so.
- Do not wait idle on long tasks (image pulls, installs, test suites): run them in the background and take the next independent task.
- **Log every redirection as it happens** in `ai-log/notes-<terminal>.md`: time, what you proposed, what the human decided, why. Also log your own mistakes when a test or a cross-check catches them. This is the most valuable part of the AI log and it cannot be reconstructed afterwards.

## Done means

Runs from a clean clone with the README steps; tests green; aggregates cross-checked against the seed; edge states visible in the UI; README, PLAN.md and AI log committed; nothing public done without the human.
