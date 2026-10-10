---
name: pre-release
description: Get main ready for a release — clear what finished lanes left, push main, and report which preconditions of tools/release.ps1 still fail. Runs no tests; tags and dispatches nothing. Use when the user invokes /pre-release, or says "get main ready to release", "push main for the release", "get this ready to cut".
---

# Pre-release

You get `main` onto `origin/main` before `tools\release.ps1` runs. What you deliver is
`origin/main` carrying every local commit, and a straight answer on whether the release will be
refused for some other reason. The whole suite runs in `tools\release.ps1`, not here.

`/pre-release` carries no argument. Start in the turn it arrives in.

## Turn the voice on first

Before the first step, run `/claude-voice Pre-release`. It is a default, not a fixture:
`/claude-voice off` stops it and the run carries on unchanged.

## No lane still running

Before anything else, run `git worktree list`. A worktree under `.claude/worktrees/` is a lane
working an issue. Its merge would land after the push and miss the release. Name each one and ask
whether to wait; start nothing until the maintainer answers.

## Clear what finished lanes left

Once no lane is running, remove what `/issue-worker` leaves behind after a merge. Do it without
asking.

```bash
git worktree prune
git branch --list 'issue/*'
ls -A .claude/worktrees
```

- **Folders.** A folder under `.claude/worktrees/` that `git worktree list` does not show is a
  leftover. Usually it holds copies of tracked files that a lock kept from being removed. Compare
  each file with the main checkout, ignoring line endings
  (`diff -q --strip-trailing-cr <file> <main copy>`). Delete the folder if every file matches or is
  an older version of a file that `main` has since changed. If a file exists only in the folder,
  keep the folder, name the file and ask.
- **Branches.** Delete merged `issue/*` branches with `git branch -d`, which refuses an unmerged
  one. Report an unmerged branch and leave it. Do not touch branches with other names.

## Push once, at the end

The workflow builds `origin/main`, so a commit still sitting locally does not ship. Push:

```bash
git push origin main
```

Push `main` and nothing else — no tags, no other branch. The push is also what closes any issue
whose commit carries a `Fixes` trailer, so list what went by subject, including commits this
session did not write; they are going out under the same version.

Local `issue-worker` commits go out with the push. Do not ask whether their reviews have run.

## What else refuses the release

`tools\release.ps1` throws before it builds anything when any of these is false. Check them after
the push, and report only the ones that still fail:

- The working tree is clean. `git status --porcelain` lists untracked files too, so a stray
  directory beside the source refuses the release exactly as an uncommitted edit does. Name it;
  do not commit it to make the check pass, and do not add it to `.gitignore` uninvited.
- The branch is `main`.
- `HEAD` matches `origin/main`. Fetch rather than assume. After the push this holds; when it does
  not, say what is behind and why.
- A `v*` tag exists and is `vX.Y.Z`.

```bash
git status --porcelain; git rev-parse --abbrev-ref HEAD
git fetch origin main --tags --quiet && git rev-list --count origin/main..HEAD
```

## The worker is not in this suite

`worker/` carries a `node --test` suite that is deliberately outside `dotnet test`, and the
release workflow never builds it. Leave it alone unless commits since the newest tag touched it:

```bash
git diff --name-only $(git describe --tags --abbrev=0 --match 'v*')..HEAD -- worker/
```

If they did, run `node --test` from `worker/` and report the result as a separate line. Deploying
it is `wrangler deploy` and is the maintainer's.

## Output

Short, and in this order:

1. What was pushed: the commit subjects, or one line saying the branch was already current.
2. The preconditions that are still false, one line each. Nothing when they all hold.
   Then one line for the lane leftovers removed, if there were any, and each one kept and why.
3. The release line:

   ```
   tools\release.ps1 -Patch
   ```

   Take the increment from the unreleased `CHANGELOG.md` headings — a capability added or removed
   makes it `-Minor`. A wrong increment is cheap for the maintainer to correct.

No preamble. He ran this to find out whether he can cut.

## What this does not do

It runs no tests, dispatches nothing, tags nothing, renumbers no changelog heading and chooses no
version. It clears finished lanes, pushes `main`, and says where the tree stands. Cutting the
release is `tools\release.ps1`, and it is the maintainer's.
