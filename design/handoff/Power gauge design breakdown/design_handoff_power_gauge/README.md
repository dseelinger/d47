# Handoff: Power gauge (3a: stack, priority drill-in and D47 check)

## Overview
This replaces the single-bar POWER gauge in d47. The old bar tried to answer five questions at once and was hard to read. The new view splits the job into three parts:

1. **Verdict.** Does the build fit the plant with hardpoints deployed? With them retracted?
2. **Diagnosis.** Which priority groups (P1–P5) keep power at full output and at each damage level? Which module sits where?
3. **D47 check.** A short list of modules that are probably in the wrong priority, with a one-tap fix for each.

Direction **3a** in `Power Gauge.dc.html` is the chosen one. Options 1a–1c and 2a in the same file are earlier explorations and are for reference only.

## About the design files
The files in this bundle are **design references made in HTML**. They are prototypes that show the intended look and behaviour, not production code. The job is to rebuild 3a in the d47 Avalonia app, using its existing D47 styles and controls and its real data (`PowerGauge`: `Fits`, `Overage`, the priority groups, and so on). All module names and MW figures in the prototype are made up.

## Fidelity
**High fidelity.** Colours, type, spacing and behaviour are final and follow the D47 design system (Elite theme). Build it to match, using the app's D47 resources.

## Terminology
- **Priority Pn.** The game's power priority, 1 to 5. The prototype's earlier "G1–G5" labels were wrong; always use **P1–P5**.
- **Cumulative draw.** Priorities are counted in order: P1, then P1+P2, and so on. **Priority n is powered only if the cumulative draw up to and including Pn is no more than the available output.** A priority is powered in full or not at all.
- **Available output levels.** Full = plant output. Damage levels come from the community wiki and are **not verified**; show that caveat: destroyed = 50%, malfunctioning = 40%, both = 20%.
- **Deployed / retracted.** Retracted draw = deployed draw minus all hardpoint modules.
- **Measured / modelled.** Modelled figures are worked out from a plan and get a `~` prefix; measured figures are read from the game. The panel head shows `~ MODELLED FROM PLAN` or `MEASURED IN GAME`.

## Layout (3a), one panel 980px wide
Panel: background `--d47-bg`, 1px border `--d47-line2`, padding 20px, children 14px apart.

1. **Section head.** `POWER` (Saira 15/600, tracking 0.1em, `--d47-a`) on the left. Provenance label on the right (Saira 11, 0.1em, `--d47-grey`, no wrap). A 1px `--d47-a` rule below, with 4px padding above the rule.

2. **Control row.** A grid with columns `380px | 1fr` and a 20px gap.
   - **Left column:**
     - Two verdict tiles on `--d47-slab`, 2px apart, padding 10×12.
       - Label: `DEPLOYED` / `RETRACTED` (Saira 11, 0.1em, grey).
       - Verdict: `OVER ~2.11` in `--d47-red` or `FITS` in `--d47-blue`, Saira 20/600, 0.06em.
       - Sub-line: `~25.04 MW · 109%` (JetBrains Mono 12, white).
     - Below the tiles, a Segmented control (`d47-segmented cols-2`): `DEPLOYED | RETRACTED`. It switches the stack between deployed and retracted draw.
   - **Right column:**
     - A Stepper (`d47-stepper`): `‹ PRIORITY P5 · 5 of 5 ›`. The arrow is disabled at either end.
     - A compact tile button `RESET ORDER` next to the stepper.
     - Below them, a mono 13 white line: `2.34 MW · 22.70 → 25.04 MW`. That is the priority's total, then its start and end on the cumulative axis.

3. **Chart area**, 705×500, then a 215px side column 20px to the right. All x-positions below are within the chart area.
   - **Overview stack.** x 205–295 (90 wide). Scale: 480px equals max(plant, deployed) × 1.06. Stacked bottom-up, 10px up from the bottom.
     - One slice per module, with a 1px gap between slices. Slices are ordered by priority, then by the user's order within each priority.
     - A slice is `--d47-a` if its priority is powered at full output, otherwise `--d47-red`.
     - Priorities other than the selected one are shown at 45% opacity.
     - A P-label (Saira 13/600, `--d47-knock`) sits centred inside each priority block, if the block is at least 16px tall.
     - Clicking any slice or label selects that priority.
   - **Output lines.** Each runs from x 0 to 295. Full output is 2px white; the damage levels are 1px white at 60% opacity.
     - Each line's label sits just above it, right-aligned at x 196, as two lines: the name (Saira 12, 0.06em; white for full, grey for damage) and `22.93 MW · P1–4` (mono 12, white).
     - The P-range lists the priorities that stay powered at that level (`NONE`, `P1`, `P1–n`).
   - **Drill-in column.** x 425–705 (280 wide), 480px tall. It shows the selected priority stretched to fill the column.
     - Each module is a bar. Bar height = 24px minimum + (module MW ÷ priority MW) × the height left over after the minimums. There is a 2px gap between bars.
     - Bar colour is `--d47-a` if the module's cumulative top is within plant output, else `--d47-red`.
     - Inside each bar: the name on the left (Saira 12/600, uppercase, knock colour), a `HARDPOINT` tag where relevant (Saira 10, `--d47-brown`), and the MW on the right (mono 12, knock colour).
     - The start and end MW (mono 11, grey) are shown right-aligned below and above the column.
     - If an output line falls inside the priority, it is drawn across the column at the right point inside the module it crosses.
     - An empty priority shows the slab message "Nothing draws power in this priority."
   - **Leader lines.** An SVG layer over the gap between x 295 and 425.
     - A funnel from the selected priority block to the full height of the drill-in column: fill `--d47-tile`, 35% opacity.
     - One cubic curve per module, from its slice midpoint in the stack to its bar midpoint in the drill-in. Stroke `--d47-a`, 1px, 70% opacity.
     - A dashed white curve (`4 3`) for each output line that crosses the selected priority, from its position in the stack to its position in the column.
     - Hovering a drill-in bar turns its curve white at 2px, and turns its slice in the stack white.
   - **Side column (215px):**
     - A group head `LINES IN P5` with the lines that cross this priority, one slab row each (name + MW). If there are none: "No line crosses this priority."
     - A group head `P5 POWERED AT`, then four rows 40px tall (FULL OUTPUT, DESTROYED 50%, MALFUNCTIONING 40%, BOTH 20%). Each row is a slab label block plus a 56px state tile:
       - ON: `--d47-tile2` with orange text.
       - OFF: slab with `--d47-grey2` text.
       - OVER: `--d47-red` with knock text; this appears only on the full-output row.
     - In retracted mode, any hardpoints in this priority are listed at the bottom as "Stowed: Beam Laser 0.80 · …" (13, grey).

4. **D47 CHECK.**
   - Section head: `D47 CHECK` on the left. On the right, `DEPLOYED · P5 UNPOWERED`, `DEPLOYED · P4–5 UNPOWERED`, or `DEPLOYED · EVERYTHING POWERED`.
   - One row per flagged module, 44px tall, grid `200px | 44px | 72px | 1fr | 130px`, 2px gaps: name (slab, 14 white), priority (slab, Saira 12 grey), tag tile, reason (slab, 13 grey), and an optional action tile button `MOVE TO Pn`.
   - If nothing is wrong: "Nothing a fight needs loses power when deployed."
   - An `UNDO MOVES` tile appears once any move has been applied.

5. **Footnote** (12, `--d47-grey2`): "Damage levels from the community wiki, not checked against the game."

## Interactions
- **Selecting a priority:** click it in the stack, or use the stepper arrows. Selecting clears any drop indicator.
- **Reordering in the drill-in:** drag a bar and drop it on another bar. The pointer's position decides where it lands: above the target's midpoint puts it higher (after the target), below puts it lower (before).
  - While dragging, the dragged bar is shown at 40% opacity, and a 3px `--d47-cyan` line shows where it will land.
  - `RESET ORDER` restores the original order.
  - Reordering is a **what-if** only. The game powers each priority in full or not at all, so it doesn't change what stays powered. It only shows which module each line crosses.
- **Deployed / retracted:** switches whether hardpoints count in the stack and the drill-in. The D47 check always judges the deployed build.
- **Check actions:** `MOVE TO Pn` changes that module's priority straight away, and everything recomputes. A fix can knock a priority over the line; that's intended, because it shows the knock-on effect.
- **Minimum hit target:** 44px, except the stack's slices, which also have the stepper as an alternative.

## D47 check rules (first draft; confirm the role list)
Each module has a role. The roles are **a guess and need confirming with the developer or the commander**:
- `core`: Power Distributor, Frame Shift Drive, Sensors, Thrusters.
- `life`: Life Support.
- `keep` (needed in a fight): Shield Generator, Shield Cell Bank, Shield Boosters, all weapons, Point Defence, Chaff Launcher, Wake Scanner.
- `depends` (depends on the build): Heat Sink Launcher, Guardian FSD Booster.
- `shed` (shut off first): Fuel Scoop, Cargo Hatch, AFMU.

In the rules below, a module is "powered" if its priority is powered at full plant output with hardpoints deployed. "Last powered" is the highest-numbered priority that is still powered.

| Condition | Tag | Colour | Reason | Action |
|---|---|---|---|---|
| `life`, and its priority is not powered at 20% output | AT RISK | red | Off if the plant is damaged at all. | MOVE TO P1 |
| `core` or `keep`, and not powered | OFF | red | Core module. / Needed in a fight. Unpowered when deployed. | MOVE TO P(last powered) |
| `depends`, and not powered | CHECK | slab, white | Unpowered when deployed. Depends on the build. | none |
| `shed`, powered, while the build is over | SHED | tile2, orange | Not needed in a fight. Shed it before anything else. | MOVE TO P5 |
| `shed`, and not powered | OK | slab, blue | Not needed in a fight. Shed first. | none |

Row order: problems (AT RISK, OFF, CHECK, SHED) first, then OK rows.

## Reduced states (use the same D47 surfaces)
- **Priority groups not read** (the ship hasn't been boarded since #253): show the verdict tiles, then a slab row "Board this ship once to read its priority groups." Show no stack.
- **No plant visible:** show two slab tiles, DRAW DEPLOYED and DRAW RETRACTED (mono values), then "No power plant visible, so there is nothing to compare the draw against."
- **Ship never boarded:** show one sentence of placeholder text, taken from the app's strings.

## State
- `mode`: `deployed` or `retracted`.
- `selectedPriority`: 1 to 5. Default: the priority containing the full-output line, or else the last one.
- `order`: the module order within each priority, as a what-if.
- `priorityOverrides`: modules the user moved using check actions, keyed by module id.
- Transient: `dragId`, `dropTarget {id, after}`, `hoverId`.
- Everything else is derived from the modules (id, name, MW, isHardpoint, priority, role) and the plant output: cumulative totals, whether each priority is powered at each level, verdicts, and the check rows.

## Design tokens (Elite theme; use the D47 resources, not hard-coded values)
`--d47-bg #070606`, `--d47-bar #0F0D0C`, `--d47-slab #232120`, `--d47-white #EDE9E3`, `--d47-grey #A09B94`, `--d47-grey2 #6E6A65`, `--d47-a #FF7A1A`, `--d47-knock #140800`, `--d47-brown #6B2F00`, `--d47-cyan #33D6E8`, `--d47-blue #1FA8F5`, `--d47-red #F0343F`. `tile`, `tile2`, `line` and `line2` are the accent mixed into the ground at 20%, 30%, 55% and 28%.

Type:
- Saira 500/600 for chrome: uppercase, tracking 0.04–0.12em.
- Sintony for prose.
- JetBrains Mono for all numbers.

Shape: no rounded corners, no outlines, 2px between tiles.

Colour meanings:
- Blue: FITS or OK.
- Red: over, off or at risk.
- Orange: values, and powered.
- Cyan: the drop indicator only.

## Reference screenshots
Captured from the prototype at 1×, Elite theme. Use them to check the build against the design:
- `screenshots/01-3a-deployed-P5.png`: the default state. Deployed, P5 selected, and the full-output line crosses Auto Field-Maintenance.
- `screenshots/02-3a-retracted-P5.png`: retracted mode. Hardpoints are left out, the build fits, and no line crosses P5.
- `screenshots/03-3a-deployed-P3-damage-line.png`: P3 selected, with the DESTROYED 50% line crossing the Shield Generator.

The screenshots may be drawn in a fallback font. The app should use Saira, Sintony and JetBrains Mono as specified above.

## Files
- `Power Gauge.dc.html`: the prototype. Section **3a** is the design to build; 1a–1c and 2a are explorations. The logic is in the `v3()` and `conn()` methods.
