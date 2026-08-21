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
weapon recipes at all. EDSY holds a name, `maxgrade: 1` and three materials, and marks *both* the
blueprint's Frontier symbol and all three materials `// TODO` as its own guesses; FDevIDs'
`material.csv` has no row for Hardened Surface Fragments, Caustic Crystal or Tactical Core Chip, so
the ingredients cannot be keyed to `Materials.tsv` at all and a recipe whose ingredients cannot be
keyed cannot be costed, gathered or put on a checklist. `special_choke_canister` and
`special_super_penetrator` are in the same state.

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
