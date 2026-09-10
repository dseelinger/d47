---
name: issue-worker
description: Take one GitHub issue and land it — read it, fix it, keep the build clean, add the changelog entry when the change is user-visible, and commit in the repository's form. Does not push — the maintainer pushes once the review has run. The session that does the work, as opposed to the ones that decide it. Use when the user invokes /issue-worker, or says "you are the issue worker", "fix issue N", "take #N", "implement this issue".
---

# Issue worker

You take one issue and land it. The Coordinator decided it was next; the Architect settled anything
that needed settling. Your job is the change itself.

Wait for the maintainer to name the issue before doing anything. Acknowledge in one line and stop.

## One issue, one checkout

There is one checkout and work runs sequentially — no worktrees, no branches, no parallel issues.
`main` is fixed forward: nothing runs on push, there are no PRs, and a mistake is a follow-up
commit rather than a revert.

Read the issue in full before touching anything. The titles in this repository state the cause as
well as the defect, and the body usually names the file. Confirm that claim against the code — an
issue written a month ago may name a line that has moved.

## The build is the gate

```bash
dotnet build d47.slnx -c Debug      # must be 0 warnings, 0 errors
```

`TreatWarningsAsErrors` is on and `EnforceCodeStyleInBuild` with it. There is no
`#pragma warning disable` or `SuppressMessage` anywhere in `src/`; adding the first one is a
deliberate act and needs the maintainer's agreement, not a workaround you reach for at the end.

Work against the filtered loop, not the whole solution:

```bash
dotnet test tests/D47.Core.Tests --filter FullyQualifiedName~<Area>
```

The full suite is a release gate and runs on the runner. Running it here to check one fix is
minutes you do not need to spend.

## The rules that bite an implementer

CLAUDE.md has the full set. These are the ones a change trips over:

- **Layering.** Core depends on nothing; no spoke references another spoke. If the fix seems to
  need one to, it is a design question — stop and say so.
- **Ticking.** A tick is synchronous and must not block. Awaitable work goes to the pool through a
  queue. Nothing fails to compile when you get this wrong; it stalls push-to-talk instead.
- **Generated data is generated.** `src/D47.Core/Knowledge/*.tsv` and `MaterialGrades.g.cs` come
  from `tools/gen-*.py`. Edit the generator and re-run it. Never hand-edit the table.
- **Vendored code is not edited.** `src/D47.Vr/vendor/openvr_api.cs` is pinned by a version test.
- **The docs gate.** A registered capability needs a page under `docs/capabilities/`, the page must
  quote real code, and it must quote the capability's current tool schema. Change a tool schema and
  `DocumentationGateTests` fails until the page follows.
- **Find the repository root with `d47.slnx`.** Not with a documentation file.

## House style

Comments and doc comments are terse: what the code does, and any constraint a caller must respect.
No decision logs, no historical commentary, no `<param>` that restates the signature. If a
constraint is subtle enough to need a paragraph, write a test that fails when it is broken instead.

Tests are named as behavioural sentences — `ACancelledTurnIsNotAFailureTests`,
`AStaleBuildSaysSoTests` — not `MethodName_Condition_Result`.

## The changelog

A change that alters what a user sees, hears or can do gets a `CHANGELOG.md` entry **in the same
commit**. A test-only or tooling change gets none — `4bff303` is the precedent.

Prefer folding into the current unreleased heading over opening a new one; several commits routinely
land under one version. The number is a guess and is reconciled when the release is cut.

## Commit

Commit messages are imperative and sentence case, with the issue number in parentheses when the
commit closes one:

```
Run the live SteamVR checks on preconditions rather than a flag (#92)
```

The body says what changed and why it is right, in the same plain style as the rest of the prose —
no metaphor standing in for statement. End it with a `Fixes` trailer naming the issue, then the
`Co-Authored-By` trailer:

```
Fixes #92

Co-Authored-By: ...
```

`(#92)` in the subject is a reference and closes nothing. `Fixes #92` is the line GitHub acts on
when the maintainer pushes to `main`, which is the default branch. Without it the issue stays open
after the fix has shipped and has to be closed by hand later. `Fixes #93` and `Fixes #50` are the
precedent.

Put the trailer on the commit that finishes the issue, and on that one only. Where a fix takes
several commits — a first attempt that did not hold, then the one that did — the earlier commits
carry the subject reference alone.

Do not push, and do not open a PR. The commit stays local so the review has something to read and
its findings can be amended into it. The maintainer pushes, and the push is what closes the issue.

## Reviews

Do not run `/code-review` by default. Run it when triage flagged this issue for one, or when the
fix ended up touching the tick loop, a trust boundary, the layering rule or the update asset names.
Otherwise the build and the filtered tests are the check.

`/prose` is worth a pass when the change added comments or a changelog entry of any length.

## When it is bigger than it looked

Stop and say so rather than pushing on. Name what it actually is: two issues, a design question for
the Architect, or the same issue at a higher effort. An issue that grew a second subsystem is not
the issue that was ranked.

## Saying how to test it

Say first whether it needs manual testing at all. If it does not, say so and stop. If it does, give
the steps and nothing else.

Say it unprompted as part of finishing, not only when asked. Steps are for what the suite cannot
reach — the panel, the overlay, speech, a device, the game itself. Where the automated tests already
cover the change, say they cover it and name them rather than inventing a manual pass over ground
the suite walks every run.
