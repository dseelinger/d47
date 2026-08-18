---
title: Macros
group: Acting on the game
nav_order: 135
---

Your own named sequences of ship actions. Say the name, and Directive 47 runs them in order.

## Ask for it

> "docking prep"
> "run docking prep"
> "what macros do I have"

The name is whatever you called it. Both "the name" and "run the name" work, and neither needs
the AI to be configured — a macro is the most closed vocabulary there is, because you wrote it.

## Writing one

Macros are the one thing here you cannot set up by voice, and that is deliberate. Everything else
Directive 47 does has a fixed list of words behind it; composing a *new* sequence does not, and it
never can. So authoring happens in the panel or in the file.

**In the panel:** Settings → Macros → **Edit macros**. Each step is a drop-down of actions, a
drop-down of on / off / toggle, and a pause in milliseconds. There is nothing to type but the
name, so nothing you build there can be rejected.

**In the file:** `data/macros.json`, beside the executable like everything else Directive 47
writes.

```json
{
  "macros": [
    {
      "name": "docking prep",
      "steps": [
        { "action": "landing_gear", "state": "on", "pauseMs": 200 },
        { "action": "lights", "state": "on" }
      ]
    }
  ]
}
```

Both routes write the same file, and it is re-read while Directive 47 is running — a macro saved
in a text editor is sayable a moment later, with no restart.

## The pauses matter

Elite's panels animate. Without a pause, the second keystroke of "open the right panel, then go
down twice" arrives before the panel is there. 250 ms is the default and is usually enough.

## What a macro is allowed to do

Only actions Directive 47 already has — the same list that "gear down" comes from. It cannot
express a key that is not already reachable by asking out loud.

Two limits worth knowing:

- **No weapons.** The fire groups exist internally for the arrival honk, and macros cannot reach
  them. A macro is authored text, and authored text must not be a way around the rule that the
  AI never gets to open fire.
- **No taking a name that already means something.** A macro called "gear down" is refused,
  because otherwise two things answer to it and you cannot tell which one ran.

## When something is wrong with one

A bad macro is never silently dropped — that would leave you saying a phrase into the dark for a
week. It is refused by name, with the reason, in the editor and in "what macros do I have":

```text
- Refused: "combat" uses an action D47 does not have: shields_up.
```

One bad macro does not cost you the others. The rest of the file still loads.

## It stops before it starts

Every step is checked before any of them is sent. If the fourth action is on your joystick, the
macro does not run at all rather than pressing the first three and leaving the ship half
configured in a state you did not ask for and have to work out for yourself.

Steps already in the state you asked for are skipped, so a macro that puts the gear down does
nothing about the gear when it is already down.

Macros need **Let Directive 47 press keys in Elite** switched on — see
[Flight and navigation](flight-controls.md).

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `list_macros`

Report the Commander's macros, their steps, and any that were refused. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `run_macro`

Run one of the Commander's own named macros. Only names listed in the current game state exist;
anything else comes back saying so.

```json
{"type":"object","properties":{"name":{"type":"string","description":"The macro\u0027s name, as the Commander wrote it."}},"required":["name"],"additionalProperties":false}
```

The name is a free string rather than an enum, and that is not laziness. Macro names change
whenever the file is edited, and a schema that changed with them would invalidate the entire
cached prompt prefix. The names available now ride in the live game-state block below the cache
breakpoint, and the handler enforces the list regardless of what the model asked for.

The model-free route needs the same trick from the other end: descriptors are registered once and
never mutated, so macro phrases reach the keyword router through a function it consults rather
than through the descriptor. A macro name never enters a schema, so nothing about caching is
affected by letting that source change.

</details>
