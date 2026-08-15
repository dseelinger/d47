# What the on-foot engineering sources actually contain

**Measured 2026-08-14.** Parsed from the live files and researched against community sources. This
is the ground under Phase 19, and the reason that phase exists separately from the ship engineering
items rather than as a footnote to them.

**Read the gaps as open, not closed.** Where this document says something was not found, it means
exactly that — not that it does not exist. On-foot is less documented than ships, not undocumented,
and every "not found" below is a lead rather than a verdict.

---

## 1. On-foot is not the ship feature with a different vocabulary

Two axes, and neither one is the ship model.

| | Grade | Modification |
|---|---|---|
| Applied at | **Pioneer Supplies**, the Concourse shop | An engineer |
| Range | 1 → 5 | **Ungraded** — a mod is present or absent |
| Randomness | **None.** Fixed material list, fixed stat ladder | None |
| Reversible | Buy a higher grade | **Never.** A mod cannot be removed or replaced |
| Limit | — | **Four per item**, one slot earned per grade above 1 |

> "You upgrade suits at pioneer supplies. You modify them at engineers."
> — [Steam](https://steamcommunity.com/app/359320/discussions/0/595145370018823747/)

> "Unlike ship mods, equipment mods are only available in a single quality grade, and require only a
> single transaction with the Engineer to fully apply it."
> — Fandom *Engineers* page, via search excerpt (the domain returned HTTP 402 to direct fetch)

Three consequences that shape the whole phase:

- **The ship items' floor-and-rolls hedge does not apply.** There is nothing random to hedge against.
- **Planning matters more here than for ships.** Four slots, permanent, no way to practise. A wrong
  mod is unrecoverable except by buying and re-upgrading a fresh item.
- **There is an ordering constraint ships do not have.** A grade 1 item has **zero** mod slots, so
  "upgrade before you fly to the engineer" is a real step in the right order.

## 2. EDEngineer holds 90 on-foot rows, and they are two different things

Of 1,172 blueprints, 63 distinct `Type` values, exactly two are on-foot gear: `Suit` (26) and
`Weapon` (64).

**Grade distribution is the tell:**

```
Suit    : Grade key ABSENT 14,  G2 3, G3 3, G4 3, G5 3
Weapon  : Grade key ABSENT 20,  G2 11, G3 11, G4 11, G5 11
```

- **34 rows have no `Grade` key at all** — the key is absent, not zero. These are the real engineer
  modifications: 14 suit, 20 weapon.
- **56 rows graded 2–5, all owned by the pseudo-engineer `@Merchant`** — these are not engineering.
  They are the Pioneer Supplies **grade upgrade recipes**: 3 suits × 4 grades + 11 weapons × 4
  grades. Grade 1 never appears, because the baseline item is bought rather than upgraded into.

Whole-file grade distribution is `{1:183, 2:192, 3:192, 4:171, 5:169, absent:265}`. A schema
assuming every blueprint carries a grade 1–5 breaks on 265 rows, including all 34 on-foot mods.

`CoriolisGuid` is present on 838 ship rows and on **none** of the 90 on-foot rows — EDEngineer
itself signalling that coriolis-data has no on-foot coverage. Confirmed independently: the full
coriolis tree is 161 files and contains no suit, weapon or micro-resource data at all.

## 3. Ingredient sizes are real quantities — the inverse of ships

The ship finding was that every ingredient entry is `Size: 1`, which is what made ship totals
unquotable without a roll count. On-foot is the opposite:

```
34 ungraded mods   (159 entries): {10: 88, 5: 48, 15: 23}          — only 5/10/15, never 1
56 @Merchant rows  (292 entries): {1:45, 5:73, 10:45, 15:73, 25:28, 35:28}
```

The upgrade tiers are strictly patterned: G2 uses 1/5, G3 uses 5/15, G4 uses 10/25, G5 uses 15/35.

**So an on-foot build's material cost is exactly and completely knowable.** No floor, no estimate,
no caveat. That is a stronger answer than the ship side can give even now.

Ingredient counts per blueprint are 4–6 for on-foot against a ship mode of 3.

## 4. The unlock chain is partly sourceable — unlike ships

Phase 14 refused to ship a referral graph for ship engineers because none was found. On foot, six of
them are **in the data**: `Type: Unlock` rows whose `Engineers` array names the *referring* engineer.

```
Kit Fowler       ← Domino Green      Push 5, Opinion Polls 40
Yarden Bond      ← Kit Fowler        Surveillance Equipment 5, Smear Campaign Plans 8
Wellington Beck  ← Hero Ferrari      Settlement Defence Plans 15, Classic/Multimedia/Cat Media 25 each
Uma Laszlo       ← Wellington Beck   Insight Entertainment Suite 5
Terra Velasquez  ← Jude Navarro      Genetic Repair Meds 5
Oden Geiger      ← Terra Velasquez   Financial Projections 15, Biological Sample 20, …
```

Domino Green, Hero Ferrari and Jude Navarro have no unlock row because they are the three roots.
The structure is three chains of three. The four Colonia engineers have no rows at all (§7).

Community sources give the non-material half of each unlock — travel 100 ly by Apex for Domino
Green, ten on-foot Conflict Zones for Hero Ferrari, Unfriendly with Sirius Corporation for Uma
Laszlo — from [E:D Black Box](https://edblackbox.com/guides/engineering/engineering-manuals/checklist.html)
and [Inara](https://inara.cz/elite/engineers/). **Not yet cross-checked against a second independent
source**, and worth doing before shipping, because a wrong unlock requirement costs a trip.

## 5. Micro-resources join cleanly, with one known drift

FDevIDs `microresources.csv`: 196 rows, columns `id, symbol, category, English name`. Categories are
`Data 114, Item 43, Component 33, Consumable 6`.

**83 of the 84 distinct on-foot ingredient names match exactly.** The one failure is
`Ballistic Data` (EDEngineer) against `Ballistics Data` (FDevIDs) — already a named alias in
`tools/gen-engineers.py` from the ship work, so the fix is free.

Three further EDEngineer names have no FDevIDs match — `Geographical Data`, `Mineral Analytics`,
`Security Plans`. Not chased down yet; a symbol-substring search found nothing obvious, but that is
one search, not a conclusion.

## 6. Sourcing detail is richer than anything ships get

`entryData.json` (UTF-8 **with BOM** — plain `json.load` fails, use `utf-8-sig`) carries 371
entries, of which 201 are `OdysseyIngredient`, sub-grouped
`Data 118, Item 44, Circuits 12, Tech 11, Chemicals 10, Consumable 6`.

`OriginDetails` is coarse for Odyssey — only five distinct strings, led by `Planetary Settlement`
(195). The real detail is in three Odyssey-only fields:

```json
{"Name":"Aerogel","Kind":"OdysseyIngredient","Group":"Chemicals",
 "ValueCr":500,"BarterCost":9,"BarterValue":5,
 "SettlementType":[], "BuildingType":["LAB","PROC","RES","IND","EXT","AGRI"],
 "ContainerType":["Industrial Locker (S)","Research Locker (L)"]}
```

`SettlementType` (ALL, High Tech, Industrial, Research, Tourist), `BuildingType` (16 codes: AGRI,
BAR, CBN, CMD, DORM, EXT, HAB, IND, LAB, MED, OPR, PROC, PWR, RES, SEC, STO) and `ContainerType`
(22 values). **"Which building, which locker" is answerable on foot in a way it never is for ships.**

`BarterCost` and `BarterValue` are present on **exactly the 33 Components** and nothing else — which
matches the game rule that only Components can be bartered. Whether those two numbers compose into a
usable exchange rate is untested and is the most promising lead d47 has for a conversion table it
never got for ship materials.

## 7. What has not been found yet

Every row here is a lead, not a verdict. The information is out there; this is a record of where the
search stopped, so the next person starts further along.

| Gap | Where to look next |
|---|---|
| **No suit or weapon ids in any checked source.** FDevIDs has no `suits.csv` or on-foot `weapons.csv`; `outfitting.csv` is ship modules only | The **journal itself** — `SuitLoadout` carries `SuitID`, `SuitName`, `LoadoutID` and per-module `SuitModuleID`. That makes the journal the id authority and inverts the ship arrangement. Also worth checking the Frontier CAPI docs in FDevIDs' `Frontier API/` folder, which mention `onfootmicroresources` and `pioneersupplies` |
| **Base stats for suits and weapons** — damage, magazine, shield, armour | Inara publishes per-item equipment-blueprint pages with fixed stat ladders (e.g. Maverick shield +22.5% at G2 rising to +125% at G5). Not yet harvested |
| **26 of 34 mods carry no effect values** in EDEngineer | Inara's mod pages give flat figures (Improved Battery Capacity +50%, Faster Shield Regen +33%, Greater Range +50%, Headshot Damage ×1.5). A second source to derive from rather than hand-write |
| **The complete weapon-mod → engineer map.** Only Domino Green and Yi Shen verified in full | Per-engineer Inara pages exist for all thirteen; only two were read |
| **Nothing at all for the four Colonia engineers** — Baltanos, Eleanor Bresa, Rosa Dayette, Yi Shen | Their specialities are described as Dynamic / Force / Strategic slices, and Yi Shen's four mods *were* read from Inara. So the data exists in community sources even though it is absent from EDEngineer |
| **Ship-locker cap: 1000 per category, or per item type?** Sources conflict | Per-category is the reading consistent with years of "1000 is not enough" threads, but it was not confirmed. The journal's `ShipLocker.json` plus a full locker would settle it |
| **Bartender exchange ratios** | `BarterCost`/`BarterValue` above; and a `TradeMicroResources` event in a real journal is ground truth |
| **Whether the grade upgrade charges credits** as well as materials | Inara's blueprint pages list credits for engineer mods and none for grade upgrades, which may be an omission rather than a zero |
| **Anything post-Operations** (30 June 2026) | Every mechanical source reached predates it. Frontier's own notes returned 403 |
| **Whether Merc Coin on-foot gear mods are grade-upgradeable at an engineer** | One unverified forum comment suggests so, which would be a genuinely new mechanic. Official Operations notes, read directly |

## 8. Data traps

- **`Manticore Oppressor` is spelled `Opressor`** (one `p`) at grades 3, 4 and 5 in EDEngineer. A
  name join will split that weapon in half.
- **Duplicate mod names.** `Improved Hip Fire Accuracy (…)` and `Higher Accuracy (…)` are three pairs
  with byte-identical ingredient lists — an old and a new naming of the same mods, both still present.
- **Two duplicate entry names** in `entryData.json`: `Biological Sample` and `Virology Data`, so 201
  entries are 199 distinct names.
- **`@Merchant` never appears on a ship blueprint**, and `@Bartender` appears only on four Odyssey
  `Unlock` rows. `tools/gen-engineers.py` currently drops both as `NOT_PEOPLE`, which is why none of
  §2's 56 upgrade recipes reach d47 today.

## 9. Terminology, because the sources disagree with the game

| In-game UI | Community sources |
|---|---|
| Items | **Goods** |
| Components | **Assets** (subdividing into Chemicals, Circuits, Tech) |
| Data | Data |

One guide collapses Goods out entirely and promotes the Asset sub-types to top level, giving four
categories. That reading is inconsistent with the rest of the corpus and with `SuitInventory.Kinds`,
which already uses Elite's own four labels — Items, Components, Consumables, Data.

## 10. How to re-measure

- **EDEngineer** (MIT): `blueprints.json` — filter `Type` to `Suit`/`Weapon`/`Unlock`, and note that
  `@`-prefixed entries in `Engineers` are vendors rather than people. `entryData.json` — read with
  `utf-8-sig`.
- **FDevIDs**: `microresources.csv` and `engineers.csv`. The `4000xx` id block is the reliable
  discriminator for Odyssey engineers — 13 of the 38.
- **Web**: Inara's engineer and equipment-blueprint pages fetch cleanly and are the richest source
  found. `elite-dangerous.fandom.com` returns **402** and `forums.frontier.co.uk` returns **403** to
  automated fetch, so anything from those two arrived as a search excerpt and is marked as such
  above. Reading them in a browser would upgrade several rows in §7 from lead to fact.
