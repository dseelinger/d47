---
title: Ship systems
group: Acting on the game
nav_order: 126
---

Moves power around and reaches the two panic buttons: silent running and heat sinks.

## Ask for it

> "pips to engines"
> "balance the power"
> "heat sink"
> "silent running"

## Pips

Each request moves power one step, the same as one press of your own key. "Four pips to engines"
is four presses, so ask for it the way you would press it.

```text
Pressed Numpad_7 for power to engines.
```

In the SRV the same words reach the SRV's own power bindings, because Elite binds those
separately and you meant the vehicle you are sitting in.

## Heat sinks and silent running

Both work only while you are flying — they are refused when docked or landed, with the reason.
Silent running is worth knowing about before you use it by voice: it is a toggle, so asking twice
turns it back off, and Directive 47 will not stop you cooking your own ship.

## The fuel scoop is not a switch

Elite has no fuel scoop binding, because scooping is not something you turn on. Fly a
scoop-fitted ship into a star's scoop zone and it starts by itself.

So there is nothing here to press, and asking for it gets an answer rather than a keystroke —
Directive 47 can tell you whether you are currently scooping, because the game reports that, but
it cannot start or stop it and neither can you.

## You have to turn it on

Everything here needs **Let Directive 47 press keys in Elite**, which is off by default and
cannot be switched on by the AI. See [Flight and navigation](flight-controls.md) for why, and for
what happens when an action is on your stick rather than your keyboard.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `control_systems`

Move power between engines, weapons and systems, and reach silent running and heat sinks. Only
the actions listed as reachable in the current game state will work; anything else comes back
with the reason it did not.

```json
{"type":"object","properties":{"action":{"type":"string","description":"Which action to perform.","enum":["power_to_engines","power_to_weapons","power_to_systems","balance_power","silent_running","heat_sink","analysis_mode"]},"state":{"type":"string","description":"What to leave it in. Elite binds a single toggle, so asking for \u0022on\u0022 or \u0022off\u0022 checks the game\u0027s own report first and does nothing if it is already there. Defaults to toggling.","enum":["on","off","toggle"]}},"required":["action"],"additionalProperties":false}
```

`silent_running` resolves to Elite's `ToggleButtonUpInput`, which is the game's own name for it
and has been since long before silent running had a HUD indicator. It is spelled that way here
because that is the only spelling that resolves against a bindings file.

</details>
