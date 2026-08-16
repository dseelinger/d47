---
title: On foot
group: Knowledge
nav_order: 109
---

What you are wearing and carrying, what an on-foot grade or modification costs, and where its
materials come from.

> "what suit am I wearing"
> "what does night vision cost"
> "what would grade 5 on this suit take"
> "where do I find graphene"

Nothing here touches the network.

## On foot is not the ship feature with a different vocabulary

Two axes, and neither one is the ship model.

| | Grade | Modification |
|---|---|---|
| Applied at | **Pioneer Supplies**, in the Concourse | An engineer |
| Range | 1 → 5 | **Ungraded** — present or absent |
| Randomness | **None.** Fixed list, fixed price | None |
| Reversible | Buy a higher grade | **Never** |
| Limit | — | **Four per item**, one slot earned per grade above 1 |

Three things follow, and they run through every answer on this page.

**Nothing is estimated.** There is no roll count, so no hedging: every quantity below is what the
game charges, exactly.

**Planning matters more here than for ships.** Four slots, permanent, no way to practise. A wrong
modification is unrecoverable except by buying and re-upgrading a fresh item, so d47 says so once
per answer rather than never.

**Ordering is a routing fact.** A grade 1 item has *zero* modification slots, and an engineer's base
has no Pioneer Supplies — only an Apex desk. So "upgrade first" is a step in the order, not a
footnote: the upgrade cannot be done when you get there.

## What you are wearing

### `get_on_foot_loadout`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

```text
Wearing: Maverick Suit, grade 5 — loadout "Sneaky-Snipy".
  Suit: Improved jump assist, Quieter footsteps, 2 of 4 slots free.
PrimaryWeapon1: Manticore Executioner, grade 5.
  PrimaryWeapon1: Noise suppressor, Magazine size, Stability, 1 of 4 slots free.
SecondaryWeapon: Manticore Tormentor, grade 5.
  SecondaryWeapon: Stowed reloading, Magazine size, Stability, 1 of 4 slots free.
Modifications are permanent — they cannot be removed or replaced, and a wrong one is recoverable
only by buying and re-upgrading a fresh item.
```

**The suit's name never comes from Elite's own localisation.** Frontier's localisation is broken for
every suit above grade 1, and it is not an edge case: of 768 `SuitLoadout` events in a 912-journal
corpus, **269 carry an unresolved `$UtilitySuit_Class1_Name;` token, and every one says Class1**
whatever the real class is. Speaking that string would give you the wrong grade more than a third of
the time. The symbol plus a shipped table is the only honest route.

A modification d47 has no name for is said as the symbol Elite wrote, and labelled as one, rather
than dropped from the list as though the slot were empty.

## What it costs

### `get_on_foot_engineering`

```json
{"type":"object","properties":{"equipment":{"type":"string","description":"A suit or weapon by name \u2014 for example \u0022Maverick\u0022, \u0022Dominator\u0022 or \u0022Karma AR-50\u0022. Defaults to what the Commander is wearing."},"grade":{"type":"integer","description":"The grade to reach, 2 to 5."},"modification":{"type":"string","description":"A modification by name \u2014 for example \u0022Night Vision\u0022, \u0022Magazine Size\u0022 or \u0022Quieter Footsteps\u0022."}},"required":[],"additionalProperties":false}
```

Name a modification for its recipe and who offers it:

```text
Quieter footsteps — a suit modification.
  Costs: 3 × Settlement Assault Plans (have 0, short 3), 5 × Tactical Plans (have 12),
         5 × Patrol Routes (have 2, short 3), 3 × Micro Hydraulics (have 9),
         8 × Viscoelastic Polymer (have 21)
  From: Yarden Bond.
It is ungraded and permanent, and it needs the item at grade 2 or better before it can be applied
at all.
```

Or name a suit or weapon and a grade for what reaching it takes:

```text
Maverick Suit to grade 4, at Pioneer Supplies.
  Costs: 4 × Suit Schematic (have 1, short 3), 4 × Health Monitor (have 6),
         4 × Manufacturing Instructions (have 4), 9 × Carbon Fibre Plating (have 30),
         9 × Graphene (have 12)
  Credits from grade 1: 6,150,000.
  Grade 4 carries 3 modification slots. An engineer's base has no Pioneer Supplies, so this has to
  happen before the trip.
```

### The quantities are not the published ones

**This is the part most worth knowing about.** Every on-foot quantity in the community sources
predates the patch that cut them. `tools/gen-blueprints.py` restates them to what the game actually
charges:

| | Published | What the game charges |
|---|---|---|
| Modification ingredients | 5 / 10 / 15 | **3 / 5 / 8** |
| Grade upgrade ingredients | 1 / 5 / 10 / 15 / 25 / 35 | **1 / 2 / 4 / 5 / 9 / 12** |
| Power Regulators in an upgrade | listed | **not asked for at all** |

Measured against the game itself: 78 ingredient comparisons against the `Resources` list on 16 real
upgrade events, and a zero-remainder cover of 41 material lines across four `ShipLocker` deltas,
each corroborating the other's leftovers. Every quantity in both tables was observed; nothing is
extrapolated. See [the finding](https://github.com/dseelinger/d47/blob/main/docs/spikes/journal-corpus-on-foot.md).

Shipping the published figures would have quoted you two to three times the real cost of everything
on foot — and it would have looked right, because it is what every other tool says.

### Credits

Reaching a grade costs the item's grade 1 price times a fixed multiplier — **×4, ×15, ×30, ×50** for
grades 2 to 5, per step rather than cumulative, so grade 1 to 5 in full is 99 times the base price.
Exact on all 12 weapon upgrades in the corpus across five weapons and four grades, and on both suit
grades it covers.

Six of the eleven weapons have no published price, and for those d47 gives **no** credit figure
rather than a total missing a step — the two read identically otherwise.

## Where the materials come from

### `find_micro_resource`

```json
{"type":"object","properties":{"material":{"type":"string","description":"A micro-resource by name \u2014 for example \u0022Graphene\u0022, \u0022Circuit Board\u0022 or \u0022Opinion Polls\u0022."},"wanted":{"type":"integer","description":"How many are wanted, for the trade arithmetic. Defaults to the shortfall."}},"required":["material"],"additionalProperties":false}
```

```text
Graphene — Component. You have 12.
  Found in: Planetary Settlement.
  The Bartender charges 23 barter value for each.
  For 9, you could hand over any of:
    16 × Microelectrode (you have 22)
    23 × Weapon Component (you have 40)
    35 × Chemical Superbase (you have 51)
  Whatever value the last unit does not use is lost, so trade in one go.
```

**The sourcing is more specific than anything a ship material gets.** Every micro-resource carries
the settlement types, the building types (16 codes, AGRI to STO) and the container types (22 kinds
of locker and data port) it turns up in — so "which settlement, which building, which locker" is
answerable rather than approximated, where the best a ship material manages is "Planetary
Settlement".

**The Bartender's rate is arithmetic.** It takes Components for a fixed barter value each and sells
Components for a fixed barter cost, so a trade is
`floor(Σ(offered × value) ÷ cost)` — reproduced exactly on 49 of 49 real trades. This is a better
position than the ship material trader, whose rate took a 1,096-trade corpus to pin down.

Two things that change what you should do:

- **Leftover value is lost**, per trade, with no carry-over. Modelled and refused — crediting the
  remainder to the next trade drops the score from 49 to 30. So trade in one lump, not in ones.
- **Components only.** Items and Data are sold for credits and cannot be exchanged at all, and
  illegal goods are refused outside Anarchy-controlled systems.

### The locker cap is per category

**1,000 per category, not per item type** — the sources conflicted and the game settles it. Across
28,148 locker snapshots, Components total exactly 1,000 in 7,931 of them and never once more, while
the largest single component ever held is 94. d47 says so when you are within a quarter of the cap,
because gathering past it is wasted time.

Consumables carry a separate cap of **100 per item**.

## Notes for anyone reading the code

Two generated tables sit under this.

`tools/gen-onfoot.py` builds `OnFoot.tsv` — suits, hand weapons, hand tools and the modification
name map — from **EDDiscovery/EliteDangerousCore** (Apache-2.0) `Items/Suits.cs` and
`Items/HandItems.cs`. That is the only permissive source found keyed on the symbols the journal
writes: **FDevIDs has no suit list and no hand-weapon list**, which inverts the ship arrangement
where the id list named things and the journal merely agreed.

**Names come from there and numbers do not.** Its per-grade stats have provenance that varies per
figure, one row annotated as a guess, and a transcription bug that assigns the health multipliers to
the shield fields. Prices are read off real `BuySuit`/`BuyWeapon` events instead, and are blank
where none was ever seen.

`tools/gen-blueprints.py` builds the recipes, with the restatement described above. It refuses to
run if an on-foot ingredient size falls outside the measured tables, because one uncorrected number
sitting among corrected ones is invisible and is wrong by a factor of two or three.

**The modification name map is authored, and that is a deliberate exception.** Elite writes
`weapon_clipsize`; every recipe source writes "Magazine Size"; **nothing published carries both**,
and a relaxed match joins only 5 of the 13 symbols the corpus contains. The ship side answered the
same problem by refusing to guess, because 786 blueprints is not a table anybody can author. 25
modifications is. Each row records whether its symbol was read out of a real journal or follows the
convention and has never been seen, so a join d47 has watched work and one it merely believes in are
told apart.
