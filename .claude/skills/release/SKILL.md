---
name: release
description: Cut a patch or minor release — first install the new version over the local install, backed up, and tell the maintainer it is ready to run, then run tools/release.ps1 and show its progress as a checklist with one plain sentence per stage — the suite, the avatar clips, the dispatch, the workflow, the comparison and the Defender scan — and when the build or a test fails, locally or on the runner, fix it forward — the new version stays installed, the fix is pushed, the fixed build is installed over it and the release runs again. Says aloud and in a heading when d47 is ready to run and when the release needs a fix. Use when the user invokes /release, /release patch or /release minor, or says "run the release", "release a patch and fix what breaks".
---

# Release

You install the version being cut over the local install, tell the maintainer it is ready to run,
then run `tools\release.ps1` and stay with it until the version is published. The script does the
work; you keep a checklist of its stages and write one plain sentence as each one finishes. When a
build or test fails you fix it forward: the new version stays installed, you fix the cause, push,
install the fixed build over it and start the release again.

The maintainer's first priority is a working d47 on this PC; the published release comes second.
Every step that can be ordered so the app is ready sooner is ordered that way. Closing and starting
the installed d47 is allowed whenever a step needs it.

What you deliver is a published release, or a plain statement of what is stopping it, and in both
cases a statement of which version is installed.

## The argument

- `patch` runs `-Patch`, `minor` runs `-Minor`.
- No argument: ask with AskUserQuestion, Patch or Minor, before anything else runs. The Stream
  Deck's Release key starts the session this way.
- `major`, or anything else: refuse. A major release is cut at the desk with the script.

The invocation is the confirmation. Do not ask again before running the script.

## Turn the voice on first

Run `/claude-voice Release`. The release takes about twenty minutes and the maintainer will be
doing something else. `/claude-voice off` stops it and the release carries on.

## The checklist

The maintainer follows the release on the session's task list, not by reading log output. Set the
list right after the argument, with `mcp__ccd_turn__update_plan` in the desktop app, or TaskCreate
and TaskUpdate where that tool is missing:

1. Check the branch
2. Install the new version locally
3. Run the test suite
4. Publish the avatar clips
5. Dispatch the release workflow
6. Build and test on GitHub
7. Package and scan on GitHub
8. Publish the release
9. Compare with a rebuild of the tag
10. Scan the download with Defender
11. Write the report

Workflow steps map to items 6 to 8: "Install the .NET SDK" through "Test" are item 6, "Publish"
through "Attest the artifacts" are item 7, "Publish the release" and the docs job are item 8.

`update_plan` must go out in the same message as another tool call. Send `{"advance": true}`, or
`{"done": [...], "start": N}` when several stages finished at once, alongside the next call you
make anyway: re-arming the Monitor, reading the log, running the next command. Never let the list
fall behind by more than one event.

When a fix is needed, send the whole list again with the failed item still in progress and these
inserted after it, then the remaining items from "Run the test suite" on:

- Fix <the failure, in a few words>
- Push the fix
- Install the fixed v<version> locally

### What to write

Write plain sentences, not log output. When a stage finishes, write one sentence that says what
happened, with the numbers that matter: "The suite passed: 7 projects in 2 min 4 s.", "Avatar
clips: 3 uploaded, 41 already there.", "Dispatched v1.33.4: <run URL>.", "GitHub built and tested
it in 5 min 12 s." Lines that do not finish a stage get no text; advance the checklist if they
should, and say nothing.

Use a fenced block only for failure output: the failing test's message and stack, the runner step's
error from `gh run view --log-failed`, a script's red block. Quote the lines that show the cause,
not the whole log.

## Ready and fix notices

The maintainer waits to hear when they can use the new d47. Two notices tell them, each as a `##`
heading in the transcript and a spoken sentence:

| Notice | Heading | Spoken |
|---|---|---|
| Ready | `## d47 v<version> is ready` | `Release. d47 version <version in words> is ready.` |
| Fix | `## v<version> needs a fix` | `Release. Version <version in words> needs a fix. It stays installed while I fix it forward.` |

Under the ready heading, one line: `It is running.` when the deploy started d47 again, or `You can
launch it.` when d47 was not running.

Print the heading outside any fenced block, then speak the sentence at once, mid-turn, with the
voice skill's command:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File "$HOME/.claude/skills/claude-voice/say.ps1" -Text "Release. d47 version zero point one one two point three is ready."
```

These sentences are spoken in addition to the voice skill's two moments, and do not count against
its one sentence per turn. Echo each one in a quoted block as that skill says. After
`/claude-voice off`, print the heading and skip the speech. `say.ps1` handles the global mute by
itself.

**Ready** goes out as soon as `deploy-local.ps1` finishes with `selftest passed`, before the release
script starts. It goes out again after every redeploy that follows a fix. Never after a failed
selftest.

**Fix** goes out when you decide a failure needs a change to the code: a local suite failure, a
runner Build or Test failure, or a runner failure before "Publish the release" whose cause is in the
repository. The installed build is not touched until the fixed one replaces it.

## Before the script

The maintainer runs `/release` only once the lanes and issues for the release are done. Do not
check for running lanes or ask whether to wait for one.

Put the branch where the script needs it. These are checks; do not print their output:

```bash
git status --porcelain; git rev-parse --abbrev-ref HEAD
git fetch origin main --tags --quiet
git rev-list --left-right --count origin/main...HEAD
```

- Not on `main`, or the tree is dirty (untracked files count): report it and stop. Do not commit,
  stash or delete anything to get past it.
- Ahead of `origin/main` with a clean tree: push `main`. The suite in the script runs before
  anything is dispatched, and a red suite is fixed forward.
- Behind: `git pull --ff-only`. Diverged: report it and stop.

## Install the new version locally

Install the version the script is about to cut over `%LOCALAPPDATA%\Programs\d47`. This comes after
the branch checks so the installed build is the commit the workflow builds, and before the script so
the maintainer has it as early as possible. Substitute `-Patch` or `-Minor`:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/skills/release/deploy-local.ps1 -Patch
```

`timeout: 600000`. It computes the next version the way `release.ps1` does, publishes `D47.App` in
Release stamped with it, closes the installed d47, backs up its payload and `data\` to
`%LOCALAPPDATA%\d47-release-backup`, mirrors the new payload in, runs `d47.exe --selftest` on it and
starts the app again if it was running. `data\` is backed up without the folders the script's
`$leftOut` names (downloads, recordings, logs, music, models), which no build changes.

Remember three things from its output: the version deployed, the version backed up, and whether it
closed a running d47 (and so started it again: `started …d47.exe again`).

- **Another d47 is running** outside the install, such as a test drive or a local release: the
  script refuses, because a Debug build writes the installed `data\` from wherever it runs. Close
  that copy (`Stop-Process` on the pid the message names), run the deploy again, and start
  `%LOCALAPPDATA%\Programs\d47\d47.exe` once it has passed.
- **Another process has a file from the install loaded**: Elite in VR loads
  `runtimes\openxr\XR_APILAYER_D47_panel.dll`, and the copy of `runtimes` would fail part way. The
  script refuses before closing or copying anything, and names the process and pid. Ask the
  maintainer to close it and run the same command again once they have. Do not close Elite yourself.
- **The publish failed.** Nothing was installed or backed up. It is a build failure: fix it as in
  "The local suite failed", commit, push, and deploy again.
- **`SELFTEST:` failed.** The deployed build is defective. Restore at once (below, with `-Start` if
  the deploy closed a running d47), read the log it names, and fix it as a build failure when the
  cause is in the repository. A missing model (exit 3) is the machine, not the build: report it
  and stop.
- **`v… is already installed; keeping the backup of v…`.** An earlier deploy of this version was
  never restored. The backup still holds the version before it, which is what a restore needs.

When it passes, tick item 2 and send the ready notice.

When `release.ps1` prints its first line, check that its `vX -> vY` names the version you deployed.
If it does not, restore and stop.

### Restore

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/skills/release/deploy-local.ps1 -Restore
```

It closes the installed d47, saves the `data\` the deployed build wrote to a dated
`replaced-data-…` folder in `%LOCALAPPDATA%\d47-release-backup`, puts the backed-up payload and
`data\` back, and starts the app again if it was running. Add `-Start` when the app is closed only
because a deploy step stopped part way. Put the `replaced-data-…` path it prints in the report: it
is whatever the maintainer's play wrote during the release.

A fix is never a reason to restore. Fix forward instead: the deployed build stays installed while
you fix, and after the push you deploy again, which installs the fixed HEAD over it under the same
number, and send the ready notice. The redeploy is not optional. Once a commit lands between the
deploy and the dispatch, the published build differs from the installed one but carries the same
number, and the updater treats the install as current and never replaces it.

- **A redeploy refused because Elite holds the layer DLL.** When the fix commits changed nothing
  outside `tests/`, the installed payload is already the code the release ships: say so and run
  the script again without the redeploy. Otherwise ask the maintainer to close Elite and wait; do
  not run the script again until the fixed build is installed.
- A runner re-run with no code change (`gh run rerun`) keeps the deploy.

Restore only when the installed build cannot be used or cannot be released:

- **The selftest failed** (above).
- **The maintainer says the deployed build is broken** while the release is running. If the run has
  been dispatched, cancelling it is the maintainer's decision.
- **`release.ps1` names a different version** than the one deployed.

When the release stops for a reason that is not a defect in the build (a refusal from the script's
checks, an avatar upload error, a dispatch or sign-in failure, the "Publish the release" step),
leave the deploy in place and say in the report that an unreleased version is installed, with the
restore command.

## Run it in the background and follow its log

A release takes longer than a monitor can live (30 minutes): the suite, the workflow and the
comparison together run past it, and a monitor that expires kills the command it runs. So the
script runs as a background Bash writing a log, and Monitor only reads the log.

First clear the last run's log, in the foreground:

```bash
mkdir -p "$LOCALAPPDATA/d47-release-log"; rm -f "$LOCALAPPDATA/d47-release-log/release.log" "$LOCALAPPDATA/d47-release-log/release.log.seen"
```

Then start the script with Bash `run_in_background: true`. Substitute `-Patch` or `-Minor`:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/release.ps1 -Patch 2>&1 | gawk -f .claude/skills/release/progress.awk > "$LOCALAPPDATA/d47-release-log/release.log"; echo "release.ps1 exited ${PIPESTATUS[0]}" >> "$LOCALAPPDATA/d47-release-log/release.log"
```

Then follow the log with Monitor. Monitor is a deferred tool: load it with `ToolSearch`
(`select:Monitor`) first.

```bash
bash .claude/skills/release/follow.sh "$LOCALAPPDATA/d47-release-log/release.log"
```

`timeout_ms: 1800000`, description `release.ps1 -Patch`. `progress.awk` drops the watcher's
in-place status line and dotnet's build chatter and keeps everything else, failures included.
`follow.sh` prints each line of the log once and exits after `release.ps1 exited N`. When the
monitor expires, start the same Monitor command again: it carries on from the first line it has not
printed. The background Bash's own completion notice repeats what the log says; use the log.

The log's stages, in order, and the checklist item each one finishes:

1. `v1.33.3 -> v1.33.4` — the version this run will produce. Check it against the deploy.
2. `Running the suite...`, then one `Passed!` or `Failed!` line per test project (about two
   minutes). Item 3 when the script moves on.
3. `Publishing avatar clips...` and what it uploaded or skipped. Item 4.
4. `Dispatched v…` and the run URL. Item 5.
5. Each workflow step with its time as it finishes. Items 6 to 8, as mapped above.
6. `success in …` or `failure after …`.
7. After a success, `Comparing v… with a rebuild of its tag...` and one of: `v…: N of N entries in
   d47.zip match a rebuild of the tag`, a red block, or a line saying the comparison did not run.
   Item 9.
8. `Scanning v… with Defender...` and its result. Item 10. Then `release.ps1 exited N`.

The workflow alone takes about fourteen minutes. If the background Bash ends and the log has no
`release.ps1 exited` line, the script was killed. If the log has a `Watching …/runs/<id>` line,
the dispatch has already happened and the runner carries on: watch it with Monitor, using that
run id, and run the comparison and the Defender scan from the end of `tools/release.ps1` by hand
once it succeeds:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/watch-release.ps1 -RunId <id> 2>&1 | gawk -f .claude/skills/release/progress.awk; echo "watch-release.ps1 exited ${PIPESTATUS[0]}"
```

## When it fails

Read which stage failed before doing anything. Only build and test failures are yours to fix. For
each one: fix notice, fix, commit, push, deploy again, ready notice, run the script again. The checklist shows that sequence (see "The checklist").

### The local suite failed

The script throws `The suite failed. Nothing dispatched.` Nothing was tagged.

**`D47.App.Tests` alone failed, and d47 is running.** The Avalonia headless tests share one
dispatcher thread, and a running d47 competes with them for the machine. Close the installed d47,
re-run that project alone (`dotnet test tests/D47.App.Tests -c Release`), and start d47 again.
If it passes with d47 closed, this is contention, not a defect: run the script again with d47
closed, and start d47 again when the suite has passed. Report it as contention, not as fixed. If
it fails again, it is a real failure: carry on below.

Fix it under `/pre-release`'s rules — read `.claude/skills/pre-release/SKILL.md` sections "Three
kinds of failure", "Never fix a test by weakening it" and "Commit the fixes" and follow them. In
short: a build break is fixed in the code with no new `#pragma` or `SuppressMessage`; a gate test is
satisfied, never edited to accept the tree; a test that fails in the suite and passes alone is a
finding to report, not green; a fix a user would notice gets a `CHANGELOG.md` entry in the same
commit.

Re-run the failing project, not the solution, while fixing:

```bash
dotnet test tests/<Project> -c Release --filter FullyQualifiedName~<Name>
```

Send the fix notice, leave the deployed build installed, then commit, push `main`, deploy again, send the ready notice, and run the script again in the background, following its log. It
recomputes the same version from the newest tag and runs the whole suite again, which is the check
that counts.

### The runner failed

The watcher prints the failing step in red and `failure after …`. Read the log:

```bash
gh run view <id> --log-failed
```

- **Build or Test.** The tag does not exist yet. Confirm with
  `git ls-remote --tags origin v<version>` (empty output). Fix notice, fix it as above,
  push, deploy again, ready notice, and run the script again: it dispatches the same version.
- **Publish, the installer, packing or checksums** — before "Publish the release". Still no tag.
  When the cause is in the repository, handle it the same way. When it is the runner (a download or
  network failure, `choco` failing to install), keep the deploy, re-run the job once with
  `gh run rerun <id> --failed` and re-arm the watcher on the same id.
- **"Publish the release" or the docs job.** The tag may already exist, and the workflow refuses a
  version whose tag exists. Report what failed and stop. Re-deploying the docs
  (`gh workflow run release.yml -f version=<v> -f docs_only=true`) is the maintainer's decision.

### The comparison after the release

`tools/verify-release.ps1` runs once the watcher reports success. It rebuilds the tag from a fresh
clone mapped to `B:\` and only reports.

- **Exit 0 with the match line**: every entry matches. Say so in the closing lines.
- **Exit 4** (the verifier found a difference): the release is live and does not match a rebuild of
  its tag. This is not a code defect to fix and run again. Report the differing entries the script
  printed, including any bundle entries inside `d47.exe`, and the command it gave to take the
  release out of `releases/latest`. Do not run that command. Leave the installed version in place
  and stop. The verifier keeps its temporary folder; name its path.
- **Exit 0 with `did not run`**: the release succeeded and the comparison could not run. Say so in
  the closing lines, with the reason the script printed.
- **Exit 0 with `except the OpenXR layer DLL`**: every entry matches except
  `runtimes\openxr\XR_APILAYER_D47_panel.dll`, which the runner linked with another MSVC linker
  version. Say so in the closing lines with both linker versions the script printed. It is not a
  mismatch to report as one.

### Defender detections

`tools/scan-release.ps1` scans with Microsoft Defender in two places. A detection is a verdict on
the shipped bytes, not a code defect: never change code or re-run to get past it.

- **On the runner**, the step "Scan the artifacts with Defender" fails the run before the tag
  exists. Read its log for the file and threat. If it names a detection: restore the backup, report
  the file and threat to the maintainer, and stop. If the scan could not run (`NOT SCANNED`, no
  `MpCmdRun.exe`), report that and stop as well; the runner's Defender state is unverified.
- **On this PC**, after the release is live, `release.ps1` scans the downloaded `d47.zip` and
  installer. A detection prints a red block with the file, the threat and the Microsoft submission
  page, and the script exits 3. Report the same facts, leave the installed version in place, and
  stop. Do not edit or delete the release. A line saying the scan did not run is reported and the
  release stands.

### Anything else

A refusal from the script's own checks, a `publish-avatars.ps1` error (ffmpeg, upload), a dispatch
failure or a `gh` sign-in problem is not a build or test failure. Report it with the message the
script printed and stop.

### When to stop

Stop and report when the same failure survives a fix, or after three runs of the script. Say what
failed, what you changed, what is pushed, and which version is installed. A stop after a fix is
committed leaves an unreleased version installed: say so in the report, with the restore command.

## Pushing and the local install

The maintainer asked for this skill to fix and continue, so pushing a fix to `main` is part of the
job and needs no question. Push `main` only: no tags, no other branch, never `--force`. Every push
also ships any local commits this session did not write; name them in the report.

The invocation also authorizes closing and starting the installed d47 and any other d47 that blocks
the deploy, and writing `%LOCALAPPDATA%\Programs\d47`, its `data\`,
`%LOCALAPPDATA%\d47-release-build`, `%LOCALAPPDATA%\d47-release-backup` and the release log in
`%LOCALAPPDATA%\d47-release-log`. Nothing else outside the checkout.

## Output

The report's first line is a level-two heading that says whether the release succeeded. ✅ is the
green mark and ❌ the red one:

```
## ✅ Release v<version> succeeded
## ❌ Release v<version> failed at <stage>
## ❌ Release v<version> is live and failed its <comparison | Defender scan>
```

The success heading goes out only when the release is published and both checks that follow it
passed or did not run: `release.ps1` exited 0, or, after the script was killed, the watcher reported
`success` and the comparison and the Defender scan found nothing. A difference from the rebuild
(exit 4) or a Defender detection (exit 3) gets the third heading. Every other stop gets the second,
naming the stage: the local suite, the runner's step, the avatar upload, the dispatch, or the
script's own checks. Apart from the ready and fix notices earlier in the run, no other line in
the report is a heading.

Under it, a short list in plain words:

- The release: the version, the release URL
  (`https://github.com/dseelinger/d47/releases/tag/v<version>`) and the total time.
- The local install: `v<version> is installed`, or `restored to v<previous>` with the
  `replaced-data-…` path, or an unreleased version left installed with the restore command.
- The checks after publishing: the comparison and the Defender scan, one clause each.
- Anything still open: a test that failed only in the full run, a docs job that failed, a fix you
  could not make.

If there were fixes, a table follows: stage, test or step, the cause in a clause, and the commit
subject.

When it stops without a release, the failure heading names the stage it stopped at. Then come the
same list and table.

## What this does not do

It never runs `-Major`, never creates or moves a tag by hand, never renumbers a `CHANGELOG.md`
heading, and never weakens a test to get a release out. `/deck patch` and `/deck minor` run the same
`release.ps1` from the Launcher session without deploying locally or fixing anything.
