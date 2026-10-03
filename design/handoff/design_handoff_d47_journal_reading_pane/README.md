# Handoff: the Journal reading pane (2026-10-03)

Build issue: [#817](https://github.com/dseelinger/d47/issues/817). Design issue: [#13](https://github.com/dseelinger/d47/issues/13).
Canvas: https://claude.ai/artifact/FrydUjFSLKnLoMdgLpcKfW (private to the maintainer).

## What changed
Transcript › Journal file keeps its two panes. The right pane stops showing the selected event's
pretty-printed JSON. It shows the event in the game's own words, with the JSON one fold away. The
left list, the search box, the noise filter, Raw Journal and the headset are unchanged.

The pane draws, top to bottom, in this order:

1. **Headline**: the sentence the list shows for the event.
2. **Time**: local `HH:mm` and the age (`23:11 · 4 minutes ago`).
3. **Rows**: a label and one or more values.
4. **What this means**: the paragraph from `JournalExplainers.For(kind)`, folded or open. A kind with no paragraph has no band at all.
5. **Every field**: folded by default. Opened, it lists every field of the event in file order.

**Start here:** read this README, then work through `CHECKLIST.md`, comparing the build against `screenshots/`.

## About the design files
`D47 Journal Reading Pane.dc.html` is a **design reference made in HTML**: a clickable prototype of the
look and behaviour, not production code. Recreate it in the Avalonia app with the existing kit styles.
Open it in a browser with `support.js` beside it (it loads React from unpkg). Pick an event in the
stand-in list on the left. Pick it again to see the empty state. Both folds and "+4 more" work, and every hover
works. The `theme` Tweaks prop takes `elite`, `dark`, `light` and `elite-palette`.

The stand-in list is only there to drive the prototype. It is not a design for the list.

The four events are real journal lines from 2026-09-20 and 2026-09-29, except the player message,
which is made up. Row labels, values and tooltip text come from the code (#813, #815, #816). The
paragraphs are example wording; the final text comes from #814. The empty-state sentence is a
proposal: change it freely.

## Fidelity
**High fidelity.** Colours are Palette roles only, from the D47 Design System. Type is the system's
three faces. Zero radius, no shadows, no glow.

## Screenshots
`NN-state-theme.png`, captured headlessly at the pane's own width (650px; 325px for the narrow pair).
Hover states are drawn with the hover forced on.

| # | State | Themes |
| --- | --- | --- |
| 00 | Prototype, with the stand-in list | Elite |
| 01 | Docked, a curated kind | all four |
| 02 | Docked, Every field open | all four |
| 03 | Docked, a decoded value hovered (token) and a row label hovered (tooltip) | all four |
| 04 | Docked, What this means folded | all four |
| 05 | ShieldState, a kind nobody curated | all four |
| 06 | ReceiveText, a player's message | all four |
| 07 | EngineerCraft, a link and a list of items | all four |
| 08 | EngineerCraft, the engineer link hovered | all four |
| 09 | Nothing selected | all four |
| 10 | Docked while wanted | Elite |
| 11 | Docked, every service shown after "+4 more" | Elite |
| 12 | Narrow (325px): Docked, Every field open | Elite |
| 13 | Narrow (325px): EngineerCraft | Elite |

Theme suffixes: `elite`, `dark`, `light`, `elite-palette` (Match my Elite colours).

## The spec

### Pane
- Padding `20 24 32`. The five parts are 18px apart. 1px `line2` rule on the pane's left edge, where it meets the list.
- Scrolls vertically, never horizontally.

### Headline and time
- Headline: Sintony 20, line height 1.3, `white`.
- 6px below it, the time line: the time in JetBrains Mono 12, `a`; then ` · ` and the age in Sintony 13, `grey`.
- The age redraws at least once a minute while the pane shows.

### Rows
- Each row is its own `slab` strip, 2px apart (`gap-tile`). No grouping beyond that.
- Row padding `10 14`. Two columns: label 148px, value `1fr`, 16px apart.
- **Label**: Saira 12/500, uppercase, tracking 0.06em, line height 18, `grey`.
- **Values**: Saira 15/500, line height 20. Default `a`. A name (a faction, an engineer) is `white`. A legal status such as "You are wanted here." is `red`. The current system is `cyan`.
- **Numbers inside a value** (counts, grades, distances, module size) are JetBrains Mono 13/400 in the same colour: `4` small, `213` ls from the star, `4A` Thrusters, Iron × `5`.
- **Several values** are separated by ` · ` in `grey`, 8px each side. The separator trails the value before it, so a wrapped line never starts with a dot.
- **Narrow**: below about 480px of pane width, the label sits above the value (one column, 4px apart) (12, 13).

### Value kinds
- **Plain**: as above.
- **Decoded** (a value with a `Symbol`): a 1px dotted `grey` underline, 5px below the baseline. On hover or keyboard focus the underline turns `a`, and a box opens 8px below the value. The box is 32px tall, with a `bg` ground, a 1px `a` border and 10px side padding. It holds `JOURNAL` in Saira 11/500 uppercase `grey`, then the raw token in JetBrains Mono 12 `white` (03).
- **Link** (Engineer, and a Ship the Commander owns): a flat tile. The face is 30px tall with 10px side padding, `tile` ground, the name in Saira 15/500 `white` and a `›` in `a`, 10px after it. The hit area is 44px tall. Hover is `tile2`. Pressed is solid `a` with `knock` text and glyph. On hover, a label in the glyph-tip style sits 6px to the right: `OPEN ON THE ENGINEERS TAB`, or `OPEN ON THE SHIPS TAB` for a ship. A hull the Commander does not own is a plain `white` name with no tile (08).
- **Typed by a player** (`Typed = true`): Sintony 15, line height 22, `grey`, shown exactly as typed. It wraps anywhere and never passes through `TranscriptMarkup`. It has no tile, underline or quote marks. It differs from d47's own text by face (Sintony, where d47's values are Saira) and by role (`grey`, where d47's values are `a` and `white`). It uses no `red` or `warn`, so it does not read as a warning (06).

### Label tooltip
- Only on a label whose `Field` `JournalFields` knows. There is no marker at rest. On hover or focus the label turns `white` and the cursor is help.
- The box opens 10px below the label, aligned with the row's left edge. It is 300px wide (260 narrow), with padding `8 10`, a `bg` ground and a 1px `a` border. It holds the field name in JetBrains Mono 12 `a`, then ` — `, then the meaning in Sintony 13, line height 1.45, `white` (03).

### Long rows: "+N more"
- A row shows at most **six** values. The rest fold behind a tile after the sixth: `+4` in JetBrains Mono, then `MORE` in Saira 12/600 uppercase. The tile is 26px tall in a 44px hit area, 12px after the last value, `tile`, hover `tile2`.
- Pressing it shows every value in place, and the tile then reads `FEWER`. A new selection starts folded again (11).

### Wanted
- A docking while wanted adds a full-width row, first, with no label: "You are wanted here." in `red` (10).

### What this means
- A disclosure head: a 44px full-width button with a 1px `a` rule under it. It holds a glyph (`▼` open, `▶` folded) 10px in `a`, then `WHAT THIS MEANS` in Saira 13/600 uppercase, tracking 0.06em, `white`. Hover is a `tile` ground.
- Open: the paragraph sits 12px below the rule, in Sintony 15, line height 1.55, `white`, at most 62ch wide (01).
- Folded: the head carries a one-line preview of the paragraph after the title, in Sintony 13 `grey`, cut with an ellipsis (04).
- #14 decides when it opens. The band for what an event changed (#14) goes between this band and Every field, as another head of the same form.

### Every field
- The same head form. Title `EVERY FIELD`. The field count sits right-aligned in JetBrains Mono 12 `grey`. Folded by default.
- Open: every field in file order, one line per field. JetBrains Mono 12, line height 18. Each line has padding `5 2` and a 1px `line2` rule under it. Name column 212px in `grey` (it fits `StationGovernment_Localised`). Value in `white`.
- Values: strings unquoted; numbers and booleans as the journal wrote them; a nested object or list as one line of JSON.
- A long value **wraps** at any character. It does not scroll sideways.
- All of it is selectable text (02).
- Narrow: the name sits above its value (12).

### Nothing selected
- In the headline's place: `No event selected` in Sintony 20 `grey`. Below it: `Select an event in the list. D47 shows it here in words, with every field the journal wrote one fold below.` in Sintony 14, line height 1.5, `grey`, at most 44ch (09).
