---
name: triage-and-implement
description: Run /triage lanes, then work every lane without the maintainer — each issue a background subagent started from here on the model and effort the grid gives it, running /issue-worker in its lane's worktree, and the lane's next issue started as soon as the one before it has merged into main. Subagents do no manual testing and speak only when something stops them. Use when the user invokes /triage-and-implement, /triage-and-implement <N> for N lanes, or says "triage and implement", "triage and run the lanes", "work the lanes without me".
---

# Triage and implement

`/triage lanes` and then the lanes themselves, run from this session. Everything `/triage` and
`/issue-worker` say still holds; this file covers only what differs.

Each issue runs as a background subagent of this session, not as a session of its own. A
subagent's completion notice starts a turn here, and that turn can start the lane's next issue
straight away. A session started with `start_session` cannot do this: the app wakes the session
that started it only once every session it started has ended its turn, so each lane would wait for
the slowest one.

## 1. Triage

Invoke `/triage lanes`, or `/triage lanes <N>` when this skill was given a number. Follow it in full:
the report, the voice, and the grid written to `.claude/triage-state.json` in the main checkout.

`/triage` says it starts no work. This skill is the instruction to start it, so carry on to step 2
in the same turn without asking.

## 2. Check the advisor

A subagent cannot have the Opus advisor. Under the report, list in one line the issues whose row
has `"advisor": "opus"`; they run without it. Then go on: this is not a reason to stop.

## 3. Start each lane's first issue

Read the `lanes` object from `.claude/triage-state.json`. For each lane, its first issue with no
`Fixes #N` line on `main`:

```bash
git log main --format=%B | grep -qx "Fixes #<N>" && echo merged || echo open
```

Ready the lane's folder first, as the Issue key and `tools/deck/run-queue.ps1` do:
`.claude/worktrees/lane-<letter>`, kept from one issue to the next and switched to branch
`issue/<N>`, from local `main` so it holds what other lanes merged and have not pushed. An existing
branch is reused, which is how a paused issue keeps its work:

```bash
M="$(git worktree list | head -1 | cut -d' ' -f1)"; python "$M/tools/deck/lane_folder.py" prepare <letter> <N>
```

It prints the folder. If it refuses, start nothing in that lane and give its reason in the report.

Then start the issue with the `Agent` tool:

- `subagent_type`: `general-purpose`. It needs `Skill`, `Bash`, `Edit` and `git`.
- `model`: the grid's `opus`, `sonnet` or `haiku` as it is. `effort`: the grid's token as it is.
  This skill asks for both, which is what the tool requires before `effort` may be set.
- No `isolation`. The lane folder is the worktree; one the tool creates is not the folder or branch
  `/issue-worker` and the merge steps look for.
- `description`: `Lane <letter>: #<N>`.
- `prompt`: the block below, with the number, lane letter, lane folder and main checkout filled in.

```
Run /issue-worker <N> to land issue #<N>. If the Skill tool is not available to you, read <main checkout>/.claude/skills/issue-worker/SKILL.md and follow it.

This issue is in lane <letter> of a lanes run that works without the maintainer. Other lanes run at the same time and merge into main too.

Where to work:
- Work only in the lane folder, <lane folder>, which is on branch issue/<N>. Never edit the main checkout, <main checkout>.
- Your shell starts in the main checkout, and cd does not carry from one Bash call to the next. Start every Bash and PowerShell command with a cd to the lane folder, or use git -C <lane folder>. Give Read, Edit, Write, Grep and Glob absolute paths inside the lane folder.
- /issue-worker's checkout check (git rev-parse --show-toplevel), run from the lane folder, finds it on issue/<N>: work there and merge into main as its Lanes section says. Never call EnterWorktree or ExitWorktree.
- Where /issue-worker says to delegate to an agent and you cannot, do the step yourself.

How to finish:
- There is no manual testing in this run, and these lines override /issue-worker's **Saying how to test it** and **Finishing**. Do not run /test-drive, do not write manual test steps, and do not ask for screenshots, /desktop or a device. Headless captures for your own checking are fine; do not send them.
- Nobody can answer a question during the run. Where /issue-worker says to stop and ask, stop: your final message opens with its failure banner and states the question.
- Once the change is committed, merged into main and pushed, stop. Your final message is the report /issue-worker's Finishing describes, from its completion line or failure banner on, without test steps. You have no wrap-up or plan tools; do not look for them.
- Speak through /claude-voice only when something stops you from finishing: a question only the maintainer can answer, a rebase conflict, a build or test failure you cannot fix, a denied permission. Say the issue number and what you need.
- Do not speak the done sentence when the issue lands.
```

Lanes run at the same time, so start every lane's first issue before doing anything else.

## 4. Wait for the notices

End the turn once every lane's first issue has started. Each subagent's completion notice starts a
new turn here. Start no `Monitor`, `ScheduleWakeup` or watcher loop: the notices are the signal.

## 5. When a subagent finishes

Check its issue against `main` and read the subagent's final message:

- **Merged** (a `Fixes #N` line on `main`): ready the lane's folder for its next issue and start it,
  as step 3 says. A lane with no issue left is finished.
- **Not merged**: the lane is paused, as `/issue-worker` defines it. Start nothing more in that
  lane and keep the others running. Speak one sentence through `/claude-voice` naming the lane, the
  issue and what stopped it, unless the subagent already spoke it.

Then end the turn again, as step 4 says.

Never ask whether to start a lane's next issue, in text, in the wrap-up or through the voice. The
maintainer invoking this skill is the instruction to start every issue in every lane.

When every lane is finished or paused, speak once through `/claude-voice` saying so, and write the
report: one row per lane with the issues merged, the issue it paused on and why, and the commits by
hash and subject, each marked pushed or not pushed.

## What this does not do

It never edits files, merges, pushes or resolves a conflict itself; the subagents do that through
`/issue-worker`. The lane folders it readies are the only thing it adds to the repository. It does
not resume a paused lane: the maintainer does that with `/issue-worker <N>`.
