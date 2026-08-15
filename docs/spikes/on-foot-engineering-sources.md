# What the on-foot engineering sources actually contain

**Measured 2026-08-14.** Parsed from the live files and researched against community sources. This
is the ground under Phase 19, and the reason that phase exists separately from the ship engineering
items rather than as a footnote to them.

**Read the gaps as open, not closed.** Where this document says something was not found, it means
exactly that — not that it does not exist. On-foot is less documented than ships, not undocumented,
and every "not found" below is a lead rather than a verdict.

> **Second pass, 2026-08-15.** Half of §7's original gaps were closed within a day by opening the
> pages in a browser instead of fetching them. `elite-dangerous.fandom.com` answers **402** and
> `forums.frontier.co.uk` answers **403** to an automated fetch and render perfectly in a browser —
> so "no source has this" meant "a user agent check stopped me". Sections marked **Confirmed at
> source** below were read that way. What it also turned up: **EDEngineer's Odyssey unlock
> quantities are stale**, by as much as 8×. See §4.

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
> — [Fandom *Engineers*](https://elite-dangerous.fandom.com/wiki/Engineers), read at source in a
> browser on 2026-08-15. The same page adds two things worth having: *"Suits and handheld weapons
> need to be grade 2 or higher before you can modify them"*, and that upgrading and modding both
> cost **credits** as well as materials.

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
The structure is three chains of three. The four Colonia engineers have no rows *in this file* —
which is not the same as having no data, see §6a.

Community sources give the non-material half of each unlock — travel 100 ly by Apex for Domino
Green, ten on-foot Conflict Zones for Hero Ferrari, Unfriendly with Sirius Corporation for Uma
Laszlo — from [E:D Black Box](https://edblackbox.com/guides/engineering/engineering-manuals/checklist.html)
and [Inara](https://inara.cz/elite/engineers/).

### The quantities above are stale — confirmed at source

Cross-checking against [Frontier's Type-8 notes](https://www.elitedangerous.com/update-notes/4-0-18-08)
found that the same update which rebalanced ship rolls also **cut four on-foot unlock requirements**,
and EDEngineer still carries the pre-patch numbers:

| Engineer | EDEngineer says | Frontier says |
|---|---|---|
| Kit Fowler | Opinion Polls ×40 | **×5** (down from 10) |
| Hero Ferrari | Settlement Defence Plans ×15 | **×5** |
| Yarden Bond | Smear Campaign Plans ×8 | **×5** |
| Wellington Beck | 25 each of three entertainment kinds | **15 total** across the three |

Kit Fowler is off by a factor of eight. The same notes also record **"Removed Power Regulators from
recipes"**, which settles a conflict logged in §9 as unresolved: newp.io listed them, Inara did not,
and Inara was right because they were taken out.

**The lesson is about the generator, not these four rows.** A patch that changes unlock costs will
not change EDEngineer's file until somebody updates it, so the unlock column is the part of the
generated table most likely to be quietly wrong, and it is also the part whose failure wastes a
Commander's trip. It wants a source that tracks patches, or a loud staleness note in the answer.

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

## 6a. The complete mod-to-engineer map — confirmed at source

Read from the [Fandom Engineers page](https://elite-dangerous.fandom.com/wiki/Engineers), which
carries all thirteen. This was §7's "only two engineers verified in full", and it is now closed.

| Engineer | Base, system | Offers |
|---|---|---|
| Domino Green | The Jackrabbit, Orishis | Enhanced Tracking, Extra Backpack Capacity, Reduced Tool Battery Consumption, Greater Range, Stability |
| Hero Ferrari | Nevermore Terrace, Siris | Improved Jump Assist, Increased Air Reserves, Faster Handling, Noise Suppressor |
| Jude Navarro | Marshall's Drift, Aurai | Added Melee Damage, Damage Resistance, Extra Ammo Capacity, Magazine Size, Reload Speed |
| Kit Fowler | The Last Call, Capoya | Added Melee Damage, Extra Ammo Capacity, Faster Shield Regen, Magazine Size, Stowed Reloading |
| Oden Geiger | Ankh's Promise, Candiaei | Enhanced Tracking, Improved Battery Capacity, **Night Vision**, Scope, Stability |
| Terra Velasquez | Rascal's Choice, Shou Xing | Combat Movement Speed, Increased Air Reserves, Increased Sprint Duration, Improved Hip Fire Accuracy, Noise Suppressor |
| Uma Laszlo | Laszlo's Resolve, Xuane | Damage Resistance, Faster Shield Regen, Headshot Damage, Reload Speed, Stowed Reloading |
| Wellington Beck | Beck Facility, Jolapa | Extra Backpack Capacity, Improved Battery Capacity, Reduced Tool Battery Consumption, Greater Range, Scope |
| Yarden Bond | Salamander Bank, Bayan | Combat Movement Speed, Improved Jump Assist, **Quieter Footsteps**, Audio Masking, Faster Handling, Improved Hip Fire Accuracy |
| Baltanos | The Divine Apparatus, Deriso | Combat Movement Speed, Improved Jump Assist, Increased Air Reserves, Increased Sprint Duration, Faster Handling, Improved Hip Fire Accuracy, Noise Suppressor |
| Eleanor Bresa | Bresa Modifications, Desy | Added Melee Damage, Damage Resistance, Extra Ammo Capacity, Faster Shield Regen, Magazine Size, Reload Speed, Stowed Reloading |
| Rosa Dayette | Rosa's Shop, Kojeara | Enhanced Tracking, Extra Backpack Capacity, Improved Battery Capacity, Reduced Tool Battery Consumption, Greater Range, Scope, Stability |
| Yi Shen | Eidolon Hold, Einheriar | **Night Vision**, **Quieter Footsteps**, Audio Masking, Headshot Damage |

**The four Colonia engineers are not data-less after all.** §7 originally recorded "nothing at all"
for Baltanos, Eleanor Bresa, Rosa Dayette and Yi Shen. That was true of *EDEngineer*, and false of
the game — they carry the widest mod lists of the thirteen, and a Colonia Commander reaches nearly
everything through three of them where a Bubble Commander needs nine.

Two mods have exactly one Bubble source each and are therefore routing-critical: **Night Vision**
(Oden Geiger) and **Quieter Footsteps** (Yarden Bond). Both are also at Yi Shen in Colonia.

The 25 distinct mods here reconcile with EDEngineer's 34 rows: the extras are the per-manufacturer
splits of Headshot Damage and Improved Hip Fire Accuracy, plus the old/new naming duplicates
recorded in §8.

## 6b. Three more, confirmed at source

- **Grade upgrades cost credits as well as materials.** *"Both upgrading and adding mods requires
  new types of 'materials' … as well as credits."* Closes a §7 row and one of the four spike
  questions.
- **The barter rate is computable after all.** The Bartender page describes exactly the mechanic
  EDEngineer's two fields encode: *"trading Assets for 'Barter Value', and then spending the
  accumulated Barter Value on the desired Component. **Each Component traded in is worth a fixed
  amount of Barter Value**"*. So `BarterValue` is what one gives and `BarterCost` is what one costs,
  both fixed — which makes the conversion arithmetic rather than a guess. **This is a better
  position than the ship material trader is in**, where no rate was found at all. Assets only;
  Goods and Data are sold for credits and cannot be exchanged, and illegal items are refused outside
  Anarchy-controlled systems.
- **On-foot engineer bases have no Pioneer Supplies.** *"Odyssey … engineers' bases Concourse does
  not have most of standard services (bar, terminals, Pioneer Supplies). They only have Apex taxi
  desk."* That turns "grade before mods" from an ordering preference into a **routing fact**: the
  upgrade cannot be done at the engineer, so it has to happen before the trip.

## 7. What has not been found yet

Every row here is a lead, not a verdict. The information is out there; this is a record of where the
search stopped, so the next person starts further along.

| Gap | Where to look next |
|---|---|
| **No suit or weapon ids in any checked source.** FDevIDs has no `suits.csv` or on-foot `weapons.csv`; `outfitting.csv` is ship modules only | The **journal itself** — `SuitLoadout` carries `SuitID`, `SuitName`, `LoadoutID` and per-module `SuitModuleID`. That makes the journal the id authority and inverts the ship arrangement. Also worth checking the Frontier CAPI docs in FDevIDs' `Frontier API/` folder, which mention `onfootmicroresources` and `pioneersupplies` |
| **Base stats for suits and weapons** — damage, magazine, shield, armour | Inara publishes per-item equipment-blueprint pages with fixed stat ladders (e.g. Maverick shield +22.5% at G2 rising to +125% at G5). Not yet harvested |
| **26 of 34 mods carry no effect values** in EDEngineer | Inara's mod pages give flat figures (Improved Battery Capacity +50%, Faster Shield Regen +33%, Greater Range +50%, Headshot Damage ×1.5). A second source to derive from rather than hand-write |
| **Ship-locker cap: 1000 per category, or per item type?** Sources conflict | Per-category is the reading consistent with years of "1000 is not enough" threads, but it was not confirmed. The journal's `ShipLocker.json` plus a full locker would settle it |
| **Anything post-Operations** (30 June 2026) beyond the reward list | The Operations page itself is now read (§6c). What remains is whether Operations changed any *mechanic* rather than adding a currency — the update notes thread, in a browser |
| **Whether Merc Coin on-foot gear mods are grade-upgradeable at an engineer** | One unverified forum comment suggests so, which would be a genuinely new mechanic. Official Operations notes, read directly |


**Four rows were struck from this table on 2026-08-15** by opening pages in a browser rather than fetching them: the full mod-to-engineer map (§6a), the four Colonia engineers (§6a), whether grade upgrades cost credits (§6b) and the barter rate (§6b). They are left described in those sections rather than deleted, because the reason they were ever open is the useful part.
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
