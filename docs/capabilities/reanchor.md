---
title: Re-anchor
---

**Group:** Interface
**Capability id:** `reanchor`

Puts the world-locked headset panels back in front of you.

## Why this exists

Elite's in-game recenter moves the cockpit without telling SteamVR. Your world-locked panels
stay exactly where they were, in a world that has quietly rotated underneath them — and there
is no event to hook, because from SteamVR's point of view nothing happened.

So there is nothing to detect and nothing to correct automatically. What there is, is a
Commander who can see that their panels are in the wrong place and would like them back.

## It moves them as a group

Every world-locked surface gets the same delta, so their positions relative to each other
survive. That is the difference between re-anchoring and resetting: a per-surface "put it back
where it started" stacks them all in the same place in front of you, which is a different
feature and not one anybody asked for.

Only the yaw and the position are inherited. If you happened to be looking at your feet when
you triggered it, that is not an instruction to tip every panel forward and hang them over your
knees.

Head-locked surfaces and the caption layer are unaffected — they were never anywhere to drift
from.

## Three ways to reach it, and never only one

```text
the hotkey       Ctrl+Alt+R out of the box, system-wide
by voice         "re-anchor", "put the panels back", "recentre the panels"
the tool         reanchor_headset_surfaces
```

The hotkey is registered system-wide rather than scoped to D47's window, and that is the whole
point of it: the case this exists for is Elite holding the foreground, so a gesture that needs
D47 focused is a gesture that does not work when it is wanted.

The voice route goes through the model-free keyword router, so it works with no provider
configured — a drifted panel is not a thing that should need an API key to fix.

Notice that none of the three is the panel itself. A drifted panel is precisely the case where
you cannot aim at it.

## Tools

### `reanchor_headset_surfaces`

Snaps every world-locked headset panel back in front of the Commander, keeping their positions
relative to each other. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

It says how many moved, so "there is nothing to re-anchor" is a real answer rather than silence
that looks like a failure:

```text
Re-anchored 2 surfaces, keeping their layout.
```

## Why it is its own capability

The keyword router reaches a capability's first argument-free tool. A capability owning both
"how is the headset" and "put the panels back" could therefore only be asked one of them
without a model in the path — and this is the one that has to work with no model at all.
