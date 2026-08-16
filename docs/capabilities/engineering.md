---
title: Engineering
group: Knowledge
nav_order: 108
---

What a blueprint costs and changes, and how the roll on a fitted module actually went.

> "what does increased FSD range cost"
> "how good is my frame shift drive roll"
> "what can I engineer on a power plant"

Nothing here touches the network. Two halves, and they answer different questions. **What a grade 5
costs** is a fact about the game and comes from shipped tables. **What it costs *you*** depends on
your rank with the engineer who would roll it, and only your journal knows that — so the two arrive
together, the way [engineers](engineers.md) pairs a directory with your own standing.

## What a blueprint costs

```text
Increased FSD Range — Frame Shift Drive blueprint, grades 1 to 5.

At grade 5: Power Draw +15% (worse), Optimal Mass +55%, Integrity -15% (worse), Mass +30% (worse).

Per application:
  Grade 1: 1 × Atypical Disrupted Wake Echoes
  Grade 2: 1 × Atypical Disrupted Wake Echoes, 1 × Chemical Processors
  Grade 3: 1 × Chemical Processors, 1 × Phosphorus, 1 × Strange Wake Solutions
  Grade 4: 1 × Chemical Distillery, 1 × Eccentric Hyperspace Trajectories, 1 × Manganese
  Grade 5: 1 × Arsenic, 1 × Chemical Manipulators, 1 × Datamined Wake Exceptions

Offered by: Elvira Martuuk to grade 5, Felicity Farseer to grade 5, Mel Brandon to grade 5, Chloe Sedesi to grade 3, Colonel Bris Dekker to grade 3, Professor Palin to grade 3.

You are grade 5 with Felicity Farseer, at Farseer Inc in Deciat: a full grade 5 is 5 rolls, so 5 × Arsenic, 5 × Chemical Manipulators, 5 × Datamined Wake Exceptions.

Experimental effects for Frame Shift Drive: Deep Charge, Double Braced, Mass Manager, Stripped Down, Thermal Spread.
```

The recipe is quoted **per grade**, because it changes as the grade climbs and somebody gathering
for grade 3 needs that row rather than the top one.

The last paragraph before the experimentals is the one that needed the journal. **A full grade is an
exact total, not a rate.** How many rolls a grade takes is `5 − (rank − grade)` and nothing else —
not luck, not the module — so once your rank with an engineer is known, `ingredients × rolls` is
arithmetic. Five rolls of a three-material recipe is fifteen units, and that is a shopping list.

## A grade you cannot roll yet is a gate, not a grind

```text
You are grade 2 with Elvira Martuuk, at Long Sight Base in Khun, and grade 5 needs rank 5. Reaching grade 5 with them takes 16,000,000 cr of profit sold at their workshop.
```

Grade *N* cannot be rolled below rank *N* **at all**. So the answer is the rank you need and what it
costs, never a large shopping list you cannot use. Reputation rises by buying modifications and by
selling exploration data or commodities at that engineer's own workshop; grade 1 arrives with the
unlock and has no price.

## How your own roll went

```text
5 of 6 modules engineered on Fixture, a Krait_MkII.

  Frame Shift Drive — FSD LongRange, grade 5, finished (1.0)
  Thrusters in Main Engines — Engine Dirty, grade 3, 3 rolls to go (0.4)
  Cargo Rack in Slot01 Size6 — CargoRack IncreasedCapacity, grade 5, finished (1.0)
  Shield Booster in Tiny Hardpoint1 — ShieldBooster HeavyDuty, grade 5, finished (1.0)
  Shield Booster in Tiny Hardpoint2 — ShieldBooster HeavyDuty, grade 3, 3 rolls to go (0.2)
```

Name one and it opens up:

```text
Frame Shift Drive.
FSD LongRange at grade 5, rolled by Felicity Farseer.
The grade is finished at 1.0, where 0.85 is as far as the game insists.
Experimental effect: Mass Manager.

What the roll did:
  Mass 26, was 20 (+6, worse)
  FSD Optimal Mass 1692.75, was 1050 (+642.75, better)
```

**The last block is the only place your actual roll exists.** The blueprint and the grade say what
was attempted; these say what came out, in real units, with the figure before engineering beside it
so the change is subtraction rather than a claim. Which direction counts as better comes from the
game — mass going up is worse, optimal mass going up is better — and it is read from a field Elite
writes as `0` or `1`. Read as a true/false it answers false every time, and every improvement on
mass, heat and power draw reports backwards.

Ask about something a ship has several of and both come back, each with its own state, rather than
one being picked for you:

```text
2 fitted modules match 'shield booster':
  Shield Booster in Tiny Hardpoint1 — ShieldBooster HeavyDuty, grade 5, finished (1.0)
  Shield Booster in Tiny Hardpoint2 — ShieldBooster HeavyDuty, grade 3, 3 rolls to go (0.2)
```

## 0.85 is finished, not 1.0

This is the number that would have shipped a bug. Of 994 completed grades measured across a
912-journal corpus, 926 reach exactly 1.0 — so gating "done" on 1.0 looks right in testing and is
wrong in the last 7%. Of the 68 that stop short, the **45 the game let the Commander move on from**
all sat at 0.85 or above, and the 23 genuinely abandoned all sat at 0.8 or below. Nothing falls
between.

So a module you can see is finished is called finished. 0.85 is the **lowest observed completion in
that sample rather than a proven threshold**, and it is carried with its sample size so a larger
corpus can move it.

Rolls remaining are counted towards a **full 1.0**, not towards 0.85: the band is where the game
stops insisting, not a target to aim at. They are quoted only when your rank with the engineer who
rolled it is known, because the step size depends on `rank − grade` and on nothing else — with no
rank, the fill is reported and the roll count is not.

## Why a fitted module says "FSD LongRange"

Because that is what Frontier calls it, and the alternative is inventing a name.

The journal writes the blueprint as a symbol and **never localises it**. Every key of every
`Engineering` block was enumerated across the corpus — 20,526 engineered modules in `Loadout` events
and 6,272 `EngineerCraft` events — and there is `BlueprintName`, `BlueprintID`, and no readable name
anywhere. The experimental effect *does* arrive with one, on all 13,660 that have an effect, which
is why "Mass Manager" reads properly and "FSD LongRange" does not.

Three sources were checked for a way to turn one into the other, and each fell short in its own way:

- **EDEngineer**, which the blueprint table is built from, carries seven fields per recipe and no
  symbol among them.
- **coriolis-data** keys its records on exactly those symbols, and joining on the shared guid reaches
  31 of the 35 blueprint symbols this corpus contains — missing the commonest one of all.
- **EDDiscovery** reaches all 35, but its display names agree with the blueprint table's on only 15
  of them. It says "Heavy Duty Armour" where the table says "Heavy Duty", so adopting them would have
  `get_module_engineering` name a blueprint that `get_blueprint` cannot find.

So the symbol goes out with its underscores taken out and nothing else done to it — the same
treatment stored modules already get. Ugly and true beats invented, and a name the other tool cannot
look up would be worse than either.

## Where to get it

```text
Yttrium — a grade 4 raw material.
You hold 60 of a possible 100.
Found at: Surface prospecting; Crashed Satellite.
Richest of the 20 nearest landable bodies carrying it, from Sol:
  Rich Rock — 1.9%, 9.0 ly
  Poor Rock — 0.4%, 1.0 ly
```

Three kinds of material are found in three genuinely different ways, so this is three answers behind
one question.

**Raw** is a body search. The index will filter on whether a body carries a material and it will
**not** rank on how much — `percentage`, `value` and `count` beside the name are all silently
ignored, and a sort on the material is dropped. So the filter is remote and the ranking is local,
and the sentence says what it ranked over: *the richest of the twenty nearest*, never the richest in
the galaxy, because it provably is not.

**Three raw materials are declined by name.** Rhenium, Lead and Boron are not in the body index at
all — 25 of the game's 28. A search for one comes back empty, and an empty result reads as "there is
none near you", which is a wrong answer wearing the shape of a right one. So it says the index does
not carry it, which is a fact about the index.

**Manufactured and Encoded** come from the shipped origin list, and where an origin names a system
state or a superpower the answer becomes a system search:

```text
Nearest systems reported in Boom, Empire-aligned:
  Alpha Centauri — 4.4 ly, population 12,000,000
```

*Reported* in Boom, not in Boom. State turns over on the background simulation's own tick and the
index is a snapshot — the same crowd-report framing the station stock carries.

## What a trader could make out of what you have

```text
A trader could make it out of what you already hold:
  300 × Iron at 6 for 1 — up to 50
```

The rate is published and was confirmed across 1,096 real trades, so this is arithmetic rather than
a guess. Two rules it will not break.

**Never across a type.** Each trader deals in Raw, Manufactured or Encoded and only one of them, so
a Raw surplus cannot become a Manufactured shortfall at any price. No trade in those 1,096 ever
crossed.

**The line decides the rate, not the type.** Within a line a grade names exactly one material, so a
same-grade exchange is always cross-line and costs the extra six. Reading the journal's `Category`
instead would price the commonest trade there is at a sixth of its cost.

Trades the rules define but the storage cap forbids — grade 1 to grade 5 within a line wants 1,296
units against a cap of 300 — are simply not offered.

## Traders

```text
Nearest traders dealing in raw materials, from Sol:
  Broglie Terminal in 61 Cygni — 11.1 ly, raw, 963 ls in
  Nowhere Dock in Nanomam — 12.0 ly, kind unrecorded
```

**The index knows the trader type outright**, which retires a heuristic this project had already
measured and disliked: reading the published economy rule literally classified 152 of 200 real
traders as Raw, and no galaxy anybody plays in looks like that. Every station carries a
`material_trader` field of Raw, Manufactured or Encoded, and filtering on it returns exactly the
stations that carry the service — 209 either way within 150 light years of Sol.

"Kind unrecorded" is a real state and not a failure: about one trader in fifty carries the service
and no type, which is the same station the economy rule could not place either.


## Tools

### `get_blueprint`

```json
{"type":"object","properties":{"blueprint":{"type":"string","description":"A blueprint by name \u2014 for example \u0022Increased FSD Range\u0022, \u0022Dirty Drive Tuning\u0022 or \u0022Lightweight\u0022."},"module":{"type":"string","description":"A kind of module \u2014 for example \u0022Frame Shift Drive\u0022 or \u0022Power Plant\u0022. Alone it lists what that module can take; with a blueprint it says which of several modules that blueprint is meant for."}},"required":[],"additionalProperties":false}
```

Both parameters are optional and either one alone is an answer, which is the shape
[`find_engineer`](engineers.md) already uses. A module on its own lists what it can take:

```text
Frame Shift Drive takes 3 blueprints:
  Faster FSD Boot Sequence — to grade 5
  Increased FSD Range — to grade 5
  Shielded FSD — to grade 5

Experimental effects: Deep Charge, Double Braced, Mass Manager, Stripped Down, Thermal Spread.
```

A blueprint name that belongs to several modules — "Lightweight" is on armour, sensors and a dozen
other things, with a different recipe each — takes the module as well to say which is meant.

### `get_module_engineering`

```json
{"type":"object","properties":{"module":{"type":"string","description":"A fitted module or its slot \u2014 for example \u0022frame shift drive\u0022, \u0022MainEngines\u0022 or \u0022power plant\u0022. Omit for every engineered module."}},"required":[],"additionalProperties":false}
```

Matches against both the module's name and its slot, because "frame shift drive" is one and
"MainEngines" is the other and you use whichever you can see.

### `find_material`

```json
{"type":"object","properties":{"material":{"type":"string","description":"The material, by name \u2014 \u0022Yttrium\u0022, \u0022Imperial Shielding\u0022."},"near":{"type":"string","description":"Search out from this system. Defaults to the Commander\u0027s own."}},"required":["material"],"additionalProperties":false}
```

### `find_material_trader`

```json
{"type":"object","properties":{"near":{"type":"string","description":"Search out from this system. Defaults to the Commander\u0027s own."},"type":{"type":"string","description":"Which kind of trader.","enum":["Raw","Manufactured","Encoded"]}},"required":[],"additionalProperties":false}
```


## Notes for anyone reading the code

**Only a modification may be multiplied by a roll count.** The blueprint table keeps six different
kinds of recipe in one list and only modifications cost their ingredients *per application*; a
synthesis recipe is a one-off. Multiplying one by a roll count produces a number with no meaning,
delivered with the same confidence as a correct one, so `Blueprint.TotalFor` returns nothing at all
for anything that is not a modification and the rank paragraph never appears for one.

**The trade rate is not this capability's business yet.** Netting a shortfall through a material
trader arrives with the sourcing tools, and it must never cross ledgers or types — see
[the plan](../plans/phase-14-102-engineering.md) Step 9.

**The engineer chosen for the rank paragraph is the highest-ranked one who offers the top grade**,
not the nearest and not the first. Where nobody unlocked can reach it, an outstanding invitation is
mentioned instead, because that is the next step rather than a dead end.

There is no setting. Nothing leaves the machine, so there is nothing to switch off, and a row that
protects nothing is a row you have to read and decide about for no reason.
