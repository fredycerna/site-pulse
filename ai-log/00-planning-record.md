# 00 — Planning session record

**What this is**: a written record of the planning session (Saturday 2026-10-03, about 1 h, 12:20 to 13:20), reconstructed from the day plan that session produced. It is not the raw transcript. The raw `/export` of the planning session is added separately on Sunday. Order follows what actually happened. Unpolished on purpose.

**Who**: me (product and scope decisions) and one planning session in Claude Code (Claude Fable 5.1). The planning session did not write feature code.

## 1. Stack, before anything else

- The original prompt says .NET 8+ and Angular. Earlier in the week, the recruiter told me in chat that the stack was free, so my first intention was .NET + React.
- At the start of the session I re-read the email: it says in writing that "submissions in another stack can't be evaluated". **I changed my mind: .NET 8 + Angular.** The chat message saying I would use React was never sent.
- No second version in React: two frontends is breadth, and the exercise rewards a small, correct slice.
- Database: SQL Server 2022 in Docker, since it is what the team uses. The agent proposed a 20-minute cap to get it running on Apple Silicon (amd64 image under emulation), with PostgreSQL as the fallback. Accepted.

## 2. Measuring the seed

The agent loaded `schema.sql` and `seed.sql` into a scratch database and profiled it before proposing any design. What came out (details in `PLAN.md` §2):

- 12,626 events, 20 accounts, 2026-02-01 to 2026-07-27 UTC: 25 complete weeks plus a partial Monday. The real clock (October) is useless, so "now" has to be pinned to the snapshot.
- A spike on 2026-06-03: 882 events across all accounts that day. Account 6 alone logged 805, against about 13 on one of its normal weekdays; its week comes to 881 against ~70. (`PLAN.md` §2 says "882 for account 6", which is wrong; see the errata in the README.)
- Per-location volumes are tiny: 2 to 15 events per week.
- 12 exact duplicate pairs with consecutive ids.
- 398 NULL `outcome`, 313 calls with NULL `duration_seconds`, some missed calls with a duration.
- Account 20 has no events; account 18 is on `UTC`; Phoenix has no DST.
- A few location-weeks with zero events.
- Nothing else dirty: no orphans, no events before `created_at`, no name variants.

These findings drove the design more than anything else: a median instead of an average (the spike), a range instead of percentages (small volumes), duplicates handled at read time, and a pinned clock.

## 3. What "normal" means

- The agent asked what question the screen should answer. My answer: "was last week normal for us, and which location stepped outside its own normal?"
- The agent asked what to compare against. Decided: the last complete week, Monday to Sunday in the account's timezone, against the **same location's** 8 previous complete weeks. Not against other locations.
- The agent proposed median ± 2σ with σ = max(1.4826·MAD, √median), and back-tested it against the seed together with the common alternatives: how many of the 1,173 location-weeks would each rule flag?
  - median ± 2σ (proposed): **6 %**
  - interquartile range p25–p75: 55 %
  - average of the last 4 weeks ± 30 %: 51 %
- Caveat: this back-test, and the list of locations it flags for the last week, came from an exploratory run that bucketed weeks in UTC. The app buckets weeks in each account's local time, so the exact counts may differ slightly. The README puts the app's real numbers next to them.
- Accepted. A screen that flags half the locations every Monday is noise.
- Metric for the slice: weekly volume with a type filter. Quality (missed-call rate, no-shows) is deferred and named.
- Ordering: out-of-range locations first, by size of deviation.

## 4. My design, and where the agent did and did not take it

I drew my own version of the screen (`design/sketch-1-dashboard.png`, `design/sketch-2-edge-states.png`; the sketches are in Spanish). The agent merged it with its own proposal into `design/mockup-dashboard.html`, which is the spec for the view.

**Where my design corrected or improved the agent's proposal** (taken):
- **One visual view.** Verdict, cards and locations on a single screen; the detail opens on request inside the same page. No second page for the detail.
- **One card per event type**, each with its own range bar. A deviation in one type is visible even when the total looks normal. In the merge, the selected card became the type filter, so the separate type tabs from my sketch went away, and an "All activity" card was added.
- **Notes about the data** shown to the user: duplicates excluded, the exceptional week that is in the history but does not move the range, the current week not compared yet.
- **Colors without judgment.** Out of range is shown with color and a label: blue above, amber below, never red/green. The screen says "different from your usual", not good or bad: more calls is not necessarily good news.
- The edge states from my second sketch: account with no activity, insufficient history ("We need at least 4 full weeks… N weeks to go").
- The verdict sentence with numbers, and the line that says what it is compared against.

**Where the agent did not take my proposal, and why:**
- **My definition of normal: "the range you saw in 6 of your 8 weeks".** The agent back-tested it against the seed: it flags **36 %** of location-weeks, against 6 % for the median ± 2σ rule. Too noisy to act on. I agreed to drop it.
- **A mini 9-week series in every location row.** Moved to the detail on click, to keep the main view light.
- **A "current week in progress" state.** Dropped: the current week is not offered in the week picker at all, so the state never appears. It stays as a line in the data notes.
- **The product brand header.** Dropped; it adds nothing to the answer.

## 5. Architecture and scope questions

- **Layers**: thin endpoint → `BaselineService` → `IActivityQueries` (the only class with SQL) + pure functions. No generic repository. Accepted.
- **Data access**: EF Core for migrations and simple reads, raw parameterized SQL (`SqlQuery<T>`) for the aggregation. Dapper was considered and rejected: it would need a second migration tool.
- **Week boundaries in C#**: T-SQL does not understand IANA zones; computing 10 UTC boundaries in C# keeps the predicate a plain indexed range. The integrity check goes the other way: an independent T-SQL computation with `AT TIME ZONE`.
- **Cache**: no. The query touches ~2,600 rows per account through an index. Measure, log the time, and describe in the README what to do at scale.
- **Duplicates**: kept in the table, removed in a named view (`activity_events_deduped`).
- **Integration tests**: the planning session proposed them, pinned to fixed numbers. I said I would leave them out: in my experience they cost a lot of back-and-forth. The session pushed back: the aggregation lives in SQL, and a unit test never touches it. I accepted, with conditions: **30-minute cap** to get them green, no Testcontainers, no `WebApplicationFactory`, no separate database. The API is read-only, so the tests read the same seeded database. If they are not green in 30 minutes, the manual verification goes in the README.
- **State that survives a reload**: URL query params, no state library.
- **Repo name**: `site-pulse`.
- **Questions to the recruiter**: none sent. It is the weekend and the prompt accepts documenting assumptions, so they went into `PLAN.md` §3.

## 6. How the work was split

The planning session wrote one closed task list per execution terminal (docs, backend, frontend), with file ownership, decisions already made, a cut line and the commit messages. See `AGENT_WORKFLOW.md`. Product decisions stayed with me; the plan went to `PLAN.md` as the first commit.

## 7. Process notes

The docs terminal's first session (13:20) was stopped by a content filter in the tool. It had been instructed to read and copy its own session files into this log. I dropped that instruction (the readable record comes from `/export`, which I run myself) and restarted the terminal with an adjusted prompt.

After `PLAN.md` was committed, a cross-review by the planning session found that the spike figure in §2 was wrong (882 is the all-accounts total for that day, not account 6's). `PLAN.md` stays frozen; the correction, the UTC caveat on the back-test, and the real planning time go in the README as "Errata to PLAN.md".
