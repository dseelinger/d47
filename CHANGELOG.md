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
