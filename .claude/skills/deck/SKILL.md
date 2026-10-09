---
name: deck
description: Start the maintainer's deck sessions and releases from the phone, through the Launcher session — triage, architect, an issue worker, pre-release, or a patch or minor release. Each session opens in its own console on the PC with the settings its Stream Deck key uses, and shows up in the Claude app's Code tab. Use when the user invokes /deck <name>, or says "start triage", "open the architect", "work issue N", "run pre-release", "cut a patch", "cut a minor".
---

# Deck

The phone counterpart of the desk Stream Deck. The maintainer runs this in the **Launcher**
session (`tools\deck\launcher.cmd`, the Launcher key) and reaches it from the Claude app.

## Starting a session

Run from the repo root, with the Bash tool:

| Argument | Command |
| --- | --- |
| `triage` | `cmd.exe //c 'tools\deck\spawn.cmd' Triage triage.cmd` |
| `architect` | `cmd.exe //c 'tools\deck\spawn.cmd' Architect architect.cmd` |
| `issue <N>` or `issue <lane>` | `cmd.exe //c 'tools\deck\spawn.cmd' Issue issue-worker.cmd <N>` |
| `pre-release` | `cmd.exe //c 'tools\deck\spawn.cmd' Pre-release pre-release.cmd` |

`spawn.cmd` clears this session's `CLAUDE*` variables and opens the launcher in a new console, so
the new session is separate and registers with Remote Control on its own. It returns at once.

For `issue`, the argument must be a number or a single letter. Without one, ask for it — the
console on the PC cannot be answered from the phone. Before spawning, run
`python tools/deck/issue_settings.py <arg>` and report its stderr line (which issue, which model
and effort, how old the triage is). If it prints nothing on stdout, report why and spawn nothing.

Then say the session is starting and will appear in the Code tab as its name (`Triage`,
`Architect`, `#<N>`, `Pre-release`) within a few seconds.

## Cutting a release

`patch` and `minor` run here, so the output reaches the phone:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/release.ps1 -Patch
```

`-Minor` for a minor. Never `-Major` — that stays a deliberate act at the desk.

- Confirm first: say the release type and the version it will produce (newest `v*` tag, bumped),
  and wait for a yes. Pushing the tag publishes it.
- Run it in the background — the suite takes longer than a foreground command may run — and
  report when it ends: the version dispatched, or the error it threw.
- The script refuses a dirty tree, a branch other than main, or HEAD different from origin/main.
  Report the refusal; do not commit, stash or push to get past it. `/deck pre-release` is the step
  that pushes.

## Anything else

For an argument not listed, list the six and do nothing.
