# Is there anything in the mirror to read?

**Method and instrument, written 2026-08-16. The measurement itself is untaken.**

This is the other half of `list.md` Phase 22, *Spike: is there anything in the mirror to read*. The
first half — the licence graph and Frontier's published rules — is
[screen-reading-licence-and-rules.md](screen-reading-licence-and-rules.md), which was desk research
and said so in its own first paragraph:

> This page cannot answer the spike's primary question. Whether a panel can be located at all in the
> desktop mirror needs three screenshots at three head angles in a headset.

This page is the instrument for taking that measurement, what it has already been proved to do
against real Elite pixels, and the empty table the numbers go into. **It deliberately stops short of
a verdict**, because the frames that would support one have not been captured.

---

## What the answer decides

Everything else in Phase 22. In VR, Elite renders its panels in **world space**: on the desktop
mirror they move with the head, change scale with distance and skew with viewing angle, so there is
no region to crop and the problem is feature matching and a homography rather than a pixel
comparison. If nothing can reliably locate a panel in that view, the branch closes there and what is
left is a desktop-only feature — which works for half the Commanders who would want it, and the half
that does not include this repository's maintainer.

The two desk questions cannot decide it. A licence answer and a rules answer say nothing about
whether a picture contains a locatable panel.

## The instrument

`spike/MirrorProbe`, three verbs and nothing cleverer:

```
MirrorProbe windows
MirrorProbe capture --label <name> --note "vr, head straight ahead" --delay 10
MirrorProbe analyse --panel <png> --rect x,y,w,h --against captures
```

**`windows`** lists every visible top-level window with its framing. This is not a convenience. In
VR there is more than one candidate and they are different pictures: Elite's own window, and
SteamVR's view. Choosing between them by guessing is how a spike answers the wrong question
confidently.

**`capture`** takes one frame down **all three** capture paths and writes each one beside a sidecar
recording the window, its framing, `GuiFocus` from `Status.json`, and the `--note`. The three paths
are there because they disagree in a useful way:

| Path | What it is | Why it is in the set |
|---|---|---|
| **Windows Graphics Capture** | What d47 would actually use | The frames judged here have to be the frames the product would get, or the finding does not transfer |
| **GDI blit from the screen** | What the compositor has on the glass | The control — and the only cheap way to tell **borderless from exclusive fullscreen** from outside the game, since styles and rectangles cannot: both are a popup covering the monitor exactly. A black frame here means exclusive |
| **`PrintWindow`** | The window asked to draw itself | Frequently declines for Direct3D content. Free to try, and its refusal is information |

Every frame is measured for blankness — mean, standard deviation and brightest pixel — because **a
capture path that "worked" and returned an empty picture is the failure most likely to be read as a
success.**

**`analyse`** cuts the panel out of one frame and tries to find it in the others: detect, describe,
match, Lowe ratio at 0.75, RANSAC homography at 3 px. Textbook and nothing else, on purpose — the
question is whether the mirror carries enough for the textbook method, not whether a sufficiently
determined pipeline could squeeze an answer out of it. A result only a bespoke pipeline can reach is
a result the phase should not be built on.

It reports keypoints, good matches, inliers, inlier ratio and mean reprojection error, and it calls
a panel **located** only at twelve or more inliers, a quarter of the surviving matches, and a
projected quadrilateral that is still convex and still roughly panel-sized. A homography will
happily fold the panel into a bow tie and report a good fit; rejecting that is the difference
between a located panel and a number that looks like one. Anything short of the bar is *not located*
rather than a weak yes, because a weak yes is the confident wrong answer this whole phase is written
against.

### Three things in it that exist because of a specific way to get this wrong

- **The source frame stays in the comparison set as a control.** If the panel cannot be found in the
  picture it was cut out of, the fault is in the probe and not in the mirror, and the run says so
  loudly before any other row is worth reading.
- **The probe's own output is excluded from the comparison set.** The saved crop matches itself at
  100%, and that row reads exactly like a result.
- **`--note` carries what nothing else can.** No API on this machine reports whether Elite is
  rendering to a headset, and it is not recoverable from the frame afterwards. Unnoted, a frame is
  unclassifiable by the time anybody looks at it.

## What is already proved, and what is not

**Proved, against the running game on 2026-08-16** — Elite was up, docked, `GuiFocus 5`, station
services open, window `2560x1440`, undecorated and covering its monitor:

| | |
|---|---|
| All three capture paths returned real frames | WGC mean 39.1, GDI 41.0, PrintWindow 42.2 — no black frame, and the small spread is animation between the three grabs |
| **The window is composited, not exclusive** | The GDI screen blit returned content. Under exclusive fullscreen it would be black |
| `PrintWindow` works on Elite | Which is not the usual outcome for Direct3D content and is worth knowing |
| The matching path works on real Elite pixels | An 800x560 crop carried **2,483 keypoints**, and was located in frames from the other two capture paths at **0.64 px** mean reprojection error |
| A crop is locatable across capture paths | 68/245 and 59/203 inliers — the low ratios are two grabs of a moving scene, and the sub-pixel error is the part that matters |

**Not proved, and not hinted at.** Whether Elite was rendering to a headset at that moment is not
recorded by anything the probe reads, so that frame is filed as unclassified. SteamVR was running;
that is not the same claim and is not offered as one. **No frame in this measurement was taken in
VR, at a head angle, or with a world-space panel in it.** Everything above says the instrument works
and says nothing whatever about the mirror.

## The measurement, and how to take it

Elite in VR, a panel open, and for each row: `capture` with a `--note` saying what it is, then one
`analyse` run with the panel cropped out of the **head-straight-ahead** frame and matched into the
others.

```
MirrorProbe capture --label vr-straight --note "vr, panel open, head straight ahead" --delay 10
MirrorProbe capture --label vr-left     --note "vr, panel open, head ~30 deg left"   --delay 10
MirrorProbe capture --label vr-right    --note "vr, panel open, head ~30 deg right"  --delay 10
MirrorProbe analyse --panel captures/vr-straight.wgc.png --rect x,y,w,h --against captures
```

`--delay` counts down before grabbing, which is how a frame gets taken with a headset on. The
`--rect` is read off the straight-ahead frame by hand; there is no way around looking at it once.

Then the same on the desktop with no headset, fullscreen and borderless, which is the control the VR
rows are read against.

| Frame | Capture path | Located? | Inliers | Ratio | Reprojection |
|---|---|---|---|---|---|
| VR, head straight ahead | | | | | |
| VR, head ~30° left | | | | | |
| VR, head ~30° right | | | | | |
| Desktop, borderless | | | | | |
| Desktop, fullscreen | | | | | |

**Look at the annotated frames, not only at the table.** Every run writes `located.<name>.png` with
the projected quadrilateral drawn on it. A homography can produce a confident number and put the
quadrilateral in the wrong place, and that combination is this phase's named failure mode.

## Two things found by building the instrument rather than by reading

Both refine [screen-reading-licence-and-rules.md](screen-reading-licence-and-rules.md), and neither
contradicts it.

### The recommended package name pulls in three that are not wanted

That page recommends `OpenCvSharp4.Windows.Slim`, and the recommendation is right about the thing it
was checking: the resolved slim runtime package contains **exactly one binary**,
`OpenCvSharpExtern.dll`, and **no FFmpeg DLL of any version**, confirmed on disk rather than from the
packaging file. But `OpenCvSharp4.Windows.Slim` is a metapackage, and on any `net8.0`-or-later
framework it also pulls **`OpenCvSharp4.WpfExtensions`**, which pulls `System.Drawing.Common` and
`Microsoft.Win32.SystemEvents`.

All three are permissive and none would trouble the licence gate. They are simply three packages,
one of them a WPF interop assembly, that an Avalonia application has no use for. **Naming
`OpenCvSharp4` and `OpenCvSharp4.runtime.win.slim` individually resolves to the same managed
assembly and the same single native with none of them**, which is what `MirrorProbe.csproj` does.

### SIFT is not in the namespace its siblings are in

The licence page's advice — *use ORB or SIFT, never SURF* — is correct and survives intact. But ORB,
AKAZE and BRISK are in the `OpenCvSharp` namespace and **SIFT is in `OpenCvSharp.Features2D`**, while
`OpenCvSharp.XFeatures2D` — the namespace whose name says contrib — is where SURF lives and SIFT does
not. The native entry points say the same thing and are the authority: `features2d_SIFT_create`
beside `xfeatures2d_SURF_create`. A five-minute trap, recorded so it costs nobody five minutes
twice.

## Where the pixels went

Nowhere. The frames taken while proving the instrument were the maintainer's own game session, and
they were deleted once their numbers were read; `spike/MirrorProbe/.gitignore` keeps `captures/` out
of the repository, which means the sidecar and the report from that run survive on the bench and not
in git either. Every number quoted above is therefore reproduced here rather than linked to, because
this page is the only place it now exists. This is the same rule Phase 22 states for the product — local matching only, nothing leaves
the machine, no frame reaches the model — applied to the spike that precedes it.
