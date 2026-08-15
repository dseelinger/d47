# Phase 14 `#102` — engineering, end to end

## Context

Two `list.md` items carry `#102`: **Know what engineering actually does** (the facts) and **Go and
get it** (the sourcing). Both were written around uncertainty that no longer exists.

Three things changed on 2026-08-15, all now committed in `7e2f3ac`:

- **6,272 real engineering rolls** settled what `Engineering.Quality` means, that a grade completes
  at **0.85 rather than 1.0**, and that the published trade rate holds exactly across 1,096 trades.
- **EDDiscovery/EliteDangerousCore** (Apache-2.0) turned up via edcodex.info carrying the **referral
  graph**, and a `MaterialGroupType` that is the material **line** the trade rate depends on.
- The trade rule's published wording hides a trap: its extra 6× applies across material *lines*, not
  the journal's Raw/Manufactured/Encoded `Category` — which nothing ever crosses.

So this plan is **less hedged and more capable** than the items it implements. It quotes totals
instead of floors, prices a rank block instead of shrugging at it, and ships the referral chain the
*Engineers* item had to leave out.

**Out of scope, said plainly rather than quietly dropped:** Phase 14's *Web Search* — its own
capability, and a sequencing dependency for the **conversation** about a build rather than for these
tables. Say the word and it joins.

---

## Stage 1 — Three generated tables

Established shape throughout: a Python generator under `tools/`, output committed as TSV, embedded
via `D47.Core.csproj`, read on first use through `Lazy<T>`, header recording sources, counts, **and
the upstream commit SHA plus build date** (the habit `docs/spikes/README.md` asks for).

**`tools/gen-materials.py` → `src/D47.Core/Knowledge/Materials.tsv`**
Columns per `list.md`: `symbol, name, ledger, category, grade, line, origins`.

- **Key on the FDevIDs `symbol`** the journal writes; join EDEngineer on display name. This is the
  trap that silently loses 22 of 45 Encoded materials, plus four named aliases
  (`Ballistic`/`Ballistics Data` and three more). Generator **fails loudly** on anything unresolved.
- **`ledger`** classifies into material / ship-locker / cargo / rare-cargo, from which FDevIDs file
  the symbol appears in. Load-bearing: Gold ×200 is two hundred tonnes of cargo and must never be
  totalled against a 300-unit material cap.
- **`line`** from EDDiscovery `Items/MCMRType.cs` `MaterialGroupType`. **A line is one column of the
  material trader's grid — one material per grade**, e.g. `Carbon → Vanadium → Niobium → Yttrium`,
  or `Salvaged Alloys → Galvanising Alloys → Phase Alloys → Proto Light Alloys → Proto Radiolic
  Alloys`. 32 of them, sitting inside the three types. Trading *within* a line is the cheap move;
  crossing lines costs another 6×. **The journal cannot tell you which line a material is in** — its
  `Category` names the type — so without this column every cross-line trade is priced as if it were
  free, and that is most trades. Nothing else priced can be built on top of a wrong rate.
- **`origins`** from EDEngineer `entryData.json` `OriginDetails` — 365 of 371 entries, 77 strings.

**`tools/gen-blueprints.py` → `src/D47.Core/Knowledge/Blueprints.tsv`**
Per-application ingredients, grade, engineers, effects, experimentals.

- **EDEngineer is the authority on experimentals** — 11 of 62 have a different recipe per module
  type, which coriolis models wrongly rather than staledly.
- Cross-check against coriolis on `CoriolisGuid`; **print the 27 blueprint and 11 experimental
  disagreements** rather than resolving them silently.
- Repair EDEngineer's double-encoded `Effects` strings (`âœ“`) in the generator, never at runtime.

**`tools/gen-engineers.py` — extend, and stop dropping rows**
New columns: `referred_by`, `referral_grade`, `body`, `discovery`, `meeting`, `unlock`,
`reputation`.

- Parse EDDiscovery `Items/Engineers.cs`. It is **C# source rather than data**, so: regex over the
  `new EngineeringInfo(...)` initialisers with a **hard assertion of 38 rows** — proven to work in
  this session's research; a shape change fails the run rather than half-populating a table.
- **Bill Turner is an explicit, commented override**: EDDiscovery says "common knowledge", the wiki
  and a journal trace both say Selene Jean. The override names the evidence.
- Also drop the `NOT_PEOPLE` filter's collateral damage — `@Merchant` rows are Phase 19's whole
  on-foot vocabulary and are currently discarded by one line. Keep them out of the *directory*, but
  emit them.

## Stage 2 — Fixed game rules, hand-written

`src/D47.Core/Knowledge/EngineeringRules.cs`. These are game rules about a handful of numbers, not
per-item data, so they live beside the generated tables and **regenerating cannot disturb them** —
the split `MaterialGrades.CapacityOfGrade` already makes and documents.

- `ProgressPerRoll(grade, rank)` — the published table, with 34% represented as **`1/3`** because
  that is what the game applies.
- `RollsFor(grade, rank)` — 5,4,3,2,1 on `rank − grade`; **null when `rank < grade`**, which is a
  hard gate rather than a slow path.
- `CompleteAt = 0.85` — doc comment carrying the sample size and that it is the **lowest observed**
  completion, not a proven threshold.
- `RollsRemaining(grade, rank, quality)` — 0 at or above `CompleteAt`, else `ceil((1−quality)/step)`.
- `ReputationCost(grade)` — 500k / 2M / 8M / 16M.
- `ReferralGrade = 3` — an engineer names their network at grade 3 plus half the bar to 4.
- `TradeRate(from, to, sameLine)` — and a companion that flags trades whose input exceeds
  `MaterialGrades.CapacityOfGrade`, i.e. **defined and physically impossible**.

## Stage 3 — Stop dropping the `Engineering` block

*What it is, in one line: today d47 reads three fields out of a module's engineering and throws the
other four away, which is why "how good is my roll" cannot be asked.*

`src/D47.Core/Journal/ShipLoadout.cs`. `ShipModule` currently keeps only blueprint, level and
experimental. Add `Quality`, `Engineer`, `EngineerId` and `Modifiers` (label, value, original value,
less-is-good) — `Modifiers` being the only place this module's actual roll exists in real units.

Gotchas taken from EDDiscovery's reference implementation rather than discovered the hard way: the
pre-3.0 `Blueprint` spelling, `ExperimentalEffect` vs `ApplyExperimentalEffect`, an
`ExperimentalEffect` that is present but empty, and `Value` arriving as the wrong JSON type.

**There is no fixture anywhere with an `Engineering` block.** Add several, drawn from the corpus.

## Stage 4 — The tool surface

Deliberately small: tools sit in prompt position 1 and every one costs cache.

**New capability `engineering`** — with `docs/capabilities/engineering.md`, which the documentation
gate requires and which must quote the live schema.

- `get_blueprint` — a blueprint *or* a module kind (one tool, two parameters, the `find_engineer`
  pattern). Says what it does, per-application ingredients, who offers it and to what grade, the
  experimentals available for that module type, and — folding `EngineerProgress.Rank` — **the exact
  roll count and exact total**, or the rank block *with its credit price*.
- `get_module_engineering` — for a slot or module on the live `Loadout`: blueprint, grade,
  experimental, engineer, the modifiers in real units, and progress. **`Level` states the grade;
  the 0.85 band decides finished; rolls-remaining is quoted only below it.**

**Extend `engineers`** — `find_engineer` gains the chain: who refers them and at what grade, where
the Commander stands on that path, and the reputation price of a rank they lack.

**Extend `galaxy`** — one new `GalaxyFilters` row, `state`, which is what makes grade-5 Manufactured
sourcing derivable end to end.

**Sourcing tools** (`Go and get it`), on the existing galaxy seam:

- `find_material` — origins from the table; state-gated origins become a system search on `state`,
  worded as "systems **reported** in Boom" per the existing crowd-report framing; raw materials via
  the `find_body` materials **group** filter, **ranked locally by share** and saying what it ranked
  over. **Rhenium, Lead and Boron are declined by name** — absent from the index, and a search that
  returns nothing reads as "there is none near you".
- `find_material_trader` — the published location rule (Refinery/Extraction, Extraction/Industrial,
  High Tech/Military; medium-or-high security; population 1–22M; not anarchy) as a filtered search
  plus the `services` group filter. `MaterialTrade.TraderType` overrides per market id, so the
  heuristic only ever answers about stations nobody has visited.
- Shortfall netting through `TradeRate` — **never across ledgers, never across types**, and
  impossible trades named as such.

## Stage 5 — Speak up

Arriving at an engineer, or picking up the last unit a plan needed, are moments rather than
questions. The callout engine already reads `StationServices` for engineering.

## Stage 6 — Fold in the Operations spike

`list.md`'s pre-engineered spike sits between the two `#102` items and can invalidate the
"unmodified → blueprint → grade" premise. **The corpus can now answer it**: `BuySuit`/`BuyWeapon`
carry mod arrays at point of sale (all empty in 912 journals), and the ship-side equivalent is a
module purchase carrying an `Engineering` block. Finding written to `docs/spikes/`.

## Stage 7 — Community goals

Two halves, and **the split is not the one the item assumes.** Measured against the corpus:
**13,341 `CommunityGoal` events**, ten distinct goals over thirteen months — and **952 of those goal
entries carry `PlayerContribution: 0`**. So the journal is not limited to goals the Commander
*joined*; it is limited to goals they **encountered**, because the event fires off the board at a
station they docked at. The line INARA has to cover is therefore "everywhere I haven't been", which
is wider than "everything I haven't joined" and is the honest framing for the setting row.

**The journal half is rich and needs no key.** One `CommunityGoal` event carries the whole board:
`CGID`, `Title`, `SystemName`, `MarketName`, `Expiry`, `IsComplete`, `CurrentTotal`,
`PlayerContribution`, `NumContributors`, `TopTier`, `TopRankSize`, `PlayerInTopRank`, `TierReached`,
`PlayerPercentileBand` and `Bonus`. Tier and percentile band are exactly what the item asks for, and
they are already on disk. `CommunityGoalJoin`, `CommunityGoalDiscard` and `CommunityGoalReward`
complete the picture.

**One trap, visible in the sample and easy to ship.** A goal dated `Expiry 2026-01-17` was still
being reported on **2026-01-21** with `IsComplete: true`. The array is a board snapshot, **not a
list of live goals**, so d47 must filter on `Expiry` against injected time rather than reading the
list back. Announcing a finished goal as current is a wrong answer that reads exactly like the
feature working.

**The INARA half** is the item's stated condition and stays that way: a key, a settings row, an
`EgressDisclosure` entry computed rather than hand-written, and the response treated as **untrusted
input** like every other outside source. With no key the capability reports what the journal knows
and says plainly that it is only what the Commander has seen — capabilities are state, not guards
(Phase 3), so this must not dead-end.

---

## Verification

- `dotnet build` (warnings are errors) and `dotnet test` — including `CoreDependencyTests` and
  `DocumentationGateTests`, which will demand the new capability page quote its real schema.
- **Generators are run, not mocked**, and print join failures and applied aliases. A run that
  resolves everything silently is the suspicious one.
- Tests against the **real shipped tables**, as `SpecificationTests` and `EngineerTests` already do:
  all 22 drifting Encoded symbols resolve; the four aliases resolve; every blueprint ingredient
  resolves to a material row; **every ingredient size is 1**; no `ledger` bucket is empty.
- `EngineeringRules` against the corpus's own numbers — grade 5 admits only a 0.2 step; three 1/3
  rolls reach 0.99 and count as complete; `rank < grade` is unreachable.
- **Re-run the corpus analysis** over the extract as a one-off check that the shipped rules agree
  with 6,272 observed rolls. The extract stays out of the repo.

## Sequencing

Stage 1 and 2 are independent of each other and both precede everything else. Stage 3 is independent
of both and unblocks "how good is my roll" on its own. Stage 4 needs 1–3. Stages 5, 6 and 7 are
independent tails: 5 and 6 can slip without stranding anything, and **7 shares nothing with the rest
of the plan but the capability pattern**, so it can be built first, last or by itself.

**Prove `line` early.** It is the one place the plan has a single point of failure: the material
lines exist in exactly one permissive source, they are parsed out of C#, and every priced answer in
Stage 4 rests on them. Stage 1's materials generator should be run and its 32 lines eyeballed
against the trader grid before anything is built on top.
