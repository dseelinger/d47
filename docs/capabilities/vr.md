---
title: Headset
---

# Headset

**Group:** Interface
**Capability id:** `vr`

d47 in the headset, as a SteamVR overlay drawn over Elite in your own cockpit.

It is an *overlay* application, never a scene one. That is what lets it sit alongside the game
instead of displacing it, and it is also what keeps d47 clear of anything resembling game
injection — it never owns the frame loop, never hooks the game, and never touches Elite's
process. Elite renders through OpenVR, which is why this is OpenVR and not OpenXR.

## The order does not matter

SteamVR before d47, d47 before SteamVR, SteamVR restarted halfway through an evening — none of
those is an ordering you should have to know about. d47 asks for a session every five seconds
while it does not have one, and rebuilds every surface when it gets one.

Three states, and the difference between the first two matters:

| State | What it means |
|---|---|
| `Unavailable` | No SteamVR runtime is installed. Nothing to wait for, and d47 keeps asking anyway in case one arrives. |
| `Connecting` | A runtime is installed but not usable yet — SteamVR is not running, or it is running and there is no headset. |
| `Active` | The session is up and the surfaces exist. |

The middle one is a real condition rather than a moment in passing. `VR_Init` returns
`Init_HmdNotFound` while `vrserver.exe` is already running: *SteamVR is running* and *a headset
is present* are two different facts, and a check that collapses them leaves a machine which
will never produce a headset sitting in a connecting state forever. So the readiness signal is
`VR_Init` succeeding, never a process check.

Recovery rebuilds rather than repairs. A session that has gone away leaves every overlay handle
stale, and a stale handle fails forever with a plausible error — so the next attempt starts
from nothing.

## Two quads, not three

| Handle | What it is |
|---|---|
| `com.dseelinger.d47.panel` | The panel. Full and mini are content modes of it. |
| `com.dseelinger.d47.captions` | Captions. Head-locked, flat, output only. |

Mini is a mode of the panel, not a second surface and not a scaled-down copy: it shows less.
It is also a genuinely smaller image, because apparent text size in a headset is the texture's
pixel count and the quad's width in metres *together* — drawing the full panel and hanging it
nearer gives you text a third of the size, which is the one thing a surface meant to be read at
a glance cannot be.

```text
full   1024 x 640 px   at 1.10 m wide
mini    640 x 280 px   at 0.34 m wide
```

## One widget tree

The panel in the headset is not a picture of the window. It is a second instantiation of the
same view definition, bound to the same view model, rasterised offscreen and copied into a
shared Direct3D texture the compositor reads. There is no second UI codebase and no screenshot
anywhere in the chain, which is what makes it impossible for the windowed surface to be more
functional than the headset one.

The whole render-and-upload chain costs about 0.75 ms a frame measured live against SteamVR, of
which the rasterise is 0.30 ms — under one per cent of one core at the rate the panel updates.
And it only runs when something changed.

## Tools

### `get_headset_status`

Reports whether d47 is showing in the headset, and if not, why not. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

## Settings

### Show d47 in the headset {#enabled}

On by default. That costs nothing on a machine with no headset: d47 looks for a runtime, does
not find one, and reports `Unavailable`. Off leaves SteamVR alone entirely, which is for the
Commander who has it installed for something else and does not want d47 in it.

Also reachable without the panel:

```text
"headset overlay on"
"headset overlay off"
```

### Captions {#captions}

Everything d47 says, written under it. They place themselves, they clear themselves, and they
cannot be moved — which is why they are their own overlay quad rather than part of the panel:
a caption you can drag somewhere you will not see it is not a caption, and the placement
settings must not be able to reach them by accident.

They follow the closed-caption standard, and the numbers are asserted by test rather than
claimed here:

```text
42 characters   the most on one line
 2 lines        the most for one thing said
 3 lines        the rolling window
20 per second   reading speed, which decides the dwell
5/6 s .. 7 s    the shortest and longest a caption stays up
```

Two of those are worth spelling out. The **three-line window** is not a contradiction of the
two-line maximum: two lines is the most one utterance occupies, three is the most on screen at
once, which is the roll-up form live captioning has always used. And the dwell is **timed from
the end of speech**, not from the start — nobody is reading along with a voice they can hear,
they are catching the last line after it has gone.

Line breaks go after punctuation, or before a conjunction or preposition, and the result is
bottom-heavy where nothing else decides it. When those compete, the syntactic break wins: the
standard treats the shape as a preference and the break points as where a break belongs.

Captions are driven by what is *audible* rather than by what was generated, because the audio
arbiter is the one thing that knows what is actually coming out of the speaker. That is what
keeps them in step with a reply that got interrupted — say "shut up" and the captions go with
the voice, because a caption still sitting there after a silence command is d47 visibly not
having stopped.

### Caption size {#size}

`small`, `medium` or `large`. Three sizes rather than a number, because a caption is either
legible at a glance or it is not and there is nothing useful between two adjacent values.

### Caption background {#background}

How solid the box behind the text is, from 0.2 to 1, defaulting to 0.78.

A caption sits over a starfield, a station's floodlights and the cockpit's own instruments.
Text with nothing behind it is unreadable against half of those, which is why broadcast
captioning has always put it on a box — and why the box is not fully solid, because one that
you cannot see through is a hole cut in the cockpit. The text stays fully opaque whatever this
is set to; only the box fades.

### Reading speed {#speed}

Characters a second: `20` is the standard's adult rate, `17` its children's rate, and `12` is
offered because reading speed is the one thing about a caption that is a property of the reader
rather than of the caption.

### Headset {#state}

Not a setting — a state, reported where the switch is, because *it is switched off* and
*SteamVR is not running* look identical from the outside if nothing says which one it is. When
a session is up it also names the graphics adapter the overlay renders on, which is the one
thing most likely to differ between your machine and the one this was built on.

## Two things this gets right on purpose

**The graphics device is created on the adapter SteamVR names**, via `IVRSystem.GetDXGIOutputInfo`,
rather than on the system default. On a machine with one GPU those are the same and it does not
matter. On a hybrid-graphics machine the default can be the integrated GPU, and a texture
created there is a cross-adapter share that SteamVR will reject or slow-path — a failure that
only ever happens on somebody else's computer.

**Nothing is drawn or sent unless it changed.** The panel is driven by its view model, so a
tick with nothing new costs one boolean. A transform pushed every frame would also fight the one
you are currently dragging it to.
