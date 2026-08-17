# What 692,631 real events say about three phases that shipped in one day

**Measured 2026-08-16** against the corpus of **912 journals, 376 MB, 2 July 2025 to 11 August
2026** — the same files as [journal-corpus-engineering.md](journal-corpus-engineering.md), read
this time by driving `JournalReader.Poll()` and the whole `GameStateStore` fold over every one of
them.

Phases 17 (Checklists), 18 (Activity assistants) and 19 (Session tooling and release polish) all
shipped on 2026-08-16 with no soak time. This is the soak. It needs no HOTAS, no headset and no
running copy of Elite, which is why it was worth doing on the same day rather than later.

**The headline is that the crash hunt is empty and the symbol hunt is not.** Three findings are
recorded below; one is fixed with a test, two are reported rather than guessed at.

---

## 0. How it was driven, and where the corpus actually is

`spike/CorpusReplay` is a console app referencing `D47.Core` and nothing else. It walks every
journal in filename order, builds a `JournalReader` per file, calls `Poll()` until it stops
producing, and feeds each event through one shared `GameStateStore` — then through the full
production callout set, one event per tick, so every callout sees every event as new. Each of
`Apply` and `Tick` is wrapped so a throw is recorded and replay continues, and an
`ILoggerFactory` captures everything at `Warning` or above.

**The whole corpus replays in 6.9 seconds.** That is the invariant paying for itself: Core owns no
thread and reads no clock, so the harness runs it as fast as the disk allows. There is nothing to
wait for and no reason not to run this on every phase from here.

> **The corpus was on this machine all along.** Both
> [journal-corpus-engineering.md](journal-corpus-engineering.md) and the README describe it as
> living on "a second machine" and being "read over SSH". It is at
> `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous\` on the development machine —
> 912 files, the same count, the same date range. No SSH was involved and no gotcha was hit,
> because there was nothing remote to reach.
>
> That is the same lesson those pages already make, wearing a third costume. The first two were *a
> user-agent check* and *one machine's play history*; this one is **a note about where the data was
> that outlived the data moving**. Recorded here rather than quietly corrected, because the
> instruction that sent this session looking for an SSH recipe was itself downstream of it. The
> honest sentence is still *where the looking stopped* — and it stopped, correctly, at `ls`.

## 1. Crashes and unhandled event shapes: nothing

| | |
|---|---|
| Events replayed | **692,631** |
| Distinct event kinds | **221** |
| Unparseable lines | **0** |
| Throws out of `GameStateStore.Apply` | **0** |
| Throws out of `CalloutEngine.Tick` | **0** |
| Log records at `Warning` or above | **0** |
| Announcements produced | 75,608 |

221 distinct event kinds is every event Elite has written in thirteen months across three
accounts, including the on-foot, Powerplay, carrier, colonisation and mining families the three
new phases touch. Nothing threw and nothing was logged.

That is a real result rather than an empty one, and it is worth saying why it holds: `JournalJson`
answers **null** for a field that is missing or the wrong type, everywhere, without exception. A
schema change is therefore inert by construction rather than by each folder remembering to check.
The replay exercises that on 692,631 real events and finds no hole in it.

**One number in the older page is wrong and this run corrects it.** The corpus is **three
Commanders, not nine** — nine is the count of *character names*, and the three Frontier ids behind
them have been renamed over the thirteen months (`F12242026` alone answers to CALVIN INSTI,
DEPARAGON and MOSBY-S). d47 is right about this and the document was not: `GameStateStore` keys on
the FID with the comment *"The name can change (rename); the FID is the stable key"*, and the
replay produces exactly three buckets.

## 2. The symbol-spelling family, measured across the whole corpus

Two scans, both in `spike/CorpusReplay`. `scan_case.py` asks whether **one event** spells a field
more than one way. `scan_join.py` asks the harder and more useful question — whether **event A's
spelling equals event B's**, which is what every join in d47 actually rests on.

Where the corpus disagrees with itself, by family:

| Family | Raw spellings | Folded | Clashing across events |
|---|---|---|---|
| module | 911 | 579 | **332** |
| commodity | 280 | 129 | **86** |
| ship type | 55 | 33 | **22** |
| suit | 18 | 12 | **6** |
| hand weapon | 13 | 8 | **5** |
| engineering material | 115 | 115 | 0 |
| blueprint | 37 | 37 | 0 |
| experimental effect | 29 | 29 | 0 |
| engineer | 34 | 34 | 0 |
| genus | 126 | 126 | 0 |
| micro-resource | 81 | 81 | 0 |
| module slot | 134 | 134 | 0 |

`aluminium` is the sharpest single case: **four spellings across four events** —
`$Aluminium_name;` from `ColonisationContribution`, `$aluminium_name;` from the depot, `Aluminium`
from `CollectCargo`, and `aluminium` from `Cargo.json`, `MarketBuy`, `MarketSell` and `EjectCargo`.
`JournalJson.Symbol` folds all four, and the colonisation join is correct.

**The good news is the larger half of this section.** The two spots the brief expected to find more
of this family are already right, and both carry their measurement in a comment: `ChecklistKeys.Compact`
collapses `MainEngines` / `main engines` / `Main Engines`, and `JournalJson.Symbol` folds
`ColonisationContribution`'s mixed case against the depot's. Blueprints, experimentals, engineers,
genera, slots and engineering materials do not vary at all in 912 journals — so the risk in those
families is genuinely absent rather than merely unobserved.

### 2a. FIXED — a ring signal Elite left unlocalised is said in the wrong case

**`SAASignalsFound` is the one surface that got the prospector's lesson a day late.**

Where the event names a ring's mineral it usually omits `Type_Localised` and leaves a bare symbol.
Eleven of the twelve are already title case — `Alexandrite`, `Painite`, `Serendibite`,
`Musgravite` — and one is not. Across 792 events Tritium arrives as:

| Spelling | Rows |
|---|---|
| `tritium` (no `Type_Localised`) | **22** |
| `Tritium` (no `Type_Localised`) | **21** |

`BodySignals` read it with `Named`, so `get_body_biology` answered *"Also down there: 4 tritium"*
on 22 rows and *"4 Tritium"* on 21 — the same mineral, twice, in one list.

**Why it survived review.** Tritium is the only lower-case one of the twelve, so every other ring
material makes the code look correct, and the eleven that read properly are the common case.

**It is the same defect Phase 18 had already fixed one file away.** `ProspectedRock.Display` exists
because `ProspectedAsteroid` has the identical habit — measured at **14 of 27 materials appearing
under both spellings, 41 raw spellings for 27 materials**. The prospector got a fix; the surface
scan did not.

Fixed by promoting that private helper to `JournalJson.Spoken`, the counterpart to `Named` for
speaking as `Symbol` is for matching, and using it in both places. It touches the first letter and
only when it is lower case: inventing a prettier name than the one Frontier wrote would be
inventing game data. Covered by
`ExobiologyCapabilityTests.ARingSignalEliteLeftUnlocalisedIsSpokenLikeTheRest`, which was watched
failing before the fix went in.

> The two figures in `ProspectedMaterial`'s own comment were off by one and are corrected in the
> same change: **14** of 27 materials appear under both spellings and there are **41** raw
> spellings, not 13 and 40. Recounted here against the same 1,633 events.

### 2b. REPORTED — the "newer than my table" sentence reaches one hull in three

`EliteSpecifications` carries a `[known-but-unmeasured]` section for hulls it knows exist and has
no figures for, and `SpecificationCapability.Unknown` says so in as many words:

> *"…is a ship I know of and have no figures for — it is newer than the specification table d47
> ships. I would rather say that than guess at its numbers."*

The comment above it states the intent exactly: *"Collapsing them into 'I don't know that ship'
would tell a Commander flying a brand new hull that d47 is broken, when the truth is that the table
predates their ship."*

**The list is keyed on display names and the lookup arrives holding a journal symbol.** Put every
unresolvable hull in the corpus through the match the capability actually performs:

| Journal symbol | Corpus events | What the Commander hears |
|---|---|---|
| `corsair` / `Corsair` | 1,354 | ✅ "newer than my table" |
| `explorer_nx` / `Explorer_NX` | 373 | ❌ *"flying a 'explorer_nx'"* |
| `smallcombat01_nx` / `SmallCombat01_NX` | 79 | ❌ *"flying a 'smallcombat01_nx'"* |
| `mediumtransport01` / `MediumTransport01` | 79 | ❌ *"flying a 'mediumtransport01'"* |

The table lists `Caspian Explorer`, `Corsair` and `Kestrel Mk II`. **Corsair works only by
coincidence** — its symbol happens to be its name. Caspian Explorer is in the list and is never
reached, because Elite writes `explorer_nx`. Kestrel Mk II is in the list and is never reached,
because Elite writes `smallcombat01_nx`. Lynx Highliner (`mediumtransport01`) is not in the list at
all, though its armour rows are in the table.

So a Commander in a Caspian Explorer — a real hull, 373 events in this corpus — hears a raw
internal symbol read back at them, which is the exact outcome the section was written to prevent.
It is not a *wrong* answer, which is why it is reported rather than counted as confidently wrong:
d47 says it has no figures, and it has none.

**Not fixed, because the fix is a decision rather than a correction.** The symbols are recoverable
— the armour rows already carry them (`explorer_nx_armour_grade1` → "Caspian Explorer Mk II
Ablative Lightweight Alloys") — but making the join work means the `[known-but-unmeasured]` section
carrying a symbol column beside the name, which is a table format change plus a regeneration
through `tools/gen-elite-specs.py`, and that is the maintainer's call. Worth noting that the
generator has the symbol in hand at the point it writes the section, so the change is small where
it is not free.

### 2c. REPORTED — `EliteSpecifications.Module` does not strip Frontier's decoration

`Module(symbol)` lowercases its argument and stops. Elite writes module symbols two ways —
`int_powerplant_size6_class5` from `Loadout`, `$int_powerplant_size6_class5_name;` from `ModuleBuy`,
`ModuleSell`, `ModuleStore`, `ModuleRetrieve` and `StoredModules`. Measured over every module
symbol in the corpus:

| Lookup | Spellings resolving |
|---|---|
| `Module(raw)` | 348 / 911 |
| `Module(JournalJson.Symbol(raw))` | **684 / 911** |

The remaining 227 are nameplates, decals, bobbles, paint jobs, voice packs and cockpits — correctly
absent from a specification table, since they have no specifications.

**This is latent rather than live, and the honest version of the finding says so.** Nothing today
passes a decorated symbol to it: `ModuleStore` reads `StoredModules` through `Named` and stores a
*readable name* rather than a symbol, and every live caller sources its symbol from `Loadout`,
which writes the bare form. The 348/911 figure describes the function, not any answer a Commander
has had.

Left alone deliberately. Folding it would be one line and is arguably right, but it is a change to
a lookup with no failing caller, and the brief for this pass was to fix what is unambiguous rather
than to harden what is merely fragile. Recorded so the next surface that reads `ModuleBuy` — the
obvious candidate is a "what did that refit cost" item — starts knowing it.

## 3. Figures quoted without their freshness caveat: none found

This was the third thing hunted and it came back clean, across every surface checked:

| Surface | Stamp | Carried in the answer |
|---|---|---|
| Construction depot rows | `ConstructionSite.SeenAt` | *"As of your last visit, 2026-08-14."* |
| Your own contributions | session-scoped by construction | *"since I started reading this session"* + *"the delivered figures above are everybody's"* |
| Stored modules | `ModuleStore.TakenAt` | *"as of 2026-08-11 19:24 UTC."* |
| Carrier cargo | `CarrierState.StatsSeenAt` | *"was holding … as of …"* |
| Community goals | `CommunityGoal.SeenAt` | *"Reported 3 days ago."* |
| Surface scans | `BodyBiology.SeenAt` | *"surface-scanned 2026-08-16 10:00."* |
| Market stock | `StationQuery.StockLastSeen` | *"stock last reported 2026-08-09"* |
| Engineering completion | `EngineeringRules.CompleteAt` | carries its 0.85 **and its sample size**, and says it is the lowest observed rather than a proven threshold |

Two are worth calling out as better than merely caveated. `ColonisationQuery.KnownPlanets`
distinguishes *how many planets are known* from *how many there are*, and the colonisation search
counts unsurveyed systems out loud rather than recommending or dropping them — a system nobody has
honked reads as a star and nothing else, and reporting that as a small system would be a wrong
answer that looks right. And `BodyBiology` refuses to quote a value at all, because
`SAASignalsFound` names the genus and never the species, and the species is what sets the price.

**One thing to watch, offered as a question rather than a finding.** `ColonisationQuery.BodyCount`
is parsed as `Integer(element, "body_count") ?? 0` and reported bare as *"N bodies"*. The `?? 0`
turns *unknown* into *zero*, which is the substitution `JournalJson`'s own doctrine exists to
refuse — *"a helper that returned 0 for a missing number would put an invented value into game
state"*. Whether spansh ever omits `body_count` for a system that passes the `KnownPlanets > 0`
filter was **not** measured here: this pass was offline by design and answering it needs a live
query. If it can, the answer reads *"Kappa Fornacis — 12.3 ly, 0 bodies"* directly above a list of
that system's planets. Stated with its limit, per the habit these pages keep: the code path is
real, the reachability is unmeasured.

## 4. How to re-run this

```
dotnet run --project spike/CorpusReplay              # replay, callouts, table resolution
python spike/CorpusReplay/scan_case.py               # one event, two spellings
python spike/CorpusReplay/scan_join.py               # event A vs event B
```

All three take a journal directory as an optional argument and default to Elite's own. The replay
exits non-zero if anything throws or logs, so it is usable as a gate rather than only as a report.

The probe is throwaway per `spike/README.md`, but this one is worth keeping until the phases it
soaks have shipped a release: **6.9 seconds for 692,631 events** is cheap enough that "has anything
started throwing" stops being a question anybody has to schedule.
