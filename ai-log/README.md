# AI log

The raw record of how this slice was built with AI agents: the plan, each terminal's notes written as things happened, the unedited session exports, the end-to-end report, and a short reflection at the end.

## Read in this order

1. [`AGENT_WORKFLOW.md`](AGENT_WORKFLOW.md) — how the work was split: one planning session, execution terminals with closed task lists, me deciding product and scope.
2. [`00-planning-record.md`](00-planning-record.md) — the planning session (12:20 – 13:20), written from the day plan it produced: seed profiling, the back-test of "normal", my design and where the agent did and did not take it.
3. [`design/`](design/) — my two sketches (in Spanish) and the merged mockup that served as the spec for the view.
4. [`notes-q3.md`](notes-q3.md) — docs terminal: PLAN.md, CLAUDE.md, checklist, planning record; where the 882 error was found and how the errata were decided.
5. [`notes-q1.md`](notes-q1.md) — backend terminal: walking skeleton, aggregation, independent SQL check, `historyWeeks`, first clean clone.
6. [`notes-q2.md`](notes-q2.md) — frontend terminal: the Angular view, contract gaps found by measuring the real API.
7. [`notes-q4.md`](notes-q4.md) — end-to-end terminal: the browser pass, findings, fixes with failing-first tests, second pass.
8. [`e2e-report.md`](e2e-report.md) — the end-to-end matrix, findings F1–F12 with classification and outcome; screenshots in [`e2e/`](e2e/).
9. [`notes-q5.md`](notes-q5.md) — publishing terminal: README polish, this index, clean-clone test, final checklist.
10. Raw session exports (`/export` from Claude Code, unedited except as stated below):
    - [`01-q3-docs.md`](01-q3-docs.md) — docs terminal
    - [`02-q1-walking-skeleton.md`](02-q1-walking-skeleton.md) — backend terminal, block A
    - [`03-q1-backend.md`](03-q1-backend.md) — backend terminal, evening
    - [`04-q2-frontend.md`](04-q2-frontend.md) — frontend terminal
    - [`05-q4-e2e.md`](05-q4-e2e.md) — end-to-end terminal
    - Planning session export — pending; added by the planning session at the end, trimmed.
    - Publishing terminal (Q5) export — pending; I run `/export` when the session closes.

## Tools and models

All sessions in Claude Code: planning on Claude Fable 5.1; the execution terminals (docs, backend, frontend, end-to-end, publishing) on Claude Opus 5.5; the end-to-end pass drove Chromium through the Playwright MCP tools, with nothing added to the repository.

## What was cleaned before publishing

The exports were reviewed and cleaned by the planning session, not by a terminal (what a terminal reads ends up in its own export). What changed:

- Local paths outside the repository replaced by `<local-plan>`, `<local-notes>` and `<local-workspace>` (in `01` and `02`).
- Three blocks replaced by a `[redacted before publishing: …]` mark in the same place: in `01`, 20 lines quoted from a private calendar note and 3 lines from a private file unrelated to this exercise (a terminal read them while looking for the plan); in `04`, the names of 2 unrelated containers that showed up in a `docker ps`.
- Nothing else was touched. `03` and `05` are identical to the original.

## Reflection

**How I organized the work.** I made the product and scope calls. A planning session wrote the plan with me and later validated each terminal's work; execution terminals ran closed task lists. `PLAN.md` was the first commit and was never touched. Mine: the question the screen answers, each location against its own history, Angular because the email rules out other stacks. The design: one visual view saying whether the week was normal, detail on request rather than a mass of data up front, every "not normal" saying why and against what. My sketch merged with the agent's mockup; from mine came the single view, a card per type, data notes and colors with no good/bad judgment. My "6 of 8 weeks" rule was dropped: it flagged 36 % of location-weeks, against 6 %. Hand-written SVG charts: two small charts did not need a library.

I doubted the integration tests: in my experience they cost much back-and-forth for little return, and most teams cover that level with end-to-end suites. Planning's argument won: the aggregation lives in SQL, and a unit test never touches it. We bounded the cost: no Testcontainers, no running API, the seeded database (the API is read-only), a 30-minute cap. Green in about 2 minutes.

**What worked.** Every number was checked by a path other than the code: hand-written SQL against the API, the API against the view. For the end-to-end pass I chose one terminal owning the whole repo, since a defect can sit in the view, the API or the contract: first the full pass without touching code, each finding classed as code defect or data condition, validation before fixing, each fix with a test that fails without it. One principle: when data is missing or anomalous, the screen says what it is not showing and why, so nobody reads it as an error. The validation loop caught two risks before they happened, which planning flagged and I approved and relayed: `historyWeeks` had to come from the same computation that decides `insufficient_history`, and the frontend's series was not evidence, as it came from the backend's own API (a warning in advance: the backend had already pinned its numbers from hand SQL).

**Where we were wrong.** The plan gave account 6 a spike of 882 events, the all-accounts total for that day. Planning misattributed it, the docs terminal copied it unchecked, and a cross-review caught it after the commit. Two small slips, a tautological test and log times written without a clock, were caught in the same session.

In the end-to-end pass, with 4 weeks of history the screen said "your 8 previous weeks" and drew weeks before any data as zeros (F1). A median of 0 gave a 0–0 range, so a single event was "above": 199 rows in the seed, and a unit test had pinned that behavior on purpose (F2). The tests proved the code did what we said; the screen against the data showed what we said was wrong.

**What I would do differently.** Cross-check every number in the plan against the data before freezing it, list the degenerate cases of the rule (median 0, short history) at design time, and run the browser pass before the README.
