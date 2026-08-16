# Exobiology: what can be known before you land

**Measured 2026-08-16** against the corpus used by
[journal-corpus-engineering.md](journal-corpus-engineering.md): **912 journals, 3 July 2025 to 11
August 2026**, nine Commanders — plus the live spansh service and two vetted code sources. Probe:
[`spike/ExobiologyProbe`](../../spike/ExobiologyProbe).

This answers `list.md` Phase 16, *Spike: what can be known about exobiology before you land*. Six
questions; **four came back yes, one came back no, and one came back "this corpus cannot tell you"**
— which is the most important of the six, because it is the one that would have shipped folklore.

---

## 1. There is an exobiology route planner, and its contract is now recorded

`POST https://spansh.co.uk/api/exobiology/route`, then poll `api/results/{job}` — the same
submit-queue-poll protocol the other four plot types use.

**It exists**, established the way the route items establish everything: `api/exobiology/route`
answers **400** to a bad request while `api/exobio/route` and `api/organic/route` answer **404**. A
400 is an endpoint rejecting your parameters; a 404 is an endpoint that is not there.

It then says what it wants outright, which none of the other plotters do:

```json
{"error":"from, range, radius and max_results are required"}
```

**The parameter oracle**, run by sending ten and reading back what came home:

| Sent | Understood? |
|---|---|
| `range`, `radius`, `max_results`, `max_distance`, `loop`, `min_value` | **echoed** |
| `from` | **dropped — and re-emitted as `source`** |
| `use_mapping_value`, `cargo_capacity`, `nonsense_param` | silently dropped |

**`from` not surviving as `from` is the trap here.** It is required, it works, and it comes back under
a different name — so a caller checking its own parameters against the echo would conclude the
origin was ignored and "fix" it into something that really is ignored. `use_mapping_value` being
dropped is the other surprise: the Road to Riches plotter honours it, and this one does not.

**The response is a list of hops rather than an object**, which is a second difference from the
plotters already wired up:

```
result[] → { name, id64, jumps, x, y, z, bodies[] }
  bodies[] → { name, subtype, type, distance_to_arrival,
               estimated_scan_value, estimated_mapping_value,
               landmark_value, landmarks[] }
    landmarks[] → { type (genus), subtype (species), count, value }
```

So a plotted hop already carries, per body, **which species are on it, how many, and what each is
worth**. That is most of *Find the exobiology* arriving for free.

## 2. The first-footfall multiplier is exactly ×4 on top, measured

`SellOrganicData` carries `Bonus` beside `Value` on every row, so this was counted rather than
recalled: **79 rows with a bonus, one distinct ratio, zero variance.**

> **bonus = 4 × value**, so a first footfall pays **5× total**.

Ten further rows carried `Bonus: 0` — sales that were not first footfalls — which is what makes the
79 a measurement rather than an artefact of everything being bonused.

## 3. `Value` is a row total, not a unit price — and this nearly shipped a wrong number

The first draft of this spike treated `SellOrganicData`'s `Value` as the price of one specimen. It is
not. **Radicoida Unica**, sold at the same station as the same variant on five occasions:

| Value | as a multiple of the smallest |
|---|---|
| 119,037 | ×1 |
| 476,148 | ×4 |
| 7,618,368 | ×64 |
| 14,284,440 | ×120 |
| 33,330,360 | ×280 |

**Every value is an exact multiple of 119,037, and the row carries no count field.** So a row is the
total for however many specimens of that species went into the transaction, and the unit price can
only be recovered as the GCD of what was observed — exact when some sale happened to be of one
specimen, an over-estimate otherwise. Across all 31 species sold, **0 break the multiple rule**.

**Two independent confirmations that the reading is right.** The spansh plotter returns a per-species
`value`, and it agrees exactly with the GCD-derived unit on every species that appears in both:

| Species | corpus GCD | spansh | |
|---|---|---|---|
| Stratum Paleas | 1,362,000 | 1,362,000 | match |
| Aleoida Gravis | 12,934,900 | 12,934,900 | match |

And within one three-system route spansh valued 29 species with **no species priced two ways**.

**So use spansh's `value`, never a value derived from a Commander's own sales.** The sales are
aggregated and the aggregation is invisible.

## 4. The mass code question: this corpus cannot answer it, and that is the finding

This was "the one question no source can answer and the strongest reason this spike exists". The
honest result is that **the Commander's own history cannot answer it either** — not yet.

Of 632 `ScanOrganic` events, 374 are in procedurally-named systems where a mass code exists at all:

| Mass code | scans | genera | top genera |
|---|---|---|---|
| `b` | **5** | 2 | Stratum 4, Fonticulua 1 |
| `c` | 154 | 9 | Bacterium 52, Stratum 24, Tussock 20 |
| `d` | 167 | 10 | Bacterium 40, Frutexa 36, Tubus 24 |
| `e` | 48 | 4 | Tussock 16, Bacterium 16, Stratum 12 |

**Three reasons this settles nothing.**

Only three codes carry a usable sample, and `b` — the one a "more mass, more Stratum" claim would
lean hardest on — has **five scans**. Stratum is 80% of `b`, 16% of `c`, absent from `d`'s top three
and 25% of `e`: no trend, on numbers too small to have one.

A per-scan value column can be computed, and should not be trusted, for the reason in §3: 30 of the
31 sold species were sold exactly once, so their "unit" is a total that may cover any number of
specimens. The value axis is unusable at this sample size.

**And the data is not a sample of the galaxy — it is a record of where somebody chose to fly.** If a
Commander goes Stratum-hunting in low-mass systems because they already believe the folklore, the
correlation measures the belief. No amount of this data fixes that; it needs systems picked without
reference to what is expected in them.

**So the heuristic does not ship.** Not because it was refuted, but because confirming it and failing
to test it look identical from here, and *"more mass, more Stratum"* stated by a ship's AI is
indistinguishable to a Commander from a measured fact. The spike asked whether folklore could be
promoted to a heuristic; the answer is not on this evidence.

## 5. No licence-clean genus conditions table exists

EDDiscovery's `EliteDangerousCore` is Apache-2.0 and already vetted here, so it was the first place
to look. It has **`SignalsGenusFDName.cs`, which is a name normaliser** — it turns
`$SAA_SignalType_Biological;` into readable text — and nothing else. Targeted searches of the repo
for the conditions themselves return nothing:

| Search | Files |
|---|---|
| `atmosphere.*genus` | 0 |
| `MinGravity` | 0 |
| `genus.*temperature` | 0 |

The search could have succeeded — the same repository answered the `GuiFocus` question in §6 on the
first query — so this is evidence rather than a failed lookup.

**But the question is smaller than it looked.** §1 means spansh already knows what is on a body that
somebody has scanned, with counts and values. A conditions table is only needed to predict what will
be on a body **nobody has visited** — which is exactly and only the first-footfall case, the one
worth 5×. That is a much narrower thing to send to web search than "the prediction half".

## 6. `GuiFocus` does distinguish the DSS panel; `ScanOrganic` carries no position at all

**The first rider is a yes.** `Status.json`'s `GuiFocus` is an integer, and EDDiscovery enumerates it:

```
NoFocus 0, SystemPanel 1, TargetPanel 2, CommsPanel 3, RolePanel 4,
StationServices 5, GalaxyMap 6, SystemMap 7, Orrery 8,
FSSMode 9, SAAMode 10, Codex 11
```

**`SAAMode = 10` is the DSS.** d47 reads `Status.json` already and does not read `GuiFocus` at all
today, so this is a field to start reading rather than a mechanism to build.

**The second rider is the structural one, and it is a no.** `ScanOrganic` carries no position
whatsoever — measured across all 632 events, every one of which has exactly these fields:

```
timestamp, event, ScanType, Genus, Genus_Localised, Species, Species_Localised,
Variant, Variant_Localised, SystemAddress, Body        (+ WasLogged on 525 of 632)
```

`Body` is a body **id**, not a location on it. There is no latitude, no longitude, no altitude.

**So the sample-spacing figure cannot come from the journal.** It has to be computed by pairing a
`ScanOrganic` line against the position `Status.json` held at that instant, which means d47 must be
running and sampling before the scan happens — there is no retrospective path and no backfill. That
is a real constraint on *Exobiology sampling* and it should be settled before that item is specified.

**What the journal does give is the sequence.** Grouping by system, body and species, 94 of 101
organisms scanned to completion show exactly:

> `Log → Sample → Sample → Analyse`

Three samples and a completion, four events. So "how many samples left" is answerable from the
journal alone even though "how far apart" is not.

## 7. One naming trap, free of charge

The genus localised as **"Radicoida"** has the symbol `$Codex_Ent_Ingensradices_Genus_Name;`. Stem
and display name share nothing. Anything keying exobiology on localised names will silently mismatch
against anything keying on symbols — which is the same shape as the Phase 14 finding where keying
materials on `FormattedName` lost 22 of 45 Encoded materials.

## 8. The mass ladder, derived from the game rather than recited

**Measured 2026-08-16, same corpus, while building *Read a system name*.** §4 answered what the mass
code is *worth* — nothing d47 may claim. This answers what it *means*, which is the half that ships,
and the item required it to come from a source with its provenance recorded rather than from memory.

**The strongest available source turned out to be the game itself.** A procedural name encodes a
boxel index in its three letters plus the boxel number. If a mass code's boxels are `S` ly on a side
then a sector — 1,280 ly — holds `N = 1280/S` of them per axis, and the index decomposes as
`i = idx % N`, `j = (idx // N) % N`, `k = idx // N²`. Regressing that decoded index against real
`StarPos` coordinates, with each sector's own mean removed so no sector grid origin has to be
assumed, recovers the box size as the slope of the fit.

Over 2,854 procedurally-named systems:

| Mass code | Systems | Measured slope | Nominal | Fit (`i`→`x`) |
|---|---|---|---|---|
| `a` | 298 | **9.99** | 10 | r² 0.999 |
| `b` | 1,400 | **20.02** | 20 | r² 0.997 |
| `c` | 638 | **39.51** | 40 | r² 0.993 |
| `d` | 489 | **78.23** | 80 | r² 0.975 |
| `e` | 26 | **165.32** | 160 | r² 0.990, thin |
| `f` | 2 | — | 320 | not measurable |
| `g` | 1 | — | 640 | not measurable |
| `h` | 0 | — | 1,280 | not measurable |

**The slope is stable across candidate `N`, which is what makes this a measurement rather than a
curve fitted to an assumption.** A wrong `N` scrambles the index-to-position mapping and fits
nothing; the `i`→`x` component recovers the same number regardless.

**The top three rungs are not measured and the shipped answer says so.** They rest on the doubling
the five rungs above establish, closed at the far end by the published rule that `h` is the sector
itself — and 10 × 2⁷ is exactly 1,280, so the ladder has nowhere else to land. Community
documentation corroborates both ends independently: mass code `a` within a 10 ly cube, `h`
"somewhere in this 1280 ly cube", halving through `g` at 640 and `f` at 320
([Marx's guide to boxels](https://forums.frontier.co.uk/threads/marxs-guide-to-boxels-subsectors.618286/),
which answers `403` to an automated fetch and renders in a browser — the trap this folder's README
names, hit again and handled the same way).

**And the grammar survey the parser's tests cite comes from the same run:** of 4,746 distinct real
names, 2,854 are procedural and 1,892 are hand-named, with **0 hand-named names containing a
boxel-shaped designator**. That last count is the one that matters — a grammar which silently
rejects a real name reports "this one has no mass code", which is a wrong answer wearing the shape of
a right one.

## 9. The star class comes from the route, not from the scan

*Read a system name* carries the main star's class, because a variant's colour follows the star and
the variant sets the price. Two candidate sources, measured over **7,412 arrivals**:

| Source | Coverage |
|---|---|
| `FSDTarget`, which fires *before* the jump and names the target and its `StarClass` | **99.7%** |
| The arrival auto-scan — a `Scan` of the main star carrying `StarType` | **28.6%** |

**The obvious source is the one that is usually not there.** So the class of the star a Commander is
sitting next to is the class the route named for the system they were entering, captured as the
`FSDJump` lands rather than discarded with the rest of the targeting state.

Two guards fall out, and both produce *no* answer rather than a plausible one: a jump that ended
somewhere other than the plotted target carries no class, and a name merely read aloud borrows none
from wherever the Commander happens to be standing.

## 10. The mass code question, answered — with a denominator under it

**Measured 2026-08-16.** §4 could not answer this and named the reason: a Commander's flight log is a
record of where somebody *chose to fly*, so if they already believe the folklore the data agrees with
the belief. The fix is to stop sampling routes and start sampling **bodies**, which is what the
maintainer proposed and what this does.

**The design, and the one thing that makes it valid.** Numerator and denominator come out of *one*
query stream — every landable body in a ball, with its landmarks attached — so they cannot disagree
about volume. Counting only the good systems would have "discovered" that the common mass codes are
the best ones, because they are common. Six references spread across the galaxy (Sol, Colonia,
Sagittarius A\*, Beagle Point, Merope, Diaguandri), each measured separately as well as pooled, so a
result that only holds in the bubble is visible as one that only holds in the bubble.

**13,000 landable bodies later, the folklore is directionally right and about the wrong quantity.**

| Mass code | Landable bodies | P(any value) | P(≥5M cr) | Mean cr/body | Best seen |
|---|---|---|---|---|---|
| `a` | 1,176 | 5.4% | **0.17%** | **79,333** | 5,289,900 |
| `b` | 2,504 | 7.5% | 2.88% | 453,106 | 83,537,100 |
| `c` | 5,697 | 5.6% | 3.05% | 460,293 | 46,880,300 |
| `d` | 2,672 | 6.8% | **4.19%** | **682,261** | 45,542,900 |
| `e` | 627 | 2.2% | 0.80% | 190,073 | 54,933,000 |
| `f` | 9 | — | — | — | — |

**Whether there is biology barely moves. What it is worth moves a great deal.** P(any valuable
landmark) sits between 5.4% and 7.5% across `a` to `d` with no trend in it. But P(a body worth 5
million or more) runs **0.17% → 2.88% → 3.05% → 4.19%**, a **25× rise from `a` to `d`**, and mean
credits per landable body runs **79k → 453k → 460k → 682k**, a **8.6× rise**. So *"more mass, more
biology"* is not supported and *"more mass, more valuable biology"* is.

**Per system, which is the unit a Commander actually chooses:** 20.2% of `a` systems hold at least
one landmark, against 34.0% of `b`, 29.6% of `c` and 37.2% of `d`.

**It appears to turn over at `e`**, which is worse than `a` on the 5M measure. That is the weakest
claim here — 627 bodies from a subset of the references — and `f`, `g` and `h` are unmeasurable at 9,
0 and 0 bodies.

**The mechanism shows through in the genus mix, not just the totals.** The top ≥1M species per mass
code are different lists rather than longer ones: `a` is almost entirely *Bacterium Acies*; `b` and
`c` bring in *Fonticulua Campestris*, *Stratum Tectonicas* and *Electricae Radialem*; `d` is
*Tussock Catena*, *Frutexa Flammasis*, *Stratum Paleas*, *Osseus Spiralis*; `e` is *Cactoida Vermis*,
*Clypeus Speculumi*, *Concha Renibus*. Mass code shapes what bodies and atmospheres generate, and
those gate genera — which is why the honest predictor is body type and atmosphere, and the mass code
is a **proxy that happens to be readable off a name with no network**. That is exactly the case where
a proxy earns its keep.

**One methodological result worth keeping.** Landmarks are not all organic — *Fumarole*, *Gas Vent*,
*Lava Spout*, *Geyser*, *Thargoid Barnacle* and *Surface Station* are about 19% of landmark rows. No
hand-written genus list was needed to exclude them, because the `≥1` and `≥1M` columns come out
near-identical: geological landmarks carry no `landmark_value`, so they never enter the count. The
measure is organic-only for free, and that is a property of the data rather than an assumption.

**What would have to be true before d47 says any of this out loud.** The residual bias is upload
coverage — the index holds what people uploaded, which still correlates with where people fly, though
far more weakly than one Commander's beliefs. Shell radii differ by reference (30 to 120 ly), so the
pooled row weights regions unequally. And the per-reference tables vary a lot: Beagle Point's `b` is
1.4% where Merope's is 8.2%. **The direction is solid; the magnitudes are not yet quotable.**

## 11. `CodexEntry` carries a position, and §6 did not look at it

**Found 2026-08-16 while chasing the above.** §6 established that `ScanOrganic` carries no latitude or
longitude, and concluded that sample spacing therefore needs live `Status.json` pairing and cannot be
backfilled. That is still true of `ScanOrganic` — and there is a second event it did not check.

`CodexEntry` fires 354 times in the corpus and carries **`Latitude` and `Longitude` on 130 of them**,
alongside `Name`, `Category`, `Region`, `System` and `BodyID`. 61 are *Organic structures*.

It is not a general fix: the Codex logs a **first** entry, so it covers the discovery rather than
every sample, and `IsNewEntry` is on 345 of 354. But it is a real positional record of organic finds
that costs nothing to fold, and *Exobiology sampling* should be re-scoped around it before that item
is built. **It does not carry genus conditions** — the Codex event is a discovery log, not the
encyclopedia's contents, so §5's null result stands.

## What this means for the items downstream

- ***Find the exobiology*** (Phase 18) — **ships, and is mostly wiring.** A fifth plot type on
  `IRouteService` using the contract in §1, with `from` → `source` written down where the next person
  will look, and values taken from the response rather than computed.
- ***Exobiology sampling*** (Phase 18) — **blocked on a decision, not on research.** Spacing needs
  live `Status.json` sampling paired to `ScanOrganic` by timestamp; sample *count* does not. Shipping
  the count half first is available and cheap.
- ***Read a system name*** (Phase 18) — **shipped 2026-08-16**, still the only item here that needs
  nobody to have been there first. It carries the ladder derived in §8 and the star class sourced in
  §9, and **it does not carry the mass-code payout heuristic**, per §4 — it declines it in the answer
  rather than by omission, because a Commander asking what the letter means is usually asking
  precisely that.
- **First-footfall value** — the ×5 in §2 is measured and may be quoted as a figure.

## Reproducing this

[`spike/ExobiologyProbe`](../../spike/ExobiologyProbe) holds the journal probe; the spansh contract
in §1 was established against the live service and is reproducible with any HTTP client. The corpus
is one person's play history and is not in the repo.
