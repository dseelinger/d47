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

Open defects live in [bugs.md](bugs.md), and wanted changes in
[docs/plans/change-requests.md](docs/plans/change-requests.md). An entry leaves either file when
it ships, and the line it gets here is its permanent record.

---

## 0.38.1 — 2026-08-19 — The mining missile is a mining missile

**Remediation 15 items 2a, 3, 5, 7 and 14.** Item 2a led it, because the wrong rows were in the
table a Commander already has; the other four are small, independent and were ready alongside it.

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

### Four smaller things in the Loadout tab and the Persona card

**A searchable chooser takes the keyboard when it appears.** Reported as *"I should not have to
click it"*. Every searchable chooser now focuses its search box, not only the module one — the same
line the text-entry prompt has always carried, and safe for the same two reasons: nothing in the
headset sends a keystroke, and the panel swallows the push-to-talk key before any control sees it.

**A question with one answer is not asked.** Reported against Life Support: *"there is only one
choice, it can't be anything else"* — and the page drew two rows anyway, "Anything — I only want the
engineering" and the single answer. The module step and the grade step now take a lone option and
move on. For a socket that accepts one type, *anything* and *the one thing it takes* are the same
want, so the plan records the module instead of leaving it blank, and the line reads properly rather
than opening with a bare grade.

The experimental-effect step deliberately keeps asking. Its decline is **"No effect"**, which the
single effect does not satisfy — those are opposite wants, not the same one, and taking it would put
an experimental on the plan nobody asked for.

**"Point Defence is not currently engineered."** That verdict is a reading taken at a moment, not a
property of the module: the plan carries the journal's verdict with its date, and it ships as still
to do. *Is not engineered* reads as a fact about Point Defence; the only thing Directive 47 knows is
a fact about right now. It is in Core, so it reads that way aloud as well.

**A hull is named even when Frontier does not name it.** Reported as *"Oxen, a type9_military is not
bound to a core"*. Elite does not always send a localised ship name, and the Type-10 Defender is one
it omits — its internal symbol is `type9_military`, which is not even the hull it names. The
specification table has resolved that symbol all along, and the fleet page one tab over was already
printing "Oxen (Type-10 Defender)" off it. The journal's own name still wins wherever Frontier sends
one; the table is the fallback, and the raw symbol only the last resort.

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
