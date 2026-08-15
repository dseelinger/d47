# Can d47 derive the material trader's lines?

**Yes, and it is stronger than a parse: the derived lines price 1,096 of 1,096 real trades
correctly.** Measured 2026-08-15 by `spike/MaterialLineProbe/probe.py`, against EDDiscovery at
commit `459d01d` and the same 912-journal corpus as
[journal-corpus-engineering.md](journal-corpus-engineering.md).

This is Step 0 of [the Phase 14 `#102` plan](../plans/phase-14-102-engineering.md), and it was Step 0
because it is that plan's single point of failure. A *line* is one column of the material trader's
grid — one material per grade. Trading up and down within a line is the cheap move; crossing to
another line costs a further 6×. **The journal cannot supply it**: its `Category` field names the
Raw / Manufactured / Encoded type, which no trade ever crosses, so Iron-for-Vanadium reads as "same
category" and prices as free when it is in fact a 6× cross-line trade. Background:
[journal-corpus-engineering.md](journal-corpus-engineering.md) §3.

Every priced answer later in that plan rests on this page.

## What the probe checked, and what it found

| Check | Result |
|---|---|
| Lines declared by `MaterialGroupType`, and populated | **32 declared, 32 carry materials** |
| Every material in `MaterialGrades.g.cs` lands in exactly one line | **137 of 137, none dropped** |
| A material appearing in more than one line | **0** |
| Two materials at the same grade inside a **trader grid column** | **0** |
| Grades from `ItemType` against the grades already shipped | **137 compared, 0 disagreements** |
| Real trades priced using the derived line | **1,096 of 1,096 correct** |

The grade check is the one that mattered, because it is the only one where two independently derived
tables could have contradicted each other. `MaterialGrades.g.cs` comes from EDCD/FDevIDs
`material.csv` and its `rarity` column; the grades here come from EDDiscovery's `ItemType`
(`VeryCommon`=1 … `VeryRare`=5). They agree on every one of the 137 materials d47 ships.

## The 32 lines are not 32 columns — 23 are, and 9 are something else

The count came from one pass in an earlier session and is repeated in
[journal-corpus-engineering.md](journal-corpus-engineering.md) §3 as "there are 32 of them, sitting
inside the three types". **The 32 is right and the reading is not.** The enum declares 32 non-`NA`
members, but only the first 23 are columns of the trader's grid:

- **7 Raw** — `RawCategory1`..`RawCategory7`
- **6 Encoded** — `EncodedEmissionData`..`EncodedFirmware`
- **10 Manufactured** — `ManufacturedChemical`..`ManufacturedAlloys`

The remaining **9 are display groupings for Guardian and Thargoid materials**, which the trader does
not deal in: `EncodedGuardian`, `EncodedGuardianObelisk`, `ManufacturedGuardian`, `EncodedThargoid1`,
`EncodedThargoid2`, `ManufacturedThargoid1`..`ManufacturedThargoid4`.

That is not inferred from the shape of the data. **EDDiscovery's own trader panel says so**, walking
the enum and admitting exactly those three contiguous ranges — `UserControlMaterialTrader.cs` in the
`EDDiscovery` repository, the `sel == 0/1/2` tests around line 197. The corpus agrees independently:
**none of the 1,096 trades touches a Guardian or Thargoid material.**

This matters because the "no line holds two materials at the same grade" condition **fails 11 times**
— and every one of the 11 is inside those 9 groupings, where it is not a contradiction but a
description. `EncodedGuardianObelisk` holds five materials all at grade 4; the five Pattern Alpha
through Epsilon obelisk datas are peers, not a ladder. Read as a grid column that would be a defect.
Read as what it is — a bucket for a panel to draw — it is correct, and it is simply not a line.

**So the rule for Step 1 is: a Guardian or Thargoid material has a group, not a line, and the trade
arithmetic must decline rather than guess.** It cannot be traded, so there is no rate to get wrong,
and the only failure available is answering as though there were one.

## Raw lines are four deep, not five

All seven Raw lines stop at grade 4, and no Raw material anywhere in `MaterialGrades.g.cs` has a
grade of 5. Reported rather than padded. The Encoded and Manufactured columns are all five deep.

## The strongest evidence: the lines predict what traders charged

Parsing cleanly only proves the parse. The line table was therefore used to price every
`MaterialTrade` in the corpus from the published rule — 3× per grade down, ⅙ per grade up, a further
⅙ for a different line — and compared against what the game actually gave:

```text
Priced 1096 real trades using the derived line
  predicted rate correct: 1096/1096
  same line: 536, different line: 560
  trades crossing Raw/Encoded/Manufactured: 0
  trades touching a Guardian/Thargoid group: 0
```

**560 of 1,096 trades are cross-line** — the majority. That is the size of the mistake the journal's
`Category` field would have caused: not an edge case, the commonest trade there is.

## One real risk, already visible in the source

`MCMRType.cs` has five `Add(...)` overloads, and **two of them do not take a `MaterialGroupType` at
all** — they default it to `NA`. A material filed through one of those has no line, and would price
as a free same-line trade with nothing anywhere reporting a problem.

There is **one such material already**: `Encoded.SearchRescueVoucher`, the Search and Rescue Voucher.
It is harmless today because it is not in `MaterialGrades.g.cs`, so d47 never sees it. The next one
might not be.

**So `gen-materials.py` must fail the run on any material with an empty `line`, rather than emitting
a blank cell.** The probe already reports groupless rows for exactly this reason.

## Ten materials EDDiscovery knows and d47's table does not

Lined in `MCMRType.cs`, absent from `MaterialGrades.g.cs` — all Thargoid, all in groups the trader
does not deal in, so none affects trade arithmetic:

```text
tg_abrasion01  tg_abrasion02  tg_abrasion03  tg_causticcrystal  tg_causticgeneratorparts
tg_causticshard  tg_interdictiondata  tg_shutdowndata  tg_structuraldata02  unknowncorechip
```

They are the expected shape of the lag §"A habit worth keeping" in [README.md](README.md) warns
about: `material.csv` has not caught up with materials added since. Worth knowing, not worth acting
on here.

## The full ladders

Run the probe to print them; this is the trader's grid as derived, for eyeballing against the game.

```text
RawCategory1   Carbon → Vanadium → Niobium → Yttrium
RawCategory2   Phosphorus → Chromium → Molybdenum → Technetium
RawCategory3   Sulphur → Manganese → Cadmium → Ruthenium
RawCategory4   Iron → Zinc → Tin → Selenium
RawCategory5   Nickel → Germanium → Tungsten → Tellurium
RawCategory6   Rhenium → Arsenic → Mercury → Polonium
RawCategory7   Lead → Zirconium → Boron → Antimony

EncodedEmissionData     Exceptional Scrambled Emission Data → Irregular Emission Data →
                        Unexpected Emission Data → Decoded Emission Data →
                        Abnormal Compact Emissions Data
EncodedWakeScans        Atypical Disrupted Wake Echoes → Anomalous FSD Telemetry →
                        Strange Wake Solutions → Eccentric Hyperspace Trajectories →
                        Datamined Wake Exceptions
EncodedShieldData       Distorted Shield Cycle Recordings → Inconsistent Shield Soak Analysis →
                        Untypical Shield Scans → Aberrant Shield Pattern Analysis →
                        Peculiar Shield Frequency Data
EncodedEncryptionFiles  Unusual Encrypted Files → Tagged Encryption Codes → Open Symmetric Keys →
                        Atypical Encryption Archives → Adaptive Encryptors Capture
EncodedDataArchives     Anomalous Bulk Scan Data → Unidentified Scan Archives →
                        Classified Scan Databanks → Divergent Scan Data → Classified Scan Fragment
EncodedFirmware         Specialised Legacy Firmware → Modified Consumer Firmware →
                        Cracked Industrial Firmware → Security Firmware Patch →
                        Modified Embedded Firmware

ManufacturedChemical    Chemical Storage Units → Chemical Processors → Chemical Distillery →
                        Chemical Manipulators → Pharmaceutical Isolators
ManufacturedThermic     Tempered Alloys → Heat Resistant Ceramics → Precipitated Alloys →
                        Thermic Alloys → Military Grade Alloys
ManufacturedHeat        Heat Conduction Wiring → Heat Dispersion Plate → Heat Exchangers →
                        Heat Vanes → Proto Heat Radiators
ManufacturedConductive  Basic Conductors → Conductive Components → Conductive Ceramics →
                        Conductive Polymers → Biotech Conductors
ManufacturedMechanicalComponents
                        Mechanical Scrap → Mechanical Equipment → Mechanical Components →
                        Configurable Components → Improvised Components
ManufacturedCapacitors  Grid Resistors → Hybrid Capacitors → Electrochemical Arrays →
                        Polymer Capacitors → Military Supercapacitors
ManufacturedShielding   Worn Shield Emitters → Shield Emitters → Shielding Sensors →
                        Compound Shielding → Imperial Shielding
ManufacturedComposite   Compact Composites → Filament Composites → High Density Composites →
                        Proprietary Composites → Core Dynamics Composites
ManufacturedCrystals    Crystal Shards → Flawed Focus Crystals → Focus Crystals →
                        Refined Focus Crystals → Exquisite Focus Crystals
ManufacturedAlloys      Salvaged Alloys → Galvanising Alloys → Phase Alloys →
                        Proto Light Alloys → Proto Radiolic Alloys
```

The Encoded display names diverge from their symbols throughout, which the plan already knows about
as the 22 drifting Encoded symbols. Worth adding is that **three Manufactured ones diverge too**,
where the rest match almost exactly: `uncutfocuscrystals` is shown as **Flawed** Focus Crystals,
`fedcorecomposites` as **Core Dynamics** Composites, and `fedproprietarycomposites` as plain
**Proprietary** Composites. Step 1 joins EDEngineer on display name, so these are three more places
that join can quietly lose a row.

## Correction, found while building Step 1: there are two sources, not one

**FDevIDs `material.csv` has carried the line all along**, in a `category` column sitting
immediately beside the `rarity` column d47 already reads to build `MaterialGrades.g.cs`. It holds
`1`..`7` for Raw and a named group for the other two, and `None` for the rest.

It agrees with EDDiscovery **completely**: the 23 distinct FDevIDs groups map one-to-one onto the 23
trader `MaterialGroupType` values, no group splits across two lines and no line merges two groups,
and FDevIDs' 29 `None` rows are exactly the Guardian and Thargoid materials. Two independently
maintained sources, neither derived from the other, agreeing on all 137 rows.

The column was added on **13 January 2021**, fifty-one minutes after the file itself. It is not new,
and it was not missed because it was stale.

So this page's opening framing — and the plan's, and
[journal-corpus-engineering.md](journal-corpus-engineering.md) §3's "the only permissive source
found that carries the grouping at all" — is **wrong**, and wrong in the exact way §"The method" in
[README.md](README.md) is written to prevent. *A failed fetch is a fact about the fetcher.* This is
the third costume that mistake has worn here: first a user-agent check, then one machine's play
history, and now **a column nobody read in a file already open**. The habit that catches all three is
the same one: say *where* you looked before saying a thing is not there.

What it changes:

- **The plan's single point of failure is not single.** Step 0's conclusion survives and gets
  stronger — it is now two sources in agreement rather than one regex over C#.
- **`line` comes from FDevIDs**, in the same fetch as the symbol, name, grade and type, from a CSV
  with a schema rather than C# that can be restructured. EDDiscovery is the **cross-check**, pinned
  at a commit, and `gen-materials.py` fails the run if the two ever disagree — which is the standard
  this repo already holds its joins to.

## What this settles for the plan

- **Step 0 passes.** The `line` column is derivable, and the priced half of the plan proceeds
  unchanged.
- **Step 1** gets its `line` values from FDevIDs `material.csv`, cross-checked against
  `MaterialGroupType` at a pinned commit, and must fail loudly rather than emit a blank one.
- **Step 2's `TradeRate(from, to, sameLine)`** is confirmed against 1,096 trades, including that a
  same-grade trade is always cross-line.
- **Step 9's shortfall netting** must decline for Guardian and Thargoid materials by name rather
  than pricing them, because they have a group and not a line.

## How to re-measure

```bash
python spike/MaterialLineProbe/probe.py
```

That runs everything except the rate check, which needs a `MaterialTrade` extract. The corpus is a
Commander's own play history and stays out of this repository — pull an extract per
[journal-corpus-engineering.md](journal-corpus-engineering.md) §7, keep it in a scratch directory,
and pass its path as the one argument.

The probe pins EDDiscovery at commit `459d01dc2bccf688019c2f8e45c3909277a4e316` and asserts the
things it cannot verify by reading: that `ItemType` still begins `VeryCommon, Common, Standard, Rare,
VeryRare`, that `MaterialGroupType[0]` is still `NA`, and that the three trader ranges still exist.
Each of those, if it changed silently, would shift every grade or every line while still parsing.

The game data underneath is Frontier's, used under their media usage rules; EDDiscovery's Apache-2.0
grant covers their code, not the game facts. See `NOTICE`.
