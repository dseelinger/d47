# Handoff: Directive 47 panel — control system redesign

## Overview

Directive 47 ("d47") is a voice companion for Elite Dangerous. The same panel is drawn
twice: in a desktop Avalonia window, and in a SteamVR overlay. This handoff covers a
redesign of the panel's **control vocabulary, spacing system, type scale and colour
token derivation** — plus two screens rebuilt on it (Settings › Voice Input, and the
Help Improve D47 dialog) and a control-kit reference sheet.

The redesign answers a specific set of defects found in the shipping 1.1.0 UI:

1. Control identity was unreadable — buttons, text inputs, read-only report boxes and
   list rows were all the same orange-outlined rectangle.
2. Two-state controls could not be read at a glance; several sliders showed a bar fill
   with no value, no label and no ticks, and were visually identical to genuine
   progress bars elsewhere in the app.
3. Labels sat ~900px from their controls, and the control column went ragged because a
   per-row reset button shifted every control left when present.
4. Truncation and overflow everywhere — clipped buttons, mid-word cuts, colliding labels.
5. No spacing scale; density was inverted (settings sparse, scannable lists cramped).
6. Three different patterns for picking one value (spinner, dropdown, unlabeled bar).
7. Walls of legal text with five co-equal buttons and no primary action.
8. Status colours (red, blue) were fixed hexes that break under the HUD-matrix theme,
   and blue carried three unrelated meanings.
9. Four competing heading treatments; nav levels 2 and 3 looked identical.
10. Hit targets and glyph sizes below what is usable in a headset.

## About the design files

The file in this bundle is a **design reference written in HTML** — a prototype showing
the intended look and behaviour. It is **not production code to port**. The task is to
recreate this design in d47's existing environment: **Avalonia UI (C#/XAML)**, using the
project's established control templates, styles and resource dictionaries.

`D47 Panel v2.dc.html` is a self-contained page. Open it in a browser. The panel has
three views, switched by the strip under the tab bar, and a tweaks panel (theme, accent,
bloom, scanlines, headset scale) that demonstrates the token derivation live.

## Fidelity

**High fidelity.** Colours, type, spacing and geometry are final and should be matched
precisely. Motion and hover states are specified below but were not all built in the
prototype — implement them from this document.

---

## Fixed constraints (do not change these)

- **Four themes.** Elite orange, Phosphor green, HUD matrix, Daylight. In HUD matrix the
  player's in-game HUD colour matrix supplies Accent, so **every accent-related colour
  must be derived from Accent at runtime — never a fixed hex.**
- **Glow (bloom) and scanlines on dark themes only.** Daylight gets neither.
- **The panel is read in a VR headset.** Small text and thin lines are much harder to
  read there. Minimum interactive target 44 device-independent px; body text 16px.
- **Two-state controls are toggle switches. No ComboBox anywhere.**

---

## Design tokens

### Theme roots

| Theme | Accent | Background | Dark? |
|---|---|---|---|
| Elite orange | `#FF7A1A` | `#08070A` | yes |
| Phosphor green | `#3BE377` | `#040705` | yes |
| HUD matrix | from the game's HUD colour matrix | `#05070C` | yes |
| Daylight | `#9A3B00` | `#EAE6DE` | no |

### Derived ramp

Everything else is computed from Accent and Background. In the prototype this is CSS
`color-mix(in oklab, …)`; in Avalonia, compute it in C# (see "Implementing the ramp").

| Token | Rule | Used for |
|---|---|---|
| `hot` | dark: 58% Accent + 42% `#FFFFFF`<br>light: 55% Accent + 45% `#140800` | screen titles, selected values, numbers that matter |
| `ink` | Accent, unchanged | body text, row labels, primary outlines |
| `ink-2` | 66% Accent + 34% Background | secondary text, deks, inactive control glyphs |
| `ink-3` | 42% Accent + 58% Background | captions, unit labels, disabled, off-state text |
| `line` | 50% Accent + 50% Background | control borders |
| `line-2` | 24% Accent + 76% Background | row dividers, group rules, panel edges |
| `fill-1` | 9% Accent + 91% Background | control troughs, footer ground |
| `fill-2` | 16% Accent + 84% Background | field ground, stat blocks, inactive tab ground |
| `fill-3` | 27% Accent + 73% Background | the value cell inside a stepper, binding chips |
| `knock` | dark: Background · light: `#FBF8F2` | text/glyphs sitting on an Accent fill |

All mixes are in **OKLab**, not sRGB. sRGB mixing muddies the orange badly.

### Status ramp — hue anchored, lightness and chroma from Accent

```
danger = oklch(from Accent L C 27)    rebuy, over-power, destructive actions
warn   = oklch(from Accent L C 82)    protected rows, budget warnings
good   = oklch(from Accent L C 146)   unlocked, PTT ready, satisfied prerequisites
info   = oklch(from Accent L C 248)   inherited values, MW figures, jump range
```

This is the fix for defect 8. The hue is fixed so the meaning survives (red still reads
as danger); the lightness and chroma come from Accent so the colour always belongs to
the theme, including an arbitrary HUD matrix colour. **One meaning per colour** — blue
must stop meaning "numeric", "inherited" and "range" simultaneously; pick "info" and
use `hot` for emphasised numerics.

### Bloom

```
bloom = 0 0 (10 × amount)px  Accent at 34% alpha
```
Applied as an outer glow on: the diamond mark, the title, active tab fills, Accent-filled
switch blocks and segments, the primary button, slider fills, status dots.

Bloom is **off entirely on Daylight**. On dark themes it should be *more* pronounced than
1.1.0, not less — Elite's own HUD blooms hard. The rule that makes it work: bloom belongs
on `hot` and on Accent fills, never on `ink-2` or `ink-3`. Bloom on a bright core reads
as emission; bloom on a dim glyph reads as smear.

### Scanlines

1px black at 34% alpha every 3px, vertical repeat, over the whole panel,
`IsHitTestVisible="False"`, overall layer opacity 0.55. Dark themes only.

### Spacing scale

`4 · 8 · 12 · 16 · 24 · 32 · 48`. Nothing else. Panel padding 32. Group gap 30.
Row vertical padding 7–9.

### Sizes

| Thing | Value |
|---|---|
| Settings row min height | 52 |
| Minimum interactive target | 44 × 44 |
| Switch | 128 × 42 |
| Stepper / number / binding height | 44 |
| Slider track | 30 tall; handle 10 wide, overhanging 5 top and bottom |
| Gauge | 14 tall, 2px end caps |
| Label column | `minmax(0, 300px)` — must be allowed to shrink |
| Reset gutter | 44, **always reserved whether or not a reset is shown** |
| Panel content max width | 920 (settings) / 860 (dialog) / 1120 (kit) |

### Type scale

| Role | Family | Size / weight | Tracking | Colour |
|---|---|---|---|---|
| Window title | Saira Condensed | 23 / 700 | 0.22em | `hot` |
| Tab label | Saira Condensed | 15 / 600–700 | 0.16em | `ink-3` inactive, `knock` active |
| Screen title | Saira Condensed | 31 / 700 | 0.07em | `hot` |
| Group heading | Saira Condensed | 21 / 600 | 0.15em | `ink` |
| Subgroup / caption | JetBrains Mono | 11 / 400 | 0.20em | `ink-3` |
| Row label | Titillium Web | 16 / 400 | — | `ink` |
| Dek / helper | Titillium Web | 15–16 / 400 | — | `ink-2` or `ink-3` |
| Body in a report | Titillium Web | 16 / 400 | — | `ink-2` |
| Button label | Saira Condensed | 14–17 / 600–700 | 0.14em | varies |
| Numeric value | JetBrains Mono | 17–18 / 400 | — | `hot` |
| Machine text (logs, bindings, payload) | JetBrains Mono | 13–15 / 400 | — | `ink-2` / `hot` |

Three families, three jobs: **Saira Condensed** for chrome and headings, **Titillium Web**
for prose and labels, **JetBrains Mono** for anything the machine wrote (log lines,
journal, keybinds, counts, raw payload). If d47 already licenses a Eurostile-alike for
chrome, keep it and drop Saira Condensed — the rule that matters is one family per job.

---

## The control kit

This is the core of the handoff. Each control below replaces something in 1.1.0 that
could not be told apart from its neighbours.

### 1. Report — read-only text

**No box at all.** A 2px `line-2` rule on the left edge, 14px padding, `ink-2` text at 16px.

> A box is a promise you can type in it. 1.1.0 put read-only status ("11 ships, the
> oldest last seen about a day ago", "Data folder", "Attribution", "Version") inside the
> same bordered rectangle as the ask box, so every one of them looked editable.

### 2. Field — editable text

Ground `fill-2`, **bottom border 2px Accent**, no other borders. Height 44, padding 0 13.
Text `hot` 16px. Block caret: 9 × 21 Accent, blinking 1.1s steps(1).

Inset ground + one lit edge + block caret = unmistakably editable, and it is the period-
correct terminal idiom rather than another outline.

### 3. Actions — exactly three weights plus destructive

| Weight | Spec | Rule |
|---|---|---|
| Primary | Accent fill, `knock` text, clipped top-right corner `polygon(0 0, 100%-11px 0, 100% 11px, 100% 100%, 0 100%)`, padding 15 × 34, 17/700/0.16em, bloom | **One per surface.** |
| Normal | 1px `line` border, `ink` text, padding 11 × 20, 15/600/0.14em | any number |
| Quiet | no border, `ink-2` text, 1px dotted `line` underline, padding 11 × 4 | alternates and exits |
| Destructive | 1px `danger` border, `danger` text, padding 11 × 18 | deletes only |

### 4. Switch — the only two-state control

128 × 42, 1px border, 3px padding, 3px gap, two equal halves, each showing its word.

- **ON**: left half filled Accent with `knock` text and bloom; right half transparent with
  `ink-3` text; border `line`.
- **OFF**: right half filled `line-2` with `ink-2` text; left half transparent with `ink-3`
  text; border `line-2`.

State is **double-coded** — the lit block both moves *and* names itself, and the whole
control dims when off. This is what makes a column of thirty switches scannable, which
1.1.0's small square-in-a-box did not achieve. Animate the fill 120ms ease-out.

In Avalonia: retemplate `ToggleSwitch` (keep the control, replace the template) so
existing bindings are untouched.

### 5. Choice — up to six options

Segmented: 1px `line` frame, `fill-1` ground, 3px padding, 3px gaps. Each segment padding
9 × 15, Saira Condensed 14/600/0.12em. Selected = Accent fill, `knock` text, bloom.
Unselected = transparent, `ink-3`. Container must wrap.

Replaces the `◄ Information ►` spinner for short enums: all options visible, one click to
any of them, and it satisfies the no-ComboBox rule properly rather than hiding the option
set behind arrows.

### 6. Choice — more than six options

Stepper, height 44, 1px `line` frame: `◄` 44 wide · value cell · `►` 44 wide. Value cell
is `fill-3` ground, `hot` text 16px, padding 0 14, **`display:block` with ellipsis** (a
flex/grid box will hard-clip instead of showing the ellipsis — this was a real bug in the
first prototype pass).

Below the control, on one line, mono 12px: **position** (`3 / 6`, `ink-3`) and
**consequence** (`1.5 GB · English only · slower than Small`, `ink-2`).

The 1.1.0 spinner told you neither where you were in the list nor what the choice costs.

### 7. Amount — number with unit

Height 44, 1px `line` frame, width ~216: value (mono 18, `hot`, padding 0 14) · unit chip
(mono 12, `ink-3`, padding 0 11, left border `line-2`) · `▲` 40 · `▼` 40, each with a left
`line-2` border.

The unit lives in the control, so the label stops reading "Capture before the key, in
milliseconds" and becomes "Capture before the key".

### 8. Level — a settable slider

Track 30 tall, 1px `line`, `fill-1` ground. Fill inset 3px, Accent, bloom. Handle 10 wide,
`hot`, overhanging the track 5px top and bottom. **The numeric value is always shown** to
the right, mono 17, `hot`, min-width 52, right-aligned.

Where the underlying value is a named threshold rather than a number (1.1.0's "How D47
decides you are talking to it", "Provider", "Whose log it is", "Panel content"), **do not
use a slider at all** — use the segmented Choice with named stops. Those controls
currently show a bar fill with no value and are unreadable.

Hit area for the handle must be ≥44 wide even though the handle draws 10.

### 9. Gauge — reported, not settable

Deliberately a different shape from the slider: 14 tall, **no handle**, 2px `ink-2` end
caps, hatched fill `repeating-linear-gradient(90deg, colour 0 7px, transparent 7px 10px)`
in `danger` / `warn` / `good` as appropriate. Label and value on one mono line above it.

### 10. Binding

Mono 15 `hot` on `fill-3` with a 1px `line` border, padding 10 × 14, **`white-space:nowrap`**
so "button 11" never breaks across lines. Multiple chips sit in a wrapping row. A quiet
`CLEAR` action follows them. Never let the reset control overlap the clear control — this
happens in 1.1.0 on the Cancel row.

---

## Row layout (the association fix)

Every settings row:

```
grid-template-columns: minmax(0, 300px)  minmax(0, 1fr)  44px
align-items: center;  gap: 10px 20px;  min-height: 52px;  padding: 7px 0
border-top: 1px solid line-2
```

- Label left, **control immediately beside it** — not pushed to the far right edge.
- Both columns `minmax(0, …)` so they shrink instead of overflowing.
- The **44px reset gutter is always reserved**, empty when there is nothing to reset. This
  is what stops the control column going ragged down the page.
- Rows whose control can wrap must be allowed to grow; never clip.

### Protected rows

1.1.0 put a "protected" chip on ~60 rows. Replace with a **3px `warn` left border** on the
row plus 12px extra left padding, and **one** legend line under the screen title:
"Rows marked ▌ are protected — D47 will not change them on your say-so alone."

### Info affordance

Drop the per-row `ⓘ` entirely (40 identical 14px targets of pure noise). Put the
explanation in a dek under the group heading, or on hover/focus of the row label.

---

## Heading and navigation ranks

Four ranks, visibly different (defect 9):

1. **Screen title** — Saira Condensed 31/700/0.07em `hot`, with a one-line dek beneath in
   `ink-2` 16.
2. **Group** — Saira Condensed 21/600/0.15em `ink`, `white-space:nowrap; flex:none`,
   followed by a 1px `line-2` rule filling the remaining width. An optional dek line in
   `ink-3` 15 sits under it. (The nowrap matters: without it the heading wraps and the
   rule slices through the second line.)
3. **Subgroup** — JetBrains Mono 11/400/0.20em `ink-3`.
4. **Row label** — Titillium Web 16/400 `ink`, sentence case.

Navigation levels must also differ:

- **Level 1 (top tabs)** — sheared parallelogram, `polygon(13px 0, 100% 0, 100%-13px 100%, 0 100%)`.
  Active: Accent fill, `knock` text, bloom, padding 13 × 30. Inactive: `fill-2` ground,
  `ink-3` text, padding 11 × 26. Strip sits on a 2px `line` rule.
- **Level 2** — plain text row with a **3px Accent underline** on the active item and `hot`
  text; inactive `ink-3`, no underline. No boxes. Buttons need
  `white-space:nowrap; flex:none` and the strip wraps as a whole.
- **Level 3** — mono chips.

In 1.1.0 levels 2 and 3 were identical bordered rectangles, so depth was unreadable.

### Settings sidebar

Width `clamp(170px, 22%, 250px)`, right border `line-2`. Section (level 1): Titillium 16/600,
3px left border — Accent and `fill-2` ground when the active section, transparent otherwise.
Item (level 2): Titillium 15, 38px left indent; **active item is an Accent fill with `knock`
text** (reverse video), inactive `ink-2`. Colour here must encode *state*, not depth —
depth is carried by indentation alone. (1.1.0 used colour for depth in the sidebar and for
state everywhere else.)

---

## Screens

### A. Control Kit (reference sheet)

Not a shipping screen — a living spec of the kit above, and useful to keep as a debug page
behind a flag so the token derivation can be eyeballed per theme.

### B. Settings › Voice Input

**Purpose:** choose the microphone, the trigger, and the speech model.

Layout: sidebar + content column (max 920), padding 26 / 32 / 70 / 32.

Screen title "VOICE INPUT", dek "Changes apply as you make them. Rows marked ▌ are
protected — D47 will not change them on your say-so alone."

**Group: MICROPHONE** — dek "The input device, and how D47 decides you are talking to it."

| Row | Control | Notes |
|---|---|---|
| Microphone | Stepper | value "Logi 4K Stream Edition" |
| Push to talk | Binding `button 11` + CLEAR | protected |
| Cancel what it is saying | Bindings `Ctrl+Alt+X` `button 9` + CLEAR | protected; **the two chips and CLEAR must wrap, never overlap** |
| When it assumes you mean it | Segmented: NAMED ONLY / CAUTIOUS / BALANCED / EAGER | protected. Was an unlabeled slider with no readable value. |
| Cancel its own voice out of the mic | Switch ON | |
| Take the room out of what it hears | Switch ON | |
| Capture before the key | Amount `500` `ms` | reset shown |

**Group: SPEECH RECOGNITION** — dek "Which model turns speech into words, and where it runs."

| Row | Control | Notes |
|---|---|---|
| Speech model | Stepper "Medium (English only)" | sub-line `3 / 6` + "1.5 GB · English only · slower than Small" |
| Run the model on the GPU | Switch ON | reset shown |

**Group: CORRECTIONS**

| Row | Control |
|---|---|
| Names it has learned to hear | Report + destructive `FORGET THEM ALL` |

Footer voice hint, Titillium 15 italic `ink-3`: `Say: "stop hearing Farseer as far seer"`.
Keep these hints — they are the best thing in 1.1.0 — but they belong at the foot of the
*content*, not below a scroll boundary.

### C. Help Improve D47

**Purpose:** send the developer a scrubbed excerpt, with informed consent, every time.

This replaces a dialog that opened with five bullets of legal text, a 9,968-character raw
payload in a scroll box, and **five co-equal buttons** (Cancel / Save a file instead… /
Copy it for the report / Forget / Send it) with no primary.

Max width 860.

1. **Title** "HELP IMPROVE D47" + one dek sentence: "Send the developer a slice of what
   just happened, so a fix can be proved against it. Nothing leaves until you press
   **Send it**."
2. **Three plain consent lines** in a report block (2px `line-2` left rule):
   - "Your name and IDs are swapped for stand-ins. Other people's words are stripped."
   - "It goes to Directive 47 and nowhere else, and is deleted after thirty days."
   - "No standing consent — you are asked every single time."
   Then a quiet link, `READ THE FULL NOTE ↗` →
   `https://dseelinger.github.io/d47/donation-privacy.html`.
   **The full legal text does not belong in the app.** It lives at that URL.
3. **Group WHAT TO INCLUDE** — two rows (260px label column):
   - Journal history: Switch + segmented window `10 MIN / 1 HR / 6 HR / 12 HR`. The
     window group is dimmed to 35% opacity and disabled when the switch is OFF.
   - What I said out loud: Switch.
4. **Group WHAT WILL LEAVE** — four stat blocks in a wrapping row, `fill-2` ground,
   padding 14 × 16: mono 24 `hot` figure over a mono 11/0.16em `ink-3` caption.
   `74 LOG ENTRIES` · `0 JOURNAL EVENTS` · `2 NAMES REPLACED` · `9,968 CHARACTERS`.
   These are computed from the current toggle state and must update live.
5. **Disclosure** `▸ SHOW ME THE EXACT TEXT` → expands a mono 13 payload box, `fill-1`
   ground, 1px `line-2`, max-height 230, scrollable. Collapsed by default.
6. **Action bar** above a `line-2` rule: primary `SEND IT`; quiet `SAVE A FILE INSTEAD` and
   `COPY FOR A BUG REPORT`; spacer; `CANCEL` in `ink-3`; destructive `FORGET`.

The same treatment applies to **Settings › Privacy and this install**, which has the same
problem: three multi-paragraph read-only blocks. Reduce each to a two-line summary with a
disclosure for the detail, and link out for the rest.

---

## Interactions and behaviour

- **Switch** — click anywhere on the control toggles. Fill and border transition 120ms
  ease-out. Keyboard: Space / Enter.
- **Segmented choice** — click a segment to select. No transition on the fill (instant is
  correct for a discrete pick). Left/Right arrow moves selection when focused.
- **Stepper** — `◄`/`►` step by one and wrap at the ends. Position readout updates. Hold to
  repeat after 400ms at 8/s.
- **Amount** — `▲`/`▼` step; hold to repeat on the same timing. Typed entry allowed in the
  value cell (it is a field — give it the field's bottom-border treatment on focus).
- **Slider** — drag; the numeric readout updates continuously. Arrow keys step, Page
  keys step ×10.
- **Reset ↺** — appears only when the row differs from its default, in the reserved gutter.
  Click restores the default with the same 120ms transition.
- **Disclosure** — glyph `▸` → `▾`, height animates 160ms ease-out.
- **Hover** — controls lift one ramp step: `line` → Accent on borders, `ink-2` → `ink` on
  glyphs, `fill-1` → `fill-2` on grounds. Rows get a `fill-1` ground on hover.
- **Focus** — 2px Accent outline offset 2px. Must be clearly visible; it is the only
  keyboard affordance in the headset.
- **Disabled** — 35% opacity on the whole control, no pointer events.

### Responsive behaviour — this is the important one

The desktop window is wide, but **the VR panel is narrow** (512×280 and 1024×640 are both
offered in Settings › Screens) and the user can resize the desktop window down. Design to
the narrow case first. Concretely:

- Label and control columns both `minmax(0, …)`; never a fixed track width.
- `white-space: nowrap` on: tab labels, group headings, binding chips, position readouts,
  unit chips, switch state words. These are short and must never break mid-token.
- Everything else wraps. `text-wrap: pretty` on prose.
- Stepper value cells ellipsis; nothing else truncates.
- Segmented groups and chip rows wrap inside their frame.
- No `nowrap` and no fixed height on any box that holds prose.
- **Headset scale:** a global 1.2× scale factor for the VR surface, applied at the root.
  Everything is specified in DIPs so it scales cleanly.

## State

| State | Type | Screen |
|---|---|---|
| `theme` | enum of four | global |
| `accent` | colour, from the game's HUD matrix when `theme == HUD matrix` | global |
| `bloom` | 0–1.8 | global, dark themes only |
| `scanlines` | bool | global, dark themes only |
| `headsetScale` | bool / factor | VR surface |
| per-setting value + `isDefault` | varies | drives the reset gutter |
| `includeJournal`, `journalWindow`, `includeSpoken` | bool / enum | Help Improve |
| `rawExpanded` | bool | Help Improve |
| payload counts | computed from the three above | Help Improve |

## Implementing the ramp in Avalonia

Avalonia has no `color-mix()`. Build a small `ThemeColors` service:

1. Convert Accent and Background from sRGB to **OKLab** (standard sRGB → linear → LMS →
   OKLab conversion).
2. For each ramp token, lerp in OKLab by the percentage in the token table, convert back
   to sRGB, clamp.
3. For the status ramp, convert Accent to **OKLCh**, keep L and C, substitute the anchored
   hue (27 / 82 / 146 / 248), convert back.
4. Publish every result as a `SolidColorBrush` in the application resources and reference
   them with `{DynamicResource}` throughout, so a HUD-matrix change recolours the whole
   panel by reassigning resources — no per-control work, no rebuild.
5. Recompute on: theme change, HUD-matrix change, and app start.

Bloom: a `DropShadowEffect` with `BlurRadius = 10 × amount`, zero offset, Accent at 34%
alpha, bound to a `DynamicResource`. Gate it and the scanline layer on `IsDarkTheme`.

No-ComboBox: if any `ComboBox` remains in the XAML, replace it with the segmented Choice
(≤6 items) or the stepper (>6). Note that 1.1.0 still has real dropdowns on the Model,
Output device and Voice rows — those violate the rule and should be converted too.

## Assets

None. The diamond mark is a 16×16 square rotated 45°. All glyphs are text
(`◄ ► ▲ ▼ ↺ ▸ ▾ ▌ ↗`) — if any of these render poorly in the headset at size, replace them
with vector paths rather than shrinking them.

Fonts: Saira Condensed, Titillium Web, JetBrains Mono (all SIL Open Font License, Google
Fonts). Ship them with the app rather than relying on system fallback.

## Files

- `D47 Panel v2.dc.html` — the full design reference. Three views: Control Kit,
  Settings › Voice Input, Help Improve D47. Tweaks panel drives theme, accent, bloom,
  scanlines and headset scale.

## Suggested order of work

1. `ThemeColors` service and the resource dictionary — nothing else can be right until the
   ramp derives correctly across all four themes.
2. The type scale and spacing constants.
3. The control templates, in this order: Switch, Choice (segmented), Stepper, Amount,
   Field, Report, Level, Gauge, Binding, Buttons.
4. The settings row layout, including the reserved reset gutter and the protected border.
5. Heading and navigation ranks.
6. Retrofit Settings › Voice Input as the pilot screen.
7. Help Improve D47 and Privacy and this install.
8. Everything else, screen by screen, against the kit.
