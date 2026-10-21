# Completeness checklist: the Short Story ask form

This is the definition of done. Tick an item only when the Avalonia build matches both its screenshot and the
README spec for it. Each item names its reference screenshots in `screenshots/`, by number.

The data doesn't have to match. Structure, order, colour roles, type, sizes and states do.

## Shared
- [ ] Stories › Short Story and the Next chapter page both use the new layout (01, 07).
- [ ] The form is at most 900px wide and scrolls vertically only.
- [ ] The page title row ("Ask for a short story", "Write the next chapter") is gone; the intro line remains (01, 07).
- [ ] Behaviour is unchanged: defaults, the brief entry, asking by voice, GO, STOP, settling and failures.
- [ ] Colours are Palette roles only. Radius is zero, with no shadows.
- [ ] All four themes render correctly (01–04, each in `elite`, `dark`, `light` and `elite-palette`).

## Choice rows (01, 02)
- [ ] Each row has a 120px label column in Saira 13/500 uppercase `grey`, 24px from the control.
- [ ] REACH and LENGTH are three equal 44px tiles, 2px apart, without the `Reach: ` and `Length: ` prefixes.
- [ ] Selected is solid `a` with `knock` text; rest is `tile`; hover is `tile2`.
- [ ] The hint under REACH shows `Radius()` for the selected reach, in mono, and changes with the selection.
- [ ] The hint under LENGTH names the objectives for the selected length and changes with the selection.
- [ ] SHIP shows the checkbox tile with its hint beside it; the hint changes when the box is ticked (02).
- [ ] With nothing to choose between, the SHIP row is left out entirely (06).

## Brief (01, 02)
- [ ] The brief is a 44px tile across the control column, text Sintony 15, left-aligned.
- [ ] Empty, it shows the entry's hint in `grey` (01). Set, it shows the brief in `white`, no quote marks (02).
- [ ] Pressing it opens the existing brief entry; committing updates the tile.
- [ ] Once set, a CLEAR tile sits beside it and empties the brief without opening the entry (02).
- [ ] A long brief is cut with an ellipsis and never widens the form.
- [ ] The hint "Optional. Press it to say it, or type it." sits under it.

## Action row (01, 03)
- [ ] A 1px `line2` rule, then GO, 200px, aligned with the controls.
- [ ] While writing, GO is disabled (`slab`, `grey2`) and STOP sits 2px to its right (03).

## Progress block (03)
- [ ] It sits in the control column under the action row: stage words and clock, then the gauge, then the hint.
- [ ] The clock is mono `a`; the gauge is 8px with a `tile` track and an `a` fill.
- [ ] A write's aside shows under the hint.

## Shut (04, 05)
- [ ] With no language model, a Warning-level `Notice` above the rows says so, with OPEN SETTINGS in it (04).
- [ ] With galaxy search off, the same Notice gives that reason (05).
- [ ] GO is disabled while shut; the choices still work.
- [ ] There is no separate Open settings button and no grey status line for the shut reason.

## Headset (08)
- [ ] On the big panel at 1024×640 the rows keep two columns and nothing is cut; the form scrolls.

## Next chapter (07)
- [ ] The breadcrumb shows where the page was reached from, and the intro names the story it follows.

## Documentation
- [ ] `docs/capabilities/adventures.md` describes the form as built, including the SHIP row's name.
