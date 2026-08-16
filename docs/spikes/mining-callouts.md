# Prospecting: what the limpet reports, and the grade that means something else

**Measured 2026-08-16** against the corpus used by
[journal-corpus-engineering.md](journal-corpus-engineering.md) and
[colonisation-sources.md](colonisation-sources.md): **912 journals, 1,633 `ProspectedAsteroid`
events**. Probe: [`spike/MiningProbe`](../../spike/MiningProbe).

This backs `list.md` Phase 18, *Prospector and core callouts*. It was not a planned spike — it is
what the item needed before a threshold could be chosen honestly, and it turned up one finding that
changes what ships.

---

## 1. The event, and how often it arrives

Every one of the 1,633 events carries `Materials`, `Content`, `Content_Localised` and `Remaining`.
Nothing is optional, so nothing needs a fallback. A rock holds **one, two or three materials and
never more** — 265, 424 and 944 respectively.

**A prospect arrives every 48 seconds at the median**, 22 at the tenth percentile. That is slow
enough for a spoken line per rock to be the feature rather than an annoyance, which is what the item
asks for — but fast enough that it needs its own settings row, because an hour of mining is roughly
seventy-five announcements.

**A core is 3 in 1,633 — 0.18%.** That asymmetry is why this ships as two callouts with two settings
rows rather than one: a Commander who finds the running commentary chatty must be able to silence it
without losing the rare announcement they are actually mining for.

```json
{ "timestamp":"2025-08-18T22:46:17Z", "event":"ProspectedAsteroid",
  "Materials":[ {"Name":"tritium","Proportion":23.450556},
                {"Name":"liquidoxygen","Name_Localised":"Liquid oxygen","Proportion":5.142477} ],
  "MotherlodeMaterial":"Alexandrite",
  "Content":"$AsteroidMaterialContent_Low;", "Content_Localised":"Material Content: Low",
  "Remaining":100.0 }
```

**`MotherlodeMaterial_Localised` was present on one of the three**, so it cannot be relied on. The
raw `MotherlodeMaterial` is already a display name — `Alexandrite`, `Painite`, `Void Opal` — rather
than a slug, so the fallback is safe.

## 2. The headline: Elite's own grade does not mean what a miner wants it to mean

**This is the finding, and it is the one that would have shipped a wrong answer.** Every rock is
graded `Material Content: Low / Medium / High`, and the obvious implementation passes that on.

| Grade | Rocks | Best material: median | p90 | max |
|---|---|---|---|---|
| Low | 880 | **19.9%** | 35.6% | **66.7%** |
| Medium | 589 | 22.0% | 39.1% | 64.1% |
| High | 164 | **20.3%** | 40.2% | **66.4%** |

**Low and High are the same distribution.** And of the 135 rocks carrying a material at 40% or more,
**61 — 45% — are labelled Low.** The corpus holds a 58.3% Platinum rock graded `Low`.

The reason is that the grade is about **engineering-material content** — the fragments a collector
limpet picks up — and not about the **commodity proportion** being refined. Two different questions
that share a word. So the callout **ignores `Content` entirely**; repeating it would send a Commander
past the best rock in the cluster with d47 sounding authoritative.

That is a distinction any experienced miner knows and no amount of reading the schema reveals, which
is worth recording as its own lesson: the field name is not the field's meaning.

## 3. No single percentage threshold is meaningful

The obvious alternative — "tell me about anything over 25%" — fails on the same measurement.

| Material | n | median | p90 | max |
|---|---|---|---|---|
| Platinum | 185 | **26.6%** | **55.7%** | 66.7% |
| Water | 139 | 7.7% | 20.8% | 30.0% |
| Tritium | 502 | 12.3% | 25.7% | 35.6% |
| Liquid oxygen | 311 | 6.3% | 18.0% | 26.7% |

**Platinum's median is above Water's 90th percentile.** A 25% cut-off is routine for one and
near-unheard-of for the other, so a fixed number is wrong per material and silently so — and there is
no licence-clean table of per-material distributions to threshold against.

**So the callout thresholds against what the Commander has actually seen this session.** It needs no
source, it adapts to whatever they are mining, and it is honest about being relative: the line is
*"best you have found this session"* rather than a claim about the galaxy. The first rock of a
session is never announced as a best, because it is the only one.

## 4. The identity trap, for the third time in this repository

**13 of 27 materials are written two ways** — `gallite` and `Gallite`, `gold` and `Gold` — depending
on whether Elite wrote a `Name_Localised` beside the raw name. That is **40 raw spellings for 27
materials**.

Tracking a session high-water mark on the raw name keeps two marks for one material and announces a
personal best that is not one. `JournalJson.Symbol` — added for the colonisation commodity join, and
before that the lesson `MainEngines` taught the checklist — already folds it, and this is its third
call site. Worth noting that the same trap has now appeared in commodities, in module slots, and in
mining materials: **anywhere Elite writes an optional localised name, the raw one is a different
string and both reach d47.**

## What this means for the item

*Prospector and core callouts* ships with:

- **Two callouts, two settings rows** — `prospector` and `core-asteroid` — on the 0.18% asymmetry.
- **No mention of `Content`**, per §2.
- **A session-relative best**, per §3, rather than a fixed or per-material threshold.
- **Folded material identity**, per §4.
- **A core announced as `Routine` rather than `Urgent`.** Urgent speaks over the top of whatever is
  being said and is reserved for danger and fuel; a core is exciting and is not a safety matter, so
  announcing one across a hull warning would get the priority exactly backwards.

## Reproducing this

[`spike/MiningProbe/scan_prospect.py`](../../spike/MiningProbe/scan_prospect.py). It reads the
journals and prints; it writes nothing. The corpus is one person's play history and is not in the
repository.
