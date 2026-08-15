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

**And most of that matters less than it looks**, because a module nobody can buy is one the
ship's AI only ever meets in two states — already stored, or already fitted — and both are
answered from the journal without the tables at all (section 9).

**They are community goal rewards** (section 6). The module appears in `StoredModules` at the
awarding station, at `BuyPrice: 0`, **32 seconds** after `CommunityGoalReward` — which is the
acquisition event this spike first reported as missing. **Merc Coin is a second route to the same
module** (section 8), sold through Operations to Commanders who missed the goal, so both stories
end at one blueprint and d47 needs to tell neither apart.

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
section 6. Note the scope: what follows is measured on the community goal route, the only one in
these files. Section 8 establishes a second route, buying the same module with Merc Coin, and this
corpus contains no instance of it — whether *that* transaction writes an event naming the module
is unmeasured here. It is right that no *module* event carries engineering at the moment of acquisition,
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

## 5. Engineered further on top — no, and the corpus was the weakest reason why

Of the 10 slots that ever held a sentinel module, **0 ever changed engineering state**, and **0
`EngineerCraft` events landed on any of them**. That is the whole of the direct evidence, and it
is the weak kind: it says this Commander never tried.

The structural argument is stronger for the rack than the corpus is, and section 8 confirms it
from outside: **no source lists any cargo rack blueprint**, so there is nothing to apply to one by
any route, and the community says plainly that cargo racks cannot be engineered normally and have
no experimental effect to receive. For the power distributor the
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

## 7. "Never seen before" — where the lag actually bites

**Added after being told these modules are often something the game has not had before**, which
the `list.md` item also predicts: "their blueprints will not be in the community datasets yet —
the same lag that already left Caspian Explorer, Corsair and Kestrel Mk II in the specification
table with a name and no figures." Measured three ways, and the lag turns out to sit somewhere
other than where that sentence puts it.

**The blueprint lag has closed.** Coriolis is keyed on the symbol the journal writes, and it knows
**all 35** blueprint symbols in this corpus — nothing unknown, including both sentinel names. The
gap d47 actually has is not the community being behind; it is that d47's own table comes from
EDEngineer, which carries **no symbols at all** and no cargo rack blueprint under any name. That is
the Step 6 finding wearing a different hat.

> A first attempt at this measured journal symbols against d47's shipped `Blueprints.tsv` and
> reported all 35 as unknown. That table is keyed on EDEngineer display names and has no symbol
> column, so the join could only ever return "all of them" — a fact about the join, and the same
> mistake this page warns about in section 4. The number above is against coriolis because coriolis
> is the only one of the three the question can be asked of.

**The signature is unique, which makes it a detector rather than a list.** Of all **81** blueprints
in coriolis, exactly **one** has no ingredients at any grade — `CargoRack_IncreasedCapacity`, the
community goal rack. So "this is not something you make" is readable from the shape of the data,
and a blueprint minted by next year's goal is covered the day coriolis lists it, with no code
change and no name to hard-code. That matters precisely because the class is expected to grow.

**The module lag is real and live.** FDevIDs `outfitting.csv` has never heard of
`int_fighterbaymk2_size5_class1` or its `_free` variant — the Fighter Hangar Mk II. Those are the
only two real modules among 217 unrecognised symbols; the other 215 are bobbles, ship kits, paint
and cockpits, which are noise rather than lag. And d47 inherits it: `EliteSpecifications.tsv`
carries `fighterbay_size5_class1`, `_size6_`, `_size7_` and no `fighterbaymk2` at all, so a
Commander flying one gets the symbol fallback today — exactly the armour gap Step 6 found and
closed, still open for this module.

## 8. Merc Coin, settled at source rather than in the journal

**The journal cannot answer this and never will**: Merc Coin appears nowhere in 692,631 events
because this Commander has not done Operations. Absence here is absence of evidence about one
machine's play history, which is the mistake this repository has now made six times. So it was
read at source instead, on 2026-08-15.

**Merc Coin buys these same modules.** It is earned by completing Operations and spends on ship
modules and engineering blueprints; the module side is described by players as a way to obtain
modules **previously handed out as community goal rewards** — a second acquisition route to the
same thing, offered to Commanders who missed the goal. That makes the two stories one story.

**One figure cross-checks cleanly, from a source with no connection to the other.** The community
describes the rack as pre-engineered with the Expanded Cargo Rack grade 5 blueprint for
**+34.4% cargo capacity**. Coriolis, read independently in section 4, gives
`"cargo": [0.344, 0.344]`. Two unrelated sources, the same number — which is worth more than
either on its own.

**It also settles section 5's open question**, in the direction the structural argument pointed:
cargo racks cannot be engineered through the normal system at all, and no experimental effect
exists to apply to one. That is what "EDEngineer lists no cargo rack blueprint under any name" was
already saying about the data; it is now said about the game. Frontier's own issue tracker
separately carries a report of class-5 Merc racks not upgrading to grade 5 properly while class-6
racks do, so the edges are rough enough that this is worth re-reading before anything is built on
it.

**Provenance, kept separate, because the item insisted on it** — it points out that the word
*pre-engineered* came from a community write-up rather than from Frontier. The currency's purpose
and the modules' properties above are **community documentation** (Fandom, Steam discussions,
Frontier forums), not Frontier's own product wording; only the upgrade-bug report is on Frontier's
own tracker. Nothing in sections 1 to 7 depends on any of it: those are measurements, and this
section is corroboration that arrived from outside.

> This is exactly the gap `list.md` describes Step 10's web search as existing to fill — "the
> honest escape hatch for the things engineering help refuses to assert". The spike needed it one
> commit after building it.

**Sources:** [Cargo Rack — Elite Dangerous Wiki](https://elite-dangerous.fandom.com/wiki/Cargo_Rack),
[Cargo Rack Merc Engineering (Steam)](https://steamcommunity.com/app/359320/discussions/0/568165608207886319/),
[New community goal, pre-engineered cargo rack reward (Steam)](https://steamcommunity.com/app/359320/discussions/0/596288556263300605/),
[Engineered Cargo Racks (Frontier Forums)](https://forums.frontier.co.uk/threads/engineered-cargo-racks.573473/),
[Merc updated cargo rack issues grade 5 (Frontier issue tracker)](https://issues.frontierstore.net/issue-detail/86823).

## 9. Two states, and why that makes most of this moot

**The scoping insight, and it retires work the earlier sections proposed.** A pre-engineered
module cannot be bought, so the ship's AI can only ever meet one in two states:

- **A — sitting in the Commander's module storage**, visible in `StoredModules` with its
  `EngineerModifications`, `Level` and `Quality` (section 1).
- **B — fitted to one of their ships**, visible in `Loadout` with the full `Engineering` block,
  including `Modifiers` in real units.

There is no third state, because there is no shop. **And that is structural rather than a matter
of sample size**: a module offered for sale is a listing of exactly three fields — `Name`,
`BuyPrice` and `id`, read from `Outfitting.json`, which the `Outfitting` *event* only points at.
There is no field in which a stock listing could express engineering, so what a station sells is
always the plain module.

**The symbol is purchasable; the module is not.** `int_cargorack_size5_class1` was bought over the
counter 27 times in this corpus and `int_cargorack_size6_class1` 45 times — the same symbols the
pre-engineered racks carry. So d47 must never reason from "this module is for sale" to "you can
get one of these", because the thing for sale is the ordinary rack with the same name.

**Both states are answerable without the blueprint table.** That is the part that matters. A
fitted module carries its own `Modifiers` in real units, which Step 3 established is the only
place its actual roll exists; a stored one carries blueprint, grade and quality. Neither needs a
recipe, an engineer list or an effect table — those exist for **planning**, and a module that
cannot be acquired cannot be planned for.

So the unknown-blueprint case never actually arrives anywhere that needs an answer. d47 meets one
of these only while describing something the Commander already owns, and describing owned things
is exactly the path that reads the journal rather than the tables.

> **This retires the derived flag proposed in section 7.** That suggestion — mark recipeless
> blueprints so d47 could say "this is not something you make" — was solving a problem that does
> not occur: nothing asks d47 to make one, because nothing can offer one. Section 7's measurement
> still stands as the reason the class is detectable if a later phase ever wants the wording; it
> is no longer a thing to build.

## What this changes

- **The premise holds.** Nothing here needs a second reading path: a pre-engineered module is a
  blueprint and a grade like any other, and Step 6 already described all 320 of these blocks
  inside its 20,526-module corpus run without failing.
- **A build intent must not name one** (Phase 16), and the crisp reason is section 9's rather than
  this page's first one. Not "there is no recipe to total" but **"there is nowhere to get one"** —
  a target you cannot acquire is not a target. `TotalFor` returning null for anything that is not a
  modification already covers the arithmetic; what a build plan needs is to not offer it at all.
- **The blueprint table needs no second source and no new flag** — and section 9 is the reason,
  which is better than the one this page reached first. An unknown blueprint is a permanent
  condition rather than two special cases, because the class grows with each goal; but it never
  arrives anywhere that needs the table, because the only two states d47 can meet one of these in
  are *stored* and *fitted*, and both are answered from the journal. The tables exist for planning,
  and a module nobody can acquire cannot be planned for.
- **`EngineerModifications` on stored modules is unclaimed ground** — section 1 above. Answering
  "which of my stored modules are engineered, and to what" needs no new source and no new table.
- **Merc Coin needs no further chasing.** Section 8 settles it at source: it buys the same
  modules the goals hand out. Two acquisition routes, one blueprint, one detector — the section 7
  flag covers both without knowing which route a module took, which is just as well, because the
  journal shows the route only for one of them.
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
`coriolis_entries.py` fetch the three upstream sources live; `scan_purchasable.py` separates the
purchasable symbol from the unpurchasable module; `scan_cg.py` and `scan_cg_link.py`
are the community goal pass that should have been in the first sweep; `scan_unknown_surface.py`
measures how much of what the journal names the sources do not know.

**Merc Coin does not appear in the corpus at all**, and section 6 is why that is no longer a gap
worth chasing for this question — the modules found here came from community goals, not from
Operations. The original measurement stands: Every payload was searched for a
Merc-Coin-shaped currency — merc, coin, frontline, operation, prize — and every hit was
incidental: a station-services list, a fleet carrier called MERCURIAL, the faction Frontline
Solutions, an NPC named Mercy. This Commander has not done Operations, so the item's premise about
what Merc Coin buys stays a claim read at source and unverified from the journal. Nothing above
depends on it: the pre-engineered modules found here arrived by some other route entirely.
