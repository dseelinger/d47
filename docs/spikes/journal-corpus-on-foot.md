# What the journal says about a suit

**Measured 2026-08-16** against the same corpus as
[journal-corpus-engineering.md](journal-corpus-engineering.md) — **912 journals, 373 MB, 3 July 2025
to 11 August 2026**, nine Commanders, read over SSH from a second machine. Everything below is
counted from those files.

That page said the on-foot half of this corpus was thin. It is thin in *events* and it is not thin in
*answers*: the two questions list.md Phase 20's spike left open are both settled below, and on the way
the corpus overturned the one thing that phase was most confident about.

| Event | Count |
|---|---|
| `ShipLocker` | 46,766 — **28,148 carrying contents** |
| `SuitLoadout` | 768 |
| `BackpackChange` | 2,835 |
| `CollectItems` | 1,963 |
| `TradeMicroResources` / `BuyMicroResources` / `SellMicroResources` | 49 / 49 / 39 |
| `UpgradeWeapon` / `BuyWeapon` / `BuySuit` / `SellWeapon` / `SellSuit` / `UpgradeSuit` | 12 / 11 / 10 / 6 / 4 / 4 |
| `SwitchSuitLoadout` / `CreateSuitLoadout` / `LoadoutEquipModule` | 43 / 12 / 7 |

---

## 1. The ship-locker cap is per category, and it is 1,000

The sources conflicted and the previous pass recorded the corpus as unable to help, because
`ShipLocker.json` is a state file rather than a journal event. That was wrong about the journal:
Elite writes a **`ShipLocker` event carrying the whole locker**, 28,148 times in this corpus.

Summing each category in every one of those snapshots:

| Category | Highest total seen | Times at exactly 1,000 | Ever above 1,000 | Highest single item |
|---|---|---|---|---|
| **Components** | **1,000** | **7,931** | **0** | 94 |
| Items | 570 | 0 | 0 | 63 |
| Data | 493 | 0 | 0 | 43 |
| Consumables | 600 | 0 | 0 | **100** |

> **Per category. 1,000. Not per item type.**

Components sit at exactly 1,000 in 7,931 of 28,148 snapshots and never once above it, while the
largest single component ever held is 94. A per-item-type cap of 1,000 is refuted outright by that
pair of numbers: it would allow a category total in the tens of thousands, and the total stops dead
at 1,000 instead.

**Stated with its limit.** Only Components was ever filled. Items, Data and Consumables never came
near 1,000, so for those three the cap is inferred from the mechanic rather than observed. What *is*
observed for Consumables is a **separate per-item cap of 100** — six kinds, every one topping out at
exactly 100, and a category total topping out at 600.

## 2. The barter rate composes exactly, and one published figure is wrong

`BarterValue` and `BarterCost` sit on exactly the 33 Components. The previous pass called it "the most
promising lead d47 has for a conversion table it never got for ship materials" and left it untested.

**Tested against 49 real trades. The rule is:**

> `received = floor( Σ(offered × BarterValue) ÷ BarterCost(wanted) )`

**47 of 49 exact** with EDEngineer's published figures. **49 of 49** once Graphene's `BarterValue` is
read as **13** rather than the published 12.

Both misses involve Graphene and both are short by one unit, and two trades independently pin the
figure:

```text
Graphene ×4                                   → Chemical Catalyst ×7   ⇒ value ∈ [12.25, 14)
Epoxy ×1 + RDX ×3 + Chemical Catalyst ×1
                        + Graphene ×2         → Viscoelastic Polymer ×4 ⇒ value ∈ [12.5, 18)
```

The intersection contains exactly one integer. Its `BarterCost` of 23 is separately consistent with
both trades that *received* Graphene, so it is the value alone that is off.

**Why the wrong figure looks right**, which is the part worth carrying: cost runs `2×value − 1` for
every component valued 3 to 6 and `2×value` for those valued 2, 8 and 9. A value of 12 makes
Graphene's 23 fit the dominant pattern perfectly. The correct value of 13 makes it the one outlier in
the table. **The plausible number is the wrong one**, which is exactly the shape of error a
cross-check against the game itself exists to catch.

**Leftover points are not carried between trades.** Modelled and refused: crediting the remainder to
the next trade at the same market drops the score from 47 to 30. Each trade is priced on its own.

## 3. EDEngineer's on-foot recipes are stale, by two different factors

**This is the finding that changes what Phase 20 can promise.** list.md says the material cost of an
on-foot build is *"exactly and completely knowable"*, and it is — but not from the numbers that were
about to be shipped. Every on-foot recipe in EDEngineer is pre-patch.

### Grade upgrades: divided by three, and one ingredient removed

Sixteen `UpgradeSuit` / `UpgradeWeapon` events carry `Resources`, which is what the game actually
took. Against EDEngineer's `@Merchant` rows, over **78 ingredient comparisons across 2 suits, 5
weapons and all four grades**:

| EDEngineer size | 1 | 5 | 10 | 15 | 25 | 35 |
|---|---|---|---|---|---|---|
| **What the game charges** | **1** | **2** | **4** | **5** | **9** | **12** |

Every one of the 78 matches, and every size EDEngineer uses appears in that table — nothing is
extrapolated. The pattern is `⌈size ÷ 3⌉`, which is a description of the six measurements rather than
a rule anything relies on.

**And Power Regulators are gone.** EDEngineer lists `Power Regulator` in all four suit upgrade
recipes; it is absent from all four `UpgradeSuit` events. That is Frontier's own published
*"Removed Power Regulators from recipes"*, landing in the data.

### Modifications: divided by two

There is **no journal event at all for applying an on-foot engineer modification** — confirmed
against the complete event vocabulary of all 912 journals. So the mod recipes were measured a second
way, by differencing consecutive `ShipLocker` snapshots and covering the drop with recipes:

| EDEngineer size | 5 | 10 | 15 |
|---|---|---|---|
| **What the game charges** | **3** | **5** | **8** |

Four locker spends, **17 applications across 8 distinct recipes and 41 material lines, remainder
zero**. The largest is a single spend of 16 materials decomposing into four different mods at one
sitting:

```text
2025-11-20  gmeds 5, topographicalsurveys 5, microthrusters 3, motor 5      Improved jump assist
            settlementassaultplans 3, tacticalplans 5, patrolroutes 5,
                                      microhydraulics 3, viscoelasticpolymer 8   Quieter footsteps
            audiologs 3, patrolroutes 5, scrambler 5, transmitter 8,
                                                      circuitboard 3             Audio Masking
            operationalmanual 5, combatantperformance 5,
                       combattrainingmaterial 5, viscoelasticpolymer 3           Faster handling
```

Every quantity in that block is `⌈EDEngineer ÷ 2⌉`, the two shared materials add up exactly, and
nothing is left over. A wrong scale factor does not produce a zero remainder over sixteen materials.

**Two different factors is not a mechanism and is not claimed as one.** What is claimed is the two
lookup tables above, each of whose keys was measured directly, and each of which is complete over the
sizes EDEngineer actually uses.

**Corroborated twice, by two mechanisms.** The two locker spends the mod recipes could *not* cover
turn out to be grade upgrades, and both decompose exactly under the ÷3 table — a Maverick's grade 4
and grade 5 in one sitting, and a TK Aphelion and a Karma AR-50 both to grade 5. So the event's own
`Resources` list and the locker delta agree on both ladders independently.

## 4. The credit cost of an upgrade is the item's price times a fixed multiplier

Not published anywhere the earlier passes found, and exact here:

> **cost to reach grade N = the item's grade 1 purchase price × { G2: 4, G3: 15, G4: 30, G5: 50 }**

Per step, not cumulative, so grade 1 to 5 in full is **99× the base price**.

12 of 12 weapon upgrades match, across five weapons and all four grades — a 50,000 cr Manticore
Tormentor pays 200,000 / 750,000 / 1,500,000 / 2,500,000, and a 175,000 cr Manticore Executioner pays
5,250,000 and 8,750,000 at the same two multipliers. Suits are confirmed at grades 4 and 5 only, on 4
events: a 150,000 cr suit pays 4,500,000 and 7,500,000, which is the same 30 and 50.

Buying a higher-grade item outright is priced differently — a class 2 suit sells for 750,000, where
upgrading into grade 2 would be 600,000 — so the two must not be conflated.

## 5. What a suit is called, and the localisation that lies about it

**`SuitName` encodes the grade**, as the previous pass found: `explorationsuit_class1`,
`utilitysuit_class5`, `tacticalsuit_class3`. The free suit is the exception and has **no class
suffix at all** — plain `flightsuit`, 42 events.

**The broken localisation is not an oddity, it is the common case.** Of 768 `SuitLoadout` events,
**269 carry an unresolved token** — `$UtilitySuit_Class1_Name;`, `$ExplorationSuit_Class1_Name;`,
`$TacticalSuit_Class1_Name;` — and every one of them says **Class1** whatever the suit's real class
is. Only grade 1 suits and the flight suit localise correctly.

So anything that speaks `SuitName_Localised` says the wrong grade more than a third of the time, or
reads a raw symbol aloud. **`SuitName` and the separate `Class` field are the truth**, and the
suit's proper name has to come from a table.

`UpgradeSuit` adds the trap the earlier pass named: it carries the suit's **old** symbol beside its
**new** `Class`, so reading the grade out of the symbol on that one event is off by one.

## 6. The mod symbols do not join the mod names

The journal names a fitted modification with a symbol. The shipped recipe table names the same
modification in words. **They are not the same spelling**, and this is the on-foot repeat of the
`Engine_Dirty` against "Dirty Drive Tuning" problem `ChecklistNaming` already exists for.

Thirteen distinct symbols appear across the corpus. Stripping the `suit_` / `weapon_` prefix and
comparing on letters and digits alone, **five join and eight do not**:

| Journal symbol | Table name | Joins? |
|---|---|---|
| `suit_improvedjumpassist` | Improved jump assist | yes |
| `suit_quieterfootsteps` | Quieter footsteps | yes |
| `suit_nightvision` | Night vision | yes |
| `suit_increasedsprintduration` | Increased sprint duration | yes |
| `weapon_stability` | Stability | yes |
| `suit_increasedbatterycapacity` | Improved battery capacity | **no** |
| `suit_increasedammoreserves` | Extra ammo capacity | **no** |
| `suit_increasedshieldregen` | Faster shield regen | **no** |
| `suit_improvedarmourrating` | Damage resistance | **no** |
| `weapon_clipsize` | Magazine size | **no** |
| `weapon_backpackreloading` | Stowed reloading | **no** |
| `weapon_suppression_unpressurised` | Noise suppressor | **no** |
| `weapon_handling` | Faster handling | **no** |

A relaxed matcher gets 38% of them, which is worse than useless on its own — it would confirm five
mods and quietly fail to recognise eight that are fitted and paid for. Nothing in EDDiscovery's
`Items` tables carries both spellings either.

## 7. Suit and weapon identity, which no id list has

FDevIDs has no suit list and no hand-weapon list. EDDiscovery's `Items/Suits.cs` and
`Items/HandItems.cs` (Apache-2.0) are keyed on exactly the symbols the journal writes, and the corpus
exercises **9 of the 21 suit symbols and 7 of the 11 weapons**, agreeing on every one.

Three slot names, and only three: `PrimaryWeapon1`, `PrimaryWeapon2`, `SecondaryWeapon`.

EDDiscovery's per-grade stats remain a lead rather than a table. The transcription bug the earlier
pass reported is still there, in as many words — `SuitStats` assigns the health multipliers to the
shield multiplier fields:

```csharp
ShieldMultiplierKinetic = hk;
ShieldMultiplierThermal = ht;
```

## 8. How to re-measure

The corpus is not in this repository and must not be — it is a Commander's own play history.

- The `ShipLocker` **event** carries the whole locker. Filter on lines containing both
  `"event":"ShipLocker"` and `"Items":`; 40% of them are the bare pointer form and carry nothing.
- Category totals are the measurement for the cap. Per-item maxima are the control that rules out
  the other reading.
- Recipe scale is measured two ways and both are needed: `Resources` on `UpgradeSuit` /
  `UpgradeWeapon` for the grade ladder, and consecutive `ShipLocker` deltas for the mods, because
  **no event records a mod being applied**.
- Cover a locker delta with whole recipes and require a **zero remainder**. A partial cover is not
  evidence of anything.
- The remote shell is PowerShell and reads stdin a line at a time, so every statement has to be on
  one line. A pipeline split across lines is swallowed with exit code 0.
