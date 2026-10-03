# Handoff: the Missions pane (2026-10-03)

Design issue: [#10](https://github.com/dseelinger/d47/issues/10). Build issue: none filed yet.
Canvas: https://claude.ai/artifact/BMx1w38X6EA3Zu6JFaJPqo (private to the maintainer).

## What it is
A **Missions** root on the **COMMANDER** tab, second after Checklist, in the root order #550 and #805
set: CHECKLIST · MISSIONS · STANDING · STATISTICS · THIS SESSION. It is not a top-level tab.

It has two panes. On the left is the list of accepted missions, in three groups ranked the way
`get_mission_board` ranks them aloud. On the right is the selected mission's detail. The headset
draws the same pane at its own size.

Settled with the maintainer on 2026-10-03:

- **Only live missions.** Completed, failed and abandoned missions are not kept or drawn. The pane
  draws what `MissionBoard.Missions` holds, and nothing more.
- **Under COMMANDER**, not a tab of its own.
- **The headset is the same pane**, search box included.

**Start here:** read this README, then work through `CHECKLIST.md`, comparing the build against `screenshots/`.

## About the design files
The three `.dc.html` files are **design references made in HTML**: clickable prototypes of the look
and behaviour, not production code. Recreate them in the Avalonia app with the existing kit styles.
Open one in a browser with `support.js` beside it (it loads React from unpkg). They link the design
system's stylesheets from `../../system/`, so open them from inside the repository.

- `D47 Missions Pane.dc.html`: the desktop window. Press a row to select it.
- `D47 Missions Headset.dc.html`: the headset panel at its default 1024 × 640.
- `D47 Missions Empty.dc.html`: no missions accepted.

The query string picks the state: `?theme=elite|dark|light|elite-palette&pick=1..6`.

The missions, factions, stations and times are made up. The header's tabs, the avatar and the
search box come from the header-consistency canvas; this handoff does not change them. The
Standing, Statistics and This session sub-tabs are drawn in their planned places, though those
screens do not exist yet (#552 onward).

## Fidelity
**High fidelity.** Classes and tokens are the D47 Design System's own (`kit.css`, `palette.css`,
`type.css`). Zero radius, no shadows. Glow only where the kit puts it (the active tab, the title,
the brand diamond).

## Screenshots
`NN-state-theme.png`, captured headlessly: the window at 1280 × 900 (560 for the empty state), the
headset at 1024 × 640.

| # | State | Themes |
| --- | --- | --- |
| 01 | A wing delivery expiring within the hour, with delivery progress | all four |
| 02 | A courier that hands in where the Commander is docked | all four |
| 03 | A massacre mission: a target faction, and a redirected hand-in | Elite |
| 04 | A passenger mission | Elite |
| 05 | A mission accepted before the oldest journal D47 has read | Elite |
| 06 | No missions accepted | all four |
| 07 | Headset, as 01 | all four |
| 08 | Headset, as 03 | Elite |

Theme suffixes: `elite`, `dark`, `light`, `elite-palette` (Match my Elite colours).

## The spec

### Header
- The standard tab header: COMMANDER active, H1 `COMMANDER`, the grey summary line, the search box,
  the 104px avatar, the 1px `a` rule, then the sub-tabs with MISSIONS active.
- **Summary**, in the header's grey line: `6 missions. 2 hand in here. 21,550,500 credits in rewards.`
  The count, the number that hand in where the Commander is docked (left out when none, or when not
  docked), and the reward total. Where some missions have no reward, it reads `At least … credits`,
  as the spoken board does. With no missions: `No missions accepted.`

### Layout
- Two panes, 24px apart (20 on the headset). The list is 360–420px wide; the detail takes the rest,
  with a 1px `line2` rule on its left edge and 24px (20) padding after it.
- On the window the page scrolls. On the headset the panel is a fixed size, so the list and the
  detail each scroll on their own, vertically only.

### The list
- Three groups, each under a `d47-list-head`: **EXPIRING WITHIN THE HOUR**, **HAND IN HERE**,
  **EVERYTHING ELSE**. A group with no missions is left out.
- The groups and the order inside them are **the same ranking `MissionsCapability` uses for the
  spoken answer**: expiring within the hour first, then handed in at the station the Commander is
  docked at, then the rest, soonest expiry first in each, expired ones last in the third group. Use
  one ranking for both; do not write a second one for the pane.
- Each mission is a `d47-list-row` button, rows 2px apart, at least 50px tall. A long name wraps to
  a second line; the row grows and never shrinks to fit.
  - **Name**: `Mission.Title`, row-name style (Saira 15/600 uppercase, `white`).
  - **Under it**: the hand-in as `STATION · SYSTEM`, chrome 12 uppercase. `a`, or `cyan` when it
    is in the current system. A mission with no detail reads `NO DETAIL` in `grey`.
  - **Right, top**: the countdown in JetBrains Mono 14. `a`, or `warn` under an hour.
  - **Right, under it**: the reward, `4,812,000 CR`, JetBrains Mono 12 `grey`. Nothing when there is
    no reward.
- **Selected**: solid `a`, the name in `knock`, the rest in `brown`, the countdown in `knock`. Hover is `tile2`.
- Under the list: `Say: “read the mission board”` in the hint style.

### Countdown
- Time left from now to `Mission.Expiry`. Under a day: `0h 38m`, `3h 12m`. A day or more: `1d 4h`, `5d 2h`.
- Redraws at least once a minute while the pane shows.
- Under an hour it turns `warn`, in the list and in the detail.
- Expired (not drawn in the screenshots): `EXPIRED` in chrome 13/600 `red` in place of the countdown.
- No `Expiry`: nothing in its place.

### The detail
Top to bottom:

1. **Crumb**: `COMMANDER › MISSIONS › <group>`, the `d47-crumb` style.
2. **Title block**: the mission title, `d47-title` at 24px in `white`, with the 1px `a` rule under
   it. At the right:
   - Hand-in in another system: a copy glyph (copies the system name, `copy_to_clipboard`) and a
     `PLOT TO HAND-IN` tile, 44 tall, which plots to the hand-in system (`plot_course`).
   - Hand-in at the station the Commander is docked at: `HAND IN HERE` in chrome 13/600 `cyan`, and no tile.
   - No detail: nothing.
3. **Data blocks**, a `d47-data-grid` of `slab` strips, 2px apart, three columns:
   - **TIME LEFT**: the countdown, JetBrains Mono 22. The label reads `TIME LEFT · UNDER AN HOUR` when it is.
   - **REWARD**: JetBrains Mono 22 `a`.
   - **ACCEPTED**: local `HH:mm` in mono 13 `a`, then ` · ` and the age in Sintony 13 `grey`.
   - **HAND IN AT** (two columns): the station in `white`, ` · `, the system in `a`, or `cyan` for
     the current system.
   - **GIVEN BY**: the faction in `white`.
   - **AGAINST** (two columns), only with a `TargetFaction`: the faction in `red`.
   - **CARGO**, only with a commodity: the count in mono, then the commodity name.
   - **PASSENGERS**, only for a passenger mission: the count in mono, then `aboard`.
4. **Delivery**, only where `Mission.Cargo` is known (the cargo depot reports it): a section head
   `DELIVERY` with the aside `FROM THE CARGO DEPOT`, then one `slab` strip holding
   `Delivered 72 of 120`, `Collected 120` at the right, numbers in mono `a`, and a `d47-gauge` filled to
   delivered ÷ total.
5. **Redirected**, only where `Mission.Redirected`: one hint line, `Redirected. The hand-in above is
   where the game now sends you.`

**No progress where the game reports none.** Courier, passenger, source and massacre missions
have no Delivery section at all: no empty bar, no zero.

### A mission with no detail (05)
`HasDetail` is false: it was accepted before the oldest journal D47 has read, so the `Missions`
snapshot has given only its name and expiry. The detail shows the TIME LEFT block alone, then one
`slab` prose strip: `Accepted before the oldest journal D47 has read. The game reports only its name
and time left.`

### Nothing accepted (06)
In place of both panes, at most 640px wide:
- `There are no missions on your board, Commander.` Sintony 16 `white`.
- `Missions on offer at a station are not in the journal. Accept one and it appears here.` Sintony 14 `grey`.
- `Say: “read the mission board”` in the hint style.

### Headset (07, 08)
- The same root on the headset's COMMANDER tab, with the same list, detail and search box. The
  headset keeps its own `ChromeRow` items (HELP, the avatar, the badge, resize) as #550 says.
- The panel is 1024 × 640 by default and never scrolls as a page: the list and the detail scroll inside.
- At 1024 the tab row wraps SETTINGS to a second line (07). That is the tab row's own behaviour,
  which #806 changes. This handoff does not change it.
