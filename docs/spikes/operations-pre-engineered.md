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

**They are community goal rewards** (section 6). The module appears in `StoredModules` at the
awarding station, at `BuyPrice: 0`, **32 seconds** after `CommunityGoalReward` — which is the
acquisition event this spike first reported as missing.

## 1. No module event carries engineering at the moment of acquisition

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

**This was first written as "the acquisition predates the corpus", and that was wrong** — see
section 6. It is right that no *module* event carries engineering at the moment of acquisition,
which is what d47 needs. It was wrong that the acquisition is missing: it is there, in a shape
nothing about modules would lead you to look for.

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

## 6. Where they come from: a community goal reward

**Added after the first pass, which did not look.** The search for the acquisition covered every
module and purchase event and every Merc-Coin-shaped word; it never searched for community goals,
even though the `list.md` item itself says the wiki's pre-engineered section "describes community
goal and tech broker modules". The corpus answers plainly once asked.

The Trailblazer goals pay out at Minerva:

```text
2025-08-22T11:45:40Z  CommunityGoalReward  275,000,000 Cr  Brewer corporation calls for Trailblazer resupply (Minerva)
2025-08-22T11:45:47Z  CommunityGoalReward  140,000,000 Cr  Defend Trailblazer Resupply Routes (Minerva)
2025-08-22T11:46:19Z  StoredModules        4x Expanded Capacity cargo rack, Minerva, BuyPrice 0, TransferCost 0
```

**Thirty-two seconds.** The modules do not arrive as a purchase, a redemption or anything that
names them — they simply appear in module storage at the station that paid the goal out, costing
nothing. The Commander retrieves them two hours later, and that `ModuleRetrieve` at 13:43 is the
event the first pass mistook for the beginning of the story.

**`BuyPrice` is a second, independent signature, and it is one-directional.** Every stored
pre-engineered module reads `BuyPrice: 0` — 485 of 485 cargo rack entries, 4,280 of 4,280 power
distributor entries, none with a price. But zero is not sufficient on its own: 2,455 unengineered
and 2,545 otherwise-engineered stored modules also read zero. So a price **rules out** a granted
module, and a zero does not rule one in.

**The rack is evidenced; the power distributor is not.** `PowerDistributor_PrioritySystems` was
already in storage on 2025-07-05, before any community goal in the corpus paid out, spread across
five different distributor sizes — and it is the blueprint The Dweller also rolls, with a full
coriolis recipe. Its route is not in these files. Given section 3, the likeliest reading is that it
is not a pre-engineered module at all: it is ordinary engineering whose engineer went unrecorded,
which is exactly what `399999` says and no more.

So the acquisition question has a better answer than "nothing records it": **a granted module
materialises in `StoredModules` at the awarding station, at `BuyPrice: 0`, within a minute of
`CommunityGoalReward`** — and d47 can see all three of those things today.

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
- **It joins up with community goals**, the last open item in Phase 14. The journal half of that
  item already reads `CommunityGoal` events; section 6 shows the same events explain where a
  module a Commander cannot make came from. A goal that pays out in modules is worth saying so
  when it is reported, and that costs nothing extra to know.

## How to re-measure

`python spike/OperationsProbe/scan_events.py` enumerates every event; `scan_payloads.py` finds
Engineering blocks and engineer ids; `scan_399999.py` characterises the sentinel;
`scan_origin.py` traces first sightings and tests the `Quality` marker; `scan_further.py` traces
each slot through time and reads EDEngineer's coverage; `scan_blueprint_engineers.py` is the one
that separates a pre-engineered blueprint from an ordinary one; `check_sources.py` and
`coriolis_entries.py` fetch the three upstream sources live; `scan_cg.py` and `scan_cg_link.py`
are the community goal pass that should have been in the first sweep.

**Merc Coin does not appear in the corpus at all**, and section 6 is why that is no longer a gap
worth chasing for this question — the modules found here came from community goals, not from
Operations. The original measurement stands: Every payload was searched for a
Merc-Coin-shaped currency — merc, coin, frontline, operation, prize — and every hit was
incidental: a station-services list, a fleet carrier called MERCURIAL, the faction Frontline
Solutions, an NPC named Mercy. This Commander has not done Operations, so the item's premise about
what Merc Coin buys stays a claim read at source and unverified from the journal. Nothing above
depends on it: the pre-engineered modules found here arrived by some other route entirely.
