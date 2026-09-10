---
name: triage
description: Read the open GitHub issues that are ready to be implemented and report a build order — what to do next, which issues ship together as one release, the model and effort each is worth, and the few that are worth a code review. Reports only; files, labels and starts nothing. Use when the user invokes /triage, or says "what should I work on", "triage the issues", "what's next", "plan the next release".
---

# Triage

Run this in the desktop app. The report is tables, and a terminal does not render them.

## Turn the voice on first

Before the first `gh` call, run `/neural-voice Triage`. There is no issue number to name here — the
report covers all of them — so the phrase is the bare word. Several of these sessions run at once
and are told apart by ear.

It is a default, not a fixture: `/neural-voice off` stops it and the report carries on unchanged.

## The eligible set

One call, filtered locally:

```bash
gh issue list --state open --limit 300 --json number,title,labels,author,createdAt
```

Keep an issue only if **all** of these hold:

- It does not carry `tabled`. That label is a moratorium on new-capability work, not a priority.
- It does not carry `phase` or `design`. Both label descriptions say so outright — a `phase` is a
  product description for work not yet built, and a `design` is a promise to discuss that spawns
  build issues when it settles. Neither is implementable as written.
- Either `dseelinger` opened it, or it carries `ready`. Anything else is unvetted.

Say in one line how many survived and how many each rule removed. Then stop justifying: the point
of the report is the order, not the filter.

## Read only what you need

The titles in this repository state the defect and usually its cause — "the row is decided from a
DrillView that has not drawn its panes yet" is a diagnosis, not a summary. Title and labels are
enough to rank most issues.

Fetch bodies only for the issues going into the first release group. Reading 36 issue bodies to
produce a ranking that only acts on five is waste the maintainer pays for.

## Order

Development here is sequential — one checkout, one session at a time — so the order is a queue, not
lanes. Rank by, in this order:

1. **Blocking.** Anything a later issue needs. Rare; say why when you claim it.
2. **Truth.** A `data-accuracy` issue or a crash outranks a nicety. d47 stating something untrue
   about Elite is the worst thing it does.
3. **Adjacency.** Issues touching the same file or subsystem go consecutive, so one session's
   context pays for several fixes. This is usually the strongest signal available.
4. **Certainty.** A cheap, well-diagnosed fix before an expensive, vague one. Each fix is
   independently releasable, so certain work first is not a compromise.

`needs-repro` sinks. The label means a lead, not a diagnosis, and the session's first hour goes to
reproducing rather than fixing.

## Release groups

The version line is `0.110.x`; `git describe --tags --abbrev=0 --match 'v*'` gives the last one.
While the moratorium holds, groups are patch releases.

A group is what ships under one version:

- **2 to 5 issues.** Fewer wastes a release; more delays every fix in it behind the slowest.
- **They share a subject**, so they fold into one CHANGELOG entry. Precedent: several headset
  entries were folded into one 0.110.9 entry rather than shipped as separate versions.
- Each fix commit still carries its own entry with a guessed version number. The numbers are
  reconciled when the release is actually cut.

Name each group with the version it would take and a working title in the CHANGELOG's form
(`0.110.10 — <title>`). The title is a guess and should be marked as one. The title carries the
subject the group shares; if it cannot, the group is wrong and the issues belong elsewhere.

## Model and effort

Emit the exact tokens, so the line can be pasted: `opus` / `sonnet` / `haiku`, and
`low` / `medium` / `high` / `xhigh` / `max`.

| | When |
| --- | --- |
| `haiku` | A generated table or a string. Almost never — the generators are the edit point, not the table. |
| `sonnet` | The default. The issue names the cause and the fix follows from it. |
| `opus` | The fix crosses a project boundary, moves a Core seam, touches `TickLoop` or its subscribers, or the issue names a symptom without a cause. |

Effort: `low` only for a change whose diff you could write from the title. `medium` is the default.
`high` where the cause is named but the fix is a judgement. `xhigh` or `max` where the issue is a
design question wearing a bug's clothes — flag those as candidates for the `design` label instead of
picking an effort for them.

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

## Output

Markdown, and short. Three parts:

1. One line: how many eligible, and what the filter removed.
2. **Next up** — the queue and its release groups, as one table:

   | Release | # | Issue | Model | Effort | Review |
   | --- | --- | --- | --- | --- | --- |
   | 0.110.10 — Tables answer for themselves *(guess)* | 105 | Join the experimental effect on its symbol | `sonnet` | `medium` | |
   | | 104 | No way to ask which engineer works in a system | `sonnet` | `medium` | |

   A group's issues are consecutive rows. The version and title go in the first of them; the
   Release cell is blank on the rest, and blank throughout for an issue in no group. There is no
   separate release section and no sentence explaining a group — the title says what they share.

   Shorten titles to the claim. The full title is one click away.
3. **Not now** — one line naming anything eligible you deliberately left out of every group, and
   why. Omit the section when there is nothing.

For the first group only, end with a copyable launch line per issue:

```
claude -n "#105" --model sonnet --effort medium "Fix #105."
```

No preamble, no summary of what triage is, no restating the rules above. The maintainer ran this to
find out what to do next.

## What this does not do

It files nothing, labels nothing, closes nothing and starts no work. Applying a `ready` label or
moving an issue to `tabled` is the maintainer's, and a triage that edits the queue it just read
cannot be run twice.
