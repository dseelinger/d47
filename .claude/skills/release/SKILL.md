---
name: release
description: Cut a patch or minor release — first install the new version over the local install, backed up, then run tools/release.ps1 and relay its progress line by line as the script prints it — the version, the suite, the avatar clips, the dispatch and every workflow step — and when the build or a test fails, locally or on the runner, restore the backup, fix it, push the fix and run the release again. Says aloud and in a heading when the maintainer may launch the installed build and when they must close it for a fix. Use when the user invokes /release, /release patch or /release minor, or says "run the release", "release a patch and fix what breaks".
---

# Release

You install the version being cut over the local install, then run `tools\release.ps1` and stay
with it until the version is published. The script does the work; you show its progress to the
maintainer as it happens, and when a build or test fails you put the previous install back, fix the
cause and start the release again. What you deliver is a published release, or a plain statement of
what is stopping it, and in both cases a statement of which version is installed.

## The argument

- `patch` runs `-Patch`, `minor` runs `-Minor`.
- No argument: ask with AskUserQuestion, Patch or Minor, before anything else runs. The Stream
  Deck's Release key starts the session this way.
- `major`, or anything else: refuse. A major release is cut at the desk with the script.

The invocation is the confirmation. Do not ask again before running the script.

## Turn the voice on first

Run `/claude-voice Release`. The release takes about twenty minutes and the maintainer will be
doing something else. `/claude-voice off` stops it and the release carries on.

## Launch and close notices

The maintainer waits to hear when they may use the installed d47 and when they must stop using it.
Two notices tell them, each as a `##` heading in the transcript and a spoken sentence:

| Notice | Heading | Spoken |
|---|---|---|
| Launch | `## You may launch d47 v<version>` | `Release. You may launch d47 version <version in words>.` |
| Close | `## Close d47 now: v<version> needs a fix` | `Release. Close d47 now, version <version in words> needs a fix.` |

When the deploy restarted d47 itself, the launch heading reads `## d47 v<version> is running: you may
use it`, and the sentence says the same.

Print the heading outside any fenced block, then speak the sentence at once, mid-turn, with the
voice skill's command:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File "$HOME/.claude/skills/claude-voice/say.ps1" -Text "Release. You may launch d47 version zero point one one two point three."
```

These two sentences are spoken in addition to the voice skill's two moments, and do not count
against its one sentence per turn. Echo each one in a quoted block as that skill says. After
`/claude-voice off`, print the heading and skip the speech. `say.ps1` handles the global mute by
itself.

**Launch** goes out once the local suite has passed (the line after the last `Passed!`, when the
script moves on to the avatar clips), for the version deployed. Not straight after the deploy:
a running d47 competes with `D47.App.Tests`, and a suite failure would close it again minutes later.
It goes out again after every redeploy that follows a fix. Never after a failed selftest.

**Close** goes out as soon as you decide that a failure needs a change to the code and a new deploy,
before you start the fix: a local suite failure, a runner Build or Test failure, a runner failure
before "Publish the release" whose cause is in the repository, or the maintainer reporting the
deployed build broken. Send it only when a launch notice went out for this deploy or the deploy
restarted d47 itself; otherwise nothing is running from it. Add one plain line under the heading:
the restore, just before the fix commit, closes d47 if it is still open and starts the previous
version again if it was running. A restore that no redeploy follows (a Defender detection, a version
mismatch) gets no close notice.

## Before the script

The maintainer runs `/release` only once the lanes and issues for the release are done. Do not
check for running lanes or ask whether to wait for one.

Put the branch where the script needs it:

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

## Deploy the new version locally

Before the script runs, install the version it is about to cut over
`%LOCALAPPDATA%\Programs\d47`. This comes after the branch checks so the installed build is the
commit the workflow builds. Substitute `-Patch` or `-Minor`:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/skills/release/deploy-local.ps1 -Patch
```

`timeout: 600000`. It computes the next version the way `release.ps1` does, publishes `D47.App` in
Release stamped with it, closes the installed d47, backs up its payload and `data\` to
`%LOCALAPPDATA%\d47-release-backup`, mirrors the new payload in, runs `d47.exe --selftest` on it and
starts the app again if it was running. `data\` is backed up without the folders the script's
`$leftOut` names (downloads, recordings, logs, music, models), which no build changes.

Both modes refuse while any other d47 is running outside the install, such as a test drive: a Debug
build writes the installed `data\` from wherever it runs. Report the refusal and stop; do not close
that copy.

Relay what it prints. Remember three things from it: the version deployed, the version backed up,
and whether it closed a running d47 (and so started it again: `started …d47.exe again`).

- **The publish failed.** Nothing was installed or backed up. It is a build failure: fix it as in
  "The local suite failed", commit, push, and deploy again.
- **`SELFTEST:` failed.** The deployed build is defective. Restore at once (below, with `-Start` if
  the deploy closed a running d47), read the log it names, and fix it as a build failure when the
  cause is in the repository. A missing model (exit 3) is the machine, not the build: report it
  and stop.
- **`v… is already installed; keeping the backup of v…`.** An earlier deploy of this version was
  never restored. The backup still holds the version before it, which is what a restore needs.

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

Restore when:

- **You are about to commit a fix.** Once a commit lands between the deploy and the dispatch, the
  published build differs from the installed one but carries the same number, and the updater
  treats the install as current and never replaces it. Restore before the fix commit; the next run
  of this skill's deploy step installs the new HEAD. A runner re-run with no code change
  (`gh run rerun`) keeps the deploy.
- **The selftest failed** (above).
- **The maintainer says the deployed build is broken** while the release is running. Send the close
  notice. If the run has been dispatched, cancelling it is the maintainer's decision.
- **`release.ps1` names a different version** than the one deployed.

When the release stops for a reason that is not a defect in the build (a refusal from the script's
checks, an avatar upload error, a dispatch or sign-in failure, the "Publish the release" step),
leave the deploy in place and say in the report that an unreleased version is installed, with the
restore command.

## Run it under Monitor

Run the script with Monitor, not a background Bash, so each line it prints reaches you as it is
printed. Monitor is a deferred tool: load it with `ToolSearch` (`select:Monitor`) first. Substitute
`-Patch` or `-Minor`:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/release.ps1 -Patch 2>&1 | gawk -f .claude/skills/release/progress.awk; echo "release.ps1 exited ${PIPESTATUS[0]}"
```

`timeout_ms: 1800000`, description `release.ps1 -Patch`. `progress.awk` drops the watcher's
in-place status line and dotnet's build chatter and keeps everything else, failures included.

Relay every event as it arrives: the lines verbatim in a fenced block, so the step names and
times stay aligned, with nothing added except a one-line note when a step failed and the launch or
close notice where it falls. The stages, in order:

1. `v1.33.3 -> v1.33.4` — the version this run will produce.
2. `Running the suite...`, then one `Passed!` or `Failed!` line per test project (about two minutes).
   The launch notice goes out when the suite has passed.
3. `Publishing avatar clips...` and what it uploaded or skipped.
4. `Dispatched v…`, the run URL, then each workflow step with its time as it finishes.
5. `success in …` or `failure after …`.
6. After a success, `Comparing v… with a rebuild of its tag...` and one of: `v…: N of N entries in
   d47.zip match a rebuild of the tag`, a red block, or a line saying the comparison did not run.
   Then `release.ps1 exited N`.

The workflow alone takes about fourteen minutes. If the monitor expires while the run is going,
the dispatch has already happened and the runner carries on. Re-arm it on the watcher, using the
run id from the `Watching …/runs/<id>` line:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/watch-release.ps1 -RunId <id> 2>&1 | gawk -f .claude/skills/release/progress.awk; echo "watch-release.ps1 exited ${PIPESTATUS[0]}"
```

## When it fails

Read which stage failed before doing anything. Only build and test failures are yours to fix.

### The local suite failed

The script throws `The suite failed. Nothing dispatched.` Nothing was tagged.

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

Send the close notice before starting the fix. Restore the local install before you commit the
fix. Then commit, push `main`, deploy again, and
run the script again under Monitor. It recomputes the same version from the newest tag and runs the
whole suite again, which is the check that counts.

A failure in `D47.App.Tests` that appears only in this run, with d47 started again by the deploy,
may be the app competing for the machine: the Avalonia headless tests share one dispatcher thread.
Report it rather than calling it fixed.

### The runner failed

The watcher prints the failing step in red and `failure after …`. Read the log:

```bash
gh run view <id> --log-failed
```

- **Build or Test.** The tag does not exist yet. Confirm with
  `git ls-remote --tags origin v<version>` (empty output). Send the close notice, restore, fix it as above, push, deploy
  again and run the script again: it dispatches the same version.
- **Publish, the installer, packing or checksums** — before "Publish the release". Still no tag.
  When the cause is in the repository, send the close notice, restore and fix it the same way. When it is the runner (a
  download or network failure, `choco` failing to install), keep the deploy, re-run the job once with
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
committed leaves the restored install in place; it gets the release through the updater once the
release is cut.

## Pushing and the local install

The maintainer asked for this skill to fix and continue, so pushing a fix to `main` is part of the
job and needs no question. Push `main` only: no tags, no other branch, never `--force`. Every push
also ships any local commits this session did not write; name them in the report.

The invocation also authorizes `deploy-local.ps1` to close the installed d47 and to write
`%LOCALAPPDATA%\Programs\d47`, its `data\`, `%LOCALAPPDATA%\d47-release-build` and
`%LOCALAPPDATA%\d47-release-backup`. Nothing else outside the checkout.

## Output

The report's first line is a level-two heading that says whether the release succeeded. ✅ is the
green mark and ❌ the red one:

```
## ✅ Release v<version> succeeded
## ❌ Release v<version> failed at <stage>
## ❌ Release v<version> is live and failed its <comparison | Defender scan>
```

The success heading goes out only when the release is published and both checks that follow it
passed or did not run: `release.ps1` exited 0, or, after a re-arm, the watcher reported `success`
and the comparison and the Defender scan found nothing. A difference from the rebuild (exit 4) or a
Defender detection (exit 3) gets the third heading. Every other stop gets the second, naming the
stage: the local suite, the runner's step, the avatar upload, the dispatch, or the script's own
checks. No other line in the report is a heading.

When the script exits 0:

1. One line: the version published, the release URL
   (`https://github.com/dseelinger/d47/releases/tag/v<version>`), and the total time.
2. One line on the local install: `deployed v<version>`, or `restored to v<previous>` with the
   `replaced-data-…` path, or an unreleased version left installed with the restore command.
3. The fixes, if there were any: a table of stage, test or step, the cause in a clause, and the
   commit subject.
4. Anything still open: a test that failed only in the full run, a docs job that failed, a fix you
   could not make.

When it stops without a release, the failure heading names the stage it stopped at. Then come the
install line and the same table.

## What this does not do

It never runs `-Major`, never creates or moves a tag by hand, never renumbers a `CHANGELOG.md`
heading, and never weakens a test to get a release out. `/deck patch` and `/deck minor` run the same
`release.ps1` from the Launcher session without deploying locally or fixing anything.
