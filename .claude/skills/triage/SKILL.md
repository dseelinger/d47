---
name: triage
description: Read the open GitHub issues that are ready to be implemented and report a build order — what to do next, which issues ship together as one release, the model and effort each is worth, and the few that are worth a code review. With "lanes", also splits every eligible issue into lanes that run in parallel — each issue its own session, in its own worktree. Reports only; files, labels and starts nothing. Use when the user invokes /triage or /triage lanes, or says "what should I work on", "triage the issues", "what's next", "plan the next release", "split the issues into lanes".
---

# Triage

Run this in the desktop app. The report is tables, and a terminal does not render them.

## Turn the voice on first

Before the first `gh` call, run `/claude-voice Triage`. There is no issue number to name here — the
report covers all of them — so the phrase is the bare word. Several of these sessions run at once
and are told apart by ear.

When this session has already turned the voice on or off — `/coordinator` turns it on as
"Coordinator" before it runs triage — leave the voice and its phrase as they are.

It is a default, not a fixture: `/claude-voice off` stops it and the report carries on unchanged.

## The eligible set

One call, filtered locally:

```bash
gh issue list --state open --limit 300 --json number,title,labels,author,createdAt
```

Keep an issue only if **all** of these hold:

- It does not carry `under-speced` or `design`. Both label descriptions say so outright —
  `under-speced` needs more specification or missing data, and `design` needs Claude Design before
  it can be built. Neither is implementable as written.
- Either `dseelinger` opened it, or it carries `Vetted`. Anything else is unvetted.

Say in one line how many survived and how many each rule removed. Then stop justifying: the point
of the report is the order, not the filter.

## What each issue needs first

Many bodies name their prerequisites: a `Needs first: #537, #538.` line, or a sentence
`Needs #582 first`. Title and labels do not show them, so read them in one more call:

```bash
gh issue list --state open --limit 300 --json number,body --jq '.[] | {n: .number, needs: ([.body | scan("(?:^|[^`])Needs first:[^.\n]*"), scan("Needs #[0-9][^.\n]* first")] | map([scan("#[0-9]+") | ltrimstr("#")]) | add // [] | unique)} | select(.needs != []) | "\(.n): \(.needs | join(" "))"'
```

A needed issue is done when it is not in the open list, or when `git log main --format=%B` has the
line `Fixes #N`. An eligible issue with a needed issue that is not done is **waiting**.

## Read only what you need

The titles in this repository state the defect and usually its cause — "the row is decided from a
DrillView that has not drawn its panes yet" is a diagnosis, not a summary. Title and labels are
enough to rank most issues.

Fetch bodies only for the issues going into the first release group. Reading 36 issue bodies to
produce a ranking that only acts on five is waste the maintainer pays for. In lanes mode every
eligible issue goes into a lane, so fetch every eligible body: the lane split depends on the files
each one names. Fetch them in one call, save them to the scratchpad, and extract the file paths,
type names and needs lines with a script rather than reading each body in full. Read a body in full
only where the extract leaves its files or its needs unclear.

## Order

By default development is sequential — one checkout, one session at a time — so the order is a
queue. `/triage lanes` splits that queue into lanes afterwards (see **Lanes**); the ranking itself
is the same in both modes. Rank by, in this order:

1. **Blocking.** A waiting issue ranks after every issue it needs, never before and never first.
   Where one of those is not in the queue — ineligible, or left out — the waiting issue is left out
   too and named in **Not now** with what it waits on.
2. **Stories.** An issue labelled `stories` ranks ahead of every issue without it, and so does
   every issue it needs, followed through the needs list until it ends: if a `stories` issue needs
   #A and #A needs #B, both #A and #B move up with it, whatever their own labels. Rules 3 to 5 order issues within the prioritised set and within
   the rest, never across the two. A `stories` issue left out under rule 1 goes in **Not now**
   with the number that is not eligible, so the maintainer can see which issue to vet or specify.
3. **Truth.** A `data-accuracy` issue or a crash outranks a nicety. d47 stating something untrue
   about Elite is the worst thing it does.
4. **Adjacency.** Issues touching the same file or subsystem go consecutive, so one session's
   context pays for several fixes. This is usually the strongest signal available.
5. **Certainty.** A cheap, well-diagnosed fix before an expensive, vague one. Each fix is
   independently releasable, so certain work first is not a compromise.

`needs-repro` sinks. The label means a lead, not a diagnosis, and the session's first hour goes to
reproducing rather than fixing.

## Release groups

The current version is the newest tag: `git tag --list 'v*' --sort=-v:refname | head -1`. The
numbers above it in `CHANGELOG.md` are the per-commit guesses, reconciled when the release is
cut — the batch guessing 0.110.23 to 0.110.26 went out as `v0.111.0`. Read them for what is
unreleased, never for what the next number is.

Number a group from what it does. A corrected behaviour is a patch. A user-visible capability
added or removed is a minor, and the major is 1.0.0. A run of
patches in the changelog records what has been worked on, not a rule about numbering.

A group is what ships under one version:

- **2 to 5 issues.** Fewer wastes a release; more delays every fix in it behind the slowest.
- **They share a subject**, so they fold into one CHANGELOG entry. An issue moved up only because
  a `stories` issue needs it shares that issue's subject, and can go in its group.
- **They take the same increment.** A group holding both a fix and a new capability is numbered
  by the capability, which makes the patches in it read as features. Split it instead. A single
  issue that adds or removes a capability is worth its own minor even though it is one issue.
- Each fix commit still carries its own entry with a guessed version number. The numbers are
  reconciled when the release is actually cut.

Name each group with the version it would take and a working title in the CHANGELOG's form
(`0.110.26 — <title>`, `0.111.0 — <title>`). Both are provisional, and the maintainer knows it: do
not mark them as guesses.
The title carries the subject the group shares; if it cannot, the group is wrong and the issues
belong elsewhere.

## Model and effort

Emit the exact tokens, so the line can be pasted: `opus` / `sonnet` / `haiku`, and
`low` / `medium` / `high` / `xhigh` / `max`.

| Model | Effort | When |
| --- | --- | --- |
| `haiku` | `medium` | A generated table or a string. Almost never — the generators are the edit point, not the table. |
| `sonnet` | `medium` | The default. The issue names the cause and the fix follows from it. |
| `opus` | `medium` | The cause is named but the fix is a judgement: placement, layout, which of several sites changes. |
| `opus` | `high` | The fix crosses a project boundary, moves a Core seam, touches `TickLoop` or its subscribers, or the issue names a symptom without a cause. |

Opus runs at `medium` unless the last row applies. Do not pair `sonnet` with `high`: work that needs
more than `sonnet medium` goes to `opus medium`.

`low` only for a change whose diff you could write from the title. `xhigh` or `max` where the issue
is a design question wearing a bug's clothes — flag those as candidates for the `under-speced` label
instead of picking an effort for them.

Never go below `medium` on anything in `src/`. `TreatWarningsAsErrors` is on and there is no
`#pragma warning disable` in the tree, so a careless fix does not merely read badly, it fails to
build.

## Review, and its budget

**The default is no review.** Leave the cell blank rather than writing "none" — a column of "none"
is thirty-six pieces of noise to carry one signal.

Recommend `/code-review` only when the fix is likely to touch one of these:

- The layering rule — a spoke referencing a spoke, or anything reaching Core. `CoreDependencyTests`
  catches the assembly reference, not a bad seam.
- `TickLoop` or a tick subscriber. A subscriber that blocks stalls push-to-talk and every callout
  behind it, and nothing fails to compile when it does.
- `Capabilities.ToolCaller` or a protected tool. Trust is a property of the caller; getting it
  wrong hands the model a Commander tool.
- `UpdateChecker`'s `d47.zip` / `d47.zip.sha256` asset names. Renaming either breaks every install
  in the field silently.

Recommend `/security-review` only for a **new egress destination**, a change to `SecretStore`'s read
path or DPAPI use, a change to `EgressDisclosure`, or the installer's `PrivilegesRequired`. Not for
anything else. Most releases should have none.

**The budget: at most one review recommendation per release group.** If more than a third of the
list is flagged, the bar was set too low — raise it and report again. A triage that flags everything
is a triage the maintainer learns to skip, and then the one that mattered goes unread too.

## Lanes

Only when invoked as `/triage lanes`, or `/triage lanes <N>` to set the number of lanes. A plain
`/triage` produces no lanes and writes none.

A lane is a list of issues worked in order, one session per issue on that issue's own model and
effort, each in its own worktree and merged into `main` when it lands (`/issue-worker` defines
how). The next issue in a lane starts once the one before it has merged. Lanes run at the same time
as each other.

**Every eligible issue goes into a lane.** The only issues left out are those the lanes cannot
hold: one waiting on an issue that is not eligible, and a tracking issue with nothing to build.
The split is about keeping merge conflicts unlikely, not about keeping the queue short.

### What must share a lane

Two issues go in the same cluster when:

- one needs the other, directly or through the needs chain. A waiting issue goes later in the
  lane of the issue it needs, never in a lane of its own;
- both edit the same code — the same method, panel page, capability, docs page section or test
  file;
- both add to the same append point, where every new feature adds a line beside the last one and
  two lanes would edit the same lines. In this repository: callout registration in `AppHost.cs`,
  `HandledEvents`' sets, `CalloutSettings` and the callout toggles in `CalloutCapability`,
  `TurnLoop`, `KeywordRouter`'s dynamic commands, `HistoryBackfill`'s per-Commander maps, and
  `PersonaCatalog`;
- both change the same generated table or its `tools/gen-*.py` generator.

A file several features touch in different places does not by itself put two issues in one lane:
`AppHost.cs`, `PanelView.axaml.cs`, `MainWindow`, `D47Settings` and `EgressDisclosure` take edits
in separate methods, tabs, sections or entries, and git merges those. Two lanes may also touch the
same code when one issue sits early in its lane and the other late in its own, so the first has
merged before the second starts. Say in the report which shared files are edited from more than one
lane.

`CHANGELOG.md` does not count. Every fix commit adds an entry at the top, and the issue worker
resolves that conflict by keeping both entries.

To find the files, take the files and types each body names. Where it names a type and not a file,
one `Grep` for the type's declaration, or for where it is constructed when the issue changes what
is passed in. Read no further than that. An issue whose files you still cannot name goes in the
cluster of the subsystem its title points at.

### From clusters to lanes

The default is 3 lanes; `/triage lanes <N>` sets another number. Each lane has one session
building the solution at a time, so more lanes than that contend for the machine.

- Never split a cluster across lanes.
- **The lanes should finish at the same time.** Weigh each issue as one and an `opus` / `high`
  issue as two, and size the lanes to within one of each other across the whole eligible set. A
  lane that empties early leaves its machine share idle while the others still run.
- Assign whole clusters to lanes until every cluster is placed, keeping the lanes close in weight.
  Nothing is cut to make the lanes match: move clusters between lanes instead. Where one cluster
  alone outweighs a lane's share, report the lanes as they are and say which cluster sets the
  length.
- Within a lane, issues keep their order from the queue, and a release group's issues stay
  consecutive where the needs allow.
- Letter the lanes `A`, `B`, `C`… by the queue position of each lane's first issue.

Release groups are unchanged and independent of lanes. A group can span lanes; it is ready to cut
once every issue in it has merged, whichever lane ran it.

### When a lane pauses

When an issue's rebase onto `main` conflicts in a file other than `CHANGELOG.md`, the issue worker
aborts the rebase and stops, leaving the worktree and branch in place. That lane is paused: its
next issue does not start, because the issue before it has no `Fixes #N` commit. The other lanes
keep running. The maintainer resumes the paused issue with `/issue-worker <N>`, usually once the
other lanes have finished, so the conflict is resolved once against everything they merged. The
Issue key given that lane's letter also starts the paused issue, since it is the lane's first issue
with no `Fixes #N` commit.

A later triage run keeps a paused issue first in its lane. Find paused issues with
`git worktree list`: a worktree under `.claude/worktrees/<N>` whose issue is still open.

### The main checkout

Every lane merges into the main checkout's working tree, so it must have no uncommitted changes to
tracked files:

```bash
git status --porcelain --untracked-files=no
```

If that lists anything, say in one line above the table that the lanes cannot merge until those
files are committed, and how many there are.

## Output

Markdown, and short. Three parts:

1. One line: how many eligible, and what the filter removed.
2. **Next up** — the queue and its release groups, as one table:

   | Release | # | Issue | Model | Effort | Review |
   | --- | --- | --- | --- | --- | --- |
   | 0.110.10 — Tables answer for themselves | [105](https://github.com/dseelinger/d47/issues/105) | Join the experimental effect on its symbol | `sonnet` | `medium` | |
   | | [104](https://github.com/dseelinger/d47/issues/104) | No way to ask which engineer works in a system | `sonnet` | `medium` | |

   A group's issues are consecutive rows. The version and title go in the first of them; the
   Release cell is blank on the rest, and blank throughout for an issue in no group. There is no
   separate release section and no sentence explaining a group — the title says what they share.

   **Every issue number is a link** — `[105](https://github.com/dseelinger/d47/issues/105)` in the
   table's `#` column, and `[#105](https://github.com/dseelinger/d47/issues/105)` wherever a number
   appears in the prose, including **Not now**. The report is read in the desktop app, where a bare
   number is a number to go and look up.

   Shorten titles to the claim. The full title is one click away.

   In lanes mode the table gains a **Lane** column first, and rows are ordered by lane, then by
   their order within it:

   | Lane | Release | # | Issue | Model | Effort | Review |
   | --- | --- | --- | --- | --- | --- | --- |
   | A | 1.25.0 — Missions rank the same everywhere | [794](https://github.com/dseelinger/d47/issues/794) | Rank the mission board in one place | `sonnet` | `medium` | |
   | | 1.25.0 — Missions rank the same everywhere | [841](https://github.com/dseelinger/d47/issues/841) | Rank the Situation missions like the board | `opus` | `medium` | |
   | B | 1.26.0 — Carrier warnings | [834](https://github.com/dseelinger/d47/issues/834) | Warn when the carrier cannot jump twice | `sonnet` | `medium` | |

   The lane's letter goes in its first row. A group can span lanes, so the Release cell is filled
   on every row of an issue in a group.
3. **Not now** — one line naming anything eligible you deliberately left out of every group, and
   why, including each waiting issue left out and the numbers it waits on. In lanes mode this is
   only what the lanes cannot hold: an issue waiting on an ineligible one, and a tracking issue.
   Omit the section when there is nothing.

No launch lines. The Stream Deck's Issue key starts a session from the grid below, with
`/issue-worker` as its opening command, so the finish line that skill defines is in its first
message. A pasted line would bypass the skill. Given a lane letter instead of an issue number, the
same key starts that lane's next issue — the first one with no `Fixes #N` commit on `main` — on
that issue's model and effort.

No preamble, no summary of what triage is, no restating the rules above. The maintainer ran this to
find out what to do next.

## Save the grid

After the report, write the same rows to `.claude/triage-state.json` with the Write tool. The
Stream Deck's Issue key reads it: it asks for a number and launches the session on the model and
effort chosen here, so a row missing from this file is a session that starts on the defaults.

```json
{
  "generated": "2026-09-12T15:04:00Z",
  "eligible": 36,
  "issues": {
    "105": {"title": "Join the experimental effect on its symbol",
            "model": "opus", "effort": "high",
            "release": "0.110.10 - Tables answer for themselves",
            "review": "/code-review"}
  }
}
```

- `generated` is UTC, and the launcher shows its age. Get it from `date`, never from memory.
- Every issue in the **Next up** table gets a row, whether or not it landed in a release group.
- `model` and `effort` carry the exact tokens from the table. The launcher accepts
  `opus`/`sonnet`/`haiku` and `low`/`medium`/`high`/`xhigh`/`max`, and falls back to
  `sonnet`/`medium` for anything else.
- `release` and `review` are optional; leave them out where the table's cell is blank.
- In lanes mode, each laned issue also carries `"lane": "A"`, and the file gains a top-level
  `lanes` object: each lane's issue numbers in work order. A plain `/triage` writes neither.

  ```json
  "lanes": {
    "A": ["794", "841"],
    "B": ["834", "835", "837"]
  }
  ```
- Write the whole file each run. It is this triage's grid, not a record that accumulates.

Say it was written in one line at the end of the report, with the issue count. Nothing else.

## What this does not do

It files nothing, labels nothing, closes nothing and starts no work. Applying a `Vetted` label or
moving an issue to `under-speced` is the maintainer's, and a triage that edits the queue it just read
cannot be run twice.
