---
name: issue-worker
description: Take one GitHub issue and land it — read it, fix it, keep the build clean, add the changelog entry when the change is user-visible, and commit in the repository's form. With "lane <letter>", works a lane from /triage lanes in order, each issue in its own worktree, fast-forwarding main as each lands. Does not push — the maintainer pushes once the review has run. The session that does the work, as opposed to the ones that decide it. Use when the user invokes /issue-worker or /issue-worker lane <letter>, or says "you are the issue worker", "fix issue N", "take #N", "implement this issue", "work lane B".
---

# Issue worker

You take one issue and land it. The Coordinator decided it was next; the Architect settled anything
that needed settling. Your job is the change itself.

`/issue-worker 62` has named the issue. The number is the work, not a label for the session, so
start on #62 in that same turn — do not acknowledge it and wait for a second instruction saying the
same thing.

`/issue-worker lane B` names a lane from the last `/triage lanes`. Start on its first issue in that
same turn, and follow **Lanes** below.

Only a bare `/issue-worker`, carrying no issue, waits: acknowledge in one line, stop, and start when
the maintainer names one.

## Turn the voice on first

Once the issue is named, the first step of the working turn is `/claude-voice Issue worker <number>`,
the number spoken as words — `/claude-voice Issue worker sixty six` for #66. Several of these
sessions run at once and are told apart by ear, and the number is what tells them apart. A lane
session uses the lane instead, because it spans several issues: `/claude-voice Lane B`.

It is a default, not a fixture: `/claude-voice off` stops it and the work carries on unchanged.

## One issue, one checkout

By default there is one checkout and work runs sequentially — no worktrees, no branches, no
parallel issues. `main` is fixed forward: nothing runs on push, there are no PRs, and a mistake is
a follow-up commit rather than a revert.

Two cases put the work in a worktree instead, both covered by **Lanes** below:

- the session was started as `/issue-worker lane <letter>`;
- `git worktree list` shows a worktree under `.claude/worktrees/`. Lanes are running and merging
  into `main`, so an issue started on its own goes through a worktree too, or its uncommitted
  edits sit in the tree the lanes fast-forward.

Read the issue in full before touching anything. The titles in this repository state the cause as
well as the defect, and the body usually names the file. Confirm that claim against the code — an
issue written a month ago may name a line that has moved.

## Check what it needs first

Before touching the tree, list the issues this one names as prerequisites. Two forms count: a
`Needs first: #537, #538.` line, and a sentence `Needs #582 first` (including
`Needs #606 (…) and #607 (…) to land first`). Prose that names no number, such as "this lands
first", does not.

```bash
gh issue view <number> --json body --jq '[.body | scan("(?:^|[^`])Needs first:[^.\n]*"), scan("Needs #[0-9][^.\n]* first")] | map([scan("#[0-9]+") | ltrimstr("#")]) | add // [] | unique | join(" ")'
```

A needed issue is done when it is closed on GitHub, or when a commit on local `main` carries the
line `Fixes #N` — the Night Shift commits without pushing, so its work is not closed until morning:

```bash
for n in <needed numbers>; do
  s=$(gh issue view "$n" --json state -q .state)
  if [ "$s" != CLOSED ] && ! git log main --format=%B | grep -qx "Fixes #$n"; then echo "#$n is not done"; fi
done
```

If any is not done, stop with the tree unchanged, name each one, and ask whether to take it
instead. A direct instruction from the maintainer to go ahead anyway overrides the check.

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

Before committing, also run the gate tests. They check the whole tree against a list or a rule — a
journal event dispatched on must be in `HandledEvents.ActedOn`, a capability must have a docs page
— so a change can break one without matching the area filter (it takes a few seconds):

```bash
dotnet test tests/D47.Core.Tests --filter FullyQualifiedName~Gate
```

One exception: when the diff touches anything under `src/D47.App/`, run the whole of
`D47.App.Tests` before committing (about 90 seconds):

```bash
dotnet test tests/D47.App.Tests -c Release
```

Several of its tests read `AppHost.cs` as text and count call sites, so moving code out of the app
breaks them even though the area filter passes. Nothing else runs them before `release.ps1` does.

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
commit**. A test-only or tooling change gets none.

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
after the fix has shipped and has to be closed by hand later.

Put the trailer on the commit that finishes the issue, and on that one only. Where a fix takes
several commits — a first attempt that did not hold, then the one that did — the earlier commits
carry the subject reference alone.

Do not push, and do not open a PR. The commit stays local so the review has something to read and
its findings can be amended into it. The maintainer pushes, and the push is what closes the issue.

## What triage chose

`.claude/triage-state.json` holds the last triage's grid, keyed by issue number. Read the entry for
this issue when the file is there; it is a snapshot of a queue that moves, so treat a missing entry
as no information rather than a verdict.

The Stream Deck's Issue key has already applied `model` and `effort` at launch. **A session cannot
change its own model or effort** — the desktop app refuses both for the session itself. So where
the entry names a model other than the one you are running as, say so in one line and carry on;
switching is the maintainer's, from the model picker. A lane session compares against the lane's
model and effort in `lanes`, once at the start, not against each issue's.

`review` is the other reason to read it: it is how triage's recommendation reaches you.

## Reviews

Do not run `/code-review` by default. Run it when the triage state flags this issue for one, or
when the fix ended up touching the tick loop, a trust boundary, the layering rule or the update
asset names.
Otherwise the build and the filtered tests are the check.

Act on a review's findings only where you would block the commit for them, and for each one state
the file, the line, why it is wrong, and how to show it fails. Drop the rest.

`/prose` is worth a pass when the change added comments or a changelog entry of any length.

## When it is bigger than it looked

Stop and say so rather than pushing on when the fix turns out to need something the issue did not
name from this list:

- new journal state
- a new column in a generated table
- a new seam in Core
- a second subsystem

This list is shared with `/new-issue`, which splits issues on the same items. Name what the issue
actually is: two issues, a design question for the Architect, or the same issue at a higher effort.
An issue that grew one of these is not the issue that was ranked.

## Lanes

`/issue-worker lane B` works the issues in `lanes.B.issues` of `.claude/triage-state.json`, in that
order, one at a time. If the file has no lane `B`, stop and say `/triage lanes` has to run first.
Other lanes run in other sessions at the same time; triage put issues that edit the same code in
the same lane, so lanes do not wait on each other.

The main checkout is the first entry of `git worktree list`. Before the first issue, check it has
no uncommitted changes to tracked files (`git -C <main checkout> status --porcelain
--untracked-files=no`). If it has, stop and list them: every merge fast-forwards that working tree.

### Each issue

1. Skip it if `main` already has a commit with the line `Fixes #N`, and say so.
2. Run **Check what it needs first**. A needed issue earlier in this lane has merged by now; one
   that is not done stops the lane.
3. Create the worktree from local `main`, then move the session into it:

   ```bash
   git -C <main checkout> worktree add .claude/worktrees/<N> -b issue/<N> main
   ```

   Then call `EnterWorktree` with that path. Do not create it with `EnterWorktree`'s `name`: that
   branches from `origin/main` and misses every commit not yet pushed, including the ones this
   lane just merged.
4. Do the work as the rest of this skill says — build, filtered tests, gate tests, changelog,
   commit, and a review where **Reviews** calls for one — all inside the worktree. The first build
   in a fresh worktree restores and builds everything, so it takes longer than usual.
5. Merge it, as below.
6. Call `ExitWorktree` with `keep` (it does not remove a worktree entered by path), then remove the
   worktree and its branch:

   ```bash
   git -C <main checkout> worktree remove .claude/worktrees/<N>
   git -C <main checkout> branch -d issue/<N>
   ```

   If the removal fails on a locked file, leave it and name it in the report.
7. If it needs manual testing, write its steps down for the end of the lane. Then take the next
   issue.

When an issue does not land — it is bigger than it looked, the build or tests cannot be made
green, or a conflict cannot be resolved as described below — stop the lane and ask, as for a single
issue. Leave its worktree and branch in place and say where they are. The rest of the lane stays
unstarted.

### Merging into main

`main` takes fast-forwards only, so its history stays one line with no merge commits. In the
worktree:

```bash
git rebase main
```

Then run the checks again on the rebased tree — the build, the area filter, the `Gate` filter, and
`D47.App.Tests` when the diff touches `src/D47.App/` — because other lanes' work is now under the
change. Then:

```bash
git -C <main checkout> merge --ff-only issue/<N>
```

If another lane merged in between, `--ff-only` refuses; if one is merging at that moment, git
reports an `index.lock`. Either way, rebase again, run the checks again, and retry.

Rebase conflicts:

- `CHANGELOG.md` conflicts are expected, since every lane adds entries at the top. Keep both,
  this one under the unreleased heading `main` has, and leave `main`'s entries as they are.
- A conflict in any other file means triage judged two issues independent and they are not.
  Resolve it when both sides' changes can be kept as written, run the checks, and name the file in
  the report. Otherwise `git rebase --abort` and stop the lane.

Never create a merge commit, never force anything, and never push. The rebase changes the commit's
hash, which matters to nothing: it is still local.

### Testing a lane

Captures are taken in the worktree before the merge, as **Capture it yourself** says. Manual testing
waits for the end of the lane: run `/test-drive` once, from the main checkout, after the last issue
merges. That build holds everything merged so far, from every lane, so say so in the line above
the steps. Then give the steps grouped under each issue's number.

## Saying how to test it

Say first whether it needs manual testing at all. If it does not, say so and stop. If it does, give
the steps as a numbered list and nothing else — one action per step, in the order the maintainer
performs them, each step saying what to do and what to look for. Not a paragraph, and not a bulleted
list: the numbers are what lets them say which step failed.

Say it unprompted as part of finishing, not only when asked. Steps are for what the suite cannot
reach — the panel, the overlay, speech, a device, the game itself. Where the automated tests already
cover the change, say they cover it and name them rather than inventing a manual pass over ground
the suite walks every run.

### When it needs manual testing, launch it

Run `/test-drive` before writing the steps, so the build under test is already up when the
maintainer reads them. It builds Debug, mirrors it to the test-drive folder, syncs data and
launches `d47.exe` from there. If the build fails, the test drive stops; fix that before reporting —
steps written against a binary that does not contain the fix test nothing.

The steps start from the running test drive. Do not include "build the app" or "launch d47"; the
app is open. Say in one line above the list that the test drive is running with this commit.

### Capture it yourself where a picture can check it

Captures are shown to the maintainer, and only the desktop app displays them. Before taking the
first capture, check where the session is running: the system prompt says so when it is the Claude
desktop app. If it does not, stop. Tell the maintainer the change needs screenshots, ask them to run
`/desktop`, and ask them to say "continue" once the session is open in the desktop app. Do nothing
more until they do.

Where a result can be read off a picture — hex values in the Control Kit, a colour, a layout, a
label — render the screen headlessly and check the image yourself, rather than handing over a step
for the maintainer to judge by eye. Write or reuse a test in `D47.App.Tests` that renders the
changed view and saves it with `CaptureRenderedFrame()` to `TestSurface.CaptureDirectory`, run it
with a filter, and read the PNG. Capture before and after the change where the change is a look.

A capture shows what the app would draw. Anything the app loads at startup, the test sets up the
same way — hull art is the one that has been missed: `MainWindow` points `ShipArt.Folder` and
`ShipArt.Shipped` at the data and build `ships` folders, and a test that leaves them unset draws
every Fleet card without its picture. The build's stills are copied beside the test binary under
`ships\`. Before sending a capture, compare it with what the running app shows on that screen; if
something the app draws is missing, fix the test's setup and capture again. A capture missing
something the app draws looks like a regression the change caused.

Send every capture you checked to the maintainer with `SendUserFile` (`display: "render"`), with a
caption naming the screen and what it shows, so they see what you judged. A capture you did not
check is not sent.

Ask the maintainer for a screenshot only for what headless rendering does not show: display
scaling, the native window border, and the headset overlay. Name the window and what the shot must
show; reading one needs the desktop app, so in a terminal session stop and ask for `/desktop` as
above. Steps that need a device, speech or the game stay with the maintainer.

### Every step is exact

Read the code the change touched before writing the steps, and take the names from it — the
maintainer should never have to guess what a step means. Each step names:

- **Where**: the window, panel section, tab or overlay, by the label it shows on screen.
- **What to do**: the control by its visible label, the key or binding, or the exact words to say in
  quotes — `Say "set volume to forty percent"`, not "ask it to change the volume".
- **What to expect**: the visible text, the spoken reply or the state change that means it passed.
  Quote it where the code fixes it. Where the change is a fix, also say what the defect looked like,
  so a failure is recognisable.

Where a step needs game state — docked, in supercruise, a particular journal event — say which, and
how to get there in the game. Where a setting has to be changed first, name the setting and the
value, and add a final step that puts it back.

Not acceptable: "check that it works", "verify the panel looks right", "try a few commands",
"confirm nothing regressed".

## Finishing

The turn where the work lands ends in this order:

1. Commit.
2. `/code-review` or `/prose`, when the Reviews section calls for one, with its findings amended into
   the commit.
3. `/test-drive`, when the change needs manual testing.
4. The spoken done sentence, through `/claude-voice`'s command, unless the voice was turned off. It
   is the last tool call of the turn.
5. The written report, then how to test it — the exact steps, against the test drive now running.

The voice rules were loaded at the start of the session, many tool calls earlier, and writing the
report ends the turn. A sentence left until after the report is not spoken.

A lane ends the same way, once, after its last issue: steps 1 and 2 have already happened per issue
inside **Each issue**, then `/test-drive` when any issue needs manual testing, the spoken sentence,
and a report with one line per issue — merged with its commit subject, skipped, or not landed and
why — followed by the steps.

This applies equally when the work lands on a turn started by a background agent's completion
notice rather than by the maintainer.
