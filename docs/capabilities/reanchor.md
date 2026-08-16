---
title: Re-anchor
group: Interface
nav_order: 122
---

Puts your world-locked headset panels back in front of you.

## Ask for it

> "re-anchor"
> "put the panels back"
> "recentre the panels"

Or press **Ctrl+Alt+R**, which works anywhere — including with Elite in the foreground.

## When you need it

Elite's in-game recenter moves your cockpit without telling SteamVR. Your world-locked panels
stay exactly where they were, in a world that has quietly rotated underneath them.

Nothing can detect that and put it right on your behalf: from SteamVR's point of view, nothing
happened. What can happen is you noticing your panels are in the wrong place and asking for them
back.

## What it does to them

Every world-locked panel moves together, so their arrangement relative to each other survives.
That is the difference between re-anchoring and resetting — putting each one back where it
started would stack them all in the same spot in front of you.

Only your facing and position are used. If you happened to be looking at your feet when you
triggered it, that is not an instruction to tip every panel forward and hang them over your knees.

Head-locked panels and the captions are untouched. They follow you already, so they were never
anywhere to drift from.

It tells you how many moved, so "there was nothing to re-anchor" is an answer rather than silence
that looks like a failure:

```text
Re-anchored 2 surfaces, keeping their layout.
```

## Never only from the panel

The hotkey works system-wide rather than only when Directive 47 has focus, and the spoken route
needs no model configured. Both matter for the same reason: a drifted panel is exactly the case
where you cannot aim at the panel, and the moment you want this is the moment Elite is holding
the foreground.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `reanchor_headset_surfaces`

Snaps every world-locked headset panel back in front of the Commander, preserving their relative
layout. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

This is its own capability rather than part of the headset one because the keyword router reaches
a capability's first argument-free tool. A capability owning both "how is the headset" and "put
the panels back" could only be asked one of them without a model in the path — and this is the
one that has to work with no model at all.

</details>
