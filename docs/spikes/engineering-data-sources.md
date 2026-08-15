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

### Correction — the count is deterministic, published, and the data files were the wrong place to look

**Read at source 2026-08-15**, in a browser, from
[Frontier's Type-8 update notes (18.08)](https://www.elitedangerous.com/update-notes/4-0-18-08) and
the [Fandom Engineers page](https://elite-dangerous.fandom.com/wiki/Engineers). Both refuse
automated fetches — 403 and 402 respectively — which is the only reason this took two passes.

Frontier's wording:

> "Updated engineer rolls to consistently give a fixed roll depending on the grade of the recipe and
> the commander rank with the engineer."

And the table, in full:

**Progress gained towards a modification, per roll**

| Modification grade | Access 1 | Access 2 | Access 3 | Access 4 | Access 5 |
|---|---|---|---|---|---|
| Grade 1 | 20% | 25% | 34% | 50% | **100%** |
| Grade 2 | — | 20% | 25% | 34% | 50% |
| Grade 3 | — | — | 20% | 25% | 34% |
| Grade 4 | — | — | — | 20% | 25% |
| Grade 5 | — | — | — | — | 20% |

**Two rules fall straight out of it, and they answer the whole question.**

1. **Rolls needed = 100 ÷ the percentage**, so the ladder is `5, 4, 3, 2, 1` indexed by
   `accessLevel − grade`. Nothing else is involved: not luck, not the module, not the Commander.
2. **A dash is a hard gate, not a slow path.** Grade *N* is unreachable below access level *N*. A
   grade 5 blueprint simply cannot be rolled at rank 4.

So the material total is `ingredients × rolls(grade, rank)`, exactly, and d47 already reads
`EngineerProgress.Rank`. The three-layer floor-and-record hedge the ship items were designed around
is unnecessary: **a plan can quote a total and be right.** The Commander's own `EngineerCraft`
history is still worth folding, but as corroboration rather than as the only source of a number.

Rule 2 is the more valuable half for a *plan*, because it converts a material shortfall into an
ordering problem: "you cannot roll grade 5 at Farseer until you are rank 5 with her" is a blocker no
amount of gathering fixes, and it is now statable as a fact rather than as a suspicion.

The lesson worth keeping: the community *data files* genuinely do not encode the roll count, and
that was measured correctly. Concluding that the number therefore did not exist was a **different
claim, made without evidence, and it was wrong** — the game's own patch notes had published the
whole table, behind nothing more than a user agent check.

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

## 10. Third pass — three more "no source" claims were wrong

**2026-08-15, same browser session.** Prompted by a fair question: if the wiki had the roll table,
what else does it have? All three of these were recorded in earlier passes as unfindable.

### The material trader rate is fully published

[Fandom, Material Trader](https://elite-dangerous.fandom.com/wiki/Material_Trader). It is a pure
function of grade delta and whether the category changes, exactly the shape §"unknowns" hoped for:

- one grade **lower** → `1 → 3` (you gain)
- one grade **higher** → `6 → 1`
- **different category** → `6 → 1`
- combinations multiply

| Same category, out ↓ in → | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| 1 | – | 1→3 | 1→9 | 1→27 | 1→81 |
| 3 | 36→1 | 6→1 | – | 1→3 | 1→9 |
| 5 | 1296→1* | 216→1 | 36→1 | 6→1 | – |

Cross-category is one further factor of 6 throughout, and **the asterisked conversions are flagged
by the wiki itself as impossible because of the storage cap**. That is a detail d47 can act on
rather than merely repeat: `MaterialGrades.CapacityOfGrade` already knows a grade 1 material caps at
300, and 1,296 does not fit in 300. So "that trade is defined and you cannot physically do it" is
computable, and it is exactly the sort of thing a Commander cannot work out in their head.

**This also means the "do not net a shortfall against a surplus" rule can be relaxed** — with a
published rate, netting is arithmetic. What must not be netted is a *cross-type* surplus: each
trader deals in one type only (Raw, Manufactured or Encoded), so Raw cannot become Encoded at any
price.

### The trader's location rule is authoritative, and the FDevIDs pseudocode was the wrong source

> Raw: **Refinery and Extraction** · Manufactured: **Extraction and Industrial** · Encoded:
> **High Tech and Military**

Extraction appears under *both* Raw and Manufactured, which is why §8's economy heuristic could not
be made to behave — the economy alone genuinely does not determine the type for an Extraction
station. Alongside it, four restrictions that are all **already filterable on spansh**:

- medium or high security
- population between 1,000,000 and 22,000,000
- not controlled by an anarchy faction
- not damaged, repairing or under lockdown

`security`, `population` and `government` are in `GalaxyFilters` today. So the right implementation
is a filtered system search plus the `services` group filter, not a heuristic over economies — and
the 152-of-200 embarrassment in §8 stops mattering.

### Engineer reputation has a published price, and the referral graph exists

Two things Phase 14 recorded as unsourceable:

**Rank costs money, and the amount is stated.** Reputation rises by buying modifications, and also
by selling exploration data or commodities at the workshop:

| Reach | Net profit sold at that workshop |
|---|---|
| Grade 2 | 500,000 cr |
| Grade 3 | 2,000,000 cr |
| Grade 4 | 8,000,000 cr |
| Grade 5 | 16,000,000 cr |

Combined with the roll table in §2 — where grade *N* is unreachable below rank *N* — a plan can now
say "that grade 5 blueprint needs rank 5 with Farseer, which is 16 million in profit sold at her
workshop", instead of "rank blocks you and I cannot say by how much".

**The referral graph is on the same page**, as a collapsed table, covering ship *and* on-foot
engineers: Elvira Martuuk → Mel Brandon and Zacariah Nemo; Felicity Farseer → Juri Ishmaak →
Colonel Bris Dekker and The Sarge; Selene Jean → Bill Turner and Didi Vatermann; and so on, plus
the three on-foot chains already known from EDEngineer.

**Not transcribed here on purpose.** The table uses row and column spans to express a tree, and the
flattened text is ambiguous in at least one place — Marco Qwent reads as publicly known in the
flattened form while the page's own prose says he is reached through Elvira Martuuk. A graph whose
failure mode is sending a Commander to grind the wrong engineer deserves a careful read of the
rendered table, not a scrape. **What is settled is that it exists and where it is**, which is what
the earlier claim got wrong.

## What is still unknown

Stated here rather than left implicit, because each is a place where guessing produces an answer
that reads exactly like the feature working. **Every row is a lead, not a verdict** — the first one
below was written as settled and was answered within hours by looking somewhere else.

| Unknown | Where the search stopped |
|---|---|
| ~~How many applications a grade takes~~ — **answered in full**, see §2 | The complete table is published. `rolls = 5,4,3,2,1` on `rank − grade`, and grade > rank is unreachable. Nothing left open. |
| ~~Whether application count varies with engineer `Rank`~~ — **answered: it is the entire mechanic** | Rank is the only variable. An empirical table keyed on anything else pools runs that are not comparable. |
| Whether `Engineering.Quality` is a per-roll draw or a cumulative fill | The journal schema says only `number, 0..1`. Now much easier to settle: with rolls at fixed 20/25/34/50/100% steps, a cumulative reading should land on those exact values. Getting it wrong produces a module the Commander can see is finished and d47 will not call finished. |
| Whether ship trade ratios are a pure function of grade delta | Suggested by schema examples. Still unread — but the on-foot pass found the *equivalent* mechanic fully documented and fully computable (§ on-foot doc), which is a strong hint that the ship one is written down somewhere too. |
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
