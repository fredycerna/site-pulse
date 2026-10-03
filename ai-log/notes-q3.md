# AI log notes — Terminal Q3 (documentation)

Running notes, written as things happen. Each entry: time, what the agent proposed, what was decided, why. Also the agent's own mistakes when it catches them.

## 2026-10-03

- 13:27 — Session start (Claude Opus, execution terminal Q3: documentation). Task: write PLAN.md, CLAUDE.md, AGENT_WORKFLOW.md, CHECKLIST.md and the planning record. Found `~/projects/site-pulse/` already created by Terminal Q1 (scaffolding in progress, no `.git` yet), so Q3 ran `git init -b main` in place instead of `mkdir`. Q1's files stay untracked; Q3 only stages its own paths.
- 13:26 — The previous Q3 session was stopped by a tool content filter. It had been told to read and copy its own session files into the log; Fredy dropped that instruction (the readable record will come from `/export`, which he runs) and resumed Q3 with an adjusted prompt.
- Resumed session — verified task 1: `git init -b main` done (no commits yet), `ai-log/notes-q3.md` exists. Continuing with PLAN.md.
- PLAN.md draft written from the planning session's decisions and the starter's `docs/TICKET.md`. Flagged to Fredy: the clumsy "walking skeleton" row in the time budget, and naming seed accounts (Metro Collision, Quiet Harbor Spa). Fredy accepted the draft as-is ("así queda"), no corrections. Committed as the first commit of the repo.
- Tasks 3–5 done without corrections from Fredy. Own catch while writing the planning record: three sentences were inferred, not backed by the day plan (that the agent asked about a React version, a reason for dropping the brand header, and the direction of the integration-test change of mind). Rewritten to state only what the plan records before committing.
- After a cross-review with the planning session, Fredy sent three corrections to PLAN.md, already committed:
  1. §2, the spike: 882 is the total for **all accounts** on 2026-06-03. Account 6 alone had 805 events that day, against ~13 on one of its normal weekdays. The 881-event week stands. (The planning record repeated the same error.)
  2. §2 and §5: the back-test and the flagged locations were measured in an exploratory run that bucketed weeks in UTC. The app buckets weeks in each account's local time, so exact counts may differ slightly.
  3. §10: replace the time table with Planning 1 h, walking skeleton + docs 1 h, implementation 3.5 h, polish 0.5 h; the cut rule becomes "if the evening goes past 3.5 h".
  Kept as they are, at Fredy's call: the seed account names and §11.
  Open question: PLAN.md is frozen after its first commit, and Q1 has committed on top of it. Asked Fredy how to apply the corrections.
- Fredy's decision: **errata, not an edit.** PLAN.md stays frozen. The planning record gets corrected, and Q1 adds an "Errata to PLAN.md" section to the README with the text Q3 leaves in its delivery. The time correction above (item 3) was replaced: the real figures are planning ~1 h (12:20–13:20), walking skeleton + docs ~15 min with two terminals in parallel, evening still 4 h, Sunday 0.5 h. The README will show the app's real numbers next to the back-test figures.
- The 882 error was found by a cross-review from the planning session, after the PLAN.md commit. The agent that wrote PLAN.md copied it from the day plan without checking it against the data.
- Precisions for the record, from Fredy: planning model = Claude Fable 5.1, execution = Claude Opus 5.5 (confirms the inferred value). The integration tests went like this: planning proposed them with pinned numbers; Fredy wanted them out (too much back-and-forth in his experience); planning pushed back (the aggregation lives in SQL, so unit tests never touch it); Fredy accepted, with a 30-min cap and the same seeded database.
