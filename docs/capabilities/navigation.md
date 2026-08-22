---
title: Navigation
group: Acting on the game
nav_order: 133
---

Puts a system name where you can use it, and tries to plot a course to it.

## Ask for it

> "plot a course to Shinrarta Dezhra"
> "copy that system name"
> "put the route on my clipboard"

## What you hear

Asking for a course is an action, and the standing rule for actions is *act first, talk least*.
When the destination has to be found first — "set course for the closest Imperial Shielding" —
you hear one sentence as soon as it is found and the map starts moving, and a short answer when
it is done:

```text
Closest Imperial Shielding is likely Scorpii Sector BB-O a6-2. Plotting.
Course plotted.
```

The first line is spoken *while* the map is being driven, not before. The figures, the material
ledger and the trader arithmetic wait until you ask for them.

## The clipboard is the part that always works

Asking for a course always puts the name on your clipboard first, before anything else is
attempted. Elite's galaxy map has a search box, and pasting into it works every time, in every
language, whatever your controls look like.

```text
Colonia is on your clipboard. Paste it into the galaxy map's search box to plot it.
Automatic plotting is switched off.
```

That is the whole feature, and it is deliberately the primary one. Everything below is a
convenience on top of it that can fail.

The **Course** page of the window's **Routing** tab is this as a control rather than a sentence,
in the same order: the name goes on the clipboard, then the map is driven if you asked for that,
then d47 says whether it took. Every system name drawn anywhere on that tab — a waypoint on the
route you are flying, a stop on a trade plan — copies when you press it.

## Letting it drive the map

Turn on **Try to plot courses in the galaxy map** and Directive 47 will also drive the map for
you, with your own keys, in this order:

1. Open the galaxy map, and wait until the game reports it showing.
2. **Up**, then **select** — that is the search box.
3. Paste the name, then **return** — the map flies to the first match.
4. Three seconds for the camera to get there.
5. **Select**, held for 1.2 seconds — a tap opens the system, a hold plots to it.
6. The galaxy map key again, which closes it.

Return rather than the UI down key, because the search box keeps focus after the paste and an
interface key sent to it is a character — the first cut of this macro typed an S into the box.

This is still best-effort. Directive 47 cannot see the map, so it cannot check that the search
matched the system you meant rather than another that starts the same way, and it cannot see
where the camera landed. What it can see, it checks.

Elite writes your whole route to a file the moment one is plotted, which means "did that work"
has a real answer:

```text
I tried to plot Colonia and no route appeared, so assume it did not work. Colonia is on your
clipboard. The search may have matched a different system.
```

Three answers, and they mean different things. **Course plotted** means a route to that system
really is in the file. **Assume it did not work** means the file was readable and no such route
appeared. **Cannot tell** means the file never became readable at all — which usually means Elite
is not running.

None of them is "done" said hopefully. A companion that leaves you flying towards a course you do
not have is worse than one that never tries.

Two more things it watches for. If the map key is pressed and the game never reports the map
open, nothing else is sent — the remaining keys are a W and a space bar, and typed into the
cockpit instead of the map they would fly the ship. And if the closing press leaves the map
showing, it says so: *the galaxy map is still open*.

**It needs three keys**: the galaxy map, UI up and UI select. All on the keyboard or mouse — a
key on a stick is one Directive 47 cannot press. It takes all three or none: a macro that opens
the map and then has no "select" leaves it open over the cockpit, which is worse than the
clipboard alone. So the first key it cannot press stops the whole attempt before anything is
sent, and you hear which one:

```text
Colonia is on your clipboard. I could not drive the galaxy map myself — You have no binding for
select, so there is no key for me to press. Paste it into the map's search box to plot it.
```

Elite's own default keyboard preset ships the galaxy map **unbound**, so out of the box you will
get the clipboard and that explanation.

## Pasting is an ordinary paste

Directive 47 sends Ctrl+V, not one of your bindings, because Elite does not bind paste — the
search box is a normal text field and the clipboard is the operating system's.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `copy_to_clipboard`

Put text on the Commander's clipboard so they can paste it into the game or a browser. Use for
system names, routes and values they asked for.

```json
{"type":"object","properties":{"text":{"type":"string","description":"What to put on the clipboard."}},"required":["text"],"additionalProperties":false}
```

### `plot_course`

Put a system name on the clipboard and, if the Commander has allowed it, try to plot a course to
it in the galaxy map. The plotting attempt is best-effort and is verified afterwards; the
clipboard always works.

```json
{"type":"object","properties":{"system":{"type":"string","description":"The star system to plot to, spelled as the game spells it."}},"required":["system"],"additionalProperties":false}
```

The confirmation lives in the app rather than in Core, because it waits and no Core component
reads the clock. It polls `NavRoute.json` for up to six seconds and distinguishes "no route
appeared" from "the file was never readable" — the two answers send the Commander to different
places. The same split holds for the map itself: the app waits up to three seconds on
`Status.json`'s `GuiFocus` for the map to open before the interface keys go, and again for it to
close after the two backs.

Return and Ctrl+V are sent as plain virtual keys rather than resolved from the bindings file,
because Elite binds neither. Every wait in the sequence is the Commander's own figure
(2026-08-21), not a measurement.

</details>
