# How the agents were organized

A light planner / executor split. One planning session decides; a few execution terminals run closed task lists; I confirm product decisions and review the output. No orchestration framework, just documents and discipline.

## Roles

| Role | What it does | What it does not do |
|---|---|---|
| **Me** | Product and scope decisions, review of plans and diffs, every public action (publishing, email) | Re-explain context to each terminal: the plan carries it |
| **Planning session** (one, larger model) | Reads the ticket and starter, profiles the seed data, back-tests candidate rules, asks me the product questions, writes the day plan with one closed task list per terminal | Write feature code |
| **Execution terminals** (one per theme) | Run their list top to bottom, self-review before each commit, log redirections as they happen | Re-decide priorities, touch another terminal's files, do anything public |

Terminals used: **docs** (PLAN.md, agent context, checklist, planning record), **backend** (schema, seed, aggregation, tests), **frontend** (the Angular view).

Models, all in Claude Code:

| Session | Model |
|---|---|
| Planning | Claude Fable 5.1 |
| Docs terminal | Claude Opus 5.5 |
| Backend terminal | Claude Opus 5.5 |
| Frontend terminal | Claude Opus 5.5 |

## The loop

1. **Measure, then plan.** The planning session works from the data as it is today, not from assumptions: it loaded the seed into a scratch database and measured ranges, duplicates, nulls, the outlier day and per-location volumes before proposing anything.
2. **Decisions with evidence.** Where two designs competed, they were tested against the seed (for example, three candidate definitions of "normal" compared by how many location-weeks each would flag). I made the product calls; the planning session closed the technical ones and wrote down why.
3. **Closed lists.** Each terminal gets: tasks in order, the decisions already made, the files it owns, what is out of scope, a cut line if time runs out, and the commit message for each step.
4. **Run without stops.** A terminal works its list end to end. It asks me only for product questions; it escalates back to planning only if the real code or data contradicts the plan in a way that changes scope, architecture or data safety.
5. **Self-review before each commit.** Whole diff reread, new tests proven to fail without the change, edge cases enumerated, SQL scoping and parameterization checked.
6. **Plan first in git.** `PLAN.md` is the first commit and is never edited. Deviations go to the README.

## Guardrails every terminal carries

- File ownership per terminal; commits by explicit path only, since terminals share one working tree.
- Shared resources (the database container) are owned by one terminal; the others reach it through the API.
- Nothing public without me.
- The AI log is written during the work: each terminal keeps `ai-log/notes-<terminal>.md` with every correction I made and every mistake a test or cross-check caught, and I export each session's conversation at the end.

## Why this shape

- Planning and execution need different things: planning needs breadth, measurement and questions; execution needs a narrow scope and no interruptions. Mixing them in one session produces either unplanned code or a plan that keeps being re-opened.
- Closed lists make the terminals fast and make my review cheap: I compare the diff with a list I already agreed to.
- Parallel terminals only pay off when the contract between them is fixed first. The API contract was written into the plan before the backend and frontend terminals started.
