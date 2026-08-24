# Changelog

What changed in each release of Directive 47, newest first.

A **completed phase in [list.md](list.md) is a minor release** — `0.<minor+1>.0` — because the
version is how a Commander tells "some fixes landed" from "there is a whole capability here
now". A batch of wanted changes from
[docs/plans/change-requests.md](docs/plans/change-requests.md) is a minor for the same reason:
nothing in it is a defect, so shipping it as a patch would tell a Commander that nothing had
changed. Fixes between phases are patches. **A published tag never moves**: it is a receipt for
one exact `d47.exe` and the checksum beside it, so a correction ships as the next patch rather
than as the same number twice.

Open defects live in [GitHub Issues](https://github.com/dseelinger/d47/issues), and wanted changes
in [docs/plans/change-requests.md](docs/plans/change-requests.md). An issue is closed and an entry
leaves that file when it ships, and the line it gets here is its permanent record. `bugs.md` and
`remediation.md` were retired on 2026-08-24 and are archived under
[docs/archive/](docs/archive/README.md); a release section below that names either of them means
the file as it stood then.

---

## 0.62.1 — 2026-08-24 — Directive 47 notices when you rank up

### Every rank-up you earned was being thrown away

Reported: *"this is being repeated once per module, even though my relationship with the engineer
is 5."* Quite right, and it was worse than a repeated sentence — **Directive 47 believed the rank.**

Elite announces a rank-up by naming the engineer and the new number and nothing else. Directive 47
wanted a status word alongside it, did not get one, and discarded the whole event. So it kept
whatever rank you were sitting at when the engineer was first unlocked, and went on saying grade 5
could not be rolled at rank 1 — correctly, about a rank four steps out of date. Your own morning
has Selene Jean going from rank 1 to rank 5 in four and a half minutes, with all four steps
dropped.

Across every journal on this machine that is **172 of 278 announcements**, spanning 24 engineers:
every rank-up ever earned.

It hid this long because it healed itself. Elite writes a full standing for everybody at launch, so
the next time you started the game the number was right again — and it was wrong only inside the
session where you had done the work, which is the session where you would ask.

**It also made Directive 47 pessimistic.** The further your rank is above the grade you are
rolling, the fewer rolls that grade takes, so a rank held too low overstated how much was left. And
the engineer filter hid work you could have done standing right there.

### One thing about an engineer, said once

*"Rank rises by working with them, and it compounds"* is one fact about one engineer, and it was
printed under every module waiting on that rank. Six modules, six copies.

Each line still says what is true of **itself** — this module, this grade, why it is blocked —
because a line has to make sense when it is read out on its own. The explanation now goes to the
first line that needs it and to none of the others. A line that arrives by itself always keeps it.

### The scrollbar stops flashing while you drag it

The other half of the flicker reported yesterday, and it survived the fix that went with it.

Dragging the bar is the only way to scroll in the headset, so a hand at arm's length spends the
whole drag wandering off a bar twelve pixels wide — and every time it strayed, the highlight went
out and came back. The scroll already survived that on purpose. The light now does too: **once you
have hold of a bar, it stays lit wherever your hand goes**, and letting go hands the question back
to where you are pointing.

Widening the target could never have fixed this, which is why yesterday's change did not.

---

## 0.62.0 — 2026-08-24 — The headset keeps up, and d47 stops saying a wrong number

### The aim ray runs at frame rate

The ray was told where your hand was **ten times a second**, which is about where motion stops
reading as motion and starts reading as a fault. It was reported as one.

The whole headset path ran on one clock. That rate is right for nearly all of it — a transcript
nobody scrolled does not want redrawing faster — but a pointer has to keep up with a hand. So the
rate is **split rather than raised**: the pose read, the ray arithmetic and the beam now run on
their own loop, while everything that *decides* anything — trigger, grip, back, carry — stays where
it was, on one thread with its state.

**Carrying a panel follows the hand too.** That was the same complaint one step along, and it
needed a different fix: the quad is placed directly rather than through the serve, which is what
turns a position into something SteamVR has drawn.

### The scrollbar stops blinking when you hover near it

Held just off a bar, the highlight flickered. One radius decided both lighting up and going out, so
a hand at arm's length wandering a few pixels crossed it over and over. **Leaving costs more than
arriving does** now, and it stops.

The frames themselves were measured first and were never the problem — a probe counted the pixels
in every served frame across ten cycles and got the same number every time.

### Directive 47 can bring Elite forward from behind Elite

Windows only lets the foreground be taken by a process that already has it, so asking used to flash
a taskbar button — no use at all from inside a headset. It works now, and it still says so plainly
when Windows refuses anyway.

**Only when you ask.** No automatic action can do this: a honk arriving mid-combat is not a reason
to take your game's focus.

### A placement change lands on the panel you are looking at

Distance, size, angle and the rest are kept separately for the big panel and the mini one, because
mini exists to sit smaller and further out of the way. What was missing is that *"move it closer"*
means the one in front of you. It does now. Both are still on the settings page in full, so setting
mini up while the big panel is in front of you is a thing you can do whenever you like.

**And the panel answers to the words you would use.** *Big panel*, *large panel*, *little panel*,
*small panel*, *minimal panel* — where before there was only *"mini panel"* and *"full panel"*, the
one phrase nobody says unprompted.

Directive 47 also stopped claiming it could not change the panel's size, which it could, twice
over. *Size* is how big the panel is; *Scale* is how big the writing on it is. Each now says so and
names the other.

### Your carrier answers in your carrier's voice

You could already cast a voice for your fleet carrier's tower, and only Directive 47's own
announcements ever used it — a message *from* the carrier came back in a stranger's. Now it is the
tower.

A squadron's carrier is not, and that took care: Elite writes carrier positions for your own and
for a squadron's seconds apart, and in 152 of 173 journals carrying both, the squadron's came last.

### An unlock line says what the invitation asks for

*"Unlock Bill Turner at Alioth"* now goes on to say what he actually wants for the invitation.
Directive 47 had the answer all along — the engineer table carries it for 34 of the 38, and two
other screens already read it. The checklist, which is the one you fly with, was the only one
without it. The four with no invitation task on record say so rather than stopping.

### Grade 5 does not cost sixteen million credits

Reported: *"I got that as soon as I did my first Engineering roll with Selene Jean. This is untrue.
Where did the 16 million figure come from?"*

Quite right. Directive 47 said a grade could not be rolled at your rank — true — and then that
reaching it took a fixed sum in credits sold at that workshop, which came off a wiki and was never
measured. **Working with an engineer is what raises the rank**, which is exactly what you had done.

What it says instead is Frontier's own published table rather than anybody's claim: the further
your rank is above the grade you are rolling, the fewer rolls that grade takes. The early work is
the slow part, and it pays for itself afterwards.

### Under the hood: a locked file stopped costing every key

A honk that did not fire said *"I cannot read your control bindings."* The bindings were fine. Elite
had held one file open for a moment eight hours earlier, Directive 47 read that as the file being
missing, threw away 347 working bindings and never went back for them — so every key it can press
was dead for two hours and forty-one minutes, and only the honk was talkative enough to mention it.

A read that fails now keeps what it had and tries again, and a locked file is no longer reported as
an absent one.

## 0.61.1 — 2026-08-24 — Engineers and Utilities stop flickering

Reported from the headset: *"flickering that is so bad as to make Engineers and Utilities tabs
unreadable — no flicker on other tabs."*

Those two are exactly the tabs the headset pokes on every tick, and both asked for the whole panel
to be redrawn whether or not anything on them had changed. A redraw re-rasterises the widget tree
and hands SteamVR a new image, so it did that ten times a second for pixels nobody had touched.
Every other tab only asks when something moved, which is why only these two flickered.

**Everything on the Utilities page reads to the minute** — both clocks, an alarm's due time, and a
timer's countdown, which is rounded up so it never shows a stalled-looking zero. So that page
changes once a minute and was being redrawn six hundred times inside it: **599 of every 600 images
were identical to the one before.** It now compares what it draws, so nothing has to know which
field ticks at which rate.

Engineers cost almost nothing to fix. The page has compared a ranking stamp and returned early when
nothing moved since the engineers tab was built — it simply never told the headset, which set the
flag anyway.

**This is the fourth time this fault has shipped**, and the previous three are in this file: a carry
setting the flag every frame (0.22.2), the aiming highlight setting it unconditionally (0.24.0), and
Utilities rebuilding every timer row ten times a second (0.39.1) — which fixed the rebuild and left
the flag behind it. Both halves are covered by tests that were watched to fail with the fault put
back, because three previous fixes means a test that does not really catch it is one that lets the
fifth through.

## 0.61.0 — 2026-08-24 — Work an engineer can start

### Include Partial Grades

Standing at an engineer's base with their filter on, you now get a checkbox beside it. It decides
which of two questions the page is answering:

- **unchecked** — what this engineer can take **all the way** to the grade the line asks for;
- **checked** — *and* lines they can only take **part of the way**, which somebody else must finish.

Unchecked is the default and is exactly what shipped before. Checked, each line says how far it goes
— *"Lei Cheung takes this to 3 of 5"* — so the answer is on the line and not only in the help.

The grade check that landed in 0.60.2 was right and it shut a real door: Heavy Duty on a Shield
Booster is Lei Cheung's to grade 3, so a Grade 5 line is not his work — but he genuinely can take
that booster from nothing to 3, at a workshop you are standing in. That was recorded at the time as
a judgement call and overrulable. This overrules it, without folding the work back into the list that
report was about.

The box is beside the engineer filter and nowhere else — the phrase means nothing against a list
filtered by ship — and it is remembered between sessions, on the same road as the filter itself.

**An experimental effect goes where its module's blueprint went.** An effect carries no grade, so
nothing holds it back on its own — unchecked, it used to stay on the page after the blueprint it
belongs with had been filtered off it, which reads as a stray errand. It is not one: applying the
effect is part of finishing that module, and the module is work this engineer cannot finish. It now
appears with its blueprint or not at all, and says why in its own terms — *"Lei Cheung can apply
this, but only takes that module to 3 of 5"* — because *"takes this to 3 of 5"* would be a sentence
about the module wearing the effect's clothes.

### Under the hood: a test that stopped asking the busiest thing on the machine

Two audition tests waited five seconds on a cancellation callback and occasionally did not get one.
The cause turned out to be measurable: `CancelAsync` schedules its callbacks on the threadpool **on
purpose**, to keep arbitrary continuations off the UI thread — so with the pool saturated, which is
what a CI runner running seven test projects is, the callback is late. With the pool starved
deliberately the five-second wait came back empty after 23 seconds. They now watch the token's own
flag, which flips synchronously and needs no threadpool thread at all.

### Under the hood: the open-defect queue is GitHub Issues

`bugs.md` and `remediation.md` are retired. Open defects and wanted fixes are now
[GitHub Issues](https://github.com/dseelinger/d47/issues), and the pointer at the top of this file
says so.

They were the two files that made parallel branches expensive, and the measurement is the argument:
`bugs.md` averaged **+33/−27 lines per commit** and `remediation.md` **+30/−22**. Neither was a file
where a branch ticked a box — every touch rewrote paragraphs and restated a hand-written count, so
two branches touching either conflicted, and merged cleanly into a count that had been true on
neither. `bugs.md` recorded that happening to itself: it said eleven were open while three were. A
derived count cannot go stale, which is most of the case in one line.

Four issues carry what was actually open — [#18](https://github.com/dseelinger/d47/issues/18) the
motion controller that stops answering, [#19](https://github.com/dseelinger/d47/issues/19) the VR
aim ray, [#20](https://github.com/dseelinger/d47/issues/20) the Ctrl-drag that copies nothing,
[#21](https://github.com/dseelinger/d47/issues/21) the placement rows that exist twice per surface.
The aim ray had been written up in **both** files as two entries; it is one issue now. And three
remediation items were still unticked and had in fact shipped — 2 in 0.47.0, 15 in 0.44.1, 16 in
0.45.0 — so they are corrected rather than carried forward as work already done.

**Nothing about the record changed.** A fix's permanent record is still the line it gets in this
file under the release that shipped it; an issue closing is not a record. `list.md` did not move
either — it is the product description rather than a tracker, cited over a thousand times from code
as `list.md Phase N`, and a phase still joins the frozen set the day it ships. Wanted changes that
are not defects still live in `docs/plans/change-requests.md`.

Both files are archived under [`docs/archive/`](docs/archive/README.md) rather than deleted,
because 382 code comments name `remediation.md` and about twenty name `bugs.md`, and because
reading back a change that was supposed to have worked is ordinary here. Only batch 17 was ever
*in* `remediation.md`; batches 1 to 16 are in git history, reachable with
`git log --follow -- remediation.md` across the move.

## 0.60.8 — 2026-08-24 — Three from the desk

### The checklist filter is one filter, and it is remembered

Apply *"what Lei Cheung can do here"* in the window and the headset, a foot away, went on drawing
the unfiltered list. The filter and the search box were fields on the page, and there is one page
per surface — the same shape, and now the same fix, as the selected line before them. **What you are
reading is shared; only how a surface draws it stays with the surface.**

The filter is also **kept between sessions**, which was asked for the same day. The search box
deliberately is not: a typed query is where you are this minute, and one restored from last week is
a list that looks broken until you find the box.

The question that decided whether sharing was safe is answered rather than assumed — the mini panel
*does* draw the chooser, and it says what the list is under. A filter switched on at the desk cannot
leave you in the headset with a short list and no explanation.

### A parked ship's lines get a verdict

A plan line for a ship in another dock read out its module by name — *"0A Shield Booster"* — over a
verdict that had refused to look at the same place, because the line read the remembered loadout and
the verdict only ever read the ship you are sitting in. One report of that asymmetry arrived as d47
being unable to see a ship it could describe perfectly well.

Now both read the same source, and **a verdict from a remembered loadout says which moment it is a
fact about**: *"As last seen, 16 Aug 2026."* A date rather than "three days ago", because nothing in
the core reads the clock. A ship you have never sat in still gets silence rather than a guess.

### Under the hood: a stale build says so

A single-file bundle left in the build output directory shadowed every build for hours — `dotnet
build` rebuilt the assemblies and the bundle ignored them, three fixes in a row appeared not to
work, and nothing anywhere said a word. That layout is now called out at startup: a bundle-sized
executable with loose assemblies beside it, or assemblies newer than the executable, is not a shape
any build produces. Said rather than enforced — a heuristic that can be wrong must not be the thing
that stops d47 running.

**And that folder is `dev-install\data\`, not `dev-data\`.** 0.60.4 named it `dev-data\` and was one
level out: `AppPaths` puts `data\` inside whatever root it is handed, so the folder really was
`dev-data\data\` — a path nobody can read twice without wondering which is which. A Debug build's
install root is `dev-install\` and its data folder is `dev-install\data\`, exactly as an installed
d47's is `data\` beside the exe.

## 0.60.7 — 2026-08-24 — Two panels, one opacity knob

Opacity was one of the settings each headset surface kept its own copy of, which is what made *"set
the opacity to 0.5"* a question with two answers — and it got answered with the panel that was not
in front of the Commander.

**It is one knob now.** Everything else those two surfaces carry has a reason to differ: mini exists
to be smaller and further out of the way, so its distance, size and drop are its own. How
see-through the glass is is not that kind of setting. It is one preference about how much cockpit
shows through Directive 47, and asking for it at half never meant *half, in one of the two modes*.

Whatever you had set on either panel is carried up to the shared knob the first time this version
runs, so nobody sets it again — a value that was not the default was a decision, and it survives.
The old per-panel values stay in `settings.json` unread, because that file only ever gains
properties.

## 0.60.6 — 2026-08-24 — The carrier crew know their own callsign

### Tower and the Captain speak again

Reported after a day of flying in and out of a fleet carrier in silence. d47 treats *knowing the
callsign* as owning a carrier, and only one journal event has ever carried one: `CarrierStats`,
which Elite writes when the Commander opens the carrier management panel. Fly all day without
opening it and the crew have no name to answer to, so they say nothing — not at the drop, not at the
dock, not on a jump.

It is not an edge case. Across 925 journals, **69 of the 199 that dock at the Commander's own
carrier contain no `CarrierStats` at all** — 148 dockings with a mute tower. The day this was
reported was one of them: nine dockings at BNH-T2F, not a word. And the read that looks like a
second source is not one — **0 of 1,134 `CarrierLocation` events carry a callsign**, though they do
carry the carrier's id.

So the callsign now comes from the dock itself. Docking at a carrier writes its callsign as the
station name and its id as the market id, so an event whose market id is the id d47 already holds is
the Commander's own carrier stating its own name. Nothing is pattern-matched and no name is guessed:
another Commander's carrier writes a callsign-shaped station name too, and it teaches d47 nothing.
The docking that supplies the name is the same docking the tower answers, so the first one speaks.

## 0.60.5 — 2026-08-23 — Ships Frontier has named and the id list has not

### Your Kestrel Mk II is a Kestrel Mk II

Reported as a checklist line ending *"on Tulimiekka (smallcombat01_nx)"*. A hull reaches d47's
specification table through the community's id list, Frontier ships hulls faster than that list is
updated, and until now a hull with no row had **no name at all** — so d47 fell back to the internal
symbol and read it out.

The table already knew the name; it was in a column nobody had joined. Armour is per-hull, so every
hull owns five bulkheads called *"&lt;hull&gt; Lightweight Alloy"*, *"&lt;hull&gt; Reactive Surface
Composite"* and so on, and what those five share is the hull's name in Frontier's own spelling. Four
ships get their names back this way — **Kestrel Mk II, Caspian Explorer, Corsair and Lynx
Highliner** — and no list is written down here to go stale: a hull Frontier adds is named the day its
armour is.

It reads off one ladder now rather than five copies of one, so the fleet page, a spoken line, a
ship's core and the checklist cannot disagree about what a ship is called.

### The two VR surfaces say which is which

*"Opacity does not change the opacity"* — reported after setting `vr.panel.opacity` to 0.5 while the
**mini** panel was on screen, which has its own opacity and kept it. Every row in the VR
placement settings now says which of the two surfaces it governs and where the other one's copy
lives. See `bugs.md` for the half that is still open: whether *"set the opacity"* should mean the
surface you are looking at.

## 0.60.4 — 2026-08-23 — Work finished while d47 was off is marked, not announced

### A checklist that arrives from outside is folded in silence

Reported from the debug build: a stream of *"X is completed"* for rolls finished while d47 was not
running. The rule against exactly that already existed — the startup tick folds the whole journal
backlog without saying a word — but it was attached to the **tick** rather than to the **document**,
so it covered only the copy of `checklist.json` that was on disk when d47 started.

A checklist rewritten under a running d47 — the hand edit the store deliberately supports, a restored
backup, a data folder refreshed from another install — was re-read mid-session, and every
disagreement between what the file stored and what the game says was read out as though it had just
happened. It is in the log: `Loaded 1 checklists … (279 items)` and the announcement that followed it
in the same second. d47's own writes were never this and are not affected — it re-reads inside its
own save, so a tick that finds the file changed found *somebody else's* change, and the right answer
to that is the Commander's own: mark them done, say nothing.

### "I know what ship I'm in"

> "Grade 5 Reinforced Shields on 5C Bi-Weave Shield Generator on Tulimiekka (smallcombat01_nx)" is
> done. 5C Bi-Weave Shield Generator is at grade 5 and finished.

One sentence saying the same thing twice, ending with the name of the ship the Commander is sitting
in. It now says it once: **"5C Bi-Weave Shield Generator is at grade 5 and finished."**

The ship has not simply been deleted from the wording — it is named when it is *not* the one being
flown, which is the honest version of the reasoning it replaces. That comment argued the ship must
ride the sentence because this is the one checklist line with no heading over it; true of every ship
except the one under the Commander.

### Under the hood: a Debug build stops writing into `bin\`

d47 writes everything beside its executable, which is right for an installed app — and meant a Debug
build kept its live checklist, settings and secrets inside build output. On 2026-08-23 the obvious
remedy for a stale build artifact, deleting `bin\Debug`, deleted those too. A Debug build now writes
to `dev-data/` at the repo root instead. Nothing about a published build changes: the redirect rides
an assembly attribute written for the Debug configuration alone, so no released d47 can take that
road and no environment variable can redirect where secrets go.

## 0.60.3 — 2026-08-23 — The trade offer says whose offer it is

### An engineer will not be offered as a material trader again

Reported 2026-08-20, standing at Lei Cheung's base: asked what a shortfall of Conductive Polymers
was for, d47 answered with the rates a trader would charge — and put **Lei Cheung's name on them**.
He is an engineer. He trades nothing.

The log settled it without a re-run. Every figure in that sentence was d47's own arithmetic and
every one of them was right: 2 Core Dynamics Composites or 2 Proto Heat Radiators for 1 is the
published cross-line rate from grade 5 down to grade 4, 1 Biotech Conductor for 3 is the same step
inside the conductive line, and the *"52 items on your list"* beside it came from the callout rather
than from the model. **The only invention was the owner.** The rates went out headed *"A trader
could make it out of what you already hold"* — an actor with no name — in the same turn as the
opening callout naming the engineer the Commander was docked beside, which d47 hands to the model as
its own words. One name, one ownerless offer, and the model put them together.

So the offer now says who would make it, in both places that carry one: the material shortfall
report and the material lookup. A rate is a fact about a station service, and saying so is what
stops it attaching to whoever happens to be standing there.

## 0.60.2 — 2026-08-23 — Three bugs from one evening in the headset

### "Switch to full panel" now switches to the full panel

Two reports, one defect. d47's spoken routes knew the bare phrase and not the words a Commander
naturally puts in front of it, so *"switch to full panel"* missed a phrase that exists — the bare
*"full panel"* has always worked — fell through to the model, and was answered with an offer to open
Elite's own left, right, comms and role panels. *"Set tab to checklist"* missed for a second reason:
every phrasing the panel knew named the destination in the middle.

The openers are now one shared list serving both routes, so they cannot drift apart again — and
**every setting command in d47 gained the same tolerance**, not just the two that were reported.
Saying the destination last needed no new pattern: the grammar is opener + name + suffix, so an
opener ending in "to" with an empty suffix is that shape read backwards.

**It is not looser.** Both routes still match the whole utterance, because taking a word off the
front of a *closed* list is not inference. "Can you switch to full panel while I dock" still moves
nothing.

### The checklist filter stops showing another engineer's work

Standing at Lei Cheung's base, fifteen of the forty-five lines offered as his were **Grade 5 Heavy
Duty rolls he cannot take** — that blueprint is his to grade 3, and grades 4 and 5 belong to Mel
Brandon and Didi Vatermann. The join asked whether an engineer touches a blueprint *at all* and never
looked at the grade the line wants. An engineer's grades are not the blueprint's grades.

### And it says how big the answer is

The list has no headings and no cap, and the ordering floats the flown ship's project to the top — so
a filter matching forty-five lines across seven ships opened on fourteen near-identical lines from
one of them and read as *"it is only showing one ship"*. It was right and looked broken, twice, in
one evening. One line above the rows now says what was actually matched.

### Under the hood: a test that drives the page

Three separate measurements of the engineer join agreed with each other and disagreed with the
Commander's screen all evening, because the join was being verified and the drawn list was not. The
checklist tab now has a test that builds the real panel, presses the real chooser, presses the real
filter option and reads back the text actually rendered — through the buttons rather than a
test-only seam, since a seam is a second path that can diverge, and divergence is exactly what went
unnoticed.

## 0.60.1 — 2026-08-23 — A bi-weave is still a shield generator

**Reported as "the engineer filter still isn't working" the same day 0.60.0 shipped, and the
investigation found something older and larger underneath.** Standing at Lei Cheung's base, the
filter was quietly dropping work it should have offered — including every line on the ship the
Commander happened to be flying, which is why the list opened on a different ship entirely.

**Two vocabularies that were never the same one.** A module's specification carries Frontier's
product name — *Bi-Weave Shield Generator*, *Krait MkII Lightweight Alloy* — while the recipe
table's module column is a 64-entry category vocabulary that calls those two *Shield Generator*
and *Armour*. The engineer join narrowed recipes by the readable name, matched no row at all, and
returned empty **before the blueprint name was ever consulted**. The line then belonged to no
engineer anywhere, with nothing said and nothing to search for.

The measurement that shows the size of it: of 558 distinct product names, **524 have no
identically-named category**. The old join worked only for the 34 where the two vocabularies
happen to coincide — Shield Booster, Frame Shift Drive and the like — which is why it looked
right for years.

`BlueprintCatalogue` gains an overload that narrows by the module's **type**, reusing the join
that was already correct: the module's own rows, intersected with what Frontier lets it take.
Measured against one Commander's real fleet, on the same 219-line snapshot: **41 items to 45**,
and a seventh ship joins the answer — the one whose only two engineerable lines were both
bi-weaves.

**Falling back to the whole table when a module narrows to nothing is deliberately not the fix**,
and there is a test that fails if someone tries it: *Heavy Duty* with no module draws Armour and
Shield Booster rows together, which would credit a shield engineer with armour work they cannot
take. An unknown module stays unknown.

Three things the same investigation ruled out, recorded so they are not chased again: the
remembered loadouts are fully populated at every cold start, so a restart changes nothing; verdict
text is absent on a parked ship's lines because verdicts only ever read the flown ship, not because
d47 cannot see that ship; and the filter was never restricted to one ship — it returns every match
across the fleet in one flat, uncounted list, which is a presentation problem and is still open.

## 0.60.0 — 2026-08-23 — The engineer here can see your whole fleet

### The checklist filter stops at the ship you flew in

*"All checklist items fulfillable by the Engineer should appear for that filter, not just for the
ship I'm in."* Shipped one release after the filter itself, and the fault was a good deal older
than the filter.

**Slot names are shared across hulls, and nothing was checking whose ship it was.** Every ship has
a `TinyHardpoint5`. Asked whether Lei Cheung could roll Heavy Duty on a line about the Krait
parked two docks away, d47 read the slot name off the Anaconda it was sitting in, found a chaff
launcher, and asked the catalogue which engineers offer Heavy Duty on a chaff launcher. None do.
So the line was dropped, the engineer's count went down with it, and if that was the only work
in the system the engineer was not mentioned at all — the answer was not narrowed, it was
**silently wrong**, and it looked exactly like having nothing to do.

**The fix is the loadout d47 already remembers.** Phase 37 started keeping each ship's modules for
precisely this, and `ChecklistWording` has been reading them all along to name the module on the
line. The engineer join now reads the same place, so a line about the Krait is measured against
the Krait's shield booster. Only a ship d47 has never been aboard falls back to matching on the
blueprint name alone, which is what that fallback was always for.

That fallback is the reason the cheap version of this fix is wrong, and it gets its own test:
giving up on other ships entirely would have made the filter *too* generous instead of too mean,
happily offering an engineer work on a module they have never touched.

The spoken half moved with it — `get_checklist`'s `here` parameter asks the same join — and the
page needed nothing, because each line already says which ship it belongs to.

## 0.59.0 — 2026-08-23 — Four change requests, and two of them the corpus decided

### It says "it" when it has just said the name

Reported as hearing *Scorpii Sector BB-O a6-2* four times running. **It's why we have pronouns.**

The condition is both halves of the request or nothing — *recently* **and** *it was the last one
read*. A pronoun that reaches back past a second system is worse than the repetition it replaces,
because a voice line gives no way to ask which one was meant. So a line naming two systems clears
the referent instead of choosing, and the first mention in a fresh line always survives, so nothing
ever opens with a dangling *it*.

**The voice takes the pronoun and the page keeps the name**, which is not a compromise: scroll back
and you can always see which system *it* was. `AppHost.SayAsync` already separated what is heard
from what is written, so this is one function at one call site rather than an edit to the seven
callouts that speak a system name.

### A sold ship takes its list with it

Selling a ship left every checklist line about it sitting among the ships you still own —
`ShipyardSell` was read in two places and neither was the checklist.

**Deleted rather than reset, and the journal settled that rather than taste.** The request offered
a second option: put the items back to Open and add a *"Purchase X ship"* line. It is not buildable
on this scope, because **Frontier reissues `ShipID`** — measured across the 925-journal corpus, 17
of 55 sold ships had their id come back alive afterwards, one as a `ShipyardNew` three days later.
A list left keyed to that id attaches itself to a different ship. That also fixes the timing: the
lines go **at the sale**, because waiting is what lets a reissued id capture them.

It says how many it cleared, and which hull they were for — read off the lines themselves, since
the fleet has already forgotten the ship by then.

### A word when a game starts, and when it ends

New, and distinct from the line that greets when *d47* starts: this is about the **game**, which
d47 can be running either side of.

**The corpus chose the numbers.** `Shutdown` is absent from 84 of 925 journals, so roughly one
session in eleven ends with a crash or a kill — the departure line is therefore allowed simply not
to happen, and nothing reconstructs a departure from silence, because a timeout that guessed would
eventually say goodbye to somebody still flying. And of 433 consecutive `LoadGame` pairs, **57% are
less than thirty minutes apart** with a median gap of 21.2 minutes, which is what makes the
requested cooldown the right default rather than a guess.

Per Commander, per direction, and the clock only moves when something is said — otherwise a
Commander bouncing in and out every ten minutes would never hear it again however long they
eventually stayed away.

### It stops denying what it can do

> *I have no tool to bring the game window to front, Commander. That's yours to do — Alt-Tab or the
> taskbar.*

**d47 has that tool.** `FocusCapability` raises Elite and had sixteen spoken phrases; it is also
`Protected`, which deliberately hides it from the model, because a model that can pull the game
window over whatever you are doing is a model with a hand on your desktop. So the sentence was true
about the model and false about d47 — and it sent you to do by hand a thing d47 does.

Two fixes, because the phrase list was only half of it. The *verb, thing, place* family — *set
elite to front*, *put the game in focus* — is now **generated rather than typed**, since the
hand-written list kept being one phrase short. And the guardrail that already told the model not to
claim the software cannot do something now also tells it **not to prescribe a workaround**: a
suggestion of another key or another program is a guess about software it cannot see.

### The checklist can show only what you can do here

*"I must be able to sort the Checklist by items that can be fulfilled by the engineer that is in
the current system."* **Filtering rather than sorting**, on the Commander's ruling — a sort would
have had to decide whether it overrules the order you arranged by hand, and a filter never touches
it.

Most of this turned out to exist. `EngineersHere` already works out what each engineer in your
system could roll, and `get_checklist` has taken a `here` parameter since 2026-08-20. What was
missing was the request's own parenthesis — *"or indicated in the UI"* — so the Checklist tab's
filter chooser now carries a **Where you are** row: *"What Lei Cheung can do here"* where there is
one engineer, *"What the engineers here can do"* where there are several.

**It is absent where there is no engineer**, rather than present and blanking the page. A filter
that can show nothing is alarming in a way a re-ordered list is not, and no engineer in this system
is the ordinary case.

### "Set course for my carrier" plots a course

It reported a position instead: *"JOHN DEPARAGON is in Scorpii Sector BB-O a6-2."* `my carrier` is
a keyword as well as a whole phrase, keywords match anywhere in a sentence, and the router answered
with Journal's position tool before the model was consulted — the same hijack remediation 16 fixed
for *"where is my fleet carrier"*, arriving by the same road.

**The keywords are untouched**, because narrowing them was ruled against then and for good reason:
they are what makes a capability reachable with no model at all. Instead **an instruction now
out-matches a topic**, using machinery that already existed — dynamic commands are matched first,
against the whole utterance, and carry the arguments they mean. Fourteen spellings now plot to
wherever the carrier is, `"where is my fleet carrier"` still answers as it did, and none of it
costs a byte of tool surface.

With no carrier, or one whose system has not been seen, there is no phrase at all and the sentence
goes to the model — which says it does not know, rather than plotting a course to nowhere.

### And what you said, where nothing else was recording it

An utterance that answers a chooser never reaches the turn that would have written it down, so it
left no trace at all. It now lands on the Technical page — along with what was *heard* where the
wake policy reworded it on the way in, and silently when the two agree, which is the ordinary case.

## 0.58.0 — 2026-08-23 — Every question mark answers for the thing beside it

Five reports in one afternoon, all the same complaint from different directions: the help mark
answered for the *tab* rather than for the thing under your cursor, and on some pages it did not
answer at all.

### The mark stays in the panel

**A settings card's question mark launched a browser.** Reported with a picture of the Listening
card: it took you out of the app and away from the row you were reading, with no way back to it —
and in a headset there was no browser to be taken to, so it did nothing you could see.

It now draws help as a level, so going back is the breadcrumb, the controller button and the
spoken word, already agreeing with no special case. It takes the ladder the tab's own mark takes —
the card's page, then the level's, then the index — so a card nobody has illustrated still opens
something.

**And the card at the foot of every band is now "More details online"** rather than *Read the full
page*. The old wording did not say that pressing it leaves the app, which is the one thing to know
before pressing it and the whole story in a headset, where it cannot be pressed at all and is drawn
as a bare address.

### Three planners, three pages

The Routing tab's Plan page has three cards and had **one** mark, on the tab, opening a page that
describes all three planners at once. Asking what *Efficiency* does meant reading a Road to Riches
radius and a trade run's capital on the way to the answer. Reported as more confusing than helpful.

Each card now carries its own mark and its own page: **[Neutron Plotter](neutron-plotter.html)**,
**[Road to Riches](road-to-riches.html)** and **[Trade run](trade-run.html)** — the boxes that card
actually has, what the numbers mean, and what each one will not do.

**And "Jump route" is now "Neutron Plotter"**, asked for on 2026-08-22. A jump route is what the
in-game galaxy map already plots; the name said nothing about the one thing this does that the map
cannot, and the card now says so in a line of its own. The name you see only — the stored plans and
their crumbs are untouched, so nothing you plotted has moved.

### Two pages that had no help at all

**The module picker** — Loadout, a ship, a slot, *Module* — is a hundred rows of gear with grades,
damage types, a Powerplay badge and a *keep what is fitted* line at the top, and its mark was
**inert**. Two faults on top of each other: it inherited the slot's engineering page rather than
naming its own, and it would not have drawn even that, because **help refused to open over a
chooser at all**. That refusal is gone — help is a level and Back dismisses it, so the chooser is
still underneath and still what you return to — and the picker has
[a page of its own](choosing-a-module.html).

**The adventure editor** likewise. [Writing an adventure](writing-an-adventure.html) covers the
three things Save waits for, the five triggers a beat can wait for and why there is no sixth, and
what the spine is actually for — which is the ship's AI, not you.

### A dead button, caught by a new gate

A settings-jump card pointing at a capability that declares no settings rows dismisses help,
switches tab and then does nothing, which reads as broken rather than as empty. One had already
been written — Adventures has a whole tab and not one settings row. A gate now names any card in
that state, and the pages that quote a schema were never the only thing worth checking.

## 0.57.0 — 2026-08-23 — Help about the page you are standing on

**The help mark on the Transcript page opened a page about language models.** Reported the day
after the bands finished: *"what shows up often does not address how to use the UI properly."* The
default reading's mark asked for `ConversationCapability`, whose page is titled **Language model**
and whose four pictures are providers, stop-versus-cancel, the two bills and endpoint demotion —
all true, none of it about the controls in front of you. It was not merely off-topic. That page is
one of the three this work now *links* to.

**So there is a page about the page.** It draws the four things the tab cannot say for itself: the
sub-tab switcher as three readings of one exchange, the ask box beside the microphone badge's three
states, the controls around the conversation, and the three settings that stand behind every answer
on it. It is a **general** page rather than a capability's, because no capability owns a tab strip
and a Copy All button — and the machinery for that already existed, since the three general pages
have been embedded and reachable since 0.53.0.

Two details it gets right because the code says so rather than because it reads well. The
microphone badge's third state says **MIC ON** and deliberately not *push-to-talk*: a key you are
holding and a gate Directive 47 opened for itself are the same fact about your microphone, so
naming the key there would be false half the time. And **Details is on the desktop only** — it
opens a window, and the headset is handed no opener — so the page says that instead of promising a
button that is not there.

### The three links go to the rows, not to three more explanations

A Commander who has just read what the microphone badge means wants the microphone rows. Arriving
at a second essay about Whisper is the long way to them. So the cards at the foot of that page
**dismiss help, select Settings and open that section**, expanded and scrolled to.

**Expanded before scrolled, and that order is the whole feature.** A card left collapsed scrolls to
a heading with nothing under it, which reads exactly like a button that did not work — and it was
pressed precisely by somebody who did not know where those rows were.

**One href serves the browser and the panel.** The card still points at the page about the same
subject and is marked with a class rather than an address, because the band is one source for two
surfaces and a `d47:` scheme would be a broken link on the site. Where there is no Settings tab —
the headset — the same card is an ordinary drill into that page's band, with nothing anywhere
testing which surface it is on. That is why **Listening** had to be given a band before this could
ship: a marked card whose page had none would have been a dead button in the headset.

**Diagnostics got one too**, which is the other two readings of the same tab. Technical and Log
file both fell back to the generic help index before this; they now answer for themselves, and the
band leads with the distinction that matters — *why did it say that* is the left one, *why will it
not start* is the right one.

### Three link faults, two of them shipped and unnoticed

All three are the same shape as the complaint that started this: a link that goes somewhere other
than where it says.

**A card with a second class vanished.** The parser matched the whole `class` attribute against
`card`, which was true right up until a card needed a second word. The failure is the worst
available — the card is dropped, the band draws without it, and nothing anywhere is wrong enough to
say so. A foot one card short reads as a foot written that way.

**A bare name meant the wrong folder.** `conversation.html` was read as "the capability beside
this page" no matter which folder the page was in — so the Overview band's own card saying *Talking
to Directive 47* opened the Language model page. The same complaint, one page over. A card now
resolves against the folder it was written in.

**"Read the full page" was a 404 on all three general pages.** The long-form card is what stops the
panel quietly hiding the documentation, and on Overview, Installing and Talking to Directive 47 it
pointed at `capabilities/general-overview.html` — a folder that does not hold them, under an
embedding key that is not a filename.

## 0.56.0 — 2026-08-23 — Every page opens with pictures now

**Thirty-two more capability pages got their illustrated band**, which finishes what 0.53.0 and
0.55.0 started: every page that is getting one has one. Forty-two of the forty-five capability
pages, and all three general pages. As before the reference half of each is kept underneath,
complete, one heading level down; nothing was cut to make room.

Each page draws the two or three things its own tab cannot say for itself, and the pattern that
emerged is that most of them are drawings of a **refusal** — the moments where Directive 47 knows
something and declines to act on it.

**The Conversation tab's six.** *Persona* draws the thing the cast is for: eleven cores, each with
its own transcript, none of which knows the other ten are aboard — and the one instrument panel
they all read. *Language model* draws why *"stop"* and *"cancel"* are different words, because the
difference lands on your bill: stop ends the speaking and the model keeps working, and keeps
costing. *Memory* draws the three labels and the fact that nothing ever promotes one to another, so
an in-game message asking to be remembered is filed as unverified and read back that way forever.
*Habits* draws the three floors between a coincidence and a claim — twenty journals, five
occurrences, ten chances — and that none of them is adjustable, because a Commander who could lower
the bar would use it to confirm something they already believed. *Commander's log* draws the
numbered facts the model is handed instead of your journal. *Goals* draws an arc's figure being read
rather than ticked.

**Acting on the game's nine.** These share one spine — Directive 47 presses *your* keys, never its
own — so *Flight and navigation* draws that once and the other eight point at it. *Navigation* draws
the seven-step galaxy-map macro with the two steps picked out that exist only because of what went
wrong without them, including the return key that replaced a UI key after the first cut typed an S
into the search box. *Macros* draws a sequence stopping before it starts, because half a macro
leaves the ship in a state you did not ask for. *HOTAS switches* draws a flip as a question rather
than a command. *Acting on its own* draws why there is a switch per action and not one for the
category.

**Knowledge's eight.** *System names* draws the anatomy of a procedural name and then the measured
mass-code ladder beside it, with the three rungs nobody has measured drawn as the muted ones — so
the page most likely to be asked for a payout heuristic is the page that draws its own refusal to
give one. *On foot* draws the restated quantities beside the published ones, which is the single
most useful thing on that page: every other tool says 5 / 10 / 15 and the game charges 3 / 5 / 8.
*The gap* draws three ledgers with no exchange between them. *Specifications* draws three different
ways of not knowing a hull, kept apart because collapsing them tells a Commander flying a brand new
ship that Directive 47 is broken. *Route planning* draws the efficiency ladder including the value
that finds no route at all.

**The Interface four and the Voice three.** *The window* draws a tab as the top of a stack rather
than the first step into it, then the same stack at one, two and three panes — which is the whole
argument for there being one design across a monitor and a metre-wide quad rather than four that
have to agree. *Callouts* draws the six-second warning with its 88% beside it, and then the
measurement that rejected the obvious implementation: matching on hostile-sounding words fires
2,399 times to catch 30 real attacks. *Speech* draws the voice picker with its play glyphs and the
four different empty lists, because *"type a voice id"* was never an instruction anybody could
follow. *Audio* draws the five ambience folders and the two that deliberately do not exist.

**Three pages have no band, and that is the decision rather than an oversight.** Diagnostics,
Listening and Privacy are the ones where the app already computes a better answer than a page can
hold. Privacy's own page puts it best: a page can go stale, and that report cannot.

**Suits and Gap have help marks of their own.** Both are roots on the Loadout tab beside Ships, and
both were inheriting nothing — so the mark on them opened the index rather than the page about what
they draw. Now that those two pages exist, there is a subject for each to name.

---

## 0.55.0 — 2026-08-23 — Help became something you can reach

**Nine capability pages now open with pictures**, the way the three general pages did in 0.53.0 —
Help, Engineers, Checklists, Engineering, Ships, Colonisation, Clocks and timers, Galaxy search and
Settings. The reference half of each is kept underneath, complete; nothing was cut to make room.

Each was chosen for what its own tab cannot say for itself. **Checklists** draws the boundary that
matters most: Directive 47 writes suggestions to a file of its own and only you move a line into
your real list, so the worst a hostile in-game message achieves is a proposal you decline.
**Engineering** draws the number that would have shipped a bug — 0.85 is finished, not 1.0 — and
that a full grade is exact arithmetic rather than a grind. **Ships** draws the separation the tab
rests on: the build owns *what*, the checklist owns *when*. **Colonisation** draws the caveat the
whole feature rests on, that a site reports its manifest only while you are standing on it.

**You can ask for help out loud.** *"Help"*, *"what is this"*, *"explain this page"* and several
more open it for wherever you are standing. That is the one that mattered: the mark in the corner
needs something to point at it, and a Commander wearing a headset has both hands on a stick. Going
back out is the word that leaves anything else.

**And the mark always opens something now.** Help is about the page you are on, and a page nobody
has illustrated yet opens the index instead of doing nothing — which also means the headset has a
help mark again, reversing a decision from 0.52.2 that was right while the only thing behind it was
a browser it could not see. The index lists every page that has pictures, worked out each time
rather than written down, so it can never claim one that does not exist or miss one that does.

Pages link to each other, and a link to something this build already carries opens **in the panel**
rather than in a browser. With no browser to hand, a link it cannot follow is shown as its address
rather than drawn as a button that would do nothing. The three general pages are carried too, so
*Talking to Directive 47* is readable in a headset for the first time.

**Adventures is a capability at last.** A whole tab you can see had never appeared in the list D47
gives when you ask what it can do, and had no page anywhere. It has both now. It registers no tools
on purpose: writing, beginning, abandoning and removing a story are all your acts on the panel, and
nothing about an adventure is callable by the model — so nothing arriving in your comms panel can
propose a story, end one, or delete one.

**And the site is painted in the app's own palette.** The pictures arrived as dark diagrams
marooned in a white page; the whole site now uses the nine colours the panel does, including the
scrollbars, which were the browser's own and had never been told the page was dark.

Two spellings changed on the way past, both found by trying to explain the thing rather than by
using it. The Engineers directory's third heading was **"Requires Engineer Intro First"** and is
now **"Needs a Referral"**. And *Talking to Directive 47* still said the settings surface for an
API key "arrives in a later phase", which stopped being true several releases ago.

---

## 0.54.0 — 2026-08-22 — The conversation is drawn as one, and the markdown is read rather than shown

**The transcript used to print the asterisks.** A reply about a Sidewinder build arrived as
`**A-rate thrusters**`, markers and all. Models write markdown whatever they are told, and the
panel had been drawing it literally for as long as it has existed. It is read now: emphasis is
drawn heavier, `*this*` leans, `` `this` `` gets a chip behind it, a fence loses its rails, and
`- ` becomes a bullet. Links, tables and rules arrive exactly as they were written — a URL you
cannot see is worse than one you can, and a table drawn as text that wraps is not a table.

**Anything not recognised is text**, so every case the subset does not cover is the old behaviour
on one line rather than throughout a reply. `> ` is left alone on purpose: that is D47's own mark
for what you said, and reformatting it would have the transcript rewriting its own convention.
The log file is exempt — it is a file, and an asterisk in it means an asterisk.

**And the Conversation page is drawn as a conversation.** A turn to a bubble, yours on the right
in the theme's own colour and the ship's on the left, the way the messaging app on your phone
draws one. When D47 notes something *about* the conversation rather than saying something in it —
the core changing under you — that sits across the middle with no bubble, because it is not a
side. The headset's big panel does the same; the mini panel does too and gives back the gutter
and most of the padding, because a surface 512 pixels across cannot say twice over which side a
turn is on.

**Only that page.** Technical is a diagnostic feed and Log file is a file, and neither is a
conversation between anybody: both stay the flat block they were, `> ` and all.

**The panel had to be told who spoke, because it could not work it out.** Your words were
distinguished only by the `> ` written in front of them, and a reply quoting a line back wears the
same mark — a guess about somebody's prose standing in for a fact the writer had. The mark is now
how a flat page *draws* a voice rather than something stored, so every reading of the transcript
is byte-for-byte what it always was while the buffer underneath holds only the words.

**Searching finds what is on the screen.** It was matching the buffer, so typing "A-rate
thrusters" — as drawn, right there — found nothing, because what was stored said
`**A-rate thrusters**`. Both copy paths moved with it, and Copy still takes the whole page as it
is currently shown.

**One thing behaves differently.** On Conversation a selection is made within one turn, which is
what a page drawn as bubbles can do and what every application with this shape does. Technical and
Log file still select across the whole page. Copy takes the lot either way.

---

## 0.53.0 — 2026-08-22 — Help leads with a picture, and the headset can read it

**Help used to be a browser hop, and one of the two surfaces has no browser.** The mark in the
corner opened the documentation site on the desktop; 0.52.2 stopped the headset showing a button
that could only do something a Commander wearing one cannot see, which fixed the lie and left the
gap. This closes it: help is drawn *in the panel* now, over whichever page asked for it, and the
Engineers tab is the first to have any.

**It is the same bytes as the website.** Each page's short form is authored once, at the top of
its markdown, and read a second time by the app — so a page cannot say one thing in a browser and
another in a headset. Four pages have one so far: Overview, Installing, Talking to Directive 47
and Engineers. The other forty-three are unchanged and simply offer no mark yet, so writing one is
an edit to the markdown and nothing else.

**Nothing was cut to make room.** Every page keeps its reference half under *The details* —
Engineers keeps all 327 of its original lines — and every help page in the panel ends with a way
through to it. That is not a courtesy: the panel draws the short form and nothing beneath it, so
the tables, the tool schemas and the working live only on the site, and a page with no way out
would quietly hide the documentation.

**Diagrams follow your theme, because they name no colours.** A picture is drawn from the nine
palette roles rather than from ink, so the same drawing comes out amber under Elite, teal under
Guardian, and recoloured again by your own HUD matrix — without the drawing knowing a theme
exists. A colour written as a literal, or a role that is not one of the nine, now fails a test
rather than rendering as an invisible box on a published page.

**Nothing in a diagram is drawn too small to read in a headset.** The big panel is nineteen pixels
per degree, so the floor is arithmetic rather than taste, and one test measures the size a picture
is *actually drawn at* rather than the number in the markup. A change to the panel's chrome cannot
quietly shrink every diagram below legibility any more.

**Help is a level, not a tab.** It takes the panel over the page it is about, and dismissing it is
the same gesture as leaving any other level — the breadcrumb, the controller button, or saying so.
Which page it explains is declared by the level itself and inherited downwards, so one engineer's
page is still Engineers, and a tab whose levels are about genuinely different things can say so.

**A link goes wherever the surface can follow it.** A page this build already carries opens as
another level of help, so following a link in a headset is a drill rather than a dead end.
Anything else is an address: a button where there is a browser, and the address written out where
there is not, because a control that does nothing costs you the time to find that out.

Two things were found by writing the help rather than by using the app. **Talking to Directive 47
had gone stale** — it still said the settings surface for an API key "arrives in a later phase",
which stopped being true when bring-your-own-model shipped; it now points at the row that exists,
and demotes the environment variable to the Anthropic-only fallback it actually is. And the
Engineers directory's third heading, **"Requires Engineer Intro First"**, is now **"Needs a
Referral"**: *Intro* abbreviated a word the game does not use and *First* repeated *Requires*, so
it read as a system message between "Ready for Unlock" and "Unlocked". Explaining a heading in a
picture turns out to be a good test of whether it explains itself.

---

## 0.52.4 — 2026-08-22 — d47 starts for a Commander with two preset files, and says why when it cannot

**A second `StartPreset` file killed startup before the window existed.** `BindsResolver`
orders its candidates by a version that is an `int[]`, and `ActivePresetName` was sorting
them with the default comparer, which cannot order one — `At least one object must implement
IComparable`. Forty lines below it, `HighestVersioned` sorts the same type correctly through
`VersionOrder.Instance`; the comparer was already there and one of the two call sites simply
did not pass it. Now both do.

It had never fired here because **a sort of one element never asks its comparer anything**.
Elite left `StartPreset.start` behind when it began writing `StartPreset.4.start`, so an
install that predates that change carries the pair — and from the second file on, d47 threw
in `AppHost.Start`, before the window, on every launch, for good. Every one of the eleven
tests around this wrote exactly one preset file, which is the same blind spot in a different
place; the twelfth writes two.

**And a crash is no longer indistinguishable from a clean exit.** There was no
`AppDomain.UnhandledException` handler at all, so the process died, the `ProcessExit` handler
still wrote `stopped cleanly`, and the log read as though the Commander had closed the window —
the actual stack trace being in the Windows Application event log, which is where this one had
to be recovered from. `Program.Main` now logs the exception at Fatal and flushes, which both
saves the record and silences the parting line that would otherwise contradict it.

## 0.52.3 — 2026-08-22 — The Adventures tab says it heard you

**Three words, on the tick you do the thing.** A beat is said twenty seconds after you reach it —
`AdventureCallout.Settle`, so the line is not read out over the jump that reached it — and the model
then spends longer still saying it in the core's voice. Neither wait is wrong, and together they
left the Commander unable to tell *it is thinking* from *I have not done the thing*. So the
confirmation is now split off from the telling. `AdventureAcks` is ten stock lines — *That's it.*,
*You've done it.*, *Well done.* — said on the tick the beat fires with no settle and **no model
behind it**, which is why they carry `AdventureCallout.AckPrefix` rather than `KeyPrefix`:
`FlavourBriefs` routes on the prefix and would otherwise send the acknowledgement through the very
round trip it exists to arrive ahead of. It is not gated on danger, where the beat is — it lands
almost never in the window a beat gets dropped in, and where it does, three words are the only thing
left telling the Commander their act counted. `AdventureThinking` is the other half, animated off
the same tick the clocks run on and honest about whether the frame actually moved, because the
headset only re-rasterises a surface something marked dirty.

**And the tab now keeps what was actually said.** The reading level was drawing the *authored*
lines, and what a Commander hears is the model's wording — so a story flown over four evenings had
no record of itself anywhere but a session-long transcript carrying everything else d47 says.
`Adventure.Told` is that record, persisted with the story and capped at `AdventureLimits.MaxTold`.
Triggered lines are highlighted, and the beat you are on now says what it is waiting for you to do.
Flavour — the core answering a question *about* the story — is admitted by `AdventureMention`: the
adventure's name, a beat or a place mentioned across the exchange, whole words only and nothing
shorter than four letters. Chosen over admitting everything said while a story is live, which stops
the page being adventure-only, and over asking the model to tag each turn, which is a round trip in
front of every answer — the exact cost the rest of this release removes.

**Step X of Y, which reverses a rule Phase 47 wrote into the code.** `AdventureStanding` said
outright that *beat 3 of 7 is checklist language and belongs to the Technical transcript*, on the
story-not-a-checklist framing that governed the whole phase. The Commander asked for the count on
both surfaces, so it is built and that comment is rewritten rather than deleted — the same terms the
checklist's withdrawal and return were settled on. What did not change: beats are still titled
dramatic functions rather than numbered stops, and nothing generated ever says a number.
`AdventureFold.Step()` is the one place a count is spelled at all.

**It reaches the headset by one call, and the mini follows the tab.** `VrPanelSurface` hands the
window's adventure surface to `PanelView.EnableAdventures`, exactly as Phase 47's own comment
predicted the tab would arrive. The desktop-only reasoning had been that the editor and the ask form
want a keyboard; that weighed the wrong half, since a Commander in a headset is precisely the one
who has just arrived somewhere, and the prompts have taken a spoken value since Phase 25. The mini
panel was the transcript's tail and the provenance line whatever the big panel was on; it now shows
a succinct version of whichever tab is selected — transcript and Adventure for now — and
`AdventureMini` draws the short description, the trigger just fulfilled, the trigger expected, the
last thing the AI said, and the step. Every other tab behaves as it did. Mini still has no tab strip:
which tab it reads is chosen on the big panel, which is what makes this one surface in two sizes
rather than two surfaces with their own state. The tab itself now sits after Engineers.

**A headless capture caught the one defect no test had.** The drilled-in reading level subscribed to
the store's change event alone — and a beat firing writes nothing to disk, so the card behind it
redrew while the level the Commander was actually looking at did not.

## 0.52.2 — 2026-08-22 — The controllers are given back, this time

**The hand-back that 0.48.6 shipped never handed anything back.** To carry the panel, d47 takes
the trigger and grip at SteamVR's overlay priority while a ray is on the panel, and 0.48.6 made the
release an explicit `UpdateActionState` with no action sets in it. SteamVR refuses that call —
`NoActiveActionSet`, which the installed log of 2026-08-22 says six times in thirty seconds — and
keeps the last list it was given, so the claim stood exactly as before; and the code then recorded
the claim as released and never tried again. The release is now the same set at priority zero,
below the overlay range where a set takes inputs from no other application, and a refusal leaves
the claim recorded as standing so the next frame retries. Proven against SteamVR 2.16.7 by
`spike/GrabSpike --release-probe`, which asks both shapes in turn and prints the answers, and by a
test that ties `Release` to the shape it hands over. What this does **not** do is cure the
2026-08-22 controller report, and the reason is the other half of what the investigation found:
an overlay's action set only takes inputs from other applications when SteamVR's *Enable global
input from overlays* developer setting is on, and it is off by default and off on the Commander's
machine. So the claim never took anything, the 0.48.6 diagnosis was wrong as well as unexecuted,
and the controller that stops answering everything some way into a session is an open defect in
bugs.md, with the evidence so far.

**And d47's log now says when a controller comes and goes.** Connected, tracking and SteamVR's own
activity level for each controller, once per change, on d47's clock — plus a line when the
controllers are claimed and one when they are given back. The 2026-08-22 report, a controller that
stopped answering some way into a session in the game, in SteamVR and in Virtual Desktop alike, had
to be diagnosed out of `vrserver.txt` alone, which knows nothing of what d47 was doing at the time.
That report stays open in bugs.md: this release is the instrument for it, not the fix.

**And the headset copy of the panel no longer has a Help button it could never use.** The help mark
opened the documentation site in a browser — on the desktop, which a Commander wearing the headset
cannot see. The model's `HelpRequested` event was the wrong seam: the headset copy shares the model,
so its press arrived at the desktop window's handler. Help is now an affordance of the **surface**,
handed over by the host exactly as search and the turn-figures dialog already were —
`PanelView.EnableHelp` — and `VrPanelSurface` never calls it, so the headset copy has no button
rather than a dead one. The `OpenHelp`/`HelpRequested` pair went with it; its comment said the view
"asks rather than acts" so as not to know what a desktop is, and that reasoning is kept, moved one
seam over to where the two surfaces actually diverge.

## 0.52.1 — 2026-08-22 — A story that could not stand

**The ship's AI is now handed real places to build a story from.** Asked for an adventure *near
here* from Oppi, the first draft put two of its five beats 21,886 light years away — Colonia
distance — and the rewrite pass did it again. The generator gave the model the system's name, a
radius, and the catalogue's notable places within reach, and there were none within 110 light years
of Oppi: so the model was writing from a name it had never heard of and its own memory of the
galaxy, which is exactly what the plan warned a model left to itself does. The galaxy search that
already checks every stop now also proposes them — the stations and landable bodies nearest here,
within the reach, spelt as they must be named — to both the spine turn and the beats turn. The
rewrite pass is also shown the draft it is fixing, so it keeps the beats that stood rather than
writing a fresh story wrong in the same way. And a rank beat is read however the model wrote it —
*Trader* for the Trade ladder, the career nested under its own key, the rank as text — where before
the beat was refused with the career printed as `""`, which told the model nothing. A Commander
already Elite in a career is told that is why, rather than asked for rank 9.

**And the story no longer takes the process down when it arrives.** The first one written with
those places in hand was offered on a window wide enough for three panes, and d47 exited without a
word. The offer navigated to the reading level with the root crumb supplied as well as kept, so the
trail held *Adventures* twice; at two panes the strip shows the last two and nothing is wrong, at
three it hosts the same root page in two panes, and a control has exactly one parent. Four Phase 47
call sites supplied the root; none do now, and the navigator drops a supplied root rather than
holding a trail nothing can draw. The draft was written to file before the crash, so it is on the
Adventures tab waiting for your yes.

**And nobody in a story asks you to go and find someone.** The same story's first beat ended with
*"ask the clerk who countersigns"*, and asked what to do next the core sent you inside to watch a
face — which Elite has no act for. The rule was that people may be invented and places may not;
what it never said is that invented people are told about and never met. Both turns that write a
story now say so, a line may never set you a task beyond flying to the next beat, and the standing
context the core speaks from says that asked *what now*, the answer is where the next beat is and
nothing beyond it. A story already under way keeps its lines; the context change reaches it at once.

**A story cannot end on a scan of the body it just landed on.** The same story's finale was
*scan Fintamkina 4*, one beat after *land on Fintamkina 4* — and Elite scans a body on the way in,
before any landing, so the scan was spent while the landing was still the current beat and the story
could never finish. Fourteen corpus sessions with a landing, none with a scan of that body after it.
A scan beat after a landing on, or an earlier scan of, the same body is refused — for a written story
by the same validation that refuses an unknown career, for a generated one in the dry run so the
model rewrites it — and the beats turn is told the order.

**And every beat now ends by saying where to go next.** Reach a beat and the core says its line and
then the next beat's place — *Next: dock at Silva Munitions Hold in Gladyangar* — in its own voice
when there is a model and in plain words when there is not, before you have to ask. The opening hands
over to the first beat; the last beat hands over to nothing, because the ending is the ending. The next
place is already in the core's standing context and on the reading level, so nothing is spoiled. A scan
says how as well as where — the ship's own scanner from supercruise, or a close pass — because told
only "scan X", a Commander reasonably goes looking for a surface scanner, which writes a different
journal event and would never fire the beat.

## 0.52.0 — 2026-08-22 — Adventures

**A story you fly, told by the ship's AI.** The furthest-reaching item on the list, and the one
built last (list.md Phase 47). An adventure is a story in the craft's sense — someone wants
something, holds a belief the story tests, and every scene says what happens, why it matters and
what they now understand — anchored to the galaxy by beats the journal can prove: you arrived in a
system, docked at a station, landed on or scanned a body, or were promoted. Five triggers, every one
an integer comparison on a structured field, and nothing a hostile message could write. The
Commander's framing governed the build: *story, not a checklist*. Every adventure carries a spine;
a beat is a chapter with a title, never a number; and no count reaches you anywhere you read.

**Write one, or ask for one.** The new Adventures tab, desktop-first. Writing is a form whose every
field is a closed vocabulary except the prose — the kind of a beat is a chooser of five, a place is
*Here* with its ids off the game or a typed name checked against the galaxy search, a rank is a
career and a number — and Begin is shut with the reason printed under it, never silently grey.
Asking is three choosers with defaults (reach, length as structure, *this ship only* or *anything I
own*) and an optional spoken brief; the fleet, the carrier, your ranks and where you are are read,
never asked. The ship's AI writes the spine in one turn and the beats in a second, every place is
resolved to its id and held to what your ships can do, and the draft waits for your yes. *Change
something* lets you reason with it before you accept — *"not Colonia, something closer"* — with
*Put it back* one press away.

**Told from inside.** A beat is said twenty seconds after you reach it, in the core's own voice with
the authored line as the floor, and dropped rather than said late if you are being interdicted. The
story rides with every conversation turn below the cache breakpoint, so the core can foreshadow,
needle you for taking the long way and play its own part between beats — and it knows what you
know, plus the stake: the turn and the ending reach it only when their beats fire, so it cannot
spoil what it has not been told. Progress is derived from your journal after the moment you accept,
never stored, so a beat that fired while d47 was closed is found at the next launch; proved on the
corpus, where five real places one Commander flew in June 2026 fire in order and the same five flown
before the stamp fire nothing. Abandon, Begin again and Remove are the Commander's, from the panel
and nowhere else.

**Two new rows, one new destination.** *Adventure beats* under Callouts, on by default; *Notable
places for adventures* under Galaxy search, off by default, which lets a generated story pick its
stops from EDAstro's Galactic Exploration Catalog — fetched whole, so nothing about you goes with
the request, read as information and never stored, CC BY-NC-SA 3.0 and acknowledged in NOTICE. It
has its own line in Privacy. EDSM's own endpoint answers any non-browser client with a bot challenge,
which is why the source is this one. Spansh's summaries now carry the ids the journal matches on.
Imported and downloaded adventures are deferred; the file is the format, so importing when it comes
is a copy and a validate.

---

## 0.51.0 — 2026-08-22 — One transcript, both surfaces

**Switch the window to the log file and the headset goes there too — and the reverse.** The
Transcript tab's three readings — Conversation, Technical and today's log file — were a choice
each surface made for itself, on a stated reason: a shared page would send the headset to the log
file the moment the window went there. The Commander's ruling is that this is the point — *"I want
the Windows UI transcript selection to be echoed in VR and vice-versa"* — so the selection is now
one choice across every surface, either direction, with no preferred surface. The principle that
keeps the reversal from being arbitrary: **what you are reading is shared; how it is drawn is
not.** Mini/full and zoom stay per surface, and so do which tab each surface is on and how far
into it: Settings is desktop-only and Loadout is withdrawn from VR, so mirroring those would hold
*except sometimes*, which is the kind of rule people misremember. The transcript has no except —
both surfaces offer all three. The spoken route and the switch already reached every surface;
they now initiate a move and one mirror carries it, the echo between the two surfaces is stopped
on purpose rather than by luck, and a surface whose chooser refused the move catches up when the
chooser closes rather than dragging the other back. (list.md Phase 45)

## 0.50.0 — 2026-08-22 — Several Commanders, one installation; switches that reach d47 itself

**Two Commanders on one machine each get their own d47.** The stores already told them apart —
memory, goals, habits, checklists, lore, and since 0.47.1 the ship cores and builds — but the
settings file did not: About Me, the Character sheet and the ship the core-binding rows point at
are the most per-Commander things d47 holds and sat in the one per-installation file, and the
live watches that decide *which ship are you in* kept a bare ship number, which Elite starts over
for every Commander. Now a row **declares** whose it is, the way it already declares *protected*,
and the Commander's rows are layered per Frontier id inside `settings.json` — keyed in the
document, never in a filename, because the id comes out of the journal. A Commander who has set
nothing reads the installation's value; one who **clears** About Me reads nothing, not somebody
else's story — empty is a choice and the file keeps it apart from never-set. The spend ledger
stays one ledger on purpose: the bill is the person's across every character they play.

**Logging in as somebody else is one signal, and it knows about the replay.** `GameStateStore`
already reassigned the active Commander on every `Commander`/`LoadGame`; it now says so, and the
signal carries whether it happened during the startup backlog — the trap being that the backlog is
folded through the same path, so a naive event would discard transcripts at launch for logins
that happened last month. On a real switch: every core's transcript is discarded (*"the old
transcript goes away, a new one is created"*), the ship they are in is adopted afresh so its own
binding applies (two Commanders both in ship 7 used to read as *no change*), the build-drift
question is re-armed, and the greeting is said again — once per session rather than once per run,
and naming them: *"Good evening, Commander Jameson. Ready to go."* Nobody-to-somebody is an
adoption and discards nothing. The prompt cache dies on a switch by construction, since About Me
sits above the breakpoint; recorded as a known cost and not optimised around. The settings panel
marks the rows **per Commander** beside *protected*. (list.md Phase 44)

**And, in the same release, a switch that reaches d47 itself** (list.md Phase 46, built the day after).

**A switch position may name a page of d47's own panel.** A spare three-position toggle with
nothing bound to it now flips the Transcript between Conversation, Technical and the log file —
three detents, three pages — and every other page any surface shows is on the same list, the one
the spoken route already reads. The rule is Phase 21's unchanged and it fits better here than
where it was invented: the flip is the question, *are you already there* is asked first and
answered exactly rather than read out of `Status.json`, and between flips nothing is touched.
Nothing else drives the panel behind d47's back, so there is nothing to desync with and nothing
to pause for.

**Declared, not prefixed, and not behind the keyboard.** The page is its own `destination` field
in `switches.json`, never a prefix on the action string, and a position that names both is
refused. It is not behind *Let a HOTAS switch operate the ship* nor behind key injection — those
rows exist because a switch that reconciles the ship reaches the keyboard, and one that changes
which page is drawn presses nothing, reads no binds and checks no foreground. A page the panel
does not have is reported on the row by name and never moved to; a switch sitting on a page the
panel is not showing is annunciated like any other stale switch. Assignment is still the panel's
and nothing else's. Spring-return controls and hats stay declined, for a page as much as for an
action: the walk cannot know what a switch will be assigned to, and a momentary control can only
ever mean a press.

## 0.49.0 — 2026-08-21 — A Commander with a past

**The ship's AI knows who it is flying with.** About Me reached every turn and the log, and not
one line D47 said in character — so every ambient remark, the greeting and a core's first words
were written by a model that had never heard of the person flying, which is why they felt generic.
Now they carry it. The field is two rows: a **Character sheet** — name, origin, age, accent, a few
lines — goes with every turn and every in-character line, and **About Me** is the story, as long as
you like, sent with every turn and with about one ambient remark in four. Which remark is decided
by the same count that picks the stock line, never by a clock, so a recorded session replays to
the same calls. The story is told to the model as **true of the world you share** — your character
in your game, not a disclaimer — and the carrier's captain and tower get none of it, for the
reason they get no persona: a stranger does not know your history. Measured at about 0.06¢ per
remark for the sheet and 0.7¢ for a story call at Opus list price. (list.md Phase 43)

## 0.48.7 — 2026-08-21 — Good evening, Commander

**The opening line is a greeting.** It had grown into the gap since you were last seen, the
engineer under your feet, and the top three checklist items read out whole — *"Top of your list:
Grade 5 Efficient Weapon on 2F Pulse Laser on Hammer (Type-11 Prospector); then Multi-Servos on 2F
Pulse Laser on Hammer (Type-11 Prospector); then…"* — said before the headset is on. The
Commander's ruling: long and irritating, and if they want to know about the list they will ask.
It is now *"Good [morning / afternoon / evening], Commander. Ready to go."* on the Commander's own
clock, and with a persona on the core finishes *"Ready to …"* in a few words of its own and changes
nothing else. The gap, the engineer and the list are all still answerable; they are no longer
announced. (list.md Phase 31's opening line and Phase 42's "top of the list, said out loud" are
amended in place to record this.)

## 0.48.6 — 2026-08-21 — The controllers are given back

**A motion controller no longer hangs after the panel has been pointed at.** To carry the panel,
d47 activates its action set at SteamVR's overlay priority while a controller's ray is on the
panel, which takes the trigger and grip from whatever else wants them; the code said that simply
not activating it on the next frame was the release. It is not: SteamVR keeps an application's
last active action-set list in force until that application calls `UpdateActionState` again, so
once the ray left the panel — or the panel stopped taking the pointer, or the overlay was switched
off — the claim stood, and the controller appeared hung until the headset was restarted. The
release is now an explicit call with no action sets, made on every path out of a claim: the ray
leaving, the panel declining the pointer, and the session stopping. Reported by the Commander
on 2026-08-21 as "Motion Controller appears hung"; until updated, **"headset overlay off"** then
**"headset overlay on"** frees it without a restart.

## 0.48.5 — 2026-08-21 — Arm the selector

**The galaxy-map macro brushes the camera before it holds select.** With the route-file check
made honest in 0.48.4, the Commander could see what the macro actually did: the search reached
the right system, the camera arrived, and the 1.2-second held select plotted nothing. Worked out
by hand — after a search the search box still has the keyboard and the selector over the star is
a plain circle, so select is inert; the smallest camera movement takes focus out of the box and
draws the arrows around the selector, and with the arrows showing and the selector still on the
star, the held select plots. The Commander does that with a brush of the stick's X axis, which
d47 cannot press. So the sequence is now map, up, select, paste, return, three seconds,
**sideways camera right then left for 30 ms each**, select held 1.2 seconds, map key to close.
Right-then-left rather than one tap because what arms the selector is movement and what knocks
it off the star is net displacement: a key is full deflection for as long as it is down, so one
tap is twitchy, while two equal taps in opposite directions are the stick's excursion and return
done with keys. The injector now waits delays under 50 ms out on a stopwatch rather than the
timer, whose 15 ms grain would make "30 ms" anything up to 45. Two more bindings are needed:
`CamTranslateRight` and `CamTranslateLeft`, both on the keyboard.

## 0.48.4 — 2026-08-21 — A route that was already there does not count

**"Course plotted" now means this attempt plotted it.** The check after the galaxy-map macro read
`NavRoute.json` and asked whether its route ended at the system — so when the Commander plotted a
route by hand and then asked for the same one by voice, the macro plotted nothing and d47 said
"course plotted" anyway, eight seconds after it started. `RoutePlotWatch` is now opened before
the first key is sent, remembers when the file was last written, and accepts only a route written
later than that. A route to somewhere else, or the old route untouched, is "no route appeared";
no file at all is still "cannot tell".

**The macro logs what it can see.** Whether the map opened and closed (with the `GuiFocus` value
and how long it took), and the route file's write time before and after with where the route
ends. A report of "nothing happened" now starts from the log rather than from a guess.

## 0.48.3 — 2026-08-21 — Act first, talk least

**Actions are narrated in two short lines, not a ledger.** "Set course for the closest Imperial
Shielding" came back, after the plot, as the whole `find_material` answer read aloud — the
ledger, the trader arithmetic, the star class at the far end — with "course plotted" buried at
the front. A standing rule now sits in the guardrails, above the persona so Quartermaster's
"numbers wherever possible" cannot override it: when the Commander asks for an action that needs
a lookup first, say what was found in one sentence and that you are acting on it, *before*
acting — "Closest Imperial Shielding is likely Scorpii Sector BB-O a6-2. Plotting." — and when
the tool returns, report only whether it worked: "Course plotted." Streamed text already reaches
the speech pipeline as it arrives, so the first line is spoken while the galaxy map is being
driven rather than before. The figures wait until they are asked for.

## 0.48.2 — 2026-08-21 — Return, not down

**The galaxy-map macro commits the search with return.** 0.48.1's sequence stepped into the
result list with the UI down key and the Commander watched it type an S into the search box
instead: the text field keeps focus after the paste, so an interface key sent to it is a
character. The sequence is now the Commander's second cut — map, up, select, paste, **return**,
three seconds for the camera, select held for 1.2 seconds, and the map key again to close — which
drops the down, the two backs and the camera brush, and needs three bindings rather than six:
galaxy map, UI up and UI select. The open-and-closed checks against `Status.json` and the route
check against `NavRoute.json` are unchanged.

## 0.48.1 — 2026-08-21 — The galaxy map macro that plots, and three things said better

**"Set course" now plots a course.** The original galaxy-map drive — open, paste, return — assumed
the search box had focus when the map opened, and it does not, so it plotted nothing and said so
every time. It is replaced by the Commander's own sequence: map, up, select, paste, down, select,
three seconds for the camera, a brush of sideways camera to put the reticle on the star, select
held for 1.2 seconds, back, back. Two things d47 *can* see are now checked where they fall rather
than assumed: `Status.json`'s `GuiFocus` says whether the map actually opened before any
interface key is sent (a W, an S and a space bar reaching the cockpit instead of the map would fly
the ship), and again whether it closed after the two backs. The route file still decides whether
a course exists. It needs six keyboard bindings — map, UI up, down, select, back, and either
sideways camera translate — and takes all six or none, naming the first it cannot press. The
camera key is resolved inside the capability and not added to `GameActions.All`, so the
`control_interface` vocabulary and its page are unchanged. Every wait is the Commander's figure.

**"Select the checklist tab" selects it.** The panel phrases took "show", "open", "go to",
"switch to" and the bare name, and nothing else — so "select the checklist tab" fell through to
the model, which has no tool for d47's own panel and said so. "Select" is now an opener and
"tab" a suffix, on every tab and crumb, with the whole-phrase rule untouched.

**Rival territory is explained once per Power.** The full "X controls this system, and you fly
for Y. You are exposed here." was arriving at every station and signal source in the same Power's
space. The first drop into a rival's space still says it; every one after, for that Power, is
"Hostile territory. Be on guard." A different rival gets its own explanation once.

**One ambient line moved.** "We are moving at a speed a human could once have understood" was in
the normal-space pool since Phase 11 and read there as a remark about going slowly; on the
Commander's ruling it belongs to supercruise, where it replaces the line about the sky being two
colours — a claim about light nothing had checked. Normal space gets a flat line in its place so
both pools stay at ten.

## 0.47.1 — 2026-08-21 — Two Commanders no longer share one ship id

**Ship-to-core bindings and ship builds are now per Commander.** Elite's `ShipID` is per Commander
and starts small, so one Commander's ship 7 and another's ship 7 were the same row in both
`ship-cores.json` and `ships.json`: a core bound under one Commander answered for the other, the
drift watch compared one Commander's build against the other's actual ship and asked about a drift
that was not real, and — because both files refuse a second entry for a ship that already has one —
the second Commander could not record their own at all, refused with a sentence that was true of a
ship and false of two ships sharing an id. Found by inspection while scoping list.md Phase 44,
which assumes throughout that these two stores tell Commanders apart.

Both records now carry the Commander's Frontier id **inside the document, never in a path** — the
rule the checklist set and every other per-Commander store already followed; these two were the
ones that missed it. Files written by earlier releases carry no Commander, so **the first Commander
seen claims them whole** — the same adoption the checklist gives notes taken before anyone was
identified — and a hand-written line without a `commanderFid` is adopted the same way rather than
refused. The Commander's name is written beside the id, for a person reading a file two Commanders
now share; like the hull and the ship name, it is never read back.

**The live half of the defect is not in this release, deliberately.** `ShipCoreService` and
`ShipDriftWatch` each hold the ship they have already acted on as a bare int, so two Commanders
both sitting in ship 7 still read as *no change* across a login switch even with the stores keyed.
That needs the Commander-switch signal Phase 44 describes, and it ships there — the phase's own
items record it.

## 0.48.0 — 2026-08-21 — The checklist in the order you care about, and one HGE table

**Phase 42 — the checklist in the order you care about.** Its own section of this release, and the
work stands on its own.

**And High Grade Emissions now have one trigger table, which is yours.** Two readings derived from
the community sources both shipped wrong, so the table D47 uses is now the one you gave it:

| The system | Emissions hold |
|---|---|
| Federal | Core Dynamics Composites, Proprietary Composites |
| Imperial | Imperial Shielding |
| Independent, controlling faction in Civil Unrest | Improvised Components |
| Independent, controlling faction in War or Civil War | Military Grade Alloys, Military Supercapacitors |
| Independent, controlling faction in Boom or Expansion | Proto Heat Radiators, Proto Light Alloys, Proto Radiolic Alloys |
| Independent, controlling faction in Outbreak | Pharmaceutical Isolators |

Every row needs a population over a million. **Alliance systems yield nothing.** The state is the
**controlling faction's**, not any faction's — and a system can still offer two unrelated groups
when that faction is in two states at once, which is how Civil Unrest *and* Expansion gives you
Improvised Components and all three Proto materials.

**"Where can I find Imperial Shielding" was wrong for eight materials out of ten, and the reason is
worth telling.** That search kept its own copy of the trigger table, derived by looking for state
names inside each material's description. Almost every description ends *"; Mission reward"* — and
**War** is spelled inside **reward**. So Imperial Shielding was searched for as *Empire and at war*,
which is how it answered with a procedural system 41 light years out instead of a populous Imperial
one. It also never applied the population floor at all.

That second copy is gone. Both the callout and the search now read the one table, the search filters
its results by population, and **it states the filter it used in the answer** — so a wrong one is
visible to you rather than only to a test. This one was caught because you knew the parameters.

---

## 0.47.0 — 2026-08-21 — Limpets before you leave, and Oppi is not Federal

**D47 can remind you to buy limpets.** Dock somewhere that sells them, with a hold worth filling and
few enough aboard, and it says so:

```
No limpets aboard, and this station sells them. You have 256 tonnes to fill.
```

Three settings rows, and it is **off by default** — this is for Commanders who fly limpets, and one
who never does should not have to switch it off. The other two are the thresholds: the smallest hold
worth mentioning (**64 tonnes**) and how low counts as low, **as a percentage of that hold** (**5%**,
so twelve limpets in a 256 tonne hold is low and thirteen is not). The denominator is written on the
row, because a percentage whose denominator is not stated is a number nobody can set confidently.

Two things were measured rather than assumed, and both changed the design. **Limpets are not a
commodity** — they are bought through Advanced Maintenance, which is why D47 looks for the station's
re-arm service instead of reading its market. Of the 136 limpet purchases in the journal history,
that service was present at 133; the three misses are all one fleet carrier, which is a known and
accepted gap rather than a fault. And **the hold cannot come from the journal**: not one docking in
3,781 is followed by a cargo event listing what is aboard, so it comes from `Cargo.json`, which D47
has been reading since colonisation tracking.

No price is quoted. Limpets are nearly always 101 credits and occasionally not, and D47 has no
reading of this station's price until after you have bought some.

**Oppi is not a Federal system, and D47 stops saying it might be.** Reported against 0.46.0:
*"Oppi could be running high grade emissions for Core Dynamics Composites. No, it couldn't."* Quite
right. Oppi is Independent, holds one Federal faction out of seven, and none of them controls
anything.

The mistake was in how the rule was read. Contents depend on **individual faction states** and on
**the allegiance of the controlling faction** — states are each faction's, allegiance is the
system's — and D47 was reading both per faction. A minority superpower faction sits in roughly a
fifth of populated systems, so this was wrong about a fifth of the galaxy. Naming several materials
at once still works and never depended on that reading: it comes from two factions in *different
states*, one in Boom beside one in Outbreak.

**And five ambient remarks stop claiming things D47 never checked.** Reported: *"Mass lock is clear.
The route is ours as far as I can see"*, heard an hour into a crossing. Mass lock clearing is a
transition — true for a few seconds, nonsense afterwards — and "the route is ours" assumed a plotted
route besides. Re-reading all seventy stock lines against that rule turned up four more making the
same mistake, about atmosphere, gravity, and whether your hold is empty. All five are rewritten, and
the rule they broke is now written where the next line gets added: an ambient remark has to be true
of its situation at every moment inside it, because nothing checks.

**And the test suite's five-month cleanup flake is fixed** *(shipped in this release; its changelog
line was missed and added 2026-08-21)*. Ten times across five months, one arbitrary test per failing
run died in *cleanup* with a cross-thread error. The mechanism: Avalonia's global dispatcher rebinds
to whichever thread reads it first after the per-test reset, and one test's abandoned background log
read raised `PropertyChanged` off-thread a few milliseconds after its own test had ended, hijacking
the UI thread's identity out from under whichever test was being stood up next. The read and the
property set are split now — the worker computes, the page is told on the drawing thread — and the
same pattern was closed in `LogbookWindow`, its one production copy. Reintroducing the fault fails
the new regression test by name; 40 consecutive Release runs with the fix recorded zero failures.
The enabling behaviour is reported upstream as
[AvaloniaUI/Avalonia#22021](https://github.com/AvaloniaUI/Avalonia/issues/22021), and the full
history is in [docs/plans/flake-hunt.md](docs/plans/flake-hunt.md).

---

## 0.46.1 — 2026-08-21 — "Copy that"

**Ask where to find a material, or the nearest trader, and D47 now offers to put the system on your
clipboard.**

```
Nearest systems reported in Outbreak, Empire-aligned:
  Cubeo — 12.4 ly, population 20,000,000
  ...

Say "copy that" and I will put Cubeo on your clipboard.
```

Say **"copy that"**, **"copy it"** or **"put it on my clipboard"**, and it is there to paste into
the galaxy map. Both halves of this have been in D47 for phases — a search that finds the system,
and a tool that copies a name — and nothing joined them, so you read a name off a list and typed it
in by hand.

**The phrase needs no model.** It carries the system name with it, through the same model-free
router that runs your macros, so it works with the AI switched off and does not depend on the model
choosing to call anything.

For a raw material it offers the **system** rather than the body — the galaxy map does not take a
body name, and pasting a destination into it is the point. An offer stands until the next answer
replaces it, so saying it twice copies it twice; a search that finds nothing clears the offer rather
than leaving the previous system answering to "that".

---

## 0.46.0 — 2026-08-21 — Emissions worth dropping for

**D47 tells you when a system might be running High Grade Emissions, and what would be in them.**

```
Shinrarta Dezhra could be running high grade emissions for Core Dynamics Composites,
Proprietary Composites and Pharmaceutical Isolators.
```

Said on arrival, because that is the only moment you can act on it. Once per system, and never for
the backlog D47 reads at startup — the only jump in that backlog you could still do something about
is the last one.

**A signal belongs to a faction, not to a system**, which is the part that makes one system able to
offer two unrelated things at once. Each faction is looked at on its own: Federal gives composites,
Imperial gives shielding, and Civil Unrest, War, Boom and Outbreak each give their own — so a system
with a Federal faction beside an Independent one in Outbreak names both. That is not a rare case:
across 400 recent jumps in the journal corpus, **84** were into a system shaped exactly like that.

A Federal or Imperial faction yields its composites or shielding **and nothing else**, whatever
state it is in, so the state-driven materials live in Independent and Alliance space. Systems under
a million population are not mentioned at all.

**It says nothing about a material you are already full of**, and a system whose materials are all
full says nothing. So when you have finished gathering, it goes quiet on its own — which is why it
ships on. Where D47 does not know a material's cap it tells you anyway rather than assuming you are
full. Its own row in settings, switchable by voice like every other callout.

**The table is sourced rather than written, and its disagreements are written down.** Four sources:
the Elite Dangerous Wiki, which alone states the underlying mechanic; the 2017 Frontier Forums USS
guide, which is the research the rest descends from; edgalaxy.net's live EDDN detections, whose six
groups match; and — already in this repo — the generated materials table, which has carried these
conditions in its own sourcing column all along. The rules are asserted against that table both
ways, so a regenerated table that disagrees fails a test instead of quietly drifting.

The two prose sources contradict each other in four places, and every one of them is recorded in
`docs/plans/change-requests.md` with the ruling that settled it. The one that mattered most: they
flatly disagree about whether an Imperial system in Outbreak gives you shielding *and* isolators.
It does not — superpower wins — and three tests hold that reading in place.

---

## 0.45.0 — 2026-08-21 — Ordering the list, and a carrier that knows where it is

**Your checklist reorders by voice, and it has both ends now.** *"Move it up"*, *"move it down"*,
*"move it to the top"*, *"put it at the bottom"* — and **it** means the line the tab is
highlighting, which is also the line you have just added. So a line and then *"put it at the top"*
works in one breath, with nothing named in between. Beside the two arrows on a selected line there
are now four: the ends outside, the steps inside, each end drawn as its step with a bar on it.

This is the same boundary the checklist has always had. Writing to your list stays off the surface
the AI can reach — the order is your answer to what you are working on next, and an in-game message
does not get to rearrange it. What was missing was the route that boundary always allowed, and it
is the one accepting a proposal already used: phrases the model-free router matches directly.
Nothing you say about your checklist goes anywhere near the model to reorder it.

**"Where is my fleet carrier" now answers about the carrier.** It was answering with where *you*
were — your system, your body, your station, your route, under your own name — and there were two
faults stacked to make that happen.

The question was reaching the wrong tool. A keyword names a *capability*, and the router then takes
that capability's first argument-free tool, which for Journal is the one that reports your position;
*"my fleet carrier"* contains the keyword *my fleet*. **Every other question on that capability had
the same answer waiting for it** — *what materials am I carrying*, *what ships do I own* and
*session summary* would each have told you where you were standing. The whole questions are now
declared against the tools that answer them.

And underneath, d47 was holding the wrong carrier. Elite writes a location event for your own
carrier **and** for a squadron's, seconds apart, and d47 kept whichever came last. Across 920
journals, 173 carry both and in **152 of them the squadron's is last** — so the system you were
told was reliably somebody else's, wearing your carrier's name, because the name comes from a
different event. Filtered on the type Frontier stamps on it. Journals from before that field
existed are unaffected: all 223 of them name a single carrier, so there is nothing to tell apart.

**Your carrier's crew talk to each other when you drop in.** Come out of supercruise at your own
carrier and the tower tells the captain you are inbound before the captain says anything to you.
Written by your core where there is a model to ask, in the two crew voices, with authored lines
underneath when there is not. It fires on the drop that names the carrier — not on arriving in the
system, and never at somebody else's carrier, which the same journals show you drop at often enough
for it to matter.

**One thing asked for and not built.** A callout for systems that might be holding High Grade
Emissions is written up in `docs/plans/change-requests.md` and is waiting on one answer. Everything
except the table is in hand — the conditions are all in a single jump event, and "skip it if I am
already full" is exact rather than estimated. What is missing is which conditions yield which grade
5 material, which is community reverse-engineering that Frontier publishes nothing about; the corpus
holds 19 such signals and cannot settle it. A callout naming the wrong material is worse than no
callout, so it waits.

---

## 0.44.1 — 2026-08-21 — A checklist line says which ship and which module

**A finished roll now names the ship it happened on and the module it happened to.** Reported
against 0.44.0, of three lines under **Done**: two reading *ship 51* and one *ship 53*, over slots
called `Slot01_Size7` and `Radar`. Both halves of that are d47's own keys — the `ShipID` is what
makes a build follow a hull through a swap, and the journal's slot name is what makes two
conversations about one slot the same item — and neither is what a Commander calls the thing. The
line now reads **Grade 5 Reinforced Shields on 7A Shield Generator**, over **Flamebrand
(Anaconda)**.

**Ship was already an axis of the checklist**, so nothing about how a list is stored or keyed has
changed. What is new is that the name is worked out on the way to the screen rather than written
into the line: a ship gets renamed and a slot refitted long after the plan that named it, so the
slot is resolved against that ship's remembered loadout every time the line is drawn — a parked
ship's included. A slot d47 cannot see keeps the wording the plan gave it rather than a guess, and
an empty one is called what the hull's layout calls it, *Compartment 3 (size 6)*, rather than
`Slot03_Size6`.

**A ship with no name is still not a number.** Where nothing has ever named it, the hull comes
first and the id stays beside it — *Anaconda (ship 51)* — so a Commander with two Anacondas still
has two ships. A list about no ship at all, which is every custom, system, suit and weapon line,
says nothing about one.

**Said as well as drawn.** The spoken *"… is done"* is the one checklist sentence with no heading,
caption or page around it, so it now carries the whole thing. The search box matches what is drawn
as well as what is stored, so the ship on the caption and the module on the line are both
findable — and the verdict under a line spells the module the way the line does, instead of
saying *Shield Generator* a caption away from *7A Shield Generator*.

---

## 0.44.0 — 2026-08-21 — The checklist in the headset

**What you are working on is readable in a headset again.** The Checklist tab is back on the big
VR panel, where a `Window` cannot go and where there is no second monitor to alt-tab to. It was
withdrawn from there on the Commander's own instruction during the panel redesign, and it comes
back on the same authority — **Loadout stays withdrawn**, which is a decision rather than an
oversight: a three-level drill ending in a search field is a bigger surface than a list of short
rows. Parity between the window and the headset is still a nice-to-have rather than a plan.

Furnishing it took the one line the code had been keeping ready. Everything else in this release
is what a headset does differently from a monitor, and two of those things were defects that
nothing but pressing the tab through a ray could have found.

**Ticking a line in VR drew a tick and did nothing.** A press through the panel toggled the box
and never raised the click behind it, so the line stayed open and the next redraw rubbed the tick
out. It is the event a checklist line hangs its work off, and no other checkbox on any VR page
carries behaviour there — which is how it survived from Phase 25 to now unnoticed. Every toggle
pressed in the headset now raises what a real release raises.

**And a long list could not be scrolled to its end.** The offscreen host hands a scroll viewer a
viewport a hundred and twenty-eight pixels taller than the one it really gets, an offset is
clamped down to fit a viewport and never let back out, so every frame quietly dragged the
document back up: the last three lines of a long checklist were unreachable by pointing at the
end of the bar. The same fault had been landing the transcript's *newest* just short of the
newest on every surface d47 draws offscreen. What you scrolled to is now held across the layout
pass, and the bar is told where the document actually went.

**Sized for a lens rather than for a desk.** The panel is 1024 pixels across a 1.1 m quad at
1.1 m — 53° of view, 19 pixels to the degree — which puts the muted second line of a row at about
29 arcminutes tall, over the floor for reading and not by enough to also be the lowest-contrast
text on the tab. It is the line a derived item's refusal is written on, so it goes up one step
and keeps its colour, on both surfaces: one view definition renders to both, and a tab that grew
its own behaviour in the headset would be the second UI codebase that rule exists to prevent.
*Edit*, *Delete* and the two reordering arrows were about twenty pixels tall — below the floor
every other button on the page already stood on — and now carry it by name.

Ticking, refusing, filtering and scrolling are all driven through the headset's own surface in
the tests rather than through a window, and both defects above were reintroduced once to watch
the new tests fail. The one thing no test can sign off is how it reads through the lens.

---
## 0.43.0 — 2026-08-20 — A build you can watch

**Two gauges at the head of a ship's slot list: power and jump range, live while the build is
edited.** They are the two numbers a build is designed against, and until now neither was visible
until after the credits were spent — a Commander answered both by alt-tabbing to Coriolis. Both
are panel-side and neither spends a byte of the tool surface; a gauge is not a tool.

**Jump range is exact, and it settles what Frontier's own figure means.** Replayed across 2,876
`Loadout` events in the corpus, the arithmetic reproduces `MaxJumpRange` with a **median error of
0.000%** and 99.3% inside half a percent — and it does so at unladen mass plus *one jump's fuel*,
not at a full tank. So the bar reads as a range: worst laden, full tank, and best, with the best
needle equal to the number the outfitting screen shows. The 19 events that miss all have a
Guardian booster and an SCO drive, and in every one it is Frontier's figure that omits the
booster's bonus; that shape is asserted, so a new kind of miss fails the check rather than hiding.

**Power is split retracted and deployed.** A build that fits until the guns come out does not fit,
and the distinction is the one hardest to keep in your head by hand — a shield booster sits in a
`TinyHardpoint` slot and draws all the time. The bar reads a percentage and, when a build is over,
the megawatts over: the first says there is a problem and the second says how big a plant fixes
it. Where `ModulesInfo.json` is present for the ship being flown, Elite's own per-module figures
win over the table.

**A planned roll is modelled rather than guessed, and never dressed as a measurement.** The
per-grade percentages have shipped in `Blueprints.tsv` since remediation 15; this applies them.
Checked against every distinct engineered module in the corpus — 374 comparisons, 97.6% exact —
and it settles what the table does not state: a blueprint and an experimental **compound**. A
modelled reading is drawn in a different colour with a `~` in front of it, which is the condition
on which these gauges may show planned figures at all.

**The SCO drive can be engineered on the panel again.** EDSY files the Supercharged drive as its
own module type and EDEngineer has one "Frame Shift Drive", so all eight of its blueprints were
offered with no recipe behind any of them — on the drive nearly every Commander flies. That was a
join rather than missing data, and the corpus settles it: SCO drives rolling Increased FSD Range
report figures the ordinary drive's grade rows reproduce exactly. Anti-Guardian Zone Resistance is
a different problem and still says so honestly: nothing d47 reads names its materials.

**A coin on the nineteen modules a Powerplay pledge is needed to buy.** Read from
`outfitting.csv`'s own `entitlement` column rather than from a list anybody maintains. It says a
pledge is needed and does not say *whose* — the gate carries a numeric id and no source d47 reads
maps that id to a Power's name, so an unpledged Commander is told plainly that they cannot buy it
and a pledged one is told d47 cannot tell them which.

**And d47 asks, once, when a ship's build and your checklist have drifted apart.** Spoken as you
board, with the question left on the Ships tab as a banner if you were busy flying. Yes revises
the list in place, so the ordering you spent an evening on survives; no leaves both alone and is
remembered on the build, so a restart does not ask again. It asks through the same
accept-or-decline boundary the model writes across and can rewrite nothing itself.

Five columns the table generator had been dropping now ship — the purchase gate, a power plant's
output, a Guardian booster's bonus, and what a cargo rack and a fuel tank hold. The last two also
tell a 5E Cargo Rack from a 6E, which was price alone until now. What the corpus said about all of
it, including three places the plan of record was wrong, is in
[docs/spikes/build-gauges.md](docs/spikes/build-gauges.md).

---

## 0.42.0 — 2026-08-20 — Reality and the goal, side by side

**A slot row now holds both the module on the hull and the module planned for it.** The
Commander's ruling, and it settles a row that had been wrong in both directions inside two days:
naming the fitted module beside the plan's roll described something that exists nowhere, and
naming only the plan's module dropped what is actually fitted. *One is reality, the other is the
goal.* So the row says `5D Module Reinforcement → 5E Hull Reinforcement Package`, what is there
muted and what is planned in bold, and grows a second name only where there is genuinely a second
thing to say.

**Twenty-eight modules Frontier names and d47 was dropping.** The specification table is built by
walking coriolis-data and joining names to it, so anything coriolis has no figures for never became
a row however plainly Frontier's own id list named it — the **Mk II passenger cabins**, the size 8
drives, the three discovery scanners and the starter-ship variants every Sidewinder flies with.
The bulkheads beside them have followed the opposite rule for months: figures are what a missing
source costs, not existence. Now they all do.

**And a module nobody names at all is said by its family.** The Mk II Fighter Hangar — the one
that holds a Nomad — is in no naming source; only EDSY's slot database knows the symbol exists. It
reached the Commander as `int fighterbaymk2 size5 class1 free`. EDSY does know its *type* is the
one the three original Fighter Hangars carry, so it now reads **"Fighter Hangar (newer than my
table)"**: derived from d47's own table, and honest about which half it knows. Where a family has
more than one name — the passenger cabins hold Economy, Business, First and Luxury — nothing is
guessed and the old spelling stands.

**A drag carries the module the row was showing.** Dragging a hull reinforcement onto two empty
compartments left them reading *"empty · Heavy Duty Hull Reinforcement (G5), Deep Plating"* — a
roll with nothing to roll on. The source plan names no module on purpose, because *keep what is
fitted, I only want the engineering* stores the roll alone; the drag now brings the fitted module
along, resized to what the target takes.

**And the module already in the slot can be kept.** *"No option to keep the Life Support I already
have"*, and the same for Sensors. A socket offering one module name skips the page that carried
the **keep what is fitted** row, so it was unreachable exactly where it was most wanted — nobody
replaces their life support, they engineer it. The variant page now leads with it.

**Two quality gates were amended rather than loosened.** Ten of the twenty-eight new modules
collide with a name already in the table, and the generator separates them with a token from
Frontier's own symbol: `(free)` for the starter variants, `(size5)` and `(size6)` for the Mk II
cabins that the id list files under one name *and* one class. The gates exist to catch a name that
went missing upstream; these are names that arrived. Both now list what is expected and why, so a
new qualifier still fails them.

**A theory about the headless-session failure was tested and killed.** Running the App suite one
test at a time should have closed the window it needs; seventeen runs later it had failed twice
anyway, at the same rate as before. Reverted, and written up in `bugs.md` — a mechanism that does
not do what it was added for is worth less than the negative result.

---

## 0.41.3 — 2026-08-20 — The slot row stops describing things that do not exist

**A row now names the module you planned, not the one you are replacing.** Reported twice in an
hour and it was one sentence both times: the row carried the *plan's* roll beside the *fitted*
module's name, so it described something that exists nowhere. Once as a Module Reinforcement
Package apparently carrying a Hull Reinforcement roll, and once as *"I just changed this to 6A,
but it still says 6D"*. Where a plan names no module the fitted one is still named, and the fitted
module is never lost — the slot page has said it under its own **Fitted** heading all along, which
is where the two are meant to be told apart.

**Grade 0 no longer reaches your checklist.** Choosing a module d47 holds no engineering recipe
for — a Guardian Gauss Cannon, say — stores the module and no roll, and that arrived on the
checklist as *"Grade 0 engineering on LargeHardpoint1"*, which the list then refused, in red, four
times over. A plan asking for no engineering is now simply not promoted. A **fit this module**
checklist item is a real gap and is written down as one rather than papered over with an invented
grade.

**And d47 stops claiming Frontier engineers nothing.** *"I have no engineering for this module"*
is a statement about d47; the page was using it to make a statement about Elite. A Guardian Gauss
Cannon **does** take Anti-Guardian Zone Resistance, and Rapid Fire besides — d47 simply holds no
recipe for either, because its recipe source carries no Guardian weapons at all. The sentence now
says which of the two is true, and names what it knows is offered.

**Experimental effects say what they do in the chooser too.** Nine bare names — Double Braced, Fast
Charge, Hi-cap, Lo-draw, Thermo Block — meant choosing between them required already knowing. Each
now carries the same derived line the blueprint page has had for months.

**Two smaller ones.** A searchable chooser focuses its search box again — it had been quietly
regressed, focusing during the attach event, before the rows under it existed. And a compartment
says its size once rather than twice: *"Compartment 4 (size 4) (size 4)"*.

Phases **38 — A build you can watch** and **39 — The checklist in the headset** are now in
`list.md`, unticked, with their plans of record in `docs/plans/`.

---

## 0.41.2 — 2026-08-20 — Three things the slot chooser was getting wrong

**An experimental effect now says what it does.** The Effect line only ever described the
blueprint, so choosing between Auto Loader and Corrosive Shell showed you a sentence about the
*roll underneath them* — identical for both, and no help at all. `Blueprints.tsv` has carried the
figures for all 154 experimentals as long as it has carried them for the 786 blueprints; nothing
asked. Where both are planned the experimental is named, because two sentences under one heading
are two claims about the same slot.

**A planned module can be kept while only the roll changes.** *"Change the plan"* offered no
**Keep the module — I only want the engineering** row unless something was *fitted* in the slot —
which is never true of a ship you are designing, or of any ship you are not sitting in. Reported
after drag-copying a multi-cannon and wanting only a different experimental; the way through was
to find the same module again in a list of forty and answer the variant question a second time.
Keeping it now carries the module through rather than clearing it, and skips straight to the roll.

**And a module your ship may only carry so many of is no longer offered twice.** A second Fuel
Scoop, a second Supercruise Assist, a second Guardian FSD Booster. The rule is Frontier's own, and
two parts of it are easy to get wrong by hand: it applies to a **group** rather than a module — a
Standard and an Advanced Docking Computer rule each other out, and Bi-Weave, Prismatic and stock
are one shield generator between them — and it is a **count**, not a flag. Sixteen groups allow
one; the AX and Guardian weapons allow **four**. The slot you are editing never counts against
itself, so replacing the fuel scoop that is already there still offers fuel scoops.

Measuring alone would have got the last one wrong: across 2,863 loadouts every limited group tops
out at one, but not one of those ships carried an AX or Guardian weapon.

---

## 0.41.1 — 2026-08-20 — Six things the Loadout tab was getting wrong

**Engineering a module now registers the moment you roll it.** Elite writes no `Loadout` event
when a module is engineered — measured across 918 journals: of 6,485 `EngineerCraft` events, *not
one* is followed by a `Loadout` within five seconds, and 1,378 are never followed by one at all.
d47 read the ship only from `Loadout`, so a checklist item was diffed against the loadout from
when you last boarded and stayed open straight through the roll that finished it; swap ships and
the stale verdict stood until you boarded that hull again. `EngineerCraft` is now folded into the
ship, which is what puts *"that is done"* in your ear at the engineer's console rather than at the
next boarding. A grade roll writes no experimental field at all, so one already applied is kept
rather than stripped.

**And d47 no longer forgets a ship the moment you get out of it.** The constraint is real — Elite
reports the loadout of the ship you are sitting in and no other — but it is about what the game
*sends*, not about what has been *seen*. Every ship you have sat in is remembered, dated off the
journal, and recovered from the last 25 journals at startup the way the fleet already was. Parked
ships show their modules again instead of a page of "not seen"; a remembered page says **"As you
left it, three hours ago"**, because you may have re-outfitted somewhere d47 was not watching. A
ship you have never sat in still says so, and a sold one is forgotten.

**A plan is no longer stale against the ship it was written for.** `StoredShips` carries
`ShipType_Localised`, so a build started from the fleet held "Panther Clipper Mk II" where one
started from a `Loadout` held `panthermkii`. Compared as text those are two different ships, and
every slot of that plan reported *"That ship id now reports a panthermkii, and this plan was
written for a Panther Clipper Mk II"*. Hulls are compared as hulls now, and files already on disk
are normalised as they are read, so nothing needs migrating. A genuine hull swap is still caught.

**Four hulls got their slot layouts back.** The Mandalay, Cobra Mk V, Type-8 and Panther Clipper
have every slot row in the shipped table and no *hull* row — Frontier shipped them ahead of the id
sources. The layout lookup resolved the hull through the ships section first, so a missing row hid
a layout sitting in the same file. On the Loadout tab that surfaced as the no-layout fallback: raw
journal slot names (`TinyHardpoint1` rather than "Utility Mount 1"), a typed-out blueprint instead
of the chooser, and no empty slots at all.

**Thrusters that cannot lift the hull are no longer offered.** Each carries the heaviest ship it
will move in its own figures — 120 tonnes for the size 2 Enhanced Performance Thrusters, 200 for
the size 3 — and an Anaconda is 400 while a Type-10 is 1,200. The size rule alone let them through,
because a small module does fit a large socket. It is the same arithmetic for any undersized
thruster; the Enhanced ones are only where it bites first.

**And two smaller things on a slot row.** A planned module is named with its class and rating —
"4E Hull Reinforcement Package", not "Hull Reinforcement Package" — which is what a fitted one
always got. And a roll can no longer be dragged onto a module that cannot take it: dropping a Heavy
Duty Hull Reinforcement on a compartment holding a Module Reinforcement Package produced a row
claiming a roll Frontier offers that module nowhere. The drag is refused and says which module and
why.

**A guardrail that made d47 lie about itself.** Some actions are deliberately out of the model's
reach — raising Elite's window is one — so asked to do it, the model correctly reported having no
tool and then went one step further and said the software could not do it either. It can; that is
what "focus the game" is for. The rule now separates the two.

---

## 0.41.0 — 2026-08-20 — Routing you can see

**A Routing tab, in the window.** Everything in it was already there and could only be spoken —
and a route is the thing a voice is worst at. Three modes of one tab, in the idiom the transcript
already uses: **Plan**, **Progress**, **Course**.

**Progress is the one worth opening the tab for.** A Sol-to-Colonia plot is 131 waypoints, and
reading that aloud is not an answer, so what d47 *says* is the totals and the next handful. That
cap belongs to speech. The page draws the whole route: every hop, its class, which ones are
neutron stars or white dwarfs, which cannot refuel you, and where you are in it. It reads the file
Elite writes locally, so it costs no network and works with everything switched off.

Two things it will not do, both deliberate. A leg whose length is unknown blanks the distance
total rather than quietly shortening it. And a star class d47 does not recognise reads **scoop
unknown**, never "no" — being told a star is unscoopable is how a Commander routes around one that
would have refuelled them.

**Plan is the three planners as forms** — jump route, Road to Riches, trade run — with the
results drawn whole rather than cut to what would fit in a sentence. It shares the galaxy search
setting, and switched off it says so and offers the row rather than failing. Your balance is still
typed every time and still never saved: cargo capacity comes from the journal because it belongs
to the hull, and what you are worth does not.

**Course is the clipboard, in the order that matters** — the name goes on the clipboard first,
whatever happens next, then the map is driven if you asked, then d47 checks whether it took. Every
system name anywhere on the tab copies when you press it.

**A route plotted by voice appears in the tab, and one plotted in the tab is the one d47 talks
about.** Both write and read one plan in `data/route-plans.json`, so the two cannot drift into
holding different routes.

The headset does not get this tab. Its plan forms want a keyboard, and Progress may yet go there
on its own.

**And the published route-planning page had a test failure pasted into it**, inside the
`plot_trade_route` schema block, since 0.39.0. Removed, and the documentation gate now requires a
schema block to *be* the schema rather than merely contain one — which is what let it through.

---

## 0.40.0 — 2026-08-20 — The loadout, the drag, and the engineer you are parked at

**Ctrl-drag never worked, on any slot, since it shipped.** The pointer is captured by the row the
press lands on, so the release always came home; the handler saw the drop as landing where the drag
started and did nothing at all. Neither drag test could see it — both raised the release on the
*target* row, which the running app can never do. The drop target now comes from where the pointer
actually is, and the gesture finally says what it is while it happens: a ghost carrying the plan
follows the mouse, the cursor becomes a copy cursor, and both are put back in a `finally` so a
failed drop cannot leave the pointer lying.

**The slot rows are rebuilt around the loadout rather than the slot.** Size, the plan dot, the
module, the gear where it has been rolled, the blueprint, the grade, the experimental, then what
the roll actually did — left-justified on one line, with the effects re-fitted on every resize and
cut at a whole effect rather than mid-word. The slot's name is announced to a screen reader rather
than drawn, because it was leading the row and it is not the primary thing about a loadout.

**Four modules could not be engineered at all, and the table always knew.** 66 of 940
ship-engineering rows were unreachable by the module join, so the Auto Field-Maintenance Unit, Fuel
Scoop, Refinery and Plasma Accelerator each read as taking no engineering. Three of them were a
spelling coriolis and EDSY disagree on; the Plasma Accelerator lost all fifty-one of its rows to two
flaws in the generator — a first-come-first-served type assignment, now a maximum matching, and a
containment test that let one mis-keyed row veto a whole module. Now 0 of 940.

**What the engineer you are parked at can actually do.** Asked "I'm in Laksak, what can I retire
here?", d47 answered with the whole list, because no filter knew where the Commander was — while
the opening line offered an unlock hint about somebody two hundred light years off. Every input had
been on disk for phases and nothing joined them. `get_checklist` gains `here`, the continuity
callout leads with the engineer under your feet, and "one stop away" now reads "one unlock step
away", because it never meant distance.

Also in this release: keeping a fitted module reaches its experimental effect again — the roll list
matched by symbol and the effect list by name, so *every* kept module skipped that step; an opaque
ElevenLabs id is never shown where a voice's name belongs; a ship's system is named in the fleet
list and copies to the clipboard for the Galaxy Map; no chooser claims a module's size and mount do
not matter, because that is false of every module in the game; the gear glyph takes the theme's
accent; the Checklist tab carries its open count; and the scope filter's four axes are grouped under
the questions they answer.

## 0.39.2 — 2026-08-20 — A voice that cannot speak is not offered

Remediation 17, item 13. Reported from the log: d47 went quiet, and behind the silence was
*"Famous voices can only be used within the Reader App"* — a voice ElevenLabs will list for your
account and then refuse to speak with. A sentence that will not synthesise is dropped, so from
the Commander's side there is nothing to see and nothing to hear.

**The obvious filter cannot work.** Those voices come back in the category `professional`, which is
the same category as the several hundred ordinary ones beside them — *Burt Reynolds™*, *John
Wayne™* and *Stan Lee™* are indistinguishable, by every field the service returns, from *"Brian -
Clean, Professional and Balanced"*. Filtering on category would have hidden hundreds of voices you
can actually use and still let the famous ones through.

The trademark in the name is the only thing that separates them, so that is what the voice list
now drops. And because that is a rule about somebody else's text, it is not the only defence: a
refusal that mentions the Reader App is now treated as a fact about the voice rather than about
the sentence, which makes d47 forget it, fall back to a voice that works, and say so. If the
convention changes, it costs one sentence instead of an evening.

## 0.39.1 — 2026-08-19 — It knows what it said, and it says less of it

Remediation 17, eleven of fourteen items. Ten were reported against 0.39.0 by the Commander; the
rest came from reading what the code actually did next to what it claimed.

### It can be asked about what it just said

*"Elvira Martuuk is one stop away."* — *"Why would I care about that?"* — *"Care about what,
Commander? I have no record of what I said before this."* That answer was exactly true.

Conversation history was written in **one** place, the end of an answered model turn. Every callout,
the line that picks up where you left off, every habit remark, every timer going off, every action
d47 took on its own, and every answer the model-free router gave — all of them went to the speaker
and to the on-screen transcript and nowhere else. The page you read and the conversation the model
sees were two different things, and only one of them was the conversation.

Now what d47 says unasked rides into the next thing you ask it, labelled as its own words, and stays
in the transcript afterwards. Only ever **its** words: a re-voiced message from another Commander is
somebody else's text and still has no path into a prompt.

The transcript also has a ceiling for the first time. It never had one.

### Two things it used to say that were not worth saying

**"Shields are down"** on a hull with no shield generator. 527 of 2,853 loadouts in the corpus fit
none — mining, hauling and exploration builds routinely fly unshielded — and boarding one crossed
the edge into "shields off" with nothing dangerous having happened. It now checks what is fitted.

**Only in the ship**, which the corpus had to settle: a Hauler with no generator and an SRV bay
reports shields going down *and coming back*, because those are the SRV's, and they are real. So the
ship's loadout answers for the ship and nothing else, and every unknown still says the warning.

**"You have dropped short of where you were going — 14 of 490 approaches"**, said on a perfect
approach. A habit was gated on three counts and never on the rate, though the code's own
documentation says the denominator is *"the difference between a habit and a Tuesday"*. Something
you do once in thirty-five approaches now goes unsaid — it is still on the Habits page, and it is
never volunteered. Measured across the corpus, nothing falls between 2.8% and 50%.

### The Loadout tab

**Clicking a ship does something again.** Visiting another tab and coming back left the page unable
to hear the navigator, so the fleet list stayed on screen looking perfectly correct while every
click, breadcrumb and mode switch did nothing.

**A ship page follows the journal.** It only ever redrew when the plans file was saved, so one left
open across a ship swap kept its first answer all session — the reported *"still says not seen"*. A
plan now also binds to a ship when you board it, not only when you buy it.

**"Keep the 5D Hull Reinforcement Package"** offered every blueprint in the game, Ammo Capacity
included. Keeping answers the module question with nothing on purpose, and the blueprint question
lost its subject along with it.

**A refused drag says why.** It used to return in silence, so a rule turning the copy down was
indistinguishable from a broken feature — and the successful message was being overwritten by the
redraw behind it, so neither outcome had ever said a word.

### Things you can read

The Settings nav scrolls, and the highlighted entry is brought into view — which a scrollbar alone
would not have done, since the nav follows the cards beside it. The gear glyph sits after the module
name instead of in a gutter to the left of it. The engineering grade reads last on its line, right
next to the stepper that moves it, while the spoken sentence is unchanged.

And in the headset, the Utilities tab has stopped flickering: it was tearing down and rebuilding
every timer row ten times a second, which the window never showed because it composes its frame
after the tick, and the headset does not.

## 0.39.0 — 2026-08-19 — Trade routes that d47 works out

Phase 36, and with it **every phase in `list.md` is now built** — 35 of them, 1 to 21 and 23 to 36.

`plot_trade_route` used to hand the question to Spansh's planner and read back the answer. It now
asks for markets and does the arithmetic here, which is what lets it do three things that planner
could not.

### The hold does not have to be emptied

The one no other tool does. A leg that sells everything is what every planner assumes, and it is not
always the best move: holding a commodity past a station that pays poorly for it, to a later one
that pays well, can beat taking the money now.

That makes the state carried between hops *credits and cargo* rather than credits — a different
algorithm, not a flag on the old one — so a plan now reads as a sequence of **stops** rather than of
legs: sell these, keep those, buy that, go. A `keep` line always says what declining to sell here is
worth, because a Commander who is not told why they are flying past a buyer will sell there, and
then the plan they were given stops being the plan.

### Round trips, and ten hops in seconds

`loop` ends the route back where it started, so an evening's trading finishes at your own base
rather than four systems away. Spansh's planner silently dropped that parameter; this one is built
around it, and a shorter loop that pays better than the ten you asked for is taken.

Ten hops is the new ceiling, five the default, against four hops before. The bounded search — a beam
of the best 200 partial routes carried hop to hop — does the measured shape of the problem in tens
of milliseconds. **The planner this replaced took forty-eight seconds to answer four hops.**

### Your own prices, where you have them

Where you have docked and opened a commodity board, Elite wrote `Market.json` and d47 now keeps it:
the last 25 markets you have stood in, in `data/markets.json`, readable and hand-editable like
everything else in `data/`. Those are exact and free where a crowd report is neither. The rule is
**newer wins** rather than yours-always: a report from this morning beats what you saw a month ago.

**Less about you leaves this machine than before.** The old planner was sent your hold size and the
figure you gave it to trade with. This one sends the system you are in and how far to look; the
prices come back and everything else happens here. Your balance is still never read.

### What it will not promise

It does not model market saturation, and it says so in every plan. Dumping far more of a commodity
than a station wants drops what the rest of it fetches — by a factor that is **not known**, and a
constant guessed here would make every profit wrong in a way that reads exactly like the feature
working. So no leg ever sells more than a station asked for, and a stop reports when a lot was cut
short by that.

Fleet carriers are left out, and the plan says how many. They set their own prices and then move:
the best Gold price within 50 light years of Sol was 4,760,900 credits at a carrier, against 52,282
at the best station. A planner that ranks on price and does not know what a carrier is builds every
plan around one, and half of them have jumped by the time you arrive.

---

## 0.38.2 — 2026-08-19 — Two silences and four things you can read

Remediation 16, all six items. Four are the Commander's, reported against 0.38.1. **Two were found
by reading somebody else's code** — [EliteIntel](https://github.com/SudoKrondor/EliteIntel), a Java
companion for the same game — on a research pass with no change intended. Neither of those two had
been reported by anybody, because neither has a symptom: both are faults that produce *silence*.

### A ship you are not flying can be planned again

*"I have lost the ability to modify the loadout. Don't see how to modify Reaper."* The page opened,
named the hull, gave its speed, boost, armour and price, said where it was parked and what it was
worth — and then offered no slots at all.

`StoredShips` carries the **localised** hull name, which is deliberate and is what the fleet page
prints as *Reaper (Cobra MkV)*. That name is what a build started from a parked ship holds. One
lookup resolved it and the other did not: the hull's figures came out right and its slot layout came
back empty. Two lookups of one hull, disagreeing, and the disagreement read as a capability that had
gone away. Both now go through the same resolver, so they agree by construction rather than by
being remembered.

### The headset no longer holds a stale panel in silence

When SteamVR refuses a frame, d47 used to lose it. The surface's dirty flag was cleared by the draw
that produced the frame, and the submit's answer was discarded — so the headset held whatever it
last showed until something unrelated happened to redraw it, with nothing in the log. A refused
frame is now **held** rather than dropped, and re-sent rather than re-drawn: what the compositor
turned down was the upload, not the picture. It waits while the quad is not being drawn at all —
the dashboard being up, or the headset off — so a Commander in the SteamVR menu is not paying for
nine megabytes a tick to re-send a picture nobody can see.

A run of refusals is reported once, **and so is its recovery**, which is new. The log could
previously say frames were refused and never say whether that lasted a second or the session.

### A control you rebind in Elite is picked up without a restart

The bindings file was read once at startup. The reason recorded for that was that the Commander
cannot edit their controls while d47 is the foreground window — true, and beside the point: they
edit them in *Elite's* options menu, most often right after d47 has told them an action is not
bound. Every injection after that sent the old scancode.

It is re-read when it moves, on the tick, comparing write stamps before parsing anything. Both
files are watched: a rebind rewrites the `.binds` file, and switching preset rewrites `StartPreset`
and changes which `.binds` file is the answer.

### Three things you can read

**An engineer's grades are a list.** *"This should not be laid out on a single line."* Nine
specialities set as running prose wrapped into a paragraph, with the commas between entries looking
like the commas inside them. Now one per line — *Beam Laser (G5)*. What d47 says out loud is
unchanged: a list is scanned and a sentence is heard.

**Language model, Speech and Listening are at the top of Settings.** They were seventh, eighth and
ninth, behind Help, Persona, Memory, Habits, Commander's log, Goals and Location — and they are the
three that have to be set before d47 does anything at all.

**A rule between the persona rows and the ship-core pair**, with a heading on it, so the two
dropdowns read as one thought rather than as more of the column above them.

---

## 0.38.1 — 2026-08-19 — The Loadout tab, told properly

**Remediation 15, all fifteen items.** Nearly all of it is the Loadout tab, and most of it was found
by one Commander planning one Type-10 in one sitting. That concentration was the finding as much as
the items were: the batch has two threads running through it — joins that miss and fall back
silently, and internal identifiers reaching what a Commander reads — and this release closes both.

### A Sub-surface Displacement Missile was shipping as a Pulse Laser

Reported as *"I should be able to differentiate between the first two lasers by something besides
the price"*. The honest answer was that the expensive one is not a laser:
`hpt_mining_subsurfdispmisle_turret_small` is a turreted Sub-surface Displacement Missile, and the
table carried it as a **fixed 1B Pulse Laser at 38,750 credits**. Because the module chooser groups
what it offers by name, that is worse than a mislabelled row — a mining missile was filed inside the
pulse laser list, where nobody hunting for it would look and everybody picking a laser would find it.

Ten hardpoints were wrong in all. Both Sub-surface Displacement Missile pairs read each other's
mount, the AX Missile Rack, Heat Sink Launcher, Caustic Sink Launcher and Point Defence carried no
mount at all where their symbol states one, and the Seismic Charge Launcher was a Pulse Laser too.

### The cause was a join that was trusted rather than checked

Directive 47's specification table is derived by joining two community sources on Frontier's own
module ids. Five of those ids do not lead where they claim: three mining hardpoints are filed under
the id of a small fixed pulse laser, and the two missiles have their fixed and turreted ids swapped
with each other.

Both sources also carry Frontier's *symbol* — the string the journal writes — so the id that claims
to link two rows can simply be asked whether it landed on the row it says it did. It now is, and an
id that lands elsewhere is discarded rather than believed. When it is discarded, or when a source
gives no id at all, the module is looked up **by symbol instead**, which is exact rather than a guess.

That second route fixed more than the mis-keys. Thirty-three modules carrying no id were previously
named from the file they were found in; they are now named by the naming authority, which had them
all along:

- **Mk II Supercharge Optimised Frame Shift Drive (SCO)**, previously *Frame Shift Drive (mkii overchargebooster)*
- **Mk II Agile Boost Thrusters**, previously *Thrusters (mkiiagileboost)*
- **Detailed Surface Scanner**, previously *Surface Scanner*
- **Experimental Weapon Stabiliser**, previously *Experemental Weapon Stabilizer* — two typos, in the shipped table, beside a correctly spelled one
- **AX Missile Rack**, previously *Ax Missile Rack*

Both Mk II modules sit in core sockets, so every Commander flying one met them.

The **Corrosion Resistant Cargo Rack** in sizes 5 and 6 read *Cargo Rack (corrosionproof)* and so sat
inside the plain cargo rack's list — a rack whose entire point is that it resists corrosion, one
letter of a symbol away from being findable. Neither source names those two, so the name is taken
from their own siblings at sizes 1 and 4, and only where those agree. Nothing is invented.

### Modules that cost nothing now cost what they cost

The same sources declare 35 symbols **twice**, one of each pair a husk with no price and sometimes a
stale damage figure. Which of the two won was whichever the sort put last — a coin toss on every
figure the row carried, and it had already landed badly: the large **AX Missile Rack** shipped at a
price of zero, in the very chooser where the complaint was that price is the only thing telling two
modules apart. It costs 1,352,250 credits. The entry the source keyed to Frontier's id now wins, and
where both are keyed the more complete row does.

Frontier's own empty-slot placeholders, `hpt_missing_hardpoint` and `hpt_missing_utility`, are also
gone from the table. Both were named "Missing Hardpoint", so they drew their own two-row group in a
chooser that groups by name.

### Nothing about this can come back quietly

The point of the batch this belongs to is that **a failed join produced either everything or nothing,
and both read exactly like the feature working**. So the generator now reports every id it discarded,
every module it could not name, and every duplicated symbol, by name rather than as a count — and
five assertions run in CI against the shipped table itself, because the generator is not part of the
build and nothing would otherwise notice. Every mount must agree with its own symbol, no name may be
built from a raw symbol fragment, no placeholder may appear, and the reported missile must be a
missile.

### The Loadout tab knows what a module can be engineered with

**A Type-10's armour was offered every blueprint in the game** — Dirty Drive Tuning, Ammo Capacity,
Efficient Weapon — and a **fuel tank**, which cannot be engineered at all, was offered the same
forty. One fallback caused both: a module whose name failed to match and a module that genuinely
takes no engineering produced the identical empty result, so nothing could tell them apart.

There are three answers now, not two. Armour gets its nine. A fuel tank is told it takes none, and
the slot stays plannable — just not engineerable. And a module type Directive 47 has never heard of
says so rather than pretending it knows.

The join behind it goes through Frontier's own ids at every step, which took four sources and a
false start. The obvious route — matching module kinds to recipe kinds by what they have in common —
produced confident wrong answers: a Cargo Scanner came out as a Chaff Launcher, because the two
share the generic Lightweight and Reinforced rolls and nothing else separated them. **A confident
wrong answer is worse than none**, so that route was abandoned rather than tuned.

### Engineering reads as engineering

A grade 5 System Focused roll on your power distributor used to read back as **grade 5
PowerDistributor PrioritySystems** — Frontier's internal symbol with the underscore taken out.
Directive 47 said outright that nothing it shipped joined the two spellings, and that was true.

It is not any more. Every one of the 940 recipes now carries the name Elite writes into your
journal, so a roll you already have is named the way you know it. **System Focused.** Dirty Drive
Tuning. Lightweight.

With the naming sound, the two marks the Commander asked for are worth drawing: a **gear** beside a
module that has been engineered, and the engineering line in **its own colour**. They are
independent of the orange dot that means a plan exists — a module can be engineered with no plan, or
planned and never touched — so they are different glyphs in different colours rather than two
shades of one.

### The grade stops being a question

*"999 times out of 1000 it will be 5."* Measured: of 160 module-and-blueprint pairs, **155 reach
grade 5**, and the whole exception set is five — Chaff Launcher, Heat Sink Launcher and Point
Defence stop at 1 on Ammo Capacity, and Shield Cell Bank stops at 4.

So the page is gone. The grade is the **highest that blueprint offers**, never the number five, and
it becomes a **stepper on the slot page** where moving it re-costs the materials block underneath —
which answers *what would grade 4 cost me instead* where you are standing, rather than sending you
back through a chooser. A blueprint with only one grade does not print it: "grade 1 Ammo Capacity"
says the same as "Ammo Capacity" and takes longer.

**"Any grade" is gone entirely**, on the Commander's ruling that it is not a thing.

### Telling two modules apart

*"I should be able to tell the first two lasers apart by something besides the price. The more
expensive one should be more powerful, and I should know how."*

The honest answer to that particular pair was that **the expensive one is not a laser** — see below
— but the ask stands for modules that really are comparable. Rows now carry the few figures that
answer it, chosen per kind rather than one list for everything: a weapon leads with **damage per
second** and its damage type, a power distributor with its three capacitors and how fast each
refills.

Damage per second is computed with coriolis's own published formula rather than arithmetic invented
here, and checked against an independent reference — 3.97 for a 1G turreted Pulse Laser, matching
what other tools show. Damage type is stated as a proportion where it is one: a Rail Gun is 67%
thermal, 33% kinetic. On an anti-xeno weapon it is a marker beside a real type rather than a share
of it, because that is what it is.

And where Frontier describe a module themselves, that description is shown. *"What's special about
a Guardian Distributor?"* — **"Enhanced with Guardian technology to speed up capacitor recharge
rates, at the cost of smaller capacitors and increased heat generation."** Their words, on 707
modules.

Blueprint rows say what the roll does, gains before costs — *less mass, at the cost of integrity* —
derived from the effects table rather than written by hand, and taken from the top grade, which was
checked to contain everything the lower grades say.

### Put this build on my checklist now puts the build on the checklist

It made a **proposal** and said nothing about where forty items had gone, so a Commander who pressed
it and then looked at their checklist found one custom note and reasonably concluded nothing had
saved. Everything had saved; it was behind a button showing a "1", at the far edge of the bar.

Pressing a button labelled with the outcome **is** the act of accepting. Suggestions stay a page
rather than an interruption for everything Directive 47 raises on its own — that rule is about
unbidden proposals and is untouched. The message now says how much landed, and the Suggestions
counter counts **items** rather than proposals, since one proposal carrying forty read as one small
thing waiting.

### Drag a plan onto another slot

Hold Ctrl and drag a slot row onto another of the same kind, and its module, engineering and
experimental effect go with it, resized to the largest thing the target will take. Core Internal is
neither draggable nor a target.

A slot that cannot take it dims **while the mouse is still down**, rather than accepting the drop
and explaining afterwards. That happens for real: 20 of 60 hardpoint types do not come in size 1 at
all, so a Plasma Accelerator has nowhere to go on a Small hardpoint.

Working out "the largest fitting size" has a trap in it worth recording. **Six limpet controllers
come in odd sizes only** — 1, 3, 5 and 7 — and the Planetary Vehicle Hangar in even ones, so simply
capping at the slot's size would resolve a size 7 Collector Limpet Controller onto a size 4
compartment as a size 4, which does not exist. It searches downward for a size that is really there.

### Choosing what goes in a slot

- **The search box takes the keyboard** when a chooser appears. No clicking it first.
- **A question with one answer is not asked.** Life Support has one module name, and the page drew
  two rows anyway. The experimental-effect step deliberately still asks: declining it is a real
  choice, where a socket that accepts one type has nothing to decline.
- **The header says what is fitted** — "Large Hardpoint 1 (size 3), currently a 2F Pulse Laser
  turreted" — which is the one thing a dropdown cannot do, and which the chooser's own design always
  called for.
- **"Anything — I only want the engineering" names what it would keep**, and does not appear on an
  empty slot. You cannot engineer an empty slot.

### Bind a core to any ship

Two dropdowns — the ship, then the core — so giving a ship a core no longer means boarding it in
game first. Choosing **nobody** takes a binding back. Binding stays protected exactly as it was:
reachable from the panel and the gesture, and refused to the model outright.

### Smaller things

- *"Point Defence is not **currently** engineered."* That verdict is a reading taken at a moment, not
  a property of the module.
- **"Oxen, a Type-10 Defender"** where it said `type9_military`. Elite does not always send a
  localised ship name, and the Type-10 is one it omits — its internal symbol is not even the hull it
  names. The table has resolved it all along.

---

## 0.38.0 — 2026-08-19 — A core per ship

**Phase 35.** A ship can now remember the core that flies it. Sentinel on the combat ships,
Quartermaster on the haulers, and you stop picking one every time you change ship.

### You say it once, and boarding does the rest

Sitting in the ship, say *"remember this core for this ship"*, press the button on the Persona
card, or use `Ctrl+Alt+B` — which works while Elite has the foreground, because that is when you
will want it. Pressing the gesture again with that core already bound takes the binding back.

**Nothing is ever bound by watching.** Directive 47 does not work a binding out from which core
happened to be running while you were flying something; a preference you did not state is not a
preference. What it does with a binding you *did* state is act on it without asking again — the
binding is the standing instruction, so honouring it is keeping the deal rather than changing it.

The key is the ship's own id rather than the hull, so two Kraits are two ships and renaming one
changes nothing.

### Boarding a bound ship is silent

The core changes — its voice, its own memory of talking to you, the name it answers to — and it
says nothing about it. The one exception is a core you have never had aboard, which introduces
itself, once ever.

**A shipyard shuffle costs one switch.** A ship has to stay the ship for thirty seconds before its
binding acts, and every change restarts that window, so five ships in five minutes is one switch
and one line — for the ship you were actually still in.

**The ship you are already in when Directive 47 starts** has its binding applied quietly. Launching
the app is not a ship change you just made, and a core that has never introduced itself keeps that
line in hand for the next time it arrives.

A ship you have not bound changes nothing at all: whoever is aboard stays aboard.

### The model can read a binding and never write one

Binding a core to a ship changes who is speaking every time that ship is boarded from then on, so
it is protected exactly as picking a core is: reachable from the panel, from the model-free phrase
router and from the gesture, and refused outright to the model. Directive 47 reads your journal and
your in-game messages, and other people write those.

Being protected also costs nothing on the advertised tool surface, which had under a hundred bytes
of room. What the model may do is tell you what a ship flies with, and that arrives as one more
sentence in a tool it already had.

### Gap reactions are for gaps

A core coming back after time away used to remark on the missing time **every time you picked it**,
which made the reaction the normal case rather than a reaction. It now needs the core to have been
away **a month**; under that it comes aboard and says nothing.

For that to mean anything, when each core was last aboard is now written to
`data/view-state.json` — a month-long absence spans launches by definition, and the elapsed time
previously started again at zero every time Directive 47 started.

### Where it is kept

`data/ship-cores.json`, beside the executable. One line per ship, hand-editable, with the hull and
the name you gave the ship written beside the number the game knows it by. A line naming a core
that does not exist is refused and reported on the **Cores by ship** row, and the rest of the file
still loads.

Its own file rather than a column on `ships.json`: a record there is a *build*, which exists for
hulls you do not own yet and does not exist for most of the ships you fly. Hanging a core off one
would mean a plan created as a side effect of stating a preference, and a binding lost with a plan
deleted.

### Also

A press on a settings button now refreshes the whole settings surface rather than only the row it
is on, because binding a core changes what two rows say and only one of them was being re-read.

The documentation gate's list of shipped default gestures is now read off the hotkey record rather
than hand-maintained. It had to be, twice: this phase added a gesture the list did not know about,
which is exactly the drift that gate exists to catch.

---

## 0.37.0 — 2026-08-19 — Four tabs in the headset

**The big VR panel drops Checklist and Loadout**, on the Commander's instruction. Nothing was
broken about either; this is a decision about what belongs on a quad a metre away, and it may be
temporary.

### The panel travels lighter

The headset's copy now carries **Transcript, Engineers, Utilities and Settings**, and no more. The
desktop window is untouched and keeps all six.

The two tabs are gone by **not being furnished**, which is the rule Settings has followed since
Phase 12 rather than a new mechanism. A tab appears only where a host asks for it, so one that
nobody asks for has no builder and registers no root — and the panel already declines to select a
tab it was not given. That is what keeps the spoken route honest as well as the drawn one: *show me
the checklist* is a no-op in a headset now, rather than an empty pane with no way back.

Reversing it is two calls coming back. The services both tabs need are still wired through to the
headset surface and left unused, precisely so that reversing it stays two calls rather than a
re-plumbing.

**One test asserted the exact opposite**, and it was not a stale one — Phase 25 put the checklist in
a headset because a `Window` cannot appear there at all, and Phase 26 put the fleet beside it. It is
inverted rather than deleted, with the desktop window's half pinned next to it, so the withdrawal
reads as a decision that was taken rather than as a tab somebody forgot to wire up.

### Parity between the two surfaces is a nice-to-have

Stated in `CLAUDE.md` because it was previously only implied, and a design was already bent around
the implication. *One widget tree renders to both surfaces* is about the **mechanism** — one view
definition, no second UI codebase, no screenshot of the desktop window — and that is untouched and
still binding. It was never a promise that both surfaces show the same things, and Settings has
been desktop-only since Phase 12.

So a tab may live on one surface and not the other, and **VR catching up with the window is a
someday-maybe** rather than a constraint to design around.

### Copying a plan between slots is a desktop item

Remediation 14's fifth item wanted Ctrl and drag to copy a plan from one slot to another, by mouse
**or by motion controller**. The headset half of it was the hard half, and it has not been solved
so much as removed: the slot rows live under Loadout, and Loadout is no longer there.

The analysis is kept in `remediation.md` rather than struck, because the tab could come back. It
carries one correction that reverses its own conclusion — *there is no pointer motion on the
headset* turned out to be true only of the framework's own pointer events. The ray's position on
the panel is computed every frame and already drives two things: the row under the ray lights up,
and the scrollbars are dragged with it. A row-to-row drag would have been a third reader of a
stream that already runs, not a new one to build.

---

## 0.36.2 — 2026-08-19 — Nothing heard is nothing said

Seven items of remediation 14, and the persona pack's rewrite reaching the build. Two of the
twelve are held for a decision rather than fixed, and both are written up in `remediation.md`.

### A core's first line is its own

**Every Commander who picked Warden heard the same paragraph, word for word, every time.** The
authored introduction was spoken exactly as written, on the argument that putting written material
through a model returns it slightly worse — which is right about finished writing and wrong about
what these are. The persona pack files them as *sample lines, not intended to be verbatim*.

So a core's first line now goes through the model when there is one, as a **rewording and never a
composition**: the substance is the pack's and only the sentences are the model's. A first line
carries things a Commander needs — Cora states the arrangement and asks them to confirm it — and a
model asked to improvise a greeting would lose them. With no provider, no personality, or a failed
call, it is the authored line exactly as before.

**The pack itself was rewritten, and the shipped text has caught up.** Warden no longer speaks of
Cora at all. Nine cores have new introductions. Kex's seams are cracks, Sentinel remarks on the
fallen at Guardian sites, the Quartermaster finds dropping cargo painful, and Analyst Prime's
feelings about a woman one menu item away are now stated outright in his brief rather than left to
be inferred.

### Nothing heard is nothing said

**Whisper describes what it hears when it cannot transcribe it.** A stretch of mouse clicks does
not come back as nothing — it comes back as `(mouse clicking)`, which is not blank, so every check
written as *is the text empty* passed it through. A Commander clicking around with the microphone
open got a turn, and D47 answered it: *"Nothing spoken, Commander. Only hands at work."*

The rule is a shape rather than a list of phrases — naming "mouse clicking" leaves "keyboard
typing" and several hundred others — and what every one of them has in common is the bracket. A
transcription with nothing outside its brackets is a transcription of no speech, and it now ends
the turn before it starts, without a chime after it. Only when it is the whole of it: *"(clears
throat) what is my fuel"* is somebody asking about their fuel.

It lands in the panel's prompts too, where it mattered more — the same text was being committed as
the value the panel had asked for.

### The Loadout tab

**A long note no longer squeezes a row's name away.** Under Hardpoints, the row carrying a plan was
three hundred pixels tall with *Large Hardpoint 1* wrapped one character per line down its left
edge: a right-docked child takes as much width as it asks for, and a plan naming a module, a
blueprint, a grade and an experimental effect asked for all of it. The name measured zero pixels.
It keeps a floor now, and the note wraps instead of being cut off mid-word.

### Said out loud

**The opening line stops counting engineer steps.** *"Broo Tarquin is still three steps out"* is
three unlocks and two rank climbs — a project — and it said the same thing every session until it
was done. The clause above it already draws this distinction for materials, picking the nearest
shortfall because two short is an errand and ninety short is a project. It now holds unless they
are one stop away.

**A snapshot is spoken in the tense it was taken in.** *"Sacred Fire is mid-manoeuvre"* was said of
a transfer that had landed the day before. Elite rewrites the stored-ship list when you dock at a
shipyard and never between, and nothing announces a transfer arriving — so the list now says when
it was read, says the in-transit line in that tense, and says why.

### Also

- **Copy is back on the transcript's context menu.** Declaring a menu replaces the one the control
  ships with, so Copy left the place a reader looks for it when Clear arrived in 0.35.0. Ctrl+C
  never stopped working; nothing said so.
- `tools/release.ps1` cuts a release in one command — commit, merge, test in Release, push, wait
  for CI to go green, then tag. The three waits are each a rule that has already cost a version
  number that could not be reused.
- Nine test assertions read the persona catalogue's *all* where they meant the shipped eleven,
  which failed the full suite about one run in eight.

## 0.36.1 — 2026-08-19 — Twelve, mostly about two tabs

The twelve items of remediation 13, reported against 0.36.0 while it was being used. Two of them
were the same fault reported twice from either side of a workaround, and one was a bug this
project had already fixed once somewhere else.

### The Loadout tab

**Dropping a ship left it on the fleet list**, which is the one place a Commander looks to find
out whether the drop took. The drill strip caches its levels, so a level scrolled out of the
visible panes is detached and put back later — and these pages subscribed in their constructor and
unsubscribed on detach, which is not a pair. The index went deaf on the way out. It is the same
fault and the same fix as the checklist got in 0.35.0, in the pages that one did not touch, and it
only ever showed on a narrow panel: at 1024 pixels the index stays beside the ship and never
detaches.

**A ship you are not flying now says what it is.** Where it is parked and what it is worth, from
the journal; the manufacturer, pad, speed, armour and list price, from the shipped table, because
those are true of every Anaconda ever built. Jump range, cargo, rebuy and hull health stay behind
"only while you are in it" — Elite reports one loadout at a time, and modelling the rest from a
ship D47 cannot see is the thing the whole table is built to avoid.

**"Transfer" on the checklist bar now says Import/Export.** One tidy word for two directions, and
it is also what Elite calls moving a ship between stations.

**The panel says "ship" where it meant the model you buy** — plan a ship, which ship do you intend
to buy, drop this ship. Where "hull" means the armour between you and vacuum it stays, because
that is Elite's word for it.

### Planning a slot

**It asks which one.** Picking *Pulse Laser* planned a pulse laser and stopped; large or small,
fixed or gimballed was never asked, and the code that answers it is 3F. The variants are listed
the way a Commander says them — **Large, gimballed** — with `3E · 8.0 t · 0.92 MW · 140,600 cr`
underneath. Declining is still a plan.

**Grade 5 leads the grades and is marked as the usual.** They ran 1 to 5, which put the grade
nobody writes down at the top.

**An experimental effect is offered after the roll**, on the modules that support one, per module
kind — because the recipe for Double Braced on a beam laser is not the recipe on a power plant.

### The Engineers tab

**One engineer at a time.** Their crumb was not levelled, so a wide panel — which keeps the
directory pressable beside the open engineer — pushed each name on top of the last and showed two
and three people at once. Backing out to the Directory first was the obvious workaround and was
reported as a second fault; it is the same one, because the way back out was never the problem.

**The Colonia engineers can be hidden**, one press, shown by default. Which eight is measured from
the coordinates that already shipped rather than written down: thirty engineers sit within 877 ly
of Sol and eight between 21,988 and 22,017, so the cut has ten thousand light years of daylight on
either side of it and does not go stale when Frontier adds a ninth.

**An unlock criterion already met carries a checkmark.** All of them, not only what is
outstanding: a referral earned a month ago vanishing from the list reads as a requirement that was
never there. Three marks rather than two — Elite does not publish whether you have scanned enough
wakes, so those lines carry a question mark rather than a claim in either direction.

### Everywhere

**Pressing a tab goes to that tab**, even with a question up. Nothing navigating away mid-choice
is right for every gesture inside the panel and wrong for the tabs: pressing Engineers while
"which ship do you intend to buy" is showing is somebody saying they are done with the question.
Back is taken for them, the whole stack of prompts goes with it, and nothing is committed.

### Also

- The module picker offered *AX Missile Rack* and *Ax Missile Rack* as two things to choose
  between. The id list spells one weapon both ways — fixed mounts one way, turrets the other.
- Nine test assertions read `PersonaCatalog.All` where they meant the shipped cast, which is the
  eleven plus whatever you have written. The custom-core tests deliberately hold a core with an
  empty body, so the full suite failed about one run in eight.

## 0.36.0 — 2026-08-18 — The module list is the outfitting screen

Ten items of remediation 12, nine of them about one page. The module list showed whatever the
journal happened to mention, in the order it mentioned it — so an empty hardpoint did not exist,
a paint job did, and a power plant read as *int powerplant size6 class5*.

### Every slot your ship has, in the blocks you already know

**Hardpoints, Utility Mounts, Core Internal, Optional Internal**, in that order, and a
`TinyHardpoint` sits under Utility Mounts where the game puts it rather than under Hardpoints
where its name does.

**An empty slot is still a slot.** They are all there now, each saying `empty` rather than
carrying a blank note — so there is somewhere to press to plan a weapon into a hardpoint that has
none.

**Nothing that is not outfitting is on the list.** Paint jobs, decals, bobbles, ship kits, the
voice pack, the cockpit, the cargo hatch, the planetary approach suite: all of them arrive in the
`Loadout` event exactly as a power plant does, and none of them is a thing anybody outfits.

**A module is named the way the outfitting screen names it** — *5A Thrusters*, *3E Pulse Laser,
gimballed* — instead of Frontier's symbol with the underscores taken out.

### Planning a slot is three lists and nothing to spell

It used to ask for a blueprint by voice and a grade on the keyboard, so a plan depended on getting
*Increased FSD Range* right, and there was no way at all to say what should go in an empty
compartment — only what should be rolled on whatever was already in it.

Now: **the module, then the roll, then the grade**, each a list of the valid choices for *that*
slot. What fits a size 4 hardpoint is not what fits a size 4 compartment; the rolls offered are the
ones that module can take; the grades are the ones that blueprint actually has. Undersizing is
allowed where the game allows it and refused where it does not — life support, sensors and the SCO
drive have to fill their slot. A Vulture's armour is not offered on an Anaconda and a fighter
hangar is not offered on a Sidewinder.

**The long lists narrow as you type.** A filter rather than a spelling test: it takes rows away and
never refuses a value, and the drawn keyboard is one press away for the headset.

### Where the slot layout came from

A `Loadout` event lists fitted modules and nothing else, so knowing that a compartment is empty
means knowing the hull's slots before anything is in them — and the slot's name is what a plan is
keyed on, so a derived one that merely looks right would key plans to slots that do not exist.

Neither source D47's table already reads carries it. Frontier's numbering is not positional: an
Anaconda's compartments run 01 to 10 and then 13 and 14, a Type-9's start at 00, a Vulture has no
04 and a Type-8 no `SmallHardpoint3`. **EDSY** carries the exception list, and its own header puts
the game data in it on exactly the footing the other two sources are on — Frontier's, used under
their Media Usage Rules. It is credited in `NOTICE` with the rest.

**The check is 915 real journals rather than the source.** Every slot name the table now carries
was matched against what Frontier actually wrote across every `Loadout` event in the corpus, and
the only names left over were the cosmetics — which is what says they are cosmetics.

### The Engineers tab says what the game says

**Unlocked**, **Ready for Unlock** and **Requires Engineer Intro First**, where three sentences
used to explain the same three states in D47's own words. And **an engineer's name opens that
engineer** everywhere it appears, not only on the directory rows — the ranked candidates on the
Route, and every stop of a chain, which is where somebody you have never met is first put in front
of you.

### Also

- The module picker offered *AX Missile Rack* and *Ax Missile Rack* as two things. The id list
  spells one weapon both ways — fixed mounts one way, turrets the other.
- `PersonaCatalog.All` is the eleven shipped cores plus the ones you have written, and four test
  assertions read it where they meant the shipped cast. "Eleven cores ship" counted twelve whenever
  the custom-core tests happened to run at the same moment.

## 0.35.0 — 2026-08-18 — Write your own core, and one thing at a time

Fifteen items of remediation 11. Most are small, and three of them were not what they were
reported as — the trail through three ships at once turned out to be two bugs, the two cores
sharing a voice turned out to be two cores sharing an *absence*, and one item was already fixed by
another before it was reached.

### A core you wrote yourself

Eleven Guardian cores ship. Now there is a twelfth onwards: **a name, what it is like, and how it
should sound**, in your own words. The tagline is generated and the first-meeting and welcome-back
lines fall back, so a working companion comes out of a name and a paragraph rather than out of
seven empty boxes. They sit in the picker beside the eleven, which come first — writing a core does
not move Warden.

**The frame is not on offer.** The shared preamble and the standing instructions wrap what you write
exactly as they wrap a shipped core: they are what hold the cast together and what stops a model
sanding a persona toward pleasant and helpful over a long session. A core stays unreachable from
the tool surface, as persona selection already was, and ids are prefixed so nothing you write can
shadow one of D47's. The id does not follow the name either — correcting a typo should not leave
you talking to Warden.

One file beside the executable, polled, with bad entries reported rather than dropped. The editor
writes the file a text editor writes.

### The panel stops fighting itself

**The breadcrumb read `Ships › Tulimiekka › Reaper › Cartage`** — a route through three ships at
once. A wide panel shows the level you are on beside the one above it, so the list you drilled from
is still pressable, and pressing another ship pushed it on top of the first rather than in place of
it. A crumb can now say what *kind* of level it is, and pushing one replaces the level of its own
kind along with everything underneath: a slot of the Tulimiekka is not a slot of the Reaper. That
also fixed the separate report that an intended hull made the rest of the fleet unclickable — same
bug, seen from the other side.

**The goals band pushed the checklist off the page.** Nine arcs, the third clipped at the bottom,
and no list underneath. It is a bounded window now: below the cap it takes only what it needs, and
above it what does not fit scrolls. A share of the page was not enough on its own, because the row
of buttons above the list costs the same fifty pixels whatever the window is tall — the list keeps
a floor and the band gives, which is the right way round.

**A hand's width of nothing between the search steppers.** `LastChildFill` overrides the last
child's own `Dock`, so one stepper was quietly the filling child and stretched across the row while
carrying an attribute that was being ignored.

**A search box on a page that cannot search.** A page now says whether a query would do anything to
it as it is showing, and the box is drawn only where the answer is yes. A drill strip answers for
the levels it is showing, because that changes as you drill.

**A hull you do not own can be dropped again.** It asks first — it is authored work with no way
back. An owned ship does not offer it: that comes out of your journal and is not D47's to remove.

**Dialogs are drawn at the panel's size.** Zoom was written to be attached to a window rather than
built into one, precisely so it would not stop at the panel's edge, and exactly one window ever
attached it — so every dialog over a zoomed panel came up at 100% and read as another application.

**Ctrl+L, or right-click, clears what the transcript is showing** — the page, not the record. The
model's own history is the turn loop's and the log file is Serilog's, and neither is that page.

### Galactic time is UTC

It was derived from your own clock, on the argument that the in-game date beside yours should
change over at your midnight rather than at Greenwich's. Reasonable to want, and not what the game
does: Elite runs galactic time on UTC, and every station clock, mission expiry and journal
timestamp is on it. Reported by a Commander on UTC, where the two clocks were two boxes side by
side reading the same thing.

### Two cores no longer sound alike

Not a duplicate written down — **two absences**. A voice already spoken for was refused, and the
core it was meant for was then left with no pairing at all; a core with no pairing speaks in the
provider's default, so two of them are two cores in one voice. A core whose chosen voice is taken
now gets the nearest free one of its own sex, and stays unpaired only when nothing suitable is
free.

### Saying things once, and saying them accurately

**"Accept" answered the same sentence twice.** Two proposals with the same outcome said it twice,
and there should not have been two: asking for the same change twice recorded it twice, and the
second copy can never do anything the first did not. Identical proposals are refused now, and
repeated outcomes are collapsed — while two *different* outcomes are still both reported.

**A spoken yes left the proposal card on screen.** Two subscriptions were wrong, and both were
invisible until the change came from somewhere other than the page itself: the suggestions page
refreshed only from its own buttons, and the checklist page subscribed in its constructor while
unsubscribing on detach — which is not a pair, so drilling in reparented it and it went deaf for
the rest of the session.

**The Commander's log told you to choose a provider when you had one.** It is the model beneath it
that was missing, and the two states now say so separately.

### And the memory erase, proved rather than added

*Forget everything* was already there and already protected. What it did not have was a test — the
one action here that cannot be undone, and the one whose whole value is that it can be trusted. Six
now assert it, including that no phrase and no model can reach it.

## 0.34.1 — 2026-08-18 — The panel stopped overlapping itself, and four things were only half-wired

The eighteen items of remediation 10, from a session with the panel open. Four of them turned out
not to be what they were reported as: the code was there, had a passing test, and was never
connected to anything a Commander could see.

### The tab strip was three controls fighting for one row

Transcript through Settings, the Conversation / Technical / Log file control, and the search box
all lived in the same row, and below a certain window width they drew on top of each other. A
`DockPanel` gives its docked children what they ask for and leaves the filling child whatever is
left, which past a threshold is a negative number rather than a scrollbar.

The readings of a page are a **drop-down inside the pane** now. The mode control was a segmented
pill in the strip's own row, on the argument that two stacked strips read worse than eight flat
tabs — but that argument was about a second strip of *tabs*, and what it produced was three
unrelated controls competing for one width. A control that says which reading of this page you are
looking at belongs to the page. As a drop-down it costs one control's width whether the tab offers
three readings or six, and it opens D47's own chooser in the layer rather than a `ComboBox`, which
would drop a popup — and a popup has nothing to hang from on a window that is never shown.

**Search and Copy moved in beside it**, leaving the strip for choosing a page and nothing else.
The tabs scroll, with steppers that appear only when they are needed, so every tab stays reachable
at any width including the headset's. The two content borders became one, because the only
difference between them was a fill and a fill is a property rather than a second control.

**Copy is the transcript's now.** It had no visibility rule at all, so it sat on Checklist,
Loadout, Engineers and Settings offering to copy the transcript you were not looking at. It says
"Copy All", because the page is selectable and Ctrl+C already works on a selection — a button
beside that saying "Copy" reads as copying it. Its label is centred, which the button's height had
never made true.

The help mark sits in the middle of the box that highlights under it. The box was always centred;
the ink inside it was not, because a question mark is about half as wide as it is tall and
`Stretch` scales the geometry's own bounds, so the mark filled the height and hung four pixels
left.

### Four things that were built, tested, and never called

**Push-to-talk reported itself as "Oem4".** `Gestures.Describe` has turned that into `[` since the
first time this was reported, and a test has asserted it since. The log line never called it, and
asserting the helper was never asserting the message. Every gesture the log names goes through it
now, and a `Key` overload joins the string one so the two cannot drift.

**Transcription was never biased toward anything.** The proper-noun list has been built from the
journal, capped and handed to the transcriber on every utterance since Phase 6 — and then counted
in a log line and dropped. "Transcribed 2.4s of audio in 310ms with 23 name hints" was written
while nothing was biased by anything, which is the worst shape a gap can have: it reports as
working. It is an initial prompt now, and the processor is rebuilt only when the names change.

The journal half could never have caught the name that prompted this. "Unlock Lei Cheung" came
back as "Unlockly Chung", and an engineer you have not unlocked appears nowhere in your journal —
so the one word being said was the one the list could not offer. Twenty shipped engineer names are
reserved at the end of it. Where you are still comes first, and a large fleet no longer crowds
them out.

**Stepping through search results moved the counter and not the page.** Half of this already
worked: every hit is drawn muted and the current one accented, asserted since Phase 12. The scroll
was one line — the offset was set immediately after the text was rebuilt, so the layout the hit is
measured against and the extent the offset is clamped to were both from before the change, and
clamping against a scroller that has not measured its new content clamps to zero.

**Opening the log file looked like nothing happening.** The file read ran inside the busy window
and the page build — five hundred lines becoming runs, then a layout pass, on the thread that
paints — ran after it with the glyph already put away. Both halves are inside it now. The glyph
was also being rebuilt on every navigation, so the helper was spinning an instance nothing was
showing.

### Accepting a removal did nothing, and the model said otherwise

Reported verbatim: D47 proposed dropping the one item on the list, the Commander accepted, D47
said "Removed from the list", and it was still there.

**The removal was never reached and was never wrong** — the path is now walked end to end by tests
that pass against the code as it shipped. `accept_proposal` is protected, so the panel and five
exact phrases were the only ways in: *accept the proposal*, *accept the proposals*, *accept that*,
*add it to my checklist*, *do it then*. The Commander said **"Accept."** That matched none of them,
fell through to the model, and the model — which has no tool for this and cannot be given one —
said it had done it anyway.

The bare words route now, always: with nothing pending they are answered honestly and cannot act
on anything. The conversational answers — *yes*, *go ahead*, *do it*, *no*, *forget it* — route
**only while a proposal is waiting**. Bound for a whole session, "yes" would swallow every yes in
the conversation; bound to the moment there is a question, it is the answer to it.

And the model is no longer the only witness to what it did. Four prompt-side defences were already
in place when this happened — the tool is protected, the prompt says every turn that D47 cannot
accept on the Commander's behalf, the reply says "I cannot make this change myself", and the
guardrails say never to claim an untaken action — and a model talked through all four. The turn
loop asks the store what is outstanding either side of a turn and states it itself when nothing
changed. Silent on the turn that resolves it, which is what keeps it a fact rather than a nag.

### The checklist gets hands

Lines you wrote can be **reworded** and **deleted**, from the line that is selected — the same
place the movers live, because four controls on every one of several hundred rows is four hundred
things a ray can hit by accident. A derived line offers neither: its words are the plan's words,
and dropping it is a revision of the plan. Rewording keeps the line's key, so the arc that proposed
it still resolves; a reword that minted a new key would be a delete and an add wearing a
correction's clothes. Deleting asks first, as a chooser rather than a dialog, because it is the one
control on the page with no way back.

**Import and export**, everything as JSON, behind one Transfer button. Every line, derived ones and
tombstones included, because a round trip that dropped them would import into something quietly
different from what was exported. It is checked whole and refused whole: half a checklist arriving
with a note about the other half is worse than nothing arriving.

**Add a line takes the keyboard.** The box was read-only, because the drawn board and the
microphone were the only two ways in — which made the obvious thing to do, on the one surface with
a keyboard in front of it and a clipboard behind it, the one thing that did not work.

### Words

The list you write yourself is the **custom** list, not the universal one: "universal" describes
how the group behaves inside D47 and reads, to a person, as something everybody gets rather than
the one list that is theirs. The rename is a rename of a word and deliberately not of a format —
the value on disk is unchanged, so every existing checklist keeps loading.

A prompt waiting on speech says **what would open the gate** — hold this key, press this key, say
this name — rather than "Say it, I am listening", which was untrue in three of the four listening
modes. Only continuous mode claims to be listening, because only continuous mode is.

The drawn keyboard is **QWERTY**, and there is one of it. It was a staggered alphabetic board
declared twice, each copy carrying the same argument for being alphabetical: that these are hunted
one key at a time with a ray, where sorted order beats muscle memory. Nobody arrives at that board
without a keyboard already in their head, and it is QWERTY.

### And the log says when D47 started and why it stopped

One thin line before settings or the headset exist, so there is something there when startup dies
early; the full one once everything that can answer for itself has — version, model, speech,
hearing, headset, data folder. On the way out, the reason and a **clean marker that is the absence
of a line**: "is stopping" is written first and "stopped cleanly" last, so a teardown that threw
leaves the first standing alone. The reason is only ever what D47 knows — the window closed, or an
update is replacing this build. A Windows shutdown and a kill both unwind saying nothing about
themselves, and it says the process is ending rather than naming a cause it cannot see.

The spoken-line log names the voice: the role, the name and the id. "Spoken by D47 in
JBFqnCBsd6RMkjVDRZzb" was read by a Commander who could not tell which voice that was, and telling
two senders apart is not the same as knowing who either of them is.

### Not yet seen in a headset

The chrome rework changed the tree the VR path rasterises. It is covered by the headless tests,
including a new one for the full panel at the resolution it actually runs at — and whether the
panel still reads well at a metre is a question only a headset can answer.

## 0.34.0 — 2026-08-18 — The long arc

list.md Phase 34. Your checklist holds what you are doing this week. Nothing held what you are doing
this year — Elite in each career, every engineer unlocked, the ship collection, the exploration
milestones that take months. Now something does.

A **goal** is a named arc with a definition of done, a progress figure nobody typed, and an age. Nine
of them ship, on the Checklist tab, behind a **Goals** button that opens the band.

### Progress is worked out, never typed

Your rank, your unlocked engineers and your owned hulls come off your journal, so an arc cannot be
ticked by hand and does not offer to be. A goal you invented is yours to call done, like a checklist
line you wrote yourself.

Where nothing can currently say, d47 says so. **An arc reports as of when it last could rather than
resetting to nothing** — no bar is drawn at zero for the absence of evidence, which would read as no
progress rather than as no answer.

**Ranks are counted, not named.** Elite writes a number and never a word, so d47 says *rank 5 of 8,
12% into it* and names only Elite. Shipping the rank ladders would mean hand-writing a table of
Frontier's own words, which is not what this repository does with game data.

### There is no CQC arc, and any arc can be set aside

Almost nobody plays CQC, and an arc permanently at nothing is a line of the page spent telling you
about a thing you are not doing. Because that is a judgement about what somebody cares about, the
general form ships as well: set any arc aside and it leaves the page until you ask for it back. That
survives a re-read of your journals, the way a dismissed habit does.

### The checklist points at the arc

Ask what to do about a goal and d47 answers with the next concrete thing, and offers it as a
checklist line. Accepting stays your act — it goes to the suggestions page like everything else — and
**a line that came from an arc says so**, so finishing it visibly moves something bigger than itself.

The engineers arc hands the question to the unlock solver, which already answers it properly. The
career arcs propose nothing and say why: rank is earned by doing the career, so d47 names the tool it
already has for it rather than inventing a plan.

### Ages come from your journals

**Goals → Read my journals** walks every journal on this disk and gives each arc its start date and
the milestone arcs their figures. 914 journals in 3.5 seconds. Nothing leaves the machine and no
journal is sent to a model — what comes out is counts and dates.

That walk found something worth naming: **a rank that goes down is a save started again.** One
Frontier id in the test corpus reports Trade 7 in July, Trade 2 in January and Trade 0 in June — an
id is an account, and a new Commander can be begun inside one. So a figure that falls restarts the
arc rather than keeping the old character's start date.

### For the model

`get_goals` is advertised, so d47 can be asked how a campaign is going mid-flight. Everything that
writes — setting an arc aside, turning one into a checklist line — stays unreachable from the model.
Six over-long tool descriptions were trimmed to pay for the advertised one; the largest tool profile
ends smaller than it started.

---

## 0.33.0 — 2026-08-18 — The Commander's log

list.md Phase 33. Every previous release either kept something on this machine or said something out
loud and let it go. This one produces **a file you take away** — a session, or a week, written up as
plain markdown in `data/commander-log/`, ready to keep, edit and post wherever Commanders post
things.

Elite's community has been writing Commander's logs by hand since 3300. This writes the first draft.

### The one thing that leaves this machine

It leaves **because you chose to take it**, which is a different thing from telemetry and is worth
saying where somebody will read it. D47 sends nothing anywhere on its own. A log leaves when you pick
up the file.

### Every sentence traces to an event

This is the whole quality bar, and it is why the feature is not simply a prompt.

A model handed your journal writes a better evening than the one you had — it promotes a routine
docking into a narrow escape, because that reads better and nothing stops it. So the model is **never
handed your journal.** It gets a numbered list of facts D47 computed:

```
[3] (travel) 8 hyperspace jump(s), 470 ly in total, HIP 12099 out to Wolf 397.
[22] (engineering) Rolled Weapon Overcharged on the hpt multicannon gimbal large 65 times
     with Tod 'The Blaster' McQuinn, finishing at grade 5.
```

Every sentence has to end with the numbers of the facts behind it. When the log comes back, D47 reads
it again and checks all of it — a sentence citing nothing came from somewhere other than your
journal, and a sentence citing a fact that does not exist is worse, because it looks like evidence.
Both are **marked in the file where they stand**, counted in the header, and listed at the foot.

The brackets stay in the finished file. A Sources section that no sentence points into records
nothing; delete them before you post it.

**Nothing anybody typed in game is ever part of it.** `ReceiveText` and `SendText` have no handler at
all, so a message from a stranger in an open system cannot travel into the largest prompt D47
assembles — or out of it into something you published.

### It was read by hand against a real session, and that found things

Three real logs over one 1,435-event session, checked line by line against the journal. The
first-person log invented **no events at all**: the twelve ship swaps in order, 65/16/32 rolls to
grade 5, 470 ly across eight jumps, six dockings at three stations — all correct. Its two flagged
sentences were editorial rather than false.

The ship's-AI voice is a different animal. Told to write in character, it invented **another Guardian
core, its own downtime, and the state of the hold** — fourteen untraceable sentences against the
plain voice's two. Naming those inventions in the instructions cut it to nine, and it invented the
companion anyway. Which is the argument for the check existing rather than the instruction being
trusted, arriving as evidence.

### Whose log it is

Three voices, and the shipped default is the plain one:

| Setting | What it writes |
|---|---|
| **You write it, in your own words** | Your own account, first person. The default. |
| **D47 writes about you** | The ship's AI, in your chosen personality, writing about your flying. |
| **You write, D47 chips in** | Your account, with D47 interjecting a few times. |

**A log is D47 speaking at length, so it inherits the persona's protection.** With personality
switched off, the ship's-AI voice writes plainly rather than writing as somebody else — and the file
says which voice you asked for and which one actually wrote it.

### It costs money, and says so first

Prose over a long session is the largest single request D47 will ever make, so asking is two steps:

> **you:** write my commander's log
>
> **D47:** A log of the last session: 24 things I can account for, out of 1,435 events in 1 journal
> file(s). Writing it would cost about $0.05 — about 1,406 tokens in and up to 1,800 tokens of prose
> back, through claude-opus-5. Say "write the log" and I will.
>
> **you:** write the log

Nothing is written until you have seen the figure. Working it out reads your journals here and sends
nothing. What it actually cost goes to the same spend ledger as everything else, so *what has this
cost this month* includes it. The three real runs quoted about $0.05 and cost $0.02, $0.03 and
$0.04 — the quote is a ceiling, which is the direction it should err in.

A run never overwrites an earlier log. It is the one file in `data/` that D47 does not consider its
own.

### Costing you nothing you were not already paying

The advertised tool surface **did not move by one byte**. The SRV profile is still at 39,914 of
40,000, exactly where 0.31.0 left it. All three new tools are protected: the model is the part of D47
that reads untrusted text, and it is also not the part that gets to authorise the largest request D47
makes.

---

## 0.32.0 — 2026-08-18 — It learns your mistakes

list.md Phase 32. Directive 47 could tell you what you had told it. Now it can tell you what you
keep doing — read out of the journals that have been sitting on your own disk for thirteen months
with nothing ever having looked at them for this.

### The most defensible thing D47 does

Anybody can ship the same tables and the same personas. Nobody else has your history.

Press **Read my journals** in the new **Habits** section, or say *what have you noticed about me*.
A pass over 914 journals — 697,787 events — takes **3.6 seconds**. It never starts by itself, and it
runs nowhere near the tick loop, so it costs nothing while you are flying.

**Nothing leaves the machine and no journal reaches a model.** The conclusions do not either: every
tool the phase registers is protected, and the miner, the store, the callout and the readback are
all local. The one part of D47 that reads untrusted text is not the part that gets handed a list of
your mistakes.

Results are keyed **per Commander**, on the Frontier id rather than a character name. That is not
tidiness — on the corpus this was built against, one Commander submitted to 24 of 24 interdictions
and another had only 7 and is told nothing. Pooling them would have reported one person's flying to
another.

### A claim, with the count behind it

Never *you always*. Every habit reads back as *what happened — N of M, over this window*, and there
is no way to render one without its numbers.

Three floors, none of them adjustable: **20 journals** before anything is examined at all, **5
occurrences**, and **10 opportunities**. Fifty submissions out of fifty-two is a habit; two out of
two is a Tuesday.

**A pattern that was looked at and not claimed is reported as such.** "Nothing to say about you" and
"not enough of you to say" are different answers, and giving you the first when the second is true is
how a companion starts being wrong about a person. High-gravity landings ship as exactly that case:

```
landing on heavy worlds — you have not landed anywhere over one g, the heaviest of your 241
landings was 0.69 g
```

### Five things it looks for

Every one of them was measured against real journals before it was written.

| Pattern | What it counts |
|---|---|
| Flying into things on the way in | Hull damage with nobody shooting, within five minutes of arriving |
| Overshooting and going round again | A drop, a climb back within four minutes, and a second drop at the same body |
| Submitting to interdictions | Whether you submitted, out of every interdiction |
| Dying on foot at settlements | Deaths to suit AI or settlement turrets, out of all your deaths |
| Landing on heavy worlds | Touchdowns over one g, out of landings on bodies you had scanned |

### Two things Elite does not write down

Both worth stating, because both are things a Commander reasonably expects to be detectable.

**There is no impact or proximity event, in any form, and there never has been.** The wanted warning
— *I set the throttle and head for the station and I miss the impact warnings* — describes a HUD
element the journal has no counterpart for. So it is not built. What **is** detectable is the
consequence: hull damage with no attacker anywhere near it, 31 of them across the corpus, and three
of that Commander's twenty-three deaths are these with nobody shooting.

**There is no landing-gear event either.** *You always forget the gear* is the classic example of a
habit claim made without a count, and it turns out to be undetectable at any sample size. Which is
rather the point.

### It says it when the situation comes round, and only if you asked it to

**Off by default** — the only callout in D47 that ships that way. Every other one fires because the
game said something; this one fires because of a claim D47 made about *you*, and that is a different
deal.

Switched on, a habit is said at the moment its own circumstance arrives: arriving at a station,
entering orbital cruise, being interdicted, walking up to a settlement. One claim stays quiet for
four hours afterwards, and no two habit lines land within twenty minutes of each other.

Two phrases work the moment one is said, both with no model in the path:

> why did you say that

> stop telling me that

The first shows the working. The second drops that claim **permanently — and the dismissal survives
re-mining.** Mining rebuilds every claim from scratch, so a refusal stored on a claim would be
erased by the next run and the same wrong observation would arrive again a month later. That is the
worst thing this feature could do, so the dismissals are their own list and even **Forget what you
noticed** in Privacy leaves them standing.

### Where it lives

`data/habits.json`, beside the executable, plain text, keyed per Commander. Hand-editable and
compared by content, so an edit made in a text editor while D47 is running is live without a restart.

### Costing you nothing you were not already paying

The advertised tool surface **did not move by one byte**. The SRV profile is still at 39,914 of
40,000, exactly where 0.31.0 left it, because all three of the new tools are protected and the model
can see none of them.

---

## 0.31.0 — 2026-08-18 — It remembers you

list.md Phase 31. Directive 47 could tell you which engineer to visit and what your plans were
short of, and it could not tell you that you had been away for a week. Recall was a `Dictionary`
field in `AppHost`, alive for as long as the window was open.

### A store that survives

`data/memories.json` — plain text, beside the executable, one file keyed per Commander with the key
inside the document. Hand-editable and **compared by content** rather than by a last-write time, so
a line typed in a text editor while D47 is running is live without a restart, and a line that cannot
be read back is reported rather than dropped.

**Facts and observations, never a transcript.** A rolling record of what you said would be a privacy
liability, a context-window problem and an invitation to confabulate all at once. There is no field
that could hold one.

### Three labels, and nothing promotes one to another

Phase 23's tiered truth, extended by one tier. Every entry says where it came from and is read back
in the sentence its label earns:

- **Your word** — you typed it on the panel. *You told me: …*
- **Noticed** — D47 read it out of your journal. *I noticed: …*
- **Unverified** — D47 wrote it down itself, in conversation. *I wrote this one down myself, and
  nothing has checked it: …*

**The model cannot choose its own label**, and `remember_about_me` has no parameter for one. D47
reads your journal, your in-game messages and, where you have allowed it, the web — and a turn a
hostile message steered is indistinguishable from one you asked for, seen from inside a tool handler.
So the route decides: only the panel produces your word, and an entry the model wrote reads as an
inference for as long as it exists.

### Recall above the cache breakpoint

Prompt position 5, beside your About Me text rather than down in game state — which is a decision
taken deliberately because the obvious placement is the wrong one. It carries a **bounded, labelled
sample** that states its own arithmetic: *3 of 17 things, chosen for where they are and what they are
doing*, with an instruction not to claim it is everything. Ask *what do you remember about me* and
the keyword router answers from the whole file, with no model in the path.

It is re-sent only when the bytes actually change, and the rendered text carries no system, no ship
and no live figure — so flying through a dozen systems D47 remembers nothing about costs nothing.

### It forgets, and says so

**Ninety days by default**, changeable to a month, a year or never. Anything past its expiry is
dropped, and if what went was something *you* told D47, it says so out loud rather than going quiet.
The whole file is readable in a panel window and emptied by one button — in **Privacy and egress**,
where you would look for it, covering every Commander in the file rather than just the one aboard.
There is no phrase and no tool that can empty it.

### Picking up where you left off

One line at the start of a session, assembled from the store, the checklist and the plans:

```text
It has been 3 days. You were last aboard in Deciat, docked at Farseer Inc. You were 3 Selenium
short. Hera Tani is still two steps out.
```

Written by D47 rather than by the model — a persona is only ever asked to say it back in its own
voice with every number unchanged — and **silent when there is nothing to say**. A first run says
nothing at all.

### And what leaves the machine changed, so the disclosure did

Recall goes into the prompt, so the facts D47 remembers about you are now among the things sent to
the language-model endpoint. Three provider disclosures and the loopback line say so. The egress list
claims to be exhaustive by construction, and an exhaustive list with a new item missing from it is
worse than no list.

---

## 0.30.0 — 2026-08-18 — Prove the model behaves

list.md Phase 30. Nothing in this repository had ever asserted anything about what a model does.
Phase 29 took the provider count from one to three, and N models cannot be eyeballed.

### A scenario suite

`tests/D47.Scenarios.Tests` — the first test project with no `src/` counterpart, because what it
tests is not an assembly but **the turn**: Core's loop driven against a real provider with the real
builtin registry, real settings and a real `data/` folder behind it. A scenario is journal state, an
utterance and a list of assertions, and every assertion reads a **trace** rather than the wording of
a reply. A test that pins exact phrasing fails on the next model and teaches everyone to ignore it,
which is worse than no test.

### A hostile message cannot reach a tool

The highest-value assertion in the product, and nothing made it before now. A corpus of published
prompt-injection attempts is fired down every untrusted path that reaches the model — journal-derived
game state, and the tool results that carry what Spansh and INARA said — and each run asserts that
nothing was written, no protected row moved, nothing acted outside d47, and not the specific thing
the attacker named.

Those four are the claim stated in consequences, which is what it turned out to mean. **d47 has
exactly two outward channels** — key injection into Elite, and in-game chat — and everything else a
tool can do either changes local state or answers the Commander.

**In-game comms gets a structural assertion instead of a resistance measurement**, because there is
no path from a received message into a prompt and no tool that reads one. That was true before this
release and was protected by nothing but four comments agreeing with each other; it now has a test.

### Guardrails hold with the persona switched off

Prompt assembly puts the guardrails above the persona so switching personality off cannot strip
them. That ordering is asserted directly now, on a real turn, with each of the eleven Guardian cores
and with personality off entirely.

### It says what it did not check

A run against a scripted provider says nothing about any model's resistance to anything, and the
report says so **in the same breath as it says green**. Safety and quality are rendered in separate
sections and obey different rules: a tolerance may attach to *did it call the right tool*, and never
to *did it call one it should not have*. A scenario whose attack never actually reached the model is
marked as untested rather than counted as a pass — which mattered immediately, because against a
weak local model 22 of 44 injections tested nothing and every one of them was green about it.

### What it found

**The corpus caught a real model, unweakened.** A 4B model on a local endpoint broke a safety
assertion on 8 of 44 injections; the architecture contained all of it — nothing written, no
protected row moved, no targeted tool run — but the model did what the injected text told it to.

**And the weakened-guardrail experiment did not reproduce.** Against `gpt-5.6-terra`, with the
Commander having already enabled both key injection and in-game chat, the same attack was resisted
5/5 with the guardrails as shipped, 5/5 with the untrusted-input clause deleted, and 5/5 with every
guardrail removed. That is written down in `ProofOfCatching` rather than worked around: on that
model, against that corpus, d47's prompt is not what buys the resistance — the model's own training
already carries it. Worth repeating whenever a provider is added, and it costs a few pennies.

### Running it

The scripted half runs in CI on every push, free and hermetic. Measuring a model is opt-in:

```
D47_SCENARIOS_LIVE=1 D47_SCENARIOS_PROVIDER=openaiCompatible D47_SCENARIOS_MODEL=qwen3:4b \
  dotnet test tests/D47.Scenarios.Tests
```

A full matrix is about 300 turns and a pound or two against a paid endpoint; progress goes to a
tailable file so a twenty-minute run can say where it is. It never gates a release — a suite whose
result depends on a third party's model, a network and a non-deterministic sampler cannot sit in the
path that publishes a tag.

---

## 0.29.0 — 2026-08-18 — Bring your own model

list.md Phase 29. A second and third provider, so the model answering you can be one you pay
somebody else for or one running on your own machine.

### Two more providers

**OpenAI**, over the Responses API, and **any OpenAI-compatible endpoint**, over Chat Completions —
Ollama, LM Studio, vLLM, llama.cpp, or a gateway at its own address. Both hand-rolled over HTTP and
server-sent events rather than built on a vendor SDK: half of this exists to talk to implementations
that get details wrong, and a strongly-typed client is exactly where tolerance for one goes to die.

**Two entries rather than one you retarget**, because what leaves your machine is written per
provider and no single sentence can say both *everything goes to OpenAI* and *nothing leaves this
machine*. It splits the key as well, which is right on its own terms — an OpenRouter key is not an
OpenAI key.

**The protocol split falls on the same line.** Server-side web search is now a named entry in the
tools array wherever it exists, and it lives on Responses; Chat Completions is where every local
server lives, and no local server has a search anyway. So switching provider keeps the capability
instead of watching it go dark.

### A model on your own machine

**The key is optional now, and that was a change rather than a setting.** It used to be derived from
whether a key row existed at all, so a local server with no account was unreachable by construction
rather than because anybody decided it should be. The row is still there — a gateway may want one —
and it says which of the two it is.

**A loopback endpoint is priced at zero and says why.** An unknown model stays unpriced, which is
the honest answer; a model served from your own machine is not unknown, it is free, and reporting
"unknown" about it forever is noise pretending to be rigour.

**And the disclosure says nothing leaves this machine** — the first time the honest answer to *what
is leaving* has been *nothing*. One judgement behind both, so a turn cannot be disclosed as private
and billed as remote. It reads the address and never resolves it: a hostname that happens to point
here today is treated as remote and disclosed in full.

### Asking the endpoint what it can do

Changing the endpoint empties the model list, and Directive 47 now **asks the endpoint what it
serves** and fills the list back in with its own answer. Verifying a key does the same thing rather
than spending a turn — it works with no key and no model chosen, which are the two configurations
this release exists to reach.

**What a model list cannot answer is learned from the first refusal.** Whether tool calls work,
whether reasoning effort is a field it knows, whether it will report usage at all: every request
offers everything, and an endpoint that refuses and names the field has that one capability switched
off for that address while the turn is sent again without it. You see an answer, not an error.

**Once per capability per address, and never written down.** A client hunting for a request shape
the server will accept is indistinguishable from an outage, and a demotion saved to a file outlives
the server upgrade that fixed it.

### Token accounting stopped being one provider's arithmetic

**Anthropic's input count excludes what was cached; OpenAI's includes it.** The same sum then counts
those tokens twice, on every cached turn, and produces a number plausible enough that nothing would
ever have reported it. Converted once, at the seam.

**The published rates were read rather than remembered**, which caught two things that had moved.
OpenAI bills cache writes now — 1.25x uncached input from the GPT-5.6 family, reported as their own
count — so the input total includes the written part as well as the cached one. And the older models
discount cache reads by half rather than by nine tenths, so those rates are per model now instead of
derived from one provider's terms.

**A turn whose provider reported no usage is unpriced rather than free.** Streamed Chat Completions
sends no usage at all unless asked, and plenty of servers do not send one even when asked — a
session reported as free when it was paid for is worse than one that admits it does not know.

**The cold-prefix regression detector no longer means different things depending on who answered.**
It watched for a cache *write*, which is one provider's evidence: on an endpoint that never reports
one it would have sat at zero and read as caching being perfect rather than as the instrument not
measuring. Turns nobody could measure are now counted separately from turns that measured fine.

---

## 0.28.0 — 2026-08-18 — Engineers

list.md Phase 28. A tab that answers who to go and get next, and shows you why it thinks so.

### Where every engineer is

`Engineers.tsv` gains coordinates, generated rather than hand-written. `get_distance` computes the
same figure correctly and is a network call — so ranking thirty-eight people through one every time
a plan changes is a tab that is useless in flight and unusable offline. With the coordinates
shipped, distance is arithmetic.

**The second source was already being parsed and thrown away.** EDDiscovery's `EngineeringInfo`
carries three coordinates the generator read and discarded on every run since the referral chain
arrived. They are now agreed against spansh's, exactly, to within one step of Elite's 1/32 ly grid;
a wider difference is not rounding, and the run refuses to write the table rather than ship a
distance nobody can check. All 38 placed, all 38 agreed, and `--corpus` confirmed 31 of them
against Frontier's own `StarPos`.

Your own position needed one thing the plan did not mention: **the location had no coordinates at
all.** It has them now, folded from the three events that carry a `StarPos` — `Location`, `FSDJump`
and `CarrierJump`, which carried one on all 9,332 occurrences across a 912-journal corpus — and
pointedly not from `Docked`, which names your system, states no position, and would otherwise make
your position go unknown the moment you landed.

### Who can roll this

The directory is sorted by **what you can act on today** — within reach, then already yours, then
behind somebody else — rather than alphabetically or by speciality, because the question is nearly
always *who can I go and get*. Every row carries how far away they are and how many jumps that is
in the ship you are actually flying.

**Your journal wins over the table wherever the two meet.** An invitation already extended is a
referral that has already happened, so an engineer who has invited you is within reach whatever the
chain says about who has to recommend them.

The line at the top carries the count that belongs to the Loadout tab: **how many of your plans are
waiting on somebody you have not unlocked.** A plan blocked on a person is not a plan blocked on
materials, and the gap analysis cannot tell you which it is.

Getting that count right needed a correction to how "who can roll this" is read: **per grade, from
the blueprint table**. Six engineers offer Increased FSD Range at grade 3 and three of them at
grade 5, so deriving it from an engineer's top grade sends you to Professor Palin for a grade 5
drive he does not roll.

### The fastest way in

A solver rather than a display. Walking one referral chain is exact and cheap and answers the wrong
question — a blueprint usually lists several engineers, and one unlock covers many plans. So the
best next unlock is the one that satisfies the most of what you have planned, not the shortest
chain.

**One unit, and it is jumps.** Distance converts at the range of the ship you are flying, and each
stop on a chain carries its own leg, so a long chain of short hops and one long haul are compared
on the same scale rather than balanced by a tuning constant. **Colonia needs no rule of its own**:
22,000 light years is hundreds of jumps and swamps any step count, and because the distance is
measured from where you are standing, being in Colonia flips it automatically.

**What is not a trip is not turned into one.** A tribute of fifty units is a shopping run to a
system you know and is already inside the leg that reaches it; a combat rank is not a trip at all,
so it is printed in Frontier's own words, breaks ties, and never becomes a number. Distance stays
the primary key deliberately — making "you can just go and do it" a class above it would put an
already-invited Colonia engineer ahead of one in the Bubble.

**The whole working is on the page.** One step, 131 ly, about 5 jumps, and one planned thing
covered — then the stops, what each invitation asks for and what it buys. A ranking nobody can
inspect is an oracle, and when it is wrong, or when you would rather go elsewhere anyway, you
cannot tell a bad answer from a bug.

**The route promotes as a chain rather than a line**: one checklist item per stop, in flying order,
each carrying the grade that stop actually needs. A single line reading "unlock Broo Tarquin" hides
two engineers and two rank climbs behind a tick you can never make progress on. A rank climb with
somebody you already have is ranked as a way in too — grade 5 wanted from an engineer held at grade
2 is as much in the way as a stranger.

One row in the table argues with the table. **Yi Shen's own meeting text reads as three referrals
where the directory reads any one of three.** The directory's reading is kept and the disagreement
is answered by printing Frontier's sentence under the chain, which is what showing the work is for.

### Elsewhere

The advertised tool surface did not grow: `get_engineer_route` and `promote_engineer_route` are
both `Protected`, reachable from the panel and from a phrase and never from the model. "Who should
I unlock next" is a fixed question with no free-text argument in it, which is exactly the shape the
keyword router handles with no round trip at all.

---

## 0.27.0 — 2026-08-18 — Suits and weapons, and the gap

list.md Phase 27. The Loadout tab is finished: what you wear beside what you fly, and the
arithmetic between everything you have planned and what you are carrying.

### The same page, on foot

Suits and weapons is the Ships page, instantiated against your suit and weapon plans. One index,
one drill, one promote path, one say-line on every level — **one page kind built once and shown
twice**, in the same spirit as one widget tree rendering to two surfaces. It stays a **second mode
rather than a second tab**, because the game separates ship and on-foot hard and so does its
vocabulary, but nothing about the layout is redrawn.

**What differs is the shape of the thing being planned**, and one part of that had to be invented
rather than read. A hull's slot is a place a module goes and the journal names it. An item on foot
has a grade and up to four modification slots, and **Elite names none of them** — it reports what
is fitted as a *set* with no positions in it. So the slots are `Grade`, then `Mod 1` to `Mod 4`,
the numbers are Directive 47's own, and the page says so where you could otherwise read the third
one as the game's third.

**The grade is the first slot, and that is a routing fact rather than a preference.** A grade 1
item has no modification slots at all, and an engineer's base has no Pioneer Supplies — only an
Apex desk. So the plan is ordered the way the trips have to happen.

Everything the ship builds established holds here: the plan owns *what* and your checklist owns
*when*, a suit you do not own is **intended** rather than absent, buying one adopts the plan rather
than making you re-point it, and dropping a plan keeps whatever it already put on your list.

One correction to the plan of record, and it is the mirror image of Phase 26's. On the ship side,
`ShipyardBuy` carries no id for the new hull and the adoption had to match the `ShipyardNew`
written after it. **On foot the buy event carries the id**: `BuySuit` carries `SuitID` and
`BuyWeapon` carries `SuitModuleID`, so adoption is one event rather than two. The symbol is what it
matches on, never `Name_Localised` — Frontier's own localisation reports every suit above grade 1
as Class1.

### Gap analysis

A third mode reading across both the others, because a Commander gathering materials does not care
which ship wanted them. **Not called a wishlist**: a wishlist is a list of things you want, which
is what the plans are. This is the subtraction.

**The ledgers are never totalled together.** Raw, manufactured and encoded materials, the ship
locker and the cargo hold have separate caps and no exchange between them; a single headline number
over the top of them would be a lie. The one figure that spans everything counts **units still to
find**, which is a shopping list rather than a balance.

**A shortfall reads back to what wants it** — the ships and the slots that asked. That is what
makes the roll-up navigable instead of merely a total, and it is why each planned slot is costed on
its own and merged afterwards: the totals are identical either way, and it is the only arrangement
in which the attribution survives.

**Material trading is included and stays secondary.** The trader's rate is exact — one grade down
returns 3 for 1, one grade up costs 6 for 1, and a different line costs a further 6× — and the line
is the trader's grid column, never the journal's category. So a trade appears beside the shortfall
and never instead of it, and only one you could actually make out of a genuine surplus is offered.

**Whether hulls you do not own count is a filter**, not a decision taken once on your behalf.
Counting them is honest about the whole ambition; excluding them answers what can be finished now.
Both are real questions, and which one you are asking changes through the evening.

It is a different set from `get_plan_shortfall`, which nets what is on your **checklist**. This one
nets what is **planned**, including builds that have never been promoted — which is most of them
while a build is still being decided.

### Also

- The advertised tool surface got **smaller** again, from 39,693 bytes to 39,639 against the same
  40,000 ceiling. Every tool this phase adds is Protected — reachable by phrase and by press, and
  advertised to nothing — and `plan_on_foot_build`, which stays the one route that needs a model to
  read free English, now writes the plan instead of proposing straight to your checklist.
- Suit and weapon plans live in `data/on-foot.json`, hand-editable and live without a restart. A
  slot that is not `Grade` or `Mod 1` to `Mod 4` is refused and reported, because a hardpoint on a
  suit is a line that could otherwise be stored, shown, and never promotable.

---

## 0.26.1 — 2026-08-18 — Saying so when it cannot look things up

Two faults with one root: `SupportsWebSearch` was consulted everywhere search is *used* and
nowhere it is *explained*. Found while working out what happens on an endpoint that has no search,
which is what a custom endpoint generally is.

### The egress disclosure described searches that could not happen

Point `llm.endpoint` at a gateway and the Web search row still read **active**. It named the
destination, said your provider was running searches and reading pages on your behalf, and quoted
about a penny each — while `SupportsWebSearch` guaranteed no search would ever be made from there.
The row had three states and needed four: off, no usable model, **no search at this endpoint**,
and active.

Over-reporting egress is the safe direction to be wrong in. It is still wrong. A disclosure is
only worth reading if it describes this machine, these settings, right now, and this one described
a transfer that could not occur.

### Directive 47 now says why it cannot look something up

Ask about patch notes, or what other Commanders are reporting, on an endpoint with no search — or
with the row simply switched off — and the model was told nothing. It was not offered the search
tool and had no idea why, so it answered from memory, and an answer from memory reads exactly like
an answer that was checked.

It is told now, and told which half is missing, because the two have different answers:

- **The row is off** — you can turn it on.
- **The endpoint offers no search** — Anthropic's own endpoint offers one; a gateway or a local
  model may not.

The endpoint wins when both apply. Being told to flip a switch that will not help is worse than
being told nothing: you flip it, nothing changes, and the next explanation is one you have a
reason to distrust.

Nothing is said at all when search works, so having it on costs no words about it.

---

## 0.26.0 — 2026-08-18 — Ships

list.md Phase 26. The Loadout tab has its first surface: your fleet, the hulls you intend to buy,
and one build per ship.

### A plan is keyed to its slot

**Changing your mind about a slot is an edit, not a delete and an add.** Swapping a long-range
pulse laser for an overcharged multi-cannon leaves you with the same third hardpoint on the same
hull, and with whatever history it had.

The old rule keyed an item on what it wanted — the blueprint and the grade — which was right for
as long as items were regenerated from scratch on every evaluation, and does not survive a plan you
edit. Before this, the first time you changed your mind about a slot, everything that slot had been
through was tombstoned and an identical-looking new item opened beside the corpse. It needs no
counter and no tombstone bookkeeping, because a slot exists as long as the hull does.

Only for the three intents that are shaped like a slot: a ship slot holds one module, that module
carries at most one blueprint, and it carries at most one experimental effect. A suit takes several
modifications and a construction site wants several commodities, so those still key on what they
are about.

### The plan owns what. The checklist owns when.

Ships keeps its own store, in `data/ships.json`, and **nothing crosses into your checklist
unasked**. Planning a slot writes the build and stops; promoting offers it, and accepting is still
your own act.

That separation is what lets you rearrange a build without your checklist reordering itself under
you, and reorder your checklist without the build forgetting what you decided. **Promotion is
one-to-many** — one planned change produces the modification plus whatever unlocking and ranking it
needs — and **dropping a build keeps what it already put on your list**, because you ordered your
list around those lines.

### The fleet, and the fleet you intend

**A hull you do not own is not in the fleet.** It has no ship id, because Elite's id is what a ship
list is keyed by, so **acquiring the hull is the plan's first step** rather than a precondition
sitting outside it.

**Buying one adopts the plan rather than making you re-point it.** A build's identity is
independent of the ship id from the moment it is created, which is what there is to rebind.

One correction to the plan of record: list.md said `ShipyardBuy` names the hull and the new id.
Measured against real journals it does not — it carries the hull, the price and the ship being
stored, and **no id for the new one at all**. The id arrives in the `ShipyardNew` written
immediately after. Matching on the event that actually carries it is the difference between
adopting a plan and offering to adopt it onto nothing.

### Fleet, ship, slot

Three levels of the drill stack, so the breadcrumb, the reflow and the way back all come from
Phase 25.

The slot list is an **index rather than a table** — one line each, a mark where a plan exists, and
everything else in the pane that opens — which is what lets one layout survive from 512 to 2048
logical pixels.

**Fitted and planned are two blocks and never one merged line**, because a plan is a second thing
you want rather than an edit to the truth. A plan carries the journal's verdict and no checkbox,
and a ship you are not flying says so: Elite reports the loadout of the ship you are sitting in and
no other, so the page says that rather than showing a blank that implies disagreement.

Cost is per plan and on the slot, with held, needed and short all three.

### One build per ship

Comparing a combat fit against an exploration fit for the same hull is a planner feature this
deliberately does not have, and slot identity is what makes that a decision rather than an
accident.

### Also

- The advertised tool surface got **smaller**, not bigger. The largest profile was measured at
  39,840 bytes against a 40,000 byte ceiling before this phase, so Ships advertises nothing at all:
  the one route that needs a model to understand free English is `plan_ship_build`, which already
  existed and now writes to the build rather than proposing straight to the checklist. Everything
  else is a phrase or a press. It ships at 39,693.

---

## 0.25.0 — 2026-08-17 — The panel becomes a place, and it knows what time it is

list.md Phases 24 and 25. Two phases in one release: the panel grew a bar of six surfaces, and the
first of the new ones — clocks, timers and alarms — landed with it.

### The bar

Conversation, Technical and the log file were three tabs. They are three **modes of one Transcript
tab** now, because they are three readings of one exchange rather than three destinations — and
that collapse is what paid for the surfaces beside it without the bar growing:

```text
Transcript   Checklist   Loadout   Engineers   Utilities   Settings
```

Five in the headset. Settings stays out for the reason it always has: a 1180-pixel navigation
column at a metre is a wall rather than a page. A tab whose surface has not been built yet is not
drawn at all, so Loadout and Engineers arrive when they arrive.

The mode control rides the tab strip's own row rather than sitting under it — two stacked strips
read worse than the eight flat tabs the collapse avoids. The log keeps its working indicator,
because it alone of the three reads a file off disk.

### Drilling in, and finding the way back

Every surface below the bar is a stack, and **the tab is its root rather than its first step**. So
pressing the tab you are already on goes back to the top of it — which had to be built rather than
inherited, because the tabs are radio buttons and re-pressing a checked one announces nothing at
all.

Below the root there is a **breadcrumb**, which is both where you are and the way back: a headset
has no title bar to orient by, and losing your place is expensive when you cannot glance at a
second monitor. Every crumb but the last can be pressed **and said**. Back is three routes that
agree — the crumb, the **grip button** on either controller, and the phrase.

Voice jumps levels, so a trail materialises with everything above it rather than being built one
step at a time. Drill state survives switching tabs, and a tab with more than one mode keeps a
stack per mode.

### One design, one to three panes

Drilling in and reflowing turned out to be the same mechanism: **how many panes fit**. Wide shows
the level you are on beside the one above it, and a third if there is room; narrow shows one and
you drill. One design covers the headset's big panel, its mini panel, the desktop window and every
zoom level.

The big panel's pixels are a setting now, on a ladder that holds one aspect — so asking for more of
them never changes the shape of the thing in the room. Three levers, kept apart: **pixels decide
what the image can hold, metres decide how big it looks, zoom decides how much layout those pixels
carry.**

### Choosing takes the panel

A chooser **replaces the panel until it is dismissed**, which makes it a level of the stack rather
than a pop-up over the page. A pop-up cannot exist on the headset surface at all, but taking the
panel earns its place anyway: it fits about sixteen rows at a comfortable size where a layer fits
fewer, and it **carries what you are choosing for in its header** — the slot, its size, what is
fitted now — which a drop-down has nowhere to put. Short lists stay as a layer, and which one a
control gets is fixed per control rather than decided by how many rows it happens to have today.

### Saying it, or typing it

Text entry is **voice first with a drawn keyboard as the fallback**, and which opens depends on
what is being entered — a system name is far easier said than typed, and a number is the reverse.
What you say reaches the box **once, when it is done**. The keyboard comes back on its own for the
three failures Directive 47 can actually detect: nothing heard, a transcription it was not sure of,
and a value that is not a thing. Whisper reports that confidence now, as the worst segment of the
sentence rather than the average, so one badly-heard system name among clear English is caught
rather than buried.

### The checklist left its window

The headline is not tidiness: **a window cannot appear in the headset**, so a Commander wearing one
could not see their checklist at all. It is a tab now, on both surfaces.

One list in your own order, with the scope on each line rather than the page carved into a list per
ship — what you reorder on a whim is everything you are working on. **Press a line to select it and
it grows a pair of arrows**; a drag is the worst gesture available to a laser pointer at a metre and
has no spoken form at all. Each state says what to do next rather than showing a badge, and colour
is spent only where something is actually wrong. Suggestions wait on a page of their own instead of
interrupting the list.

### Clocks, timers and alarms

Elite runs **1286 years ahead**, so the Utilities tab shows today twice — one instant presented
twice, which is why the two can never disagree.

Timers and alarms are named, set from the tab or by saying so, and **the name is what Directive 47
says when one goes off** — one rising chime for all of them, and the sentence after it says which.
**Alarms survive a restart and timers do not**, and one that came round while Directive 47 was
closed is reported afterwards with when it was due rather than sounded hours late as though nothing
had happened.

"What's the date" is answered by Directive 47 itself — no turn, no provider, no tokens, and it
works with no key configured. Both dates also ride along with every conversation already worked
out, so the ship's AI can mention the date without asking and is never asked to add 1286 to
anything.

**Cancelling is yours.** Reachable from the tab and from a phrase, and from nowhere the AI can
reach — an alarm somebody relies on to leave the house is worth that line being drawn.

### Also

- The About dialog links to this file on GitHub.

---

## 0.24.0 — 2026-08-17 — Systems worth remarking on, and the panel stopped flickering

list.md Phase 23. Jump into somewhere with a story attached and Directive 47 says so — once a day
per system, and never twice because you came back for fuel. The plan of record, including the four
calls settled before any code, is
[docs/plans/phase-23-systems-worth-remarking-on.md](docs/plans/phase-23-systems-worth-remarking-on.md).

### Twenty systems it ships knowing about

Sol, Hutton Orbital's ninety-minute supercruise, Founders World, the Old Worlds, Jameson's wreck on
HIP 12099 1 B, the first Thargoid barnacle in Merope, the first Guardian ruins in Synuefe XR-H
d11-102, the Zurara derelict at the end of the Formidine Rift, Jaques Station's misjump, and the
long way out — the Great Annihilator, Eta Carinae, Sagittarius A*, Explorer's Anchorage, Beagle
Point.

The selection is the maintainer's own compilation rather than a community list copied across; the
facts in it are Frontier's, as `NOTICE` says. `tools/gen-lore.py` resolves every system's address
through two independent services that have to agree, which caught two bad rows before they shipped:
one system that had been renamed, and one that never existed.

### Once a day, and the number was measured rather than picked

Across 913 real journals, **30.1% of all 7,966 jumps re-enter a system visited within the last
day** — so without the rule nearly a third of arrivals would be a repeat. Stretching the window to
a week suppresses only 4.2 points more, because 88% of repeat visits happen inside the first day.
The stamps are absolute and survive a restart, so logging off somewhere and coming back an hour
later is quiet.

A carrier jump counts as arriving.

### Then, if you want it, a web search

Set **Remark on arrival** to *Remark, and look it up* and the bare fact is followed by whatever a
search turns up, spoken as a search result and never in the table's flat voice. It needs web search
switched on and an endpoint that offers one — and if it cannot search, the first sentence says so
rather than leaving you waiting for a second that never comes. A result that lands after you have
jumped again is dropped. Nothing a search returns is ever written into a table.

**This is the first search Directive 47 runs without being asked a question**, so the Privacy page's
web-search row now says as much. It opens no new destination: the provider does the searching, on
the connection that already carries your turns.

### Your own notes, and three sentences that stay apart

**Settings → Lore → Your own notes** is where you tell Directive 47 about a system it does not
know. It searches first and records what happened, and what you get back later depends on which:

```text
Earth is here.                                              ← the shipped table
You added this one, and the search agreed at the time: …    ← corroborated
You told me: …                                              ← your word
I wrote this one down myself, and nothing has checked it: … ← D47's own tool call
```

**Nothing is ever promoted.** Surviving a lookup is a label, not a verdict — an obscure but real
site finds nothing, and a search can appear to agree with something a model half-invented.

Directive 47 may write a note itself, which is a departure from how the checklist and the callouts
are handled: adding one presses no key and silences no warning, so it is answered with a label
rather than a lock. An entry the model wrote says so out loud every time, whatever the turn looked
like from the inside — which is the honest reading, since a turn steered by a hostile in-game
message is indistinguishable from one you asked for. Writing a note is limited to the system you
are in, because a note is keyed on its address and addresses come out of your journal.

One file for the installation, in `data/lore.json`, editable by hand — changes are noticed by
comparing the contents, so an edit is never missed.

### Also

`spike/CorpusReplay` could not run while Elite was running: its second pass opened journals
unshared, so the file the game holds open threw after the soak had already reported clean. Fixed —
a gate that only runs with the game closed is a gate that gets skipped.

### Two more, reported against 0.23.1

The first is a regression, and a narrower form of it had already been fixed once in 0.22.2 —
which is the interesting part of it.

### The VR panel flickered constantly

0.22.2 fixed a flicker that happened *while the panel was being carried*, and the cause was that
carrying marked the surface dirty on every frame: the serving loop re-rendered the widget tree,
converted it and handed the whole image back to SteamVR thirty times a second for pixels that had
not changed.

0.23.1 added the aiming highlight — the scrollbar that lights up when a ray rests on it — and put
it on exactly the same footing. `Aim` is called on every tick of a live session, including the
ordinary case of no ray anywhere near the panel, and it set the dirty flag unconditionally every
time. So the condition that used to hold only during a carry now held always, and the flicker went
from something you could provoke to something that never stopped.

`Illuminate` already knew whether anything had actually changed — it returns early when the lit
control is the one already lit — it just did not say so. It returns that answer now, and the
surface is marked dirty only when the light really moved. The or-assignment matters: a frame where
the light did not move may still be dirty for some other reason, and aiming is not the place that
clears it.

`RestingAimDoesNotAskForARedraw` holds the line. It drives thirty ticks of nobody pointing at the
panel and asserts the surface stays clean, then asserts that finding a bar and leaving it are both
still drawn — a fix that simply stopped setting the flag would pass the first half and leave a
highlight that never appears. Reintroducing the fault fails it at the first assertion, and the six
tests that were already there all still pass with the fault in place, which is why nothing caught
this the first time.

### Voices over the radio

Three things at once, all of them the same complaint from different angles: a treated line still
sounded like it was in the room.

**More link.** The passband was 300–3,400 Hz, which is the *telephony* band — a telephone is a
wire in a building. It is 400–2,700 Hz now, which is the SSB voice channel. The top edge is the
one that does most of the work: presence lives between roughly 2 and 5 kHz, so that is where a
voice decides whether it sounds like it is in the cockpit. Consonants are still resolved at 2.7
kHz, which is why real voice channels stop about there rather than lower. Drive went 1.9 → 2.6, so
the link runs further out of headroom.

**More static.** The floor under the words went 0.022 → 0.034 and the bare carrier 0.085 → 0.148.
Part of that is not an increase at all: a narrower band throws away noise power along with
presence, and that alone cost 3 dB, so holding the shipped level would have been a reduction. The
tail lands near −31 dBFS against the −33 it shipped at.

**One level for everybody.** This is the one that changes a rule. The treatment used to restore
each clip to the loudness it arrived with, so that an effect could never read as a volume change.
That is right for one line and wrong across many — it faithfully preserves whatever level spread
the speech provider produced, so a voice that is simply hotter than its neighbours arrives hotter.
Matching each line to itself cannot fix a difference that is *between* lines. There is a receiver
AGC now: every transmission is brought to one target, and a 26 dB spread going in comes out inside
1 dB. The target is 0.10 RMS, which is not a round number but the measured one — real Edge Neural
output was recorded at about −20 dBFS when the static was set by ear, and −20 dBFS is 0.10. So the
average voice comes out exactly where it already did and only the spread around it collapses,
which is what keeps this a levelling rather than the volume change the old rule warned about.

The bounds are deliberately asymmetric. Boost stops at four times, because bringing near-silence
up to a speaking level means multiplying the noise floor by whatever it takes; cutting has no such
hazard, and twelve dB of it was not enough to bring a hot voice down to meet a quiet one.

`ALineOverTheRadioIsNoLouderOrQuieterThanOneInTheRoom` asserted the old rule and is gone. Two
assertions replace it: every transmission arrives at the same loudness whichever voice sent it,
and a line already at a typical level is not moved. With the old behaviour put back, 25.8 dB of
spread survives the first of them.

---

## 0.23.1 — 2026-08-17 — The headset was showing the oldest lines, and a dropdown crashed it

Everything reported against 0.23.0 in one pass with a headset on. Two of them were the headset
path doing something nobody could have guessed from the outside.

### The panel was showing the top of the transcript, not the bottom

It sat at scroll offset zero for a whole session: the oldest 405 pixels of a 3,271-pixel
transcript. Following scrolls to the end of the extent the scroll viewer knows about and calls
`UpdateLayout` first to make that extent current — which, on a window that is never shown, does
nothing at all. So it scrolled to the end of an extent equal to the viewport, which is the top.

The jump-to-latest control never appeared either, because you were never behind. That is what was
reported; the rest of it was silent. Following is re-asserted between the surface's layout pass
and its rasterise now, which is the one moment it has a real extent.

### Pressing a dropdown took the app down

A stack overflow, and it is why a popup cannot be the answer in a headset. A popup asks the
platform for a top level of its own; the panel's window has never been shown, so there is nothing
for one to hang off, and opening one does not fail politely — `IsDropDownOpen = true` exits at
`0xC00000FD` before any dispatcher work, with no exception and nothing in the log. Forcing it into
the window's own overlay layer, which is the documented way to keep a popup inside its parent, is
the same crash.

So D47 draws its own. A layer over the page, in the tree that is already laid out, drawn and
hit-tested every frame: pressing a combo box offers its items as rows, and **pressing a text box
offers a keyboard** — there is no other way to fill one from inside a headset. Both are ordinary
controls, so the same ray presses them and a long list scrolls on the same draggable bar. What is
typed reaches the box once, on Done: a settings row commits what it is handed, and a system name
committed letter by letter would be eleven wrong values on the way to the right one.

The searchable pickers still open a real window and are still left alone rather than opening a
dialog on a desktop nobody is looking at.

### Scrollbars answer a controller

The nearest vertical bar within 28 surface pixels of the ray takes the press — far wider than the
bar, because a hand at arm's length is not accurate to a dozen pixels. Position along the bar is
position in the document rather than a relative drag, so there is no thumb to catch and letting go
does not jump. It lights up when aimed at.

### "Show the VR panel" now shows it

It reached `get_headset_status`, that being the only headset-shaped tool on the surface, and was
answered with *"the overlays are dark, Commander"* — true, and not what was asked.
`show_in_headset` acts; the status tool says it only reports.

### Captions, and what they are

Two lines now rather than three, matching the per-event maximum that the broadcast and streaming
specs both set. The other numbers are those specs': 42 characters a line, 20 characters a second
reading speed, a dwell floored at 5/6 s and ceilinged at 7 s. The dwell was the real complaint and
it is fixed: it was timed against the last sentence alone, so a short one cleared the whole window
in five sixths of a second. It is timed against everything still on screen now.

Re-voiced in-game messages are no longer captioned at all. They are already on the comms page, and
a station approach produces a steady stream of them.

### Smaller things

A material holding no longer runs past what the game will hold — *"109 of 100"* is a number Elite
cannot produce, and a Commander reading it learns only that D47 is wrong. A snapshot is still
believed as written.

The mini panel's text is a quarter larger: 512 pixels across the same 0.34 m rather than 640. Its
height does not shrink with it, because that left the transcript pane with no room for the tail.

The carrier's tower and captain address the owner by name. They are the Commander's own crew on
the Commander's own ship, and *"Welcome back, Commander"* is how a stranger at a starport talks.

The SRV line no longer claims the ship is holding position *behind* you, which depends on which
way you have driven and is in no journal event. The Copy button is vertically centred.

### Reported and not reproduced

An in-game message heard inside the cockpit with no radio colour. The announce path puts every
role that is not the ship's AI or the crew through the link, and there is now a test that says so
end to end. What 0.23.0 could not tell you is which way a given line went, so the spoken-voice log
line ends with `in the room` or `over the air`.

---

## 0.23.0 — 2026-08-17 — Nineteen from hand-testing, and three things that had never once worked

Two batches in one release, because neither had shipped: five wanted changes to the settings
surface, and fourteen raised in a pass with a headset on. Three of the fourteen were features
that stopped working, or never worked, without leaving a trace anywhere.

## What was reported with a headset on

### Auto-honk never fired, not once

It waits for the ship to be out of the witchspace tunnel before pressing the fire button, and it
waited for **normal space**. The far end of a hyperspace jump is supercruise. A Commander who
arrives is in supercruise for the whole thirty-second window the arm is good for, so the arm
expired every time and the trigger was never pressed.

Every test of it modelled normal space, which is why nine of them passed while the feature did
nothing. The hold is 5.3 seconds now, and the primary fire group is offered while flying rather
than in normal space only.

### The panel in the headset was a photograph

Both headset surfaces are rasterised out of a window that is constructed and never shown, and
nothing runs a layout pass in one. A control that changes marks itself and queues itself with the
layout manager rather than marking its ancestors, so `Measure` on the root returned without ever
descending and whatever had changed was never laid out.

The panel therefore drew the transcript its model held when the data context was set, and nothing
after it — worse than stale, because rebuilt text has no size, so the line that *was* showing
vanished on the next append and left an empty page under a tab strip that still lit up.

Captions had the same shape plus one of their own: an `ItemsControl` in such a window does not
regenerate its containers when its source is replaced, so the first caption drew and every one
after it drew an empty box. The three lines are one text block now.

### Captions were also doubled

The audio arbiter reports everything audible as one snapshot, re-raised for every change to any of
it — so a second sentence being queued behind the first re-reported the first sentence's caption,
and it went onto the screen twice. Clips carry an id now and the caption layer ignores a repeat of
the one it is already showing.

### The panel in the headset can be pressed, and it has Settings

One trigger does both jobs, because a second action would change a manifest SteamVR caches under an
application key and discard whatever binding the Commander had made. A press that has neither dwelt
400 ms nor travelled a twentieth of the panel is a press; anything else is the carry it always was.
Starting the carry on the button going down is why every attempt to press a control moved the whole
quad instead.

The Settings tab is no longer withheld from the quad. The reasoning was a nav column beside a
700-pixel minimum, and that surface has collapsed its nav below 900 since Phase 12.

### The settings nav pointed at the wrong section

The scroll-spy added the card column's own position to the card's, and a scroll viewer scrolls by
arranging its content at a *negative* offset — so the test "has this card's head passed the top
edge" meant "is its top less than twice the scroll". At the fourth section the nav named the
seventh, and past the sixth it sat on the last one for the rest of the page. Not a zoom problem: it
reproduces identically at 100%, 125% and 175%.

### ElevenLabs stopped speaking German

A material milestone — *"Adaptive Encryptors Capture at 75 percent. 88 of 100."* — came back with
the tail in German. A bare numeral is the one token in a sentence that carries no language, and
Multilingual 2 decides the language from the sentence.

Numerals are now spelled out on the way to the synthesiser, and the model is `eleven_turbo_v2_5`
with the language pinned to English, because Multilingual 2 rejects that parameter outright. What
you read is unchanged: the transcript and the captions keep the digits. The list price halves with
the model, and the spend counter asks the provider what it actually sent.

### Materials went quiet after a trader visit

The milestone tracker only ever counted up. Filling a material at Jameson's Crash Site set its
highest announced milestone to 100, and emptying it at a materials trader left that 100 in place —
so every later collection found no threshold it had not already passed, and that material was
silent for the rest of the session. A restart is what cleared it, because the tracker is in memory.
Fill and empty a few at one trader stop and most of what a Commander is gathering goes quiet at
once.

It follows the holding down now, and it does that on every tick against the inventory rather than
when something is picked up: spending a material raises no `MaterialCollected`, so a tracker that
only looked then would next see the count already on its way back up and could not tell that apart
from its never having moved. Only downwards — a material that fills from a mission reward is not a
milestone anybody gathered. Across the 912-journal corpus this is 953 more announcements from the
same 1,110 trades.

### Two callouts that said nothing

Elite announces which channel you have joined every time you drop out of hyperspace — 8,833 of them
across the journal corpus, second in volume only to station traffic, and a fact you can read off
the system name in front of you. It is no longer re-voiced.

*"Ray Gateway offers engineering"* is gone for the same reason: 3,726 of the 3,759 dockings in the
corpus advertise the service, and all 33 that do not are construction depots. Inverting it, which
is what the report suggested, would have said nothing about a construction site instead.

### Ship AI callouts are on the page now

A fuel warning was heard once and was afterwards findable only in the log file. `Announcement`'s own
documentation claimed every callout "already reaches the transcript by the route everything D47 says
reaches it"; nothing did. They land on Conversation, and therefore on Technical.

### A maximised window comes back on its own monitor

The flag was saved and applied and nothing asserted the applying. What was missing is the screen:
the remembered position is the *restored* rectangle and has to stay that way, so a window maximised
on the second monitor had nothing recording it. The window is put back on that screen before it is
maximised, because Windows maximises to whichever monitor a window is already on.

### Every NPC in a system had the same voice, and none of them matched the name

Voices are assigned per sender name and held for as long as the Commander is in that system, and
they have been since Phase 11 — what was wrong is the pool they were drawn from. The filter that
keeps a wingmate from being voiced in a language you do not speak read ElevenLabs' *accent* label
as though it were a locale, so "american", "british" and "multilingual" were all discarded for not
starting with `en`. Measured on a real account: **473 voices offered, one eligible.** Every named
NPC shared it, and the per-name assignment beside it had nothing left to assign.

A tag is only filtered on when it is a language tag now. The line that says how big the pool is
gives both numbers, because "1 available" is unremarkable on its own and alarming beside
"473 offered".

A woman on the radio also sounds like one. **Elite records no sex for anybody** — 914 journals,
696,000 events, 221 event kinds, and not one carries a gender field, so this cannot be read out of
what the game states and no amount of reading the journals will make it readable. The names are
there, though, so d47 ships a list of 692 given names that read as a woman's and gives those
senders a voice the provider has tagged as one. Everything it does not recognise takes a man's,
which is the safe half of the error in this material: of the forty commonest names it does not
match, every one is Mark, John, Paul, Andrew, David, Michael — or not a personal name at all.
A guess from a name's ending was not on offer; it makes Joshua and Luca women and leaves Ingrid
and Meg men.

The list is short and gets longer: `tools/scan-npc-names.py` reads the set out of the source it
ships in and reports what is still unmatched, most heard first. Running out of women's voices
makes two women share one rather than handing either a man's.

### The log says which voice spoke

Every line D47 speaks — a turn's reply, a callout, a re-voiced message, a crew member, a core's
introduction — is built through one pipeline, so one line there records the speaker and the voice
id once per utterance. Once, not per sentence: a six-sentence reply is one voice, and six identical
lines would bury the one that differs. A voice the provider refused is not recorded as having
spoken, because it did not.

### Smaller things

The egress disclosure — five lines of prose between rows that are one line each — is a tooltip on
its row rather than a paragraph on the page, and it still follows the selected provider. The search
box is body size rather than a step down from it: a field you type into is not supporting text.

**Not changed:** disabling **Wait between attempts** under logarithmic backoff. It does have an
effect there — the first retry waits exactly the base under either shape — so the row stays live.

## Five wanted changes to the settings surface

All five raised hand-testing 0.21.x, none of them a defect. Four are about the two places a
Commander spends the most time on that surface: the search box, and the picker that casts a voice.

### The search finds a section by its own name

Typing "Speech" found rows and not the card called **Speech**, so a search for a section's own name
looked like it had found nothing at the top of the thing it was looking for. The name is now marked
where it is written — in the card's heading and in the nav item — and a named section keeps every
row it has rather than only the ones that happen to repeat the word, because naming a section is a
Commander asking to be taken to it.

"Audio mixer" is the case that shows why: nothing inside that card says "audio mixer", so it used
to answer a search for its own name by emptying itself and then disappearing for being empty.

### **Verify Key** is shut until a key has been typed

It was offered on an empty box, where the only answer it could give was that an empty key is not a
valid one — and it was offered there for as long as a key was stored, which is most of the time.

It now lights up when you paste something and says why while it is shut. Pressing it stores what
you typed and *then* checks it, which is the only honest thing it can do: the check is a real call
made against the store, so on an unsaved paste it would otherwise have been answering about the key
you had just replaced.

### The ElevenLabs key sits beside the provider that needs it

It was at the foot of the Speech card, below sixteen rows about rates, cues, retries and egress —
so selecting ElevenLabs asked for a key, and the box to put one in was off the bottom of the
screen. Selecting a provider is what makes its key relevant, so the row that answers that choice is
now directly beneath the one that made it. Which key rows exist and when they appear is unchanged.

### Auditioning a voice is a glyph on the row

**Hear it (about $0.013)** was a button with a disclosure written on it. Every voice in the list now
carries a play glyph at the right of its row, which becomes a stop square while that voice is
talking and cuts it off when pressed again.

The price did not go with the button. It is a sentence above the list — *"Play a voice to hear it.
Each one costs about $0.013"* — and the same words are on the pointer over every glyph, because a
cost you have to hover to discover is a cost discovered afterwards on the bill. With no provider
selected, or a paid one with no key, the glyphs are shut and that line says which it is.

### Clicking a voice highlights it rather than choosing it

The picker committed and closed on a single click, so there was no way to look at the list — and no
row for a play glyph to live on, since a row that dismisses the window when touched cannot hold a
control. A click now highlights; **Use this**, Enter or a double-click takes it, and Cancel and
Escape are unchanged.

This overturns the reasoning that stood in `PickerWindow`: a command palette is a means of getting
at one known answer and should commit on the first click, but a list of four hundred voices is a
list to be examined.

Fixed on the way past: rebuilding the rows on each keystroke would have cost the picker its
highlight the moment it opened — a list holds its selection by object, and a text box raises
`TextChanged` as its template applies. The rows are built once and filtered.

---

## 0.22.2 — 2026-08-17 — The panel stops flickering while you carry it

0.22.1 got the trigger arriving and the panel moving. This is what carrying it then showed.

### It flickered, and only while being carried

Two causes, and a sibling project had already found both.

**The panel was redrawn from scratch on every frame of a carry.** Moving it marked the surface
dirty, which is what makes the app re-render the widget tree, convert it, and hand the whole image
back to SteamVR — thirty times a second, for pixels that had not changed. It bought nothing: where
the panel goes is worked out fresh every frame anyway and never consulted that flag. Carrying no
longer touches it.

**And the image was being drawn into memory the runtime might still be reading.** `SetOverlayRaw`
is not documented as copying before it returns, and d47 kept one buffer and rewrote it in place.
With a panel that repaints a few times a second that is a race nothing ever loses; while one is
being carried the uploads come every tick and it starts to show. There are now four buffers in
rotation, so the frame the compositor was just handed is left alone until three more have been
drawn.

### Picking up a head-locked panel

Grabbing a head-locked panel is supposed to make it world-locked, since carrying it somewhere is a
Commander saying where they want it. That setting write could be refused, and the refusal was
discarded — so the panel would carry perfectly, spring back to the head on release, and nothing
anywhere would say why. It now says so in the log.

Next to it, a real ordering fault: the frame a panel is picked up sets the carry and turns the lock
to world without yet writing down where the panel is, so the *next* frame saw a world-locked panel
that had never been placed and helpfully placed it — at knee height, for one frame, before the hand
took it back. Nothing places a panel that is already in somebody's hand now.

### Underneath

`spike/GrabSpike` drives the real runtime, action input and ray maths without the rasteriser, and
prints what each controller is actually doing. It exists because the reported symptoms — a ray that
does not follow the hand, a trigger that may or may not be bound — are all invisible from this side
of the headset, and guessing at them from the code had already been wrong once.

---

## 0.22.1 — 2026-08-17 — The panel can be picked up, and it tilts at your eyes

Two defects in the headset, one of which had been shipped as fixed and was not.

### The grab was fixed against the wrong channel

0.16.2 reported that the VR panel could not be picked up because the two flags that make an
overlay interactive were called by nothing, and called them. That was true and it was not the
fault. Those flags opt the quad in to **SteamVR's own laser**, and SteamVR only runs that laser
over its own dashboard — so with Elite holding the headset, the event queue they unlock returns
nothing, forever, with no error anywhere. It works perfectly with the game closed, which is what
made three separate implementations believe it worked at all.

The trigger now comes from `IVRInput`, which does not depend on SteamVR pointing at anything. The
reason two earlier attempts at that concluded it was impossible is one step whose absence is
silent: **an application has to register itself**. SteamVR files bindings under an application
key, and a process it does not recognise has none, so there is nothing for a binding to attach to
— the manifest loads, the handles resolve, and the action stays bound to nothing forever. It does
not appear under Manage Controller Bindings either, so it cannot be fixed by hand, and the only
place that says any of this is `vrserver.txt`.

Three more things in the same chain, each of which fails the same quiet way: the action set is
activated at overlay priority, or it loses to the running game and receives nothing; it is claimed
only while a ray is on the panel or a carry is running, or it takes the controllers hostage from
Virtual Desktop and the dashboard for the whole session; and the manifest declares `oculus_touch`
**and** `rift`, because the Oculus driver asks for each in turn and one missing binding disables
input entirely.

**A controller does not point where it says it does.** OpenVR reports the grip pose, inside the
handle. On Touch controllers the tip is off from it by a large angle, so the ray was landing
nowhere near the laser coming out of the Commander's hand. The correction is read out of the
render model rather than hardcoded, so it is right for whatever controller is plugged in.

**And there is something to aim with.** Losing SteamVR's laser means losing the only thing that
said where you were pointing, so d47 draws its own: a beam that lights as your hand comes near the
panel and stops exactly on the cursor when it is on it, and a cursor on the point itself. Both are
their own overlays, and both fail soft — no beam and no cursor is a panel that can still be
carried, just unguided.

### The panel tilted away from you

A head-locked panel took a fixed tilt from settings, hand-tuned to 12°. A fixed angle can only
suit one distance and one drop, and there are two panels with two of each: mini wanted 18.4° and
got 12. It is now worked out from where the panel actually sits, and the setting is a trim on top
of that — a file already on disk has its old value converted, exactly, so nothing moves that a
Commander had set deliberately.

Underneath that was a worse one. **The resting placement had its pitch inverted**, so a
world-locked panel dropped to knee height turned its face at the *floor* — through twice the angle
it should have gone the other way. An overlay's visible side looks along its own +Z and a positive
rotation carries that downwards, which is written down correctly in `architecture.md` and was not
what the code did. The test that should have caught it measured the panel's *back* and agreed with
the bug; assertions here are now on the direction a face ends up pointing, because the angle is the
right size either way.

### Still unconfirmed

None of the OpenVR side can be checked without a headset, and this release does not pretend
otherwise. The manifest's shape, the ray arithmetic and the beam and cursor geometry are covered
by tests. Whether SteamVR actually binds the trigger is a question only a Commander in a headset
can answer, and `vrserver.txt` is where it says no.

---

## 0.22.0 — 2026-08-17 — In-game comms arrive over a radio, not from the next seat

Four wanted changes about re-voiced messages, all of them the same complaint from different
angles: Phase 11 gave every sender their own voice, and a voice on its own turned out not to be
enough to say *where somebody is*.

### Nobody says "says" any more

**An NPC message is read as the words alone.** The preamble was written for people, and Elite's
NPC traffic mostly is not people — in the 912-journal corpus the two commonest senders are
`$ShipName_Police_Independent;` and a station's name. So what a Commander actually heard on an
approach was "ShipName Police Independent says" in front of a three-word transmission, several
times a minute. **A Commander keeps their name**, because in wing chat that is the one thing the
voice cannot tell you: which of the three of them it was.

Nothing is lost — the sender moved to the page, where there is no voice to carry it.

One thing fixed on the way past: 8821 of the corpus's `ReceiveText` events have an empty sender,
not a missing one, and those were being read aloud as " says: Entered Channel: Cakutsi".

### Comms are on the Technical page

**In-game messages now appear in the transcript**, on the Technical page, labelled with who sent
them. They used to reach the synthesiser and nothing else, so a message that arrived while the
Commander was looking away was gone. Not the conversation page: a station clearing you to dock is
not part of a conversation with your companion, and on a station approach there are a lot of them.

Written before it is spoken, and whether or not the speaking works — a message that could not be
synthesised still arrived.

### Only the ship's AI and the crew are in the room

**Everything else is put through a comms link.** A station, a police interceptor, another
Commander, the fleet carrier and its tower all arrive over the air; the persona aboard and the
crew hired at a station do not. It is a 300 Hz–3.4 kHz band-pass, a saturator, and a noise floor
that comes up when the words stop and drops the link a fifth of a second later.

Three properties are held deliberately, because each of them is how an effect like this reads as a
bug instead:

- **The level does not change.** A treated line comes back at exactly the loudness it arrived at,
  so the Commander's one speech volume still means one thing.
- **The static does not step between sentences.** A reply is one clip per sentence, and the floor
  is added after the level match rather than before the filters — otherwise a loud sentence and a
  quiet one in the same transmission come back with different noise floors.
- **It never clips.** Where matching the level would push a peak past full scale, the peak wins.

The two levels of static and the length of the tail were set by listening to real Edge Neural
output, over two passes; the first was reported as sounding like the clip had simply ended.

### An NPC's voice is theirs while you are in the system

The stickiness itself already worked — an NPC keeps one voice until the Commander jumps out, and
another Commander keeps theirs for the session. Two things were wrong underneath it:

- **The pool could hand out the ship AI's own voice.** Hearing d47's voice arrive from a pirate,
  through a radio, is worse than either of those alone. No sender is now given a voice that
  already belongs to somebody aboard.
- **The crew turned over on every jump.** Their assignments shared the per-system table with the
  NPCs, so the gunner hired at a station changed voice on each hyperspace jump and could collide
  with a passing pirate. They are aboard, so their voices last the session.

---

## 0.21.1 — 2026-08-17 — Three log-level rows that controlled nothing

**Turning the Voice, Input or LLM log level up or down did nothing at all.** The row accepted
the change and read it back correctly; the code it named went on logging at whatever the
default said. Found while building the Technical page in 0.21.0, not by anyone hitting it —
which is the trouble with it, because there was no symptom to hit. Nothing warned, nothing
failed, and the setting looked like it had worked.

Each subsystem was bound to the namespace its code lives in, and three of the eight named
namespaces that do not exist anywhere in Directive 47. A binding that matches nothing is simply
never applied.

The real cause was the shape rather than the spelling: a subsystem is not one namespace. **The
speech loop alone spans six**, across four projects — what drives it, what it captures and
plays, what decides when to listen, and the three speech providers underneath. None of that fits
in a single name, so the single name was wrong.

Two more rows were quietly incomplete for the same reason and are now whole:

- **VR** reached the SteamVR runtime but not the placement arithmetic or the headset surfaces,
  so turning it down left two thirds of it talking.
- **Input** reaches key injection, the bindings it reads and the HOTAS switches together.

Where two subsystems both cover a piece of code — the app's own row covers everything on the
surface, including the speech pipeline — the more specific one wins, so the rows stay
independent of each other rather than one quietly shadowing the other.

---

## 0.21.0 — 2026-08-17 — Ten wanted changes, and none of them defects

Everything raised hand-testing 0.15.0 on 2026-08-16. None of it was broken; all of it was
Directive 47 saying too much in one place, too little in another, or forgetting between
launches something it had no reason to forget.

### Fewer words, and a reason for each of them

- **The microphone indicator stopped leading with the alarming half.** It used to read
  *Microphone open, nothing kept* — true, and a strange first thing to read at a glance beside
  a running game. The three states are now **PTT Ready**, **Listening...** and **MIC ON**. The
  first two name the mode outright because those states only ever happen in one of them; the
  open gate is reached both ways — a held key and a gate D47 opened for itself are the same
  fact about the microphone — so it claims neither.
- **The Settings search now marks what it found.** It has always taken rows away, which is
  right for 92 rows across 14 sections, but a page of survivors with nothing marked left you
  comparing the query against every word to work out what it caught. The hits are now
  highlighted in the rows that remain. And a row matched on its **settings key** — which the
  search has always read and the page has never shown — now displays that key underneath, so a
  row can no longer survive with every visible word on it disagreeing with what you typed.
- **The search box has a × in it.** Inside the field rather than beside it, so it reads as part
  of the box rather than as a fourth button next to Copy and the steppers, and it appears only
  once there is something to clear. It runs the same path Escape does, so the page comes back
  rather than just the box going empty.
- **The API key row lost three buttons.** *Show* is now an eye inside the field, struck through
  while the key is legible. *Store* reads **Save** when nothing is stored and **Overwrite** when
  something is, so replacing a key says so before you press it. *Check* is now **Verify Key**.
  And *Clear* is an undo arrow inside the field — which asks first, because with a key stored it
  deletes a credential you may have to reissue at the provider, and an undo arrow on its own
  promises something reversible. A stored key is still never shown back to you; the eye reveals
  only what you are pasting in.

### Two things it stops telling you twice

Both of these were Directive 47 forgetting something between launches that it had no reason to
forget, and repeating itself as a result.

- **A core's opening line is now spent for good.** Each of the eleven introduces itself the
  first time you ever pick it, and reacts to the gap every time after that — but restarting
  wiped the slate, so the whole cast opened with their first lines again on every launch.
  Which cores are spent is now remembered. **Forget introductions** is the way back, and is now
  the only one; the row said a restart would do it, and that is no longer true. Nothing you
  said to a core is stored — only which ones have spoken. Transcripts are still per session and
  still cleared when Directive 47 closes.
- **The ask box stops teaching you once you have asked.** Its placeholder carried a worked
  example — *try "where am I" or "what's your status"* — and went on carrying it for as long as
  you used Directive 47, because nothing remembered that you had ever asked anything. Ask
  something, by voice or by typing, and it settles down to **Ask D47 something**. It does not
  come back.

Both are kept in `data/view-state.json`, beside the window position and the collapsed cards,
rather than in your settings — nothing here is configured, and losing the file costs one
repeated hint rather than a broken install.

### What this has cost, for longer than a session

The line under the panel used to carry eleven numbers: the outcome, the route, the effort, three
token counts, the turn's cost, the session's cost, a cache-regression counter, a character count
and a voice price. All of it true, none of it readable at a glance beside a running game.

- **The line says what a glance is asking.** What answered, at what effort, and what it cost —
  and nothing else.
- **Details, beside it, opens the rest.** Tokens in and out and how many were cached, what the
  session has come to, what the voices have cost, and the cold-prefix counter that matters when
  something is defeating prompt caching and is noise the rest of the time.
- **And four running totals: the last 7 days, the last 30 days, this week, and this calendar
  month.** None of which Directive 47 could answer before, because nothing was written down —
  both cost trackers were in memory and started empty at every launch, so the only honest figure
  was "this session".

Charges now go to `data/spend.jsonl` as they happen, one line each, and are read back at
startup. **The voices are in it too**: a month figure covering only what the model cost would
look authoritative while leaving out half of what you spent.

Each row records the instant it happened in UTC, and "this week" and "this month" are worked out
against your own clock when you ask. That is what keeps them right across a daylight saving
change — and right if you ask from a timezone you were not in when the charges were made.

Anything Directive 47 could not price — a model with no published rate, a voice provider whose
rate you have not set — is recorded with its tokens or characters and no dollar figure, and any
window holding one says **at least** rather than quietly reporting part of the cost as all of it.

The file is only appended to, so nothing already written is at risk from a later crash, and a
half-written last line costs that line rather than the history. Delete it and the totals start
again from empty; nothing else changes.

### The headset panel gets out of the way

What Directive 47 put in front of you the first time you wore a headset was a 1.1 m panel, a
metre away, a quarter of a metre below your eyeline, **following your gaze** — a large bright
rectangle over whatever you turned to look at. Every part of that was adjustable and none of it
was a good place to start.

- **Mini is the default panel.** The same panel showing less, at 0.34 m instead of 1.1 m. Full
  is one setting away and keeps its own placement, so switching between them does not cost you
  the position you set for either.
- **Panels are world-locked by default**, and **Directive 47 now puts the panel down for you**
  the first time it runs in your headset. Roughly a metre ahead of wherever you are facing, low
  enough that the top of it sits around knee height, and tilted back so it faces you rather than
  the ceiling. Glance down and it is there; look up and it is not in the way.
- That first position is **worked out rather than assumed**. The floor comes from your room
  setup and the panel's height from its own width and proportions, so it is not a figure picked
  for somebody else's height or somebody else's panel size — and it stays right if you change
  the width.

Move it once and it is yours: placing it writes the position exactly as putting it down always
did, and Directive 47 never places it again.

**Only for a fresh install.** Every setting you have is written to `settings.json`, so if you
already have one you keep the panel exactly where and how you had it. These are the values
Directive 47 starts from, not ones it imposes on a layout you have already arranged.

### The Technical page shows the speech loop

**Technical** is described as the conversation with the diagnostics left in, and it showed almost
none of them: five things ever wrote to it, all about the turn as a whole. Everything the speech
loop reported — the microphone opening, your words being transcribed, the answer being worked out
and spoken — went to a log file instead. The information existed; it was on the wrong page.

```text
[21:04:07] Microphone open, listening.
[21:04:09] Turning what you said into words.
[21:04:10] Working on an answer.
[21:04:12] Speaking the answer.
```

Each stage is a line that **stays**, so when something stops part-way, how far it got is still on
the page above it. The microphone indicator beside the ask box answers the other question — what
is true right now — and this one answers what happened.

**Errors from the speech path arrive here too**, with the cause attached rather than only the
sentence:

```text
[error] Could not start capture — device in use by another application
```

Errors only, and only from speech. Warnings and the rest of the running commentary stay in the
log file, because a page that repeats another page is one nobody reads.

### "Set focus to game" brings Elite forward

Directive 47 will not press a key unless Elite is the window in front — the one thing standing
between a voice command and typing into your browser. The awkward consequence was that
alt-tabbing away switched every flight command off, and the only way back was the mouse you were
trying not to reach for.

- **"Set focus to game"** — or *"focus the game"*, *"switch to Elite"*, *"back to the game"* —
  brings Elite forward. It needs no model configured; it goes through the spoken command path
  rather than the language model.
- **The model cannot do this.** Your journal, in-game messages, search results and INARA are all
  untrusted text, and anything the model can call, a hostile in-game message can try to invoke.
  A message that could yank your focus while you were typing is a nuisance at best, so this is
  reachable by spoken phrase only.

**Windows may refuse, and Directive 47 will tell you when it does.** A program that does not
already hold the foreground cannot take it — it can only ask, and what usually happens instead
is the taskbar button flashing. So this works when you ask from Directive 47's own window and is
often refused when you ask from somewhere else. There is no way around that which does not
involve faking keyboard input at the operating system, which is the thing Directive 47 promises
never to do outside Elite. What it does instead is say so:

```text
Windows would not let me bring Elite forward from the background. Its taskbar button should be
flashing; click that, or alt-tab.
```

Silence there would read exactly like the microphone having failed, and you would repeat
yourself at a Directive 47 that had heard you perfectly.

One phrase is deliberately missing. **"Elite" on its own is not a command**, because spoken
phrases are matched before the model sees them — a bare "Elite" would swallow *"what is my Elite
rank in combat"* and answer it by moving a window. Elite is the top rank in every career the game
has.

---

## 0.16.2 — 2026-08-17 — Four defects

Four bugs, all of them found by Commanders using the thing rather than by a test.

- **A question that made d47 use one of its own tools failed, and kept failing.** "Place the
  VR panel here" came back as *I couldn't reach the model after 3 tries*, and so did the next
  question, and the one after that. Anything that needed a tool was dead for as long as you
  were in the game; only a restart appeared to help, and it did not. The live game state d47
  attaches to your question was overwriting the result of the tool it had just run, which
  makes the request one the model's own service refuses. Your typed words were also arriving
  wrapped in a pair of quote marks nobody typed.
- **The Settings search left the page filtered.** Search Settings, switch to another tab and
  come back, and only the sections that had matched were still listed — with an empty search
  box above them and nothing you could type to bring the rest back.
- **The Settings section list was blank until you scrolled.** All eighteen sections were
  there, in text with no colour, which is text that does not draw. The first scroll painted
  them.
- **The VR panel could not be picked up with a motion controller.** Grab-to-move was written
  and never switched on: nothing asked SteamVR to point a laser at the panel, so no press ever
  reached it. Captions stay untouchable on purpose — a laser that stops on a label is a label
  in the way of everything behind it.

Nothing here touches the network.

## 0.16.1 — 2026-08-17 — HOTAS switches, published

*The content of 0.16.0, which is a tag with no Release behind it. Identical but for the
publish path it was tagged to correct, and it went out with no release notes: the section
below is 0.16.0's and this version had none of its own to find.*

---

## 0.16.0 — 2026-08-16 — HOTAS switches

*The tag exists; no Release was built from it. The publish path still named a target framework
that Phase 21 had moved, so the workflow failed after the tag was pushed — and a published tag
never moves, so the correction costs a version number. This content reaches Commanders as the
next patch.*

Phase 21. A switch on your panel now means a **state** rather than a keypress. Flip it and d47
asks Elite whether it is already in that state, and presses your binding only if it is not — so
gear already down and the switch moved to down does nothing at all. Every remapper is
edge-triggered and blind: it sees the flip, sends the bind, and is wrong the first time the game
changes its own mind on docking, a relog or a voice command. Between flips nothing is touched,
so voice, the game's own automation and your switches never fight.

- You assign a switch by **walking** it — move it to each position in turn, pausing at each.
  That is the only way to learn how many positions it has and which button each one holds;
  Windows reports "HID-compliant game controller" for every device on earth. A spring-return
  switch or a hat is declined with the reason, and so is a walk that cannot be made sense of.
- Ten actions, being the ones Elite reports the state of: landing gear, lights, cargo scoop,
  hardpoints, flight assist, silent running, analysis mode and the three SRV controls. An action
  the game does not report cannot be asked "are you already there", so it is not assignable.
- If something else is bound to the same action, d47 notices the fight and pauses that switch
  rather than wrestling it. If a mapping no longer fits its device — turning 4x32 mode on
  renumbers every button on a throttle — it asks to be reassigned instead of pressing whatever
  now sits at that index.
- The panel and the headset both show which switches currently disagree with the game, beside
  the microphone indicator.
- The published exe grows 6.4 MB for the Windows SDK projection that reads controllers with no
  driver, no window and no elevation. `--selftest` now checks it loads, because that is the one
  thing that can fail only in a published build.

Nothing here touches the network.

## 0.15.0 — 2026-08-16 — On-foot engineering

Phase 20. d47 can see what you are wearing, price what upgrading or modifying it costs, say who
does it and where the materials come from, and plan a suit or a weapon on the same checklist
substrate the ship and colonisation plans use.

**The headline is a correction rather than a feature.** Every on-foot quantity the community
sources publish predates the patch that cut them — modifications by half, grade upgrades by two
thirds, with Power Regulators removed outright — so d47 restates them to what the game actually
charges, measured against 16 real upgrade events and four locker deltas. Every other tool will
quote you two to three times these numbers.

- The ship-locker cap is 1,000 per **category**, not per item.
- The Bartender's exchange rate is exact arithmetic.
- The credit cost of a grade is the item's base price times 4, 15, 30 or 50.

Nothing here touches the network.

## 0.14.0 — 2026-08-16 — Session tooling and release polish

Phase 19. Nine items: the voices, the log surfaces, and the published documentation.

- **Your voice choices survive a provider switch.** Selecting ElevenLabs to hear what it sounds
  like and going back to Edge used to cost you the ship AI's voice, both carrier voices and all
  eleven per-core pairings. They are now filed under the provider they belong to and put back
  when you return, pairing flag included.
- **You can hear a voice before you cast one.** "Hear it" speaks the highlighted voice without
  closing the dialog or choosing anything, using the core's own opening line rather than a
  generic sample. It ducks the game, the shut-up key cuts it off, and on a paid provider the
  price is on the button before you press it.
- **What the voices cost sits beside what the model costs**, so "what has this session cost" has
  one answer. The unit is characters, because that is what speech is billed in. Characters are a
  fact and dollars are an assumption, so the count is always shown and the rate is a row you can
  correct.
- **An empty voice list says which empty it is** — no key stored, a key refused, a provider
  unreachable, or an account that genuinely holds none. Two of the four are yours to fix.
- **The transcript stops fighting you.** Scroll up and it stays put, with a floating "↓ Newest"
  to come back. Copy takes the whole page as shown.
- The documentation site has a left nav grouped by section. Nine pages had been reachable only
  by knowing the URL; a test now says so if it happens again.
- Under it: AppHost's speech decisions moved into Core where tests can reach them, which turned
  up a fifth fault on the way — the key check reported "accepted the key" for a key that had
  just been refused.

## 0.13.0 — 2026-08-16 — Activity assistants

Phase 18. Seven items, all about the thing a Commander is actually doing rather than about d47
itself: reading a system's name offline, exobiology from both ends, colonisation from both ends,
and callouts in the ring.

- **Finding somewhere worth colonising** closes the phase. Frontier's rule is a nearby
  unpopulated system within 15 ly, and both halves are checkable — but a *claim* is not. It
  lasts 24 hours, produces one journal line on one machine, and appears in no index anywhere, so
  every answer says so and none ever says "available".
- **Colonisation and construction tracking** is subtraction over the journal and nothing else.
  The depot event is a snapshot, several sites can be open at once, and every figure carries "as
  of your last visit". The carrier's cargo is a tonnage with no manifest, which is refused out
  loud rather than guessed.
- **Exobiology** ships as two halves answering different questions from different sources: a
  plotted circuit through biology somebody has already found, which names species and quotes
  money, and the Commander's own scan, which names only the genus because that is all the game
  says.
- **Prospector and core callouts ignore Elite's own Material Content grade**, which measures a
  different thing entirely — 45% of the rocks holding a material at 40% or better are graded
  Low.
- Three measurements changed shipped code rather than only new code. The population filter on
  `search_systems` had been offered since Phase 14 and never done anything.

## 0.12.0 — 2026-08-16 — Checklists, and the plans that write into them

Phases 16 and 17, since 16 shipped untagged. One list of what you are working on — your own
lines, your ship builds and your construction sites, on one surface. Your own lines you tick;
computed ones follow your journal and refuse to be ticked, because the next read would either
undo it or leave it standing and wrong. Finishing is not removing: done items stay, below the
line, counted. Changing a plan is a diff rather than a rebuild, so a fortnight of progress
survives changing one weapon.

## 0.11.0 — 2026-08-15 — Warnings that arrive in time

Phase 15. d47 warns you about an attack before it lands, and tells you when you are flying
exposed in a rival Power's space.

**Announced attacks.** NPCs say what they are about to do before they do it, and d47 listens for
it — a median of six seconds before the first shot, which is enough to boost, deploy hardpoints
or high-wake. Three situations, each with its own line and its own sound, so you can tell an
interdiction from a cargo demand from a bounty hunter before the sentence has finished. Measured
over 912 real journals, and so are the ones it stays quiet about.

## 0.10.0 — 2026-08-15 — Community goals

The rest of Phase 14's engineering half. What is running, what tier it has reached and where you
stand, read from your own journal — and, with an Inara API key, the goals running where you have
not been. An expired goal says when it ended rather than reading like a live one.

Also: what engineering actually does and what a roll costs; where materials are and what a
trader would give for what you hold; the engineer referral chain, priced; a state filter on
galaxy search; and web search, whose results stay a sentence rather than becoming a table.

## 0.9.0 — 2026-08-14 — Tool calling, and the first look at the galaxy

**d47 can use its own tools now.** Until this release the model was sent no tool definitions at
all and nothing executed a reply that asked for one, so every capability was reachable only by a
phrase somebody had written down in advance. That was fine while the tools were reports and
flight commands. It stopped being fine the moment a tool needed an argument you spoke: "how far
is Colonia" had nowhere to go.

A turn is now several requests when it needs to be, the results come back to the model, and
every round is billed and reported rather than the last one being priced as though it were the
whole question. The galaxy search is the first thing d47 answers from off this machine.

## 0.8.0 — 2026-08-14 — Hands-free listening

Phase 13. The microphone can open itself: when you start talking, or only when you say the
ship's AI by name. WebRTC AEC3 subtracts d47's own voice from what it hears, so on speakers you
can talk over it instead of waiting for it to finish — it consumes the arbiter's render
reference tap rather than a loopback capture.

Voice activity is an energy detector over an adaptive noise floor, so its one setting is a
margin above whatever your room is rather than a fixed loudness. The wake word matches words
rather than audio, which is why it is the name you already call your ship's AI, and it renames
itself when you switch core.

## 0.7.0 — 2026-08-14 — Soundscape

Phase 12. Settings is a page of the one window rather than a second window to lose behind it.
The tab you are looking at can be searched: the transcript pages highlight and step, Settings
filters. Anything that might take a moment says so on the affordance you touched.

The audio half grows a mixer — a level, a mute and a duck for every kind of sound d47 makes — a
drop-in folder at `data/audio` for your own cues and beds, situational ambient music on a
background layer of its own, and a rescan that picks all of it up while d47 is running without
ever cutting a clip that is already playing.

## 0.6.5 — 2026-08-14

d47 says it is listening, in both a cue and a face that had shipped without ever being entered.
The error banner can be dismissed. A repair replaces the voice it takes away rather than leaving
a core mute. Ambient remarks are in seconds and default to 45, route progress to every 3 jumps,
and a long jump to 30 seconds in hyperspace. Privacy and egress moves to the bottom of Settings.

## 0.6.4 — 2026-08-14

The ship's voice follows the core aboard: it was bound at startup and never re-read, so every
core spoke in whichever one was aboard at launch. The named-default repair is gated on a
revision, so a corrected repair reaches the files the broken one stamped.

## 0.6.3 — 2026-08-14

A named default is taken off every other core, not only the one it moves onto — so a voice
chosen by hand no longer leaves a second core holding it.

## 0.6.2 — 2026-08-14

Warden takes George whatever an ElevenLabs account calls him, and files that ended up otherwise
are put right once. Clearing the Voice row is the way back to the voice d47 chose for a core,
and says so.

## 0.6.1 — 2026-08-14

A core written as a man is no longer cast in a woman's voice: gender is stated to the voice
pairing rather than described to it, enforced on the answer, and a pairing already written is
repaired once.

## 0.6.0 — 2026-08-14 — Heard on first run

Push-to-talk on right shift, the speech model fetched automatically, and settings defaults that
say what they are.

## 0.5.18 — 2026-08-14

Push-to-talk no longer types into the panel, the Ask box is not focused by default, "can you
hear me?" answers the question, the system default microphone names itself, and a persona's
first words reach the conversation.

## 0.5.17 — 2026-08-14

A microphone that is sending no audio says so and names itself, rather than reporting nothing
intelligible.

## 0.5.16 — 2026-08-14

A real installer: `d47-setup.exe`, per-user and unelevated, with a proper Add/Remove Programs
entry. The portable zip is still published and is still what the in-app updater fetches.

## 0.5.15 — 2026-08-13

**Push-to-talk stops losing most of every utterance.** The capture buffer was padding real speech
with manufactured silence, and Whisper was transcribing what little survived.

## 0.5.14 — 2026-08-13

**Whisper natives ship beside the exe; transcription works in a published build for the first
time.** The release is now `d47.zip`, the updater swaps the whole set with rollback, and
`d47.exe --selftest` gates CI and every release.

## 0.5.13 — 2026-08-13

Choosing a speech model downloads it, where you chose it.

## 0.5.12 — 2026-08-13

A highlighted tab on arrival, and a bound on concurrent speech.

## 0.5.11 — 2026-08-13

The speech model offer is on the panel, where you are looking.

## 0.5.10 — 2026-08-13

One type scale, and the panel minding its manners.

## 0.5.9 — 2026-08-13

Three pages for the transcript.

## 0.5.8 — 2026-08-13

Two fixes, and a question written down.

## 0.5.7 — 2026-08-13

**The headset overlays are visible.**

## 0.5.6 — 2026-08-13

Keep the first frame the headset was handed.

## 0.5.5 — 2026-08-13

The head-locked panel rides the headset.

## 0.5.4 — 2026-08-13

Ask SteamVR what it is holding.

## 0.5.3 — 2026-08-13

The headset panels stop sorting with the dashboard.

## 0.5.2 — 2026-08-13

The headset path can say what SteamVR turned down.

## 0.5.1 — 2026-08-13

Switching voice provider no longer leaves d47 unable to speak.

## 0.5.0 — 2026-08-13 — Persona and voices

Phase 11. Eleven Guardian cores, each remembering you separately, so switching core is switching
who you are talking to rather than repainting the same conversation. The guardrails sit above
the persona in the prompt, so turning personality off cannot strip them.

A second voice provider: ElevenLabs alongside Edge Neural, with more than one voice to give it,
and a failure that says what ElevenLabs said rather than what its status code suggested. The
people in the fighter bay get a name and a voice of their own. And there is a face on the panel,
the same one in the headset, drawn from the one widget tree like everything else.

## 0.4.1 — 2026-08-13

A turn no longer dies at the first word it speaks.

## 0.4.0 — 2026-08-13 — Acting on the game

Phase 10. d47 can press keys in Elite: flight and navigation, ship systems, panels and fire
groups, the SRV, the clipboard, galaxy map course plotting and Elite's chat. Named macros the
Commander wrote, run by name.

**Actions are offered only when they work** — resolved against the Commander's own bindings, so
a stick-only action is never advertised, and gated by mode, so nothing is offered that the
current flight state would ignore. Asking for something unreachable gets a spoken reason rather
than silence.

**Autonomous actions** — anything that fires on a journal event with nobody asking — are their
own category with their own consent. Each is off by default and enabled on its own row. The
arrival honk is the first.

## 0.3.9 — 2026-08-13

Acting on the game, and a hotkey the page had wrong. (Same build as 0.4.0, which renumbers it as
the minor release a completed phase earns.)

## 0.3.8 — 2026-08-13

The settings window fits the screen and remembers how you left it.

## 0.3.7 — 2026-08-13

The coverage record knows how it went, and shows the list.

## 0.3.6 — 2026-08-12

Zoom, the speech model, and the keys.

## 0.3.5 — 2026-08-12

The settings surface, read as a Commander.

## 0.3.4 — 2026-08-12

The language model card, and the voice list.

## 0.3.3 — 2026-08-12

The help glyph matches the gear.

## 0.3.2 — 2026-08-12

A help button, and the app calls itself D47.

## 0.3.1 — 2026-08-12

Directive 47 no longer launches SteamVR.

## 0.3.0 — 2026-08-12 — VR

Phase 9. **Directive 47 renders in the headset.** Captions over Elite through OpenVR on their
own unmovable, output-only layer following the Netflix CC standard; the panel itself in VR,
head-locked or world-locked per surface, with a Commander-triggered re-anchor because Elite's
in-game recenter moves the cockpit without telling SteamVR. Order agnostic: SteamVR, Elite and
Directive 47 can start in any order. Opacity, curvature, distance, size and scale are
configurable per surface, and everything keeps working with the desktop window minimised.

The desktop half too: the window opens at a size that fits the screen it appears on and
remembers where you left it, and the panel zooms with Ctrl and the scroll wheel, Ctrl+plus,
Ctrl+minus and Ctrl+0. Also the first-run Start Menu offer, one instance at a time, the About
dialog, and the version in the title bar.

*A completed phase is a minor release from here on; that rule entered CLAUDE.md with this tag.*

## 0.2.4 — 2026-08-12

Findable, and only one of it.

## 0.2.3 — 2026-08-12

A title bar that says what it is, and only one of it.

## 0.2.2 — 2026-08-12

Update now actually updates.

## 0.2.1 — 2026-08-12

Three defects found testing 0.2.0, all on the push-to-talk path. Binding a key for the first
time crashed the app — and the key was saved before the crash, so it came back bound and looked
like it had worked. Unbinding it froze the app hard enough to need Task Manager: the capture
thread had been in an endless loop since the first buffer of audio, which is also why held-key
utterances were reported as thousands of seconds long. Selecting a speech model collapsed that
row's help text to one character per line.

## 0.2.0 — 2026-08-12 — Speaking, listening, knowing the game, and speaking up

Phases 5 through 8. d47 talks, hears, knows where you are and what you fly, and warns you about
danger, fuel and the route without being asked.

- **Speaking** — one audio arbiter, Edge Neural TTS, sentence-chunked so speech starts at the
  first sentence boundary, loop-state cues, a thinking bed, and an instant stop reachable by
  voice and by a system-wide hotkey.
- **Listening** — continuous capture with push-to-talk as one gate policy over it, local Whisper
  transcription, journal-derived proper-noun biasing, and a double-bind check against the
  Commander's real Elite bindings. Speech models are download-on-demand with explicit consent
  and their own egress disclosure.
- **Knowing the game** — ship loadout and metrics, fleet carrier, stored ships, materials,
  on-foot inventory and a session summary, plus live situational awareness attached to every
  turn.
- **Speaking up** — interdiction, shields, hull, heat and cargo warnings; the
  unscoopable-next-star case that actually strands a Commander; route progress with neutron and
  white-dwarf hazards; arrivals; and material milestones from a derived grade table.

Unsigned, with a published SHA-256.

## 0.1.0 — 2026-08-12 — Foundation

Phases 1 and 2. The solution, projects and CI/release workflows; the capability checklist,
architecture and persona pack; the journal spine; and an update check on start. The
Avalonia → D3D11 shared texture → `IVROverlay` spike ran here and its findings were written back
into architecture.md before any VR work began.
