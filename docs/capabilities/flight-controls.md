---
title: Flight and navigation
group: Acting on the game
nav_order: 128
---

Flies the parts of your ship that are switches: gear, lights, scoop, hardpoints and the frame
shift drive.

## Ask for it

> "gear down"
> "retract the hardpoints"
> "lights off"
> "take us to supercruise"

## It presses your keys, not its own

Directive 47 has no keys of its own. It reads the bindings you already use and sends those, so
"gear down" presses whatever *you* have landing gear bound to. Change your controls in the game
and nothing here needs changing.

Your bindings are only ever read. Directive 47 never writes to them.

## You have to turn it on

**Let Directive 47 press keys in Elite** is off until you switch it on, and it is one of the
settings the model cannot reach — it can be changed from the panel, a hotkey, or by voice
through the keyword router, but never by asking the AI nicely. A companion that could grant
itself your keyboard on the strength of a message another Commander sent you is not a companion.

Keys go out only while Elite is the window in front. Alt-tab to a browser mid-command and the
rest of the sequence is dropped rather than typed into it.

## Why it sometimes says no

Two things stop an action, and it tells you which:

```text
Landing gear does nothing while you are in supercruise.
```

The cockpit is not one mode. Gear, scoop and hardpoints are inert in supercruise, so Directive 47
would rather refuse than say "done" over a key that did nothing.

```text
Hardpoints is on your joystick, which I have no way to press. Bind it to a key or a mouse
button and I can.
```

This is the common one, not the rare one. If you fly on a stick, most of your ship is on the
stick, and there is no key there to press. Bind the handful you want by voice to a key as well —
Elite gives every action a second slot for exactly this — and they start working.

An action you have left unbound entirely says so too, rather than failing as silence.

## What it can reach

Landing gear, ship lights, cargo scoop, hardpoints, the frame shift drive, supercruise, the
hyperspace jump, flight assist, throttle to zero, and boost.

Firing your weapons is deliberately not on this list. Directive 47 reads text from the galaxy
that anyone can write, and a companion that can be talked into opening fire is a different kind
of problem from one that can be talked into turning the lights on.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `control_flight`

Operate the landing gear, lights, cargo scoop, hardpoints and the frame shift drive. Only the
actions listed as reachable in the current game state will work; anything else comes back with
the reason it did not.

```json
{"type":"object","properties":{"action":{"type":"string","description":"Which action to perform.","enum":["landing_gear","lights","cargo_scoop","hardpoints","frame_shift_drive","supercruise","hyperspace","flight_assist","throttle_zero","boost"]},"state":{"type":"string","description":"What to leave it in. Elite binds a single toggle, so asking for \u0022on\u0022 or \u0022off\u0022 checks the game\u0027s own report first and does nothing if it is already there. Defaults to toggling.","enum":["on","off","toggle"]}},"required":["action"],"additionalProperties":false}
```

The enum is the whole group and never changes, which is what keeps the schema byte-identical
across turns and prompt caching alive. What is reachable *now* rides in the live game-state
block below the cache breakpoint, and the handler enforces it regardless of what the model asked
for.

</details>
