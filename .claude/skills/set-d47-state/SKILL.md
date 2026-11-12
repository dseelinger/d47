---
name: set-d47-state
description: Set state in the maintainer's installed d47 so a callout can be heard again — for testing, retesting and recording. Takes a command; the first is bgs-tick, which makes the next start say the BGS has ticked. Stops any running d47, edits view-state.json with tools/d47-state/set-state.ps1, and starts it again. Use when the user invokes /set-d47-state &lt;command&gt;, or says "reset the BGS tick callout", "make d47 say the tick again", "set d47 state".
---

# Set d47 state

`tools/d47-state/set-state.ps1` edits `%LOCALAPPDATA%\Programs\d47\data\view-state.json`. d47 reads
that file once at startup and writes it while running, so the script stops every running d47 first
(`Stop-Process -Force`, as `tools/rec-on.ps1` does) and starts the same executables again afterwards.
Debug, test-drive and local-release builds share this data folder, so the command applies to whichever
one is run next.

Invoking this skill is the maintainer's go-ahead to write to the installed `data\` folder and to stop
and restart d47. Do not ask again.

## The argument

The first word is the command; the rest is passed to it.

| Command | Argument | Effect |
|---|---|---|
| `bgs-tick` | none, or minutes 0–59 (default 2) | Holds a BGS tick that many minutes ago and clears the record of having said it. The next start says "The BGS has ticked." up to 15 minutes, otherwise "The BGS ticked N minutes ago." (rounded to five). |
| `bgs-tick` | `clear` | Removes both BGS tick fields. d47 fetches the real tick and records it; it speaks only if that tick is under an hour old. |

No command, or one not in the table: list the commands and ask which one.

The BGS tick is galaxy-wide and the fields are not per Commander, so a Commander name in the request
(for example "for John DeParagon") changes nothing in the run. The callout speaks once d47 has caught
up on the journal; the game needs to be running only if no tick was stored, which `bgs-tick` always
stores.

## The run

```bash
pwsh -NoProfile -File tools/d47-state/set-state.ps1 bgs-tick 2
```

It prints the pids it stopped, what it set, and which executables it started. Each `bgs-tick` run is
one take: the callout speaks once per start, then records the tick as said. Run it again for the next
take.

A faked tick may be left in place. The real tick d47 fetches later is older than it and is ignored;
the next real tick is accepted. If that next tick comes within an hour of the fake, it is not spoken.
Use `bgs-tick clear` to go back to the real tick.

## Adding a command

Add a function to the script, register it in `$handlers`, and add a row to the table above. A new
command edits only fields it names and says what the next start will do.

## The report

One or two lines: what was set, what d47 will say, and whether it was restarted.
