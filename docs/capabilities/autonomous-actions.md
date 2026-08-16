---
title: Acting on its own
group: Acting on the game
nav_order: 127
---

Things Directive 47 does to your ship without being asked. There is one so far, and it is off.

## Why this is its own page

Everything else Directive 47 does happens because you said something. The sentence you said is
the permission — you asked for the gear, so the gear is what it presses.

An action that fires on a journal event has no sentence behind it. Nobody asked, nobody is
watching for it, and if it goes wrong at the wrong moment you find out from the game rather than
from Directive 47. That is a different kind of thing to agree to, so it gets a different kind of
agreement: **each one is off by default and switched on individually**, and none of them can be
switched on by the AI.

One switch per action, not one for the category. If there were a single "act on your own" toggle,
the next thing added here would arrive already enabled for everyone who wanted the first one —
and that is permission for one thing being spent on another.

## The arrival honk

Fires the discovery scanner after each hyperspace jump.

Elite has no "honk" binding, because the discovery scanner is not a button — it is a fire-group
weapon. It goes off when you hold primary fire in analysis mode with the scanner in your current
fire group. So this holds **your** fire button, for the six seconds the scan takes.

Three things have to be true, and it tells you when they are not:

```text
I did not honk: the discovery scanner only fires in analysis mode, and you are in combat mode.
```

- **Analysis mode.** Switching you into it would be a second thing acting on its own, wearing
  the first one's permission, so it says this instead.
- **The scanner in your current fire group.** Directive 47 cannot see your fire groups. If the
  honk seems to do nothing, this is the reason.
- **A fire button it can press.** On Elite's default keyboard preset that is `Mouse_1`, which
  works. On a stick it does not, and you get told which device it is on.

It arms on the jump and fires once you are actually in normal space, because during the
witchspace tunnel the game has the controls and a held button goes nowhere. If thirty seconds
pass without that happening — you dropped straight into supercruise and left — the arm expires
rather than waiting to surprise you later.

It fires once per jump. It never fires for jumps that happened before Directive 47 started, which
would otherwise mean a honk for every system you visited that afternoon.

## Turning it on

> "turn on the arrival honk"
> "stop honking on arrival"

Or the **Honk on arriving in a system** row in settings. Like every switch that reaches your
keyboard, the AI cannot touch it — it can tell you whether it is on, and that is all.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `describe_autonomous_actions`

Reports which autonomous actions exist and which the Commander has switched on. Reports only; it
cannot change any of them. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

The capability registers this one reporting tool and nothing else. The actions themselves run
from journal events on the tick loop, and the rows that arm them are protected, so there is
deliberately no path from a tool call to an autonomous action being enabled or fired.

</details>
