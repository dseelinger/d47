# Phase 38 — A build you can watch

> **Built 2026-08-20 and shipped in v0.43.0, except for half of item 10.** What the corpus
> actually said — including three places this document was wrong — is in
> [docs/spikes/build-gauges.md](../spikes/build-gauges.md), and the amendments are recorded on the
> `list.md` lines themselves. In short: `powercapacity` is spelled `pgen`; the 694 `ModuleInfo`
> events are bare markers carrying no figures at all, so that cross-check is live rather than
> replayable; nothing d47 reads maps a Powerplay entitlement id to a Power's name, so the badge
> says a pledge is needed and does not say whose; and **item 10 split in two** — the SCO drive's
> eight uncosted blueprints were a join and are closed, while Anti-Guardian Zone Resistance cannot
> be costed from any source d47 has and stays honestly uncosted. The honest risk this document
> names — item 2 — came out exact: a blueprint and an experimental **compound**, and the model
> lands on a *finished* grade.
>
> Two calls the Commander made on the day, which this document left open: **horizontal bars rather
> than dials**, and **the priority ladder is out of scope**.

The plan of record, written 2026-08-20 from a design conversation, with the arithmetic checked
against the shipped tables and the journal corpus before anything was promised.

`list.md` carries the Phase 38 entry as of 2026-08-20, unticked. This document is the reasoning
behind those lines and the measurements they rest on; the list is the product description.

---

## The number

**Phase 38.** CLAUDE.md freezes 1–21 and 23–37, retires 22 permanently, and says new phases are
appended. Phase 37 shipped as v0.41.0 on 2026-08-20, so 38 is the next free number and there is
nothing movable below it.

## The phase in one sentence

Two gauges at the head of a ship's slot list — **power** and **jump range** — that stay live while
the Commander edits a build, plus a badge on the modules a pledge is needed to buy, because power
budget and jump range are the two numbers a build is actually designed against and neither is
visible until after the credits are spent.

## Why it is worth a phase

The Loadout tab can now describe a slot exactly and say nothing at all about the ship. A Commander
planning a build is answering two questions on every change — *does this still fit in the power
plant* and *what did that cost me in jump range* — and today they answer both by alt-tabbing to
Coriolis.

**Almost all of the arithmetic is already in the repository.** What follows is what was measured
rather than assumed.

---

## Jump range: solved, and validated against the corpus

Every constant is already in `EliteSpecifications.tsv`, per drive: `optimal_mass`, `max_fuel`,
`fuel_power`, `fuel_multiplier`.

```
jump = (max_fuel / fuel_multiplier) ^ (1 / fuel_power) × optimal_mass / mass  +  booster bonus
```

Checked against **2,862 real `Loadout` events** in the 918-journal corpus, comparing the result to
Frontier's own `MaxJumpRange`:

| mass used | median error | p95 | within 0.5% |
|---|---|---|---|
| unladen + full tank | 5.395% | 7.077% | 0.0% |
| **unladen + one jump's fuel** | **0.000%** | **0.000%** | **99.3%** |
| unladen only | 0.919% | 1.555% | 5.4% |

Two things follow, and the second is the one worth writing down.

**The model is exact.** Not "close enough to show" — median error of zero across 2,862 builds,
engineered and stock, with and without a Guardian booster.

**And `MaxJumpRange` means something specific.** It is measured at unladen mass plus
`MaxFuelPerJump`, not at a full tank. Any gauge that quietly disagrees with the number the
Commander can read in-game looks broken, so the top of the range is anchored to exactly that.

The **19 events out of 2,862** (0.7%) that miss are all cases where the core formula reproduces
Frontier's figure to 0.000% and Frontier's own figure **omits the booster bonus** — every one with
an SCO drive fitted. That is a Frontier-side inconsistency, not a modelling error, and the phase
should not chase it.

### The three needles

The Commander asked for a range rather than a number, from empty to fully laden:

| Needle | Mass | Note |
|---|---|---|
| **Best** | unladen + one jump's fuel | *This is the game's own `MaxJumpRange`* |
| **Middle** | unladen + full tank | "empty cargo, full fuel" |
| **Worst** | unladen + full tank + full cargo | every rack full |

**"Empty fuel" is not a state you can jump in**, so the honest top of the range is one jump's
worth — which lands on the number the outfitting screen already shows. That is a better anchor
than a theoretical figure nothing can corroborate.

### One known gap

The **Guardian FSD Booster's jump bonus is not in the table.** The generator does not map
coriolis's `jumpboost`, so the five bonuses (4.00, 6.00, 7.75, 9.25, 10.50 ly by class) were
supplied by hand for the validation above. One column, same source, same generator.

---

## Power: derivable, with one column missing

**Draw** is in the table for all 814 consumers. The 350 blanks are bulkheads (241), power plants,
cargo racks, cabins, hull and module reinforcement, and fuel tanks — every one of them genuinely
zero-draw, so a blank means zero and that is correct rather than merely convenient.

**Retracted versus deployed needs no new data.** It falls out of `mtype`'s first letter:

| prefix | what it is | drawn when |
|---|---|---|
| `h*` | hardpoint weapon | deployed only |
| `u*` | utility mount | always |
| `c*` | core internal | always |
| `i*` | optional internal | always |

This is the distinction that is hardest to keep in your head by hand: a shield booster sits in a
`TinyHardpoint` slot, so it looks like a hardpoint, and it is `usb` and draws all the time.

**The gap: power plant capacity is not in the table.** All 42 plant rows carry an empty `power`
column, because for a plant the meaningful figure is output and the generator maps draw. Without
it there is a numerator and no denominator. coriolis carries `powercapacity`; this is one column
from a source already read.

### A measured cross-check exists

Elite writes `ModulesInfo.json` beside the journal, carrying per-module `Power` **and**
`Priority` — engineering included, computed by the game. It appears **693** times in the corpus
against 2,862 `Loadout` events, so roughly a quarter as often, and only ever for the ship being
flown.

That is the same shape as the roll figures the tab already treats as measured: authoritative when
present, absent often, never invented. It also makes the priority ladder answerable — *what browns
out when the plant is at half health* — which is a question no spreadsheet answers today.

### Planned rolls: already modellable, from a table that has always shipped

> **Corrected 2026-08-20, the same day this was written.** The first draft said `Blueprints.tsv`
> carried ingredients, grades and engineers and **no stat deltas**, and proposed adding coriolis's
> `modifications.json` to get them. That was wrong, and it was wrong about a file this repository
> already ships and already draws a sentence from: the Loadout tab's *Effect* line has been
> deriving "Mass −85%, at the cost of Integrity −50%" from it since remediation 15 item 8. The
> error would have cost a whole item and a second source for data already on disk.

`Blueprints.tsv` carries an **`effects`** column — attribute, change, and a good-or-bad flag per
line — and it carries it **per grade**, on all 786 modification rows and all 154 experimental ones:

```
Increased FSD Range g4 | Power Draw|+12%|bad;Optimal Mass|+45%|good;Integrity|-12%|bad;Mass|+25%|bad
Increased FSD Range g5 | Power Draw|+15%|bad;Optimal Mass|+55%|good;Integrity|-15%|bad;Mass|+30%|bad
```

Both attributes the gauges need are in it: **Power Draw** on 280 rows, **Optimal Mass** on 30,
plus Power Generation and Power Capacity for the plant. Mass is on 388, which the jump gauge needs
too.

So a planned roll is modellable now: the change is a percentage, and the base figure it applies to
is the module's own `power` or `optimal_mass` in the specification table. **No new source, no new
table, no new licence question.** What the phase adds is applying those percentages rather than
only printing them.

Two things to check while building rather than assume:

- **Percentages compound or they do not.** A slot with a blueprint *and* an experimental has two
  sets of changes to the same attribute, and whether Frontier multiplies or adds them is not
  something this table states. It is testable: take engineered modules from the corpus, apply both,
  and compare against the `Modifiers` Elite actually wrote.
- **`ShipsMode` says the figures are the top grade's.** The comment above the Effect block claims
  that, and the rows above show per-grade figures with the lookup already filtering on
  `recipe.Grade == plan.Grade`. One of the two is stale. Read it before relying on either.

> **This is an amendment to a stated rule, and goes in as one.** `ShipsMode.Parted` says today
> that the effects shown are the fitted module's own and never a plan's, because *"showing
> modelled ones beside measured ones is the failure the specification table is built the way it is
> to avoid."* That rule is right about a slot row and wrong about a design gauge: a power budget
> you cannot see until after you have spent the materials is not a budget. The amendment is
> narrow — **these two gauges only** — and the two kinds of number must stay visually distinct
> wherever they meet.

The `effects` column serves both gauges: **Optimal Mass** is in it, so planned Increased FSD
Range is computable from the same rows as planned power draw.

---

## The badge: modules a pledge is needed to buy

FDevIDs carries an **`entitlement`** column that the generator currently discards. It is the
purchase gate, and it is precise:

| family | count | what it gates |
|---|---|---|
| `ELITE_SPECIFIC_V_POWER_<id>` | **19** | Powerplay modules — and the id names *which* Power |
| `ELITE_HORIZONS_V_*` | 168 | Horizons and the Guardian tech-broker unlocks |
| `ELITE_V_<ship>` | 51 | ship-specific entitlements |

The 19: Prismatic Shield Generator (all eight sizes), Pacifier Frag-Cannon, Imperial Hammer Rail
Gun, Cytoscrambler Burst Laser, Pack-Hound Missile Rack, Enforcer Cannon, Retributor Beam Laser,
Concord Cannon, Advanced Plasma Accelerator, Mining Lance, Pulse Disruptor Laser, Rocket Propelled
FSD Disruptor.

So the badge is not a hand-maintained list — it is one column the generator must stop dropping,
and it is exact enough to say which Power gates each module. d47 already tracks the Commander's
pledge (`PowerplayPledge`), so the ones their current pledge can actually buy can read differently
from the ones it cannot.

**The glyph is mechanically trivial and needs a capture to accept.** The engineered mark is
`new Run(" ⚙")` themed to a colour key; a coin is the same change with a red key. Choosing the
character is the part that needs care — a glyph the shipped font lacks renders as tofu, and only
an eye or a capture catches that. Phase 37 already records that trap.

---

## The items, as `list.md` would carry them

Each line carries its own acceptance criteria, in this repo's style.

1. **The generator stops dropping three columns.** `entitlement` from FDevIDs, `powercapacity`
   from coriolis for the 42 power plants, and `jumpboost` for the five Guardian FSD Boosters.
   *Accepted when:* the table's header counts are unchanged except for the new columns, the four
   irregular hulls still pass `SlotLayoutTests`, and a test asserts a Prismatic Shield Generator
   carries a Powerplay entitlement while a stock shield generator carries none.

2. **Apply the effects already shipped.** `Blueprints.tsv`'s `effects` column, per grade,
   applied to the module's base figure rather than only printed as a sentence. **No new source.**
   *Accepted when:* a test takes fitted engineered modules from the corpus, applies the blueprint's
   own grade row to the stock figure, and lands on the `Modifiers` Elite actually wrote — for
   `PowerDraw` and `FSDOptimalMass` at least. That test is also what settles whether a blueprint
   and an experimental on one module compound or add, which the table does not state.

3. **Jump range, computed.** The formula above, over any mass.
   *Accepted when:* replayed across every `Loadout` in the corpus it reproduces `MaxJumpRange`
   with a median error of 0.000% and at least 99% within 0.5%, and the ~0.7% booster disagreements
   are asserted as known rather than silently tolerated.

4. **The jump gauge**, three needles, at the head of a ship's slot list.
   *Accepted when:* the best needle equals the figure Elite reports for the ship being flown, the
   gauge redraws as slots are planned, and a ship d47 cannot see into says so instead of drawing a
   figure it cannot stand behind.

5. **Power, computed, retracted and deployed.** Draw summed by `mtype` prefix, against plant
   capacity.
   *Accepted when:* both totals are shown separately; a shield booster counts as always-on and a
   multi-cannon does not; and where `ModulesInfo.json` is present its per-module figures are used
   and any disagreement with the table is reported rather than averaged away.

6. **The priority ladder.** What is powered at each priority group, and what browns out first.
   *Accepted when:* it agrees with `ModulesInfo.json`'s `Priority` where that file is present.

7. **Planned rolls modelled, and marked as modelled.** Both gauges read a planned build, using
   item 2's table.
   *Accepted when:* a modelled figure is visually distinct from a measured one everywhere the two
   appear together, and switching a slot from planned to fitted moves the number from one kind to
   the other without changing its meaning.

8. **The entitlement badge.** A red coin glyph on modules gated behind a pledge, in the chooser
   and on the slot row.
   *Accepted when:* the 19 Powerplay modules carry it and nothing else does; the glyph renders in
   a headless capture rather than as tofu; and a Commander pledged to the gating Power reads
   differently from one who is not.

9. **Ask before the plan and the checklist drift apart.** Boarding a ship whose build carries
   engineering the checklist has not got is the moment to say so: *"Do you want to move or replace
   the checklist items that have been added or changed?"*, yes or no.
   *Accepted when:* it fires on a ship swap and only where the build and the checklist actually
   disagree; **no** leaves both untouched and does not ask again for that same difference; and
   **yes** revises rather than rebuilds, so the ordering the Commander spent an evening on
   survives — which `ChecklistDocument.Revise` already guarantees and this must not route around.
   The prompt is the Commander's act of accepting, exactly as pressing *Put this build on my
   checklist* is; it must not promote anything on its own.

---

## What this phase does not do

- **It does not model damage, shields or DPS.** Those are builds' other axes and they are not what
  was asked for.
- **It does not become an outfitting simulator.** The gauges answer two questions; a build that
  needs more than that belongs in Coriolis and d47 should not pretend otherwise.
- **It does not touch the tool surface.** Both gauges are panel-side and spend none of the
  remaining bytes. Whether the model can *ask* for a power budget is a separate decision, and the
  cheaper one to make later.


10. **Recipes for what Frontier engineers and EDEngineer does not carry.** Reported 2026-08-20
    against a Guardian Gauss Cannon, which the page said had no engineering: it has **Anti-Guardian
    Zone Resistance**, and Rapid Fire besides. The two shipped tables already disagree in a way that
    names the gap exactly — EDSY's offer list says `hexgg` takes `Weapon_RapidFire` and
    `GuardianModule_Sturdy`, while `Blueprints.tsv` has a row for neither, because **EDEngineer
    carries no Guardian weapon recipes at all**. Every Guardian hardpoint is in that state, and
    `misc_agzr` is offered to the power plant, power distributor, Guardian FSD Booster and the
    hull, shield and module reinforcements too. EDSY has the missing half: name, `maxgrade: 1`,
    materials and a Frontier symbol — which EDSY itself marks `// TODO: fdname`, so it is a lead
    rather than an authority and the generator must say which source each row came from.
    *Accepted when:* a Guardian Gauss Cannon offers Anti-Guardian Zone Resistance with a costed
    recipe; `OfferedButNotCostedTests` — which today asserts the gap exists — is inverted to assert
    it is closed; EDSY's material keys (`hasufr`, `cacr`, `tacoch`) resolve to `Materials.tsv`
    symbols rather than being written through unmapped; and a symbol EDSY guesses at is recorded as
    a guess rather than shipped as a fact.

    Until then the surface tells the truth about itself rather than about Elite — *"Frontier
    engineers this and I have no recipe for it"* — which is shipped and is not a substitute for
    this item.

---

## Sequencing

Items 1, 2 and 10 are data and unblock everything else; 3 and 5 are pure functions over that data
and are testable against the corpus with no UI at all; 4, 6 and 7 are the surface; 8 and 10 are
independent of the gauges entirely and either could ship first — 8 if the glyph question resolves
quickly, 10 whenever somebody is in the generator.

The honest risk is item 2. It is the only piece with no ground truth in the corpus to check
against — a modelled figure can only be validated against modules that have *already* been rolled,
which is a narrower test than the thing it is used for.
