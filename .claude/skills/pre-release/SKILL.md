---
name: pre-release
description: Run the release gate before a release is cut — the whole suite in Release configuration, exactly as tools/release.ps1 runs it — fix what fails, and push main once it is green. Tags and dispatches nothing. Use when the user invokes /pre-release, or says "check the suite before I release", "is the tree green", "run the full suite", "get this ready to cut".
---

# Pre-release

You run the gate before `tools\release.ps1` runs it, so a failure is found here rather than
several minutes into a dispatch. What you deliver is a green suite, the fixes that made it green,
`origin/main` carrying them, and a straight answer on whether the release will be refused for some
other reason.

`/pre-release` carries no argument. Start the run in the turn it arrives in.

## Turn the voice on first

Before the first build, run `/neural-voice Pre-release`. The suite takes minutes and the
maintainer will be doing something else while it runs. It is a default, not a fixture:
`/neural-voice off` stops it and the run carries on unchanged.

## The gate is one command

```bash
dotnet test d47.slnx -c Release --nologo
```

That is the line inside `tools\release.ps1`: same solution, same configuration. Run it — not a
filtered subset, not Debug, not `--no-build`. This run is worth something only because it is
identical to the one that decides the release.

The workflow then runs every test project except `D47.App.Tests` again on the runner, before the
tag exists. `D47.App.Tests` is checked here and nowhere else, so a local run that skips it is not
the gate.

Run it in the background and start nothing else against the tree while it is out — a second build
contends for the same output directory.

## Three kinds of failure

They are not fixed the same way, and the report should not merge them.

- **A build break.** `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are on, so a warning
  ends the command before a single test runs. Fix the code. There is no `#pragma warning disable`
  or `SuppressMessage` in `src/`; adding the first one needs the maintainer's agreement, and a
  release waiting on it is not that agreement.
- **A gate test.** `CoreDependencyTests`, `DocumentationGateTests` and the other whole-tree checks
  fail because the tree broke a rule, usually somewhere the change did not look. Satisfy the rule.
  Editing the gate so it accepts the tree is the one move never to make here — it removes the
  check that a release is the last chance to run.
- **An ordinary test.** Decide whether the test or the code is wrong. Default to the code.

## Never fix a test by weakening it

Deleting it, skipping it, loosening an assertion until it passes or widening a tolerance is not a
fix. It ships the defect, and a release is where the defect reaches Commanders. If a test really
does make a wrong claim, name the claim and say why it is wrong before you touch it.

Run each failure by itself before diagnosing it:

```bash
dotnet test tests/D47.Core.Tests -c Release --filter FullyQualifiedName~<Name>
```

A test that fails in the suite and passes alone is shared state or ordering. That is a finding,
not noise, and it will fail again on the runner at a moment nobody chose. Report it. Do not call
it green.

## Work the loop small

After a fix, re-run the one project rather than the solution:

```bash
dotnet test tests/D47.App.Tests -c Release
```

Delegate that run to `test-runner` and a fix whose cause and fix are already named to
`implementer` — one implementer at a time, and never a second agent editing alongside it. The
diagnosis stays yours.

The solution run comes back once, at the end, and only that run counts. A green project is not a
green suite.

## Commit the fixes

The repository's form: imperative, sentence case, the issue number in parentheses when the commit
closes one, and the `Co-Authored-By` trailer. A fix that changes what a user sees, hears or can do
gets a `CHANGELOG.md` entry in the same commit, folded into the current unreleased heading; a
test-only or tooling fix gets none.

## Push once, at the end

The workflow builds `origin/main`, so a commit still sitting locally is a fix that does not ship.
Once the solution run is green and every fix is committed, push:

```bash
git push origin main
```

The order is not negotiable: green first, then push. A push before the final run puts an untested
commit on the branch the release builds. If the suite is not green, push nothing and say what is
holding it.

Push `main` and nothing else — no tags, no other branch. The push is also what closes any issue
whose commit carries a `Fixes` trailer, so list what went by subject, including commits this
session did not write; they are going out under the same version.

Local `issue-worker` commits go out with the push. Do not ask whether their reviews have run: a
green suite is the go-ahead, and the push happens without a question.

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

1. One line: green or not, and how long the suite took.
2. A failures table, only when there were failures — project, test, the cause in a clause, and
   what you did about it. Anything you could not fix is a row too, and says so.
3. What was pushed: the commit subjects, or one line saying the branch was already current. Say
   plainly when nothing was pushed because the suite was red.
4. The preconditions that are still false, one line each. Nothing when they all hold.
5. The release line, when the suite is green:

   ```
   tools\release.ps1 -Patch
   ```

   Take the increment from the unreleased `CHANGELOG.md` headings — a capability added or removed
   makes it `-Minor`. A wrong increment is cheap for the maintainer to correct. A wrong claim of
   green is not, so never round a run up.

No preamble and no description of what the suite is. He ran this to find out whether he can cut.

## What this does not do

It dispatches nothing, tags nothing, renumbers no changelog heading and chooses no version. It
runs the gate, fixes what it can, pushes `main` when the suite is green, and says where the tree
stands. Cutting the release is `tools\release.ps1`, and it is the maintainer's.
