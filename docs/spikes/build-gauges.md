# What the corpus says about a build's two numbers

Measured 2026-08-20 while building list.md Phase 38, against the 919-journal corpus on this
machine and against the two shipped tables. Everything here was run before anything was promised,
and `spike/BuildGaugeProbe` re-runs all of it through the code that actually ships:

```
dotnet run --project spike/BuildGaugeProbe
```

It exits non-zero if any figure below stops holding, so it is a gate rather than a report.

---

## 1. Jump range is exact, and it settles what `MaxJumpRange` means

```
jump = (max_fuel / fuel_multiplier) ^ (1 / fuel_power) × optimal_mass / mass  +  booster bonus
```

Every constant is per drive in `EliteSpecifications.tsv`. Replayed over **2,876 `Loadout`
events**, comparing the result against Frontier's own `MaxJumpRange`:

| mass used | median error | p95 | within 0.5% |
|---|---|---|---|
| unladen + full tank | 5.4% | 7.1% | 0.0% |
| **unladen + one jump's fuel** | **0.000%** | **0.000%** | **99.3%** |
| unladen only | 0.9% | 1.6% | 5.4% |

So `MaxJumpRange` is measured at unladen mass plus `MaxFuelPerJump`, not at a full tank — which
is why the gauge's *best* needle is that figure and not a theoretical one. A gauge that quietly
disagrees with the number on the outfitting screen looks broken.

**The 19 events that miss are all the same thing.** Every one has a Guardian FSD Booster and an
SCO drive fitted, and in every one the core formula reproduces Frontier's figure to 0.000% once
the booster's bonus is left out — that is, *their* figure omits the bonus. A Frontier-side
inconsistency in the opposite direction from a modelling error. The probe asserts that every miss
is of this shape, so a new kind of miss fails the run instead of hiding inside the percentage.

### Mass reconstructs too

Hull mass plus every fitted module's mass, engineering applied, reproduces `UnladenMass` with a
median error of 0.000% and 99.6% inside 0.1% over 2,392 comparable events. That is what makes the
*modelled* half trustworthy: a build the Commander is planning has no reported mass to read.

### One column had to be added by hand's-breadth

The Guardian FSD Booster's `jumpboost` was not mapped by the generator. It is one column from a
source already read — coriolis's `internal/guardian_fsd_booster.json` — and the five values are
4.00, 6.00, 7.75, 9.25 and 10.50 ly by class.

---

## 2. A planned roll compounds, and it models a *finished* grade

`Blueprints.tsv`'s `effects` column carries an attribute, a change and a good-or-bad flag **per
grade**, and has since remediation 15 item 8. What Phase 38 adds is applying those percentages
rather than only printing them.

Every distinct engineered module in the corpus, modelled from its own blueprint row and compared
against the `Modifiers` Elite wrote — **374 comparisons**, median error 0.000%, **97.6% exact**.

**The table does not say whether a blueprint and an experimental compound or add. They compound.**
Of the 27 comparisons carrying both, `stock × (1 + blueprint) × (1 + experimental)` is exact on
all 27; adding the two is out by a median 1.4%.

**Quality is not a multiplier on the effect.** Modelling `stock × (1 + pct × quality)` is *worse*
than modelling the full grade — 90.9% exact against 97.4% — and every one of the eight remaining
misses is a module Elite reports at a quality below 1.0, landing short of the model. So the model
is "what this roll lands on when it is finished", which is exactly what a *plan* means, and it is
why a modelled figure must never be drawn as though it were measured.

**One finished roll still misses, and it is upstream.** A grade 5 Lightweight Alloy on the Caspian
Explorer: EDEngineer publishes the mass change as −56% and Elite applies −55%, so 15 t lands at
6.75 where the table says 6.6. The probe carries that as a named ceiling of one, so a second one
fails the run.

---

## 3. `ModulesInfo.json` is live-only, and the plan of record was wrong about it

The plan said Elite writes per-module `Power` and `Priority` **693 times** in the corpus, and
proposed replaying them as measured figures. It does not.

All **694** `ModuleInfo` events in the corpus are a bare marker — a timestamp and an event name and
nothing else. The figures live only in `ModulesInfo.json`, which Elite overwrites in place, for the
ship being flown, as of the last time it wrote it. So this is a **live** cross-check and there is
no history to replay it against.

What it does carry is worth having: `Power` per slot, computed by the game with the engineering
already in it, and `Priority` — which is the ladder a later phase could draw. It names no ship, so
`ModulePower.Describes` checks that every slot it lists holds the module the `Loadout` says is
there; a Commander who swaps ships without re-outfitting leaves the previous ship's figures on
disk, and using those would put one hull's draw under another hull's plant.

---

## 4. The offers d47 could not cost, split into two different problems

EDSY says which blueprints a module type may take; EDEngineer says what each costs. Twenty-three
offers had no recipe. They are not one gap:

**A join, not missing data — 18 offers.** EDSY splits a module type EDEngineer does not. The
largest by far is the Supercharged (SCO) drive: EDSY files it as `cfsdo` and the ordinary drive as
`cfsd`, EDEngineer has one module kind called "Frame Shift Drive", so **all eight** of the SCO
drive's blueprints were offered with no recipe behind any of them — on the drive nearly every
Commander now flies. The corpus is what settles that they are the same recipe rather than similar
ones: SCO drives carrying `FSD_LongRange` report `Modifiers` the `cfsd` grade rows reproduce
exactly. `MTYPE_ALIASES` in `tools/gen-blueprints.py` carries that one entry, applied only where
EDSY offers the variant that blueprint anyway.

**The obvious generalisation was tried and rejected.** "File any unanimous sibling recipe under an
uncosted type" puts *Sturdy Mount* on a cargo rack, because EDEngineer's weapon rows carry
`CargoRack_IncreasedCapacity` as a second symbol. A rule that cannot tell those apart invents game
data, so there is one alias and a generator that names every remaining gap each run.

**Genuine absence — the rest, and it did not ship.** `GuardianModule_Sturdy` — Anti-Guardian Zone
Resistance — is offered to nine module types and costed by nothing. EDEngineer carries no Guardian
weapon recipes at all. EDSY holds a name, `maxgrade: 1` and three materials, and marks the
blueprint's Frontier symbol `// TODO` as its own guess; FDevIDs'
`material.csv` has no row for Hardened Surface Fragments, Caustic Crystal or Tactical Core Chip, so
the ingredients cannot be keyed to `Materials.tsv` at all and a recipe whose ingredients cannot be
keyed cannot be costed, gathered or put on a checklist. `special_choke_canister` and
`special_super_penetrator` are in the same state.

### Re-measured 2026-09-01, and closed by a fifth source

Run again against all four sources while working
[#127](https://github.com/dseelinger/d47/issues/127). Two things this section said were wrong, and
the deadlock broke on a source it had never consulted.

| source | the recipe | the three materials |
|---|---|---|
| EDEngineer `blueprints.json` | no `Guardian*` blueprint symbol at all | absent from `entryData.json` |
| coriolis-data `modifications/` | no `GuardianModule_Sturdy`; its `Weapon_Sturdy` is Sturdy Mount, a different blueprint | absent |
| FDevIDs `material`, `microresources`, `commodity`, `rare_commodity` | — | **no row**, searched by display name and by symbol |
| EDSY `eddb.js` | present, and see below | named, and see below |
| the 941-journal corpus | — | no occurrence of any spelling |

**EDSY does not call the materials guesses.** Its rows read
`hasufr : { name:'Hardened Surface Fragments', mattype:'mfc', matgrp:0, rarity:1, fdid:null,
fdname:'TG_Abrasion03' }, // TODO: matgrp,fdid` — and likewise `TG_CausticCrystal` and
`UnknownCoreChip`. The `TODO` is on the material *group* and the numeric id; the Frontier symbol is
asserted. So the block is not "EDSY admits it is guessing" but **one source asserting something no
other source corroborates**, which is a lead rather than an authority — the same standing this
repository gives any single source on game data.

**And the quantities cannot be read even taking the symbols on trust.** The entry is
`misc_agzr : { name:'Anti-Guardian Zone Resistance', maxgrade:1, mats:[ {hasufr:2}, {cacr:1},
{tacoch:1} ], fdname:'GuardianModule_Sturdy' }, // TODO: fdname`. `mats` is one group per grade
everywhere else in that file: of the **65** entries carrying both fields this is the **only one**
where the counts disagree, and of the four ungraded ones the other three have exactly one group. So
either it is three grades and `maxgrade` is wrong, or it is one grade costing all three materials
and the shape is wrong. That difference is what a Commander would go and gather, and nothing on
disk settles it.

**And a fifth source settled the symbols.** ED Odyssey Materials Helper
([jixxed/ed-odyssey-materials-helper](https://github.com/jixxed/ed-odyssey-materials-helper),
source MIT) carries all three in `locale/material/horizons/manufactured.csv`, spelled exactly as
EDSY spells them and arrived at independently — and its spelling is load-bearing in its own app,
being the key it counts a journal inventory by, so a wrong one would show a permanent zero to
every user who gathered one. It also carries the family around them (`tg_abrasion01`,
`tg_abrasion02`, `tg_causticshard`, `tg_causticgeneratorparts`), which is what a table derived
from the game looks like rather than what copying EDSY's three-line entry would produce.

**What they disagree on is withheld entirely**, on the Commander's rule of 2026-09-01: *"If the
two trackers don't agree on an engineering item, remove that from d47's offered engineering."*
Both give the recipe as two Hardened Surface Fragments and one Caustic Crystal at a single grade
from Ram Tah; EDSY alone adds a Tactical Core Chip, and it is the source malformed in exactly that
spot. So the blueprint is dropped from the offer table itself — not costed, and not offered
either, because an offer with no recipe is still a claim and this is a blueprint d47 cannot
describe consistently.

An earlier pass the same day shipped it costed at the intersection of the two, and that was the
worse answer: a recipe missing an ingredient sends a Commander to a workshop with the wrong pile
and nothing on the page ever hinted the list might be short.

**The rule has a price and it is paid by the Guardian FSD Booster**, whose only offer this was:
that module now reads as taking no engineering, which is a claim about Elite rather than about
d47 — the exact distinction §4 exists to draw. Recorded here and asserted in
`OfferedButNotCostedTests` so it stays a decision rather than becoming a surprise.

**The grades came from the capacities**, through Frontier's published ladder
(300/250/200/150/100 = grades 1 to 5), validated on the 16 Thargoid materials in the same screen
that FDevIDs *does* key: the capacity agrees with the published rarity in all 16, across grades 2
to 5. That overrules EDSY on Tactical Core Chip, which it calls rarity 2 against a capacity of
100.

The three materials live in `tools/curated_materials.py`, read by both `gen-materials.py` and
`gen-material-grades.py` so the two shipped tables cannot disagree about them, and each is
dropped automatically the day FDevIDs names it.

So that half stays as the sentence already shipped — *"Frontier engineers this and I have no
recipe for it"* — which is a true claim about d47 rather than a false one about Elite.
`OfferedButNotCostedTests` asserts both halves: the SCO drive is costed now, and the Guardian
weapons are still honestly uncosted, with the reason asserted rather than merely written down.

---

## 5. Two corrections to the plan of record

- **`powercapacity` is spelled `pgen`.** coriolis's power plant rows carry `pgen`; there is no
  `powercapacity` field.
- **`IsDrive` was true of 94 modules that are not drives.** It tested for an optimal mass, and
  thrusters (39) and shield generators (55) have one. Only a drive has `MaxFuelPerJump`, which is
  what the test is actually about — and the specification report had been offering every thruster
  and shield generator a "max fuel per jump" with nothing after it.
