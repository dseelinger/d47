# Phase 34 — The long arc

The plan of record for list.md Phase 34. Written 2026-08-18, before any code, with Phase 33 merged
the same day.

`list.md` reads top to bottom as a description of the product. This is the order the work happens
in, and the reasoning the order cannot carry on its own.

---

## The phase in one sentence

The checklist holds what a Commander is doing this week and nothing holds what they are doing this
year, so this phase adds the **arc** — a named ambition with a definition of done, a progress figure
nobody typed, and an age — and then joins it back to the checklist, which is the part that makes
either of them worth having.

## What already exists to build on

- **`ChecklistProposals` is the accept-first path**, and it is exactly what item 3 asks an arc to
  speak through. Nothing here needs a second trust boundary: an arc proposes, the Commander agrees,
  and the committing store is the one the model cannot write.
- **`UnlockPlanner` and `EngineerPlanService` already answer item 3's question for one arc.** "What
  is the next concrete thing to do about a months-long goal" is what the engineer solver does, and
  the engineers arc delegates to it outright rather than reimplementing a worse version.
- **`HabitMiner` proved the corpus walk is affordable** — 697,787 events in 3.6 seconds over 914
  files — and it fixed the shape: per Commander on the Frontier id, carried across continuation
  files, folded by an object that owns no thread and reads no clock.
- **`HabitStore` fixed the file shape**: content-compare polling, `AtomicFile` writes, the Commander
  key inside the document, hand-editable, and a bad line reported rather than dropped. The goal
  store is the same shape with a different payload.
- **`EngineerProgressState`, `FleetRegistry` and `EliteSpecifications`** already hold three of the
  four things an arc has to be derived from: who is unlocked, what is owned, and what exists.
- **`SettingRow.Press` on an `Info` row is refused by `SettingsService.Apply`**, which is how Phases
  31, 32 and 33 kept an expensive local pass off the tool surface. Backfilling uses it again.

## Order of work

1. **Rank state** — the one thing the journal says plainly and nothing in Core reads.
2. **The arc** — what an arc is, and the nine that ship.
3. **The evaluation** — live state where it can say, the mine where only history can, null where
   neither.
4. **The mine** — one batch walk that gives every arc its start and the count-based ones their
   totals.
5. **The store** — per Commander, hand-editable, and the one thing in it a person authored.
6. **The join** — an arc proposing checklist lines that say which arc they came from.
7. **The capability and the page** — and the 86 bytes that have to be found before it can ship.

---

## Decisions taken before the code

### Ranks are counted, never named

The `Rank` event gives an integer per career and `Progress` the percent into it; neither carries a
word. Naming rank 6 in Exploration therefore means shipping a table of Frontier's rank words, and
**CLAUDE.md says a game-data table is derived by a generator with its provenance recorded, never
hand-written** — and there is no licence-clean generator source for the rank ladders the way there
is for ships, blueprints and engineers.

So d47 says *rank 6 of 8, 12% into it* and names only **Elite**, which is the arc's own definition
of done and is stated by the phase rather than read off a table. This is worse copy and better
practice, and a second thing decided it: the Mercenary ladder is the one this repo would have been
most likely to get wrong from memory, and a confidently wrong rank word is precisely the class of
error the `Knowledge` generator arrangement exists to prevent. `ShipCrew` already shows the other
half of the rule working — a crew member's rating is a *word Elite wrote*, so it passes through
untouched.

If a generator source for the ladders turns up, the words become a table and this reverses without
touching anything else. Nothing downstream depends on the absence of a name.

### CQC does not get an arc, and every arc can be set aside

Nine arcs ship: Elite in Combat, Trade, Exploration, Exobiology and Mercenary; every engineer
unlocked; the ship collection; and two exploration milestones — systems visited and distance flown.
**There is no CQC arc.** Almost nobody plays it, and an arc permanently at nothing is a line of the
page spent telling every Commander about a thing they are not doing.

That is a judgement about one game mode, and judgements about what somebody cares about are exactly
what a page like this should not be making, so the general form ships too: **any arc can be set
aside**, and a set-aside arc is gone from the page and from `get_goals` until it is brought back.
Stored the way `HabitStore` stores a dismissal — its own list, outside anything the mine rewrites —
so setting one aside survives a re-mine, which is the failure Phase 32 spent a paragraph on.

### Live state answers now, the mine answers history, and null answers neither

Item 2's rule, made concrete. Three sources and a fixed precedence:

- **Live journal state** answers ranks, engineers unlocked and hulls owned. It is current by
  definition and is preferred whenever it has anything to say.
- **The mine** answers the two things live state structurally cannot: **when an arc started**, and
  cumulative totals over a whole history — systems visited, light years flown.
- **Neither** is a real answer and is reported as one. An arc d47 cannot currently evaluate reports
  the figure it last could, stamped with when that was, and never resets to zero on the absence of
  evidence. Before Elite has emitted a `Rank` event this session the mined rank stands with its
  date; where nothing has been mined the arc says it has not looked yet.

The "as of" stamp is on the *figure* rather than on the arc, because one arc can have a live
numerator and a mined start.

### An arc proposes a task, not a verdict

The arc is the derived thing; the lines it proposes are work. So an arc proposes **authored** lines
through `ChecklistProposals` like anything else, and they tick by hand — with one exception, and it
is the exception item 3 is pointing at: **the engineers arc delegates to `EngineerPlanService`**,
which already emits a derived chain with an access step beside each modification. Reimplementing
that worse so that every arc's proposal had one shape would be shape over substance.

**A promoted line says which arc it came from.** `ChecklistItem` gains one optional `Goal` field
carrying the arc key, and the page draws it. Without it, finishing the line visibly moves nothing
bigger than itself, which is the whole emotional point of the phase — item 3 says so outright.

Rank arcs propose nothing, and say why: rank is earned by doing the career, and the honest next step
is the capability d47 already has for that career — `plot_trade_route`, `plot_exobiology_route`,
`plot_exploration_route`. **Naming a tool that exists is not inventing a plan**, and a rank arc that
manufactured a checklist line would be filler on a page whose credibility is all it has.

### `get_goals` is advertised, and something had to be trimmed to pay for it

Phases 31, 32 and 33 shipped everything Protected, and Phase 32 recorded that the arithmetic and the
reasoning happened to point the same way. **Here they do not.** *How is the Elite exploration push
going* is a thing said out loud mid-flight, and an arc the model cannot see is an arc d47 cannot be
asked about — which would leave the phase's whole subject reachable only by opening a panel.

The SRV profile measured **39,914 bytes against `ComfortableBytes` of 40,000** before this phase, so
`get_goals` does not fit and no third raise of the constant is on offer — `ToolProfiles` says in as
many words that raising it again is the wrong answer. The room is found by **trimming advertised
descriptions that are longer than they need to be**, which costs words and nothing else. Everything
that writes — setting an arc aside, backfilling, accepting a proposal — stays `Protected`, so the
model can read the arcs and cannot touch them.

### The backfill is a button, not a background job

One pass over every journal on the disk, started by a `SettingRow.Press` on an `Info` row, which
`SettingsService.Apply` refuses — the mechanism Phases 32 and 33 already use, for the same reason.
Nothing leaves the machine and no journal reaches a model: the mine is arithmetic over events, and
what it emits is counts and dates.

---

## What building it found

### The corpus corrected the mining rule, again

`HabitMiner` learned on its first real run that 914 journals are three accounts. This walk learned
something further about the same folder: **one of those three Frontier ids reports Trade 7 in July
2025, Trade 2 in January 2026 and Trade 0 in June 2026.** Ranks do not fall. A Frontier id is an
*account*, and a person can begin a new Commander inside one.

So the rule the plan wrote — an arc starts at the first evidence of a non-zero rank — was producing
a start date belonging to a character who no longer exists. The fix is one line of reasoning and it
generalises: **a figure that falls is a save started again, and the arc restarts with it.** Applied
to the ranks and to the engineer count, which is where it mattered most — the same account was being
credited with 23 unlocked engineers when its current save has 4.

Before and after, on the real folder:

```
rank.combat: have 1, started 3 Jul 2025      <- a character that no longer exists
engineers:   have 21, started 3 Jul 2025     <- max over history

rank.combat: have 1, started 6 Jan 2026      <- the save actually being played
engineers:   have 4,  started 30 Dec 2025    <- the latest snapshot
```

914 journals walk in 3.5 seconds, against `HabitMiner`'s 3.6 for the same folder.

### The provenance nearly did not survive its own accept path

`ChecklistProposal` carried the arc key correctly and `ChecklistDocument.AddNote` minted a fresh
item without it, so an accepted line landed on the Commander's list with nothing saying where it
came from — the exact failure item 3 spends its length on, in the one step nobody had looked at.
Caught by the test written for item 3's sentence rather than by anything else, which is the whole
argument for writing the assertion the item states rather than the assertion the code suggests.

### The bytes

`get_goals` costs 215 bytes advertised, against 86 of headroom. Six descriptions were trimmed —
`find_colonisation_candidates`, `read_system_name`, `get_module_specification`,
`get_construction_sites`, `plan_colonisation` and `plan_on_foot_build` — none of which lost a
caveat, only words. The SRV profile ends at **39,789 of 40,000**, which leaves the repository with
more room than it had before the phase rather than less. **The next advertised tool still does not
fit**: 211 bytes is one small tool and no margin, so deferred loading is the work that has to happen
before another one ships.
