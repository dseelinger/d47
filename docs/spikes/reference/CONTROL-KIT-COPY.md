# Control Kit — verbatim copy (for #374)

Every string below is taken literally from `D47 Panel v2.dc.html`. Use them exactly:
same words, same punctuation (note `·` U+00B7 middot, and the `—` em dashes in captions),
same capitalisation. Captions are authored uppercase; do not rely on a text-transform.

## Header

**Title** (Screen rank)

```
CONTROL KIT
```

**Intro paragraph** (Titillium 16, `ink-2`, max-width 640)

```
Every control says what it is and what it is set to, without being read twice. Reverse video carries state, not outlines — a filled block is the terminal idiom and it is the thing that still reads at arm's length in a headset.
```

## Spec cards (three, in this order)

| Caption | Body |
|---|---|
| `SPACING` | `4 · 8 · 12 · 16 · 24 · 32 · 48. Nothing else.` |
| `ROW` | `52px settings · 44px minimum target` |
| `COLUMNS` | `Label 300px · control next to it · reset gutter fixed` |

Caption: JetBrains Mono 11, letterspacing 0.16em, `ink-3`, 6px below.
Body: Titillium 15, `ink`. Card: 1px `line-2` border, padding 16 × 18, grid gap 12.

## Section headings

```
TELLING THINGS APART
STATUS, DERIVED FROM ACCENT
FOUR RANKS OF HEADING
```

Saira Condensed 21/600/0.15em, `ink`, `flex:none`, `white-space:nowrap`, followed by a
1px `line-2` rule filling the remaining width, gap 16, 20px below.

## Grid cells — caption and note

Grid order is reading order, three columns, `minmax(310px, 1fr)`, gap 28.
Caption: JetBrains Mono 11, letterspacing **0.18em**, `ink-3`, 10px below the control.
Note: Titillium 14, `ink-3`, 10px above (8px for CHOICE — MANY OPTIONS), `text-wrap: pretty`.

| # | Caption | Note |
|---|---|---|
| 1 | `REPORT — READ ONLY` | `No box at all. A box is a promise you can type in it.` |
| 2 | `FIELD — EDITABLE` | `Inset ground, one lit edge, block caret.` |
| 3 | `ACTIONS — THREE WEIGHTS` | `One filled primary per surface. Never five equals.` |
| 4 | `SWITCH — TWO STATE` | `Double-coded: the lit block moves *and* names itself. Scannable down thirty rows.` |
| 5 | `CHOICE — FEW OPTIONS` | `Up to six: show them all. No ComboBox needed, and nothing is hidden behind arrows.` |
| 6 | `CHOICE — MANY OPTIONS` | `Position shown, consequence shown. The old spinner told you neither.` |
| 7 | `AMOUNT — NUMBER + UNIT` | `The unit lives in the control, so the label stops saying "in milliseconds".` |
| 8 | `LEVEL — SETTABLE` | `A handle that overhangs the track, and the number always present.` |
| 9 | `GAUGE — REPORTED, NOT SETTABLE` | `Hatched, capped, no handle — it can never be mistaken for the slider above.` |
| 10 | `BINDING` | `Bindings are machine text, so they are mono — and they wrap instead of colliding.` |

Note on #4: `and` is italic in the reference (`<em>`). Keep the emphasis; if italics are
unavailable in Saira/Titillium as shipped, use the regular face rather than faking a slant.

Note on #7: the quotation marks around `"in milliseconds"` are straight quotes in the
reference. Keep them straight.

## Sample values inside the cells

These are content, not chrome, but they are part of the verbatim reference:

- Report body: `11 ships, the oldest last seen about a day ago.`
- Field: `Diaguandri` (with the block caret after it; this field takes focus on open)
- Actions: `SEND IT` (primary) · `RESCAN` (normal) · `CANCEL` (quiet) · `FORGET ALL` (destructive) · plus a disabled example on the second line
- Switch: `ON` / `OFF`, shown twice — first lit on, second lit off
- Choice few: `NONE` `WARN` `INFO` `DEBUG` `TRACE`, with `INFO` selected
- Choice many: `Medium (English only)`, position line `3 / 6`, consequence `1.5 GB · runs on the GPU`
- Amount: `500` with unit `ms`
- Level: fill at 58%, value `0.85`
- Gauge: label `POWER`, readout `25.04 / 22.93 MW · 109%` in `danger`
- Binding: `Ctrl+Alt+X` · `button 9` · `CLEAR`

## Status section

**Paragraph** (Titillium 16, `ink-2`, max-width 680, 18px below)

```
Status hues are anchored (danger 27°, warn 82°, good 146°, info 248°) but take their lightness and chroma from Accent. Switch theme above and watch them follow — no fixed hex survives the HUD matrix.
```

**Five bars**, contiguous (gap 2), each `flex:1; min-width:150px`, padding 14 × 16,
JetBrains Mono 12/0.12em, `knock` text on the named fill:

```
ACCENT
DANGER · rebuy
WARN · over budget
GOOD · unlocked
INFO · inherited
```

## Heading ranks

One block behind a single 2px `line-2` left rule, padding-left 22, no per-rank rules:

| Text | Treatment |
|---|---|
| `SCREEN TITLE` | Saira Condensed 31/700/0.07em, `hot` |
| `GROUP, WITH A RULE` | Saira Condensed 21/600/0.15em, `ink`, 14px above |
| `SUBGROUP` | JetBrains Mono 11/0.2em, `ink-3`, 14px above |
| `Row label, sentence case` | Titillium 16, `ink`, 10px above |
