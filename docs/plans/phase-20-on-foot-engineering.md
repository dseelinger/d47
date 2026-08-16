# Phase 20 — On-foot engineering

The plan of record for list.md Phase 20. Written 2026-08-16, **after** the phase's own spike ran,
because the spike moved two of the six items before either was specified.

`list.md` reads top to bottom as a description of the product. This is the order the work happens in,
and the reasoning the order cannot carry on its own.

---

## What the spike changed, before anything was built

[journal-corpus-on-foot.md](../spikes/journal-corpus-on-foot.md) was run first, and it turned two
open questions into answers and one settled belief into a defect.

**The settled belief was the dangerous one.** list.md's second item says an on-foot build's material
cost is *"exactly and completely knowable"* — the inverse of the ship problem, no roll count, no
hedging. That is true of the game and false of the source: **every on-foot quantity in EDEngineer is
pre-patch**, by a factor of two for modifications and three for grade upgrades, plus an ingredient
Frontier removed. Shipping the file as it stands would have quoted a Commander two to three times the
real cost of everything on foot, in the one part of the phase that was supposed to need no caveat.

So the first decision this plan makes is about the generator rather than about a feature:

> **EDEngineer is the authority on what a recipe contains and is not the authority on how much.**
> The quantities come from a measured remap whose every key was observed in the game, and the
> generator prints the correction it applied rather than performing it silently.

The two questions the spike was written for are also closed — the locker cap is **per category, at
1,000**, and the barter rate **composes exactly** — and both feed the sourcing item rather than the
knowledge one.

## The order, and why

**1. `d47 knows what you are wearing`** — leads, and would lead even if it were not the cheapest.
Nothing else in the phase can be checked against reality until d47 can read a loadout: a plan for a
grade 4 Maverick is unverifiable while d47 cannot see the Maverick. It also stands alone — *"what am
I wearing and what grade is it"* is unanswerable today — so it is the item that pays off first if the
phase is interrupted.

**2. `Know what on-foot engineering does`** — the facts layer, and the generator correction above
lives here. Nothing downstream can quote a number until this is right.

**3. `Who unlocks whom, on foot`** — separate from 2 because it has a separate failure mode. A wrong
recipe wastes materials the Commander still has; a wrong unlock wastes a trip. It also has a
different source: the referral chain is in EDEngineer's data, the tribute quantities in it are stale,
and the four Colonia engineers are not in it at all.

**4. `Go and get it, on foot`** — before planning rather than after, because it is where the spike's
two answers land, and because a plan that cannot say where to get the shortfall is half an answer.

**5. `Plan a suit or a weapon`** — last of the code, because it composes all four: it needs the
loadout to diff against, the recipes to cost, the unlock chain to order, and the sourcing to be
useful. It rides the Phase 17 checklist substrate unchanged.

**6. `Read the sources nobody has read yet`** — a standing item, run at the end, and it is a document
rather than a feature.

The spike is not in this list because it ran first. That is the Phase 16 lesson applied inside a
phase: a spike that gates the items beside it has to precede them, or the phase cannot be planned as
a unit.

## Decisions taken before the code

### The mod-symbol join has no source, and gets an alias table with the measurement behind it

The journal names a fitted modification `weapon_clipsize`; the recipe table names it "Magazine size".
Five of thirteen observed symbols join on a relaxed match and eight do not, and **nothing in any
checked source carries both spellings** — not EDEngineer, not FDevIDs, not EDDiscovery's `Items`.

This is the `Engine_Dirty` problem again, and Phase 17 already ruled on it: answer *cannot say*
rather than guessing. But the ship case had 786 blueprints and no prospect of an authored map; this
one has **25 modifications total**, which is a table a person can write and check by eye.

So: an explicit alias table, in the generator, printed and asserted — with anything not in it
reaching `ChecklistState.Unverified` and the existing sentence, exactly as a ship blueprint does.
That is a spelling reconciliation and not game data, the same category as the `Ballistic Data` alias
`tools/gen-engineers.py` already carries.

### The suit and weapon table is generated, and keyed on what the journal writes

FDevIDs has neither list, which inverts the ship arrangement. EDDiscovery's `Items/Suits.cs` and
`Items/HandItems.cs` (Apache-2.0) are keyed on exactly the symbols `SuitLoadout` writes, and the
corpus agrees on all 16 it exercises.

**Names and ids join; the stats do not come.** EDDiscovery's per-grade figures have per-figure
provenance that varies, one row annotated as a guess, and a transcription bug that assigns health
multipliers to the shield fields. Names, classes and the tool each suit carries are taken. Numbers
are not.

**`flightsuit` has no class suffix** and is the one suit that cannot be upgraded or modified. It is
in the table so that reading a grade off a symbol has somewhere to stop.

### Nothing speaks `SuitName_Localised`

269 of 768 `SuitLoadout` events carry an unresolved token that says **Class1** whatever the real class
is. This is not an edge case to guard, it is a third of the data. `SuitName` plus the table, always.

### The plan is the Phase 17 substrate with two new intent kinds and one new scope

Keyed by **suit** and by **weapon**, which is a third and fourth thing a `ChecklistScope` can be about
beside a ship and a system. The alternative — folding an on-foot item into the ship scope — would put
a Maverick's mod list on whichever ship happened to be under the Commander at the time.

Two ordering facts the ship plan has no analogue for, and both are steps rather than footnotes:

- **A grade 1 item has zero mod slots.** Grade before mods, always.
- **An engineer's base has no Pioneer Supplies.** So the grade cannot be bought on arrival, which
  turns the ordering constraint into a routing one.

### Permanence is said out loud, once, per item

Four slots, no undo, and a wrong mod recoverable only by buying and re-upgrading a fresh item. The
`Noted` flag on `ChecklistItem` already exists for exactly this shape of thing — said once, not on
every tick.

## Release

A completed phase is a minor release: **`v0.15.0`** after `v0.14.0`.

The generator correction lands in the same release as the tables it corrects, and the two must not be
separated — a `Blueprints.tsv` with the new quantities and a build that does not know why is a file
nobody can check.
