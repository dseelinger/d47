---
name: triage-and-implement
description: Run /triage lanes, then work every lane without the maintainer — each issue its own session started from here on the model and effort the grid gives it, running /issue-worker in its own worktree, the next issue in a lane started once the one before it has merged into main. Sessions skip /test-drive and push without waiting for a manual test, and speak only when something stops them. Use when the user invokes /triage-and-implement, /triage-and-implement <N> for N lanes, or says "triage and implement", "triage and run the lanes", "work the lanes without me".
---

# Triage and implement

`/triage lanes` and then the lanes themselves, started from this session. Everything `/triage` and
`/issue-worker` say still holds; this file covers only what differs.

## 1. Triage

Invoke `/triage lanes`, or `/triage lanes <N>` when this skill was given a number. Follow it in full:
the report, the voice, and the grid written to `.claude/triage-state.json` in the main checkout.

`/triage` says it starts no work. This skill is the instruction to start it, so carry on to step 2
in the same turn without asking.

## 2. Check what this session can start

A session started from here runs on this session's model and effort or lower; a higher model or
effort is ignored without an error. There is also no way to pass the Opus advisor.

Compare every row in the grid with this session's model and effort. Under the report, list in one
line each the issues that will run below their grid model or effort, and the issues whose row has
`"advisor": "opus"`, which run without it. Then go on: none of these is a reason to stop.

## 3. Start each lane's first issue

Read the `lanes` object from `.claude/triage-state.json`. For each lane, its first issue with no
`Fixes #N` line on `main`:

```bash
git log main --format=%B | grep -qx "Fixes #<N>" && echo merged || echo open
```

Start it with `start_session`, `initiation: "user_asked"`:

- `context: "fresh"`, `use_worktree: false`. The session starts in the main checkout, and
  `/issue-worker` creates `.claude/worktrees/<N>` from local `main` and moves into it, as its
  **Lanes** section says. A worktree the app creates is not the one `/issue-worker` looks for.
- `model`: the grid's `opus`, `sonnet` or `haiku` as `claude-opus-5-5`, `claude-sonnet-5-5` or
  `claude-haiku-5-5`. `effort`: the grid's token as it is.
- `name`: `issue-<N>`. `title`: `Lane <letter>: #<N> <grid title>`.
- `prompt`: the block below, with the number filled in.
- `background`: that this is lane `<letter>` of a `/triage lanes` run started by
  `/triage-and-implement`, that other lanes are merging into `main` at the same time, and that
  nobody is at the keyboard to test.

```
/issue-worker <N>

This issue is in lane <letter> of a lanes run that works without the maintainer.
- Skip /test-drive. Do not wait for a manual test. Once the change is committed, merged into main and pushed, finish the turn. Still write the test steps in the report, so they can be run later.
- Speak through /claude-voice only when something stops you from finishing: a question only the maintainer can answer, a rebase conflict, a build or test failure you cannot fix, a denied permission. Say the issue number and what you need.
- Do not speak the done sentence when the issue lands.
```

Lanes run at the same time, so start every lane's first issue before doing anything else.

## 4. Watch for merges

The app continues this session only when every session it started has ended its turn, which would
hold every lane back until the slowest one finishes. So also run a watcher in the background with
the numbers of the issues now running. It exits when one of them has merged, which continues this
session:

```bash
cd "$(git worktree list | head -1 | cut -d' ' -f1)"
while :; do for n in <running numbers>; do
  git log main --format=%B | grep -qx "Fixes #$n" && { echo "merged $n"; exit 0; }
done; sleep 30; done
```

Keep exactly one watcher running. Before starting a new one, stop the old one.

## 5. When this session is continued

Whether by the watcher or by the app, check each running issue against `main` and against its
session's state and latest note from `list_sessions` with `{"linked": true}`:

- **Merged** (a `Fixes #N` line on `main`): start the lane's next issue as step 3 says. A lane with
  no issue left is finished.
- **Not merged, and its session is waiting for an answer**: the session has already spoken its
  question. Leave it; do not speak about it again.
- **Not merged, and its session has ended its turn with a failure banner**: the lane is paused, as
  `/issue-worker` defines it. Start nothing more in that lane and keep the others running. Speak one
  sentence through `/claude-voice` naming the lane, the issue and what stopped it.

Then start a new watcher for the issues still running.

When every lane is finished or paused, speak once through `/claude-voice` saying so, and write the
report: one row per lane with the issues merged, the issue it paused on and why, and the pushed
commits by hash and subject.

## What this does not do

It never edits the main checkout, merges, pushes or resolves a conflict itself; the lane sessions
do that through `/issue-worker`. It does not resume a paused lane: the maintainer does that with
`/issue-worker <N>`.
