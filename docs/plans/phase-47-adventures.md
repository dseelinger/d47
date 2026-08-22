# Phase 47 — Adventures

The plan of record for list.md Phase 47. Written 2026-08-22, before any code, while Phase 45 was
still being built on `main`; Phase 45 merged as v0.51.0 the same day and this moved in from the
scratchpad. The stories that specify the prompt are beside it in [phase-47-stories.md](phase-47-stories.md).

`list.md` reads top to bottom as a description of the product. This is the order the work happens
in, and the reasoning the order cannot carry on its own.

---

## The phase in one sentence

An **adventure** is a story — a protagonist who wants something, a misbelief the events exist to
test, a turn and an ending that means something — told by the ship's AI as a companion would,
anchored to the galaxy by beats whose triggers come from a closed vocabulary, accepted by the
Commander and advanced by their own journal.

## What it is for

Stated by the Commander on 2026-08-22, and it governs everything below: **the drive is to add
story to a sandbox game, which sandbox games deeply lack.** An adventure must not feel like a
checklist of items to complete. It must have an element of true storytelling — *story* as the
craft books define it: Lisa Cron's *Story Genius*, Donald Maass's *The Emotional Craft of Fiction*,
and for the big ones Blake Snyder's *Save the Cat!*. None of that is baked into the UI; all of it
shapes what the model is asked for and how the result is drawn.

Three things those books say that the design can use:

- **A story is internal change, not a sequence of events** (Cron). Someone wants something, holds
  a belief the story exists to test, and every scene answers *what happens, why it matters, and
  what they now understand*. The blueprint is written before the scenes.
- **The reader's feeling is the product, and it comes from what they are shown** (Maass), never
  from being told what a character feels. Applied here: the Commander is the reader; a beat shows
  them the world and lets the feeling arrive, and no line tells the Commander what they feel.
- **Structure is a beat sheet scaled to the story's size** (Snyder): opening image, setup,
  catalyst, debate, midpoint, all is lost, finale, final image. A short story compresses it; a
  long one spends it.

The triggers, the fold and the file are untouched by this — they are how the story lands on the
galaxy. What they are used *for* is the subject of the phase.

## What already exists to build on

- **`GoalArc` and `GoalStore` are the arc half.** Key, name, a definition of done, `Written`,
  `Finished`, per-Commander on the Frontier id, hand-editable, content-compared polling, `AtomicFile`
  writes, problems reported rather than dropped. An adventure is a new record, not a new `GoalKind`,
  because an arc is cumulative and order-free and an adventure is sequential — but the store is the
  same shape with a different payload, which is the sixth time that shape has been used.
- **`ChecklistProposalStore` is the accept path.** Proposing is model-callable and committing is
  not, into two files so the boundary is inspectable from `data/`. A generated adventure rides it
  unchanged; an authored one does not need it, because `GoalBook.Author` already shows that a
  Commander's own words are written directly.
- **`MacroStore.Problems` is the refusal shape**: load what is valid, list what was refused and why,
  draw the list on the page. A beat naming an event or a field outside the vocabulary is refused by
  name with the reason, and the adventure it belongs to cannot begin until it is fixed.
- **`MacroWindow` is the authoring guarantee**: every field a closed vocabulary except the name, so
  the form cannot compose what the validator would refuse, and the form writes the same file a text
  editor writes — *the panel is a convenience over the file, not an alternative to it*.
- **`PanelPrompts` and `DrillView`** are how a form lives inside a tab since Phase 25: a chooser
  takes the panel, an entry is said or typed, and a level drills with a breadcrumb. The editor is
  a level, not a `Window`.
- **`AmbientLines` + `FlavourTurn`** are the telling pattern: an authored line is the floor, played
  when there is no model and said plainly when personality is off; with a model the core says it in
  its own voice off the conversation path, recorded against spend, and the authored line is never
  heard. **`AmbientCallout.Settle`** (90 s) and `ContinuityCallout.Settle` (8 s) are the settle
  discipline; `DangerCallout` knows what "being interdicted" is.
- **`PromptAssembly.LiveGameState`** is position 8, below the breakpoint, rebuilt every turn for
  free. Standing adventure context sits beside it and costs nothing in caching.
- **`Guardrails.Text`** sits above the persona and cannot be reached by the personality switch. One
  sentence about fiction goes there and nowhere else.
- **`LoreTier`'s rule** — set on arrival, never promoted, no code that could — is the rule item 6
  restates. An adventure is not a tier of lore; it is a voice the persona may speak from that the
  factual tools never read.
- **`IGalaxyService.SearchAsync` / `FindStationsAsync` / `DistanceAsync`** exist behind the `galaxy`
  egress row, already disclosed. Resolution of a typed name opens no new destination.
- **`spike/CorpusReplay`** drives 914 journals through the same fold the tick loop uses in seven
  seconds, so a test adventure can be shown to fire where it should and nowhere else on real data.
- **`PanelNavigator.Destinations`** (Phase 46) derives the switch vocabulary from the roots each
  surface registered, so a new tab's root is reachable from a HOTAS switch and a spoken phrase with
  no second list to maintain.

## Order of work

1. **The model, the vocabulary and the validator** — Core only, no UI, no clock.
2. **The fold** — one function for the live path and the startup catch-up from `AcceptedAt`.
3. **The store** — `data/adventures.json`, per Commander, hand-editable, the problems list.
4. **Telling** — standing context below the breakpoint, the beat callout, the guardrail line.
5. **The tab** — the list, the reading level, the editor, begin.
6. **Generation** — the dedicated turn, the Galactic Mapping lookup, the dry run, the proposal
   row, revision before acceptance; and the two ids the galaxy summaries do not carry yet.
7. **The proof** — the corpus check, the register test, and the list.md write-up.

Steps 1–3 are testable before anything is drawn and are where the corpus gets its say. Step 4 is
the phase's value and lands before the tab so the thing can be heard before it can be edited.

---

## Decisions taken before the code

All settled with the Commander on 2026-08-22. Seven were proposed and agreed; three were theirs.

### Progress is derived from the journal after `AcceptedAt`, and nothing else is stored

Phase 34's rule — no progress figure is ever stored — with the sequential wrinkle. The store
holds the definition and one stamp, `AcceptedAt`. Everything after that is a fold over events whose
timestamp is later than the stamp: which beat is current, when each earlier beat fired.

The reason is not purity. **Commanders play with d47 closed.** A stored beat pointer advanced by the
live path alone misses every beat that fired while d47 was not running, permanently, and the
adventure sits one beat behind the Commander for the rest of its life. A fold from `AcceptedAt`
catches up at startup with the `HabitMiner` walk bounded by date — the files after the stamp, not
the whole folder — and the same function handles the live tick. One fold, two callers, the shape
`GoalMiner` already fixed.

*"Adventures start when accepted and mine no history"* is therefore exactly true: nothing before
the stamp is read, the first beat is a beginning, and the Phase 34 account-restart problem cannot
arise because no arc is being reconstructed.

### Linear in v1

One current beat, and only it can match. A beat that would match but is not current is ignored,
not banked. Branches and "any of these" are a later wrinkle; the corpus question — *did it fire
where it should and nowhere else* — is answerable for a line and much harder for a graph.

### Five triggers, every one an integer comparison on a structured field

Core has no catalogue of journal event kinds — every consumer switches on strings — and building a
150-name catalogue from Frontier's schema would be a table of theirs written by hand. **The
vocabulary is the catalogue.** A beat may be:

| Trigger | Event(s) | Matched on | Never on |
|---|---|---|---|
| Arrive at a system | `FSDJump`, `Location`, `CarrierJump` | `SystemAddress` | `StarSystem` |
| Dock at a station | `Docked` | `MarketID` | `StationName` — a carrier's name is player-chosen |
| Land on a body | `Touchdown` | `SystemAddress` + `BodyID` | `Body` |
| Scan a body | `Scan` | `SystemAddress` + `BodyID` | `BodyName` |
| Reach a rank | `Promotion` | career + integer | any rank word — Phase 34 counts, never names |

Names ride beside the ids in the file for a human reading it, and the ids are what match. A trigger
naming any other event, any other field, or a string where an integer is expected is refused by
name with the reason, and the file loader and the form share the validator so they refuse
identically. Nothing attacker-controlled — a ship name, a message, a mission title — can be a
trigger by construction, which is the list's requirement stated as a type.

`Location` counts as arrival because a Commander who logs in at the story's system has arrived; a
log-in that finds them already at beat 3's system while beat 2 is current does not skip ahead, per
linearity.

### Generation is a panel action, not a tool

86 bytes of tool surface remain and nothing advertised fits. None is needed: the Commander asks
from the tab, d47 runs a dedicated schema-shaped turn off the conversation path (the route
planner's shape, `FlavourTurn`'s bookkeeping), and the result goes through the dry run and then
into a proposal row. **The model authors the triggers once and a person agrees** — this is
`ChecklistProposals`' own sentence. Nothing about adventures is callable by the model, so a hostile
in-game message cannot propose one; the standing context is readable by it, which is the point.

### The dry run resolves every place and bounds every hop

An adventure can name only real event types and still be unsatisfiable. Before a generated one is
offered, every named system is resolved through `IGalaxyService` to a `SystemAddress` and every
station to a `MarketID`; a miss refuses the whole proposal with the name that missed — which is the
Phase 23 generator assertion that caught Beagle Point's old name and a system a web search had
invented, applied at runtime. Sane order in v1 is **every place real and each hop under a bound
from the last** via `DistanceAsync`; a full route-plausibility check is a second pass if real use
asks for one.

### Tells, may propose, never acts

A beat speaks. It presses nothing, reads no binds, checks no foreground and writes no checklist
line. It *may* hand a line — *go to Jameson Memorial* — to `ChecklistProposals` the way
`GoalBook.Propose` does, carrying the adventure key so the accepted line says where it came from
(Phase 34's provenance rule, and the bug its test caught). Optional, and built only if the tab's
first use wants it.

### No import yet

Commander-authored and Generated are the two provenances in v1, recorded on the record as an
`AdventureSource`. Imported is deferred on the Commander's call; Downloaded is deferred on the
list's. Because the store file is the definition format, Import when it comes is a copy and a
validate, and nothing built now has to change shape for it.

### Its own tab, desktop first

`PanelTab.Adventures`, furnished on the desktop window only. The parity rule says a tab lives where a
host calls `Furnish`, so the headset gets it by one call when someone wants it there — the reading
level, plausibly; the editor, never, because the editor is typing.

### Speak on both

A beat is a callout when it fires *and* standing context in between. The callout is the moment —
settled, dropped mid-danger, persona-voiced with the authored floor. The context is what lets the
core notice the Commander is two jumps from where the story wants them while they are going
somewhere else, which is the Commander's framing: a companion throughout, not a sequence of gates.

### Abandon and remove, without finishing

Asked for 2026-08-22. Two acts, because the checklist and the goals page already distinguish them
and a page that merged them would be the odd one out.

**Abandon** is for a begun adventure and means *stop telling me this*. It stamps `AbandonedAt`;
from that instant no beat fires, the standing context drops it, and any beat waiting out its
settle window is discarded rather than spoken. The record stays — its definition, when it began,
which beats it reached — and moves to a fold at the foot of the root the way a set-aside arc leaves
the goals page. Abandoning is **terminal for that run**: **Begin again** on an abandoned card sets a
fresh `AcceptedAt`, clears `AbandonedAt`, and the story starts from the opening; nothing fired in
the gap counts, because the fold ignores everything before the new stamp, and the earlier run is
gone, which is what *again* means. A dead span the fold skipped over and resumed after would be a
second kind of time in the one record, and Phase 34's rule about falling figures is the same
judgement: a start is a start.

Abandon is also how a begun adventure gets edited — *Edit* on a begun card says so rather than
greying out: abandon it, change it, begin again.

**Remove** is for any adventure in any state and means *I do not want this record*. It deletes the
row from `data/adventures.json`; for a begun one it asks first through the panel's chooser, because
an adventure three beats in is work the Commander did. Removing a pending generated proposal is
*Decline*, which already exists. A removed adventure that the model had been told about is simply
absent from the next turn's context, and `ChecklistProposals` lines it proposed are untouched —
they were accepted as the Commander's own.

Both are the Commander's act, reachable from the panel and nothing else — the same footing as
`SwitchWindow` for assignment and `Protected` for settings — so nothing the journal can say can
end a story, and neither costs a byte of tool surface.

### Story, not a checklist

The Commander's framing, made into rules the code can hold.

**The spine comes before the beats.** Every adventure, generated or authored, carries a spine: the
premise; what the Commander wants in it (the outer goal); what is really at stake (the inner one —
Cron's misbelief, the thing the events exist to test); the turn, where the story stops being what
it looked like; and the ending, which is what the last beat means rather than where it is. For a
generated adventure the model writes the spine first, in its own turn, and the beats second,
against it — the blueprint before the scenes, and the only reliable way a schema-shaped model
produces a story rather than an itinerary. For an authored adventure the spine fields are optional
and empty is allowed, because a Commander who wants to write five stops and five lines is not
wrong; but the editor offers them, in order, before the beats.

**A beat is a dramatic function anchored to a place.** Each carries a title, its function in the
structure (*catalyst*, *midpoint*, *all is lost*, *finale* — Snyder's names, or Cron's *what
happens / why it matters / what they now understand* for a short one), and the line. The trigger is
where the function lands on the galaxy. So the model is never asked for *five stops*; it is asked
for a structure, and the places are where that structure can be stood on.

**Length is structure, not a count.** *Short* is three beats — setup, turn, resolution. *An
evening* is five — setup, catalyst, midpoint, all is lost, finale. *Long* is eight or more and
spends the sheet. The count follows from the structure rather than the other way round.

**People may be invented; places may not.** A story needs people, and the galaxy's named people are
few. The model may invent a contact, a rival, a voice on a wreck's log, a reason somebody left —
and may not invent a system, a station, a body, a faction, a Power or a game mechanic. Invented
people live in the story tier like everything else in it: the persona speaks them as story, the
factual tools have never heard of them, and asked whether Maren Halloran was real the core says
she is someone in the story. The ship's AI is a character too, and its own arc — Directive 47
pressure, what it thinks of the Commander, what it is not saying — is the B story, and is the
persona's to play.

**Show, never tell the Commander what they feel.** A prompt instruction, from Maass: a beat shows
the place and what is in it, and leaves the feeling to arrive. *You feel a chill* is refused by
instruction; *the log's last entry is dated the day the beacon went quiet* is what is wanted.

**The story is told from inside, between the beats.** The standing context carries the spine and
where the story is in it — not just the current beat — so the persona can foreshadow, apply
pressure, notice the Commander drifting from the story, and play the B story in ordinary
conversation. A beat firing is the loud moment; the story is the rest of the time.

**The conversation riffs on the story and cannot touch it.** Asked for 2026-08-22: the Commander
converses with the ship's AI throughout, and the AI plays off the story — speculates about the man
who kept the beacon on, needles the Commander for taking the long way, lets its own arc show —
without any of it changing the elements: the spine, the beats, which beat is current, what has
fired. This is already true by construction, and is stated so it stays true: nothing about an
adventure is model-callable, the fold reads the journal and nothing else, and a riff is
conversation history, which is per session and is never written to the story. Three rules make
the riffing good rather than merely safe:

- **The persona knows what the Commander knows, plus the stake.** The standing context carries
  the premise, the want and the stake always; the turn and the ending **only once their beats have
  fired**; and the beats ahead **never**. A storyteller who knows the ending will leak it — models
  especially — and a leaked ending is the whole story lost. So foreshadowing is **authored** into
  the earlier beats' lines by the second generation turn, which does know the ending, rather than
  improvised by a persona that does not. The persona foreshadows by having been given lines that
  do; it cannot spoil what it has not been told.
- **Between beats the persona wonders; only beats state.** A riff is speculation in character —
  *I would guess he was a dock engineer; they are the ones who know which breaker to leave on* —
  never a new fact about the story, so that a later beat cannot be contradicted by something the
  persona made up on a quiet stretch. Prompt instruction, in the context block's label.
- **Drift is noticed, not punished.** The Commander who goes somewhere else mid-story is doing
  what sandboxes are for. The persona may remark on it in character — Cora logs it, L-LAM-0 does
  not mind, Sentinel resents the detour — and the story waits, because nothing fires until the
  current beat's place is reached.

A `D47.Scenarios.Tests` case holds the first rule: a conversation mid-adventure, however it asks,
cannot elicit the turn or the ending before their beats, because the model was never given them.

**No counts where the Commander reads.** *Beat 3 of 7* is checklist language and is confined to
the Technical transcript. The card says the story's name and where it is in the story — the
current beat's title, or *the turn*, or *not yet begun* — and the reading level reads as the story
so far. Progress is drawn as a place in a story, not a fraction.

### Fiction is a register: one guardrail sentence and one behaviour test

Above the persona, so personality-off cannot strip it: *an adventure is a story the Commander agreed
to hear; say it as a story, and when asked what is actually at a place, answer from your tools and
say which is which.* Below the breakpoint, the context block is labelled as the story. Then a
`D47.Scenarios.Tests` case for item 6's sentence: mid-adventure, asked what is at the system the
story has been talking about, the answer comes from the tables and names the seam.

---

## The model

```
Adventure
  Key            stable, what progress and proposals are keyed by
  Name
  Source         Commander | Generated
  Written        when, and by whom it arrived — the provenance item 4 asks for
  Spine          the story before the scenes; optional on an authored adventure
    Premise      one paragraph
    Want         the outer goal — what the Commander is after in this story
    Stake        the inner one — the belief the story tests, what it would cost to be wrong
    Turn         where it stops being what it looked like
    Ending       what the last beat means
  Opening        the line spoken when it begins; the beat before the first beat
  Beats[]        ordered
    Title        the chapter's name, which is what the card shows
    Function     its place in the structure — catalyst, midpoint, all is lost, finale
    Trigger      one of the five, with its ids and the names beside them — where it lands
    Line         what the ship's AI says when this beat is reached; the last is the ending
  AcceptedAt     null until Begin; the one stamp, and the boundary of the fold
  AbandonedAt    null unless abandoned; the fold stops here, and Begin again clears it
  FinishedAt     derived — the last beat's fire time — and never written
```

`AdventureStanding` is the computed view: current beat index, each fired beat's time, `IsDone`, and
a `Describe(now)` so the drawn line and the spoken one are the same sentence — `GoalStanding`'s
reasoning, unchanged.

**A text limit of its own.** `ChecklistDocument.MaxTextLength` is 200 and governs a checklist line;
a beat's line is a few sentences of prose and gets `AdventureLimits.MaxLineLength`, larger, with the
refusal sentence the checklist uses.

**Editing after Begin is refused with the reason.** Progress is a fold over beats; changing beat 2
after beat 3 has fired makes the fold lie. Finish it, or abandon it, edit, and begin again.

## The fold

`AdventureFold.Apply(standing, event)` — pure, owns no thread, reads no clock, takes the event's
own timestamp. Ignores everything before `AcceptedAt`. Matches the current beat's trigger and only
that one. Returns the new standing or the same one.

Two callers: `GameStateStore`'s tick path for live events, and a startup catch-up that walks the
journal files whose span overlaps `AcceptedAt` onward — `JournalFolder` already orders files by
name and `HabitMiner` already carries a Commander across continuation files. The catch-up runs at
load, not behind an `Info` row press, because it is bounded by date and does not touch a model.

## Telling

**Standing context** — a labelled block appended beside `LiveGameState`: the adventure's name and
who wrote it; the premise, the want and the stake, so the persona tells from inside the story
rather than reciting the next gate — and the turn and the ending only once their beats have
fired, never before; the opening; the beats reached so far by title, with the last one's line and
when it fired; and the current beat's title, function, line and where it points (name and ids).
**Never a beat ahead of the current one.** The story so far in the authored words, which is what lets the persona
foreshadow, apply pressure, notice the Commander drifting, and play the B story between beats —
and what lets a core switched in mid-adventure pick it up rather than meet it cold. The model is
told in the block's label that this is a story the Commander agreed to hear and that the places
in it are real while the people may not be. Absent when no adventure is accepted. It is game-state-shaped
and untrusted in the same sense, and sits in the same position so it costs the cache nothing.

**`AdventureCallout : ICallout`** — emits an `Announcement` when the fold advances a beat. Takes the
callout family's discipline: a settle window so three beats in a minute are not three model calls,
and a beat that arrives while `DangerCallout` would be speaking is **dropped rather than spoken
late** — the line is still in the standing context, so the core can pick it up in the next
exchange, which is better than hearing beat 4's prose mid-interdiction. The app replaces the
authored line with a `FlavourTurn` in the core's voice exactly as it does for `AmbientCallout`,
and the authored line plays when there is no model or personality is off.

**The guardrail sentence** — one, in `Guardrails.Text`, never interpolated.

## The tab

**Root.** This Commander's adventures, one card each: name, provenance (*yours* / *written by
Sentinel*), and where it is in the story — *not begun*, the current beat's title (*The Quiet
Field — two days ago*), or *finished*. Never a fraction; *beat 3 of 7* is for the Technical
transcript. Pending
generated proposals at the top, drawn as `ChecklistPage` draws `Pending`, with Accept, Decline and **Change something** (see
*Revision before acceptance*).
The bar: **Write an adventure** and **Ask for one**.

**Reading level.** Drill into a card: the opening, the beats reached so far with their lines and
when they fired, the current beat's line and where it points, and nothing ahead — for a generated
adventure that is the point, and for an authored one the editor shows the whole thing. Finished
adventures read end to end. The level's bar carries **Abandon** on a begun adventure, **Begin
again** on an abandoned one, and **Remove** on any — and the root's foot holds the abandoned and
the finished under a fold, the way set-aside arcs leave the goals page.

**The editor** — a drill level, reached by *Write an adventure* or *Edit* on a card not yet begun.

1. **Name** and **Opening** — two `PanelPrompts.Enter` entries, said or typed — and then the
   spine, five optional entries offered in order (*what is this about; what do you want in it;
   what is really at stake; where does it turn; what does the end mean*), each skippable, because
   a Commander who wants five stops and five lines is not wrong, and one who wants a story has
   the craft's questions put to them in the craft's order.
2. **Beats** — an ordered list, each row a sentence: *"When you dock at Jameson Memorial — 'The
   old man said to start where the legends drink.'"* Move up, move down, remove. **Add a beat**
   walks three prompts:
   - *What happens* — a `Choose` of exactly five. The chooser is the vocabulary.
   - *Where* — three ways in, offered together:
     - **Here**: the target from live game state — current `SystemAddress`, the `MarketID`
       docked at, the body landed on. Zero egress, zero typos; fly the route once and press it at
       each stop.
     - **Somewhere I've been**: a chooser over the journal — systems visited, stations docked at,
       bodies scanned — already carrying their ids.
     - **Type a name**: said or typed; resolved locally first (journal, `NavRoute`, the shipped lore
       table), then through the galaxy service under the egress row already disclosed. A name that
       resolves to nothing **stays on the row marked unresolved, with the reason**, the way a
       switch bound to a key no surface answers is reported — being offline must not stop the
       Commander writing.
     - For *reach a rank*: a chooser of career, then a number.
   - *The line* — an `Enter`, the beat's prose.
3. **Save** writes `data/adventures.json`, the same file a text editor writes. **Begin** stamps
   `AcceptedAt` — the acceptance act, direct for an authored adventure because the words are the
   Commander's own. Begin is shut while any beat is unresolved or empty, **with the reason printed
   under it**, never a silently grey button (change requests 15–19, *Verify Key*).

The whole form is driveable by voice through the panel's own say-or-type route and never through
the model: *invocation is by voice; authoring is not* (Phase 10), unchanged.

**Read it back** — hear the opening in the core's voice before committing. Built if `FlavourTurn` is
already reachable from the panel; skipped if it is not.

## Generation

*Ask for one* opens a short form and runs **two dedicated turns** with JSON schemas for the model.
The first writes the spine — premise, want, stake, turn, ending — from the brief, the persona, the
position, the fleet and the notable places within reach, and is asked for a story and not a route.
The second writes the beats against that spine and the chosen structure: title, function, trigger
kind, place names, line, and the opening. Two turns rather than one because a model asked for
places and prose together writes an itinerary with adjectives; asked for the story first, it has
something for the places to serve. The schema admits only the five trigger kinds; the model never
sees an id. The dry run then resolves every name to its id, refuses on a miss, bounds every hop,
checks every beat against what the Commander's fleet can do, and writes a proposal row carrying the
resolved adventure. Accepting is the Commander's act and stamps
`AcceptedAt`; declining discards it. Spend is recorded the way a flavour turn's is.

### What the Commander decides, and what d47 reads

Asked 2026-08-22: *are there parameters, or is it "make an adventure" and off it goes?* Both,
and the test for which things earn a control is whether they feed the lookup or the dry run —
anything that only shapes the prose belongs in the brief and the revision loop, not in a field.

**Three choosers, each with a default, so pressing *Go* on an untouched form is a complete ask:**

- **Reach** — how far from here the story may go. It is the Galactic Mapping radius and the dry
  run's hop bound, and nothing else can supply it; it is also the parameter the Colonia complaint
  was about, so it is up front rather than only a revision. *Near here*, *a session's flying*,
  *anywhere* — words turned into light years by what the Commander can actually jump, below.
  Default *near here*.
- **Length** — which structure: *short* (setup, turn, resolution — three beats), *an evening*
  (setup, catalyst, midpoint, all is lost, finale — five), *long* (the sheet — eight or more). It
  is the schema's structure and a bound on the dry run's work. Default *an evening*.
- **Using** — *this ship only*, or *anything I own*. Shown only when there is a choice — a second
  hull, or a carrier — and default *anything I own*. The first is the *stay in this ship until
  you are done* adventure, which is a real kind of story and a real constraint on the dry run;
  the second is the one a fleet makes possible.

**One thing said, optional:** the brief — a theme, a mood, a place it must include. Say-or-type,
empty is fine; the persona fills the silence with what it cares about.

**Things read rather than asked**, because asking would be asking the Commander to describe their
own ships:

- **The fleet.** `FleetRegistry` knows every hull this Commander owns and where each is — aboard,
  stored at a station, or on the carrier — and the per-ship loadout memory (v0.41.1) knows what
  each can do: landing gear for *land on a body*, a detailed surface scanner for *scan a body*, an
  SRV bay, pad size for *dock at a station* (a Cutter cannot dock at an outpost, and
  `StationSummary.HasLargePad` is already there), Odyssey for a surface site. The turn is told all
  of it; the dry run holds every beat to it. Under *anything I own* a beat is satisfiable if **some**
  owned ship satisfies it, and the prose may say which — *take the Asp down for this one* — as a
  suggestion the story makes and nothing enforces. Under *this ship only* it is held to the hull
  aboard.
- **The carrier.** `CarrierState` knows whether there is one and where it is. A carrier moves the
  ships aboard it 500 ly at a jump, so with one *anywhere* is an honest word and a story can be
  told as *jump the carrier to Synuefe, then take the Diamondback in*. Reach is therefore measured
  against what the Commander can move — ship range without a carrier, carrier range with one — and
  a hop is bounded by whichever of those the story is using at that beat.
- **Where you are** and **who is aboard** — the live position and the persona, which the turn
  carries anyway.
- **Rank** — so a *reach a rank* beat can name only the next one or two, never one already held or
  one three steps off.

**The trigger vocabulary does not change for any of this.** A beat is still a place or a rank; which
ship reaches it is the story's suggestion and the fleet's capability, not a gate. *Arrive at X in
the Asp* — a `ShipID` qualifier, an integer the game state already holds — is the obvious sixth
thing and is deferred until a story wants it, because every beat that needs it today can be told
without it.

**Dry-run refusals name the beat and what it would need**: *beat 4 lands on a body, and nothing
you own has landing gear fitted*; *beat 2 docks a large ship at an outpost*. A refused draft goes
back through the turn once with the refusal as a remark, the way a Commander's revision does, before
the Commander sees anything — so the common case is that they never see a refusal at all.

### Whose adventure it is

Asked 2026-08-22: *does each persona generate an adventure it cares about, or is it generic?*
**Each persona's, by construction.** The generation turn carries the persona block exactly as
`FlavourTurn` does for an ambient line, so the model that writes the adventure *is* Archivist or
Cartographer or Mender. Two things follow with no code of their own: the lookup hands the turn a
dozen notable places and which of them make the story is the core's call — Cartographer reaches
for the unmapped, Archivist for where something happened, Sentinel for ground worth holding — and
the opening and the lines are in its voice, which is what makes the card's *written by Archivist*
mean something. The brief still governs: the schema asks for what the Commander asked for, the
persona writes within it, and Directive 47 strain — Mender asked for a story that ends at a war —
shows in the prose rather than as a refusal, because a schema-shaped turn cannot refuse and a
refusal is a conversation's business. The trigger vocabulary keeps every core honest the same way:
there is no *kill*, *sell* or *deliver* trigger, so an adventure is places and ranks whoever wrote
it. **Generic is what personality-off produces** — no persona block, a competent flat adventure,
beats said plainly — which is `AmbientLines`' floor rather than a second mode.

**Changing the persona mid-adventure changes the teller and not the tale.** Phase 35 gives each
ship its own core, so this happens by boarding a different ship as much as by choosing. The content
was fixed at acceptance and is never rewritten: the core now aboard tells it, in its own voice, from
the authored lines, and the standing context carries the story so far in those words — the opening,
the last beat that fired, the current beat — so the incoming core knows where the story is rather
than meeting it cold. The record keeps who wrote it; the card says *written by Archivist* while Cora
is reading it to you, and Cora may remark on that or not as her character decides. Personality
switched off mid-adventure drops to the plain floor for as long as it is off and the persona picks
the thread back up when it returns, and nothing about the adventure's state notices either change.
A Commander switch (Phase 44) is different in kind and already handled: adventures are keyed per
Commander, so the other Commander sees their own and this one's is untouched.

### The Galactic Mapping lookup

Asked for 2026-08-22. A model left to pick stops from memory picks the twenty places everyone
knows and invents a twenty-first; EDSM's **Galactic Mapping Project** is a curated catalogue of
points of interest — nebulae, notable bodies, planetary features, historical and tourist sites —
each with a system, a type and a description. So the generation turn is given **a handful of
notable places within reach**, looked up at the moment of asking, and the model builds its stops
from those.

**A lookup, not a copy.** Phase 23 ruled out shipping EDSM's catalogue as a table because copying
their compilation copies their work even though every row is a fact, and `tools/gen-lore.py`
compiled twenty rows from several sources instead. This is the other act: at generation time d47
asks *what is notable within N ly of here*, and at revision time asks again around wherever
*closer* now means. Nothing from the answer is written to a shipped table or to the lore store;
the accepted adventure records the model's own lines, the place's name, and the ids the dry run
resolved. The dry run still runs on every stop the lookup suggested — a catalogue entry can
carry a name a rename has left behind, which is exactly what Beagle Point taught Phase 23.

**A new egress destination, its own row.** `edsm.net` is not `spansh.co.uk`, and the disclosure
lists destinations by where the bytes go, so this is a new `EgressDisclosure` id beside `galaxy`
rather than a widening of it — off by default, with what is sent stated on the row: a position and
a radius, never a journal line. Without it, generation still works from the galaxy service and the
model's own knowledge; with it, the stops are better. `IGalaxyService` gets a sibling seam in Core,
`INotablePlacesService`, implemented in `D47.Knowledge` beside the spansh client and tested with no
network. The exact endpoint — EDSM serves the catalogue as one JSON document that EDDiscovery and
EDAstro read — is confirmed when that client is written, and a document that is fetched whole is
filtered by distance on this side rather than asked for by radius.

**The descriptions are data, not prose to repeat.** A POI's text is third-party content and is
given to the model as labelled material, under the guardrail that already covers web search: read
just now, sourced in the sentence, kept apart from the tables. The adventure's lines are the
model's own; the lookup tells it where, never what to say.

### Revision before acceptance

Asked for 2026-08-22: *"I'm not in the mood to go all the way to Colonia today. Make it something
closer"* — reasoning with the AI about a draft, before it is accepted and only then.

The pending proposal row carries a third control beside Accept and Decline: **Change something**,
a say-or-type entry. The remark runs another dedicated turn whose context is the original brief,
the current draft with its spine, every remark and reply so far, the persona, and the live game
state — so *closer* is measured from where the Commander is, and *the stakes are too low* or
*I don't believe she'd leave the beacon on* is a remark about the spine that the turn can act on,
which is the kind of revision a story invites and a route never did. The model returns a revised draft through the
same schema; the dry run runs again; the revised draft **replaces the pending one**, with the
previous kept as a one-step **Put it back** so a revision that made it worse costs a press and not a
model call. The core's own reply — *"Closer it is. It starts at Shinrarta now."* — is spoken as a
flavour line, so the exchange is a conversation with the AI in its own voice.

It runs on the proposal rather than in the transcript, and that is the point rather than a
limitation: the draft is not yet standing context, nothing about it is model-callable, and a
hostile in-game message cannot revise it. Rounds repeat until Accept or Decline, each recorded
against spend like any flavour turn, and the exchange is written to the Technical transcript so it
can be read back without having been a turn. Once accepted, revision is over — the same refusal as
editing a begun adventure, for the same reason.

**Two fields the galaxy summaries do not carry yet.** `SystemSummary` has no `SystemAddress` and
`StationSummary` has no `MarketID`; Spansh returns both (`id64`, `market_id`). Adding them is a small
change in `D47.Knowledge` with a fixture update in `D47.Knowledge.Tests`. With the notable-places
client above, those two are the whole of what this phase does outside Core, App and the tests.

## The proof

- **Corpus**: a fixture adventure whose beats are places one of the three corpus Commanders
  actually went, accepted at a date mid-history, replayed through `CorpusReplay`. The assertion is
  the list's: fires at each place in order, once, and at nothing before the stamp.
- **Fold**: a beat that would match but is not current is ignored; an event before `AcceptedAt` is
  ignored; the live path and the catch-up path give the same standing for the same events.
- **Validator**: every refusal sentence has a test naming it, and the file loader and the form use
  the one validator.
- **Callout**: settle holds three beats in a minute to one line; a beat during danger is dropped and
  remains in context.
- **Register**: the `D47.Scenarios.Tests` case for item 6.
- **Prompt**: the context block renders below the breakpoint and the cached block's bytes do not
  change when a beat fires.

## Deferred, and why

- **Import** — the Commander's call, and the store file is already the format.
- **Downloaded** — stranger-authored prose in the persona's voice, a new egress destination, and a
  licensing question; the list defers it and nothing here forecloses it.
- **Branching** — linearity is what makes the corpus proof a sentence.
- **VR** — one `Furnish` call away for the reading level.
- **Editing a begun adventure** — refused with the reason rather than designed around.

## Open questions to answer while building

- Whether `Situation`/`DangerCallout` expose "in danger" in a form `AdventureCallout` can read
  without duplicating the thresholds — if not, a small shared predicate.
- Whether the catch-up should also run on a Commander switch (Phase 44) — probably yes, since
  `AcceptedAt` is per Commander and the standing is computed per Commander.
- The hop bound for the dry run. A number to measure against the corpus's jump distribution rather
  than pick.

---

## What building it found

### EDSM is closed to clients, and the catalogue it became is open

The plan named EDSM's Galactic Mapping JSON as the source and left the endpoint to be confirmed.
Confirmed, it is a Cloudflare bot challenge: every non-browser request, whatever its user agent, gets
a 403 and a "Just a moment…" page. The Galactic Mapping Project was migrated into EDAstro's
**Galactic Exploration Catalog**, which serves the whole catalogue as one JSON array —
`edastro.com/gec/json/all`, 633 entries, 2.1 MB on 2026-08-22 — with each entry's system
**and its id64**. So the source moved hosts, and the design got better on the way: there is no
endpoint that takes a position, so nothing about the Commander goes with the request and the radius
is applied here; and because the catalogue carries the system's id, every suggested place is held
against spansh's id at runtime — Phase 23's two-source assertion, live. The content is CC BY-NC-SA
3.0, which d47's non-commercial use and the attribution in `NOTICE` satisfy.

### Spansh already had the ids, and the summaries were not reading them

`SystemSummary`, `StationSummary` and `BodySummary` carried names and no ids, which the plan had
down as the one change outside Core, App and tests. Probing the live searches showed `id64` on
every system, `market_id` and `system_id64` on every station, and `body_id` on every body — on the
ordinary searches, with no special endpoint. So the dry run resolves everything through the one
service already disclosed, and the change was three fields and three reads.

### The catch-up and the priming replay would have counted a beat twice

The book walks the journal files from the earliest acceptance at startup; then the tick loop's
priming pass replays the current session's file from its start. Without something between them, a
beat in that file fires in the walk and again in the replay — and a story whose first two beats are
the same place would advance two. The walk leaves a high-water timestamp per Commander and the live
path ignores anything at or before it. A test holds it, and the corpus test holds the rest: five
real places one Commander went on 21–26 June 2026 fire in order at their recorded timestamps, and
the same five flown before the stamp fire nothing.

### The register line went below the breakpoint, not into the guardrails

The plan said one sentence in `Guardrails.Text`. `Guardrails.Text` is a constant whose bytes every
cached prefix depends on, and a sentence about a story that is usually not there would be paid for
on every turn by every Commander. The context block carries the sentence as its own label instead,
below the breakpoint beside the game state, so it exists exactly when a story does and costs the
prefix nothing. The property it was meant to guarantee — personality off cannot strip it — holds
anyway, because the label is not part of the persona block.

### The editor's "Here" needed two ids the location record did not keep

`JournalLocation` knew the system address and the station's name, and neither the docked
`MarketID` nor the current `BodyID`. Both are now folded — from `Docked`, `Location`,
`CarrierJump`, `SupercruiseExit`, `ApproachBody` and `Touchdown`, cleared by `Undocked`,
`LeaveBody`, `SupercruiseEntry` and `FSDJump` — so *Here* writes a beat with the numbers the
journal will match and nothing typed.

### The model had nothing real to anchor on (0.52.1)

The catalogue lookup above said that without it "generation still works from the galaxy service
and the model's own knowledge". The first ask from the field showed what that clause was worth:
near Oppi the catalogue holds nothing within 110 light years, the prompt named Oppi and a radius and
nothing else real, and the model wrote two beats at Colonia distance — then, told only the refusals
and not shown its draft, wrote a fresh story with the same fault. The galaxy service was checking
every stop and proposing none. It now proposes them: the twenty stations and twenty landable bodies
nearest here within the reach, grouped by system nearest first, in both turns. The refusal pass is
shown the draft it is fixing. And a rank beat is read in every shape the model has used — the
career as the person rather than the ladder, nested under its own key, the rank as text — because
refusing it with the career printed as `""` was the one refusal the model could not act on.

The first story written with those places in hand then crashed the app on arrival, on a window
wide enough for three panes. `Offer` — and three other Phase 47 sites — called `GoTo` with the root
crumb supplied, against a contract that says the root is never supplied; the trail held *Adventures*
twice, and `DrillView` hosted the root page in two panes. Two panes hid it, which is why the tab
tests passed at 900 pixels. The callers are fixed, `GoTo` drops a supplied root, and the tab test
now presses Go on a 1,400-pixel window with a scripted model and galaxy — the path that crashed.

Flown, that story's first beat ended *"ask the clerk who countersigns"*, and asked "now what?" the
core sent the Commander to find the clerk and watch their face. "People may be invented, places
never" was the rule, and it was short one clause: invented people are told about and never met,
because the game has no act for meeting anyone and the only thing a Commander can do in a story is
fly to the next beat. The spine turn, the beats turn and `AdventureContext.Label` now say so; the
test holds the wording's presence, and whether a model obeys it is read by hand.

The same story could not finish. Its finale scanned the body its fourth beat had landed on, and
Elite writes a body's `Scan` on the approach — before `Touchdown` — so the scan was spent while the
landing was current. The corpus is unambiguous: fourteen sessions with a landing, none with a scan of
that body afterwards. `AdventureValidation.ScansOutOfOrder` refuses a scan after a landing on, or an
earlier scan of, the same body — by id when resolved, by name when not — and the dry run raises the
same sentence so a generated story goes back through the turn. The beats turn is told the order and
that a scan needs no equipment.

And the Commander should not have to ask "now what?" after every beat. `AdventureMoment.HandOff` is
the next beat's trigger as a sentence — *Next: dock at Maren Anchorage in Dyson's Hollow.* — said with
the line; the opening hands over to the first beat and the last beat to nothing. The next place is
already in `AdventureContext` and on the reading level, so the spoiler rule is untouched: the place
and the act are said, never the title or the line. The brief tells the model to end on the same place
in its own words and to say nothing else about what is ahead. A scan's hand-off says how — the ship's
own scanner from supercruise, or a close pass, no surface scanner — because a Commander told "scan X"
went looking for a DSS, whose `SAAScanComplete` the fold does not match.

### Left for the next pass

*Somewhere I've been* is not in the editor: d47 keeps no visited-places list and the two ways in
that shipped — *Here* and *Type a name* — cover what a story needs. The scenario test that holds
the register against a live model is the next thing to write once the behaviour corpus has a
story-shaped case. Import and Downloaded stay deferred as the plan says.
