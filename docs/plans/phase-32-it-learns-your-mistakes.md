# Phase 32 — It learns your mistakes

The plan of record for list.md Phase 32. Written 2026-08-18, before any code, with Phase 31 merged
the same day.

`list.md` reads top to bottom as a description of the product. This is the order the work happens
in, and the reasoning the order cannot carry on its own.

---

## The phase in one sentence

Phase 31 taught d47 to remember what the Commander *told* it. This phase teaches it to notice what
the Commander keeps *doing* — from 914 journals that have been sitting on the disk for thirteen
months with nothing ever having read them end to end for this purpose.

## What already exists to build on

- **`spike/CorpusReplay` proves the mechanism at speed.** 697,787 events through Core in about
  seven seconds, because Core owns no thread and reads no clock. Mining is the same walk with a
  different fold on top, so "is a batch job affordable" was answered before the phase was written.
- **`JournalEvent.Raw` is a cloned `JsonElement`.** A detector can read a field Core has never
  modelled without anything being added to `CommanderGameState` — which is what keeps the miner
  from dragging the whole state machine behind it.
- **Phase 31 built both halves of the store this needs**: `MemoryStore` is polled by content, keyed
  per Commander with the key inside the document, written through `AtomicFile`, and reports a bad
  line rather than dropping it. `HabitStore` is that shape with a different payload.
- **`CalloutEngine` already owns cooldown and precedence**, and `FlavourBriefs.For` already decides
  which lines get said in character. A personal callout is a new `ICallout`, not a new voice.
- **`SettingRow.Press`** is an `Info` row with a button, and `SettingsService.Apply` refuses `Info`
  rows outright — so "look through my journals" is unreachable from the tool surface for free.

## Order of work

1. **The claim and its evidence** — the record everything else produces or reads.
2. **The detectors** — one per pattern, each folding events and answering with a claim or with a
   reason it has nothing to say.
3. **The miner** — the batch walk that drives them, per Commander, over files that already exist.
4. **The store** — claims and dismissals on disk, so a callout can fire without re-mining.
5. **The callout** — off by default, fired by the situation the claim is about.
6. **The capability** — reading back, explaining, dismissing; all of it Protected.

---

## Decisions taken before the code

### There are 86 bytes left, so nothing here is advertised

Phase 31 shipped the SRV profile at 39,914 against `ToolProfiles.ComfortableBytes` of 40,000 and
said in as many words that the next phase wanting an advertised tool has to do the deferred-loading
work first. This phase does not want one.

Everything Phase 32 registers is `ToolDefinition.Protected` with router phrases, which costs zero
advertised bytes. That is not a compromise forced by arithmetic — it is the correct answer anyway.
The mined result is **a claim d47 made about the Commander from their own journals**, and the
untrusted-input boundary says the thing that reads hostile in-game text is not the thing that gets
handed a psychological profile. Item 1 already says *no journal reaches a model*; keeping the
**conclusions** out of the model's reach too is the same rule applied one step further along.

So the entire phase is Core-side: the miner, the store, the callout and the readback. The model can
see none of it, and the Commander can see all of it.

### The corpus was measured before anything was promised

The item says a habit is *a claim with a count behind it*. That rule was applied to the phase
itself first: every candidate detector was measured against the 914 journals before it was written
down, and two of the five candidates did not survive.

| Candidate | Measured over 914 journals, Jul 2025 – Aug 2026 | Shipped |
|---|---|---|
| Non-combat hull damage on arrival | **31** hits with no attacker in the window — 18 within seconds of a supercruise drop, 13 near a planet. **Three of the 23 deaths are these** | yes |
| Overshooting and coming back | **69** drops followed by a re-entry and a second drop at the same body, median 41 s of shame. All 69 at planets, **none** at stations | yes |
| Submitting to interdictions | **50 submits of 52**, 3 escapes | yes |
| Dying on foot at settlements | **11 of 23** deaths to suit AI or settlement turrets, and they cluster — five on 2026-01-04 alone | yes |
| Landing somewhere heavy | 477 of 532 touchdowns under 0.5 g, 7 between 0.5 and 1, **none above 1 g** | ships, and says it has nothing |
| Missing an impact warning | Elite writes no proximity, impact or collision event, and never has | **no** |

Two findings are worth writing down properly, because they are the kind that get rediscovered
expensively.

**There is no impact or proximity event, at all.** The wanted habit — *set it and forget it, and I
do not see the impact warnings* — describes a HUD element the journal has no counterpart for. This
is the same shape as Phase 15's contacts-panel finding and it gets the same treatment: it is not
built, and it is recorded here so nobody goes looking again. What **is** detectable is the
consequence — hull damage with nobody shooting — and that is the first row of the table and the
strongest signal in the phase. The warning cannot be observed; the failure to heed it can.

**The gear is not in the journal either.** `list.md` opens item 2 with *never "you always forget the
gear"*, as an example of a claim made without a count. It turns out to be a stronger example than
intended: there is no landing-gear event of any kind, so the count could not exist at any sample
size. The phase's headline illustration is undetectable, which is exactly why the item insists on
the count.

### A high-gravity detector that finds nothing still ships

Item 2's other half — *a new Commander gets silence and an explanation, not a confident habit
derived from a fortnight* — has no natural test case in a thirteen-month corpus, because everything
else in it has hundreds of samples. High gravity gives it one for real: the detector runs, folds
`Scan` for surface gravity, joins it to `Touchdown`, and reports **"I have not seen you land
anywhere heavy enough to say"** rather than nothing at all.

So `HabitReport` carries two lists and the readback reads both: the claims, and the detectors that
declined with the reason they declined. A detector that is quiet because the floor was not met is
a different thing from a detector that found no problem, and a Commander told *nothing to report*
when the truth is *not enough of you yet* has been told something false about themselves.

### The floor is three numbers, and they are on the type

- **20 journals.** Under that the whole report is a refusal — no detector is consulted and the
  reason names the count. A fortnight is explicitly what item 2 refuses to generalise from.
- **5 occurrences.** Below five, a pattern is a coincidence with a sample size.
- **10 opportunities.** A rate needs a denominator: *50 of 52* is a habit and *2 of 2* is a Tuesday.

Every claim carries all of it — occurrences, opportunities, the window's two ends, the journal
count, and how many of the occurrences fall in the last thirty days. That last number is what makes
item 2's own sentence sayable: *eleven times this month, twice last night.*

### Mining runs on demand, and per Commander

A button on the panel and a router phrase, and nothing else starts it. Seven seconds over 376 MB is
cheap but it is not free, and the item says it costs nothing while flying — which is a promise about
the tick loop that is only kept by there being no path from the tick loop to it.

**Per Commander, keyed on the Frontier id out of `LoadGame`.** The corpus is three accounts and nine
character names across thirteen months; a miner that pooled them would report one Commander's
habits to another and would be confidently wrong about a person. `MemoryStore` already keys this way
and for the same reason, and the key stays inside the document rather than in a filename, because a
Frontier id comes out of untrusted input.

Core stays synchronous and clock-free: `HabitMiner.Mine` takes the files and a `now` and returns a
report. The App is what puts it on the thread pool.

### The callout fires on the situation, not at the start of a session

`ContinuityCallout` already owns the opening line. A second one competing with it is exactly the
"second voice" item 3 forbids, so the personal callout fires **when the circumstance the claim is
about arrives**: dropping out at a station, approaching a planet, being interdicted, walking into a
settlement.

Each claim declares one `HabitOccasion`, and the callout maps this tick's events to occasions. The
bar item 3 asks for is met by four separate things rather than by one:

- **Off by default.** A companion that starts commenting on your flying without being asked has
  changed the deal, which is the item's own sentence.
- **A claim must clear all three floors** before it can be spoken at all.
- **A long cooldown** — four hours per claim — because a warning repeated on every approach is not
  a habit report, it is nagging.
- **Dismissal is permanent and is remembered**, because the same wrong observation arriving monthly
  is the failure mode the item names.

### Why it fired, and stopping it, are both argument-free

The two phrases that matter refer to the thing just said, so both are reachable through the router
with no model in the path — which is the whole reason they can be Protected and free:

| Phrase | Tool | What it does |
|---|---|---|
| *what have you noticed about me* | `get_habits` | every claim with its counts, and every detector that declined |
| *why did you say that* | `explain_habit` | the evidence behind the last claim spoken |
| *stop telling me that* | `dismiss_habit` | dismisses the last claim spoken, permanently |

`dismiss_habit` takes an **optional** key, so the panel and a hand edit can name one, and the phrase
carries none and means "the one you just said". `KeywordRouter.MatchToolCommand` is whole-utterance,
so *"why did you say that about the fuel"* falls through to the model rather than being answered by
the wrong mechanism.

### One store, one file, and dismissals outlive the claims

`data/habits.json`, the `MemoryStore` shape: `AtomicFile`, polled by content comparison rather than
by a last-write time, keyed per Commander inside the document, hand-editable, and a line that cannot
be read back is reported rather than dropped.

**A dismissal is stored apart from the claim it dismissed and survives it.** Re-mining rebuilds the
claims from scratch — that is what mining is — so a dismissal attached to a claim record would be
erased by the next run, and the Commander would be told the same wrong thing again a month later
with no memory of having refused it. The dismissed keys are their own list, and mining never touches
them.

## Measured after the fact, 2026-08-18

**The advertised tool surface did not move by one byte.** The SRV profile is still at **39,914**
against `ToolProfiles.ComfortableBytes` of 40,000, and every other profile is where Phase 31 left
it — docked, landed, normal space and supercruise at 39,202, on foot at 37,486, fighter at 36,774,
no-game and degraded at 36,305. Three tools were registered and the model can see none of them. The
86 bytes of headroom Phase 31 left are still there, and the deferred-loading work it names is still
what the next phase wanting an advertised tool has to do first.

**The mining is faster than the bar the replay harness set.** 697,787 events across 914 journals in
**3.6 seconds**, against the seven `spike/CorpusReplay` takes for the same corpus through the full
callout set.

**What the real corpus produced**, per Commander and after the split:

| Commander | Journals | Claimed | Declined |
|---|---|---|---|
| F12242026 | 369 | overshoot 34/1484, submits 19/21 | collisions 4/1989, deaths 9 (too few), gravity 0 over 1 g |
| F12484034 | 403 | collisions 9/2370, overshoot 19/1952, submits 24/24, on-foot deaths 6/12 | gravity 0 over 1 g |
| F735466 | 234 | overshoot 14/490 | collisions 1/662, interdictions 7 (too few), deaths 2 (too few), gravity 0 over 1 g |

The middle row of that table is the argument for keying on the Frontier id in one picture: the third
Commander has seven interdictions and is told nothing about them, while the second has twenty-four
out of twenty-four. Pooled, all three would have been told the same thing about themselves and it
would have been true of one of them.

## Proved to catch it, 2026-08-18

Three faults, reintroduced deliberately and watched to fail, because a negative assertion passes
when the mechanism is broken just as readily as when it holds.

1. **`HabitStore.Record` clearing the dismissals**, which is what a dismissal held on a claim record
   would amount to. `ADismissalOutlivesTheClaimItDismissed` failed — the single assertion standing
   between this phase and the failure item 2 calls the one it risks.
2. **Counting every hull-damage event rather than every accident.**
   `OneCollisionIsOneOccurrenceHoweverManyHullEventsItWrote` failed at 100 against 25, which is a
   four-times inflation of the phase's strongest claim and would have read as a habit either way.
3. **The miner ignoring the Frontier id.** `TwoCommandersAreCountedApart` failed with one report
   where there should be two — the fault that tells one Commander about another's flying.

## Acceptance

- A corpus of fewer than 20 journals produces a report that claims nothing and says why.
- A claim that clears the floors carries its occurrences, its opportunities, its window and its
  recent count, and the sentence quotes all four.
- Mining the real corpus produces the four claims the table above measured, at those counts.
- The high-gravity detector reports that it has nothing to say rather than being absent.
- The habit callout is off in a default `D47Settings`.
- A dismissed claim is not spoken, and is still dismissed after a re-mine.
- No advertised tool byte count moves — `ToolProfileTests`, unchanged.
