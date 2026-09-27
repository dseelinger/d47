# Completeness checklist: d47 brief screens (v2)

This is the definition of done. Tick an item only when the Avalonia build matches the screenshot and the README spec for it. Each group names its reference screenshots in `screenshots/`. The screenshots show the whole scrolling page, captured at 1280px wide. In the app, the window body scrolls.

Placeholder data doesn't need to match. Structure, order, colour roles, type, sizes and states do.

Capture artifact: the stepper arrows in 18 and 19 render as odd glyphs. In the app they are `◄` and `►`.

## 0 Shared
- [ ] The COMMANDER and EXPLORATION main tabs exist. UTILITIES is kept after ADVENTURES, which makes 10 tabs on one row with tab padding `0 8px` (01).
- [ ] HELP is a title-bar tile just before the window controls, not in the tab strip.
- [ ] With #321's TRADING and SEARCH added after ADVENTURES, all 12 tabs still fit on one row at 1280 (00).
- [ ] Every new screen has the footer: `Say: “…”` on the left, `KEPT CURRENT BY THE JOURNAL · HH:MM` on the right (time in mono cyan), above a `line2` rule.
- [ ] Every screen redraws when the journal changes. There is no refresh button anywhere.
- [ ] Tiles and rows are 44px tall with 2px gaps. Control text is 13px. Hover uses `tile2`. Radius is zero. There are no shadows.
- [ ] All numbers, costs, distances and times are in JetBrains Mono.
- [ ] Colour roles follow the design system: the current system is cyan; other systems are orange; met or done is blue; held against a cap is yellow; destructive or blocked is red; caution is amber.
- [ ] All four themes (elite, dark, light, elite-palette) render correctly on every screen.

## 1 Commander record

### 1a Standing (01)
- [ ] Title block: `COMMANDER RECORD`, with `CMDR <FULL NAME>` as the title.
- [ ] REPUTATION group: 4 rows (Empire, Federation, Alliance, Independents), each showing band and a signed number from −100 to 100.
- [ ] Centre-zero gauge: it fills orange to the right of the zero mark for positive values, and red to the left for negative ones.
- [ ] Negative numbers, and the Unfriendly and Hostile bands, are red.
- [ ] NAVY RANKS group: Imperial and Federal cells, each showing the rank, `RANK N OF 14 · N% TO <NEXT>`, and a progress gauge.

### 1b Statistics (02, 03)
- [ ] A sidebar lists all 16 sections from the `Statistics` event, in the game's order, each with a figure count.
- [ ] Choosing a section shows every figure it has, in a 3-column data grid.
- [ ] The subbar search finds figures across all sections, and matches on section names too.
- [ ] Search results show the section, label and value. Clicking one opens that section and clears the search.
- [ ] Search empty state: "No figure in any section matches that."

### 1c This session (04)
- [ ] Title figure: EARNED, the total of all sources.
- [ ] 8 figures: time played, jumps, distance, bodies scanned, materials gained, interdictions, deaths, balance at start.
- [ ] EARNINGS BY SOURCE: bounties, combat bonds, trade, exploration, missions, vouchers. Each has a gauge relative to the largest source. Zero amounts are grey.
- [ ] The session resets itself when the game starts. There is no reset control.

## 2 Exploration

### 2a On this body (05, 06)
- [ ] The body name is cyan.
- [ ] The UNMAPPED · WORTH IF MAPPED figure appears only when the body is unmapped.
- [ ] Data grid: biological signal count, genera, other signals.
- [ ] Three sample cells: finished samples are blue with ✓; the pending one is grey, and turns cyan `SAMPLE 3 · READY` once you're far enough.
- [ ] B4: `Exobiology.tsv` has a colony-distance column, and the screen reads it per genus.
- [ ] The distance since the last sample updates live from the game's position. Nothing else on the page updates continuously.
- [ ] Before the colony distance: the number is white and the state reads `KEEP WALKING · N M TO GO` in orange (05).
- [ ] At or past the colony distance: the number is blue and the state reads `✓ FAR ENOUGH · TAKE THE THIRD SAMPLE` in blue (06).
- [ ] With no colony distance for the genus (06b):
  - [ ] live number only, with no `OF … M` line and no gauge
  - [ ] `COLONY DISTANCE NOT KNOWN` in grey
  - [ ] SAMPLE 3 never READY
  - [ ] the explanatory hint
  - [ ] the own-sample upper bound is never shown as the threshold
- [ ] The genus list shows DONE (blue, with value), SAMPLING (cyan) and NOT STARTED (grey).

### 2b Unsold data (07, 08, 09)
- [ ] Title figure: TOGETHER, the sum of both totals.
- [ ] ORGANIC DATA shows value, species count, first-footfall count, and no-price count ("not in total").
- [ ] EXPLORATION DATA shows value, body count, mapped-efficiently count, and no-price count.
- [ ] Each has a protected row (orange left border) with a destructive RESET tile and one line of help.
- [ ] RESET opens a modal no more than 640px wide, on a 72% scrim, with the cannot-be-undone copy, RESET (destructive) and CANCEL (08).
- [ ] Confirming calls `clear_unsold_exobiology` or `clear_unsold_exploration`. Afterwards the figures read 0 and the help reads "Reset at HH:MM. Counting from zero." (09).
- [ ] The reset is a protected Commander action: D47 cannot trigger it by voice on its own.

## 3 Materials
- [ ] The sidebar has four groups, each item with a count:
  - PLANS (Needed by plans)
  - SHIP MATERIALS (Raw, Manufactured, Encoded, Guardian, Thargoid)
  - ON FOOT (Backpack, Ship locker)
  - FARMING (Farming route)

### 3 Needed by plans (10b)
- [ ] It replaces today's `GapPage`. Ship, suit and weapon needs are all still on the panel.
- [ ] One group per live build, with kind and `N SHORT` in the head.
- [ ] Rows show material, ledger (or ON FOOT), held/need, short (or `✓ MET` in blue) and a gauge.
- [ ] A row opens the material or on-foot resource detail.

### 3a Ledger (10)
- [ ] One group per grade, headed `GRADE N · CAP N`.
- [ ] Every material in a 3-column tile grid shows its held count. The count is yellow at cap.
- [ ] Each tile has a 3px yellow capacity bar along its bottom.
- [ ] Tiles that a live plan needs show `NEED n` (mono 11, grey) before the held count.
- [ ] Guardian and Thargoid ledgers are grouped by journal category and grade, e.g. `MANUFACTURED · GRADE 3` (12b).

### 3b Material detail (11, 12)
- [ ] It's a page with a clickable breadcrumb `MATERIALS › <LEDGER> ›`, not a dialog.
- [ ] Shows ledger, category, grade, and held/cap with a gauge.
- [ ] NEEDED BY PLANS: each build that needs it, with need and short. Only shown when a plan needs it.
- [ ] HOW TO FARM IT: farming advice.
- [ ] NEAREST TO YOU: live galaxy search results, nearest first. Shown only when "Look things up in the galaxy" is on (11).
- [ ] With the setting off, the section shows the turn-it-on line instead (12).
- [ ] Guardian and Thargoid detail has no trader section and shows "No material trader deals in … materials." (12c).
- [ ] AT A MATERIAL TRADER: trade up, trade down (not shown for the top grade) and across categories, each with the quantities given and received.

### 3c On foot (13, 14, 15)
- [ ] Backpack groups by kind, each showing `N HELD`, with no cap (13).
- [ ] Ship locker groups by kind, each showing held/cap against the locker cap (14).
- [ ] On-foot resource detail (15) shows:
  - [ ] breadcrumb
  - [ ] kind
  - [ ] backpack count (no cap)
  - [ ] locker held / 1,000 with a gauge
  - [ ] NEEDED BY PLANS
  - [ ] where it's found: settlements, buildings, containers
  - [ ] bartender exchange rates

### 3d Farming route (16, 17)
- [ ] Sites are listed nearest first from the Commander. The current system is cyan and reads `HERE`.
- [ ] Each site shows kinds, name, system and body, coordinates (mono), distance, how to collect, respawn behaviour and trade-down rates.
- [ ] The Segmented control (ALL / RAW / MANUFACTURED / ENCODED) filters the list (17).

## 4 Fleet

### 4a Compare (18, 19)
- [ ] Every ship's last-seen loadout: cargo, jump range, unladen mass, fuel, value, rebuy and where it is.
- [ ] ORDER BY (Segmented: JUMP RANGE / CARGO) re-sorts the list. The sorted column's head is orange with ▼.
- [ ] MINIMUM CARGO (NumberStepper: ANY, 16, 32, 64, 128, 256, 512 T) filters the list. The title figure reads `SHOWING N OF M` (19).
- [ ] Ships in the current system are cyan.
- [ ] Empty state: "No ship carries that much cargo."

### 4b Stored modules (20, 21)
- [ ] Modules are grouped by the system they are stored in. The group head shows the station and the count, with no distance.
- [ ] The class is parsed from the module symbol, e.g. `7E`.
- [ ] Each row shows the module, class, transfer cost (FREE when it's here) and transfer time.
- [ ] The subbar search filters by module name and hides empty groups (21).
- [ ] Empty state names the filter text.

### 4c Crew (22)
- [ ] `describe_crew` is shown on the panel (`ShowOnPanel` reversed).
- [ ] Each hired pilot shows combat rank, fighter-bay duty (`ON FIGHTER DUTY` in yellow, or `OFF DUTY`) and posted ship (`NOT POSTED` in grey).

## 5 Ship details: Plan section (23)
- [ ] Build task: `get_plan_shortfall` / `ChecklistService.Shortfall()` returns structured data per ship (rows, trips, blocks, one-trip sites), not prose.
- [ ] PLAN is its own section on the ship details page, with a `d47-section-head` and an aside giving the plan and delivery counts.
- [ ] Materials table: held/needed, short (or `✓ MET` in blue) and a gauge. Construction deliveries are included.
- [ ] MORE THAN ONE TRIP: amber notices for storage caps and cargo limits, with need, cap and trips in the mono detail line.
- [ ] GATHER IN ONE TRIP: missing materials grouped by origin text (e.g. HIGH GRADE EMISSIONS · SIGNAL SOURCE), with no location or distance.
- [ ] ENGINEER RANKS notices name the engineer and their rank, or "not unlocked".
- [ ] It redraws as materials are collected.

## 6 Settings › Phrases (24, 25, 26)
- [ ] It replaces Settings › Learned phrases. The subtab reads PHRASES.
- [ ] ADD A PHRASE: a SAY field (mono) that accepts wordings and `[a|b]` patterns, a Dropdown of every phrase with its capability shown, and an ADD tile. Enter also adds.
- [ ] Dropdown open state (25).
- [ ] A clash is refused. The field turns red and a red notice names the clashing wording and what it already stands for, with a mono detail line: `BUILT-IN PHRASE · <CAPABILITY>` or `YOUR PHRASE · <PATTERN>` (24).
- [ ] Clash detection expands the pattern alternatives on both sides.
- [ ] Validation notices: NOTHING TO ADD, PICK A PHRASE.
- [ ] Editing the field clears the notice.
- [ ] Adding puts the new phrase at the top of YOUR PHRASES and clears the form (26).
- [ ] YOUR PHRASES rows show the wording (mono cyan), a ›, and the phrase it stands for, with a ✕ glyph to forget it.
- [ ] Every built-in phrase is grouped by capability, with its one-sentence description (#538).
- [ ] The subbar search filters both your phrases and the built-in ones. There is an empty state.
- [ ] Teaching by voice (#539) works: "teach a phrase" is listed and mentioned in the help line, using #539's wording.
