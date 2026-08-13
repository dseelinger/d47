---
title: Panels and interface
---

Opens the cockpit panels and moves around them, so you can change fire group or read your
messages without taking a hand off the stick.

## Ask for it

> "open the left panel"
> "next fire group"
> "system map"
> "back"

## Moving around a panel

Once a panel is open, "up", "down", "left", "right", "select" and "back" walk it the same way
your own UI keys do. This is slower than doing it by hand and it is not trying not to be — it
exists for the moments when both hands are busy.

## The maps

"Galaxy map" and "system map" open them, and that is all they do here. Plotting a course inside
the galaxy map is its own thing, because it depends on where the map's focus happens to be and on
what language your game is in — see [the galaxy map page](galaxy-map.md) for why the clipboard is
the route that actually works.

On Elite's own default keyboard preset both maps ship **unbound**. If you have never touched your
controls, asking for the galaxy map gets:

```text
You have no binding for the galaxy map, so there is no key for me to press.
```

Bind them in the game and they start working. Directive 47 will not bind them for you — your
bindings file is read-only to it, always.

## You have to turn it on

Everything here needs **Let Directive 47 press keys in Elite**, off by default and unreachable by
the AI. See [Flight and navigation](flight-controls.md).

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `control_interface`

Open the cockpit panels, move around them, and change fire group. Only the actions listed as
reachable in the current game state will work; anything else comes back with the reason it did
not.

```json
{"type":"object","properties":{"action":{"type":"string","description":"Which action to perform.","enum":["left_panel","right_panel","comms_panel","role_panel","next_panel","previous_panel","galaxy_map","system_map","ui_up","ui_down","ui_left","ui_right","ui_select","ui_back","next_fire_group","previous_fire_group"]},"state":{"type":"string","description":"What to leave it in. Elite binds a single toggle, so asking for \u0022on\u0022 or \u0022off\u0022 checks the game\u0027s own report first and does nothing if it is already there. Defaults to toggling.","enum":["on","off","toggle"]}},"required":["action"],"additionalProperties":false}
```

The panel actions each have a `_Buggy` twin in Elite for the SRV, and the right one is chosen
from the mode rather than from a separate action id, because a Commander who says "left panel"
means the vehicle they are sitting in.

</details>
