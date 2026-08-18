---
title: Headset
group: Interface
nav_order: 123
---

Directive 47 in your headset, drawn over Elite in your own cockpit.

It sits alongside the game rather than replacing it. It never hooks Elite, never touches its
process, and never takes over the frame — it is an overlay, the same way SteamVR's own dashboard
is.

## Ask for it

> "is the headset working"
> "headset overlay off"
> "mini panel"
> "full panel"

## The order does not matter

SteamVR first, Directive 47 first, SteamVR restarted halfway through the evening — none of that
is something you should have to think about. Directive 47 looks for a headset every few seconds
and builds everything the moment one appears.

It **attaches** to SteamVR rather than starting it. If SteamVR is not running, or your headset is
switched off, Directive 47 waits and tells you which of the two it is waiting for:

```text
SteamVR is not running. D47 will attach when you start it.
```

```text
No headset is switched on. D47 will attach when one appears.
```

Nothing here starts SteamVR on your behalf. Launching it uninvited on a machine whose headset is
off takes over your desktop for no reason.

## What you get

Two things float in front of you.

**The panel** — the same panel as on the desktop, not a picture of it. It is the same app drawn a
second time, so the windowed version can never do something the headset version cannot.

**Captions** — everything Directive 47 says, written underneath. They place themselves, clear
themselves, and cannot be moved or dragged somewhere you would not see them.

## Moving it about

Point a controller at the panel, pull the **trigger**, and it comes with you — position and angle
together, so it feels attached rather than dragged. Let go and it stays.

Nothing turns it to face you while you hold it. A panel forced upright and square cannot be
tilted to read from below or angled to sit beside you, which is most of what moving one is for.

Picking it up makes it world-locked, because picking something up and putting it down is what
world-locked means. Directive 47 does not then argue with you about the setting.

If Elite's own recenter moves your cockpit out from under a panel you had placed, that is what
[re-anchoring](reanchor.md) is for.

## Settings

### Show Directive 47 in the headset {#enabled}

On by default, which costs nothing if you have no headset — it looks, finds nothing, and says so.
Turn it off if you use SteamVR for other things and would rather Directive 47 stayed out of it.

> "headset overlay on" / "headset overlay off"

### Panel content {#mode}

**Full** or **mini**. Mini is the same panel showing less — the gear, the banners and the ask box
go, the transcript stays. It is also drawn smaller, so it can sit at the edge of vision.

**Mini out of the box.** The full panel is 1.1 m across — close to fifty degrees of view — which
is a lot to meet before you know it can be moved or shrunk. Switch to full whenever you want it.

The two keep their own placements, so parking mini out to one side while the full panel stays in
front of you works the way you would expect.

### Placing a surface

Six settings each, and the mini panel has its own copies of all six.

| Setting | What it does |
|---|---|
| Locking {#panel-lock} | Head-locked follows you; world-locked stays where you put it |
| Distance {#panel-distance} | How far in front of you, in metres. Head-locked only — a surface you put down is where you put it |
| Size {#panel-size} | How wide, in metres. Height follows the panel's proportions, so there is nothing else to set |
| Curvature {#panel-curve} | 0 is flat, 1 is wrapped around you |
| Opacity {#panel-opacity} | How solid it is |
| Scale {#panel-scale} | How large the content is drawn, on the same steps the desktop window zooms with |

Head-locked is what you want for something you glance at; world-locked for something that lives
in a particular corner of the cockpit.

**World-locked out of the box**, and put down for you the first time Directive 47 runs in your
headset: about a metre ahead of wherever you are facing, low enough that the top of the panel sits
around knee height, and tilted back so it faces you rather than the ceiling. A panel that follows
your gaze is in the way of whatever you turned to look at, which is the one thing a companion
beside a flight sim should never be.

That first position is worked out from where your head actually is — the floor comes from your
room setup, and the height of the panel from its width and proportions — so it is not a number
picked for somebody else's height or somebody else's panel size. Move it once and it is yours;
Directive 47 never places it again.

**Scale and mini are different things**, and the difference is the point: scale changes how big
everything on the panel is, mini changes how much of it there is. Zooming a panel you cannot read
makes it readable; switching to mini gives you less to read.

The mini rows work the same way: [locking](#mini-lock), [distance](#mini-distance),
[size](#mini-size), [curvature](#mini-curve), [opacity](#mini-opacity) and [scale](#mini-scale).
The mini panel defaults to 0.34 m across — it is meant to sit at the edge of vision — against
1.1 m for the full one.

### Captions {#captions}

They follow the broadcast closed-caption standard rather than anyone's preference:

```text
42 characters   the most on one line
 2 lines        the most for one thing said
 3 lines        the most on screen at once
20 per second   reading speed, which decides how long one stays up
5/6 s .. 7 s    the shortest and longest a caption lingers
```

A caption stays up timed from when the **speech ends**, not when it starts. Nobody reads along
with a voice they can hear; they catch the last line after it has gone.

Captions follow what is actually audible rather than what was generated, so if you say "stop" the
captions stop with the voice. A caption still sitting there after a silence command is Directive
47 visibly not having stopped.

### Caption size {#size}

`small`, `medium` or `large`. Three sizes rather than a number, because a caption is either
legible at a glance or it is not, and there is nothing useful between two adjacent values.

### Caption background {#background}

How solid the box behind the text is.

A caption sits over a starfield, a station's floodlights and your own instruments. Text with
nothing behind it is unreadable against half of those — which is why broadcast captions have
always sat on a box, and why the box is not fully solid, since one you cannot see through is a
hole cut in your cockpit. The text stays fully opaque whatever you set; only the box fades.

### Reading speed {#speed}

Characters a second. `20` is the standard's adult rate, `17` its children's rate, and `12` is
there because reading speed is the one thing about a caption that is a property of the reader
rather than of the caption.

### Headset {#state}

Not a setting but a state, shown next to the switch — because *switched off* and *SteamVR is not
running* look identical from the outside unless something says which.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `get_headset_status`

Reports whether D47 is showing in the headset, and if not, why not. Reports only. Takes no
arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### `show_in_headset`

Shows D47 in the headset, or stops showing it — the same row as the switch above, reached by
voice. It answers with the *status* rather than an acknowledgement, because switching it on is
not the same as it appearing: with no runtime installed the setting takes and nothing shows.

```json
{"type":"object","properties":{"on":{"type":"boolean","description":"True to show D47 in the headset, false to leave SteamVR alone."}},"required":["on"],"additionalProperties":false}
```

It exists because asking to see the panel used to reach `get_headset_status`, that being the only
headset-shaped thing on the surface — so *"show the VR panel"* was answered with *"the overlays
are dark, Commander"*, which is true and is not what was asked. `set_setting` could always have
done it; nothing pointed at that.

An *overlay* application, never a scene one — that is what keeps D47 clear of anything resembling
game injection. Elite renders through OpenVR, which is why this is OpenVR and not OpenXR.

Three states, and the difference between the first two matters. `Unavailable` is no runtime
installed; `Connecting` is a runtime present but no session; `Active` is up. `VR_Init` returns
`Init_HmdNotFound` while `vrserver.exe` is already running: *SteamVR is running* and *a headset
is present* are different facts. `VR_Init` also **starts SteamVR** when it is not running, so it
is called only once a session already exists and a headset is present — checked with the process
list and `VR_IsHmdPresent`, neither of which starts anything. Recovery rebuilds rather than
repairs: a session that has gone away leaves every overlay handle stale, and a stale handle fails
forever with a plausible error.

Two quads: `com.dseelinger.d47.panel` and `com.dseelinger.d47.captions`. Apparent text size is
the texture's pixel count and the quad's width in metres *together*, so mini is a genuinely
smaller image as well as less content — drawing the full panel and hanging it nearer gives text a
third of the size.

```text
full   1024 x 640 px   at 1.10 m wide
mini     512 x 280 px   at 0.34 m wide
```

1.4 m was tried first and read as enormous — close to fifty degrees of view, so the panel filled
the middle and the cockpit was behind it rather than around it.

The panel is a second instantiation of the same view definition bound to the same view model,
rasterised offscreen into a plain pixel buffer and submitted with `SetOverlayRaw`. About 0.75 ms
a frame measured live against SteamVR, of which the rasterise is 0.30 ms, and only when something
changed.

**The trigger only.** A controller reports its grip and other buttons through the same channel,
so treating them all as a press means the grip that grabs the panel also clicks whatever the ray
was over. And the pointer is the only controller input an overlay gets: SteamVR takes the
controllers for its own laser and hands back mouse events.
`IVRSystem.GetControllerState` returns false for controllers that are connected and tracking,
silently, and an `IVRInput` action manifest was built twice in two separate projects, accepted by
SteamVR, and never went live.

**The pixels go over as raw bytes rather than as a texture.** A shared Direct3D texture was the
original route, and it left the overlays invisible in the headset while SteamVR reported them
visible — with every call returning success, the copy proven correct by test, and the rasterise
proven correct by writing a live session's first frame to `data/vr-PanelFull.png` and looking at
it. `SetOverlayRaw` hands OpenVR a buffer and lets it do the upload, which removes the graphics
device, the adapter match and the share flag along with the fault. Two things it has to get
right, both of which come out as a wrong picture rather than an error: the buffer is RGBA where
Avalonia rasterises BGRA, and its rows carry no padding, because `SetOverlayRaw` derives the
stride from the width.

Caption line breaks go after punctuation, or before a conjunction or preposition, bottom-heavy
where nothing else decides it; when those compete the syntactic break wins. Captions are never
curved: two short lines in the middle of the view have no far edges to bring closer.

</details>
