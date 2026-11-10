---
name: triage-and-implement
description: Run /triage lanes, then work every lane without the maintainer — each issue its own session started from here on the model and effort the grid gives it, running /issue-worker in its lane's worktree, the next issue in a lane started once the one before it has merged into main. Sessions skip /test-drive and push without waiting for a manual test, and speak only when something stops them. Use when the user invokes /triage-and-implement, /triage-and-implement <N> for N lanes, or says "triage and implement", "triage and run the lanes", "work the lanes without me".
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

No lane session starts in the main checkout. Ready the lane's folder first, as the Issue key and
`tools/deck/run-queue.ps1` do: `.claude/worktrees/lane-<letter>`, kept from one issue to the next
and switched to branch `issue/<N>`, from local `main` so it holds what other lanes merged and have
not pushed. An existing branch is reused, which is how a paused issue keeps its work:

```bash
M="$(git worktree list | head -1 | cut -d' ' -f1)"; python "$M/tools/deck/lane_folder.py" prepare <letter> <N>
```

It prints the folder. If it refuses, start nothing in that lane and give its reason in the report.

The desktop app asks the maintainer to trust a folder the first time a session starts in it, and
can only ask while this conversation is on screen. One folder per lane means one prompt per lane,
ever, and none once the lane letters have been used.

Then start the session inside it with `start_session`, `initiation: "user_asked"`:

- `context: "fresh"`, `cwd`: the lane folder's absolute path, `use_worktree: false`, so the
  session works in that folder as it is. `/issue-worker` sees its top level is
  `.claude/worktrees/lane-<letter>` on `issue/<N>` and works there, as when the Issue key starts it. Do not let the app create the worktree: its folder
  and branch are not the ones `/issue-worker` and the merge steps look for.
- `model`: the grid's `opus`, `sonnet` or `haiku` as `claude-opus-5-5`, `claude-sonnet-5-5` or
  `claude-haiku-5-5`. `effort`: the grid's token as it is.
- `name`: `issue-<N>`. `title`: `Lane <letter>: #<N> <grid title>`.
- `wake_when_done: true`.
- `prompt`: the block below, with the number, lane letter and main checkout path filled in.
- `background`: that this is lane `<letter>` of a `/triage lanes` run started by
  `/triage-and-implement`, that other lanes are merging into `main` at the same time, and that
  nobody is at the keyboard to test.

```
/issue-worker <N>

This issue is in lane <letter> of a lanes run that works without the maintainer.
- This session started inside its lane's worktree, <main checkout>/.claude/worktrees/lane-<letter>, on branch issue/<N>. Work there and merge into main as /issue-worker's Lanes section says. Never edit the main checkout.
- Skip /test-drive. Do not wait for a manual test. Once the change is committed, merged into main and pushed, finish the turn. Still write the test steps in the report, so they can be run later.
- Speak through /claude-voice only when something stops you from finishing: a question only the maintainer can answer, a rebase conflict, a build or test failure you cannot fix, a denied permission. Say the issue number and what you need.
- Do not speak the done sentence when the issue lands.
```

Lanes run at the same time, so start every lane's first issue before doing anything else.

## 4. Wait for the sessions, with nothing running in the background

End the turn once every lane's first issue has started. The app continues this session with a
side-sessions event when every session it started has ended its turn.

The app refuses `start_session` on a turn that a background task, a scheduled wakeup or another
session's message started. The one exception is a side-sessions event that directly follows the
maintainer's own turn, or another side-sessions event. So while lanes run, start no background
command, `Monitor`, `ScheduleWakeup` or watcher loop. Its notification takes the next turn, that turn
cannot start a session, and the lane stalls until the maintainer types. The cost is that a lane's
next issue waits until the slowest running session has ended its turn.

## 5. When this session is continued

Check each running issue against `main` and against its session's state and latest note from
`list_sessions` with `{"linked": true}`:

- **Merged** (a `Fixes #N` line on `main`): start the lane's next issue as step 3 says. A lane with
  no issue left is finished.
- **Merged, and `start_session` is refused anyway**: never ask the maintainer whether to start it.
  Hand the lane's remaining issues to `tools/deck/run-queue.ps1` in a console of its own, and say so
  in one line. It works the rest of the lane headless, in the same lane folder, skipping issues that
  have already landed, and pushes nothing. Run it through the PowerShell tool. The `env:` line keeps
  the queue's sessions from inheriting this session's identity, as `run-lanes.ps1` does:

  ```powershell
  $M = (git worktree list | Select-Object -First 1).Split(' ')[0]
  Get-ChildItem env: | Where-Object { $_.Name -like 'CLAUDE*' -or $_.Name -like 'MCP_*' -or $_.Name -eq 'ANTHROPIC_BASE_URL' } | ForEach-Object { Remove-Item "env:$($_.Name)" }
  Start-Process powershell -WorkingDirectory $M -ArgumentList '-NoExit','-NoProfile','-ExecutionPolicy','Bypass','-File','tools\deck\run-queue.ps1','-Lane','<letter>','-From','<N>'
  ```

  That lane is then the queue's. Start nothing more in it from here, and read its results from
  `needs-you.md` in the folder `%LOCALAPPDATA%\Temp\d47-queue\` that the queue's console names.
- **Not merged, and its session is waiting for an answer**: the session has already spoken its
  question. Leave it; do not speak about it again.
- **Not merged, and its session has ended its turn with a failure banner**: the lane is paused, as
  `/issue-worker` defines it. Start nothing more in that lane and keep the others running. Speak one
  sentence through `/claude-voice` naming the lane, the issue and what stopped it.

Then end the turn again, as step 4 says.

Never ask whether to start a lane's next issue, in text, in the wrap-up or through the voice. The
maintainer invoking this skill is the instruction to start every issue in every lane.

When every lane is finished or paused, speak once through `/claude-voice` saying so, and write the
report: one row per lane with the issues merged, the issue it paused on and why, and the commits by
hash and subject, each marked pushed or not pushed. A lane the queue finished has its commits on
`main` unpushed.

## What this does not do

It never edits files, merges, pushes or resolves a conflict itself; the lane sessions and the queue
do that through `/issue-worker`. The lane folders it readies are the only thing it adds to the
repository. It does not resume a paused lane: the maintainer does that with `/issue-worker <N>`.
