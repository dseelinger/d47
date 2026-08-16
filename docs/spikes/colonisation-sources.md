# Colonisation: what is data, what is prose, and what nobody can see

**Measured 2026-08-16** against the corpus used by
[journal-corpus-engineering.md](journal-corpus-engineering.md) and
[journal-corpus-warnings.md](journal-corpus-warnings.md): **912 journals, 373 MB, 3 July 2025 to 11
August 2026**, nine Commanders. The source survey was done the same day. Probe:
[`spike/ColonisationProbe`](../../spike/ColonisationProbe).

This answers `list.md` Phase 16, *Spike: what is already known about colonisation, and by whom*.

**What it settles.** Everything the journal reports about a construction site, including the
snapshot-or-delta question that has caught this repo twice before. Whether a licence-clean,
machine-readable facility table exists. Whether a claim is visible to anybody outside the game.

**What it does not settle.** Facility costs and effects themselves — see §4, which is the null
result, and §5 for what follows from it.

**Extended 2026-08-16** with §7, measured while building the Phase 18 tracking item: where the cargo
hold manifest actually lives, why a carrier's cargo cannot be itemised at all, and the commodity-name
fold that the whole subtraction rests on.

---

## 1. The headline: the tracking item needs no external source at all

**`ColonisationConstructionDepot` is a snapshot, not a delta.** Measured over **6,330 events and
120,208 resource rows** across 17 construction sites:

| Check | Result |
|---|---|
| Row count per site | **Constant** — 18, 19 or 20 rows depending on the facility, never varying within a site |
| Every event carries the full manifest | **Yes**, all 17 sites — the union across a site's events equals every single event |
| Does a satisfied commodity drop out? | **No** — 0 dropped, first event and last event carry the same rows |
| Does `RequiredAmount` move mid-build? | **No** — 0 of 17 sites, across every commodity |
| Is `ProvidedAmount` cumulative? | **Yes** — 0 decreases in 119,887 consecutive comparisons |

**This is the trap that caught `EngineerProgress` and `StoredModules`, and colonisation does not have
it.** A delta read as a snapshot produces a shortfall the Commander does not have, and it reads
exactly like a correct answer. Here the arithmetic is simply `RequiredAmount - ProvidedAmount` on the
most recent event per `MarketID`, with no accumulation and no history to keep.

The event:

```json
{ "timestamp":"2025-12-17T22:23:00Z", "event":"ColonisationConstructionDepot",
  "MarketID":3960809986, "ConstructionProgress":0.056927,
  "ConstructionComplete":false, "ConstructionFailed":false,
  "ResourcesRequired":[
    { "Name":"$aluminium_name;", "Name_Localised":"Aluminium",
      "RequiredAmount":500, "ProvidedAmount":0, "Payment":3239 } ] }
```

All seven top-level fields are present on all 6,330 events, and all five row fields on all 120,208
rows. Nothing is optional, so nothing needs a fallback.

**`Name_Localised` is always there**, which means the tracking item needs no commodity table at all —
not even `EDCD/FDevIDs` `commodity.csv`, which d47 already reads. The one item in this family that
ships first also has the shortest supply chain: the journal, and nothing else.

## 2. The other three journal answers

**More than one site can be active at once — so the tracking item is a collection, not a field.**
Three overlapping pairs in the corpus, and on 2025-12-23 **three sites were open simultaneously**
(`3959687426`, `3959690754`, `3959691778`). A design with one "current construction site" is wrong
for a real Architect.

**It fires while docked at that site.** Of 6,330 events, **6,307 were written while docked at that
very `MarketID`**; 14 while docked elsewhere and 9 with no docking state earlier in the file, both of
which are boundary artefacts of reading a file from its start rather than counter-examples. So the
data arrives only when the Commander visits — which means **what d47 knows is as fresh as their last
visit**, and it should say so rather than implying live numbers.

**A completed site keeps reporting.** `ConstructionComplete` goes true and events continue — 2 to 60
more per site. So completion is the flag, never "the events stopped".

## 3. Frontier's guide is the primary source, and it is prose

[System Colonisation Guide](https://www.elitedangerous.com/news/system-colonisation-guide), **28
February 2025**. It answers `403` to an automated fetch and renders fine in a browser — the trap this
folder's README names, hit again and handled the same way.

It states the mechanics outright, and confirms every claim `list.md` makes about it:

- A claim covers **an unpopulated system within 15 ly**, costs credits varying by station type, and
  **lasts 24 hours**, "preventing other players from submitting claims".
- Initial builds are Outposts, Coriolis, Ocellus or Orbis, refined by specialisation: **Commercial →
  Colony economy and increased wealth; Industrial → Industrial economy and increased tech level.**
- Facilities influence "the population, standard of living, tech level and more".
- Architects draw a **weekly tax**, with a galactic tax above **5,000,000** credits, and a discount on
  ships and outfitting in systems with **10 or more facilities**.

**And it publishes not one number.** No facility cost, no per-facility attribute value, no link rule.
It is the authority on *how it works* and silent on *what anything costs*.

**It is also eighteen months old.** It predates Trailblazers Update 3, which introduced the
port-to-facility link and economy topology that any planning table would have to model — a mechanic
the guide does not mention because it did not exist. So the primary source outranks everything on
mechanics and cannot be the source for figures, on two independent grounds.

## 4. The null result: no licence-clean, machine-readable facility table exists

Three candidates were named. All three were checked, and the checks could have succeeded.

| Source | What it is | Licence | Facility table? |
|---|---|---|---|
| [gaborauth/ed-colonisation-planner](https://github.com/gaborauth/ed-colonisation-planner) | Colonisation planner, MILP solver, models link/economy topology | **GPL-3.0** | Yes — in `src/data/buildings.ts`, **TypeScript source, not a data file** |
| [njthomson/SrvSurvey](https://github.com/njthomson/SrvSurvey) + [Raven Colonial](https://ravencolonial.com) | Live site tracking, fleet-carrier accounting, shopping lists | **GPL-3.0** | **No data files at all** — the repo contains zero `.json`/`.csv`/`.tsv` |
| [EDSC](https://www.edsc.info/) | Web tracker for systems and constructions | **none stated** | No published data, no API |

Two more places a clean table would live, and does not: **EDCD has no colonisation repository**, and
**`EDCD/FDevIDs` has no facility or construction file** — its 24 CSVs cover ranks, commodities,
materials, outfitting and engineers, and stop there.

**The real upstream is a spreadsheet.** `buildings.ts` says so in its own header: the figures are
"refreshed 2026-07-23 against DaftMav's *Colonization Construction v3* spreadsheet v3.4.1 … from the
sheet's Stats tab", and the `.ods` is vendored in the repo at 2.5 MB. Every tool in this ecosystem is
downstream of one community-maintained Google Sheet.

**The licences are a fact about the code, not about Frontier's figures.** That distinction is this
repo's rule and it cuts both ways here. GPL-3.0 means d47 cannot vendor, port or derive from
`buildings.ts` — d47 is MIT and permissive-only, and that is a hard stop on the *code*. It says
nothing about the numbers, which were Frontier's before anybody typed them into a spreadsheet. But
there is no licence-clean *route* to those numbers: the only machine-readable renderings of them are
inside GPL-3.0 source, and the sheet upstream of both states no terms at all.

**So the answer to the spike's question is: it is prose, effectively.** Not because nobody wrote the
numbers down, but because everybody who wrote them down did it somewhere d47 cannot follow.

The planner is worth reading anyway, and its header is a model of the habit this folder keeps asking
for: it names its upstream, dates its refresh, marks `system_score` as "real-game-verified 2026-08-10
… across 4 real systems", keeps an "explicitly unverified/best-effort constants" section, and records
that a previous derived model "turned out to be the wrong ingredients entirely, not just imprecise".

## 5. A claim is invisible to everybody except the Commander who made it

This is the question that decides what *Find somewhere worth colonising* may promise, and the answer
is the strong form of no.

**The journal records the act and not the clock.** `ColonisationSystemClaim` carries three things and
no fourth:

```json
{ "timestamp":"2025-07-14T21:10:54Z", "event":"ColonisationSystemClaim",
  "StarSystem":"Col 285 Sector SE-Q d5-63", "SystemAddress":2175107336563 }
```

**There is no expiry field.** The 24 hours is Frontier's published rule, not a number in the file, so
a countdown has to be computed as `timestamp + 24h` and labelled as derived. There is also **no
release or expiry event** — the corpus has exactly four colonisation event types
(`ColonisationConstructionDepot`, `ColonisationContribution`, `ColonisationSystemClaim`,
`ColonisationBeaconDeployed`), so a claim lapsing is written nowhere.

**`ColonisationBeaconDeployed` carries a timestamp and nothing else** — no system, no market id. It
is only interpretable against wherever the Commander was standing, which the journal does say
elsewhere.

**And nothing outside the game holds a claim.** The most complete live service in the ecosystem is
Raven Colonial, and its API — 19 endpoints across `project`, `cmdr`, `fc`, `quest` and `system` —
**contains the word "claim" zero times**. That is not an oversight: a claim is server-side state that
produces exactly one journal line, on one Commander's machine, and crowd-fed indexes are built from
journal lines. There is no line to send.

The consequence is structural rather than a gap to fill later. **Any "is this system free" answer is
stale by up to 24 hours and cannot be made fresh**, so the item below must present candidates as
*worth checking in the Galaxy Map*, never as *available* — and the one authority is the System
Colonisation Contact in-game.

## 6. What this means for the items downstream

- ***Colonisation and construction tracking*** (Phase 18) — **shipped 2026-08-16, and needed
  nothing.** No table, no network, no commodity list. Subtraction over one snapshot event per site,
  keyed by `MarketID`, with a collection rather than a single site and an "as of your last visit"
  caveat. §7 amends one line of the item: *what is sitting on the carrier* is a tonnage and not a
  manifest, because Elite writes no manifest and the derivable one is wrong twice as often as right.
- ***A colonisation plan writes the checklist*** (Phase 17) — **does not ship as a costed table.**
  The figures exist only under GPL-3.0 or in an unlicensed spreadsheet. What it can do is the shape
  the checklist already provides — an objective, an ordered set of intents, and progress diffed
  against the depot — with quantities entered by the Commander or read from the site once it exists,
  rather than predicted from a table d47 does not have. The strategy advice moves to web search, as
  the spike allowed for.
- ***Find somewhere worth colonising*** (Phase 18) — must promise **candidates, not availability**,
  for the reason in §5. The 15 ly rule and "unpopulated" are both checkable offline against data d47
  can reach; "unclaimed" is not checkable at all.

## 7. What the hold and the carrier can say, and what they cannot

**Measured 2026-08-16, same corpus, while building the Phase 18 tracking item.** §1 established what
a site *wants*. This is the other half of the subtraction — what the Commander *has* — and it was not
asked the first time. Two answers, and one of them closes a feature off.

### The hold manifest is in `Cargo.json`, and cannot come from the journal

| Check | Result |
|---|---|
| `Cargo` events carrying an `Inventory` array | **1,151 of 13,762** |
| …of which are the first `Cargo` in their file | 731 of 748 files |
| Later `Cargo` events carrying one | **420 of 13,014** |
| `Count` equal to the sum of `Inventory` when present | 1,151 of 1,151 |

So the event is a tonnage with a manifest attached at session start and stripped thereafter. **A
journal-only reader is correct for the first minute of a session and stale for the rest of it**,
which is the failure shape this repository keeps meeting: exact arithmetic over a stale input reads
exactly like a right answer. `Cargo.json` is rewritten on every change, so it is read the way
`Backpack.json` and `ShipLocker.json` already are.

One thing to carry rather than assume: **338 of the 13,762 events describe the SRV**, and the file is
rewritten for whichever vessel the Commander is in. Eight tonnes of scoopings reported as the ship's
four hundred would be wrong in a way nobody could see.

### A carrier's cargo is a tonnage, and there is no manifest behind it anywhere

`CarrierStats.SpaceUsage.Cargo` is a real published figure. Nothing Elite writes says what those
tonnes *are*. The only per-commodity signal is `CargoTransfer`, so the obvious model is to accumulate
it — and that model was built and reconciled against the game's own total at every `CarrierStats`:

| Check | Result |
|---|---|
| Derived stock matched the reported tonnage | **347** |
| Derived stock was wrong | **679** |
| Commodities driven **negative** | 118 occurrences across 11 commodities |

**The negatives are the proof rather than the drift.** A stock that goes below zero is a transfer out
of cargo the journal never saw arrive — the carrier's own commodity market, another Commander's
delivery, anything loaded before the file being read. So `CargoTransfer` is not an imprecise view of
carrier stock; it is a partial one, and no amount of care makes it whole. **d47 says how much and
refuses to say what**, which is the honest half rather than the useful-sounding one.

### The commodity name is spelled three ways, and one of them is the trap

| Source | As written | With uppercase |
|---|---|---|
| `ColonisationConstructionDepot` | `$aluminium_name;` | **0** of 31 |
| `Cargo.json` | `aluminium` | **0** of 64 |
| `ColonisationContribution` | `$ComputerComponents_name;` | **30 of 30** |

A normaliser that strips the `$` and the `_name;` and stops there joins the depot to the hold
perfectly — and matches **no contribution against anything**, reporting a delivery the Commander has
just completed as never having happened. Folding to lowercase is therefore load-bearing, and it is
the same lesson as the `MainEngines`-versus-"main engines" one Phase 17 learned: *the spelling of a
subject is part of its identity, and two sources will disagree about it.* With the fold, all 30
contribution symbols resolve to a commodity the depot asked for, and 0 of 31 depot symbols disagree
with the hold about the display name.

**And the two sources are not equal about naming.** `Name_Localised` is on all 31 depot symbols and
absent from **33 of 64** hold symbols, because Elite omits it where the display name is only the
symbol capitalised. So the depot names a commodity and the hold merely counts it — which is why the
report takes every spelling from the site's own manifest.

## Reproducing this

[`spike/ColonisationProbe`](../../spike/ColonisationProbe) holds the four scripts, run against the
corpus over SSH. `scan_depot.py` answers the snapshot-or-delta and docked questions; `scan_sites.py`
answers concurrency, the other three events, and whether the manifest ever moves; `scan_cargo.py` and
`scan_join.py` answer §7. None writes anything; all four print.

The corpus is one person's play history and is not in the repo. The one trap worth repeating: the
remote default shell reads piped input line by line, so send a script on stdin to `python -` rather
than splitting a pipeline across lines.
