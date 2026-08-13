---
title: Headset
---

# Headset

**Group:** Interface
**Capability id:** `vr`

D47 in the headset, as a SteamVR overlay drawn over Elite in your own cockpit.

It is an *overlay* application, never a scene one. That is what lets it sit alongside the game
instead of displacing it, and it is also what keeps D47 clear of anything resembling game
injection — it never owns the frame loop, never hooks the game, and never touches Elite's
process. Elite renders through OpenVR, which is why this is OpenVR and not OpenXR.

## The order does not matter

SteamVR before D47, D47 before SteamVR, SteamVR restarted halfway through an evening — none of
those is an ordering you should have to know about. D47 asks for a session every five seconds
while it does not have one, and rebuilds every surface when it gets one.

Three states, and the difference between the first two matters:

| State | What it means |
|---|---|
| `Unavailable` | No SteamVR runtime is installed. Nothing to wait for, and D47 keeps asking anyway in case one arrives. |
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

Reports whether D47 is showing in the headset, and if not, why not. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

## Settings

### Show Directive 47 in the headset {#enabled}

On by default. That costs nothing on a machine with no headset: D47 looks for a runtime, does
not find one, and reports `Unavailable`. Off leaves SteamVR alone entirely, which is for the
Commander who has it installed for something else and does not want D47 in it.

Also reachable without the panel:

```text
"headset overlay on"
"headset overlay off"
```

### Panel content {#mode}

`full` or `mini`. Mini is a mode of the same panel — a reduced content set, not a smaller copy:
the gear, the banners and the ask box go, and the transcript stays. Say "mini panel" or "full
panel" to switch it.

It is also a genuinely smaller image at a smaller width, because apparent text size in a
headset is the texture's pixel count and the quad's width in metres together. The two modes
therefore carry their own placements, which is why the rows below appear twice: a Commander who
parks mini out at the edge of vision and keeps the full panel in front of them is doing the
expected thing rather than fighting a shared setting.

## Placement

Six knobs per surface. Each one maps onto exactly one call into SteamVR, which is what keeps
this page honest about what it is changing:

| Row | What it does |
|---|---|
| Locking | Head-locked follows you; world-locked stays where you put it |
| Distance | Metres in front of you, head-locked only |
| Size | `SetOverlayWidthInMeters` — height follows from the panel's proportions |
| Curvature | `SetOverlayCurvature`, 0 flat to 1 wrapped |
| Opacity | `SetOverlayAlpha` |
| Scale | How large the content is drawn, on the desktop zoom's ladder |

You can also just reach out and take hold of the panel. The numbers are here for when you would
rather not.

### Grabbing it {#grab}

Point a controller at the panel and pull the trigger, and it comes with you — position and
orientation together, so it feels attached rather than dragged. Let go and it stays.

Nothing turns it to face you while it is held. That was tried in a previous implementation of
this and it is wrong: a panel forced upright and square cannot be tilted to read from below or
angled to sit beside you, which is most of what moving one is for.

Picking it up makes it world-locked, because picking something up and putting it somewhere is
what world-locked means. The setting follows the action rather than gating it — a head-locked
panel that sprang back to your face after you carried it across the cockpit would be D47
arguing with you.

Two details that are not obvious and are load-bearing:

- **The trigger only.** A controller reports its grip and its other buttons through the same
  channel, so treating them all as a press means the grip that grabs the panel also clicks
  whatever the ray was over.
- **The pointer is the only controller input an overlay gets.** SteamVR takes the controllers
  to drive its own laser and hands back mouse events. `IVRSystem.GetControllerState` returns
  false for controllers that are connected and tracking, silently, and an `IVRInput` action
  manifest was built twice in two separate projects, accepted by SteamVR, and never went live.
  This is not a shortcut; it is the road that exists.

### Panel locking {#panel-lock}

Head-locked or world-locked, per surface.

Head-locked follows you and is always in view, which is what you want for something you glance
at. World-locked stays where you put it, which is what you want for something that lives in a
particular corner of the cockpit — and it is what [re-anchoring](reanchor.md) exists to rescue
when Elite's recenter moves the cockpit out from under it.

Choosing world-locked implies re-anchor is bound and reachable off-panel, which it is: a
system-wide hotkey and a model-free voice phrase, neither of which needs you to be able to aim
at the panel.

### Distance {#panel-distance}

Metres in front of you, for a head-locked surface. A surface you have put down is wherever you
put it.

### Size {#panel-size}

How wide the quad is, in metres. SteamVR takes a width and derives the height from the
texture's proportions, so there is no height to set.

1.4 m was tried first in a previous implementation and read as enormous: at this distance that
is close to fifty degrees of view, so the panel filled the middle and the cockpit was behind it
rather than around it. The default is 1.1 m.

### Curvature {#panel-curve}

0 is flat and 1 is wrapped right around you.

This is the whole of *panels can switch between curved and flat*: a number reaching zero rather
than a second mode, because a mode is a thing that can disagree with the number. Captions are
never curved and have no row — two short lines in the middle of the view have no far edges to
bring closer, so a curved caption is a caption bent for no reason.

### Opacity {#panel-opacity}

How solid the surface is, from 0.1 to 1.

### Scale {#panel-scale}

How large the panel is drawn, as a percentage, on the same ladder the desktop window zooms
with — and by the same mechanism, a layout transform rather than a font size, so text rewraps
and spacing grows with it.

Distinct from mini mode, and the distinction is the point: this changes the size of everything
on the panel, mini changes how much of it there is. Zooming a panel you cannot read makes it
readable; switching to mini gives you less to read.

### Mini panel locking {#mini-lock}

The same six rows again, for the mini panel, because the two modes have different reasons to
exist. See [Panel locking](#panel-lock).

### Mini distance {#mini-distance}

See [Distance](#panel-distance).

### Mini size {#mini-size}

See [Size](#panel-size). The mini default is 0.34 m — it is meant to sit at the edge of vision.

### Mini curvature {#mini-curve}

See [Curvature](#panel-curve).

### Mini opacity {#mini-opacity}

See [Opacity](#panel-opacity).

### Mini scale {#mini-scale}

See [Scale](#panel-scale).

### Captions {#captions}

Everything D47 says, written under it. They place themselves, they clear themselves, and they
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
the voice, because a caption still sitting there after a silence command is D47 visibly not
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
