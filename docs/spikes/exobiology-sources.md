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

## What this means for the items downstream

- ***Find the exobiology*** (Phase 18) — **ships, and is mostly wiring.** A fifth plot type on
  `IRouteService` using the contract in §1, with `from` → `source` written down where the next person
  will look, and values taken from the response rather than computed.
- ***Exobiology sampling*** (Phase 18) — **blocked on a decision, not on research.** Spacing needs
  live `Status.json` sampling paired to `ScanOrganic` by timestamp; sample *count* does not. Shipping
  the count half first is available and cheap.
- ***Read a system name*** (Phase 18) — unaffected by all of this, still the only item here that
  needs nobody to have been there first. **But it must not carry the mass-code payout heuristic**,
  per §4.
- **First-footfall value** — the ×5 in §2 is measured and may be quoted as a figure.

## Reproducing this

[`spike/ExobiologyProbe`](../../spike/ExobiologyProbe) holds the journal probe; the spansh contract
in §1 was established against the live service and is reproducible with any HTTP client. The corpus
is one person's play history and is not in the repo.
