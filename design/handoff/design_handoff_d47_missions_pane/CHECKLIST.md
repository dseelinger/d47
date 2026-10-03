# Completeness checklist: the Missions pane

This is the definition of done. Tick an item only when the Avalonia build matches both its screenshot
and the README spec for it. Each item names its reference screenshots in `screenshots/`, by number.

The data doesn't have to match. Structure, order, colour roles, type, sizes and states do.

## Shared
- [ ] Missions is the second root on COMMANDER, after Checklist. There is no Missions tab (01).
- [ ] Only live missions are drawn. No completed, failed or abandoned mission appears anywhere.
- [ ] Colours are Palette roles only. Radius is zero, with no shadows.
- [ ] Countdowns, rewards, counts and times are in JetBrains Mono. Labels and prose are not.
- [ ] All four themes render correctly (01, 02, 06, 07, each in `elite`, `dark`, `light` and `elite-palette`).

## Header (01, 06)
- [ ] H1 `COMMANDER`, MISSIONS the active sub-tab.
- [ ] The summary gives the count, how many hand in here, and the reward total, `At least` when some have no reward.
- [ ] With no missions the summary reads `No missions accepted.` (06).

## Ranking (01)
- [ ] The list has the groups EXPIRING WITHIN THE HOUR, HAND IN HERE and EVERYTHING ELSE, each left out when empty.
- [ ] The pane and the spoken board rank the same missions in the same order, asserted by a test rather than by eye.

## Rows (01, 02)
- [ ] Each row is a `tile` button at least 50px tall, 2px apart. Hover is `tile2`. Selected is solid `a`.
- [ ] The name is row-name style in `white` (`knock` when selected). A long name wraps and the row grows.
- [ ] Under it: `STATION · SYSTEM` in `a`, or `cyan` for the current system (02). `NO DETAIL` in `grey` where there is none (05).
- [ ] At the right: the countdown in mono 14 (`warn` under an hour), and under it the reward in mono 12 `grey`.

## Countdown
- [ ] Under a day it reads `0h 38m`; a day or more reads `1d 4h`.
- [ ] It redraws at least once a minute while the pane shows.
- [ ] Under an hour it is `warn`, in the row and in the detail (01).
- [ ] An expired mission shows `EXPIRED` in `red` in place of the countdown.

## Detail (01–04)
- [ ] The crumb reads `COMMANDER › MISSIONS › <group>`.
- [ ] A hand-in elsewhere shows the copy glyph and `PLOT TO HAND-IN`. The tile plots to the hand-in system; the glyph copies its name (01, 03).
- [ ] A hand-in where the Commander is docked shows `HAND IN HERE` in `cyan` and no tile (02).
- [ ] TIME LEFT, REWARD, ACCEPTED, HAND IN AT and GIVEN BY are `slab` blocks in a three-column grid.
- [ ] AGAINST appears only with a target faction, in `red` (03).
- [ ] CARGO appears only with a commodity; PASSENGERS only for a passenger mission (01, 04).
- [ ] The redirected line appears only for a redirected mission (03).

## Progress (01, 02, 04)
- [ ] The Delivery section appears only where the cargo depot has reported progress (01).
- [ ] A mission with no reported progress has no Delivery section: no empty bar and no zero (02, 04).

## No detail (05)
- [ ] A mission with no accept read shows only TIME LEFT and the one-line explanation.

## Nothing accepted (06)
- [ ] With no missions, both panes give way to the two sentences and the hint.

## Headset (07, 08)
- [ ] The headset's COMMANDER tab carries the same Missions root, list, detail and search box.
- [ ] At 1024 × 640 the list and the detail scroll inside the panel; nothing is cut off at the bottom.
- [ ] Rows keep their full height when the list scrolls (07).
