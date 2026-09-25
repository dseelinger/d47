# d47 1.6.3 compared with Elite — gap audit

Source: 15 screenshots of d47 1.6.3 (Transcript ×3, Fleet ×4, Engineers ×3, Checklist, Routing ×2, Adventures, Settings), compared against `ELITE-COLOUR-NOTES.md`.

## Structural

1. **The window has no edge.** It's black on black, so you can't find the app on a dark desktop.
   - Elite frames every panel in 1px orange.
   - Fix: a 1px `line` frame on the window, plus a title bar ground one step lighter than the body.
2. **Fixed-height regions waste the window.**
   - Materials: five columns scroll internally while 60% of the page below them is empty.
   - Routing: forms occupy the top third.
   - Fix: regions fill the space they're given, or size to their content. Never both.
3. **Oversized one-off controls:**
   - "Settings for this page [9]" is 24px Titillium inside a full-width box with a boxed count.
   - "Delete completed items" is a two-line-tall box.
   - Journal rows push COPY onto a second line.

## Colour: every word is orange

4. **There's no white tier.** Screen titles (Campaigner (Panther Clipper MkII), Broo's Legacy in Muang), names (engineers, ships, carriers) and speech are all orange.
   - Elite: identity and speech are white, and attributes and values are orange.
5. **There's no grey tier.** "Unavailable" / "not met" / helper prose are dim orange, not grey. Elite uses neutral grey for can't-act.
6. **Stat grids float on bare black.**
   - The Carrier page (System / Tritium / Range …) is exactly Elite's Codex overview pattern, minus the grey tiles.
   - Elite: a dark grey tile per stat, light-grey label, orange value.
7. **The status colours are home-made.**
   - Current: MET is green, NOT MET is grey mono, UNKNOWN is yellow. POWER and JUMP RANGE are blue segmented bars. The rebuy line is red. The "filled from your ship" marker is a blue diamond.
   - Elite's equivalents:
     - Codex ladder: confirmed = blue ✓, reported = orange, rumoured = white.
     - Ship-spec bars are **orange** segmented.
     - Rebuy is orange in the Codex.
     - Your location / yours = cyan pin.
8. **Selection is shown with an outline** (the Campaigner card, the Broo Tarquin row). Elite: a **solid orange fill**, name white, labels dark brown.

## Controls

9. **Buttons** (Plot, Add to checklist, Reset every voice, Overwrite, Ask for one) are 1px orange outlines with wide-tracked text. Elite: a dim filled tile, uppercase, modest tracking, solid orange on focus.
10. **Toggles** are an orange block in an outlined box, with unclear state. Elite: the orange checkbox (a filled square inside a square outline) with the label beside it.
11. **List rows** (Engineers, Checklist, Materials, Hardpoints) are outlined boxes. Elite: dim **filled** tiles separated by 2px gaps, no outline.
12. **Segmented choice** (Reach / Length, BESIDE/WIDE, Ship/On foot) is an outlined frame with a glowing selected block. Elite has no segmented control. The nearest is a row of equal tiles with the selected one filled solid. Keep the behaviour and restyle it as tiles.
13. **Dropdowns remain:** Output device and Voice. They are long, variable-length lists, so they become the Dropdown tile (a `tile` with ▼ that opens a framed list), not a ComboBox. Fixed lists of 5–7 options use the ◄ value ► stepper (Elite's own `◄ [Z] 1/1 [C] ►` idiom), and 4 or fewer use Segmented tiles.
14. **Fields** (fill-2 ground with an orange bottom edge) are close enough to Elite, which uses outlined input boxes (`TO [LOCAL]:`). Keep them, but put placeholders in grey, not dim orange.

## Type

15. **Wide letter-spacing on everything,** including names and body values ("P a n t h e r  C l i p p e r"). Elite tracks only its small labels, lightly. Body and names aren't tracked.
16. **Headings are sentence-case orange** (The ship, As it is fitted, Grades). Elite: uppercase **white** with a thin orange rule below. Two-tone headings where there's context: orange context word over a white subject.
17. **Font match.** Elite's chrome face is a Eurostile-style square grotesque, and its body face is Sintony.
    - Nearest open-licence chrome font: **Saira** at normal width, not Saira Condensed.
    - Body: **Sintony** is on Google Fonts.

## Keep — these already work

- The "Say: …" voice hints. Restyle them grey.
- The orange wireframe ship renders. They're very on-brand.
- The breadcrumbs (Ships › Campaigner). Make them orange under a white title, as Elite does.
- Mono type for log and journal text.
- The stepper with a position readout.
