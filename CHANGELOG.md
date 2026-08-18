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
