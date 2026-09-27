# Handoff: d47 brief screens, v2 (2026-09-27)

## What changed in v2
This revision answers the Architect's review against the code (Q1 c, Q2 a, Q3 c, Q4 a). It replaces v1 entirely.
- **Tabs:** 10 tabs, with UTILITIES kept after ADVENTURES. HELP moves out of the strip into the title bar, as a tile just before the window controls. Tab padding is `0 8px`. That leaves room for #321's TRADING and SEARCH after ADVENTURES: all 12 fit on one row at 1280 (screenshot 00, `futureTabs` tweak).
- **Reputation** runs −100 to 100. The gauge fills from a centre zero mark: orange to the right, red to the left. Unfriendly and Hostile bands, and negative numbers, are red.
- **Sampling:** colony distance comes from the new `Exobiology.tsv` column (B4). When a genus has no value, the screen shows the live distance only: no threshold, no gauge, the state `COLONY DISTANCE NOT KNOWN` in grey, sample 3 never READY, and a hint saying why (06b, `colonyKnown` tweak).
- **Materials keeps the plan gap.** A PLANS sidebar group at the top holds NEEDED BY PLANS: every live build (ship, suit and weapon), with held/need, short and a gauge, and each row opens the material (10b). Ledger and locker tiles show `NEED n` when a plan needs them. Material and resource detail pages get a NEEDED BY PLANS group.
- **Guardian and Thargoid** have their own ledgers under SHIP MATERIALS, grouped by journal category and grade (`MANUFACTURED · GRADE 3`), with no trader section (12b, 12c).
- **Backpack** shows counts only, with no caps.
- **Stored modules:** the class is parsed from the symbol. The group head shows the station only, with no distance.
- **Plan › Gather in one trip** groups by origin text (e.g. HIGH GRADE EMISSIONS · SIGNAL SOURCE). There is no location or LY column.
- **Phrases:** the voice wording is "teach a phrase" (#539).


## Overview
**Start here:** read this README, then work screen by screen through `CHECKLIST.md`, comparing your build against `screenshots/`.

Six sets of screens for information d47 can already say out loud but no panel shows. They come from the brief "screens for what d47 can only say today". None of them needs new game data. Every screen redraws as the journal changes, and none has a refresh button.

1. Commander record: Standing, Statistics, This session
2. Exploration: On this body (with live sampling), Unsold data (with protected resets)
3. Materials: the whole inventory, material detail, on-foot resource detail, farming route
4. Fleet: Compare ships, Stored modules, Crew
5. Ship details: the Plan section (what this ship's live plans still need)
6. Settings › Phrases: replaces Learned phrases, and adds adding, patterns and clash refusal

## About the design files
`D47 Brief Screens v2.dc.html` is a **design reference made in HTML**. It is a clickable prototype of the look and behaviour, not production code. Recreate it in the d47 Avalonia app using its existing views, styles and the d47 control kit. Open it in a browser with `support.js` and `_ds/` beside it. The numbered tiles above the window jump to each brief section. The Tweaks props are `theme` (elite/dark/light/elite-palette), `start`, `galaxy` ("Look things up in the galaxy"), `colonyKnown` and `futureTabs`.

All figures, names and systems are placeholders. Real values come from the capabilities listed below.

## Fidelity
**High fidelity.** Built entirely from the D47 Design System (`_ds/.../components/kit.css`, `tokens/palette.css`, `tokens/type.css`). Match the kit classes, as they already exist as Avalonia styles.

## Standing rules (from 2026-09-26, applied throughout)
- Tiles are 44px tall with 13px text, 2px apart. Hover is `tile2`. Selected or pressed is solid `a` with `knock` text.
- Numbers, keys, costs, times and distances are in JetBrains Mono.
- Radius is zero everywhere. No shadows. Glow only on the brand mark and the active tab.
- A dialog is at most 640px wide (the reset dialog is 560). Anything wider is a page with a breadcrumb, which is why material detail is a page.
- Read-only data sits on `slab` (label `grey`, value `white` or `a`).
- Colour meaning: `cyan` = current or yours (the current system, the species being sampled, your own phrases, ready). `blue` = met or done (✓ MET, finished samples, far enough). `yellow` = stored or capacity (material held/cap gauges, cargo, on fighter duty). `red` = destructive or blocked (reset, engineer blocks, clash). `warn` = a caution you can act on (storage caps, multiple trips). Every system name other than the current one is orange.

## Navigation / placement
Main tabs (10, padding `0 8px`): TRANSCRIPT · **COMMANDER (new)** · FLEET · ENGINEERS · CHECKLIST · ROUTING · **EXPLORATION (new)** · ADVENTURES · UTILITIES · SETTINGS. Future #321 tabs (TRADING, SEARCH) go after ADVENTURES; all 12 fit on one row at 1280 wide (screenshot 00). HELP is a tile in the title bar (44 tall, 12px), placed just before the window controls. It is no longer at the end of the tab strip.

The brief's Commander and Exploration tabs stay hidden until their first screen registers a root, like every other tab except Transcript.
- Commander › STANDING | STATISTICS | THIS SESSION
- Fleet › SHIPS | **COMPARE** | **STORED MODULES** | **CREW** | SUITS | MATERIALS (reworked) | CARRIER
- Exploration › ON THIS BODY | UNSOLD DATA
- Settings › SETTINGS | **PHRASES** (replaces Learned phrases)

The brief says "Loadout › Materials" and "Loadout › Ships". The prototype shows these as Fleet, following the design system's Fleet screen. Put them wherever the app's current tab is.

Every screen has a footer line on a `line2` top rule. The left side holds the voice hint, e.g. `Say: “what's on this body”`. The right side says `KEPT CURRENT BY THE JOURNAL · 19:42`, with the time in mono cyan.

## Screens

### 1a Commander › Standing
Data: `get_standing` → `CommanderGameState.Reputation`, `.Ranks`.
- Title block: context `COMMANDER RECORD`, title `CMDR JOHN DEPARAGON` (always the full name).
- Group REPUTATION ("Where you stand with each power, from −100 to 100."). There are 4 slab rows, 44px tall, laid out as `200px | 120px | 56px | 1fr` with a 16px gap. The columns are:
  - faction: chrome 15/600, white
  - band: chrome 13/600, orange; red for Unfriendly and Hostile
  - number: mono 13, right-aligned, signed with a true minus (`−34`); white when zero or above, red when negative
  - gauge: 6px, centre-zero, on a `line2` track, with a 1px grey zero mark at 50%. A positive value fills orange rightwards from the centre by value/2 %. A negative value fills red leftwards.
- Group NAVY RANKS: a 2-column data grid, one cell each for Imperial and Federal navy. Each cell has the rank in white, the meta `RANK 12 OF 14 · 42% TO PRINCE` in mono 12 grey, and a gauge showing progress to the next rank.

### 1b Commander › Statistics
Data: `get_commander_statistics` → `.Statistics` (all 16 sections).
- Layout: a 230px sidebar and the content, with a 28px gap and a `line2` rule on the right of the sidebar.
- Sidebar: 16 `d47-sidebar__item` rows, 44px, 13px uppercase, with the figure count in mono 11 at the right. The active item is solid orange. This is how the Commander reaches one section without scrolling through all sixteen.
- Content: a page head (`CAREER STATISTICS ›` above the section title), then a 3-column `d47-data-grid` of every figure (value mono 16, orange).
- Subbar search, "Find a figure in any section": searches labels and section names across all sections. The results are tile rows (section in orange chrome, label, value in mono). Clicking one jumps to its section and clears the search.

### 1c Commander › This session
Data: `get_session_summary` → `.Session`. It resets itself when the game starts, and has no actions.
- Title block with the figure `EARNED` = the sum of the sources (mono, orange).
- A 4-column data grid: Time played (`3:42:10`), Jumps, Distance (LY), Bodies scanned, Materials gained, Interdictions, Deaths, Balance at start.
- Group EARNINGS BY SOURCE: 6 slab rows laid out as `200px | 1fr gauge | 170px amount`. The gauge is relative to the largest source. A zero amount is grey.
- Hint: "The session starts again by itself when the game starts."

### 2a Exploration › On this body
Data: `get_body_biology`, `get_sampling_progress` (`ExobiologyCapability.cs`).
- Title block: the body name in **cyan** (it's where you are). The figure `UNMAPPED · WORTH IF MAPPED ~1,240,000 CR` shows only when the body is unmapped.
- A data grid with Biological signals, Genera (span 2), Other signals and Gravity.
- Group SAMPLING:
  - Three 44px slab cells: SAMPLE 1 ✓ and SAMPLE 2 ✓ in blue; SAMPLE 3 in grey, turning cyan `SAMPLE 3 · READY` once you're far enough (only when the colony distance is known).
  - A distance block: `DISTANCE SINCE THE LAST SAMPLE` in mono 32. It is white, and turns blue at or past the colony distance. Under it, `OF <n> M FOR A NEW COLONY` and a gauge. The colony distance comes from the new `Exobiology.tsv` column (B4).
  - A state line at the right: `KEEP WALKING · N M TO GO` in orange, which becomes `✓ FAR ENOUGH · TAKE THE THIRD SAMPLE` in blue.
  - **Fallback when the genus has no colony distance** (06b):
    - The live number is shown, still white.
    - There is no `OF … M` line and no gauge, and SAMPLE 3 never turns READY.
    - The state reads `COLONY DISTANCE NOT KNOWN` in grey.
    - The hint reads: "Measured from the game's position as you walk. D47 has no colony distance for this genus, so it can't say when you're far enough."
    - D47 never presents its own-sample upper bound as the threshold.
  - **This is the only figure that updates continuously.** Bind it to the live position from the game. The prototype fakes walking with a timer.
- Group ON THIS BODY: one slab row per genus, laid out as `150px genus | species | 150px state | 150px value`. The states are: `✓ DONE · 3 / 3` in blue, `SAMPLING · 2 / 3` in cyan (with the species in cyan too), and `NOT STARTED` in grey.

### 2b Exploration › Unsold data
Data: `get_unsold_exobiology` (`ExobiologyLedger`) and `get_unsold_exploration` (`CartographyLedger`).
- Title block figure: `TOGETHER` = both totals.
- Two groups, ORGANIC DATA and EXPLORATION DATA. Each has a data grid laid out as `2fr 1fr 1fr 1fr`:
  - Estimated value (mono 22).
  - Count (species or bodies).
  - First footfall, or Mapped efficiently.
  - `NO PRICE · NOT IN TOTAL` (white).
- Under each grid is a **protected** settings row (3px orange left border) labelled "Reset the total". It holds a destructive RESET tile and the hint "Zeroes this total from now on. It can't be undone."
- RESET opens a modal: `d47-scrim` at 72%, `d47-modal` 560px wide.
  - Dek: `UNSOLD DATA · CANNOT BE UNDONE`. Title: `RESET <LEDGER>`.
  - Body: the current value in mono orange.
  - Foot: RESET (destructive), CANCEL, and the hint "Protected. D47 won't do this because you ask it to."
  - Confirming calls `clear_unsold_exobiology` or `clear_unsold_exploration`. Afterwards the values read 0 and the hint reads "Reset at HH:MM. Counting from zero."

### 3 Fleet › Materials
Data: `get_materials` (`.Materials`, `.Suit`), `find_material`, `get_material_farming_route`, `find_micro_resource`, and `how_to_get`. The tables behind them are `MaterialCatalogue`, `FarmingSites` and `AcquisitionGuide`.
- Layout: a 230px sidebar and the content. The sidebar has three groups (`d47-sidebar__group`):
  - PLANS: Needed by plans (count = distinct materials any live plan needs).
  - SHIP MATERIALS: Raw, Manufactured, Encoded, Guardian, Thargoid.
  - ON FOOT: Backpack, Ship locker.
  - FARMING: Farming route.
- **Ledger view.** The page head reads "Each held against the storage cap for its grade. Choose one to see where to find it."
  - There is one group per grade. The group head is `GRADE N`, with `CAP 300/250/200/150/100` in mono.
  - Under it is a 3-column grid of 44px tile rows. Each row has the name (chrome 13/600 white, ellipsis), the held count in mono 13 (orange, or yellow at cap), and a 3px yellow capacity bar along the bottom edge (track `--d47-yellow-track`).
  - When any live plan needs the material, a mono 11 grey `NEED n` sits before the held count. n is the total across plans.
  - Clicking a row opens its detail.
- **Guardian and Thargoid ledgers** (12b). The groups are by journal category, then grade: `MANUFACTURED · GRADE 3`, `ENCODED · GRADE 4`. Each has the same tiles and cap. Their detail page has no trader section. In its place is the grey hint "No material trader deals in Guardian materials." (12c). Nearest places are ruins, structures or sites.
- **Needed by plans** (10b). This replaces what `GapPage` shows today, so ship, suit and weapon needs stay on the panel.
  - Page head: "What every live plan still needs, for ships, suits and weapons, against what you hold."
  - One group per build: build name in the group head, then `kind · N SHORT`, e.g. `Anaconda · 2 plans · 4 SHORT` or `Suit · grade 5 · 1 SHORT`.
  - Column heads: MATERIAL | LEDGER | HELD / NEED | SHORT, laid out as `1fr | 150 | 110 | 90 | 200 gauge`.
  - Rows are 44px list rows (hover `tile2`). LEDGER is RAW, MANUFACTURED, ENCODED or ON FOOT. SHORT is mono orange, or `✓ MET` in blue.
  - A row opens that material's detail page, or the on-foot resource detail.
  - Data comes from `PlanGap` / `get_build_gap`, the same source as `GapPage`. The trader offers stay on the detail pages.- **Material detail** is a page, not a dialog.
  - Breadcrumb `MATERIALS › RAW ›` (clickable, goes back).
  - A 4-column data grid: Ledger, Category, Grade (mono), and Held `38 / 150` with a capacity gauge.
  - NEEDED BY PLANS (only when a plan needs it): rows laid out as `build · kind | NEED n | SHORT n / ✓ MET`.
  - HOW TO FARM IT: prose on slab.
  - NEAREST TO YOU: slab rows laid out as `place (orange) | note | LY (mono)`, nearest first. Only shown when "Look things up in the galaxy" is on. When it's off, show: "Turn on “Look things up in the galaxy” in Settings and D47 will search for the nearest places it's found."
  - AT A MATERIAL TRADER: rows laid out as `kind | give | › | get`, with quantities in mono orange. The kinds are Trade up 6:1, Trade down 1:3, and Across categories 6:1.
- **Backpack / Ship locker.** Groups per kind (Items, Components, Data, Consumables).
  - Backpack group heads show `N HELD` only. Backpack caps aren't in the data, so none is drawn.
  - Locker group heads show `held / 1,000`.
  - Tiles show the held count, plus `NEED n` when a plan needs the resource.
- **On-foot resource detail.** Breadcrumb, then:
  - A data grid: Kind, In backpack (a count, no cap), In ship locker `N / 1,000` with a gauge.
  - NEEDED BY PLANS, the same as on material detail.
  - WHERE IT'S FOUND: Settlements, Buildings, Containers.
  - AT A BARTENDER: exchange rows in the same layout as the material trader.
- **Farming route.**
  - Filter: a Segmented control with ALL / RAW / MANUFACTURED / ENCODED, 44px tall, max 560 wide.
  - Site cards on slab, nearest first:
    - Kinds (grey).
    - Name (chrome 17/600 white).
    - System and body (orange, or cyan if it's the current system), followed by `· lat, long` in mono.
    - Distance at the right in mono 15, or `HERE`.
  - Under a `line2` rule, three columns: How to collect, Respawn, Trade down.

### 4a Fleet › Compare
Data: `get_fleet_loadouts` → `.Loadouts`.
- Controls: ORDER BY as a Segmented with 2 options (JUMP RANGE / CARGO). MINIMUM CARGO as a NumberStepper stepping through ANY, 16, 32, 64, 128, 256 and 512 T.
- Table: a head row with a `line` bottom rule; the column that is sorted is orange with ▼, the others grey. Then 44px slab rows with the columns `1.5fr ship | 80 cargo | 90 jump | 100 unladen | 70 fuel | 140 value | 120 rebuy | 1.3fr where`.
  - Ship name in chrome 14/600 white, with the hull and ID beneath in 11 grey.
  - Numbers in mono 13. Cargo in yellow; jump, value and rebuy in orange.
  - Where: cyan for ships in the current system, orange otherwise.
- Title figure `SHOWING N OF M`. Empty state: "No ship carries that much cargo."

### 4b Fleet › Stored modules
Data: `get_stored_modules` → `.Modules`.
- A subbar search, "Filter by module name".
- One group per system where modules are stored. The system name is the group-head name (cyan for the current system, orange otherwise), followed by `station · N MODULES`. There is no distance: d47 has no position for other systems. The class (`7E`, `5A`) is parsed from the module symbol (`int_cargorack_size7_class1` → `7E`).
- Rows are 44px slab, laid out as `name (prose 15 white) | 70 class (mono) | 150 transfer cost (mono orange; FREE here) | 110 transfer time (mono HH:MM; — here)`.
- Title figure: the module count. Empty state: "No stored module matches “…”."

### 4c Fleet › Crew
Data: `describe_crew` → `.Crew`. **Reverse `ShowOnPanel = false`.**
- 44px slab rows laid out as `pilot | 160 combat rank (orange) | 170 fighter bay | posted to`.
- Fighter bay reads `ON FIGHTER DUTY` in yellow (equipped) or `OFF DUTY` in grey. `NOT POSTED` is grey.

### 5 Fleet › Ships › ship details: Plan section
Data: `get_plan_shortfall` → `ChecklistService.Shortfall()` (`ChecklistService.cs:618`). **Build task first:** return it as structured data per ship, not prose. The screen needs:
- `rows[]`: name, ledger and grade, held, needed. Construction deliveries are included as commodity rows.
- `trips[]`: storage-cap or cargo limits that force more than one trip. Each has a label, a sentence and the need/cap/trips numbers.
- `blocks[]`: engineer rank blocks. Each has the engineer, their rank against the rank needed, and what is blocked.
- `oneTrip[]`: sites that give several missing materials at once. Each has the site, the location, which materials it gives, and the distance.

Layout, below the existing details (title block with Rebuy figure and a data grid):
- A `d47-section-head` PLAN, with the aside `WHAT THE LIVE PLANS FOR THIS SHIP STILL NEED · N PLANS · N CONSTRUCTION DELIVERY`.
- A materials table with the columns `1fr material | 130 ledger | 110 held/need | 90 short | 220 gauge`. Short is mono orange, or `✓ MET` in blue.
- Two columns:
  - MORE THAN ONE TRIP: `d47-notice warning` (amber). Its detail line is mono, e.g. `NEED 130 · CAP 100 · 2 TRIPS`.
  - ENGINEER RANKS: `d47-notice` (red, blocked).
- GATHER IN ONE TRIP ("Missing materials that come from the same kind of place."): slab rows laid out as `260px origin | what it gives`.
  - The origin is the materials-table origin text (e.g. HIGH GRADE EMISSIONS), with its kind beneath in orange 12 (SIGNAL SOURCE).
  - There is no location or distance.
  - ENGINEER RANKS notices name the engineer and their rank, or "not unlocked".
- Hint: "Adds up every live plan for this ship, and construction deliveries. Redraws as you collect."

### 6 Settings › Phrases
Data: `PhraseBook` and `LearnedPhrasesStore`. The one-sentence descriptions come from #538, patterns and adding from #537, and teaching by voice from #539.
- Page head: "What D47 understands straight away, without asking the model."
- ADD A PHRASE: a row laid out as `1fr | 1fr | 110px`, 2px gaps:
  - A TextBox with the prefix `SAY` and mono input, placeholder `[boost|get clear] and [jump|engage]`.
  - A Dropdown of every phrase, with its capability shown at the right of each option (the list is long and variable, so Dropdown is correct here).
  - An ADD tile. Enter also adds.
- Clash: refuse, and show a red `d47-notice` under the row. The field outline turns red (`invalid`). The label is `CLASH · NOT ADDED`. The text names the clash: "“jump” already stands for …". The mono detail line says what it clashes with: `BUILT-IN PHRASE · SHIP` or `YOUR PHRASE · <pattern>`. Nothing is added.
  - Clash rule in the prototype: expand every `[a|b]` group into all its alternatives. It's a clash if any expansion of the new wording equals an expansion of a built-in phrase or of one of your own.
  - Editing the field clears the notice.
- Validation notices, in the same style: `NOTHING TO ADD` and `PICK A PHRASE`.
- Hint under the row: "Square brackets give choices … You can also teach one by voice: “teach a phrase”." (the #539 wording; the strings come from the code).
- YOUR PHRASES: slab rows laid out as `wording (mono 14 cyan) | › | the phrase it stands for (white) | 44px glyph ✕ on a red tile`. The glyph's tooltip is "Forget this phrase".
- Then one group per capability, with the phrase count in mono in the head. Each row is laid out as `260px phrase (white) | sentence (grey)`.
- Subbar search, "Search every phrase", filters both your phrases and the built-in ones.

## State (per screen, all local UI state)
- `nav {tab, sub}`
- Statistics: `section`, `query`
- Materials: `view` (needs/raw/manufactured/encoded/guardian/thargoid/backpack/locker/route), `selectedMaterial`, `selectedMicro`, `routeFilter`
- Compare: `order` (jump/cargo), `minCargo`
- Stored modules: `moduleFilter`
- Unsold: `resetDialog` (which ledger, or none)
- Phrases: `query`, `draftWording`, `draftTarget`, `dropdownOpen`, `clash`

Everything else is read from `CommanderGameState` and redraws on journal change.

## Design tokens
Use the tokens in `_ds/.../tokens/palette.css` (four themes) and `type.css`. Do not hard-code colours.
- Elite values: bg `#070606`, bar `#0F0D0C`, slab `#232120`, white `#EDE9E3`, grey `#A09B94`.
- Derived colours are mixes in OKLab:
  - `tile` = a 20% into bg; `tile2` = a 30%.
  - `line` = a 55%; `line2` = a 28%.
  - `red-tile` = red 22%.
  - `red-ground` and `warn-ground` = 12%.
  - `yellow-track` = yellow 22% into slab.
- Type scale: title 28 · tab 15 · group head 15/600 · row label 15 · body 16 · control 13–14 · meta 11–12. Chrome tracking is 0.06em (0.08em on subtabs).
- Spacing: tile gap 2px, hit target 44px, glyph 32px, groups 28px apart, window body padding `20px 28px`.

## Assets
None. The only icons are Unicode glyphs (› ▼ ◄ ► ✓ ✕) and the CSS brand diamond. Fonts are Saira, Sintony and JetBrains Mono, in `_ds/.../fonts/`.

## Files
- `CHECKLIST.md`: the definition of done. Every screen, element and state, each pointing to its screenshot. Work through it and tick items off.
- `screenshots/00-…26-*.png`: full-page captures, 1280px wide, one for each screen and key state. v2 adds 00 (12 tabs), 06b (no colony distance), 10b (needed by plans), 12b and 12c (Guardian). Compare against these.
- `D47 Brief Screens v2.dc.html`: the prototype. The screen markup is the template, and the logic class at the bottom holds the placeholder data, sorting, filtering, clash detection and the reset flow.
- `support.js`: the runtime needed to open the prototype in a browser.
- `_ds/d47-design-system-…/`: the bound design system (tokens, kit.css, fonts).
