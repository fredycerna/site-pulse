# Work checklist

Living list. A box is ticked only when it was verified for real, with the evidence named next to it.

## Deliverables from the prompt

- [ ] Public repo of my own (not a fork, distinct name, no starter `docs/`) with backend, frontend and migrations
- [ ] Migrations with the stack's standard tool (EF Core), not hand-run DDL
- [ ] Seed loaded as-is (`db/seed/` byte-identical to the starter, checked with `shasum`)
- [ ] Endpoint with a real aggregation
- [ ] Angular view with an input whose state survives a reload
- [ ] Tests that run (one line in the README on how)
- [ ] `PLAN.md` committed before any implementation and never edited (first commit of the repo)
- [ ] `README.md`: how to run, interpretation, assumptions, what was deferred and why, what I would do with another day, how to run the tests, deviations from the plan, real time per block
- [ ] AI log in `ai-log/`: raw prompts and responses, where I rejected or corrected the agent, short reflection, one line on tools and models
- [ ] `CLAUDE.md` for the repo (optional, adds)

## Evaluation criteria (and the evidence that covers each)

- [ ] Spec interpretation: the question the screen answers, written in `PLAN.md` §1 and shown as the verdict sentence in the UI
- [ ] Planning before code: `PLAN.md` is the first commit; git log shows the order
- [ ] Data judgment: seed findings in `PLAN.md` §2; duplicates handled in the `activity_events_deduped` view; spike kept and listed as an unusual baseline week
- [ ] Choice of "normal" backed by evidence: back-test table in `PLAN.md` §5 (6 % vs 36 % / 51 % / 55 %)
- [ ] Correct aggregates: integration tests pinned to numbers computed by independent hand-written SQL
- [ ] Timezone correctness: DST unit tests (167 h Chicago, 168 h Phoenix)
- [ ] Multi-location usefulness: locations ranked by deviation, out-of-range first
- [ ] Actionable on Monday morning: default is the last complete week; every status shows what it is compared against
- [ ] Code structure: thin endpoint → service → one query class + pure functions, DI throughout
- [ ] State survives a reload: `account`, `type`, `week` in the URL; frontend test restores them
- [ ] Edge states in the UI: loading, error, no activity, insufficient history
- [ ] AI-first workflow, honestly logged: `ai-log/notes-*.md`, exports, `AGENT_WORKFLOW.md`
- [ ] Scope discipline: deferred items named in the README with the reason

## Data checks

- [ ] Duplicates: 12 exact duplicate rows ignored at read time; `dataNotes.duplicateRowsIgnored` matches per account; account 1 week 2026-07-06 Site C counts the 11266/11267 pair once
- [ ] Timezones: weeks bucketed in each account's IANA zone; account 18 (`UTC`) and a Phoenix account (no DST) behave correctly
- [ ] Zero weeks count as 0, not skipped (account 6 Site G and Site N, account 18 Site C)
- [ ] Empty account: account 20 returns `no_data` and the UI shows "No activity recorded yet"
- [ ] Spike: account 6 week 2026-06-01 is `above`; the following 8 weeks list it in `unusualBaselineWeeks` and their range does not move
- [ ] NULL `outcome` / `duration_seconds` events are counted
- [ ] Last complete week (2026-07-20) flags 12/Site F, 15/Site C, 19/Site A as `above`, none `below`
- [ ] Per-type counts add up to the `all` count
- [ ] "Now" pinned to 2026-07-27T14:00:00Z in Development and shown in the UI

## Security checks

- [ ] API is GET only
- [ ] All SQL parameterized (interpolated `SqlQuery<T>`, no concatenation), checked at the exact lines
- [ ] Every activity query filtered by `account_id`
- [ ] `eventType` validated against a whitelist
- [ ] Errors are ProblemDetails with the right status (400 / 404 / 422), no stack traces
- [ ] No open CORS; the Angular dev proxy is used instead
- [ ] No `innerHTML` in the view
- [ ] SA password is local-dev only, labeled, overridable by environment variable
- [ ] README says that in production the account comes from the token, not the route

## Before publishing

- [ ] Review every export in `ai-log/` before it goes public (nothing private, nothing outside this exercise)
- [ ] Clean clone runs with the README steps; tests green
- [ ] No starter `docs/` in the repo; repo name is my own
- [ ] Create the public repo and push (done by me, not by an agent)
- [ ] Reply to the prompt email with the repo link (draft first)
