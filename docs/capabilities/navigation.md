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

Turn on **Try to plot courses in the galaxy map** and Directive 47 will also open the map, paste
the name in and press return.

This is genuinely best-effort. It depends on where the map's focus happens to be when it opens,
on the map's layout, and on your game's language. There is no way for Directive 47 to see any of
that.

So it checks afterwards. Elite writes your whole route to a file the moment one is plotted, which
means "did that work" has a real answer:

```text
I tried to plot Colonia and no route appeared, so assume it did not work. Colonia is on your
clipboard. The search box may not have had focus.
```

Three answers, and they mean different things. **Course plotted** means a route to that system
really is in the file. **Assume it did not work** means the file was readable and no such route
appeared. **Cannot tell** means the file never became readable at all — which usually means Elite
is not running.

None of them is "done" said hopefully. A companion that leaves you flying towards a course you do
not have is worse than one that never tries.

The map needs a binding for this to be attempted at all. Elite's own default keyboard preset
ships the galaxy map **unbound**, so out of the box you will get the clipboard and an explanation.

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
places.

</details>
