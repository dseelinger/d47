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

## 4. The unlock chain is partly sourceable — and the ship half caught up

Phase 14 refused to ship a referral graph for ship engineers because none was found. On foot, six of
them are **in the data**: `Type: Unlock` rows whose `Engineers` array names the *referring* engineer.

**That asymmetry closed on 2026-08-15.** EDDiscovery's `Items/Engineers.cs` (Apache-2.0) carries the
graph for all 38, ship and on-foot alike, and it agrees with these six exactly — which is what makes
it trustworthy on the other 32. See the ledger in [README.md](README.md) and the one conflict it
has with the wiki, decided by journal, in
[journal-corpus-engineering.md](journal-corpus-engineering.md) §4.

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
- **On-foot engineers have no reputation system at all** — confirmed at source, where the first two passes had this as an inference: *"Engineers that focus on pilot equipment do not use a reputation unlock system, and offer all of their modifications immediately upon fulfilling their meeting requirements."* So unlike ships, where rank gates which grades are reachable and costs up to 16 million credits of profit to raise, meeting an on-foot engineer is the whole of it.
- **On-foot engineer bases have no Pioneer Supplies.** *"Odyssey … engineers' bases Concourse does
  not have most of standard services (bar, terminals, Pioneer Supplies). They only have Apex taxi
  desk."* That turns "grade before mods" from an ordering preference into a **routing fact**: the
  upgrade cannot be done at the engineer, so it has to happen before the trip.

## 6c. Suit mod effects and credit costs — found, and weaker than they look

[Inara logbook 72975](https://inara.cz/elite/logbook/72975/) carries all **14 suit mods** with a
credit price, a provider list split Bubble/Colonia, an itemised material cost, and a described
effect. Credit prices fall in three tiers:

| Price | Mods |
|---|---|
| 500,000 | Added Melee Damage, Reduced Tool Battery Consumption |
| 750,000 | Combat Movement Speed, Damage Resistance, Enhanced Tracking, Extra Ammo Capacity, Extra Backpack Capacity, Faster Shield Regen, Improved Battery Capacity, Improved Jump Assist, Increased Air Reserves, Increased Sprint Duration |
| 1,000,000 | **Night Vision**, **Quieter Footsteps** |

The two at a million are the two with a single Bubble source each (§6a) — the scarcity and the price
agree, which is a small corroboration that the page is describing the real game.

Effects, where the page states a number: Added Melee Damage **+50%** (fists and strikes), Damage
Resistance **+10%** each to explosive, plasma, thermic and kinetic, Enhanced Tracking **doubles**
scan range and makes scanning instant, Extra Ammo Capacity **+50%**, Extra Backpack Capacity
**doubles** goods, data and assets, Faster Shield Regen **+25%**, Improved Battery Capacity
**+50%**, Increased Air Reserves **1 minute → 5**, Increased Sprint Duration **about ×2**, Reduced
Tool Battery Consumption **−50%**.

**Three reasons not to ship these as facts yet**, all of which the page itself supplies:

1. **It is dated 24 October 2022**, so it predates the Type-8 rebalance — the same update that
   demonstrably rewrote on-foot recipes (§4, "Removed Power Regulators"). The material costs on that
   page are therefore suspect in exactly the way EDEngineer's unlock costs turned out to be.
2. **The author hedges on several entries in their own words** — "I could not find online by how
   much" for Improved Jump Assist, and for Faster Shield Regen "it is unclear to me whether this
   applies to the regular shield regen, broken shield regen or both".
3. **An earlier search excerpt gave Faster Shield Regen as +33%; the page says 25%.** One of those
   is wrong and it does not matter which — it is the third time in this investigation that a search
   excerpt disagreed with the page it was excerpted from.

So: a real source with real numbers, and a compilation by one Commander rather than a specification.
Worth carrying with attribution and a date, not worth asserting flatly.

**Weapon mod effects are still unsourced.** That guide covers suits only, by the author's own
statement, and no equivalent weapon guide was found in this pass.

Two things on that page that must **never** reach a shipped table: the per-mod "Recommendation"
paragraphs, which are opinion and are exactly the sort of thing d47 does not assert, and the suit
comparisons, which are the same.

## 7. What has not been found yet

Every row here is a lead, not a verdict. The information is out there; this is a record of where the
search stopped, so the next person starts further along.

| Gap | Where to look next |
|---|---|
| ~~**No suit or weapon ids in any checked source.**~~ **Found 2026-08-15** | **EDDiscovery/EliteDangerousCore** (Apache-2.0) has both: `Items/Suits.cs` keyed on the `SuitFDName` the journal writes, and `Items/HandItems.cs` for hand weapons. Independently confirmed by 768 `SuitLoadout` events, which also settle that **`SuitName` encodes the grade** (`explorationsuit_class1`). FDevIDs still has neither, so the journal remains the id authority and EDDiscovery is the name and stat join |
| **Base stats for suits and weapons** — partially found, and weaker than it looks | EDDiscovery carries per-grade `SuitStats` and `WeaponStats`, but with **per-figure provenance that varies**: `rob checked 20/8/21 for all suits to class 3 in game, class 4/5 according to wiki`, and one weapon row annotated `TBD Guess at same muliplier of 1.25`. It also has a transcription bug — `SuitStats` assigns the health multipliers to the shield multiplier fields. Usable as a lead, not as a table to copy. Inara's per-item ladders remain the unharvested cross-check |
| **Weapon** mod effect values, and a *current* source for the suit ones | The suit 14 are found (§6c) but from a 2022 compilation that predates a recipe-changing patch. No weapon-mod guide turned up in this pass. Per-weapon Inara pages, and the in-game engineer screen, are both untried |
| **Ship-locker cap: 1000 per category, or per item type?** Sources conflict | Still open, and the 912-journal corpus does not answer it — `ShipLocker.json` is a state file rather than a journal event, so it was not in the extract. EDDiscovery's `SuitStats` carries `ItemCap`, `ComponentCap` and `DataCap` per suit, but those are the **backpack**, not the locker, and conflating the two would produce a confident wrong number. A full locker plus its `ShipLocker.json` still settles it |
| **Anything post-Operations** (30 June 2026) beyond the reward list | The Operations page itself is now read (§6c). What remains is whether Operations changed any *mechanic* rather than adding a currency — the update notes thread, in a browser |
| **Whether Merc Coin on-foot gear mods are grade-upgradeable at an engineer** | One unverified forum comment suggests so, which would be a genuinely new mechanic. Official Operations notes, read directly |


**Four rows were struck from this table on 2026-08-15** by opening pages in a browser rather than fetching them: the full mod-to-engineer map (§6a), the four Colonia engineers (§6a), whether grade upgrades cost credits (§6b) and the barter rate (§6b). They are left described in those sections rather than deleted, because the reason they were ever open is the useful part.
## 7a. EDOMH — MIT source, and the data is deliberately not in it

[`jixxed/ed-odyssey-materials-helper`](https://github.com/jixxed/ed-odyssey-materials-helper) does
precisely what Phase 19 describes: reads the journal, tracks micro-resources, and says how much is
needed to upgrade a suit or weapon, unlock an engineer or craft a blueprint. 387 stars, Java, last
pushed four days before this was written. It looked like the goldmine it was suggested to be.

**The licensing is cleanly separated and worth stating exactly**, because a careless reading goes
either way:

- `LICENSE` is **MIT**, and `NOTICE` says *"This repository contains source code licensed under the
  MIT License."* Reading and deriving from the repository is fine.
- `EULA.MD` covers the **compiled binaries**, which *"include third-party proprietary components and
  assets"* that are explicitly **not** MIT, and forbids decompiling them or extracting *"embedded
  credentials, cryptographic keys, configuration data, or other confidential information"*.

So the boundary is source-yes, binaries-no — which is the right answer for d47's licence invariant
either way.

**But the game data is not in the source.** The tree is 1,451 entries; the `enums` package holds
thirteen files and they are all UI sort and filter options, and the non-localisation resources are
icons, audio and stylesheets. The data arrives as a dependency:

```
implementation "nl.jixxed.ed.data:ed-data-api:1.7"
implementation "nl.jixxed.ed.data:ed-data-impl:1.36"
implementation 'nl.jixxed.ed.confidential:ed-confidential-api:1.3'
```

…from the author's own Maven repository at `repo.repsy.io/mvn/jixxed/maven/`, not from the open
tree. The `-impl` artifacts are closed, and a sibling package is called *confidential* in as many
words.

**Conclusion: EDOMH is not a source d47 can derive a table from.** The one thing worth taking from
it is architectural rather than factual — an actively maintained tool solving this exact problem
keeps its game data in a separately versioned artifact (`ed-data-impl` is at 1.36 while the API is
at 1.7), which is a strong hint about how often on-foot data actually moves, and an argument for
d47's generated tables carrying a visible version and date.

## 7b. EDSY — unusable licence, and no on-foot data anyway

[`taleden/edsy`](https://github.com/taleden/edsy), JavaScript, actively maintained. Its `eddb.js`
is a 555 KB database keyed by `fdid` and `fdname`, so structurally it is a fine source. Two
independent reasons it is not one for d47:

**The licence fails the invariant.** The file header states the design, markup and script are
*"provided under a Creative Commons Attribution-NonCommercial 4.0 International License"*.
Non-commercial is a use restriction, and CLAUDE.md's rule is permissive only. That is a harder stop
than coriolis-data's position, which at least confines its claim to Frontier's ownership of the game
data rather than adding terms of its own.

**And it has nothing on foot.** Searched for `suit`, `maverick`, `dominator`, `artemis`, `karma`,
`manticore` and `takada`: **zero hits for every one**. Like coriolis-data, it models ships and ship
modules only — so even with a usable licence it would not have answered the question it was checked
for. Which also disposes of the hope that EDOMH's EDSY wishlist import implied on-foot data flowing
between them; the import must be ship builds.

### The lead worth chasing, which is bigger than EDSY

The same header says the game data *"remains the property of Frontier Developments plc, and is used
here as authorized by Frontier Customer Services"*, linking to Frontier's **Elite Dangerous media
usage rules** (`forums.frontier.co.uk/threads/elite-dangerous-media-usage-rules.510879/`).

**That is the ground every derived table in this repo already stands on.** `MaterialGrades.g.cs`,
`EliteSpecifications.tsv` and `Engineers.tsv` are all derived from community files whose own licences
say, in coriolis-data's words, that the JSON is Frontier's property. The generators' docstrings
reason about the *community* repositories' terms and are silent on Frontier's, which is the one that
actually governs.

**Settled 2026-08-15: the maintainer accepts Frontier's terms**, and the engineering work proceeds
on that basis. See [README.md](README.md) for the decision and what it does not change — the
non-commercial condition, and tables staying derived rather than copied.

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
- **Frontier's own suit localisation is broken, and it lies about the grade.** An `UpgradeSuit` on
  `utilitysuit_class3` returned `"Name_Localised": "$UtilitySuit_Class1_Name;"` — an unresolved token
  naming **Class1** for a class 4 suit. Anything that speaks the localised string says the wrong
  grade or reads a raw symbol aloud. `Name` and the separate `Class` field are the truth. Measured in
  [journal-corpus-engineering.md](journal-corpus-engineering.md) §6.
- **`Name` and `Class` disagree by design on an upgrade.** The same event carries the suit's *old*
  symbol (`utilitysuit_class3`) alongside the *new* `Class` (4). Reading the grade out of the symbol
  on that one event is off by one.

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
