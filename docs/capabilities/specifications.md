---
title: Ship and module specifications
group: Knowledge
nav_order: 106
---

What a hull or a module can do, before anybody buys one.

> "how fast is a Python"
> "does a Type-9 need a large pad"
> "what does a 5A frame shift drive weigh"

Nothing here touches the network. This is the counterpart to the [journal](journal.md): that one
reports what *you* are flying, this one reports what a ship is *capable of*. A Type-9's landing pad
requirement is a fact about the game, not about the galaxy, and asking a third party for it would
be absurd.

There is no setting. Nothing leaves the machine, so there is nothing to switch off, and a row that
protects nothing is a row you have to read and decide about for no reason.

## What it says

```text
Python, built by Faulcon DeLacy. Needs a medium pad.
Speed 230 m/s, boosting to 300.
260 armour, 260 base shields, hardness 65, 350 t hull.
Hardpoints: 3 × size 3, 2 × size 2.
Optional internals: 3 × size 6, 2 × size 5, 1 × size 4, 2 × size 3, 1 × size 2, 1 × size 1.
2 crew seats, mass lock 17.
Hull 55,324,684 cr, before any modules.
```

Pad size comes first, because it is the one fact that decides whether a station is even an option.

Slots are **sizes**, not a count: "8 hardpoints" is not an answer to "what can it carry". And the
cost is the hull alone, said out loud — quoting 55 million for a Python that is nearer 200 million
outfitted would be a true number answering a question nobody asked.

Ask with no ship named and it answers about the one you are flying.

## The table is derived, not written

None of this is in the journal, so the choice was a table or no feature — and a hand-written one
would be exactly the confidently-invented game data the guardrails exist to prevent. A wrong top
speed reads identically to the feature working.

`tools/gen-elite-specs.py` builds it by joining two community sources on Frontier's own ids:

- **EDCD/FDevIDs** is the naming authority. `shipyard.csv` maps a hull id to the symbol the journal
  writes; `outfitting.csv` maps a module id to its name, mount, class and rating. It carries no
  performance figures.
- **EDCD/coriolis-data** carries the figures — speed, boost, armour, shields, module mass, power
  draw, and the drive numbers a jump range is computed from.

The join **is** the check. A ship in one and not the other is a gap the script refuses to hide.

It is read on first use rather than at startup. Twelve hundred module rows is a parse nobody should
pay for unless they ask a specification question.

## Armour is per-hull

A Mandalay's Lightweight Alloy and an Adder's are different objects with different mass and
different cost, so coriolis-data does not file bulkheads under `modules/` with the generic
outfitting at all — each one lives inside its own ship's JSON, and FDevIDs marks them by being the
only `outfitting.csv` rows with a ship named. Reading only `modules/` dropped every one of them,
which is how 1,725 of 20,526 engineered modules across 912 real journals came to be read out as
raw symbols like `mandalay_armour_grade1`.

So the name carries the hull. Frontier calls forty-eight different objects "Lightweight Alloy"
because the outfitting screen already knows which ship you are standing in, and a table does not:

```text
Mandalay Reactive Surface Composite — 38 t, 36,482,626 cr. Hull +250%, kinetic +25%,
thermal -40%, explosive +20%, caustic 0%.
```

Ask for a bare "Lightweight Alloy" and you get the hulls offered back rather than one of them
picked, for the same reason a bare "Frame Shift Drive" gets the sizes.

**The resistances are the point.** Mirrored and Reactive weigh the same, boost the hull by the same
amount and differ only in price — so a table carrying mass and cost alone would offer a choice
between two rows that read identically. All four are said out loud including the zeroes: "no
effect" and "no figure" are different claims. And the sign is the answer, not decoration — every
alloy below Mirrored is **-20% against kinetic**, which is a hole and not a saving.

Hull boost is the fraction added to the ship's own `armour` above, so a Sidewinder's 60 under
Military Grade Composite is the 210 the outfitting screen shows.

Armour has no size and no rating. The id list files every bulkhead as class 1 and rates the older
hulls `I` while rating the newer ones `A`, `B` or `C` for the same five grades — a placeholder that
distinguishes nothing, and "1I Lightweight Alloy" spoken aloud is a claim about the game that is
not true. Ask for one at a size and it says it has none rather than listing sizes it does not come
in.

Five bulkheads are named in the id list and absent from the figures, all the Lynx Highliner's.
They ship as a name with no numbers, on the same reasoning as a hull with no figures: the symbol is
still what the journal writes, so d47 can say what it is rather than spell it out.

## A ship it has no figures for

Three different answers, because they are three different situations and collapsing them would
tell a Commander flying a brand new hull that Directive 47 is broken:

```text
Corsair is a ship I know of and have no figures for — it is newer than the specification
table d47 ships. I would rather say that than guess at its numbers.
```

That one is in the community's ship data and not yet in its id list, so nothing can key it to what
the journal writes. Its figures are unreachable and its *existence* is certain — which is the
difference between a table that is stale and one that is wrong. The generator carries those names
through into the shipped file rather than dropping them.

A name that was nearly something gets suggestions ("did you mean Anaconda?"). A name that is
nothing at all says so.

## Tools

### `get_ship_specification`

```json
{"type":"object","properties":{"ship":{"type":"string","description":"The ship, by name \u2014 for example \u0022Python\u0022 or \u0022Type-9 Heavy\u0022. Defaults to the one the Commander is flying."}},"required":[],"additionalProperties":false}
```

### `get_module_specification`

```json
{"type":"object","properties":{"module":{"type":"string","description":"The module, by name \u2014 for example \u0022Frame Shift Drive\u0022 or \u0022Power Plant\u0022."},"rating":{"type":"string","description":"Module rating, A to E."},"size":{"type":"integer","description":"Module size, 1 to 8."}},"required":["module"],"additionalProperties":false}
```

A module name **without a size is not a module**. There are thirty-five Frame Shift Drives and they
differ by an order of magnitude in every figure worth quoting, so a bare name returns the sizes
that exist rather than picking one:

```text
Frame Shift Drive comes in 35 variants: 8A, 8B, 8C, 8D, 8E, 7A, ... 2E.
Ask for a size and rating for the figures.
```

Ask for a size that does not exist and it says which ones do — the useful answer for somebody who
asked for a 9A drive.

Drives carry the four numbers a jump range is actually computed from. They are the ones nobody can
eyeball, and every jump calculator needs them:

```text
5A Frame Shift Drive — 20 t, 0.6 MW, integrity 120, 5,103,953 cr. Optimal mass 1050 t, max fuel per jump 5 t.
```

## Notes for anyone reading the code

Modules are keyed by the same symbol the journal's `Loadout` writes, so "what have I got fitted"
and "what is that thing" are one lookup rather than two vocabularies.

**Two modules that look identical and carry different numbers is the failure mode this table is
built the way it is to avoid**, and the sources produce it: `outfitting.csv` calls both
`int_hyperdrive_size8_class5` and `int_hyperdrive_overcharge_size8_class5` a "Frame Shift Drive".
Left alone the table would hold three different 8A drives under one name.

So colliding rows get a qualifier **derived from the symbol** — whatever token one member of the
group has and the others do not. That yields `Frame Shift Drive (mkii overchargebooster)` rather
than a guess at what the outfitting screen calls it. Frontier's own placeholder rows
(`int_missing_*`) are dropped: a "0Z Frame Shift Drive" in a list of the sizes a drive comes in is
a lie about the game.

What the name already says is then struck out of the qualifier, because that is the same word twice
rather than a distinction. `outfitting.csv` calls `int_corrosionproofcargorack_size5_class1` a
"Cargo Rack", the same as the plain rack, so the tokens separating them are `cargorack` and
`corrosionproofcargorack` — and `Cargo Rack (cargorack)` was harder to say than the name it
qualified while telling a listener strictly less. Striking `cargorack` leaves nothing on one and
`corrosionproof` on the other, which is the distinction that was actually there. Striking can only
remove information, so it is kept only while the group still comes apart; a group left ambiguous by
it keeps its raw tokens and reads badly rather than reading wrong.

Mount words are mapped to a closed set of three rather than echoed. A value outside it reads as no
mount, which is true of most modules; passing an unrecognised word through would put whatever the
id list starts writing straight into something spoken aloud.

Empty is a real answer and zero is not. A module with no mass entry and a module that weighs
nothing are different claims, and the generator writes an empty cell for the first.
