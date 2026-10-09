---
name: issue-worker
description: Take one GitHub issue and land it — read it, fix it, keep the build clean, add the changelog entry when the change is user-visible, and commit in the repository's form. An issue in a lane from /triage lanes is worked in its own worktree and fast-forwarded into main when it lands. Does not push — the maintainer pushes once the review has run. The session that does the work, as opposed to the ones that decide it. Use when the user invokes /issue-worker, or says "you are the issue worker", "fix issue N", "take #N", "implement this issue".
---

# Issue worker

You take one issue and land it. The Coordinator decided it was next; the Architect settled anything
that needed settling. Your job is the change itself.

`/issue-worker 62` has named the issue. The number is the work, not a label for the session, so
start on #62 in that same turn — do not acknowledge it and wait for a second instruction saying the
same thing.

Only a bare `/issue-worker`, carrying no issue, waits: acknowledge in one line, stop, and start when
the maintainer names one.

## Turn the voice on first

Once the issue is named, the first step of the working turn is `/claude-voice Issue worker <number>`,
the number spoken as words — `/claude-voice Issue worker sixty six` for #66. Several of these
sessions run at once and are told apart by ear, and the number is what tells them apart.

It is a default, not a fixture: `/claude-voice off` stops it and the work carries on unchanged.

## One issue, one checkout

By default there is one checkout and work runs sequentially — no worktrees, no branches, no
parallel issues. `main` is fixed forward: nothing runs on push, there are no PRs, and a mistake is
a follow-up commit rather than a revert.

Two cases put the work in a worktree instead, both covered by **Lanes** below:

- the issue's entry in the main checkout's `.claude/triage-state.json` has a `lane`;
- `git worktree list` shows a worktree under `.claude/worktrees/`. Lanes are running and merging
  into `main`, so an issue outside them goes through a worktree too, or its uncommitted edits sit
  in the tree the lanes fast-forward.

**Settle this before reading the issue, and do not skip it for a small change.** A one-line
rename edited in the main checkout blocks every lane's merge just as a large change does. Run:

```bash
git rev-parse --show-toplevel; git worktree list
python -c "import json,sys; e=json.load(open(sys.argv[1]))['issues'].get(sys.argv[2]); print('lane', e['lane'] if e and e.get('lane') else 'none')" "$(git worktree list | head -1 | cut -d' ' -f1)/.claude/triage-state.json" <N>
```

The file is pretty-printed, so a line-based `grep` for the entry finds nothing; read it as JSON.

- The top level is already `.claude/worktrees/<N>`: the Issue key created the worktree and started
  this session in it. Work there, and land it through **Merging into main** whether or not the
  issue has a lane.
- Either case above holds and the top level is the main checkout: create the worktree as
  **The worktree** says and enter it before the next tool call.
- Neither holds: work in the main checkout.

If the worktree cannot be created, stop and say why. Never fall back to editing the main checkout.

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
line `Fixes #N` — work committed but not yet pushed is not closed on GitHub:

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

During issue work, run only the unit tests for the area you changed, plus the integration tests
that the change affects. A unit test does not cross a process boundary or touch the file system, and runs in under a second;
an integration test does either. Filter by the area, in each project the diff touches:

```bash
dotnet test tests/D47.Core.Tests --filter FullyQualifiedName~<Area>
dotnet test tests/D47.App.Tests --filter FullyQualifiedName~<Area>
```

An integration test is affected when the code it reaches across the boundary is code the diff
changed. Run those by class name too. Do not run a whole test project unfiltered, and never
`dotnet test d47.slnx`: the whole suite, `D47.App.Tests` included, is the release gate and runs
in `/pre-release` and `tools/release.ps1`.

Before committing, also run the gate tests. They check the whole tree against a list or a rule — a
journal event dispatched on must be in `HandledEvents.ActedOn`, `AppHost.cs` call sites are
counted, a capability must have a docs page — so a change can break one without matching the area
filter (each takes a few seconds):

```bash
dotnet test tests/D47.Core.Tests --filter "Category=Gate"
dotnet test tests/D47.App.Tests --filter "Category=Gate"
```

Run the second one whenever the diff touches `src/D47.App/`, `docs/` or `installer/`. A new App test
that reads shipped files (src, docs, assets, tools) against a rule carries `[Trait("Category", "Gate")]`, on the
method when its class also has rendering tests.

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

`.claude/triage-state.json` in the main checkout holds the last triage's grid, keyed by issue
number. It is not tracked, so a worktree has no copy; read it by the main checkout's path. Read the entry for
this issue when the file is there; it is a snapshot of a queue that moves, so treat a missing entry
as no information rather than a verdict.

The Stream Deck's Issue key has already applied `model`, `effort` and `advisor` at launch. **A
session cannot change its own model or effort** — the desktop app refuses both for the session
itself. So where the entry names a model other than the one you are running as, say so in one line
and carry on; switching is the maintainer's, from the model picker. Never run `/advisor` either: it
saves the choice to the user settings, so every later session and subagent inherits it.

When the session has an advisor, consult it twice: once the cause is confirmed against the code and
before choosing the fix, and again before committing. Consult it outside those two points only when
the same build or test failure comes back after a fix.

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

`/triage lanes` splits the queue into lanes: issues that need each other or edit the same code share
a lane, and different lanes run at the same time in other sessions. Each issue is still its own
session on its own model; this session works one issue and ends. The lanes are in the `lanes`
object of `.claude/triage-state.json`, each a list of issue numbers in work order.

The main checkout is the first entry of `git worktree list`. Before touching anything, check:

- **The issue before this one in its lane has merged** — a commit on `main` with the line
  `Fixes #N`. If not, stop and ask: this issue edits the same code and would conflict with it. A
  direct instruction to go ahead anyway overrides the check, as for **Check what it needs first**.
- **The main checkout has no uncommitted changes to tracked files**
  (`git -C <main checkout> status --porcelain --untracked-files=no`). If it has, stop and list them:
  the merge fast-forwards that working tree.

### The worktree

The Issue key creates it before the session starts, and starts the session inside it. When this
session started in the main checkout instead, create it from local `main`, then move the session
into it:

```bash
git -C <main checkout> worktree add .claude/worktrees/<N> -b issue/<N> main
```

Then call `EnterWorktree` with that path. Do not create it with `EnterWorktree`'s `name`: that
branches from `origin/main` and misses every commit not yet pushed, including the ones other lanes
just merged.

Do the work as the rest of this skill says — build, filtered tests, gate tests, changelog, commit,
and a review where **Reviews** calls for one — all inside the worktree. The first build in a fresh
worktree restores and builds everything, so it takes longer than usual. Captures are taken in the
worktree, as **Capture it yourself** says.

Once it is merged, as below, call `ExitWorktree` with `keep` (it does not remove a worktree entered
by path), then remove the worktree and its branch:

```bash
git -C <main checkout> worktree remove .claude/worktrees/<N>
git -C <main checkout> branch -d issue/<N>
```

If the removal fails on a locked file, leave it and name it in the report. A session the Issue key
started inside the worktree has no `EnterWorktree` to exit, and holds the folder as its working
directory, so the removal may leave the folder behind. That is expected: `/pre-release` clears it.

When the issue does not land — it is bigger than it looked, the build or tests cannot be made
green, or any step of **Merging into main** stops — stop and ask, as for any issue. Leave the worktree and branch in place and say where they are. The rest of its lane
waits on it.

### Merging into main

`main` takes fast-forwards only, so its history stays one line with no merge commits. Lanes merge
one at a time under a lock, so `main` does not move between this lane's rebase and its merge.

#### The merge lock

The lock is the directory `<main checkout>/.git/d47-merge.lock`. Every worktree shares that `.git`,
and `mkdir` either creates the directory or fails, so only one lane holds it. Take it once the
issue's commit is made and its review is done, in a Bash call with `run_in_background`:

```bash
L="<main checkout>/.git/d47-merge.lock"
while ! mkdir "$L" 2>/dev/null; do
  if [ -n "$(find "$L/owner" -mmin +45 2>/dev/null)" ]; then echo "STALE: $(cat "$L/owner")"; exit 1; fi
  sleep 5
done
echo "issue <N>, taken $(date -u +%FT%TZ)" > "$L/owner"
echo ACQUIRED
```

It returns at once when no lane is merging. Otherwise it waits, and this session is notified when
it exits; do nothing in the meantime. When several lanes wait, whichever `mkdir` succeeds first
merges next and the others keep waiting.

- `ACQUIRED`: run the three steps below.
- `STALE`: a lane has held the lock for over 45 minutes, which means its session ended without
  releasing it. Stop and report the owner line. Do not remove the lock: the maintainer checks that
  session and removes it.

Release the lock on **every** way out of the three steps — a merge, an aborted rebase, a failed
check, a refused merge — before the report, and only when `owner` names this issue:

```bash
grep -q "issue <N>," "<main checkout>/.git/d47-merge.lock/owner" && rm -r "<main checkout>/.git/d47-merge.lock"
```

The lock's disappearing is what lets the waiting lanes continue; nothing else signals them.

#### The three steps

Run each step below as its own command. Never chain them with `&&` or `;`: a failed check must
never be followed by a merge in the same command.

1. In the worktree:

   ```bash
   git rebase main
   ```

   - A `CHANGELOG.md` conflict is expected, since every issue adds entries at the top. Keep both,
     this one under the unreleased heading `main` has, and leave `main`'s entries as they are.
   - A conflict in any other file means triage judged two issues independent and they are not. Do
     not resolve it. `git rebase --abort`, so the branch holds the commit as it was before the
     rebase, and stop.

2. Run the checks again on the rebased tree — the build, the area filters, the affected integration
   tests and both gate filters — because other lanes' work is now under
   the change. If any fails, stop. Do not fix it in this session: the failure comes from the
   combination with another lane's work, which is the maintainer's to judge.

3. Merge:

   ```bash
   git -C <main checkout> merge --ff-only issue/<N>
   ```

   Under the lock no other lane can have merged, so a refusal or an `index.lock` means something
   changed `main` outside the lanes, such as a commit made directly in the main checkout. Stop. Do
   not rebase again.

When any step stops, release the lock, leave the worktree and branch in place and do not call
`ExitWorktree`. The report says the lane is paused at this issue, which step stopped it, and:

- for a conflict, each conflicting file and the commits on `main` that changed it since the branch
  point (`git log --format='%h %s' issue/<N>..main -- <file>`);
- for a failed check, the failing build error or test names and their output;
- for a refused merge, the commits that landed on `main` since the rebase
  (`git log --format='%h %s' issue/<N>..main`).

A paused lane starts nothing new: the issue before the next one has no `Fixes #N` commit, so the
check at the top of **Lanes** stops it. The other lanes keep running. The maintainer resumes the
paused issue once the other lanes have finished, by starting `/issue-worker <N>` again. That session
finds the worktree and branch already there, enters the worktree, takes the lock, and runs the three
steps above. On resume, a conflict in a file other than `CHANGELOG.md` is resolved against everything
merged in the meantime rather than aborted, and a failed check is fixed in the worktree and amended
into the issue's commit; a refused merge still stops.

Never create a merge commit, never force anything, and never push. The rebase changes the commit's
hash, which matters to nothing: it is still local.

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

An issue worked in a worktree adds two steps after the review: merge it into `main` and remove the
worktree, as **Lanes** says. `/test-drive` then runs from the main checkout, so the build under test
holds everything merged so far from every lane; say so in the line above the steps. When the
merge into `main` succeeded, the report's first line is `Lane <letter> issue complete`, with this
issue's lane letter — `Lane A issue complete`, `Lane B issue complete`, `Lane C issue complete`.
The report ends with the next issue in this lane, if there is one: the Issue key given the lane
letter starts it. When the merge stopped, there is no completion line, no test drive and no next
issue: the report says which step stopped it, as **Merging into main** lists, and that the lane is
paused.

This applies equally when the work lands on a turn started by a background agent's completion
notice rather than by the maintainer.
