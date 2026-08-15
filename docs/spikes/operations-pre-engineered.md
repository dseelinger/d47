# Operations, Merc Coin, and what a pre-engineered module actually looks like

`list.md` Phase 14 asks four things about a module that arrives already modified, because if such
a module exists it breaks the "unmodified -> blueprint -> grade" premise the rest of the
engineering work rests on. Measured 2026-08-15 against **912 journals, 692,631 events, 221
distinct event types, 0 unparsable lines**, spanning 2025-07-02 to 2026-08-11.

Probe under [`spike/OperationsProbe/`](../../spike/OperationsProbe/). The corpus is a Commander's
own play history and stays out of the repository; section 7 of
[journal-corpus-engineering.md](journal-corpus-engineering.md) is the recipe.

## The short answer

**The premise survives, and the blueprint table is what breaks.** A pre-engineered module reports
a blueprint and a grade in exactly the ordinary shape — nothing about reading it is special. What
is special is that its blueprint is one **no player can roll and no source d47 generates from
carries a recipe for**, so a build intent must never name one and a total must never be quoted
for one.

## 1. What event records the purchase — nothing does

No event in the corpus carries an `Engineering` block at the moment a module is acquired. The
block appears on **`Loadout` and nowhere else**, 20,526 times. Every candidate the item named was
checked and none carries one: `ModuleBuy` (1,251), `ModuleRetrieve` (157), `FetchRemoteModule`
(292), `ModuleBuyAndStore` (2), `ModuleSell`, `ModuleSellRemote`, `ShipRedeemed` (50),
`ShipyardRedeem` (50), `TechnologyBroker` (2), `CarrierModulePack` (2).

**But engineering is not invisible outside `Loadout`** — it is a different, flatter shape, and
this correction matters more than the question that produced it. `StoredModules`, `ModuleStore`
and `ModuleRetrieve` carry **`EngineerModifications`, `Level` and `Quality`** as three plain
fields: 24,451 stored-module entries have them, against 123,412 without. So d47 can tell an
engineered module in storage from an unengineered one, and can name its blueprint and grade — it
just gets no modifiers and no engineer. That is a capability nothing in Phase 14 currently uses.

**Where the looking stopped.** Both pre-engineered kinds were already owned when the corpus opens.
The power distributor first appears seconds after a `ShipyardSwap`, the cargo racks after a run of
`ModuleRetrieve` from storage — the modules were acquired before 2025-07-02 and the acquiring
event is simply not in these files. So this answers "no purchase event carries engineering", which
is what d47 needs, and does **not** answer "what event Frontier writes when you buy one".

## 2. The marker is an engineer id that is not a person

`EngineerID: 399999`, with the `Engineer` name field **absent** — 320 of 20,526 blocks. Every
other id in the corpus is a real engineer in the 300000-300300 range, 24 of them, always named.

**`Quality` is not a marker, and nearly became one.** All 320 sentinel blocks sit at `Quality`
exactly 1.0, which looks diagnostic until you count the rest: **19,609 of 20,206 named-engineer
blocks are also exactly 1.0**. In a `Loadout` a finished module reads 1.0 whoever finished it. The
0.85 completion band from journal-corpus-engineering.md section 1 is about `EngineerCraft`
progress, not about what a fitted module reports.

## 3. Two blueprints, and only one of them is pre-engineered

Three module shapes carry the sentinel, across 10 slots:

| Module | Blueprint | Grade | Blocks |
|---|---|---|---|
| `int_cargorack_size5_class1` | `CargoRack_IncreasedCapacity` | 5 | 170 |
| `int_cargorack_size6_class1` | `CargoRack_IncreasedCapacity` | 5 | 124 |
| `int_powerdistributor_size4_class5` | `PowerDistributor_PrioritySystems` | 5 | 26 |

**They are not the same kind of thing, and assuming they were is the trap here.**

- `CargoRack_IncreasedCapacity` appears **294 times, every one of them with the sentinel**, and in
  **zero `EngineerCraft` events** in 912 journals. Nobody rolled it because nobody can.
- `PowerDistributor_PrioritySystems` appears 26 times with the sentinel on a size-4 distributor
  **and 6 times under The Dweller** on a size-7 one, with **16 `EngineerCraft` events** behind it.
  It is an ordinary blueprint this Commander rolled by hand.

So **`399999` means "engineered, engineer not recorded" — it is a superset of pre-engineered, not
a synonym.** The blueprint decides which, and the module id cannot: a pre-engineered rack is
`int_cargorack_size5_class1`, the very same symbol as the 111,566 Cr rack bought off the shelf in
`ModuleBuy`. Nothing but the `Engineering` block distinguishes them.

## 4. Whether the sources know it — a split, and one non-answer

| Source | `CargoRack_IncreasedCapacity` | `PowerDistributor_PrioritySystems` |
|---|---|---|
| EDEngineer `blueprints.json` — d47's authority | absent, **and it lists no cargo rack blueprint at all** | not searchable by symbol |
| FDevIDs `outfitting.csv` | absent (14 cargo rack modules, no blueprints) | absent |
| EDCD `coriolis-data` | **present** | **present**, grades 1-5 with recipes |

**One of those cells is a fact about the looking, not the data.** EDEngineer carries no blueprint
symbols at all — the Step 6 finding — so searching it for `PowerDistributor_PrioritySystems`
returns nothing whatever the truth is. It lists 11 power distributor blueprints under display
names (Charge Enhanced, System Focused, and nine more) and one of them is almost certainly this.
The cargo rack cell is a real absence, because that check does not depend on symbols: EDEngineer
has **zero** cargo rack blueprints under any name.

Coriolis is decisive about the rack, and its shape is the whole answer:

```json
"CargoRack_IncreasedCapacity": {
  "grades": { "5": { "components": {}, "features": { "cargo": [0.344, 0.344] } } },
  "modulename": ["Expanded Capacity Cargo Rack"],
  "name": "Expanded Capacity"
}
```

**One grade, no components, and a module name of its own.** That is the structural signature: a
blueprint with an empty ingredient list is not something a Commander gathers for, and Expanded
Capacity Cargo Rack is a module you acquire rather than a modification you apply.

## 5. Engineered further on top — consistent with no, not proven

Of the 10 slots that ever held a sentinel module, **0 ever changed engineering state**, and **0
`EngineerCraft` events landed on any of them**. That is the whole of the direct evidence, and it
is the weak kind: it says this Commander never tried.

The structural argument is stronger for the rack than the corpus is. **No source lists any cargo
rack blueprint**, so there is nothing to apply to one by any route. For the power distributor the
opposite holds — `int_powerdistributor_size4_class5` was crafted 105 times in this corpus under
`PowerDistributor_HighFrequency` and `PowerDistributor_PriorityEngines` — so that module type is
plainly engineerable and the sentinel one was probably re-rollable. Settling it needs a Commander
who owns one and tries, which is a manual test rather than a measurement.

## What this changes

- **The premise holds.** Nothing here needs a second reading path: a pre-engineered module is a
  blueprint and a grade like any other, and Step 6 already described all 320 of these blocks
  inside its 20,526-module corpus run without failing.
- **A build intent must not name one** (Phase 16). There is no recipe, so there is no total to
  compute — the honest answer is that the module is acquired, not made. `TotalFor` returning null
  for anything that is not a modification already covers this shape.
- **The blueprint table does not need a second source.** It needs to keep saying "I do not know
  this blueprint" for exactly two names, which is what it already does. Adding coriolis as an
  authority to cover them would import a source d47 deliberately demoted in Step 4, to describe a
  module a Commander cannot make.
- **`EngineerModifications` on stored modules is unclaimed ground** — section 1 above. Answering
  "which of my stored modules are engineered, and to what" needs no new source and no new table.

## How to re-measure

`python spike/OperationsProbe/scan_events.py` enumerates every event; `scan_payloads.py` finds
Engineering blocks and engineer ids; `scan_399999.py` characterises the sentinel;
`scan_origin.py` traces first sightings and tests the `Quality` marker; `scan_further.py` traces
each slot through time and reads EDEngineer's coverage; `scan_blueprint_engineers.py` is the one
that separates a pre-engineered blueprint from an ordinary one; `check_sources.py` and
`coriolis_entries.py` fetch the three upstream sources live.

**Merc Coin does not appear in the corpus at all.** Every payload was searched for a
Merc-Coin-shaped currency — merc, coin, frontline, operation, prize — and every hit was
incidental: a station-services list, a fleet carrier called MERCURIAL, the faction Frontline
Solutions, an NPC named Mercy. This Commander has not done Operations, so the item's premise about
what Merc Coin buys stays a claim read at source and unverified from the journal. Nothing above
depends on it: the pre-engineered modules found here arrived by some other route entirely.
