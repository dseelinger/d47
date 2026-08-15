# Phase 14 `#102` — engineering, end to end

## Context

Two `list.md` items carry `#102`: **Know what engineering actually does** (the facts) and **Go and
get it** (the sourcing). Both were written around uncertainty that no longer exists.

Three things changed on 2026-08-15, committed in `7e2f3ac` and after:

- **6,272 real engineering rolls** settled what `Engineering.Quality` means, that a grade completes
  at **0.85 rather than 1.0**, and that the published trade rate holds exactly across 1,096 trades.
- **EDDiscovery/EliteDangerousCore** (Apache-2.0), found through edcodex.info, carries the **referral
  graph** and a `MaterialGroupType` that is the material **line** the trade rate depends on.
- The trade rule's published wording hides a trap: its extra 6× applies across material *lines*, not
  the journal's Raw/Manufactured/Encoded `Category` — which nothing ever crosses.

So this plan is **less hedged and more capable** than the items it implements. It quotes totals
instead of floors, prices a rank block instead of shrugging at it, and ships the referral chain the
already-ticked *Engineers* item had to leave out.

**Scope is the rest of Phase 14.** Both `#102` items, the referral graph, community goals and web
search. That is every open item in the phase, so finishing this **closes Phase 14** and the release
is `v0.10.0` from `v0.9.0` — per CLAUDE.md, a completed phase is always a minor release. Anything
less ships as patches and leaves the phase one item short, which is how phases stay one item short.

**Frontier's media usage rules were read and accepted** by the maintainer on 2026-08-15. Use stays
non-commercial; tables stay derived with provenance recorded; `NOTICE` and the site footer carry
their long-form attribution.

---

# Step 0 — Prove `line` before anything rests on it

A throwaway probe under `spike/`: parse EDDiscovery `EliteDangerous/FrontierData/Items/MCMRType.cs`,
emit all 32 `MaterialGroupType` lines as grade ladders, and check them against the in-game trader
grid.

**Why first.** This is the plan's single point of failure — 32 lines, exactly one permissive source,
parsed out of C# rather than read from data. Every priced answer in Step 9 rests on it. A line is one
column of the trader's grid, one material per grade:

```text
RawCategory1       Carbon → Vanadium → Niobium → Yttrium
ManufacturedAlloys Salvaged Alloys → Galvanising Alloys → Phase Alloys →
                   Proto Light Alloys → Proto Radiolic Alloys
```

**Done when** every material in `MaterialGrades.g.cs` lands in exactly one line, and no line holds
two materials at the same grade. Finding to `docs/spikes/`.

> **Done, 2026-08-15 — [`material-lines.md`](../spikes/material-lines.md).** It holds, and harder
> than asked: the derived lines price **1,096 of 1,096 real trades** correctly. Three corrections to
> what is written above. There are **23** lines, not 32 — `MaterialGroupType` declares 32 members but
> nine group Guardian and Thargoid materials the trader does not deal in, so those have a *group* and
> not a line and the arithmetic must decline rather than guess. There is **more than one source**:
> FDevIDs `material.csv` has carried the line in its `category` column since 2021, in complete
> agreement with EDDiscovery, so this was never a single point of failure and Step 1 takes the line
> from there with EDDiscovery as the pinned cross-check. And two `Add` overloads in `MCMRType.cs`
> take no group at all, so an empty `line` must fail the run.

---

# Steps 1–5 — Tables and rules

Established shape throughout: a Python generator under `tools/`, output committed as TSV, embedded
via `D47.Core.csproj`, read on first use through `Lazy<T>`, header recording sources, counts, **the
upstream commit SHA and the build date**.

## Step 1 — `Materials.tsv`

`tools/gen-materials.py`, plus `MaterialCatalogue.cs` in Core reading it lazily, mirroring
[`EliteSpecifications.cs`](../../src/D47.Core/Knowledge/EliteSpecifications.cs).

Columns: `symbol, name, ledger, category, grade, line, origins`.

- **Key on the FDevIDs `symbol`** the journal writes; join EDEngineer on display name. This is the
  trap that silently loses 22 of 45 Encoded materials, plus four named aliases
  (`Ballistic`/`Ballistics Data` and three more). Fail loudly on anything unresolved.
- **`ledger`** — material / ship-locker / cargo / rare-cargo, from which FDevIDs file the symbol
  appears in. Gold ×200 is two hundred tonnes of cargo and must never be totalled against a
  300-unit material cap.
- **`line`** from FDevIDs `material.csv` `category`, per Step 0, cross-checked against EDDiscovery.
- **`origins`** from EDEngineer `entryData.json` `OriginDetails` — 365 of 371 entries, 77 strings.

**Verified by:** generator run, not mocked. All 22 drifting Encoded symbols resolve, the aliases
resolve, zero unresolved, no `ledger` bucket empty, every line populated.

> **Done, 2026-08-15.** `tools/gen-materials.py` → `Materials.tsv` (731 rows: material 137,
> ship-locker 196, cargo 256, rare-cargo 142), `MaterialCatalogue.cs`, 12 tests against the shipped
> table. Two things the plan did not know. **Five aliases, not four** — `Ship System Data` →
> `Ship Systems Data` joins the known ones, and each is asserted to be *drift* (the name in exactly
> one source, its counterpart in exactly the other) rather than matched on similarity. **Three
> near-misses are deliberately not aliased**: `Geographical Data` is not `Geological Data`,
> `Mineral Analytics` is not `Mining Analytics`, `Security Plans` is not `Settlement Defence Plans`
> — both spellings exist in EDEngineer, so these are distinct items FDevIDs has no symbol for, and
> aliasing would hang one item's origins on a different real one. They go to a
> `[known-but-unkeyed]` section with the two Thargoid data types FDevIDs lacks.

## Step 2 — `EngineeringRules.cs`

Fixed game rules about a handful of numbers, hand-written beside the generated tables so
**regenerating cannot disturb them** — the split `MaterialGrades.CapacityOfGrade` already makes.

- `ProgressPerRoll(grade, rank)` — the published table, with 34% represented as **`1/3`**, which is
  what the game applies.
- `RollsFor(grade, rank)` — 5,4,3,2,1 on `rank − grade`; **null when `rank < grade`**, a hard gate
  rather than a slow path.
- `CompleteAt = 0.85` — doc comment carrying the sample size and that it is the **lowest observed**
  completion, not a proven threshold.
- `RollsRemaining(grade, rank, quality)` — 0 at or above `CompleteAt`, else `ceil((1−quality)/step)`.
- `ReputationCost(grade)` — 500k / 2M / 8M / 16M. `ReferralGrade = 3`.
- `TradeRate(from, to, sameLine)`, and a companion flagging trades whose input exceeds
  `MaterialGrades.CapacityOfGrade` — **defined and physically impossible**.

**Verified by:** assertions against the corpus's own numbers — grade 5 admits only a 0.2 step, three
⅓ rolls reach 0.99 and count as complete, `rank < grade` is unreachable.

> **Done, 2026-08-15.** `EngineeringRules.cs` beside the generated tables, 38 tests. Two things
> worth recording. `TradeRate` returns the exchange **in lowest terms** — the published rule quotes
> a cross-line trade one grade down as 6 for 3, but 2 for 1 is the same exchange and is what a
> Commander wanting one unit should hand over; quoting the unreduced form sends them to gather
> three times what they need. And the capacity companion finds **five** impossible combinations,
> not the one the plan implies: g1→g4 and g1→g5 cross-line, g1→g5 same-line, g2→g5 cross-line, and
> g3→g5 cross-line at 216 against a cap of 200. Checked end to end outside the suite: the rate
> logic prices **1,096 of 1,096 real trades** correctly when fed line and grade from the shipped
> `Materials.tsv`, which tests Steps 1 and 2 together against the corpus.

## Step 3 — Stop dropping the `Engineering` block

*Today d47 reads three fields out of a module's engineering and throws four away, which is why "how
good is my roll" cannot be asked.*

[`ShipLoadout.cs`](../../src/D47.Core/Journal/ShipLoadout.cs) — `ShipModule` gains `Quality`,
`Engineer`, `EngineerId` and `Modifiers` (label, value, original value, less-is-good). `Modifiers`
is the only place this module's actual roll exists in real units.

Gotchas taken from EDDiscovery's reference implementation rather than found the hard way: the
pre-3.0 `Blueprint` spelling, `ExperimentalEffect` vs `ApplyExperimentalEffect`, an
`ExperimentalEffect` present but empty, and `Value` arriving as the wrong JSON type.

**There is no fixture anywhere with an `Engineering` block.** Add several from the corpus, including
the pre-3.0 form and an empty experimental.

**Independently useful** — this answers "how good is my roll" with no table at all.

> **Done, 2026-08-15.** `ShipModifier` and four fields on `ShipModule`, 11 tests. Shapes measured
> over 772 engineered modules in 78 real Loadout events, and the parser then run over all of them:
> **3,384 modifiers, 3,368 numeric and 16 text, none dropped.**
>
> Three of the four listed gotchas needed correcting, and two more turned up.
> **`LessIsGood` is `0`/`1`, not a JSON boolean** — read as one it answers false every time and
> reports every improvement on a less-is-better figure backwards. That is the bug in this step, and
> it was not on the list. **`Value` arriving as the wrong type is really a different variant**: 16
> of 3,384 modifiers carry `ValueStr`/`ValueStr_Localised` and no `Value`, `OriginalValue` or
> `LessIsGood` — a damage type rather than a quantity. And **`Engineer` can be absent while
> `EngineerID` is present**: 27 of 772, every one id `399999` on a grade 5
> `CargoRack_IncreasedCapacity`. That is a module that arrived already engineered, so it is direct
> ship-side evidence for **Step 13** — which no longer has to start from nothing.
>
> The other two are carried on EDDiscovery's authority and **cannot be evidenced**: the pre-3.0
> `Blueprint` spelling and an empty `ExperimentalEffect` have **zero occurrences** in a corpus that
> begins in July 2025, seven years after the spelling changed. Both are one fallback each and the
> tests say plainly that they were not measured.

## Step 4 — `Blueprints.tsv`

`tools/gen-blueprints.py`, plus `BlueprintCatalogue.cs`. Per-application ingredients, grade,
engineers, effects, experimentals.

- **EDEngineer is the authority on experimentals** — 11 of 62 have a different recipe per module
  type, which coriolis models wrongly rather than staledly.
- Cross-check against coriolis on `CoriolisGuid`; **print the 27 blueprint and 11 experimental
  disagreements** rather than resolving them silently.
- Repair EDEngineer's double-encoded `Effects` strings (`âœ“`) in the generator, never at runtime.

**Verified by:** every ingredient resolves to a material row; **every ingredient size is 1**; the
disagreement list is printed.

> **Done, 2026-08-15.** `tools/gen-blueprints.py` → `Blueprints.tsv` (1,172 rows),
> `BlueprintCatalogue.cs`, 11 tests. **All 3,396 ingredient references resolve, zero unresolved**,
> and the cross-check reproduces the research exactly: 688 blueprints and 59 experimentals joined
> on `CoriolisGuid`, **27 and 11 disagreements**, printed and never resolved.
>
> Two things above are wrong. **"Every ingredient size is 1" is true of modifications and of
> nothing else** — 57 graded ship rows break it and every one is synthesis: munitions, refills,
> limpets, SRV repair. Hence a `kind` column, told apart by EDEngineer's `@`-prefixed
> pseudo-engineers rather than by a list of type names, and the size assertion scoped to
> modifications. The distinction is load-bearing: `TotalFor` returns null for anything else,
> because multiplying a synthesis recipe by a roll count is arithmetic on the wrong kind of thing.
>
> And **the `Effects` strings are not double-encoded.** The file carries 56 real U+2713 ticks and
> reads cleanly as UTF-8; `âœ“` is exactly what those correct bytes look like decoded as cp1252.
> The defect was in the reading, so the generator applies no repair — only the right encoding.
> That is the fourth time a fact about the reader has been written down as a fact about the data.
>
> The five drifting display names now live in `tools/edengineer_names.py`, shared with
> `gen-materials.py`: this generator failed on 22 ingredient references until it used them, which
> is the same 22 the research measured, and a drift repaired in one generator and not the other
> loses rows in silence.

## Step 5 — `Engineers.tsv` gains the chain

Extend `tools/gen-engineers.py`. New columns: `referred_by`, `referral_grade`, `body`, `discovery`,
`meeting`, `unlock`, `reputation`.

- Parse EDDiscovery `Items/Engineers.cs` — **C# source, not data** — by regex over the
  `new EngineeringInfo(...)` initialisers with a **hard assertion of 38 rows**. Proven in research; a
  shape change fails the run rather than half-populating a table.
- **Bill Turner is an explicit commented override**: EDDiscovery says "common knowledge", the wiki
  and a journal trace both say Selene Jean.
- Stop the `NOT_PEOPLE` filter discarding `@Merchant` rows — they are Phase 19's entire on-foot
  vocabulary. Keep them out of the *directory*, but emit them.

> **Done, 2026-08-15.** All 38 initialisers parse, the assertion holds, and **27 of 38 engineers
> are reached through somebody else**. Bill Turner's override is in the generator and asserted in a
> test so it cannot quietly stop applying. Columns as listed, except the pre-existing `unlock`
> column — the material tribute — is renamed `tribute`, so EDDiscovery's unlock prose can have the
> name the plan gave it without two columns meaning different things.
>
> Two things the plan did not know. **The on-foot chain states no grade**: ship referrals read
> "From Hera Tani (grade 3-4)" and Odyssey ones read "From Domino Green" and nothing more, because
> those unlock on a count of modifications. Seven referrals would have been lost by a regex
> requiring the grade, and defaulting them to 3 would state a requirement the game does not have —
> so `ReferralGrade` is null there. And **one engineer has three referrers**: Yi Shen, reached
> through any of Baltanos, Eleanor Bresa or Rosa Dayette, so `ReferredBy` is a list.
>
> **The `@Merchant` change is not needed and was not made.** Those 56 rows are Phase 19's on-foot
> vocabulary and Step 4's `Blueprints.tsv` already carries every one of them as `kind=merchant`
> with full recipes — which did not exist when this was written. Emitting them here as well would
> put pseudo-people into a directory of people to duplicate a table that already has them.

**Checkpoint.** Everything to here is data and pure functions; nothing is exposed yet.

---

# Steps 6–10 — The surface

Deliberately small: tools sit in prompt position 1 and every one costs cache.

## Step 6 — New `engineering` capability

With `docs/capabilities/engineering.md`, which the documentation gate requires and which must quote
the live schema.

- `get_blueprint` — a blueprint *or* a module kind (one tool, two parameters, the `find_engineer`
  pattern). What it does, per-application ingredients, who offers it and to what grade, the
  experimentals for that module type, and — folding `EngineerProgress.Rank` — **the exact roll count
  and exact total**, or the rank block *with its credit price*.
- `get_module_engineering` — for a slot or module on the live `Loadout`: blueprint, grade,
  experimental, engineer, modifiers in real units, and progress. **`Level` states the grade; the
  0.85 band decides finished; rolls-remaining is quoted only below it.**

**Checkpoint** — first point the whole loop is exercised end to end.

> **Done, 2026-08-15.** `EngineeringCapability.cs`, `docs/capabilities/engineering.md` and 19 tests
> against the real shipped tables. Then run end to end over the corpus, which is what a checkpoint
> is for: **2,196 real `Loadout` events, 20,526 engineered modules described, zero failures**, and
> all **259** blueprint names in the table asked for and answered with **zero** unresolved
> ingredient references.
>
> **The plan assumed `get_module_engineering` could name the blueprint, and it cannot.** The journal
> writes it as a symbol — `FSD_LongRange` — and never localises it: every key of every `Engineering`
> block was enumerated across the corpus, 20,526 in `Loadout` and 6,272 in `EngineerCraft`, and
> there is `BlueprintName`, `BlueprintID` and nothing readable. The experimental effect *does* carry
> `_Localised`, on all 13,660 that have one, which is why the effect reads properly and the
> blueprint does not. Three sources were checked for the join and **each fails differently**:
> EDEngineer carries seven fields per recipe and no symbol; coriolis-data keys on the symbol but the
> shared-guid join reaches only **31 of the 35** symbols the corpus contains, missing
> `Misc_LightWeight`, the commonest of all at 2,325 uses; EDDiscovery's `RecipesEngineering.cs`
> reaches **35 of 35** but its display names agree with the table's on only **15 of 35** — it says
> "Heavy Duty Armour" where the table says "Heavy Duty", so adopting them would have one tool name a
> blueprint the other cannot find. So the symbol goes out with its underscores removed, which is
> what `ModuleStore` already does to the same family of symbol, and **no partly-populated `symbol`
> column was added to a shipped table**. If a later step wants real names, EDDiscovery is the source
> and the reconciliation against EDEngineer's vocabulary is the work.
>
> Two smaller things. **The rank paragraph picks the highest-ranked engineer who offers the top
> grade**, and where nobody unlocked can reach it an outstanding invitation is named instead — a
> "nobody" answer with an invitation sitting unused would be wrong about what the next step is. And
> `EliteSpecifications` **has no armour rows at all**, 0 of 982, so 1,725 of the 20,526 engineered
> modules fall back to their symbol; all 17 distinct symbols are `<ship>_armour_<grade>` bulkheads.
> That is a Phase 7 table gap this step surfaced rather than caused.
>
> > **Closed the same day**, on a branch of its own: armour is filed per hull rather than under
> > `modules/`, so the generator never walked it, and FDevIDs had all 241 rows in the `ship` column
> > it was already reading. Re-measured against the same corpus afterwards: **0 of 20,526 fall back
> > now**, and `mandalay_armour_grade1` reads as "Mandalay Lightweight Alloy". The same branch
> > retired "Cargo Rack (cargorack)", a qualifier that restated the name it qualified, so the
> > example output on the capability page was refreshed with it.

## Step 7 — `engineers` gains the chain

`find_engineer` reports who refers them and at what grade, where the Commander stands on that path,
and the reputation price of a rank they lack. Retire the docstrings that still describe the chain as
observed-only.

> **Done, 2026-08-15.** `find_engineer` carries the chain, 9 new tests. The observed-only wording is
> retired in three places, not one: the capability's own docstring, the "what is missing, and why"
> section of `docs/capabilities/engineers.md`, and the already-ticked *Engineers* item in `list.md`,
> which still said the chain was not shipped.
>
> Three things the plan did not say. **Several referrers mean any of them**, so the Commander's
> *best* standing among the three decides Yi Shen's answer rather than all three being reported as
> requirements — a wall where there is a door. **The referrer's standing is a separate sentence from
> the engineer's**, and both had to name who they were about: two consecutive "has not met them"
> lines read as one repeated. And the prose columns Step 5 generated but nothing consumed —
> `meeting`, `unlock`, `reputation`, `body` — are the rest of the answer to "how do I get to this
> person", so they go out here; that is the difference between naming a referrer and telling a
> Commander what to actually do.
>
> One defect fixed in passing, from another step's table. Odyssey suit and weapon specialities carry
> a grade of **0** because those blueprints are ungraded in the game, and nine of 38 engineers read
> "Grades: Suit to 0, Weapon to 0" — a defect on the face of it rather than a fact. `Speciality`
> gains `IsGraded` with the zero documented as "no grade stated", and the two reporting sites name
> the speciality without a grade. The table is untouched.

## Step 8 — `galaxy` gains `state`

One new `GalaxyFilters` row. It is what makes grade-5 Manufactured sourcing derivable end to end,
and it unblocks Step 9.

> **Done, 2026-08-15.** The row is `state`, 21 values, 3 new tests — and **it is not sent under
> that name**. There is a real field called `state`: it has its own `field_values` list carrying
> exactly those 21 words, and the service honours it rather than dropping it. It also matches
> nothing — **0 systems for every value including `None`**, measured within 200 ly of Sol, where a
> bogus key returns the unfiltered count and no result row carries a `state` field at all. The
> field that works is `controlling_minor_faction_state`: 1,286 in Boom, 330 in War, 68 in Outbreak.
> **That is a worse trap than the silent-ignore this vocabulary was built for**, because it fails as
> an *empty* answer rather than a wrong one, and "no systems in Boom near you" reads as a fact about
> the galaxy. So `GalaxyFilter` gained a `Field`, and d47 offers the short word while sending the
> long key.
>
> Two things fell out of doing it. **The first attempt was a `Choice(name, field, params string[])`
> overload, and it silently captured the first choice of every existing filter as its field name** —
> allegiance would have gone out under the key "Alliance", which the service ignores, which is the
> exact failure this class exists to prevent. `SpanshRequestTests` caught it before it left the
> working tree; the factory is now a differently shaped call that cannot be captured by accident.
>
> And **the tool-profile relief valve opened**, which is the first time it has. The `srv` profile
> reached 24,470 characters against a 24,000 limit, so it degraded and dropped the Commander's
> ability to act. The fix was not the limit: `search_systems` was listing every filter's whole
> vocabulary in its description *and* again as schema enums, so the vocabulary was being paid for
> twice in prompt position 1. The description now names the filters and lets each parameter carry
> its own values — `Describe()` stays for tool *results*, which are not cached. Every profile is
> smaller than before this step despite gaining a filter, and `srv` sits at 23,843. **That is 157
> characters of headroom, and Steps 9 and 10 add three tools**, so the surface will trip the valve
> again before the phase closes.

## Step 9 — Sourcing

- `find_material` — origins from the table; state-gated origins become a system search on `state`,
  worded as "systems **reported** in Boom" per the existing crowd-report framing; raw materials via
  the `find_body` materials **group** filter, **ranked locally by share** and saying what it ranked
  over. **Rhenium, Lead and Boron are declined by name** — absent from the index, and a search
  returning nothing reads as "there is none near you".
- `find_material_trader` — the published location rule (Refinery/Extraction,
  Extraction/Industrial, High Tech/Military; medium-or-high security; population 1–22M; not anarchy)
  as a filtered search plus the `services` group filter. `MaterialTrade.TraderType` overrides per
  market id, so the heuristic only answers about stations nobody has visited.
- Shortfall netting through `TradeRate` — **never across ledgers, never across types**, impossible
  trades named as such.

## Step 10 — Web search

The phase's last open item, pulled in because it closes Phase 14 and because the build conversation
is weaker without it.

A tool the model calls when it decides it needs to, or when the Commander asks. **The rule is that a
result is a sentence in the turn, spoken as a search result, and never a row written into a shipped
table** — that boundary is the whole point, and it is what keeps the generated tables trustworthy.
Untrusted input like every other outside source: it must not be able to reach a protected setting.

Its own settings row and `EgressDisclosure` entry, computed rather than hand-written.

---

# Steps 11–13 — Independent tails

## Step 11 — Speak up

Arriving at an engineer, or picking up the last unit a plan needed, are moments rather than
questions. The callout engine already reads `StationServices` for engineering.

## Step 12 — Community goals

Two halves, and **the split is not the one the item assumed** — see
[`docs/spikes/community-goals.md`](../spikes/community-goals.md).

- **Journal half, no key.** One `CommunityGoal` event carries the whole board: `TierReached`,
  `TopTier`, `PlayerPercentileBand`, `NumContributors`, `CurrentTotal`, `Bonus`.
- **Filter on `Expiry` against injected time.** The array is a board snapshot, not a list of live
  goals — the corpus has one reported four days after it expired, `IsComplete: true`.
- **INARA half** behind a key, with a settings row, an `EgressDisclosure` entry and untrusted-input
  handling. With no key, report what the journal knows and say plainly it is only what the Commander
  has seen. Capabilities are state, not guards (Phase 3), so this must not dead-end.

Shares nothing with the rest but the capability pattern — can be done first, last or alone.

## Step 13 — The Operations spike

`list.md`'s pre-engineered spike can invalidate the "unmodified → blueprint → grade" premise. **The
corpus can now answer it**: `BuySuit`/`BuyWeapon` carry mod arrays at point of sale (all empty across
912 journals), and the ship-side equivalent is a module purchase carrying an `Engineering` block.
Finding to `docs/spikes/`.

---

## Verification

- `dotnet build` (warnings are errors) and `dotnet test` — including `CoreDependencyTests` and
  `DocumentationGateTests`, which will demand the new capability pages quote their real schemas.
- **Generators are run, not mocked**, and print join failures and applied aliases. A run that
  resolves everything silently is the suspicious one.
- Tests against the **real shipped tables**, as `SpecificationTests` and `EngineerTests` already do.
- `EngineeringRules` re-checked against the corpus extract. The extract is a Commander's own play
  history and **stays out of the repo**; pull it from `cooler` per
  [`journal-corpus-engineering.md`](../spikes/journal-corpus-engineering.md) §7.

## Order and dependencies

Step 0 gates Step 1. Steps 1, 2 and 3 are mutually independent — 3 is the shortest path to something
working. Step 4 needs 1. Step 5 needs nothing but touches the same generator as 1. Step 6 needs 1–4;
Step 7 needs 5; Step 9 needs 1, 2 and 8. Steps 10–13 are independent tails.

**Release:** all thirteen steps close Phase 14, so the tag is **`v0.10.0`** — after
`dotnet test -c Release` passes, because a failed release run leaves a published tag with no Release
behind it and costs a version number to correct.
