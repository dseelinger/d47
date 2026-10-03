# Completeness checklist: the Journal reading pane

This is the definition of done. Tick an item only when the Avalonia build matches both its screenshot
and the README spec for it. Each item names its reference screenshots in `screenshots/`, by number.
Screenshots are captured at the pane's width: 650px, or 325px for 12 and 13.

The data doesn't have to match. Structure, order, colour roles, type, sizes and states do.

## Shared
- [ ] The pane draws, in order: headline, time, rows, What this means, Every field (01).
- [ ] The left list, search, noise filter, Raw Journal and the headset are unchanged.
- [ ] The pane scrolls vertically only. Nothing in it scrolls sideways, including the open fold (02, 12).
- [ ] Colours are Palette roles only. Radius is zero, with no shadows and no glow.
- [ ] Numbers, times, keys, ids and raw tokens are in JetBrains Mono. Labels and prose are not.
- [ ] All four themes render correctly in every state (01–09, each in `elite`, `dark`, `light` and `elite-palette`).

## Headline and time (01)
- [ ] The headline is the list's sentence, in Sintony 20 `white`.
- [ ] The time line shows the local `HH:mm` in mono `a`, then ` · ` and the age in Sintony 13 `grey`.
- [ ] The age redraws at least once a minute while the pane shows.

## Rows (01, 05, 07)
- [ ] Each row is a `slab` strip, with strips 2px apart, padding `10 14`, and a label column of 148px with a 16px gap.
- [ ] Labels are Saira 12/500, uppercase, tracking 0.06em, `grey`.
- [ ] Values are Saira 15/500 in `a`. Names are `white`. Numbers inside a value are mono 13.
- [ ] Several values are separated by a `grey` ` · `. A wrapped line never starts with a separator (01, 07).
- [ ] An uncurated kind (ShieldState) draws its mechanical rows the same way, with no paragraph band (05).
- [ ] Below about 480px of pane width, labels sit above their values (12, 13).

## Decoded values and label tooltips (03)
- [ ] A value with a `Symbol` has a dotted `grey` underline. On hover or focus, the underline turns `a` and the box shows `JOURNAL` plus the token in mono.
- [ ] A label whose field `JournalFields` knows shows its tooltip on hover or focus: field name in mono `a`, ` — `, then the meaning. There is no marker at rest. The label turns `white` while hovered.
- [ ] A label with no known meaning has no tooltip and no hover change.

## Links (07, 08)
- [ ] The engineer is a 30px `tile` face in a 44px hit area, with a `white` name and an `a` `›`. Hover is `tile2`. Pressed is solid `a` with `knock` text.
- [ ] Hovering shows `OPEN ON THE ENGINEERS TAB` to the right, in the glyph-tip style (08).
- [ ] Pressing it opens that engineer's page on the Engineers tab.
- [ ] A ship the Commander owns draws as the same link, labelled `OPEN ON THE SHIPS TAB`, and opens its page on the Ships tab.
- [ ] A hull the Commander does not own is a plain `white` name with no tile.

## Typed by a player (06)
- [ ] From and Message draw in Sintony 15 `grey`, exactly as typed. `look **here** $foo;` shows its asterisks and dollar sign.
- [ ] Typed text never passes through `TranscriptMarkup`. It has no underline, tile, quote marks, `red` or `warn`.

## Long rows (01, 11)
- [ ] A row shows at most six values, then a `+N MORE` tile: `+N` in mono, the tile 26px in a 44px hit area.
- [ ] Pressing the tile shows every value in place, and the tile reads `FEWER` (11).
- [ ] A new selection starts folded again.

## Wanted (10)
- [ ] A docking while wanted adds a first, full-width row with no label: "You are wanted here." in `red`.

## What this means (01, 04)
- [ ] The head is a 44px button with a 1px `a` rule under it. It shows `▼` or `▶` in `a`, then `WHAT THIS MEANS`. Hover is a `tile` ground.
- [ ] Open, the paragraph sits 12px below the rule, in Sintony 15, line height 1.55, at most 62ch wide (01).
- [ ] Folded, the head shows a one-line `grey` preview of the paragraph, cut with an ellipsis (04).
- [ ] A kind with no paragraph has no band: no head and no placeholder (05).

## Every field (02, 12)
- [ ] The same head form, titled `EVERY FIELD`, with the field count right-aligned in mono `grey`. Folded by default.
- [ ] Open, it lists every field in file order, in mono 12. Each field has a `line2` rule under it, a 212px `grey` name column and a `white` value.
- [ ] Strings are unquoted. A nested object or list is one line of JSON.
- [ ] A long value wraps at any character (02: `StationServices`).
- [ ] The text is selectable and copies as plain text.
- [ ] Narrow, the name sits above its value (12).

## Nothing selected (09)
- [ ] With nothing selected, the pane shows `No event selected` in Sintony 20 `grey` in the headline's place, with the one-line explanation under it.
