# What the engineering data sources actually contain

**Measured 2026-08-14.** Everything below was established by parsing the files and querying the
live service, not recalled. It is the ground under Phase 14's `#102 Help with engineering` items,
and the reason several of them are shaped the way they are.

No probe directory survives. Each finding names the file or endpoint and the query, which is what
makes it re-measurable when a source moves — a preserved script that has rotted is worse than a
recipe.

**Read the gaps as open, not closed.** Where this document says something was not found, it means
exactly that. §2 is the standing warning: it originally concluded that a number did not exist
anywhere, and the number turned up a few hours later while researching something else. Treat every
row of §"What is still unknown" as a lead rather than a verdict.

---

## 1. A blueprint application costs exactly one of each ingredient

The single most useful thing found, because it collapses a vague problem into a precise one.

| Source | Blueprints | Ingredient entries | Any `Size` ≠ 1? |
|---|---|---|---|
| `msarilar/EDEngineer` `blueprints.json` | 786 graded module blueprints | 1,885 | **none** |
| `EDCD/coriolis-data` `modifications/blueprints.json` | 81 | 926 components | **none** |

So d47 is not uncertain about what a roll costs. It is uncertain about **one integer** — how many
rolls a grade takes — and it knows exactly where that integer belongs.

## 2. Neither source encodes applications per grade

The key sets are closed and were enumerated in full:

- EDEngineer: `Type`, `Name`, `Engineers`, `Ingredients`, `Effects`, `Grade`, `CoriolisGuid`.
- coriolis, per grade: `components`, `features`, `uuid`.

`features` is `[min, max]` — the effect range a grade **spans**. Armour_Explosive `explres` runs
`[0, 0.12]` at grade 1 and `[0.12, 0.19]` at grade 2. That corroborates that a grade is a range
rather than a point, and still says nothing about how many applications cross it.

Community figures range from five to eleven for a grade 5 and contradict each other. That is
exactly the shape of knowledge this repository refuses to ship.

### Correction — the count is deterministic, and the data files were the wrong place to look

**Found 2026-08-14, while researching on-foot engineering.** The two conclusions above are correct
about the *files* and wrong about the *game*.

The **Type-8 update (4.0.18.08)** changed engineering rolls *"from a random percentage of progress
gained to an exact amount of progress gained"*. There is now always an exact number of rolls to
complete a grade, **scaling down with the Commander's access level at that engineer** — at rank 5,
one roll for a grade 1 blueprint, two for a grade 2, through five for a grade 5. Below rank 5 it
takes more, and is still deterministic.

Two things follow, and they run in opposite directions:

- **A total is quotable.** A known unit cost (§1) times a known count is a real number, and d47
  already reads `EngineerProgress.Rank`. The three-layer floor-and-record hedge the ship items were
  designed around is stronger than it needed to be at rank 5, and the empirical layer stops hedging
  against randomness and starts filling in the rank 1–4 multipliers, which were not in what was
  found.
- **This is sourced but unread.** `elitedangerous.com/update-notes/4-0-18-08` returns 403 to
  automated fetch. It is corroborated across search excerpts of that page, the Fandom Engineers page
  and two Steam threads — none of them read at source. Someone should open it in a browser before
  any arithmetic is built on it.

The lesson worth keeping: the community *data files* genuinely do not encode the roll count, and
that was measured correctly. Concluding from that that the number did not exist was a different
claim, made without evidence, and it was wrong.

## 3. The two sources disagree, and the disagreements are structural

- 688 of 786 graded blueprints share a `CoriolisGuid`. **27 of those 688 disagree on ingredients.**
  Power Distributor "Weapon Focused" disagrees at all five grades and coriolis's list is a
  different blueprint's materials entirely; "Sturdy Mount" grade 5 has empty components in
  coriolis.
- Experimental effects: EDEngineer models 155, coriolis `specials.json` has 91 of which 87 carry
  components. Only 59 join by uuid and **11 of those 59 disagree**.

The experimental disagreement is not noise. **11 of 62 named effects have a different recipe per
module type** — "Double Braced" has eight distinct recipes. Coriolis models one recipe per effect
and the game does not, so experimentals must come from EDEngineer.

## 4. Keying materials on EDEngineer's own id loses 22 of the 45 Encoded materials

`FormattedName` is EDEngineer's identifier, not Frontier's symbol, and it differs for exactly 22
materials — every one of them Encoded, always by the same rule, the leading adjective retained
where Frontier drops it:

| EDEngineer `FormattedName` | Journal symbol |
|---|---|
| `anomalousbulkscandata` | `bulkscandata` |
| `crackedindustrialfirmware` | `industrialfirmware` |
| `dataminedwakeexceptions` | `dataminedwake` |
| `atypicaldisruptedwakeechoes` | `disruptedwakeechoes` |
| …18 more, all Encoded | |

The failure is not an exception thrown somewhere visible. It is a requirement for Datamined Wake
Exceptions that never matches a holding of `dataminedwake`, so d47 reports a shortfall the
Commander does not have, in the category that is already the most tedious to gather.

**Key on the FDevIDs `symbol`, join on display name, fail the generator loudly on anything
unresolved.** Four display names drift and need named aliases — all confirmed as drift rather than
absence:

| EDEngineer | FDevIDs |
|---|---|
| `Guardian Wreckage Components` | `Guardian Sentinel Wreckage Components` |
| `Abnormal Compact Emission Data` | `Abnormal Compact Emissions Data` |
| `Ballistic Data` | `Ballistics Data` |
| `Xihe Companions` | `Xihe Biomorphic Companions` |

All 3,396 blueprint ingredient references resolve against `entryData.json` by `Name` with zero
unresolved, so EDEngineer is internally consistent and both tables can be generated from one fetch
and cross-checked against each other.

One more: **EDEngineer's `Effects` strings are double-encoded** — the tick mark arrives as
`"âœ“"`, UTF-8 bytes read as cp1252. Repair in the generator, never at runtime.

## 5. Unlock tributes span three inventories, not one

Joining all 258 distinct ingredient names across the blueprint list against FDevIDs:

| Bucket | Ingredient references |
|---|---|
| `material.csv` — Manufactured | 1,430 |
| `material.csv` — Raw | 1,095 |
| `material.csv` — Encoded | 335 |
| `microresources.csv` — Component / Data / Item | 193 / 139 / 131 |
| `commodity.csv` | 46 |
| `rare_commodity.csv` | 5 |
| unresolved | 22 (4 distinct — the aliases above) |

Read down the 26 engineer tributes and all three inventories appear: **ship materials** (Modular
Terminals ×25, Sensor Fragment ×25), **cargo commodities** (Gold ×200, Landmines ×200,
Meta-alloys ×1, Soontill Relics ×3), and **ship-locker goods and data** (Push ×5, Opinion Polls
×40, Settlement Defence Plans ×15).

So "unlock costs come out of the same material caps" is true for some tributes and **false for
most**. Gold ×200 is two hundred tonnes of cargo; filing it under a 300-unit material cap produces
a feasibility verdict that is nonsense, delivered confidently. The classification is free — it
falls out of the same join that makes the table possible at all.

## 6. EDEngineer ships a sourcing table

Unexpected, and it removes most of the reason to think "where do I farm this" is unanswerable.

`EDEngineer/Resources/Data/entryData.json` (MIT), 371 entries, each with `Name`, `Kind`, `Rarity`,
`Group`, `FormattedName` and **`OriginDetails`**. 365 of 371 carry at least one origin; the six
that do not are Odyssey consumables. **77 distinct origin strings** — a closed vocabulary a
generator can enumerate and a human can map once:

```
195  Planetary Settlement          25  Signal source            13  Ancient/Guardian ruins
111  Mission reward                17  Ship salvage (transport)  12  Mining
 28  Surface prospecting           14  Crashed Satellite          8  Mining (ice rings)
  3  Signal source (High grade emissions, Boom)
  2  Signal source (High grade emissions, Federation systems)
  2  Signal source (High grade emissions, War/Civil war)
  1  Signal source (High grade emissions, Empire systems / Civil unrest / Outbreak)
```

And **spansh's system index has a `state` filter** whose values include `Boom`, `War`,
`Civil War`, `Civil Unrest` and `Outbreak` — exactly the states those strings name. So grade-5
Manufactured sourcing is end-to-end answerable from two sources d47 already uses. `state` is one
new row in `GalaxyFilters`.

The caveat belongs in the answer, not a comment: system state turns over on the BGS tick and the
index is a snapshot, so the sentence is "systems **reported** in Boom" — the same crowd-report
framing `StockLastSeen` already carries.

## 7. Three more spansh filters, and two of them lie the same way

Measured against `api/bodies/search` and `api/stations/search`, reference system Sol.

**Body materials — a group filter.** Fourth member of the silent-ignore family after `signals`,
station `modules` and `services`.

| Request | Landable bodies ≤ 20 ly |
|---|---|
| no material filter | 703 |
| `{"materials":{"Yttrium":{"min":"1","max":"3"}}}` — the obvious spelling | **703** (ignored) |
| `{"materials":{"name":{"value":["Yttrium"]}}}` — the group | **152** |
| `{"materials":{"name":{"value":["Bogusium"]}}}` | 0 |

Three consequences:

- **Share is not filterable.** `percentage`, `value` and `count` beside `name` all return an
  unchanged 152 — silently ignored.
- **Share is not sortable.** `sort: [{"yttrium":{"direction":"desc"}}]` and a bogus sort key
  returned byte-identical result orders. An unrecognised sort key is dropped.
- **Share comes back on every result** — `materials: [{"name":"Yttrium","share":1.196306}, …]`.

So: **filter on presence remotely, rank by share locally** — the same treatment
`distance_to_arrival` already gets, and say what the ranking is over. "The best of the bodies I
fetched within 20 light years", never "the best in the galaxy", because it provably is not.

**Three raw materials are absent from the index entirely.** `field_values/materials` lists 25;
Rhenium, Lead and Boron are not among them. Those must be **declined by name** — a search that
returns nothing reads as "there is none near you", which is a wrong answer that looks like a right
one.

**Station services — a group filter, and the flat shape lies.**

| Request | Stations ≤ 50 ly |
|---|---|
| `{"services":{"value":["Material Trader"]}}` | 10,000 (the unfiltered cap) |
| `{"services":{"value":["Bogus Trader"]}}` | 10,000 |
| `{"services":{"name":{"value":["Material Trader"]}}}` | **100**, all 50 sampled genuinely carrying it |

**Landmarks are already indexed.** `landmark_subtype` carries 346 subtypes including Crystalline
Shards (8,990 bodies), Guardian Beacon (29), Codex (1,218), Data Terminal (23), Pylon (1), Relic
Tower (19) and Sentinel (3). A closed vocabulary, matched locally like every other one.

## 8. The trader-type rule is wrong read literally

FDevIDs ships a file called `How to determine MatTrader and Broker type` — pseudocode mapping
station economies to trader type, permissive, and carrying its own `// needs a confirmation` on
the broker half.

Measured against 200 real stations with a Material Trader within 150 ly of Sol:

| Reading | Manufactured | Encoded | Raw | Unclassified |
|---|---|---|---|---|
| Literal (sequential assignment ⇒ **secondary wins**) | 46 | 1 | **152** | 1 |
| Primary first, secondary as fallback | 64 | 83 | 52 | 1 |

152 of 200 raw traders is not a galaxy anybody plays in. Ship the primary-first reading, **labelled
as a heuristic**, because a plausible distribution is not a proof.

Two traps found on the way:

- **`economies` does not say which economy is primary.** It comes back alphabetically ordered with
  shares — `[{Extraction,30},{Industrial,70}]` on a station whose `primary_economy` is Industrial.
  Reading position 0 as "primary" is what produced the 152. `primary_economy` is a separate field
  and the only one that answers the question.
- **One station in 200 is genuinely unclassifiable** — Gresley Dock in Nanomam, economies
  `(Agriculture,)`, has a Material Trader and the rule assigns nothing. That is the third state and
  it needs a sentence, not a guess.

The journal settles it per Commander: `MaterialTrade` carries `MarketID` and **`TraderType`**
(`raw`/`manufactured`/`encoded`). Every trader the Commander has used is a known type at a known
market id, and an observed table should override the heuristic — which then only ever answers about
stations nobody has visited, which is what a heuristic is for.

**Trade ratios are not in any permissive source.** The journal schema's own examples are suggestive
— 60 Proto Light Alloys → 10 Proto Radiolic Alloys is 6:1 up one grade; 14 Pharmaceutical Isolators
→ 42 Phase Alloys is 1:3 down one grade — but **examples in a schema are not a stated rule** and
d47 must not ship them as one.

## 9. Operations and Merc Coin — reported, not verified

Update 4.4, around 30 June 2026. Merc Coin is earned from Operations objectives (roughly 50 easy /
150 hard, capped 1,000 a week, 9,999 held) and spent on **pre-engineered ship modules, new
blueprints to engineer them further, and on-foot gear mods**. Around sixteen modules named across
exploration, trading, combat and mining, costing up to ~850 coins.

**This is community reporting, not a verified source.** The official patch notes returned 403 and
the wiki returned 402 on 2026-08-14, so nothing here was read from Frontier. Two consequences that
matter to the design, and both need the journal to settle them:

- A pre-engineered module breaks the "unmodified → blueprint → grade" model that everything else
  assumes.
- Merc Coin blueprints will not be in EDEngineer or coriolis-data yet, which is the same lag that
  already left Caspian Explorer, Corsair and Kestrel Mk II out of the specification table.

---

## What is still unknown

Stated here rather than left implicit, because each is a place where guessing produces an answer
that reads exactly like the feature working. **Every row is a lead, not a verdict** — the first one
below was written as settled and was answered within hours by looking somewhere else.

| Unknown | Where the search stopped |
|---|---|
| ~~How many applications a grade takes~~ — **answered**, see §2 | What remains is the **rank 1–4 multipliers**, absent from what was found, and reading Frontier's notes at source rather than through excerpts. |
| ~~Whether application count varies with engineer `Rank`~~ — **answered: yes, by design** | The scaling is the mechanic. An empirical table must therefore be keyed by rank, or it pools runs that are not comparable. |
| Whether `Engineering.Quality` is a per-roll draw or a cumulative fill | The journal schema says only `number, 0..1`. Getting it wrong produces a module the Commander can see is finished and d47 will not call finished. |
| Whether trade ratios are a pure function of grade delta | Suggested by schema examples. Not chased further — and the on-foot pass found per-item barter prices sitting in a file this search had already opened, so this one is probably closer than it looks. |
| Whether `MaterialTrade` ever crosses Raw/Manufactured/Encoded | Believed impossible. Believing is not knowing, so the design offers same-category surpluses only. |
| Whether the primary-first trader reading is correct, or merely less absurd | 64/83/52 across 200 stations is plausible, not proven. |
| Whether the 27 blueprint and 11 experimental disagreements are coriolis errors, EDEngineer errors or version skew | Two cases were examined of 38. |
| Whether Elite reuses a `ShipID` after a ship is sold | Decides whether a stale-hull check is load-bearing or merely tidy. |

## How to re-measure

- **EDEngineer** (MIT): `EDEngineer/Resources/Data/blueprints.json` and `entryData.json` from
  `msarilar/EDEngineer`. Ingredient sizes, origin strings and the `FormattedName` drift are all
  plain parses.
- **coriolis-data**: `modifications/blueprints.json` and `modifications/specials.json` from
  `EDCD/coriolis-data`. Join to EDEngineer on `CoriolisGuid` / `uuid`.
- **FDevIDs**: `material.csv`, `microresources.csv`, `commodity.csv`, `rare_commodity.csv`,
  `engineers.csv`, and the `How to determine MatTrader and Broker type` file.
- **spansh**: `POST api/bodies/search` and `api/stations/search` with the bodies described above,
  and `GET api/bodies/field_values/<field>` for the vocabularies. The method throughout is the one
  Phase 14 established — send the obvious spelling and the group spelling and compare the counts
  against the unfiltered total, because a filter that is ignored answers with total confidence.
