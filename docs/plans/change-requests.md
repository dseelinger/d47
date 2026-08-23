# Change requests

Wanted changes that are not defects. **Bugs are not here** — those are in
[bugs.md](../../bugs.md). Everything here behaves as built; the request is that it be built
differently.

An entry leaves this file when it ships, and the line it gets in [CHANGELOG.md](../../CHANGELOG.md)
under the release that carried it is its permanent record.

An entry states what is wanted and where the code is. Where one carries an **open question** that
changes the work materially, it says so — those want an answer before the code does, because the
answer is usually the difference between two different pieces of work rather than a flag.

Where an item contradicts a comment in the source, that is called out. Those comments are the
reasoning being overturned, and leaving one standing beside code that no longer obeys it turns
the file into a liar.

**Numbers are not reused.** Items cite each other by number, and reusing one would leave an old
citation resolving to a live entry about something else, reported by nothing — the trap the
phase-renumbering rule in [CLAUDE.md](../../CLAUDE.md) exists to name. The next batch starts
at 20.

---

## Open

### 32. Sort the checklist by what the engineer in this system can do

Asked for 2026-08-23.

> I must be able to sort the Checklist by items that can be fulfilled by the engineer that is in
> the current system when asked (or indicated in the UI) in addition to the other sort options for
> the checklist.

**The hard half is already built and is not the sort.** `EngineerAtHand.For(...)` answers exactly
this question today: given the Commander's system, it returns each engineer there with **`Ready`**
— the open items they could roll now — and **`OutOfRank`** kept separate, because those are two
different errands. It is what produces the arrival line *"Lei Cheung is here, at Trader's Rest, and
can do 52 items on your list."*

So this item is a **projection of something already computed**, not new analysis. What is missing
is a way to see it as the list rather than as a sentence.

**Two shapes, and they are not the same feature.** Worth settling first, because the ask says
"sort" and the useful thing may be the other one:

- **A sort** leaves every line on the page and moves the reachable ones to the top. Nothing is
  hidden, which fits a list a Commander has already ordered by hand — and `ChecklistDocument`
  carries a `ProjectOrder` the Commander owns, so a sort has to say whether it overrules that
  order or nests inside it.
- **A filter** shows only what this engineer can do, which is what somebody standing on the pad
  actually wants and is nearer to what *"items that can be fulfilled"* says. It is also the one
  that can show nothing at all, and a checklist that looks empty is alarming in a way a re-ordered
  one is not.

**Three things to decide beyond that.**

1. **`Ready` only, or `OutOfRank` too?** They are already separate for a reason. Showing both in
   one group would undo that; showing `Ready` alone hides a real answer to *why can I not do this
   here*. A two-band ordering — can do now, then could after a rank — is probably right and is
   more than a sort key.
2. **What happens with no engineer here**, which is the overwhelmingly common case. The mode has
   to say *"no engineer in this system"* rather than presenting an empty or unchanged list, or it
   reads as broken every time it is used away from a workshop.
3. **More than one engineer in the system.** `For` returns a list, not one, so the ordering is
   over the union — and a line only one of them can roll should probably say which.

**Where it goes.** The spoken half joins the ordering vocabulary CR 20 shipped in 0.45.0; the drawn
half is the Checklist tab, which already has the Commander's own order and a search box, so this is
a third way of arranging the same page rather than a new surface. Both routes must reach it — a
Commander at a workshop has their hands on the stick.

---

## Shipped

### 27. A sold ship leaves its checklist behind — shipped 0.59.0

Asked for 2026-08-23.

> What happens to checklist items when you sell a ship? I should be able to either A) delete all
> checklist items associated with an existing (or previously existing) ship. This is the usual
> scenario. Or B) put everything back to "Open" and add a "Purchase X ship", too.

**Today the answer is "nothing happens", and nothing is the wrong answer.** `ShipyardSell` is read
in two places and neither is the checklist: `FleetRegistry.cs:99` drops the hull from the fleet, and
`ShipLoadouts.cs:82` forgets its remembered modules. The checklist has no `Sell` anywhere in it, so
every `ChecklistScope.Ship(<sold id>)` line survives the sale — a build for a ship the Commander no
longer owns, sitting among the ones they do.

**What that looks like on the page**, because it decides how visible the problem is. The lines are
not hidden: `ChecklistEvaluator.IsActive` (`ChecklistEvaluator.cs:544`) only asks whether a
ship-scoped line is about the ship being *flown*, which sorts the sold ship's lines down rather
than out. And their wording degrades the moment the sale lands — `ChecklistWording` resolves a slot
against the remembered loadout, which `ShipLoadouts` has just deleted, so they fall back to the
stored form (`Anaconda (ship 51)`). Honest, and still a list of work for a ship that is gone.

**A and B are two different features, not two spellings of one.** Worth settling before any code:

- **A — forget it.** Delete every item in that scope. The usual case, in the Commander's words,
  and the cheaper of the two: it needs the sale event, the scope key, and a decision about whether
  it is silent or asks first.
- **B — put it back on the shelf.** Reset the items to *Open* and add a **"Purchase X ship"** item
  above them, so a build survives selling the hull it was for and comes back when the hull does.
  That is a real thing a Commander does — sell an Anaconda to fund a Cutter, buy the Anaconda again
  a month later — but it needs a scope that outlives a `ShipID`, which the current one does not.

**Three things to answer, and the third is the one that could sink B.**

1. **Silently, or asked?** A sale is unambiguous and irreversible from d47's side, but deleting a
   plan somebody spent an evening on is exactly the shape the Phase 38 banner exists for — *"you
   sold the Anaconda; the eleven items on its list are yours to keep or clear"*, answered on the
   Checklist tab. That keeps A and B as one implementation with the Commander picking per sale,
   which may be the whole answer.
2. ~~**Is a rebuy the same ship?**~~ **Answered 2026-08-23, and it settles the item: `ShipID` is
   reused.** Measured across the corpus — of 55 distinct ships sold, **17 had their id come back
   alive afterwards**, the clearest being `ShipyardNew` on ship 42 three days after ship 42 was
   sold. So **B is unbuildable as specified**: a reset list keyed on `ShipID` would silently attach
   itself to whatever ship next holds that id, which is the failure this question existed to catch.
   That also makes the timing non-negotiable — the items must leave that scope **at the sale**,
   because waiting is what lets a reissued id capture them.
3. **What about a ship destroyed rather than sold?** Insurance rebuys the same hull and, as far as
   the journal is concerned, it is the same `ShipID` — so the checklist should not react at all.
   Whatever reads the sale has to be sure it is reading a sale.

**Where it would go.** `ChecklistService` already polls the journal and already writes news for the
callout, so this is a case in that poll rather than a new reader. The banner-with-a-question shape
is the one Phase 38 built and Phase 42's *"pressing a tab goes to that tab, even with a question
up"* ruling already covers.

---

### 29. Say something when a game starts and when it ends — shipped 0.59.0

Asked for 2026-08-23.

> D47 should react to when you get into and leave a game, but have a cooldown of 30 minutes for
> each reaction (so as not to become annoying during re-logging).

**This is not the line d47 already says.** `ContinuityCallout` is the opening line of a **d47**
session — *"Good evening, Commander. Ready to go."* — timed off the first live tick and said once
per run of the app. What is asked for here is a reaction to the **game**, which d47 can be running
either side of, and which a Commander enters and leaves several times an evening.

**Getting in is `LoadGame`. Leaving is `Shutdown`, and it is not always there.** Measured across
the local 925-journal corpus on 2026-08-23:

| | Count | Note |
|---|---|---|
| Journals | 925 | |
| `LoadGame` events | 1,211 | 1.3 per journal |
| `Shutdown` events | 841 | |
| Journals with **no** `Shutdown` | **84 (9.1%)** | crash, kill, or still running |
| Journals with **no** `LoadGame` | **147 (15.9%)** | Elite rotates the file mid-session |

Two things follow and both are load-bearing. **Roughly one session in eleven never says goodbye**,
so the leaving reaction must be allowed not to happen rather than reconstructed from silence — a
timeout that guesses at a departure will eventually say it while the Commander is flying.
And **the journal file is not the session boundary**: 147 files contain no `LoadGame` at all, so
neither reaction can key off a new file appearing.

**`Shutdown` is currently read nowhere in Core.** The only match in the source is a module name in
`OutfittingCatalogue`. `LoadGame` is read in six places — identity, the miners' boundaries, the
session summary — and none of them is a reaction. So the leaving half is genuinely new and the
arriving half is a new reader on an event already parsed.

#### The cooldown, and the corpus agrees with the number

Of 433 consecutive `LoadGame` pairs within a journal:

| Gap | Count | Share |
|---|---|---|
| under 5 minutes | 120 | 28% |
| under 30 minutes | **248** | **57%** |
| 30 minutes or more | 185 | 43% |

Median gap **21.2 minutes**, longest 12 hours. So a 30-minute cooldown suppresses a **majority** of
second-and-later logins while still reacting to a gap that is a real return — and the median re-log
falls just under the line, which is the outcome the request is after. **The 30 minutes is a good
default and should be a settings row rather than a constant**, on the same reasoning
`AmbientCallout.Interval` is one: a Commander who wants a talkative companion turns it down, and
zero silences it.

#### Where the code is, and what it should reuse

This is a **callout**, not an autonomous action — it presses nothing, so it takes the callout
family's settings shape, cooldown and precedence rather than a protected row, which is the
reasoning `ContinuityCallout` and `LoreCallout` both record.

- `AmbientCallout` is the shape to copy for the cooldown: `Interval`, `_lastSpokenAt` seeded on the
  first tick, and `Enabled()`.
- **`context.IsPriming` is not optional here.** d47 starting reads a backlog that contains every
  `LoadGame` of the day; without folding it, launching d47 after an evening's flying would announce
  an arrival that happened four hours ago. It is the same guard the ambient line uses, and the same
  trap `AdventureCallout` documents.
- `FlavourBriefs` is how the line reaches a core's own voice, with personality-off saying it plainly.

#### Open, and worth settling before code

1. **One cooldown or two?** The request says *"a cooldown of 30 minutes for each reaction"*, which
   reads as one per direction. A shared one would mean a quick exit-and-return costs the arrival
   line as well, which may be exactly right — a re-log is one event, not two.
2. **Does leaving get said at all if d47 is about to be quiet?** A goodbye said to an empty room is
   harmless, but it is also the moment a Commander is reaching for the headset strap. Worth
   knowing whether the intent is a spoken line or a transcript entry.
3. **What about the Commander switching?** `LoadGame` carries identity, and a second Commander
   logging in is arriving for the first time rather than re-logging — so the cooldown is probably
   per Commander, keyed the way Phase 44's per-Commander projection already keys things.
4. **Should the arrival line say anything about the gap?** Phase 31 deliberately took the gap,
   the list and the engineer *out* of the resume line on the Commander's instruction. Whatever
   this says should stay on the right side of that ruling.

### 30. Say "it" when it has just said the name — shipped 0.59.0

Asked for 2026-08-23.

> When a system name has been read recently (and it was the last one read), it should not be
> repeated ad nauseum. Refer to it as "it", "that system" or whatever makes sense to you. Hearing
> "Scorpii Sector BB-O a6-2" repeated 4 times is annoying. It's why we have pronouns.

**The condition is in the ask and it is the whole design.** *Recently* **and** *the last one read* —
not merely recently. A pronoun that reaches back past a second system is worse than the repetition
it replaces, because the Commander cannot tell which one it means and has no way to find out from
a voice line. So the rule fires only when the name about to be said is the same as the last system
named, with nothing else named in between.

**Seven callouts speak a system name**: `ArrivalCallout`, `CarrierCallout`, `EmissionCallout`,
`FuelCallout`, `LoreCallout`, `RivalTerritoryCallout`, `RouteCallout`. Four of them firing around
one arrival is exactly the reported four.

#### The seam already exists, and it is one place

`AppHost.SayAsync` is the single point every callout passes through, and it **already separates
what is heard from what is written** — `Announcement.Text` is spoken, `Transcript` and
`ConversationLine` are drawn, and the class documents that the two want different things. So:

**The voice gets the pronoun and the page keeps the name.** That is not a compromise, it is the
better answer: a Commander who scrolls back can always see which system "it" was, and nothing is
lost from the record. It also means the rewrite is one function at one call site rather than an
edit to seven callouts.

#### What is worth measuring, and is not

`Scorpii Sector BB-O a6-2` is **24 characters**; *it* is two. Voices are billed per character and
the arithmetic is real but small — the reason to do this is that it is **irritating**, and the
saving is a side effect worth mentioning once and not designing around.

`SystemName.IsProcedural` already tells a generated name from a handcrafted one, which raises a
genuine question rather than answering it: *Sol* is three characters and pleasant to hear, and a
Commander may well want it said every time. **Whether the rule applies to every name or only to
procedural ones is open**, and it is the difference between a rule that is always right and one
that is right about the case that prompted this.

#### The model half is a different mechanism

Most callouts are assembled in Core and said as written, and those can be rewritten deterministically.
Some route through `FlavourBriefs` and are re-voiced by the model, which will happily expand
whatever it was handed back to the full name — so a Core rewrite is silently undone on exactly the
lines with the most personality in them. That half is a **line in the brief**, not a substitution.

And a model *turn* — an answer to a question — is generated text that must not be regexed after
the fact: replacing a name inside a sentence the model built is how *"it is 40 light years from it"*
gets said out loud. Prompt instruction there, or nothing.

#### Open, and worth settling before code

1. **How long is "recently"?** A window in minutes, or "until something else is named", or both.
   Both is probably right — the referent should expire, or a callout twenty minutes later says "it"
   about a system the Commander has stopped thinking about.
2. **Every name, or only procedural ones?** See above.
3. **Which pronoun?** *It*, *that system*, *there* and *here* are not interchangeable —
   *"there is 40 light years away"* reads fine and *"it is 40 light years away"* reads better, and
   arriving somewhere makes *here* correct where it was not a moment before. This probably wants a
   small closed set chosen by the shape of the line rather than one word substituted everywhere.
4. **Does the first mention in a line always survive?** Within one line, the second and later
   occurrences are the ones to replace; across lines, all of them, provided the referent held.
   Getting this backwards produces a line that opens with a dangling *it*.
5. **What resets it?** A jump names a new system, so that is one. Whether asking a question about a
   different system also resets it is a decision — the Commander said the other name, but d47 did
   not.

### 31. What the Commander said, and d47 denying something it can do — shipped 0.59.0

Asked for 2026-08-23, in two parts and then a third that turned out to be the interesting one.

> * The commander's "Ask" or STT should appear in the Technical sub-tab
> * Voice Command "Set Elite to Front" or "Set game to front" or "Put Elite in front/focus" or
>   "put elite/game in front" should be the same as "Set focus to Elite"

> It should definitely not say (from technical tab): *I have no tool to bring the game window to
> front, Commander. That's yours to do — Alt-Tab or the taskbar.* `[10:52:15] Answered.`

#### The third quote is a different bug from the second, and a worse one

**d47 has that tool.** `FocusCapability` raises Elite to the foreground and has sixteen spoken
phrases. What it also has is `Protected = true`, which keeps it off the advertised tool surface on
purpose — a model that can pull the game window over whatever the Commander is doing is a model
with a hand on the desktop, and the capability's own comment records the reasoning.

So the model's sentence is **true from where the model sits and false about d47**. The phrase missed
the router, the question fell through to the model, and the model answered honestly about a surface
that deliberately does not include this. The Commander is told that a feature they are using does
not exist.

**That will recur, and adding four phrases does not fix it.** `Keywords = Phrases` here, so the
router's whole vocabulary for this capability is those sixteen strings, and Commanders will keep
inventing a seventeenth. Two shapes worth weighing, and this is the decision the item turns on:

- **Let the model know a protected capability exists without being able to call it.** It could then
  answer *"I can — say 'focus the game'"* instead of denying it. That is a change to what the
  prompt carries, not to what the model may do, and it applies to every protected capability rather
  than only this one.
- **Or match this capability more loosely** — it is a closed intent with no arguments, and *bring
  the game forward* has a small vocabulary. Cheaper, and it fixes only this one.

The four phrases asked for should go in either way, and they are one line each:
`set elite to front`, `set game to front`, `put elite in front`, `put the game in front`,
`put elite in focus`. **None of them is in the list today** — checked, not assumed.

#### An instruction answered with a report — and it is remediation 16 arriving by the same road

Added 2026-08-23:

> **Me:** Set course for my carrier.
>
> **D47:** JOHN DEPARAGON is in Scorpii Sector BB-O a6-2. Currently in normal space.

**Diagnosed, not guessed.** `"my carrier"` is a **keyword** on `JournalCapability`
(`JournalCapability.cs:59`) as well as a whole phrase (`:182`). Keywords match **anywhere in the
input**, so *"set course for my carrier"* contains one, the router answered it before the model
ever saw it, and Journal's first argument-free tool reports where the carrier is.

That is the exact shape remediation 16 fixed for *"where is my fleet carrier"* — and it is back
because that fix added whole phrases **on top of** the keywords rather than removing any. The
ruling at the time was deliberate and still right: *"the keywords are untouched — narrowing them
would trade one wrong answer for a set of silences."* So the answer is not to delete `my carrier`;
it is that **an instruction has to out-match a topic**.

Two things make this worth more than one phrase:

- **The guardrails already name this case.** *"When the Commander asks you to act — set a course,
  press something, send something — act first and talk least."* The model never got the chance to
  obey it, because the router had already answered. A rule the model follows is no help when the
  model is not consulted.
- **The right destination may not exist yet.** `plot_route` takes a destination system; the
  carrier's system is known, so *plot me to my carrier* is a join d47 can make — but there is no
  tool that does it today, so this is not purely a routing fix.

**Not built with the rest of this item**, and deliberately: the phrase-beats-keyword machinery is
shared by every capability, and getting it wrong trades one wrong answer for a set of silences
exactly as the earlier ruling warns. It wants its own change with the corpus behind it.

#### The ask on the Technical page

**Technical already draws every run unfiltered.** `PanelViewModel` filters to
`Kind == TranscriptKind.Conversation` for the Conversation page and passes `_runs` straight through
for Technical, and `Flatten` has a `TranscriptVoice.Commander` case that renders a turn as
`> what they said`. So the page is not filtering the ask out, and the fix is in whatever should be
appending it rather than in the page — which is the narrow half of a search that would otherwise
start at the wrong end.

The quoted line supports that reading: it shows the answer with its timestamp and its *Answered.*
status and nothing above it saying what was asked.

**Spoken and typed are the same path from `MainWindow` onward** — `_host.Heard` sets `AskText` and
runs the same `AskAsync` a typed ask runs, with only a `_spoken` flag to tell them apart — so
whatever is missing is missing for both, and a fix that covers only the microphone would be a fix
in the wrong place.

Two things to settle: whether the **raw** transcription is worth showing when it differs from what
was routed (Technical is the page where that belongs, if anywhere), and whether a spoken utterance
that the **router** answers with no model turn behind it also appears — on the evidence of the
third quote, that is exactly the case a Commander is looking at Technical to understand.

### 28. Every question mark answers for the thing beside it — shipped 0.58.0

Asked for 2026-08-23, in four reports across one afternoon, and built the same day. They read as
four asks and are one: **the mark answered for the tab rather than for the thing under it**, and on
two pages it did not answer at all.

> All these question marks (help) should bring up in-app help with a breadcrumb to get back to
> where you were. There should be a "More Details Online" link on that page that brings up the
> website-based help. Currently that question mark glyph runs straight to the browser.

> The Routing tab, Plan sub tab should have a small question mark glyph in each of the different
> tools like with the Settings tab (Neutron Plotting/Jump Route, Road to Riches, Trade run). The
> help should be only for that tool and have more detail. Currently it tries to mix-n-match
> functionality of all 3 and is more confusing than helpful.

> There's no help for this page. There needs to be. *(the module picker)*

> This needs its own unique help page. *(the adventure editor)*

**The "More Details Online" link already existed** under a worse name. Every band already ends with
a card to the site; it said *Read the full page*, which does not say that pressing it leaves the
app — the one thing to know before pressing it, and the whole story in a headset where it cannot be
pressed and is drawn as a bare address. Renamed rather than added.

**Two refusals were behind "there's no help for this page", not one.** The module picker's mark
inherited the slot's engineering page rather than naming its own — and would not have drawn even
that, because `HelpLevel.Open` returned false whenever a chooser held the panel. Both had to go.
Stacking help over a chooser is safe for the reason help is a level at all: Back dismisses it and
the chooser is still underneath, which is what `PanelPrompts.Abandon` already assumed by popping
every modal level rather than one.

**The rename came with it.** *Jump route* → **Neutron Plotter** was queued from 2026-08-22 and was
built here because the card needed a name before it could have a page. Scope as agreed then: the
Commander-facing title, the card's new line, the test and the prose in `routes.md`.
`RoutePlanKind.Jump` does not move — it is serialised into the plan book and is the nav crumb key,
so renaming it would orphan every stored plan to buy nothing visible.

**One thing this uncovered rather than fixed:** a settings-jump card naming a capability that
declares no settings rows is a button that dismisses help, switches tab and does nothing. One had
already been written. There is now a gate.

### 26. Help that addresses how to use the UI — shipped 0.57.0

Asked for 2026-08-23, the day after the bands finished, and built the same day.

> What shows up often does not address how to use the UI properly.
>
> On the Transcript Page, Conversation sub-tab: since it is the first/default sub-tab, it should
> talk about the elements that are available on all the Transcript sub-tabs — sub-tab switcher,
> Copy All, Search, PTT Ready, Details, Ask/send controls. Help should address the elements of the
> sub-tab, which is pretty much the conversation panel, with links to the specific setting groups
> on the setup tab: LLM, TTS, Whisper. And those sections under Settings should have help of their
> own, which I think they probably do.

**Two of the three did; the third did not, and that decided the shape of the work.**
`conversation.md` and `speech.md` carry bands. `listening.md` did not, so the Whisper card had
nowhere to land in the headset — which is where a marked card falls back to the sibling band. It
was written as part of this rather than after it.

**The last clause was the one to correct.** Settings sections have no help of their own: the whole
tab carries one crumb (`SettingsCapability`), because sections are scroll-spy targets rather than
nav levels. What each row has is `SettingRow.Help`, whose own comment calls it the short form
against the capability page as the long form. So "link to the setting group" could not mean
"open its help" as asked — and on the ruling taken, it means **go to those rows**, which is new
machinery rather than a card.

**Three rulings, taken 2026-08-23 before anything was built.**

- **All the chrome on the Conversation band**, rather than splitting the panel-wide parts onto
  Overview. A Commander landing on the default sub-tab needs nothing else.
- **The links jump to the section**, rather than opening a second explanation of it.
- **Technical and Log file in scope** — `diagnostics.md` got a band in the same pass, so all three
  readings of the tab answer for themselves rather than falling back to the index.

**And one implementation call made rather than asked**: a new general page, `docs/transcript.md`,
instead of rewriting `conversation.md`. That page is one of the three link targets and cannot also
be the page about the tab. `NavCrumb.Help` is a `HelpLibrary` key rather than a registry id, which
is what let a general page be reached the same way the forty-five capability pages are, with no new
machinery — see the changelog entry for the three link faults the work uncovered on the way.

### 25. The Adventures tab: where it sits, what it shows, and that it is thinking — shipped 0.52.3

Asked for 2026-08-22, in five parts; built the same day, in the next release.

> * Place the Adventures tab after Engineers.
> * Current Adventure on the adventures tab should show "Adventure Only" Triggered Voice text and
>   any "Flavour" text where it responds to questions or comments about the adventure from the
>   commander. Trigger text should be in a highlight color.
> * Next Trigger should show what it expects the commander to do next to advance the story.
> * I've noticed that it can take a while after "triggering" a trigger for me to hear anything.
>   I'm assuming that it's "cogitating". If so, can I get an indication on the Adventure tab that
>   it's thinking? Something animated so I don't think that I haven't done what I'm supposed to do?
>   If it can be accompanied by a set of canned, "That's it!" "You've done it." "Well done." etc.
>   Maybe 10 of them. Short so that you don't have to wait long for the TTS.
> * And the Adventure tab (and the mini-version too) should appear in VR.

**The wait is real and it is deliberate**, which is why the fourth ask has two halves.
`AdventureCallout.Settle` holds a reached beat for twenty seconds so the line is not read out over
the jump that reached it, and `VaryAsync` then spends up to three more having the model say it in
the core's voice. Neither is wrong; what was missing is that the Commander could not tell the wait
from having failed to do the thing. So the confirmation is split off from the telling:
`AdventureAcks` is ten stock lines of four words or fewer, said on the tick the beat fires with no
settle and **no model behind it** — which is why its announcements carry
`AdventureCallout.AckPrefix` rather than `KeyPrefix`, since `FlavourBriefs` routes on the prefix and
would otherwise send the acknowledgement through the very round trip it exists to arrive ahead of.
`AdventureThinking` is the other half, driven off the same 10 Hz tick the clocks are and honest
about whether the frame moved, because the headset only re-rasterises a surface something marked
dirty.

**"Adventure only" needed somewhere to keep what was said.** The reading level was drawing the
*authored* lines, and what a Commander hears is the model's wording — so a story flown over four
evenings had no record of itself anywhere but a session-long transcript carrying everything else
d47 says. `Adventure.Told` is that record, persisted with the story and capped at
`AdventureLimits.MaxTold`. Two decisions the Commander made:

- **The flavour heuristic** is name-, beat- or place-mention over the exchange
  (`AdventureMention`), whole words only and nothing shorter than four letters. Chosen over
  "everything while a story is live" (which stops the page being adventure-only) and over asking
  the model to tag each turn (a round trip in front of every answer, which is the cost the rest of
  this item removes).
- **The feed persists** rather than living for a session.

**"Step X of Y" reverses a rule Phase 47 wrote into the code.** `AdventureStanding` said outright
that *beat 3 of 7 is checklist language and belongs to the Technical transcript*, on the
story-not-a-checklist framing that governs the whole phase. The Commander asked for the count on
both surfaces and it is built; that comment is rewritten rather than deleted, on the same terms the
checklist's withdrawal and return were. What did not change: the beats are still titled dramatic
functions rather than numbered stops, and nothing generated says a number — `Step()` is the one
place a count is spelled.

**Mini follows the tab now.** It was "the transcript's tail and the provenance line" whatever the
panel was on. The instruction is that it show a succinct version of whichever VR tab is selected,
*"but we can keep it to transcript and Adventure for now"* — so `AdventureMini` draws the five
things asked for (the short description, the trigger just fulfilled, the trigger expected, the last
thing the AI said, and the step) and every other tab behaves exactly as it did. Mini still has no
tab strip; which tab it is reading is chosen on the big panel, which is what makes one surface in
two sizes rather than two surfaces with their own state.

**The tab itself reaches the headset by one call**, exactly as Phase 47's own comment predicted it
would — `VrPanelSurface` now passes the window's `AdventureSurface` to `PanelView.EnableAdventures`.
The desktop-only reasoning was that the editor and the ask form want a keyboard; that weighed the
wrong half, since a Commander in a headset is precisely the one who has just arrived somewhere, and
the prompts have taken a spoken value since Phase 25.

A headless capture caught the one defect in this that no test had: the drilled-in reading level
subscribed to the store's change event only, and a beat firing writes nothing to disk — so the card
behind it redrew and the level the Commander was looking at did not.

### 24. No Help glyph on a VR surface — shipped 0.52.2

Asked for 2026-08-22; built the same day on `fixes-3`, in the next release. The panel's help mark
opened the documentation site in a browser — on the desktop, which a Commander in the headset
cannot see. The model's `HelpRequested` event was the wrong seam: the headset copy shares the model,
so its press reached the desktop window's handler. Help is now an affordance of the **surface**,
handed over by the host like search and the turn-figures dialog already were — `PanelView.EnableHelp`
— and `VrPanelSurface` never calls it, so the headset copy has no button rather than a hidden one.
The `OpenHelp`/`HelpRequested` pair on the model went with it; its comment said the view "asks
rather than acts" so as not to know what a desktop is, and that reasoning is kept, moved one seam
over to where the two surfaces actually diverge.

### 23. Remind me to buy limpets — shipped 0.47.0

Asked for 2026-08-21, built as list.md Phase 41. Two of the Commander's own corrections
reshaped it mid-build: limpets are bought through **Advanced Maintenance** rather than the
commodity market, which killed a design built on reading `Market.json`; and the carrier gap was
ruled to be accepted rather than special-cased. The rulings and the measurements are in
`list.md` Phase 41 and in `LimpetCallout`'s own summary.

### 22. Say when a system might be holding High Grade Emissions — shipped 0.46.0

Asked for 2026-08-21: *"Notifies me when I am in a system that has a chance of having High Grade
Emissions available, and what the material(s) is/are. Skips if I'm already full of that material …
Should support multiple material types when a system matches multiple conditions … not just Core
Dynamics Composites plus the related one that can be found in the same HGE, but when completely
different ones are there."* With an on/off row in settings.

**Built as list.md Phase 40.** Everything except the table was in hand from the start — the
conditions are all in one `FSDJump`, and "skip what I am full of" is exact rather than estimated —
so this waited on a source rather than on code, because hand-writing game data is the one thing
`CLAUDE.md` forbids outright.

#### Where the table came from

- **[Elite Dangerous Wiki — High Grade Emissions](https://elite-dangerous.fandom.com/wiki/High_Grade_Emissions)**
  is the best of them, and not only because it has the table. It is the only source found that
  states the **mechanic**: a signal is assigned to *one faction*, its contents come from that
  faction, and where a faction meets several conditions a hidden rank order picks between them.
  That is what makes the rest predictable instead of a list of folklore.
- **[Frontier Forums — Unidentified Signal Sources: A Complete Guide](https://forums.frontier.co.uk/threads/unidentified-signal-sources-a-complete-guide.377716/)**
  (2017, edited 2018), on Frontier's own site, is the primary community research the rest descends
  from. Found through [EDEngineer issue #196](https://github.com/msarilar/EDEngineer/issues/196),
  which cites it as *the* reference. It corroborates every group.
- **[edgalaxy.net/hge](https://edgalaxy.net/hge)** is not a table but a live one — HGE detections
  reported over EDDN since the last tick. Its six filter groups match the six below exactly, which
  is behavioural corroboration rather than a second copy of the same prose.
- **And a fourth, already in the repo.** `Materials.tsv` carries these conditions in its own origins
  column — "Signal source (High grade emissions, Boom)" and the rest — for all ten materials,
  generated by `tools/gen-materials.py`. So `EmissionRules` is asserted against a shipped, generated
  table both ways: no rule without a row, no row without a rule. A regenerated table that disagrees
  fails a test rather than drifting away from a callout nobody would think to re-read.

#### The groups, in the wiki's stated rank order

| Condition | Materials |
|---|---|
| Federal faction | Core Dynamics Composites, Proprietary Composites |
| Imperial faction | Imperial Shielding |
| Civil Unrest | Improvised Components |
| War or Civil War | Military Grade Alloys, Military Supercapacitors |
| Boom | Proto Heat Radiators, Proto Light Alloys, Proto Radiolic Alloys |
| Outbreak | Pharmaceutical Isolators |

#### Four places the two prose sources disagree, and how each was settled

Recorded because a table that hides its choices is one nobody can check later. All four ruled on by
the Commander, 2026-08-21.

1. **Proprietary Composites** — wiki lists it beside Core Dynamics for Federal space, the 2017 guide
   does not. **Ruled: include it** (the wiki).
2. **Expansion** — wiki says *Boom or Expansion* for the Proto materials, the guide says Boom only,
   and a third account adds *Investment*. **Ruled: Boom only.** This one is not cosmetic: `Expansion`
   is the second commonest state in the corpus after `None`, so the other reading would have made
   this the chattiest callout d47 has.
3. **Population** — wiki gates Outbreak on a million, the guide gates nothing. **Ruled: the floor
   applies to every group.**
4. **Whether superpower overrides state — the load-bearing one.** The wiki says a Federation or
   Imperial faction *never* yields anything but composites or shielding. The 2017 guide says the
   opposite in as many words: *"If you need Imperial Shielding and Pharmaceutical Isolators, look
   for an Imperial system in Outbreak."* **Ruled: superpower wins** — the wiki is newer, and its
   reading is the one consistent with the rank-order mechanic the same page states. Pinned by three
   tests, and the fault was reintroduced to watch them go red.

#### What the journal turned out to give

`FSDJump` carries a `Factions` array, each entry with `Allegiance`, `FactionState`, `Government`,
`Influence` and — the useful one — **`ActiveStates`, a list**. So the evaluation is **per faction**,
which is what the wiki's mechanic asks for and what makes the Commander's *"completely different
ones"* case fall out rather than needing a rule of its own. **84 of 400** recent corpus jumps are
into a system holding a Federal faction *and* an Independent or Alliance one, so a system offering
two unrelated groups at once is ordinary. The state spellings are the journal's own tokens, so no
name-matching guesswork was needed anywhere.

### 20. Ordering the checklist, by voice and with both ends — shipped 0.45.0

Asked for 2026-08-21 against a transcript in which d47 said *"Ordering I cannot set"* and *"no
selection, no move"*. Three parts: the selected line has to be reorderable by voice, a line just
added has to *be* the selected one, and **Move to Top** and **Move to Bottom** have to exist beside
the two steps that already did — the end glyphs drawn as the step glyphs with a bar on them.

### 21. The carrier's crew speak to each other when you drop in — shipped 0.45.0

Asked for 2026-08-21, with the exchange written out: the tower tells the captain the Commander is
inbound, and the captain answers the tower and then the Commander. Model-written where there is a
model, with the authored lines as the floor — which is what Phase 11 already built for the
carrier's other lines, so this reuses it rather than adding a second path.


**The ten raised hand-testing 0.15.0 on 2026-08-16** all shipped together in 0.21.0. Their record
is that section of the changelog, which keeps them in the order they were built.

Two of them left something worth knowing about:

- **Item 9's headset defaults have never been seen in a headset.** The arithmetic is tested and
  the first-show placement is written down, but whether knee height *reads* as the right place —
  and whether it is wrong for a seated Commander — is a question only somebody wearing one can
  answer.
- **Item 6 turned up a defect on the way past**, in the log-level rows rather than in anything it
  was asked to change: three of them named namespaces that do not exist and so controlled
  nothing. Fixed in 0.21.1, and `TechnicalLogBridge` now reads the one list rather than keeping
  a copy.

**Items 11 to 14, raised on 2026-08-17**, shipped in 0.22.0: the NPC preamble, comms on the
Technical page, the radio treatment for everybody who is not aboard, and an NPC's voice being
theirs for as long as the Commander is in the system. Their record is that section of the
changelog.

Two of them also turned up something on the way past. The empty-sender case was being read aloud
as " says: …" — 8821 events in the corpus have an empty `From` rather than a missing one. And the
crew's voice assignments shared the per-system table with the NPCs, so a hired gunner changed
voice on every hyperspace jump; they are aboard, so they now last the session.

**The five raised hand-testing 0.21.x on 2026-08-17** — items 15 to 19, all of them about the
settings surface — shipped together in 0.23.0. Their record is that section of the changelog: the
search matching a section's own name, **Verify Key** shut until a key is typed, the ElevenLabs key
row moved up beside the provider that needs it, and the voice picker's audition becoming a play
glyph on each row now that a click highlights rather than chooses.

Item 18's open question was answered **both ways**: the price is a line above the list *and* the
pointer text on every glyph. A tooltip alone would have made a cost you have to hover to discover,
which is what Phase 11 put the number on the button to prevent.

Two of them turned something up on the way past. Item 18 was only possible because of 19, which is
why they shipped together — and building the picker's rows per keystroke, as the first cut did,
cost it the highlight on the value the Commander arrived with: a list holds its selection by
object, and a text box raises `TextChanged` as its template applies, so the filter re-ran and
handed the list a different row for the same voice before the window had finished opening.
