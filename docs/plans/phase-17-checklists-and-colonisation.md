# Colonisation, and the checklist it shares with engineering

## Context

Colonisation was one line in Phase 18 — commodity requirements and delivered-so-far. The maintainer
scoped it out on 2026-08-15 into three asks: **find a target, plan it, execute it.** That is the
same triple engineering has, and engineering's plan-and-execute half was already deferred to Phase
17. So this plan covers both phases, because the interesting decision is shared.

**The community has built this three times over, and Frontier published the rules.**

| Source | What it is | Why it matters here |
|---|---|---|
| [Frontier's System Colonisation Guide](https://www.elitedangerous.com/news/system-colonisation-guide) | Primary source | States the mechanics outright — 24-hour claims, what facilities move, what a specialisation produces |
| [gaborauth/ed-colonisation-planner](https://github.com/gaborauth/ed-colonisation-planner/) | Open source | Models station service activation rules — the closest thing to a machine-readable effect model |
| [Raven Colonial](https://ravencolonial.com) | Web tool | Plans a system showing links between ports and resulting economies; tracks carrier cargo against builds |
| [EDSC](https://www.edsc.info/) | Web tool | Construction planning, deliveries, fleet logistics |
| [ArchitectTracker](https://github.com/kol19pl/EliteDangerous-ArchictectTracker) | Open source | Depot tracking, hands-free |

The existence of four mature tools is not a reason to skip this. It is a reason to be precise about
what d47 adds: **none of them can talk, and none of them are already reading your journal for eleven
other reasons.** The value here is the same as everywhere else in this repo — the answer arrives
without the Commander taking their hands off the stick.

---

## The split

Ordered by how much it depends on somebody else being right.

| Half | Phase | Needs a source? | Answered 2026-08-16 |
|---|---|---|---|
| **Execute** — what is required, what is delivered, what is left | 18 | No. Journal only. | **Confirmed, and cheaper than hoped** |
| **Find** — candidate systems for a stated objective | 18 | The objective's criteria; the index for body shape | Criteria yes; **availability never** |
| **Plan** — an objective costed into things to do | 17 | The facility cost and effect table, if it exists | **It does not exist licence-clean** |

*(The phase column was stale until 2026-08-16: it held pre-renumber numbers, because a bare digit in
a table cell matches no pattern looking for "Phase" followed by one. That is the second time that
exact shape has been missed — see `CLAUDE.md` on remapping.)*

**Execute ships first and waits on nothing.** `ColonisationConstructionDepot` carries required and
provided amounts, so the arithmetic the maintainer is currently doing on paper is subtraction over
data already on disk. It is the cheapest item in Phase 18 and the one most likely to be used daily.
The spike made it cheaper still: the event is a **snapshot rather than a delta**, measured over
6,330 events and 120,208 rows, so "what is left" is one subtraction over the latest event per
`MarketID` with no history to keep and no commodity table to carry — `Name_Localised` is on every
row. Two corrections it did make: **several sites can be open at once**, so this is a collection with
a selection rather than a current-site field, and the event only arrives **while docked at that
site**, so what d47 knows is as fresh as the Commander's last visit and should say so.

---

# The spike

Throwaway probe under `spike/`, finding to `docs/spikes/`. Read Frontier's guide **first**, because
a primary source outranks every tool above it and changes what the rest of the spike is looking for.

## 1. Is the facility model data, or is it prose?

Costs, effects, and the link rules that decide how facilities influence each other by placement.

- **First place to look:** `gaborauth/ed-colonisation-planner`, which already models activation
  rules. Then EDSC and Raven Colonial.
- Licence checked on the **transitive graph**, and the data underneath checked separately — a
  source's own licence is never mistaken for permission over Frontier's figures. coriolis-data says
  so outright about its own JSON, and that lesson was learned here once already.
- ~~**If it is prose:**~~ **It is prose, effectively — settled 2026-08-16.** Every machine-readable
  rendering of the figures is inside GPL-3.0 source (`gaborauth/ed-colonisation-planner`'s
  `buildings.ts`, and SrvSurvey, which carries no data files at all), EDSC publishes nothing, EDCD
  has no colonisation repository, and everything traces to one unlicensed community spreadsheet.
  Frontier's own guide states every mechanic and **publishes not one number** — and predates the
  link topology Update 3 added. The licences are a fact about the code rather than about Frontier's
  figures, which is why this is a routing answer and not a moral one: there is no licence-clean
  *route* to numbers that were Frontier's before anyone typed them into a sheet.

  **So the planning half does not ship as a costed table.** *A colonisation plan writes the
  checklist* becomes what the checklist substrate already provides — an objective, an ordered set of
  intents, and progress diffed against the depot — with quantities entered by the Commander or read
  from the site once it exists, rather than predicted from a table d47 does not have. Strategy advice
  moves to web search. A smaller item, and an honest one.
  See [../spikes/colonisation-sources.md](../spikes/colonisation-sources.md).

## 2. Three journal questions, none of them measured yet

- **Is `ResourcesRequired` a snapshot or a delta?** This trap has now caught `EngineerProgress` and
  `StoredModules`, and it was silent both times. Assume nothing.
- **Does the depot event fire only while docked at the site**, or does it arrive from anywhere?
- **Can more than one construction site be active at once?** Decides whether the state is a record
  or a collection, and that is expensive to change later.

## 3. Is a claim visible from outside the game?

A claim lasts 24 hours and lives on Frontier's servers. If no crowd-fed index holds it — which is
the expectation — then *Find somewhere worth colonising* can say "this system has the bodies your
objective wants" and must never say "this one is free". Confirm rather than assume, then write the
answer into the item as a **what this cannot be** paragraph.

## 4. What the objectives actually are

The recurring community finding is that the obvious choice is wrong: a **Colony** primary economy is
a poor pick for self-sufficiency, because a colony economy consumes what High Tech and Industrial
produce, so a self-feeding system must build those in-system. The rest of the archetype — one
economy per body with its own market, one or two dominant, nothing levelled equally — is strategy
rather than mechanics. Establish which parts Frontier states themselves (quotable) and which are
player experience (**attributed via web search, never a hand-written table**).

---

# The checklist model

Settled in conversation on 2026-08-15. This is the part both phases depend on, and the part that is
expensive to get wrong.

## One surface

"What am I working on" has exactly one answer, on one panel, through one set of voice commands. The
derived lists — a ship's build, a system's construction — appear there rather than each growing a
surface of its own.

## Two kinds, distinguished by how "done" is decided

| | Authored | Derived |
|---|---|---|
| What it is | A sentence — *buy limpets* | A structured intent — *grade 5 dirty drives, slot 4* |
| How it completes | A person says so | Computed from live journal state |
| Manual tick | Yes | **No — refuses, and says why** |
| Can un-complete itself | No | Yes, and it announces it once |

The kind is a property of where the item came from, not something the Commander picks. Mixing them
in one bucket produces a list where some ticks are computed and some are opinions, with nothing on
screen saying which — the failure the descriptor code already names about keyword lists: *a list
whose entries mean different things depending on a flag is a list that gets read wrong.*

A derived item refuses a manual tick because the next journal read would either undo it or, worse,
leave it standing and lying.

## Three groups, orthogonal to kind

Universal, this ship, this system. Derived items belong to whatever produced them; authored items
file anywhere, which is what lets *"ask Jim about the Krait build"* sit beside the Krait's plan.

## Completing is not removing

A finished item stays, checked. On something that runs for weeks, seeing how far you have come is
most of the point — so completed items sit below the line or collapsed, with their count visible, so
forty finished ones never bury the six still open.

**Deleting is changing your mind.** A different act, and it can happen to an item whether or not it
was ever finished.

## Revision is a diff, not a rebuild

Changing the plan — burst lasers instead of multi-cannons, a Hub instead of a simple station — is a
supported operation and must be told apart from the world moving under a plan that did not change.

- **World changed, plan did not:** the item un-completes and d47 says so **once**. A computed tick
  going backwards is information, not a glitch to hide.
- **Plan changed:** items in both versions keep their history, dropped ones tombstone, added ones
  open.

Rebuilding the list would wipe a fortnight of progress the first time somebody changed one weapon,
which quietly kills the whole point of keeping completed items.

**So item identity is the load-bearing decision.** Two plans can only be diffed if an item knows what
it is independently of its position in a list: slot plus intent for a ship, body or orbital slot
plus facility type for a system. Get that key wrong and every revision reads as *everything removed,
everything added*.

One case decides whether the history tells the truth: an item **completed and then designed out**
tombstones as *done, then superseded* rather than vanishing. The Commander really did spend that
fortnight.

## Filters are generated, not listed

Three axes — kind, source, state — so *authored*, *ship*, *colonisation*, *complete* and *open* all
exist today and a fourth kind of plan appears in the filter row without anybody remembering to add
it. The same relationship the settings surface has to the capability registry: a projection, never a
parallel list.

---

## Order and dependencies

Phase 18's tracking item needs nothing and should ship first. **Phase 16's** colonisation spike
gates the planning table and the objective criteria, not the tracking — it is named by number here
because it sits in neither of the two phases this plan covers, having been pulled into Phase 16 on
2026-08-15 for exactly that reason. Within Phase 17, the checklist substrate is built once —
**item identity and the two kinds are the two decisions to settle before any code** — and the ship
and colonisation plans are then two keys into one machine.

A second checklist engine for colonisation would double every hard part in this phase and then let
the halves drift, so that "what am I working on" has two answers depending on which is asked. What
is genuinely different between a ship plan and a system plan is the key and the table behind it.

**Release:** these items span two phases, so neither closes on this work alone.

---

## What shipped, 2026-08-16

Phase 17 is closed. The substrate is one capability (`checklists`), one service, two files, and one
panel; the ship plan and the colonisation plan are two keys into it, as this plan said they would be.

**Item identity survived contact, but only just.** The design here — content-addressed on the intent,
never on position — is right and is what makes revision a diff. What it did not say is that the
*spelling* of the subject is part of identity too: `MainEngines` and "main engines" produced different
keys, so a plan restated in a differently-worded conversation would have read as every item tombstoned
and an identical set opened, which is precisely the failure the whole decision exists to prevent. Two
fixes: keys compare with separators stripped, and a spoken slot is resolved against the live `Loadout`
before it is keyed, so "thrusters" and `MainEngines` are one item. A test found this, not a Commander.

**The planning half of the ship build has a gap this plan did not anticipate.** Elite never localises
a blueprint name — that was already measured and already documented in `EngineeringCapability` — and
**nothing d47 ships joins `Engine_Dirty` to "Dirty Drive Tuning"**. So "checked against the `Loadout`"
holds for the slot, the grade and whether the grade is finished, and does not hold for the name. The
comparison answers true or *cannot say* and never false, because guessing false would tell a Commander
their finished module is unfinished. Where the looking stopped is
[../spikes/blueprint-name-join.md](../spikes/blueprint-name-join.md), including the one place nobody
opened.

**The trust boundary needed a new flag.** `ToolDefinition.Protected` is the tool-level twin of the
settings-row flag that already existed, and it is enforced twice — left out of the advertisement so no
cached prefix pays for a refusal, and refused at the call when the caller is the model, because
advertisement alone is a lock on the front door and none on the back.

**And the depot fold was built here rather than in Phase 18.** The colonisation plan needs something
to diff against, and this plan's own order table put tracking first — which it was not. `ColonisationSites`
is the state; Phase 18's item is the reporting surface over it and is still open.
