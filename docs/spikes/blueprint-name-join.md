# Does anything d47 ships join Frontier's blueprint symbol to the table's blueprint name?

**No, and this is where the looking stopped.** Recorded 2026-08-16, during Phase 17.

## Why it matters

Phase 17's ship plan promises that progress is *a diff against the live `Loadout`*. A plan says
"grade 5 Dirty Drive Tuning on MainEngines"; the journal says
`"Engineering":{"BlueprintName":"Engine_Dirty","Level":5,"Quality":1.0}`. Everything else in that
sentence checks exactly — the slot, the grade, and whether the grade is finished. The **name** does
not, because the two vocabularies never meet.

## What was already established, and not re-derived

`EngineeringCapability` records the measurement that started this: **the game writes the blueprint
as a symbol and never localises it**, across 20,526 engineered modules and 6,272 `EngineerCraft`
events in the 912-journal corpus. Neither carries a readable blueprint name. That is why the
capability already prints `ModuleNames.Readable(blueprint)` — Frontier's own name with the
underscores taken out, "ugly and true beats invented".

So the question is not whether the journal has the friendly name. It is whether anything **else**
d47 ships has both spellings side by side.

## Where the search went

| Looked in | Carries the journal symbol? |
|---|---|
| `src/D47.Core/Knowledge/Blueprints.tsv` | **No.** Columns are `kind, module, name, grade, engineers, ingredients, effects, guid`. `guid` is coriolis's per-grade uuid, not a name. |
| `src/D47.Core/Knowledge/Materials.tsv` | No — material symbols, a different namespace. |
| `src/D47.Core/Knowledge/Engineers.tsv` | No. |
| `src/D47.Core/Knowledge/EliteSpecifications.tsv` | No. Its `[modules]` symbols are per-hull and per-size (`adder_armour_grade1`), and its names are module names, not blueprint names. |
| `src/D47.Core/Journal/MaterialGrades.g.cs` | No. |
| `tools/gen-blueprints.py` inputs | EDEngineer's `blueprints.json` is keyed on `Type` / `Name` / `Grade`; the cross-check is coriolis's per-grade `uuid`. Neither is `Engine_Dirty`. |
| `EDCD/FDevIDs` | No. Its 24 CSVs cover ranks, commodities, materials, outfitting and engineers, and stop there — the same absence `colonisation-sources.md` found for facilities. |
| The journal itself | `BlueprintID` (e.g. `128673659`) is present beside `BlueprintName`, and nothing d47 ships resolves it. |

## What has *not* been checked, and is the obvious next move

**coriolis-data's blueprint keys.** The generator reads that repository for its per-grade `uuid`
and nothing else. Its blueprint records may be keyed by a symbol resembling the journal's, in which
case a `symbol` column could be added to `Blueprints.tsv` by the generator, joined on the `uuid`
d47 already carries — derived, with its provenance recorded, exactly as everything else in that
namespace is.

That was not done here for two reasons and both are stateable: it needs a network fetch of a
repository this pass did not open, and the licence question would have to be answered again for a
*name* mapping rather than for recipe figures. It is a generator change and a spike of its own,
not a line of Phase 17.

**This page exists so that "no join exists" is never written down again as a conclusion.** It is
not a fact about the galaxy. It is a fact about seven files and one repository nobody opened —
which is the sixth worked example in [README.md](README.md) wearing a seventh costume.

## What ships instead

`ChecklistNaming.Confirms` answers **true** or **null**, never false. Null becomes
`ChecklistState.Unverified` and a sentence naming both spellings, said **once** per item. A plan
built from the ship itself carries the journal's symbol and confirms exactly; a plan built from a
conversation carries the table's name and reads unverified. Guessing `false` would tell a Commander
their finished module is unfinished, which is the one direction that must not happen.

**An experimental effect is the control case.** Elite localises that one — "Thermal Spread" arrives
as words — so the identical comparison is exact, with no null in sight. The gap is Frontier's
spelling of one field, not a shortcoming of the comparison.
