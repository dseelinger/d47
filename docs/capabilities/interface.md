---
title: Interface
---

How Directive 47 looks, and which keys reach it.

There is nothing to ask for here — this one is all settings. The model cannot repaint the app or
rebind your keys, and was never given a way to.

## Settings

### Theme {#theme}

| Choice | What it looks like |
|---|---|
| `elite` | Amber on near-black. The default, and the one that matches the cockpit. |
| `dark` | Neutral dark. No colour opinion. |
| `light` | Neutral light, for a desktop that is not in a dark room. |
| `guardian` | Guardian teal and gold. |
| `elite-palette` | Elite, recoloured to match your own HUD. |

#### Matching your HUD

`elite-palette` reads the colour matrix Elite applies to its own HUD and uses it on Directive
47's amber palette, so the panel comes out the same colour as your cockpit — teal, white,
whatever you have set — without borrowing anything from the game.

It reads this file and never writes it:

```text
%LOCALAPPDATA%\Frontier Developments\Elite Dangerous\Options\Graphics\GraphicsConfigurationOverride.xml
```

If you have not set a HUD colour, or the file was written by a HUD mod in a shape Directive 47
does not recognise, the theme falls back to plain `elite` — which is what it would have looked
like anyway. Your game configuration is yours; Directive 47 is a guest in it.

### Zoom {#zoom}

How large the panel is drawn, from 50% to 300%. The gestures are the ones your browser already
taught you:

```text
Ctrl + scroll wheel     one step per notch
Ctrl + plus             one step larger
Ctrl + minus            one step smaller
Ctrl + 0                back to 100%
```

The steps are a browser's too:

```text
50  67  75  80  90  100  110  125  150  175  200  250  300
```

This makes the whole panel larger rather than just the letters, so spacing and layout grow with
the text instead of the text growing inside a layout that stayed put. If the window is too narrow
for the panel at that size, it scrolls sideways — the same thing a browser does — rather than
crushing rows into each other.

It applies to the settings window too, and the level survives a restart like the theme does.

### Window size and position

Not something to set. The window opens at a size that fits the screen it opens on, and after
that it opens where you left it.

If a remembered position would put the window on a monitor you have since unplugged, it is
ignored and the window comes back on a screen you have — a window you cannot reach with the mouse
looks exactly like the app failing to start.

### Open settings {#open-settings}

Opens the settings window. `Ctrl+,` out of the box.

Press the combination to bind it: the row listens for one and stores what it heard, so there is
no list of key names to learn and no way to type one that does not exist. Clear it to leave the
action unbound.

This one works while Directive 47 has focus.

### Re-anchor the headset panels {#reanchor}

Puts your world-locked headset panels back in front of you. `Ctrl+Alt+R` out of the box, and it
works **anywhere** — including with Elite in the foreground, which is the only time you want it.
See [Re-anchor](reanchor.md).

A key that works everywhere needs a modifier with it. On its own it would stop working in every
other application, so a bare key is refused when you press it, with a note saying so.

### Focus the ask box {#focus-ask}

Puts the cursor in the ask box from anywhere in the main window. `Ctrl+L` out of the box.

---

**The model cannot change any of these rows.** A bound key is one of the ways to reach a
protected setting, so a model that could rebind one could hand itself a way in it is not allowed
to have. See [Settings](settings.md).

## The ship AI's face {#avatar}

Top left of the panel, and in the headset too. One look per stage of a turn, so you can tell what
Directive 47 is doing without reading anything — which in a cockpit is the point.

| Stage | What it shows |
|---|---|
| `idle` | A closed ring. Nothing in flight. |
| `listening` | An open mouth. The microphone is on. |
| `transcribing` | Three rising bars — sound becoming words. |
| `thinking` | Four blocks around a gap, the Guardian motif, working. |
| `speaking` | A widening wave. |
| `answered` | A tick. |
| `unsure` | A question mark, drawn rather than typed. |
| `failed` | A cross. |

They differ in shape as well as colour, so they read in greyscale — colour is never the only
signal. Colour comes from your theme, so switching theme repaints the face with everything else.

Each one breathes: a slow opacity cycle, faster while a turn is running and very slow at rest.
Nothing here jitters, because a companion's face that jitters is a companion that looks anxious.

### Using your own

Drop image files into `data/avatar/<state>/` beside the executable — the same shape the audio
cues use, so if you have already customised those you know this one.

```text
data/avatar/thinking/01.png
data/avatar/thinking/02.png
data/avatar/thinking/03.png
```

- **One folder per state, and replacing one leaves the rest alone.** Your `thinking` frames do
  not turn off the drawn `idle`.
- **More than one file in a folder animates**, cycling in filename order at four frames a second.
  That is how an animated avatar is supported: as a sequence of stills.
- `.png`, `.jpg`, `.bmp` and `.webp`. **Not `.gif` or `.svg`** — Directive 47 decodes neither, and
  offering a format that silently never renders would be worse than not offering it.
- An empty, unreadable or corrupt file is skipped, and a state with nothing usable falls back to
  the drawn face rather than showing a broken box.

The shipped faces are drawn rather than supplied as images, and that is not only an aesthetic
choice: one widget tree renders to both the window and the headset, so an animation made of
decoded frames would need something advancing it on two surfaces — and it would put an image
decoder in a dependency graph that has stayed clear of them since the first release.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

This capability registers no tools. A descriptor declares a capability's whole surface, and this
one's surface is settings rows — giving the model a way to repaint the app or rebind a gesture
would be reach added for the sake of symmetry.

Colour lives in one resource dictionary per theme, keyed by role rather than by colour, so no
view hardcodes a literal and a sixth theme is a file rather than a sweep through every screen:

```text
D47.Background      the window behind everything
D47.Surface         cards, the transcript, raised areas
D47.SurfaceAlt      row striping and inset areas
D47.Border          hairlines between things
D47.Text            body text
D47.TextMuted       help text, placeholders, provenance lines
D47.Accent          the theme's own colour: focus, headings, the ask button
D47.AccentMuted     the same colour with the volume down
D47.Danger          the error banner
D47.Info            the update banner
```

The HUD override file holds a 3x3 matrix — each output channel is the dot product of one row
with the source colour, which is how a Commander's HUD ends up teal without the game shipping a
teal palette:

```xml
<GraphicsConfig>
  <GUIColour>
    <Default>
      <LocalisationName>Standard</LocalisationName>
      <MatrixRed>   0.35, 0.00, 0.00 </MatrixRed>
      <MatrixGreen> 0.00, 0.80, 0.00 </MatrixGreen>
      <MatrixBlue>  0.00, 0.20, 1.00 </MatrixBlue>
    </Default>
  </GUIColour>
</GraphicsConfig>
```

Zoom is a layout transform rather than a font-size change: it re-runs measure and arrange at the
new scale, so text rewraps and padding grows with it. The zoomed content sits in a horizontally
scrolling viewport, so enlarging past the window's width scrolls rather than squeezing — below
about 420 points a settings row cannot hold its caption beside its control, and the caption was
squeezed to nothing while the control drew over it.

The opening size is clamped to 90% of the working area of the screen the window appears on —
90% rather than 100% because a window filling the work area exactly reads as maximised. Size and
position live in `view-state.json` beside the executable and fail quietly if unreadable.

</details>
