# d47 — Elite look and feel (brief 03, for /architect)

Paste everything below the line into Claude Code after `/architect`.

---

## Read this first: what this brief is and isn't

**In one line: this brief specifies UI, UX and look and feel. It does not specify d47's capabilities.**

This brief is about **appearance and standard control mechanics only.** It covers colour, type, spacing, control shapes, hover/focus/pressed/selected/disabled states, and the ordinary behaviour of standard controls: a checkbox toggles, a stepper steps, a copy glyph copies.

**It does not specify what d47 does.** Screens, data, wording, labels, numbers, providers, the list of engineers, what a button triggers, when something refreshes, what gets saved: all of that in the mockup is **placeholder**, invented to fill the frame. Some of it came from screenshots and some of it is made up. Where the mockup and the real app disagree about content or behaviour, **the C# code is right and the mockup is wrong.** Don't add, remove, rename or re-wire features to match the mockup. Restyle what exists.

Specifically:

- **The mockup's specific labels are placeholders.** Don't copy strings like "LOOP STATE · LISTENING FOR THE GAME", "PIN A BLUEPRINT WITH THEM", "SHOW 23 MORE" or the spend breakdown rows into the app. Use the app's own strings, restyled per the type rules below. (Changing the case of existing strings to uppercase for chrome is in scope.)
- **The mockup's layout is illustrative, not a new information architecture.** It groups carrier stats into tiles, puts engineer prerequisites in rows, and puts the session spend in the status bar. Apply the *patterns* (stat tile, list row, prerequisite ladder, modal) to the data each screen already has. If a screen has no data for a pattern, don't invent it.
- **Where this brief says "a control does X",** it means the standard mechanic of that control type. For example, "a checkbox toggles its bound boolean", not "this checkbox should control Y".
- **If a restyle would change behaviour, stop and ask.** For example: replacing a ComboBox needs a new binding shape, or a toggle is bound to something that isn't a plain boolean.

This brief supersedes `ARCHITECT-TRANSCRIPT-02.md` and the visual parts of `D47-SPEC.md` (tokens, bloom, control kit, nav ranks). It keeps the spec's non-visual rules:
- Everything is specified in DIPs.
- No ComboBox. Choose by list: Segmented tiles for 4 or fewer options, the Stepper for 5–7 fixed options, and the Dropdown tile only for long, variable-length lists (audio devices, voices).
- The per-row reset gutter is always reserved.
- Nothing hard-clips.
- The narrow VR panel sizes must work.
- Headset scale is 1.2×.

## Reference files

| File | What it is |
|---|---|
| `D47 Elite v4.dc.html` | **The visual reference.** Open it in a browser. The Theme control switches the 4 themes; the Screen control switches between Transcript, Fleet · Carrier and Engineers · Detail. The SPEND button opens the modal pattern. |
| `ELITE-COLOUR-NOTES.md` | What Elite actually does, observed from about 40 screenshots. It's the rationale behind every rule below. |
| `D47-VS-ELITE.md` | An audit of d47 1.6.3 against Elite, listing the defects this brief fixes. |

## 1. Principle

d47 should look like it belongs inside Elite Dangerous, not have a look of its own. When in doubt, do what the game's station and holo-panel UI does.

## 2. Colour: meaning first

| Role | Elite default | Used for |
|---|---|---|
| `a` (accent) | `#FF7A1A` | Values and attributes. Interactive text. Rules, frames, gauges, the checkbox. |
| `white` | `#EDE9E3` | **Identity and speech:** screen titles, names of things (ships, engineers, systems, carriers), message bodies, list-row names. |
| `grey` | `#A09B94` | Labels in stat tiles, helper prose, "Say: …" hints. |
| `grey2` | `#6E6A65` | Placeholders, disabled text, the lowest-priority metadata. |
| `bg` | `#070606` | The window ground. |
| `bar` | `#0F0D0C` | The title bar and modal ground. |
| `slab` | `#232120` | Neutral grey tile for **read-only data** (stat tiles, prerequisite rows). |
| `tile` | 20% `a` + `bg` (OKLab) | **Interactive surface at rest:** buttons, list rows, tabs, checkbox rows, stepper arrows. |
| `tile2` | 30% `a` + `bg` | Hover on a tile that isn't going solid. |
| `line` | 55% `a` + `bg` | Rules under subheadings, scrollbars, tooltip borders. |
| `line2` | 28% `a` + `bg` | Dividers between bands. |
| `knock` | `#140800` | Text on a solid `a` fill. |
| `brown` | `#6B2F00` | Secondary text inside a selected (solid `a`) row. |
| `cyan` | `#33D6E8` | **Yours / here / ready:** your own chat turns, the current-system pin, PTT READY, focused input outline. |
| `blue` | `#1FA8F5` | **Confirmed / met:** ✓ MET, verified. |
| `red` | `#F0343F` | **Destructive, hostile, locked, error.** |
| `yellow` | `#F5D426` | **Stored / capacity:** storage and capacity gauges and counts. |

Rules:
- Only `a` appears on interactive chrome. No colour appears without one of the meanings above.
- Don't use colour decoratively, and never reuse a hue for a second meaning.
- **Neutrals (`white`, `grey*`, `slab`, `bg`, `bar`) are never derived from the accent.** Elite's greys are not orange-tinted.

**System names:**
- The commander's **current system** is `cyan`, wherever it appears (transcript, stat tiles, lists).
- Any other system is `a` (orange).
- Whether a given name is the current system comes from the app's own state.

**Status ladder,** replacing MET / NOT MET / UNKNOWN colours:
- `✓ MET` is `blue`.
- In progress is `a` with its gauge.
- `? UNKNOWN` is `white`.
- Not met or unavailable is `grey`.

## 3. Themes (four)

| Theme | Rule |
|---|---|
| **Elite default** | The table above. Glow and scanlines on. |
| **Dark (VS Code)** | VS Code Dark+ values: `bg #1E1E1E`, `bar #2D2D2D`, `slab #252526`, `white #D4D4D4`, `grey #9D9D9D`, `a #3794FF`, `cyan #4EC9B0`, `blue #569CD6`, `red #F48771`, `yellow #DCDCAA`, `knock #0B1F33`. **No glow, no scanlines.** |
| **Light** | `bg #F4F1EB`, `bar #E6E1D8`, `slab #E4DFD6`, `white` (ink) `#1C1917`, `grey #5E5852`, `a #B84E00`, `knock #FFFFFF`, `cyan #007C8A`, `blue #0B6BCB`, `red #C8192B`, `yellow #8A6D00`. **No glow, no scanlines.** |
| **Match my Elite colours** | Read the player's HUD colour matrix and apply it to the **coloured** tokens only (`a`, `knock`, `brown`, `cyan`, `blue`, `red`, `yellow`), exactly as the game does: `out = r·MatrixRed + g·MatrixGreen + b·MatrixBlue`, clamped. Neutrals stay fixed. Glow and scanlines on. Where the matrix comes from, and when it's re-read, is an implementation decision. |

The derived tokens (`tile`, `tile2`, `line`, `line2`) are always mixed in OKLab from that theme's `a` and `bg`. Publish every token as a `DynamicResource`, so a theme switch recolours without rebuilding anything.

## 4. Type

| Family | Job |
|---|---|
| **Saira** (normal width, 400–700) | All chrome: tabs, buttons, headings, labels, values in tiles. Chrome strings are **uppercase**. |
| **Sintony** (400/700) | Prose: message bodies, helper text, descriptions. Sentence case. |
| **JetBrains Mono** | Machine text only: log lines, journal JSON, intents, timestamps in the transcript, per-turn cost lines. |

- **Letter-spacing:** 0.04–0.08em on uppercase chrome only. **None on names, values or prose.** The wide tracking in 1.6.3 is the main thing that makes it read as not-Elite.
- **Screen title:** Saira 28/500, `white`, with an orange context line above it (13/500, e.g. `FLEET CARRIER · BNH-T2F`) and a 1px `a` rule below.
- **Section heading:** Saira 14–15/600, uppercase, `white`, with a 1px `a` (or `line`) rule below.
- **Minimum body size:** 16. Label and caption minimum: 12.

## 5. Controls: shapes and standard mechanics

Every interactive element is a **filled tile, not an outline.** Tiles are separated by 2px gaps.

| Control | At rest | Hover / focus | Pressed or selected | Disabled |
|---|---|---|---|---|
| **Button** (every button, including SEND) | `tile` ground, `a` text, Saira 13–15/600 uppercase, height 36–44 | Solid `a`, `knock` text | Solid `a` | `slab` ground, `grey2` text |
| **Destructive button** | `tile` ground, `red` text | Solid `red`, `white` text | Solid `red` | as above |
| **Tab (level 1)** | `tile`, `a` text, height 40, flat (no shear, no clip) | Solid `a` | Solid `a`, `knock` text, soft glow (glow themes only). The strip sits on a 2px `a` rule. | — |
| **Sub-nav (level 2)** | Text only, Saira 14/600, `a` | `white` | `white` + 2px `a` underline | — |
| **List row** | `tile`: name `white`, secondary `a`, min-height 44 | `tile2` | **Solid `a`:** name `knock`, secondary `brown` | `slab`, `grey` |
| **Checkbox** (replaces every toggle switch) | A 16px square, 2px `a` outline, empty, inside a `tile` row with its label | `tile2` row | Checked: an 8px solid `a` square centred in the outline | `grey2` outline |
| **Stepper** (5–7 fixed options) | `◄` tile · value on `slab` in `white` · `►` tile, with the position readout (`3 / 7`) inside the value box, right-aligned in `grey2` JetBrains Mono 11 | The arrow tile goes solid `a` | — | `grey2` arrows |
| **Dropdown** (long, variable-length lists only: audio devices, voices) | A `tile` with the value in `white` and a `▼` in `a` | `tile2` | Opens a list below it: `bar` ground, 1px `a` frame, 2px gaps; the current option is solid `a` with `knock` text | `grey2` text |
| **Text input** | 1px `a` outline, transparent ground, `white` text. An optional channel prefix in `a` (`TO [D47]:`). | **Focused: outline turns `cyan`** | — | `grey2` outline |
| **Glyph button** (copy, loop state, etc.) | A square `tile` holding a glyph in `a`. **Drawn at 32–36; the hit target is padded to 44 × 44.** | The tile goes solid `a`, the glyph `knock`, **and a label appears flush beside it** (`bg` ground, 1px `a` border, `white` Saira 12, uppercase) naming what the button does. On press the label may confirm (`COPIED`). | — | `grey2` glyph, no label |
| **Stat tile** (read-only) | `slab`, label `grey` 12/500 uppercase, value `a` 16–18 (or `white` when the value is a name, `cyan` when it means "here") | none: it is not interactive | — | — |
| **Gauge** | A 6px track (`tile`, or a dim mix of its own colour) with a solid fill. `a` for progress, `yellow` for capacity, `red` over limit. Value right-aligned in the label line. | — | — | — |
| **Modal** | `bar` ground, 1px `a` frame. Header: orange context line, `white` title, `a` rule. A key figure may sit at top right. Body scrolls. Footer: a `line2` rule, then button tiles. Scrim: black at 72%. Closes on CLOSE / Esc / scrim click. | — | — | — |

The copy glyph is two overlapping 10px squares, 2px strokes. Other glyphs keep their current source. This brief doesn't add icons.

Standard mechanics only:
- Click and Space/Enter activate.
- A checkbox toggles its bound boolean.
- A stepper steps and wraps by the existing rules.
- Arrow keys move within tab strips and lists.
- Focus is always visible: a solid fill for tiles, cyan for inputs.
- Hover-reveal labels appear with no delay and no animation.
- **No gray platform defaults anywhere.** Every control gets its own theme with explicit `:pointerover`, `:pressed`, `:focus` and `:disabled` setters.

## 6. Layout and chrome

- **The window has a 1px `line` frame** on all four sides. This fixes "can't find the app edge on a black desktop". The title bar is 44 tall on `bar`, with a `line2` rule below it.
- **Content padding:** 20 top, 28 sides, 24 bottom. Spacing scale: 2 (tile gaps) · 4 · 8 · 12 · 16 · 20 · 24 · 28.
- **Regions fill the space they're given, or size to their content.** Never a fixed-height scrolling region with empty space below it (Materials and Routing in 1.6.3).
- **Transcript** (pattern only):
  - **SMS convention:** D47's turns sit on the left, with a 3px `a` bar on their left edge. The commander's turns sit on the right, with a 3px `cyan` bar on their right edge and a faint cyan ground (7% `cyan` + `bg`). Each turn is capped at 72% of the list width. Text inside a turn is always left-aligned.
  - Header: the speaker name in the same colour, then intent and delivery in mono `grey`, then the time right-aligned in mono.
  - The body is Sintony 16 `white`, capped at 76ch.
  - Delivery tags (`[calm]`) never appear in the body text. Show them in the header.
  - The list is pinned to the bottom.
- **Glow** (glow themes only):
  - A soft halo on the brand mark, the window title, the active tab and status dots.
  - Status dots glow in their own colour.
  - Nothing else glows.
- **Scanlines** (Elite default and Match themes only): a 1px black line at 30% opacity every 3px, overall layer opacity 0.5, not hit-testable.

## 7. Suggested order

1. The token service and the four themes. Verify by cycling themes on one screen.
2. The window frame and title bar.
3. Control themes, in this order: Button, Tab, List row, Checkbox, Stepper, Text input, Glyph button, Stat tile, Gauge, Modal.
4. Replace every toggle switch with the checkbox. Replace every remaining ComboBox: Output device and Voice become the Dropdown tile, and fixed option lists become the Stepper (5–7 options) or Segmented tiles (4 or fewer). **Keep the existing bindings.** Ask if a binding doesn't fit.
5. Restyle the screens one at a time. Transcript first, then Fleet, Engineers, Checklist, Routing, Adventures, Settings.

## 8. Acceptance

1. **Side-by-side check:** put each restyled screen next to `D47 Elite v4.dc.html` at 1280 × 860. Ground colours, text tiers (white / orange / grey), tile shapes, gaps and states must match. **Content and behaviour are not part of this comparison.**
2. **Theme sweep:** cycle all four themes.
   - Dark and Light show no glow and no scanlines.
   - Match my Elite colours with a blue matrix leaves no orange anywhere.
   - Neutrals stay neutral in every theme.
3. **Code checks:**
   - No `ToggleSwitch` or `ComboBox` remains. The Dropdown tile is its own control, not a restyled `ComboBox`.
   - No hex literal outside the theme table.
   - No `CornerRadius`.
4. **Glyph buttons:**
   - Every glyph button reveals its label on hover and on keyboard focus.
   - Every hit target is at least 44 × 44.
5. **Narrow sizes:** at 924 × 640 and 512 × 280 nothing hard-clips, tile rows wrap, and stat grids drop columns.
6. **Behaviour is unchanged:** every feature, binding and command that existed before the restyle still exists and does the same thing.
