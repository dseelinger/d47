---
name: triage
description: Read the open GitHub issues that are ready to be implemented and report a build order — what to do next, which issues ship together as one release, the model and effort each is worth, and the few that are worth a code review. With "lanes", also splits the queue into lanes that run in parallel, each issue in its own worktree. Reports only; files, labels and starts nothing. Use when the user invokes /triage or /triage lanes, or says "what should I work on", "triage the issues", "what's next", "plan the next release", "split the issues into lanes".
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
produce a ranking that only acts on five is waste the maintainer pays for. In lanes mode, also
fetch the body of every issue that goes into a lane: the lane split depends on the files each one
names.

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

A lane is a list of issues that one session works in order, each issue in its own worktree and
merged into `main` when it lands (`/issue-worker lane <letter>` defines how). Lanes run at the same
time as each other. So the split is about one thing: two lanes must not edit the same code.

### What must share a lane

Build clusters from the **Next up** queue. Two issues go in the same cluster when:

- one needs the other, directly or through the needs chain;
- both are likely to edit the same file — the same class, panel page, capability, docs page or
  test file. `src/D47.App/AppHost.cs` counts like any other file: two issues that each wire
  something into the app share a lane;
- both change the same generated table or its `tools/gen-*.py` generator.

`CHANGELOG.md` does not count. Every fix commit adds an entry at the top, and the issue worker
resolves that conflict by keeping both entries.

To find the files, read each issue's body and take the files and types it names. Where it names a
type and not a file, one `Grep` for the type's declaration. Read no further than that. An issue
whose files you still cannot name goes in the cluster of the subsystem its title points at.

### From clusters to lanes

The default is 3 lanes; `/triage lanes <N>` sets another number. Each lane is a session building
the solution, so more lanes than that contend for the machine.

- Never split a cluster.
- More clusters than lanes: put whole clusters together until the count fits, keeping the lanes
  close in length. Count an `opus` / `high` issue as two.
- Fewer clusters than lanes: report fewer lanes. Do not split a cluster to fill one.
- Within a lane, issues keep their order from the queue.
- Letter the lanes `A`, `B`, `C`… by the queue position of each lane's first issue.

Release groups are unchanged and independent of lanes. A group can span lanes; it is ready to cut
once every issue in it has merged, whichever lane ran it.

### The lane's model and effort

One session works the whole lane and cannot change its own model, so the lane runs on the strongest
model among its issues (`opus` over `sonnet` over `haiku`), at the highest effort among the issues
on that model. Where a single issue lifts a lane of `sonnet` / `medium` work to `opus`, say so in
one sentence under the table, so the maintainer can decide to run that issue on its own instead.

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
   | A — `opus` `medium` | 1.25.0 — Missions rank the same everywhere | [794](https://github.com/dseelinger/d47/issues/794) | Rank the mission board in one place | `sonnet` | `medium` | |
   | | 1.25.0 — Missions rank the same everywhere | [841](https://github.com/dseelinger/d47/issues/841) | Rank the Situation missions like the board | `opus` | `medium` | |
   | B — `sonnet` `medium` | 1.26.0 — Carrier warnings | [834](https://github.com/dseelinger/d47/issues/834) | Warn when the carrier cannot jump twice | `sonnet` | `medium` | |

   The lane's letter, model and effort go in its first row. A group can span lanes, so the Release
   cell is filled on every row of an issue in a group. Model and Effort stay per issue: they are
   what the issue is worth, and what the Issue key uses when the issue is started on its own.
3. **Not now** — one line naming anything eligible you deliberately left out of every group, and
   why, including each waiting issue left out and the numbers it waits on. Omit the section when
   there is nothing.

No launch lines. The Stream Deck's Issue key starts a session from the grid below, with
`/issue-worker` as its opening command, so the finish line that skill defines is in its first
message. A pasted line would bypass the skill. The same key starts a lane: given a lane letter
instead of an issue number, it opens `/issue-worker lane <letter>` on the lane's model and effort.

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
  `lanes` object: each lane's model, effort, and its issue numbers in work order. A plain
  `/triage` writes neither.

  ```json
  "lanes": {
    "A": {"model": "opus", "effort": "medium", "issues": ["794", "841"]},
    "B": {"model": "sonnet", "effort": "medium", "issues": ["834", "835", "837"]}
  }
  ```
- Write the whole file each run. It is this triage's grid, not a record that accumulates.

Say it was written in one line at the end of the report, with the issue count. Nothing else.

## What this does not do

It files nothing, labels nothing, closes nothing and starts no work. Applying a `Vetted` label or
moving an issue to `under-speced` is the maintainer's, and a triage that edits the queue it just read
cannot be run twice.
