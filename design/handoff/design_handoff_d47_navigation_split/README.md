# Handoff: the Navigation tab after the split (2026-10-03)

Design issue: [#321](https://github.com/dseelinger/d47/issues/321). Build issues: [#820](https://github.com/dseelinger/d47/issues/820) (Course and Community Goal go), [#821](https://github.com/dseelinger/d47/issues/821) (Progress beside Plan), [#822](https://github.com/dseelinger/d47/issues/822) (the Search tab). The Best cargo page has no build issue yet.
Canvas: https://claude.ai/artifact/8mk8NNH1vUX9GVbyJDtdNF (private to the maintainer).

## What changed
- **Best cargo** is a new NAVIGATION root, after MARKET. It is the screen for `best_commodities_for`: what to buy at the station you are docked at to sell in another system (01, 03–06).
- **Progress is a level beside Plan.** The tab opens on Plan › Progress. A PROGRESS tile on Plan opens it (02).
- **The tab strip** is the 2026-10-03 layout: TRANSCRIPT · SEARCH · STORIES · COMMANDER · ASSET MGMT · NAVIGATION · SETTINGS (01, 02).
- **NAVIGATION's roots**, in order: PLAN · BOOKMARKS · MARKET · BEST CARGO · TRADE ROUTE · ON THIS BODY · UNSOLD DATA. Course, Community Goal and Progress are no longer roots (01).

**Start here:** read this README, then work through `CHECKLIST.md`, comparing the build against `screenshots/`.

## About the design files
The `.dc.html` files are a **design reference made in HTML**, not production code. Recreate them in the Avalonia app with the existing kit styles. Open one in a browser with `support.js` and `d47.css` beside it (it loads React from unpkg). The `theme` Tweaks prop takes `elite`, `dark`, `light` and `elite-palette`.

| File | Screen |
| --- | --- |
| `Main.dc.html` | NAVIGATION › BEST CARGO, with results |
| `PlanProgress.dc.html` | NAVIGATION › PLAN › PROGRESS |
| `CargoNotDocked.dc.html` | Best cargo, not docked |
| `CargoGalaxyOff.dc.html` | Best cargo, galaxy lookups off |
| `CargoMarketUnseen.dc.html` | Best cargo, no prices for this market |
| `CargoNoProfit.dc.html` | Best cargo, nothing sells at a profit |

`d47.css` is the kit classes and the four palettes from the D47 Design System, cut down to what these screens use. The app already has these as Avalonia styles.

All names, prices and systems are placeholders. Strings come from the code where the code has them; the rest are proposals, marked below.

## Fidelity
**High fidelity.** Colours are Palette roles only. Type is the system's three faces. Zero radius, no shadows; glow only on the brand mark and the active tab, in Elite.

## Screenshots
`NN-state-theme.png`, captured headlessly. Windows are 1280 wide; the four state boards are the page body alone, 940 wide.

| # | State |
| --- | --- |
| 01 | Best cargo, docked, six results |
| 02 | Plan › Progress, a neutron route |
| 03 | Best cargo, not docked |
| 04 | Best cargo, galaxy lookups off |
| 05 | Best cargo, no prices for the market you are docked at |
| 06 | Best cargo, nothing sells at a profit |

Each in `elite`, `dark`, `light` and `elite-palette` (Match my Elite colours, drawn in Elite's colours).

## The spec

### Tab strip and roots (01, 02)
- Tabs as the brief screens v2 handoff draws them: 40px, padding `0 7px`, 2px apart, a 2px `a` rule under the row. Only the order and labels change.
- The roots row is the existing text choice (`ItemSpacing` 24). BEST CARGO sits between MARKET and TRADE ROUTE.
- At a drilled level the roots row gives way to the breadcrumb, `PLAN › PROGRESS`, as today (02).

### Best cargo (01)
Data: `best_commodities_for` → `BestCargoSearch`, `BestCargoAnswer`, `CargoPick` (`src/D47.Core/Knowledge/BestCargo.cs`).

- **Title block.** Context `BUYING AT`. Title: the station you are docked at, in `cyan`. Under it, the system and `DOCKED` in chrome 13, `cyan`. Figure at the right: `FREE HOLD`, the hold in mono 18 `yellow` (`256 T`). The hold is cargo capacity less limpets, as the tool computes it.
- **Sell in.** A row of 2px-gapped parts, 44px tall:
  - A text field, prefix `SELL IN` in chrome 12 `grey`, the system name in Saira 15/500 `a`. Placeholder `a system`.
  - `ROUTE END · <system>`: a tile that fills the field with the last system of the plotted route. Drawn only when a route is plotted. *Proposal.*
  - `FIND CARGO`: a tile. Enter in the field does the same.
- **Filters line.** Sintony 14 `grey`: "Uses your trade route filters: …", the active filters in `white`, separated by ` · `, then "Limpets are left out of the hold." At the right, a 36px `CHANGE FILTERS` tile that opens the Trade route page's filters. The filters are the saved `settings.Trade` values the tool already reads (#311). *Wording is a proposal.*
- **Results group.** Group head `WHAT TO CARRY TO <SYSTEM>`, the system in `a`. Description "Most credits for the whole load first." At the right, a 32px `PLOT <SYSTEM>` tile that calls `plot_course`.
- **Column heads**, chrome 12/600, tracking 0.08em, `grey`, on a 1px `line` rule: `# | COMMODITY | SELL AT | PROFIT / T | TONNES | TOTAL ▼ | (bar)`. Widths `22 | 1.3fr | 1.1fr | 100 | 96 | 130 | 150`, 16px apart. TOTAL is the sorted column, in `a` with `▼`.
- **Rows**: 44px `slab` strips, 2px apart, padding `6 14`.
  - Rank: mono 12 `grey`.
  - Commodity: Saira 15/600 `white`.
  - Sell at: the destination station, Saira 14/500 `a`.
  - Profit / t and Total: mono 13 and 14, `a`, right-aligned.
  - Tonnes: mono 13 `white`, right-aligned, with a chrome 10 `grey` word under it naming what limited the load: `HOLD`, `SUPPLY` (what the station sells) or `DEMAND` (what the buyer takes). `CargoPick` needs to carry which one it was.
  - Bar: a 6px gauge on `tile`, filled `a` to the row's total over the first row's total.
- **How many rows.** Every profitable commodity, best first. `BestCargo.Picks` (2) stays the spoken count; the page needs `Rank` to return the full list, or a count passed in.
- **Footer.** Left: `Say: “what should I buy here to sell in <system>”`. Right: `PRICES LOOKED UP · 19:42`, the time of the search in mono `cyan`. The prices are the galaxy's, not the journal's, so the footer does not say the journal keeps them current.
- The page fetches when FIND CARGO is pressed and on nothing else: no timer, no refresh button.

### Best cargo: no answer (03–06)
Each keeps the title block and the Sell in row.
- **Not docked (03).** Title `NOT DOCKED` in `grey`, the current system under it in `cyan`. `FIND CARGO` disabled. An amber `notice`, label `NOT DOCKED`: "Best cargo reads the market at the station you are docked at. Dock somewhere with a commodity market and this page fills in."
- **Galaxy lookups off (04).** `FIND CARGO` disabled. An amber `notice`, label `GALAXY LOOKUPS ARE OFF`: "Best cargo looks up prices in other systems. Turn on “Look things up in the galaxy” in Settings and D47 will search." An `OPEN SETTINGS` tile in the notice.
- **No prices for this market (05).** After a search that came back null. An amber `notice`, label `NO PRICES FOR <STATION>`: "Nobody has reported this market's prices, and you have not opened it while D47 was watching. Open the commodity market once and D47 will have them."
- **Nothing sells at a profit (06).** The results group head as usual; in place of rows, one `slab` strip in Sintony 15 `white`: "Nothing here sells at a profit in <system>." (`BestCargo.Describe`'s sentence).
- The notice wording is adapted from the tool's own error strings for a reader rather than a listener. *Proposal.*

### Plan › Progress (02)
- The tab opens on Plan with Progress drilled beside it, two panes, a 1px `line2` rule between them. Plan is 520px; Progress takes the rest.
- Plan: title block (context `PLAN A ROUTE`, title `PLAN`), then a `PROGRESS` tile, then the three cards as today (Neutron Plotter, Road to Riches, Exobiology). The tile is drawn selected (`a` with `knock` text) while Progress is the open level, and at rest when a plan result has taken the slot.
- Progress: title block with context `PROGRESS`, the title `<first system> → <last system>` (the system you are in `cyan`, the destination `a`, the arrow `grey`), figure `TO GO` in mono `a`. Under it the existing summary line, numbers in mono `a`.
- Hops: 44px `slab` strips: `28 | 1fr | 110 | 170`. Index mono 12 `grey`; system Saira 14/500 `a`, `cyan` for the current one; star class chrome 12 `grey`; the tag right-aligned in chrome 12/600: `HERE` `cyan`, `SUPERCHARGE HERE` `a`, `NO SCOOP` `warn`, `SCOOP UNKNOWN` `grey`.
- Footer: `Say: “how far to go”` and `KEPT CURRENT BY THE JOURNAL · 19:42`.

## Not in this handoff
- The SEARCH tab's SYSTEM page: #678.
- The Market, Bookmarks, Trade route, On this body and Unsold data pages, which do not change here.
- Mini and the headset: Best cargo follows the existing rules for a root.
