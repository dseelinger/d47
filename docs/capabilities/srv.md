---
title: SRV
group: Acting on the game
nav_order: 129
---

The switches on the SRV: turret, handbrake, drive assist, throttle direction, and getting your
ship back.

## Ask for it

> "turret"
> "handbrake"
> "recall my ship"
> "reverse"

## Recall and dismiss

"Recall my ship" is one binding in Elite that does both, so it recalls a ship that is away and
dismisses one that is here. Directive 47 presses the key and reports that it did; it cannot tell
you which of the two you just asked for, because the game does not say.

This is the one action here that also works on foot.

## Toggles only

Everything here is a switch. Steering, throttle and turret aim are axes, and an axis has no press
to send — Directive 47 can turn the turret on and cannot point it.

## Boarding is not a binding

Getting in and out of the SRV goes through the role panel rather than a key, so there is nothing
to press and asking for it gets a sentence rather than a keystroke. "Open the role panel" is the
closest thing, and from there "down", "right" and "select" walk you to it.

## You have to turn it on

Everything here needs **Let Directive 47 press keys in Elite**, off by default and unreachable by
the AI. See [Flight and navigation](flight-controls.md).

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `control_srv`

Operate the SRV's turret, handbrake, drive assist and ship recall. Only the actions listed as
reachable in the current game state will work; anything else comes back with the reason it did
not.

```json
{"type":"object","properties":{"action":{"type":"string","description":"Which action to perform.","enum":["srv_turret","srv_handbrake","srv_drive_assist","srv_reverse","recall_ship"]},"state":{"type":"string","description":"What to leave it in. Elite binds a single toggle, so asking for \u0022on\u0022 or \u0022off\u0022 checks the game\u0027s own report first and does nothing if it is already there. Defaults to toggling.","enum":["on","off","toggle"]}},"required":["action"],"additionalProperties":false}
```

`srv_handbrake` resolves to Elite's `AutoBreakBuggyButton`, spelled with the game's own
misspelling, because that is what appears in a bindings file.

</details>
