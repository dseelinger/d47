---
name: release
description: Cut a patch or minor release with tools/release.ps1, relay its progress line by line as the script prints it — the version, the suite, the avatar clips, the dispatch and every workflow step — and when the build or a test fails, locally or on the runner, fix it, push the fix and run the release again. Use when the user invokes /release, /release patch or /release minor, or says "run the release", "release a patch and fix what breaks".
---

# Release

You run `tools\release.ps1` and stay with it until the version is published. The script does the
work; you show its progress to the maintainer as it happens, and when a build or test fails you
fix the cause and start the release again. What you deliver is a published release, or a plain
statement of what is stopping it.

## The argument

- `patch` runs `-Patch`, `minor` runs `-Minor`.
- No argument: ask with AskUserQuestion, Patch or Minor, before anything else runs. The Stream
  Deck's Release key starts the session this way.
- `major`, or anything else: refuse. A major release is cut at the desk with the script.

The invocation is the confirmation. Do not ask again before running the script.

## Turn the voice on first

Run `/claude-voice Release`. The release takes about twenty minutes and the maintainer will be
doing something else. `/claude-voice off` stops it and the release carries on.

## Before the script

Run `git worktree list`. A worktree under `.claude/worktrees/` is a lane still working an issue;
name each one and ask whether to wait. Start nothing until the maintainer answers.

Then put the branch where the script needs it:

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
times stay aligned, with nothing added except a one-line note when a step failed. The stages, in
order:

1. `v1.33.3 -> v1.33.4` — the version this run will produce.
2. `Running the suite...`, then one `Passed!` or `Failed!` line per test project (about two minutes).
3. `Publishing avatar clips...` and what it uploaded or skipped.
4. `Dispatched v…`, the run URL, then each workflow step with its time as it finishes.
5. `success in …` or `failure after …`, then `release.ps1 exited N`.

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

Then commit, push `main`, and run the script again under Monitor. It recomputes the same version
from the newest tag and runs the whole suite again, which is the check that counts.

### The runner failed

The watcher prints the failing step in red and `failure after …`. Read the log:

```bash
gh run view <id> --log-failed
```

- **Build or Test.** The tag does not exist yet. Confirm with
  `git ls-remote --tags origin v<version>` (empty output). Fix it as above, push, and run the
  script again: it dispatches the same version.
- **Publish, the installer, packing or checksums** — before "Publish the release". Still no tag.
  When the cause is in the repository, fix it the same way. When it is the runner (a download or
  network failure, `choco` failing to install), re-run the job once with `gh run rerun <id> --failed`
  and re-arm the watcher on the same id.
- **"Publish the release" or the docs job.** The tag may already exist, and the workflow refuses a
  version whose tag exists. Report what failed and stop. Re-deploying the docs
  (`gh workflow run release.yml -f version=<v> -f docs_only=true`) is the maintainer's decision.

### Anything else

A refusal from the script's own checks, a `publish-avatars.ps1` error (ffmpeg, upload), a dispatch
failure or a `gh` sign-in problem is not a build or test failure. Report it with the message the
script printed and stop.

### When to stop

Stop and report when the same failure survives a fix, or after three runs of the script. Say what
failed, what you changed, and what is pushed.

## Pushing

The maintainer asked for this skill to fix and continue, so pushing a fix to `main` is part of the
job and needs no question. Push `main` only: no tags, no other branch, never `--force`. Every push
also ships any local commits this session did not write; name them in the report.

## Output

When the script exits 0:

1. One line: the version published, the release URL
   (`https://github.com/dseelinger/d47/releases/tag/v<version>`), and the total time.
2. The fixes, if there were any: a table of stage, test or step, the cause in a clause, and the
   commit subject.
3. Anything still open: a test that failed only in the full run, a docs job that failed, a fix you
   could not make.

When it stops without a release, lead with that and the stage it stopped at, then the same table.

## What this does not do

It never runs `-Major`, never creates or moves a tag by hand, never renumbers a `CHANGELOG.md`
heading, and never weakens a test to get a release out. `/deck patch` and `/deck minor` run the same
script from the Launcher session without fixing anything.
