# Corrections 01 — first implementation pass is off-spec

Read this with `README.md`. Evidence: `evidence/01-transcript-first-attempt.png`.

---

## Paste this to start

> The first pass does not match the handoff. Two process problems caused it:
>
> 1. You implemented a screen (Transcript) that the handoff does not spec, before building
>    the control kit the handoff says to build first. Stop doing that. Work the
>    "Suggested order of work" list in `README.md` in order: `ThemeColors` service, type and
>    spacing constants, then the ten control templates, then the row layout, then the
>    heading/nav ranks, then Voice Input, then Help Improve. Do not style any other screen
>    until those exist and I have signed off on the Control Kit page.
> 2. You designed from an impression of the aesthetic ("amber outlines, angular corners")
>    instead of from the numbers. Every colour, size, padding and polygon in the handoff is
>    a literal value. Use the literal values. If a value you need is not in the handoff,
>    ask me — do not invent one.
>
> The single biggest miss: **the derived colour ramp is not implemented.** Everything in the
> screenshot is one flat saturated Accent. Hierarchy in this design comes almost entirely
> from the eleven-step ramp (`hot`/`ink`/`ink-2`/`ink-3`/`line`/`line-2`/`fill-1`/`fill-2`/
> `fill-3`/`knock`) plus the four-hue status ramp. Without it, every element has equal
> weight and no amount of layout work will fix the screen. Build `ThemeColors` first and
> verify it on the Control Kit page before touching anything else.
>
> The second biggest miss: **you are drawing 1px Accent outline boxes around everything.**
> That is defect 1 — the exact thing this redesign removes. In the new kit, borders are rare
> and dim. Identity comes from *ground fills, one lit edge, and reverse video*. See the
> forbidden list below.

---

## Defect list against the screenshot

| # | What I see | What the handoff says |
|---|---|---|
| 1 | Every element is one saturated Accent. No dim tier anywhere; borders are as bright as text. | The derived ramp, §"Design tokens". Borders are `line` (50% Accent) or `line-2` (24%) — both much dimmer than `ink`. Secondary text is `ink-2`, captions `ink-3`. |
| 2 | Status dot on the PTT bar is Accent orange. | `PTT READY` is a `good` state → `oklch(from Accent L C 146)`. The status ramp is not implemented. |
| 3 | No bloom, no scanlines. | Bloom `0 0 (10 × amount)px Accent @34%` on the mark, title, active tab fill, Accent fills, status dots. Scanlines 1px black @34% every 3px, layer opacity 0.55. Dark themes only. |
| 4 | Level-2 strip (`In Ship / Log File / Journal File`) is drawn as a bordered segmented control with an Accent-filled active segment. | That is the **segmented Choice control**, which is for *setting a value*, never for navigation. Level 2 nav is a **plain text row**: no frame, no boxes, no grounds — active item `hot` with a 3px Accent underline, inactive `ink-3`. Never reuse a control shape for navigation. |
| 5 | Inactive top tabs are bare text with icons; only the active tab has a shape. | Level 1 tabs are *all* shapes on a 2px `line` rule. Inactive: `fill-2` ground, `ink-3` text, padding 11 × 26. Active: Accent fill, `knock` text, bloom, padding 13 × 30. Both sheared `polygon(13px 0, 100% 0, calc(100% - 13px) 100%, 0 100%)`. |
| 6 | Tabs have pictorial icons (speech bubble, gear, compass…). | The asset list is `None`. The only glyphs in this design are `◄ ► ▲ ▼ ↺ ▸ ▾ ▌ ↗`. Remove every pictorial icon, including the two unlabelled ones floating near the level-2 strip and above the PTT bar. If an action needs an affordance it gets a *word*. |
| 7 | Search field is a full-width floating underline, wider than its own ground, ~34px tall, with a sentence-case grey placeholder. | Field = `fill-2` ground, **bottom border 2px Accent and no other border**, height 44, padding 0 13, text `hot` 16, block caret 9 × 21 Accent blinking 1.1s `steps(1)`. The underline is the width of the ground, not the width of the row. |
| 8 | The transcript is a large 1px-outlined rectangle with a notched corner. | A box is a promise you can type in it (§"1. Report"). The transcript is read-only: no container border at all. |
| 9 | Each message is a bordered box with a clipped corner. | Clipped corners belong to **one** thing: the primary button, top-right, 11px. Nothing else in the design is clipped or notched. A message is: Accent-filled `D47` badge (`knock` text — reverse video, correct) · mono 13 `ink-3` timestamp · body Titillium 16 `ink-2`, on a 2px `line-2` left rule, 14px padding. No border, no clip. |
| 10 | Message body text is a fourth font. | Three families, three jobs. Saira Condensed = chrome/headings. Titillium Web = prose and labels. JetBrains Mono = anything the machine wrote. Nothing else. |
| 11 | The ask box is ~90px tall with a ~28px Saira caps placeholder reading `WHAT CAN YOU DO?`. | It is a Field: 44 tall (multiline may grow, but starts at 44), placeholder is prose → Titillium 16 `ink-3`, sentence case. Placeholder text is not a headline. |
| 12 | PTT bar is a full-width 1px Accent outlined rectangle. | Footer ground is `fill-1`, no border. Caption text mono 11/0.20em `ink-3`; the dot is `good`. |
| 13 | ~350px of dead vertical space above the messages, and padding that is not on any scale. | Spacing scale is `4 · 8 · 12 · 16 · 24 · 32 · 48` and nothing else. Panel padding 32, group gap 30, row padding 7–9. The transcript list fills its space and scrolls; it does not bottom-anchor inside a fixed frame. |
| 14 | Level-2 labels are sentence case Titillium inside the segments; tab labels are Accent-coloured. | Tab label: Saira Condensed 15/600–700/0.16em, `ink-3` inactive / `knock` active. Level-2 nav label: same Saira treatment, `hot` active / `ink-3` inactive. |

---

## Forbidden patterns — check every screen against this

Do not ship any of these. They are all present in the first pass.

1. **No 1px Accent-coloured outline as a general container treatment.** If you are drawing a
   rectangle border, it is either `line` (an interactive control frame: switch, segmented,
   stepper, amount, binding, normal button) or `line-2` (a divider or panel edge). Full
   `Accent` borders exist nowhere in the kit.
2. **No border on read-only content.** Reports, transcripts, status lines, stat blocks:
   ground fill or left rule only.
3. **No clipped or notched corners** except the primary button's top-right 11px, and no
   shear except the level-1 tab parallelogram.
4. **No pictorial icons.** Text glyphs only, from the nine listed.
5. **No control shape used for navigation**, and no navigation treatment used for a control.
6. **No ComboBox.** ≤6 options → segmented Choice. >6 → stepper.
7. **No fixed hex for anything accent-related.** Every brush comes from `ThemeColors` via
   `{DynamicResource}`, because HUD-matrix mode reassigns Accent at runtime.
8. **No slider for a named threshold.** Named stops → segmented Choice.
9. **No unlabelled slider.** Every slider shows its numeric value, mono 17 `hot`, min-width 52,
   right-aligned.
10. **No bare glyph button under 44 × 44.**

---

## Transcript screen — spec (it was missing; this is why you improvised)

The handoff spec'd three surfaces only. Here is Transcript so it is checkable. Derive
everything from the kit; invent nothing.

**Frame.** Panel padding 32. No outer border inside the window chrome — the window edge is
the edge. Column max width: none (the transcript uses the full width), but message body
text caps at 76ch for readability.

**Level-2 nav.** `IN SHIP · LOG FILE · JOURNAL FILE` as a plain text row, gap 24, Saira
Condensed 15/600/0.16em. Active `hot` + 3px Accent underline; inactive `ink-3`. Strip wraps
as a whole; each label `white-space: nowrap; flex: none`. No frame, no ground.

**Search.** A Field, width `clamp(240px, 32%, 420px)`, on the same row as the nav, pushed
right, 16px gap. Placeholder "Search this page", Titillium 16 `ink-3`. A quiet `CLEAR`
action appears inside the row only when there is a query.

**Row rule.** 1px `line-2` under the nav/search row, 16px below it.

**Message list.** Vertical stack, gap 16, fills the remaining height, scrolls, newest at the
bottom, scroll pinned to bottom unless the user has scrolled up. Empty state: one centred
`ink-3` Titillium 16 line, no box.

**Message.** 2px `line-2` left rule, padding 14 left, no border, no ground, no clip.
- Header row, gap 10, align baseline: speaker badge · optional intent · timestamp.
  - Speaker badge — reverse video, Accent fill, `knock` text, Saira Condensed 13/700/0.14em,
    padding 3 × 7, bloom. `D47` uses Accent; the commander's own turns use a `line-2` fill
    with `ink-2` text (dimmer — the assistant is the voice that matters here).
  - Intent (`continuity.resume`) — JetBrains Mono 13 `ink-3`. Optional.
  - Timestamp — JetBrains Mono 13 `ink-3`, `white-space: nowrap`.
- Body — Titillium Web 16 `ink-2`, `text-wrap: pretty`, 8px below the header. Machine text
  inside a message (log lines, journal events, keybinds) switches to JetBrains Mono 14
  `ink-2`, never Titillium.
- Hover: row ground `fill-1`, and the left rule lifts `line-2` → `line`.

**Footer.** Two stacked bands, ground `fill-1`, no borders between them except a 1px `line-2`
rule above the whole footer.
- Status band, height 36, padding 0 16: an 8px `good` dot with bloom, then `PTT READY` in
  JetBrains Mono 11/0.20em `ink-3`. Listening state: dot `warn`, text `LISTENING`. Error:
  dot `danger`. The dot is the only colour change — do not recolour the text.
- Ask band: the Field (ground `fill-2`, 2px Accent bottom border, min-height 44, grows to
  3 lines then scrolls), plus a primary `SEND` button — Accent fill, `knock` text, clipped
  top-right 11px, Saira 17/700/0.16em, bloom. One primary per surface; this is it, so the
  search row's actions stay quiet.

---

## How to check your own work before showing me

1. Open `D47 Panel v2.dc.html` in a browser, switch it to the Control Kit view, and put your
   build beside it at the same window width. Every control must be the same size, same
   grounds, same border weights, same text colours.
2. Cycle all four themes in both. HUD matrix with an arbitrary Accent (try a cyan and a
   magenta) is the real test of the ramp — if anything stays orange, you have a fixed hex.
3. Resize to **924 × 640 and 512 × 280**. Nothing may hard-clip. Only stepper value cells
   ellipsis. Group headings, binding chips, tab strips and segmented groups wrap.
4. Toggle bloom 0 → 1.8 and scanlines off → on. Confirm both are dead on Daylight.
5. Grep the XAML for `ComboBox`, for hex literals (`#`) outside the four theme roots, and for
   `CornerRadius` — all three should come back empty.

---

## Screenshots that would help me review

If you want another round of this, the useful set is: Control Kit (Elite orange and HUD
matrix with a cyan accent), Settings › Voice Input at full width and at 924px, Help Improve
with the payload collapsed and expanded, and Transcript. One image per surface, full window,
no cropping.
