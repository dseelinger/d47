# What 6,272 real engineering rolls say

**Measured 2026-08-15** against a corpus of **912 journals, 373 MB, 3 July 2025 to 11 August 2026**,
nine Commanders, read over SSH from a second machine. Everything below is counted from those files.

This page exists because three separate items were blocked on the same sentence — *there is no
journal here with engineering in it* — and that sentence was true of one laptop rather than of the
world. The corpus had been reached once before, for the NPC-comms measurement in list.md Phase 15,
and nobody connected the two.

**What it settles:** what `Engineering.Quality` means, when the game considers a grade finished,
whether the published roll table is real, whether the material trade rate is a pure function of
grade, and which of two sources is right about Bill Turner. **What it does not settle** is anything
on-foot beyond the four events below — the on-foot half of this corpus is thin.

| Event | Count |
|---|---|
| `EngineerCraft` | 6,272 |
| `EngineerProgress` | 1,455 — **1,189 snapshots, 266 single-engineer deltas** |
| `MaterialTrade` | 1,096 |
| `SuitLoadout` | 768 |
| `EngineerContribution` | 45 |
| `UpgradeWeapon` / `BuyWeapon` / `BuySuit` / `UpgradeSuit` | 12 / 11 / 10 / 4 |

---

## 1. `Quality` is a cumulative fill, and the grade completes below 1.0

The first half was the open question. The second half is the one that would have shipped a bug.

**Cumulative, without exception.** Grouping crafts into runs on one slot, module, blueprint and
grade: **1,609 of 1,609 multi-roll runs have non-decreasing Quality**. Not one run steps backwards,
which a per-roll draw would do constantly.

**But a grade does not have to reach 1.0.** Regrouping over the whole history — so an interrupted
grind is one group rather than two — gives 994 groups, of which 926 reach exactly `1.0`. Of the 68
that do not:

- **45 were followed by rolls at the next grade**, so the game let the Commander move on. Every one
  sat at **0.85, 0.95 or 0.99**.
- **23 had nothing follow** — abandoned grinds. Every one sat at **0.8 or below**, mostly 0.2 to 0.6.

No group between 0.8 and 0.85 falls on either side, so the observed boundary is clean:

> **0.85 and above is finished. 0.8 and below is in progress.**

**The mechanism, for the 0.99 cases at least.** Frontier's published table says 34% per roll at that
tier. The game applies **1/3**: the observed deltas are `0.333` (428 times) and `0.334` (190),
never `0.34`. Three rolls therefore sum to `0.99`, and the completion check tolerates the shortfall
its own rounding creates. The `0.85` cases do not decompose as cleanly and no single mechanism is
claimed for all 45.

**Why this matters more than the arithmetic.** Gating "done" on `Quality == 1.0` produces exactly the
failure list.md Phase 17 warns about — *a module the Commander can see is finished and d47 will not
call finished* — and it would have looked correct in testing, because 93% of grades do reach 1.0.
The bug lives in the last 7%.

So: **`Level` says which grade a module has**, and that is the fact worth asserting. `Quality` is
progress within that grade, and 0.85 is the **lowest observed completion** rather than a proven
threshold — it should be carried with its sample size so a later corpus can move it.

## 2. The roll table is real, and step size is the evidence

Roll *counts* are not reliably measurable here. Grouping by consecutive crafts splits an interrupted
grind; grouping over the whole history merges separate grinds, because a slot, module, blueprint and
grade key is reused every time a Commander re-outfits the same ship. Both were computed and they
disagree, which is the honest reason neither number is quoted.

**Step size is immune to both**, because it is measured between adjacent rolls within a group:

| Blueprint grade | Deltas observed |
|---|---|
| Grade 5 | **`0.2` only — 1,428 of 1,428** |
| Grade 4 | `0.25` (1,048), `0.2` (76), `0.15` (11, the clamp) |
| Grade 3 | `0.333`/`0.334`/`0.33`/`0.335` (685), `0.25` (52), `0.2` (76) |
| Grade 2 | `0.5` (339), `0.25` (56), `0.2` (76) |
| Grade 1 | `0.25` (55), `0.2` (73), `0.333` (6) — mostly a single roll, so mostly no delta at all |

Read against the published table indexed on `access − grade`, every one of these is a cell in it, and
nothing appears that is not.

**Grade 5 is the strongest result on the page.** It admits exactly one step, `0.2`, across 1,428
deltas — which is what "grade *N* is unreachable below access level *N*" predicts, and what nothing
else does. A grade 5 blueprint rollable at access 4 would show a smaller step somewhere in 1,428
samples, and it does not.

**The last roll clamps.** Deltas of `0.15` and `0.1` appear only as the final step of a run. So the
rolls a *part-finished* module needs is a function of the remaining fill, not of the grade — and
above 0.85 the answer is none.

## 3. The material trade rate is a pure function of grade delta and line

1,096 trades, every material name resolving against the shipped `MaterialGrades.g.cs`, **zero
unresolved**. Trader types: manufactured 523, encoded 368, raw 205.

**No trade crosses Raw / Manufactured / Encoded. Not one in 1,096.** That retires a row that had been
carried as "believed impossible, and believing is not knowing".

Ratios, as paid : received:

| Grade delta | Same line | Different line |
|---|---|---|
| −4 | **1 → 81** | 6 → 81 |
| −3 | **1 → 27** | 6 → 27 |
| −2 | **1 → 9** | 6 → 9 |
| −1 | **1 → 3** | 6 → 3 |
| 0 | — | **6 → 1** |
| +1 | **6 → 1** | 36 → 1 |
| +2 | **36 → 1** | 216 → 1 |
| +3 | **216 → 1** | — |

Exactly the published rule: one grade down returns 1→3, one grade up costs 6→1, a different
line costs another factor of 6, and combinations multiply.

**And it exposes a trap in how that rule is worded.** The published wording says a *different
category* costs the extra 6→1, and "category" in the journal means Raw, Manufactured or Encoded —
but no trade ever crosses those. The 6× actually applies across **material lines** within one type:
the seven Raw lines, the six Encoded ones, and so on. Reading "category" as the journal's `Category`
field would compute the wrong rate for the commonest trade there is, and be confidently wrong about
it.

So **`line` is load-bearing rather than decorative** in the `Materials.tsv` column list, and a table
without it cannot do trade arithmetic at all. A same-grade trade is always cross-line — within one
line a grade names one material — which is why grade delta 0 has no same-line column.

### What a line actually is

Defined here because the rest of this repo refers to the column without saying what it holds.

**A line is one column of the material trader's grid: one material per grade.** There are **23** of
them, sitting inside the three types.

> **Corrected 2026-08-15 by [material-lines.md](material-lines.md).** This section said there were
> 32, from EDDiscovery's `MaterialGroupType`, "the only permissive source found that carries the
> grouping at all". Both halves were wrong. `MaterialGroupType` declares 32 members but only 23 are
> columns of the grid — the other nine group Guardian and Thargoid materials, which the trader does
> not deal in and which no trade in this corpus touches. And FDevIDs `material.csv` has carried the
> line since January 2021 in a `category` column beside the `rarity` this repo already reads, in
> complete agreement with EDDiscovery. Left visible rather than silently edited, because the way it
> was got wrong is the point.

```text
RawCategory1         Carbon → Vanadium → Niobium → Yttrium
RawCategory4         Iron → Zinc → Tin → Selenium
EncodedEmissionData  Exceptional Scrambled Emission Data → Irregular Emission Data →
                     Unexpected Emission Data → Decoded Emission Data →
                     Abnormal Compact Emissions Data
ManufacturedAlloys   Salvaged Alloys → Galvanising Alloys → Phase Alloys →
                     Proto Light Alloys → Proto Radiolic Alloys
```

Trading up and down **within** a line is the cheap move. Crossing to another line costs the extra
6×, whether or not the type changes — and the type never changes, because a trader deals in one.

**The journal cannot supply this.** Its `Category` field names the type, so Iron for Vanadium reads
as "same category" and prices as free when it is in fact a 6× cross-line trade. That is the
commonest trade there is, so a table without the column is not merely less capable — it is
confidently wrong about most of what it is asked.

## 4. Bill Turner: the journal decides a cross-source conflict

EDDiscovery records Bill Turner's discovery as *"Common knowledge"*; the Fandom engineer table
records *"Learned from Selene Jean"*. One Commander's progress trace separates them:

```text
Selene Jean   2025-10-10 22:02:58  rank 2
Selene Jean   2025-10-10 22:03:17  rank 3
Bill Turner   2025-10-10 22:03:28  Invited      <—
Selene Jean   2025-10-10 22:03:36  rank 4
```

The invitation lands **eleven seconds after Selene Jean reaches rank 3 and before she reaches
rank 4** — precisely the threshold the wiki's own footnote states, *grade 3 access plus approximately
half the progress bar to grade 4*. Didi Vatermann, her other documented referral, appears the same
evening.

**The wiki is right and EDDiscovery is wrong on that one cell**, which makes the referral graph 38
for 38 once corrected. Stated with its limit: this is one Commander's trace, and the rank steps are
nineteen seconds apart, so it is strong evidence rather than proof. It is also the only source that
cannot be wrong about it, and it agrees with the documented mechanism rather than merely with the
other book.

## 5. Two things confirmed that were already believed

**The two shapes of `EngineerProgress` are real** — 1,189 events carrying an `Engineers` array
against 266 naming a single engineer. That is the distinction list.md Phase 14 says would wipe
thirty-seven engineers the first time somebody ranked up, and it holds at scale.

**The shipped engineer table is validated.** Every engineer name the corpus contains is in
`Engineers.tsv`, and there are **no names in the journals that the table does not have**. Four of its
38 never appear — Oden Geiger, Uma Laszlo, Wellington Beck and Yi Shen, the deep end of the on-foot
chain plus the Colonia convergence — which is a gap in the corpus rather than in the table.

## 6. On-foot, which is thin but not empty

> **Superseded and enlarged on 2026-08-16 by
> [journal-corpus-on-foot.md](journal-corpus-on-foot.md).** Everything below still holds. What this
> section got wrong is its scope: "thin" was true of the *events* and false of the *answers*. The
> same corpus settles the locker cap, the barter rate, the credit cost of an upgrade — and that
> EDEngineer's on-foot recipes are stale by factors of two and three, which is the one thing Phase 20
> was most confident about.

Four events, and three of them answer a question that was open:

- **`SuitName` encodes the grade.** `explorationsuit_class1` / "Artemis Suit", across 768
  `SuitLoadout` events.
- **Pioneer Supplies charges credits as well as materials.** An `UpgradeSuit` to class 4 cost
  **4,500,000 cr** plus five resources.
- **Mods are in the journal.** `SuitLoadout` carries `SuitMods` and a per-module `WeaponMods` array,
  so what is fitted can be observed without any table. `BuySuit` and `BuyWeapon` carry the same
  arrays at point of sale, which is the shape a pre-engineered purchase would show — every one in
  this corpus is empty, so nothing here is pre-engineered, but the probe now knows what to look for.

**And a trap worth catching before it is spoken aloud.** `UpgradeSuit` on `utilitysuit_class3`
returned `"Name_Localised": "$UtilitySuit_Class1_Name;"` — an unresolved token, naming **Class1** for
a class 4 suit. Frontier's own localisation is broken here. `Name` and the separate `Class` field are
the truth; anything reading the localised string would say the wrong grade or read a raw symbol out.

## 7. How to re-measure

The corpus is not in this repository and must not be — it is a Commander's own play history. What is
reproducible is the recipe.

- Journals live at `%USERPROFILE%\Saved Games\Frontier Developments\Elite Dangerous\Journal.*.log`,
  one JSON object per line.
- Filter to the events in the table at the top. `Loadout` is deliberately excluded: it is most of the
  bulk and its `Engineering` block is a subset of what `EngineerCraft` already carries.
- Group crafts by `(Slot, Module, BlueprintName, Level)` and read Quality **deltas**, not counts.
  Both groupings are wrong about counts in opposite directions, per §2.
- `Category` on a `MaterialTrade` is the Raw/Manufactured/Encoded type, **not** the material line.
  The line has to come from a table; the rate cannot be derived without it.
- Note that `JournalSpine` tails only the newest file, so anything historical needs a deliberate
  backfill rather than a folder to point at.
