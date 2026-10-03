# Completeness checklist: the Navigation tab after the split

This is the definition of done. Tick an item only when the Avalonia build matches both its screenshot
and the README spec for it. Each item names its reference screenshots in `screenshots/`, by number.

The data doesn't have to match. Structure, order, colour roles, type, sizes and states do.

## Shared
- [ ] Colours are Palette roles only. Radius is zero, with no shadows; glow only on the brand mark and the active tab.
- [ ] Numbers, prices, tonnes, distances and times are in JetBrains Mono.
- [ ] All four themes render correctly on every screen (01–06, each in `elite`, `dark`, `light` and `elite-palette`).

## Tab strip and roots (01, 02)
- [ ] The strip reads TRANSCRIPT · SEARCH · STORIES · COMMANDER · ASSET MGMT · NAVIGATION · SETTINGS (#802, #822).
- [ ] NAVIGATION's roots read PLAN · BOOKMARKS · MARKET · BEST CARGO · TRADE ROUTE · ON THIS BODY · UNSOLD DATA. No COURSE, COMMUNITY GOAL or PROGRESS (#820, #821).
- [ ] At a drilled level the breadcrumb takes the roots row's place (02).

## Best cargo, with results (01)
- [ ] Title block: `BUYING AT`, the docked station in `cyan`, the system and `DOCKED` under it in `cyan`; `FREE HOLD` in mono `yellow` at the right.
- [ ] The Sell in field, the ROUTE END tile (only when a route is plotted) and FIND CARGO sit in one 44px row, 2px apart. Enter in the field searches.
- [ ] The filters line names the saved trade filters in `white`, with CHANGE FILTERS at the right opening the Trade route filters.
- [ ] Group head `WHAT TO CARRY TO <SYSTEM>`, the system in `a`, with the description and a PLOT tile that plots the system.
- [ ] Column heads and widths as the README gives them; TOTAL is `a` with `▼`.
- [ ] Rows are 44px `slab` strips, 2px apart: rank, commodity in `white`, station in `a`, profit and total in mono `a`, tonnes in mono `white`.
- [ ] Each row names what limited its tonnes: HOLD, SUPPLY or DEMAND.
- [ ] Each row's bar is its total over the first row's total.
- [ ] Every profitable commodity is listed, best total first. The spoken answer still names two.
- [ ] Footer: the voice hint at the left, `PRICES LOOKED UP` and the search time in mono `cyan` at the right.
- [ ] The page searches only when asked: no timer, no refresh button.

## Best cargo, no answer (03–06)
- [ ] Not docked: title `NOT DOCKED` in `grey`, FIND CARGO disabled, the amber notice (03).
- [ ] Galaxy lookups off: FIND CARGO disabled, the amber notice with OPEN SETTINGS, which opens the setting (04).
- [ ] Market not seen: the amber notice naming the station (05).
- [ ] Nothing profitable: the group head, then one `slab` strip with the sentence (06).

## Plan › Progress (02)
- [ ] The tab opens on Plan with Progress beside it; the breadcrumb reads PLAN › PROGRESS.
- [ ] A PROGRESS tile sits above the cards. It is drawn selected while Progress is open, at rest when a result has the slot.
- [ ] Progress title: the current system `cyan`, the destination `a`, the arrow `grey`; `TO GO` figure in mono.
- [ ] Hops are 44px `slab` strips with index, system, star class and tag. Tags: HERE `cyan`, SUPERCHARGE HERE `a`, NO SCOOP `warn`, SCOOP UNKNOWN `grey`.
- [ ] Footer: the voice hint and `KEPT CURRENT BY THE JOURNAL` with the time in mono `cyan`.
