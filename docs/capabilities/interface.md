---
title: Interface
---

# Interface

**Group:** Interface
**Capability id:** `interface`

How d47 looks, and which keys reach it.

This capability registers no tools. A descriptor declares a capability's whole surface, and
this one's surface is settings rows — giving the model a way to repaint the app or rebind a
gesture would be reach added for the sake of symmetry.

## Settings

### Theme {#theme}

Colour lives in exactly one place: a resource dictionary per theme, keyed by role rather than
by colour. No view hardcodes a literal, which is what makes a fifth theme a file rather than a
sweep through every screen.

| Id | What it looks like |
|---|---|
| `elite` | Amber on near-black. The default, and the one that matches the cockpit. |
| `dark` | Neutral dark. No colour opinion. |
| `light` | Neutral light, for a desktop that is not in a dark room. |
| `guardian` | Guardian teal and gold. |
| `elite-palette` | Elite, recoloured by your own HUD matrix. |

The roles a theme defines:

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

#### The Elite colour scheme theme

`elite-palette` reads the matrix Elite Dangerous applies to its own HUD, from:

```text
%LOCALAPPDATA%\Frontier Developments\Elite Dangerous\Options\Graphics\GraphicsConfigurationOverride.xml
```

The file holds a 3x3 matrix — each output channel is the dot product of one row with the
source colour, which is how a Commander's HUD ends up teal or white without the game shipping
a teal or white palette:

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

d47 applies that matrix to its own Elite palette, so the panel picks up the cockpit's colour
without borrowing the game's assets.

Read-only and fail-soft, for the same reason the binds parser is read-only: this is your game
configuration and d47 is a guest in it. A file that is missing, hand-edited, or written by a
HUD mod resolves to "no matrix", and the theme falls back to plain `elite` — which is what it
would have looked like anyway.

### Zoom {#zoom}

How large the panel is drawn, from 50% to 300%. Four gestures, borrowed wholesale:

```text
Ctrl + scroll wheel     one level per notch
Ctrl + plus             one level larger
Ctrl + minus            one level smaller
Ctrl + 0                back to 100%
```

Every browser has already taught you those, which is the entire argument for them — a panel
that invents its own zoom gesture is a panel you have to be told about. The levels are
Chrome's ladder, for the same reason:

```text
50  67  75  80  90  100  110  125  150  175  200  250  300
```

This scales the rendered panel, not the text. The difference shows up at 200%: a font-size
change would grow the letters inside a layout that stayed where it was, and everything would
collide. A layout transform re-runs measure and arrange at the new scale, so text rewraps,
padding grows with it, and the panel at 200% is the panel — just larger.

It applies to the settings window too, because that is the same widget tree. A zoom that
stopped at the panel's edge would be a zoom with a boundary you have to learn.

The level is a setting rather than view state, so it survives a restart the way the theme
does, and the settings row above is the same value the gestures write.

### Window size and position

Not a settings row — there is nothing to choose. The window opens at a size that fits the
screen it opens on, and after that it opens where you left it.

The default of 820x640 is device-independent pixels, so Windows scales it: at 150% that is
1230x960 real pixels, which is nearly the whole usable height of a 1080p display and taller
than a 1366x768 laptop screen outright. The default was chosen at 100% and never checked
against a work area that had been scaled underneath it. So the opening size is clamped to 90%
of the working area of the screen it actually appears on — 90% rather than 100% because a
window filling the work area exactly reads as maximised.

Size and position are then remembered like a collapsed settings card is: in `view-state.json`
beside the executable, failing quietly if it cannot be read. A remembered position is only
honoured if enough of the window would land on a screen that still exists — restoring onto a
monitor that has since been unplugged is a window you cannot reach with the mouse, and the
symptom looks exactly like the app failing to start.

### Open settings {#open-settings}

Opens the settings window. Bound to `F10` out of the box.

Press the combination to bind it: the row listens for one gesture and stores what it heard,
so there is no list of key names to learn and no way to type one that does not exist. Clear it
to leave the action unbound.

Binding happens in the main window only. The gesture is window-scoped — a hotkey that works
while Elite has the foreground needs a system-wide registration, which arrives with the phase
that needs it rather than being built here for nothing to use.

**Protected**, like every hotkey row. A gesture is one of the three callers that can reach a
protected setting, so a model that could rebind one could hand itself a caller it is not
allowed to be. See [Settings](settings.md#the-protected-rule).

### Re-anchor the headset panels {#reanchor}

Snaps every world-locked headset panel back in front of you. Bound to `Ctrl+Alt+R` out of the
box, and **registered system-wide** rather than scoped to d47's window — unlike the two rows
above.

That is the whole point of it. The case it exists for is Elite holding the foreground with the
panels drifted somewhere you cannot aim at, so a gesture that needs d47 focused is a gesture
that does not work when it is wanted. See [Re-anchor](reanchor.md).

Protected, on the same grounds as every other hotkey row.

### Focus the ask box {#focus-ask}

Puts the cursor in the ask box from anywhere in the main window. Bound to `Ctrl+L` out of the
box, and protected on the same grounds.
