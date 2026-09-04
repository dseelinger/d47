# Changelog

What changed in each release of Directive 47, newest first.

**This file is the only record of the product this repository keeps.** Since 2026-08-27 there is
no `list.md`, no `docs/plans/`, no archive of retired queues — they were deleted, and what they
held is recorded in [#129](https://github.com/dseelinger/d47/issues/129). Everything not yet built
is a [GitHub Issue](https://github.com/dseelinger/d47/issues): defects as `bug`, wanted changes as
`change-request`, unbuilt phases as `phase`. An issue closes when it ships, and **the line it gets
here is its permanent record**; an issue closing is not a record.

A **completed phase is a minor release** — `0.<minor+1>.0` — because the version is how a Commander
tells "some fixes landed" from "there is a whole capability here now". A batch of wanted changes
— issues labelled `change-request` — is a minor for the same reason: nothing in it is a defect, so
shipping it as a patch would tell a Commander that nothing had changed. Fixes between phases are
patches. **A published tag never moves**: it is a receipt for one exact `d47.exe` and the checksum
beside it, so a correction ships as the next patch rather than as the same number twice.

**A phase takes its number here, on the release that ships it**, and that spends the number
permanently because this file never moves. Phase 59 is named under 0.7x and was still unbuilt on
the day it was named, which is the rule working as intended rather than an error.

**Sections below name files that no longer exist** — `list.md`, `bugs.md`, `remediation.md`,
`docs/plans/change-requests.md`. Each means the file as it stood on the day of that release, and
each is readable at `cd091a3`. They are left as written: this file is a record, so correcting its
history to match today's layout would be the one edit it must never take.

---

## 0.104.0 — unreleased — Every hull has its picture on the card whatever the journal calls it, a ship's own page draws it at 4K in three sizes, and a card turns once when you open it

Closes [#289](https://github.com/dseelinger/d47/issues/289).

### Every ship has a drawing, from the first launch

The hull drawings shipped in 0.103 as one file per hull that somebody had to drop into
`data/ships` by hand, and one hull had been captured. All forty-seven are now rendered, and the
small one — the drawing on the card — **comes with the download**. Eleven megabytes for the fleet,
so a fresh installation shows a fleet with pictures on it rather than a page of names.

**In `ships\` beside the executable, not in `data\ships\`, and that split is the point.**
`data\` is the Commander's: an update never touches it and an uninstall asks before removing it.
`ships\` is the build's, replaced wholesale by every update the way `runtimes\` is, so a corrected
drawing actually arrives. A hull present in both reads from `data\`, which is what keeps dropping
a file in by hand working.

### The picture at 4K, on the ship's own page

Ship Details had no picture at all. It has one now: the same drawing, the same camera, the same
Freestyle lines — rendered again at 3840x2160 rather than blown up from the 720p frame, because a
720p frame blown up to a window is a smear. Fitted to the pane, and clicking it fills the whole
window, where the wheel zooms at the pointer, dragging pans and a double click fits it again.
Escape returns.

**Zoom stops at one image pixel to one screen pixel**, which is the whole of what rendering at 4K
buys: on a 4K monitor that is the entire ship, and on a 1080p monitor the canopy fills the screen.

**A control rather than a block of the page**, on the Commander's instruction the day it was
specified: planning a hull you do not own wants the same picture, and that page will take
`HullPicture` unchanged when it wants it.

**No more than two are held**, asserted by a test rather than intended: a 4K picture is 33 MB of
pixels, so the one being looked at and the one just left is 66 MB and a third would be a hundred.
The card stills are decoded to 512 wide for the same reason in the other direction — every card on
a fleet page is on screen at once, and at full size that is 170 MB of thumbnails.

### The turntable is an MP4, played once when you open a ship

The rotation was a 120-frame sprite sheet — one 3 MB PNG per hull, about 20 MB decoded — that
played while the pointer was over a card. Both halves are replaced.

**An MP4, decoded a frame at a time.** The same rotation at 180 frames for a tenth of the bytes,
read through **Media Foundation**, which is Windows' own and costs the build no package and no
licence question. Nothing decodes ahead: a full turntable is 663 MB of pixels, and what is held is
one bitmap written in place.

**Played on the pick, not on the pointer.** A grid of hulls that turn as the mouse crosses them on
the way somewhere else is a slot machine. Opening a ship plays its rotation once and rests on the
still; opening it again plays it again. It is visible because of how the drill already lays out —
on anything but the narrowest pane, a ship's page opens *beside* the fleet, so the card turns next
to the page it opened.

`.spin.png`, the sheet decoder and the hover timer are gone rather than left beside the new path.

### The large art is fetched, and it says so

The 4K picture and the turntable are 260 MB across the fleet, so they are not carried. Opening a
ship fetches the two files for its hull from a pinned GitHub release, once, into `data/ships`. The
still shows while the fetch runs and stays if it fails; a miss is looked for once per session,
which is the rule `ShipArt` already kept for a drawing that was not there.

**Its own egress row**, although it shares a host with the update check — a disclosure organised by
address rather than by what is actually done hides things behind a brand. The update row says a
version number leaves and a build may come back; this one says which ships you looked at leave.
**Hull pictures** on the Ships card in Settings turns it off, and off, every ship keeps the small
drawing it came with.

### The rotation follows the hull, and the open ship is outlined

**One click, not two.** Opening a ship redraws the fleet index — it has to, so it can outline what
it opened — so the press started the video on a card that was thrown away a moment later. Measured:
after one click the pressed image was out of the tree and holding the frames, and the card on
screen was a different object showing its still. The second click "worked" only because the trail
no longer changed and nothing was rebuilt. The rotation now follows the hull rather than the
control: a card rebuilt for the hull that should be turning picks it back up as it enters the tree.

**And the ship whose page is open is outlined on its card**, the way a row has been since #110 and
read off the same place — the trail's last crumb, so it follows the right pane however it got
there. It was free to mean that only once "flying now" became a badge: two outlines meaning
different things on one grid is worse than neither.

### A ship's page is headed by the ship

Three more from driving the build, on 2026-09-04.

**The name is a heading now, and the rule that followed it is gone.** The page opened on one muted
line carrying both — *"Campaigner (Panther Clipper MkII). One build per ship: a slot holds one
plan."* — which is a title and a rule about the feature, in the same breath and the same grey. The
name goes at the top of the pane in the size a name goes in, at every size the picture takes; the
rule is in help, where somebody who wants it can find it, and nowhere on the page. What is left
under the heading is the one sentence that is about *this* ship and is not already in it: that
buying the hull will adopt the plan, said only for a hull nobody has bought.

**The full-screen mark sat under the scrollbar and could not be pressed.** The page scrolls, so the
bar is drawn over the content's right edge — and the mark nearest that edge is the one a Commander
reaches for. The row is held clear of it.

### Nine cards of twelve, and nothing failed anywhere

Reported from a screenshot of a real fleet the day the first build was driven: most cards had no
drawing on them. Every file was present, every name was right, and `ShipArt` resolved all
forty-seven when asked directly.

**`StoredShips` writes two spellings of a hull and Directive 47 prefers the wrong one for keying.**
The event carries `ShipType` and, for most hulls, `ShipType_Localised` beside it — and
`JournalJson.Named` deliberately takes the localised one, because a raw symbol reaching a Commander
is a fault this repository has already fixed three times. So a *stored* ship arrives as
`Type-8 Transporter` where a *planned* one arrives as `type8`. Measured on the live fleet that
reported this: nine hulls localised, three (Corsair, Anaconda, Cobra) not — which is exactly the
three that drew.

`EliteSpecifications.HullSymbol` is the inverse of `HullSaid` and the thing to call before a hull
becomes a key, a file name or a lookup. It matches with the punctuation taken out, because the two
spellings disagree about that too: the table says `Cobra MkV` and Frontier says `Cobra Mk V`.

`FleetRegistry` said in as many words that `StoredShips` carries no localised name at all. It does.
That comment is corrected rather than deleted, because it is the reason nobody checked.

### The picture has three sizes, and the flown ship wears a badge

Both asked for on 2026-09-04, after driving the first build.

**Half the pane, the width of the pane, or the whole window**, chosen by three marks above the
picture. It opens at half, with the ship's own figures in the other half — and the slot list below
runs the full width, which is where text would have wrapped under it anyway. The size you last
chose is the size the next ship opens at.

**"Flying now" is a badge, not a highlighted card.** The flown ship had an accent border round the
whole card, which is what every list everywhere uses for *the row you have selected* — so the fleet
opened looking as though d47 had already picked a ship: *"I assume that's the indication Current
Ship. That's the wrong signal to send."* A pill on the drawing says the one thing it means, in
words, which is also the only version of it that survives a Commander who cannot separate the hues.

**A card plays its turntable on the first click now**, not the second. The first selection of a hull
is exactly the one with nothing on disk yet, so a version that played only what was already there
never played the first time — which is the whole of what a Commander sees the first time they open
each ship. The fetch is asked for and the rotation starts when it lands.

**And the shipped still beats a copy in `data\ships\`.** Each folder is asked first for what it
owns: the build owns the card still and replaces it on every update, so a copy in the Commander's
folder can only be older. One was — 0.103's hand-dropped 280x158 preview of the Corsair went on
being drawn beside forty-six 1280x720 renders. The large art is the other way round, because that
is fetched into their folder, and both folders are still searched either way.

Fixing the half-width layout turned up a gauge that never wrapped: its heading was a horizontal
stack, which hands its children infinite width, so a long reading simply ran off the end. Nobody
saw it while a gauge had the whole pane.

### Twelve hulls whose pictures nothing could have found

Caught by looking at the drawn page rather than by a test, which is why there is now a test. The
render pipeline names its work for people — `python-mk2`, `type-9-heavy`, `fer-de-lance` — and
`ShipArt` finds a hull's picture by the symbol Elite's journal writes and by nothing else. Copying
the pipeline's names straight across looked entirely correct: thirty-five hulls whose readable name
happens to equal their symbol drew, and twelve did not. Nothing failed. The Python MkII's card was
simply blank, next to the Anaconda's, which was not.

`tools/ship-art.ps1` now carries the table that turns one name into the other, and
`TheShipArtIsNamedForItsHullTests` fails on a file named for anything that is not a hull.
## 0.103.1 — 2026-09-04 — `release.ps1` works from a linked worktree

Run from `.claude/worktrees/`, `release.ps1` failed at the merge: `main` is always checked out in
the primary checkout, and git refuses to check it out a second time. That arrived after the
commit, past every preflight the script has.

The merge and push now run in the primary checkout when the script is run from a linked worktree
— `git rev-parse --git-common-dir` finds it, and refs are shared, so nothing needs a second
checkout of `main` at all. A preflight before the commit refuses if the primary checkout is not on
`main` or is dirty: that may be another session's work in progress. The rollback on a refused push
puts `main` back in the primary checkout the same way it always did; the worktree that ran the
release never leaves its own branch. [#292](https://github.com/dseelinger/d47/issues/292)

## 0.103.0 — 2026-09-02 — The Transcript's file picker is drawn whole, a sold suit or weapon reaches the on-foot plan, a core you wrote can be written from the panel, body signals read the FSS as well as the surface scan, the Raw toggle stays off other tabs' roots, a hull you intend to buy is picked from a list, no prompt tells you to hold a button by its number, and the ship picker no longer repeats itself

### Writing a core of your own is reachable from the panel

Reported from a screenshot of the Persona section: *"Is there a capability to institute a custom
persona? If so, there does not appear to be a UI for it."* Both halves were right. The capability
had been built for months and there was no way in.

**A key that named a row nothing carried.** `PersonaCapability.OwnKey` was declared, the summary
it would read was written beside it, and `SettingsView` carried a branch that draws the row with
a **Write a core** button opening the editor — but `Rows(...)` never built a row with that key.
`persona.own` appeared exactly once in the repository, as the constant. So the branch could never
match, `PersonaWindow` was three hundred lines nothing opened, and the only way to write a core
was to write `personas.json` by hand and restart. Everything downstream of the row already worked:
a hand-written core joined the picker under the eleven that ship, took the shared preamble and
standing instructions like any of them, paired with a voice, and could be bound to a ship.

**The constant had been pasted into the middle of another method's documentation comment**, which
is the shape of the mistake: `SelectionPhrases` lost its summary to `OwnKey` and the row
registration never followed. Both are back where they belong.

**And the store is polled on the tick now.** It was read once at startup. Every sibling store —
alarms, ship builds, ship cores, memories, goals, adventures — is on the tick so a hand edit is
live without a restart, `OwnPersonaStore` is built for exactly that and has a test proving it
re-reads, and the promise held everywhere except in the running app.

Two tests, because one could not have caught it: the row is declared with its summary (Core), and
it draws its button when the store reaches the view (the real settings page). The second is the
one that matters — a row whose host delegate is absent draws no button, and a probe of Core goes
on passing.

**The editor drew its own legend down the left-hand side.** Found by opening the window for the
first time. A `DockPanel` child with no dock takes `Left`, and the legend added by
[#253](https://github.com/dseelinger/d47/issues/253) had none — so the key to the `*` and `◆`
marks stood in a column of its own down the side of the window, clipped at its own width, with
every card squeezed to 442 pixels of the 680 to clear it. It is docked to the top now, like the
two rows above it, and the cards get the width of the window.

Three tests: the row is declared with its summary (Core), it draws its button when the store
reaches the view, and the editor's cards get the window's width. The last two are on the real
drawn page, which is the only place either fault was visible — a row whose host delegate is
absent draws no button, and a probe of Core goes on passing.


### The file picker is sized by its longest reading, so its chevron has room (#273)

Reported from a screenshot of the Transcript: the picker offering **In Ship**, **Log File** and
**Journal File** was cut off at its right edge, the selected word readable and the chevron the
control draws for itself gone, so the box read as a sliced text field rather than as a drop-down.

**A `ComboBox` measures itself against its selected item and nothing else.** That is the mechanism:
the box was 86 pixels wide showing "In Ship" and 115 showing "Journal File", so it changed size
under the pointer as the reading changed and the room its chevron had was whatever the current word
left over. It is now sized by the widest of the three, whichever one is showing.

The chrome — the padding, the border, and the column the chevron sits in — is read off the live
control rather than written down here, as the difference between the width the box asked for and
the width of the word it was showing when it asked. A number copied out of a control theme is a
number that goes stale in silence the next time that theme moves. It can only be read once the box
has been laid out, and the first tab shown is a Transcript, so the reading happens on the box's own
`LayoutUpdated`.

**What is fixed is the sizing; the clip itself never reproduced.** The drawn page was captured at
every reading, at panel widths from 320 to 1180, at every zoom step from 100% to 200%, through the
real `ZoomHost` viewport and in the shipped dark palette, and the chevron was inside the box in all
of them. So the assertion that the chevron falls inside the box is a guard rather than a
reproduction, and the test that fails without this change is the one about the box resizing between
readings. If the slice is still there on a Commander's screen, the screenshot is what will settle
it.

### Selling a suit or weapon reaches the on-foot plan and the worn loadout (#274)

Found by the corpus diff [#270](https://github.com/dseelinger/d47/issues/270) added to
`spike/CorpusReplay`, which is the first thing that tool found: `SellWeapon` was one of eight event
kinds in a 944-journal corpus that nothing in d47 handled, and it sat beside a handled `BuyWeapon`.
`SellSuit` was handled, but only by the worn loadout and not by the plan. Two defects behind one
omission.

**The plan kept a sold item as owned.** `OnFootPlanService.Observe` adopts a prospective build onto
a real item when `BuySuit` or `BuyWeapon` matches it by name, writing the journal id into `ItemId`,
and nothing ever cleared it. After the Commander sold the item the build still answered "on you" or
"not on you now" rather than "not bought yet", and the plan went on pointing at something that was
gone.

**Selling is the buy backwards**, and it matches on the id rather than the name: by the time a sale
arrives there is exactly one build that can be meant. The build gives up its `ItemId` and keeps
everything else — owned is derived and intended is authored, so the plan the Commander wrote
survives the sale, the line reads "not bought yet" again, and buying the replacement adopts that
same build onto the new id. That is the whole reason for keeping the build rather than deleting it.

**The worn loadout kept a sold weapon in its slot**, and unlike a suit there is nothing to correct
it later. Elite writes a fresh `SuitLoadout` when the suit being worn is sold — which is what the
existing `SellSuit` fold leans on — but writes none after a weapon sale: measured over the corpus,
what follows a `SellWeapon` is `CommunityGoal`, `Embark`, `Loadout` or another `SellWeapon`. So a
weapon sold out of the slot the Commander was carrying it in stood there until the next loadout
switch. `OnFootLoadout.Apply` now takes it out of its slot, matched on `SuitModuleID` alone —
`SellWeapon` carries no `SuitID` to check the suit by and needs none, since the id is that weapon's
own — and a sale of a weapon that is not in the loadout is not news and changes nothing, including
the loadout's own "last reported".

**Ten on-foot sales in 944 journals**: six `SellWeapon`, four `SellSuit`. Two of the six weapons
sold had appeared in a loadout snapshot earlier in the same session — both a Karma P-15, one of
them five minutes before the sale, and that pair is what the test drives — so the worn-loadout case
is real rather than theoretical. None of the four suits sold had appeared in a snapshot that
session, which is consistent with the flight-suit rule the `SellSuit` fold already describes.

`BuySuit` and `BuyWeapon` stay out of the worn-loadout fold, for the reason that was already
written there: a suit just bought is by definition not the suit being worn. `SellWeapon` joins
`HandledEvents.ActedOn`, and the corpus diff now names seven unhandled kinds rather than eight.

### Body signals read the FSS as well as the surface scan

Found by the corpus diff #270 added: `FSSBodySignals` was one of eight event kinds nothing in d47
handled, sitting beside a folded `SAASignalsFound`. Elite writes the same `Signals` array earlier,
the moment the FSS resolves a body, before anyone flies to it — and over 944 journals, 349 of the
429 bodies with FSS signals were never surface-scanned. For four bodies in five, the FSS was the
only source d47 would ever see, and `get_body_biology` had nothing for them at all.

`BodySignals.Apply` now folds `FSSBodySignals` too. Order matters: a surface scan is the later,
fuller answer and always replaces an FSS row for the same body, but an FSS row must never replace
an existing surface scan. `BodyBiology` now records which event it came from, because an empty
`Genera` used to mean one thing — "the scan found no biology" — and after this change it can also
mean "not mapped yet, ask again once it is." `get_body_biology` tells the two apart: an FSS-only
body says how many biological signals were reported and that it has not been mapped, rather than
implying the genera are known to be absent.

### The Raw toggle stays off other tabs' roots (#277)

Reported from a screenshot of the Fleet tab: the Journal reading's **Raw** switch was showing on
the Ships root, above "Plan a ship you do not own", unconnected to anything Ships draws. Flipping
it did nothing to the list — it silently changed the remembered Transcript reading in the
background, only visible on returning to that tab.

**`DrawRawToggle` only ran from inside `DrawModes`, behind the mode picker's own visibility
guard.** A tab with one root — Fleet's Ships root among them — never shows a picker, so the guard
returned before the toggle was ever re-evaluated, and it kept showing whatever the Transcript tab
had last left it as. The call now runs ahead of that guard on every navigation, and `DrawRawToggle`
itself now checks `Nav.Tab == PanelTab.Transcript`, since the switch is a Transcript-only
affordance.

### A hull you intend to buy is picked, not spelled at a blind box (#282)

Fleet → Ships → **Plan a ship you do not own** asked "Which ship do you intend to buy?" as free
text. Anything that was not an exact known hull came back as *"I do not know a ship called…"* —
after the fact, and without saying what would have matched. The hulls are a closed set of a few
dozen and d47 ships the whole table, which is the one case a picker beats a box.

**It is a real `ComboBox`, and the panel spent a long time believing it could not have one.** The
first cut of this drew the hulls as a stack of full-width buttons under the text box, which the
Commander rejected on sight — *"one of the most stupid design decisions ever"* — and they were
right: forty-eight window-wide buttons is a wall, not a picker. The belief that ruled a combo out
was that a popup has no top level to hang from on a window that is never shown. That was answered
by [#231](https://github.com/dseelinger/d47/issues/231) and the answer was never applied here: the
desktop window has a real top level and gets the control's own drop-down, and the headset gets
`OffscreenSurface.Choose`, which takes the ray-press before a pointer event exists and draws the
same items on the panel. Both surfaces, one declaration, no new mechanism.

**Type-ahead comes with the control.** Avalonia 12's `ComboBox` matches multiple characters, so
"ana" is an Anaconda; the box is focused when the page opens, so that is the whole interaction for
a Commander who already knows the hull they want — which is everyone who pressed this button. The
box carries a generous `MinWidth` floor rather than sizing to its selection, because a `ComboBox`
measures itself against the selected item and nothing else, and a box that resizes under the
pointer has been reported twice already (#231, #273).

**Two controls were removed, on a principle rather than for tidiness.** The drawn keyboard exists
because free text needs one in a cockpit, and **Done** exists to commit free text once and
atomically. Neither applies when every answer is already on the page: picking is the commit. The
picker is its own page for the same reason — branching inside the entry page would have left most
of it unreachable whenever a list was offered.

**Voice is untouched.** It is still armed while the picker is open, and a spoken hull commits
through the same validation a picked one does. A value that is not a hull says so and leaves the
picker standing; there is no keyboard to put back, because the answers are all on screen.

**The second line no longer claims anything about the fleet.** It read "it is not in your fleet
until you own one", which is false the moment the hull picked is one the Commander already flies —
owning a Python and planning a second is an ordinary thing to do, and the page was telling them
otherwise. It now says what is true either way: the build is planned now, and buying one will
point the plan at it.

### A waiting prompt no longer names your stick button by its number

Every prompt in d47 that waits on speech showed the same line, and for a Commander bound to a
stick it read **"Hold button 11 on your stick and say it."** Reported: *"I don't know which of my
4 WinWing Orion 2 throttle controls is button 11."*

**The number identifies nothing, and `HotasButton` already said so.** Its own doc comment has read
*"the WinWing throttle alone presents four interfaces with 4x32 mode on — so button 7 alone is
ambiguous and always was"* since Phase 53. d47 holds the device as an opaque `NonRoamableId`, so it
cannot put a name beside the number even if it wanted to; printing the number alone was asking the
Commander to identify a control from a fact that does not identify it.

**The split is report against instruction, not stick against key.** Where the binding is being
read so it can be *changed* — the settings row, the diagnostics inventory, the collision warning
raised as you bind it — the number is the stored value and stays. Where it is being read so it can
be *acted on*, the prompt now says "Hold your push-to-talk button and say it." A key is still named
in both, because `[` is a thing you can find.

**It still names a gesture rather than claiming to be listening.** That distinction was settled
when the prompt used to say "Say it — I am listening." under push-to-talk and was reported as a
lie; dropping the number must not quietly undo it, and a test pins the whole sentence.

### The ship picker no longer says "or say it" twice

The combo box built for #282 draws its own placeholder, "Pick one, or say it", and beside it stood
the same waiting prompt every other voice entry shows — "Hold your push-to-talk button and say
it." on this Commander's build. Two controls, inches apart, both telling the same instruction.

**The waiting line is still there for what it is actually for**: it shows a partial transcript as
it is heard, and a hull that was not understood, or refused, still explains itself in the same
spot. Only the idle caption — the copy of what the placeholder already says — is gone.

## 0.102.0 — 2026-09-02 — The chatters are named and spaced, captions can sit in the cockpit, and d47 can say which journal events it handles

### One list names the journal events d47 acts on, and a gate keeps it honest (#270)

"Does d47 handle `<event>`?" had no trustworthy answer. Dispatch on an event's name was spread
across dozens of files in three syntactic shapes — `case "Docked":`, `Kind == "Docked"`,
`Kind is "Docked" or "Undocked"` — and a grep over two of the three missed `DockingGranted`, which
`CarrierState` plainly handles. A check that under-reports is worse than none: it reports a gap
that is not one and hides one that is. And a new Elite event that nothing matched fell through
every consumer with no error and no log line, which is the right runtime behaviour and the reason
the first sign was a Commander noticing d47 had stayed quiet.

**`HandledEvents`, in Core, is the list.** Two blocks rather than one, because "handled" hides the
question a Commander actually asks. The Journal File reading has a sentence for nearly every event
Elite writes and hides nine more as noise; counting those as handled would have answered "yes" for
`Screenshot` and made the diff against a corpus near-empty. So `ActedOn` is what something reacts
to — folded into game state, spoken about, planned from, written into the logbook — and
`NarratedOnly` is what only the reading knows. "Why did it say nothing when X happened" is answered
by X being in the second block far more often than by X being absent.

**The gate reads the source with the compiler, not with grep**, because grep is what the issue
caught under-reporting. `HandledEventsGateTests` compiles every `.cs` under `src/` with Roslyn,
asks the semantic model for every read of `JournalEvent.Kind` — the one question grep cannot
answer, since a dozen other types have a `Kind` — and follows each read to the names it is compared
against: `==`, `is` with any `or`, `not` and parentheses, both kinds of `switch`, property and
tuple patterns, membership in a collection whose initializer lists the names, a local the kind is
copied into, an argument followed into one of d47's own methods, and **a copy stored in a record**,
after which every read of that property is a read of the kind. That last one is what found the
reading's noise list: the kind goes into a `JournalEntry` and is tested against
`JournalSentence.Noise` from there — eight names the first pass missed, and exactly the drift the
gate exists for. It fails in both directions, and it fails on a comparison it cannot resolve — a
parameter, a method's result, a prefix test — rather than leaving it out. Three seconds, and a
test rather than a CI step for the same reason the other three gates are.

Broken three ways before it shipped, to prove it catches them: a name removed from the list, a
`== "FooBar"` added to a callout, and a `StartsWith("Carrier")`. Each failed the build naming the
file and line, and the last one said to list the names instead.

**`spike/CorpusReplay` now ends with the corpus subtracted from the list**, which is the second
thing the issue asked to make cheap and the one that finds defects nobody reported. Its first run:
944 journals, 716,736 events, 222 distinct kinds; **8 that nothing in d47 handles** —
`BackpackChange` (2,835), `Backpack` (768), `FSSBodySignals` (429), `FCMaterials` (89),
`SellWeapon` (6), `SquadronApplicationApproved` (2), `CancelDropship` (1), `GameModeChange` (1) —
and 114 that only the reading knows. `SellWeapon` beside a handled `BuyWeapon`, and
`FSSBodySignals` beside a folded `SAASignalsFound`, are the shape of gap the issue predicted. None
is fixed here: this is the instrument, not the fixes it suggests.

The gate adds `Microsoft.CodeAnalysis.CSharp` to `D47.Core.Tests` — MIT, test-only, and inside
`PackageLicenceGateTests`' walk like everything else in the solution. The donation scrubber's table
is deliberately not counted: it reads the event name out of raw JSON, and taking a person's name
out of a `Friends` event is not reacting to one.

### Captions can be world-locked, low between the console and your feet (#204)

Asked for as a placement preference and re-filed an hour later as **comfort**, which is what
moved it inside the moratorium: head-locked content is shaky, and world-locking helps prevent
motion sickness. That is not a taste. A head-locked band is the only thing in a headset that does
*not* move when the Commander turns — the cockpit sweeps past, the inner ear reports a turn, and
the caption reports nothing — and that disagreement is the mechanism motion sickness runs on.
Oculus's own best-practice guide says to put interface elements into the 3D world rather than
float them in front of the eyes.

**`Caption position`, two named values, and no third thing.** `head` is unchanged and stays the
default. `world` puts the band 0.80 m ahead of the seated Commander and 0.67 m below their
eyeline — a strip covering 37° to 43° down, between the console and the feet, where the
head-locked band covers 12° to 19°. It is one row beside the four captions already had, under
Captions and Advanced.

`settings.json` gets `"lock": "world"`, spelled the way `vr.panel.lock` is spelled and stored as a
string for the same reason it is: a `SurfaceLock` on the settings record writes `worldLocked`
beside the panel's `world`, and — found in review — a file hand-edited to the word the row, the
docs and the panel all use would then *throw* on load rather than being read. Anything that is not
`world` reads as head-locked, which is the band that is always in view.

**This narrows a rule and the narrowing is the point.** *A caption you can drag somewhere you will
not see it is not a caption* was a rule about free placement, and neither of these positions is
free: both are worked out from the geometry. Captions still gain no distance row, no curve row and
no grab-to-move, and `TheCaptionSurfaceGainsALockAndNothingElseFromThePlacementRows` is the old
test with the two assertions that still hold left standing and the one that no longer does
inverted.

**Placed against the seated origin, not against a head pose, and that is what makes it need no way
back.** Every absolute overlay d47 places goes into `TrackingUniverseSeated`, whose zero *is* the
Commander's seated eye facing their forward — verified against a recorded head pose of
y = −0.036 in `view-state.json`. So "between the console and the feet" is a constant in that
universe, and SteamVR's own *Reset Seated Position* carries the band with it. The issue asked for
re-anchor to reach a world-locked caption, and re-anchor has not existed since 0.94.0 (#219): a
pose frozen off one head sample would have kept whatever lean was in that frame with nothing left
to undo it. A constant cannot drift, so there is nothing to undo.

**The size steps did not need re-measuring, because the width moved instead.** Apparent text size
is the texture's pixel count and the quad's width in metres *together*. The world-locked band sits
1.04 m from the eye where the head-locked one sits 1.66 m, so holding 0.9 m across would have drawn
every caption 59 % larger. The width is derived from the ratio of the two eye distances — 0.565 m —
and both bands subtend the same 30.3°, so `small`, `medium` and `large` mean one thing rather than
two. Curvature is still zero, for the reason it always was.

**On the tilt (#189), the honest claim is smaller than it looks.** That shipped in 0.93.0 and
head-locked captions have been level since. What is left is that they are levelled once a serve
while `SetOverlayTransformTrackedDeviceRelative` carries the quad rigidly at headset rate in
between, so a quick roll of the head tilts the band until the next frame. A strip the headset is
not carrying cannot show even that.

*Left alone and worth knowing:* `architecture.md`'s Re-anchor paragraph still describes a feature
retired in 0.94.0, and `VrPanelSurface.Head` is dead — nothing has read it since the same
retirement. Both are #219's debt rather than this change's.

### Two unprompted voices are kept apart, and no longer come due on the same tick (#257)

Reported off the settings card as *"these appear to overlap"*. They do not: a remark is the core
itself, in its own voice, about where the Commander is; an exchange is two strangers on a channel
the Commander is deliberately not part of. **What overlapped was the timing.** In Ship chatter and
NPC chatter were two callouts, each holding its own clock, and the engine's cooldown is keyed per
announcement — so `ambient.supercruise` and `npc.chatter.passersby` never tested against each other
and nothing anywhere was a floor between two unprompted utterances. The failure was never talking
over, since the audio arbiter serialises; it was a one-line remark and a four-line invented scene
arriving nose to tail, which reads as the core filling silence with itself.

**Underneath that, the two were phase-locked by construction, which is the part the report could
not see.** Both callouts choose each cycle's wait by hashing their own pick counter with the same
Knuth constant, and that hash returns exactly zero on the first pick — so both served exactly their
interval the first time. Both are seeded on the same first live tick, both read the same situation,
both carry the same ninety-second settle, and since the rows were levelled on 2026-09-02 both carry
the same numbers. **So the first remark and the first exchange of every session were due on the same
tick, every session.** A floor alone would have converted that into a permanent ninety-second
couplet followed by minutes of silence — a cadence, which is the one thing the spread exists to
prevent. So the chatter spread is now offset by one against the ambient one. It is still off the
pick counter with no clock and no seed, so a recorded session still replays to the same spacing.

**The floor is derived, not a new row.** An announcement said because nothing happened now carries
the rate the Commander asked for it at, and the floor in force is the least of ninety seconds, that
rate, and the rate of whatever spoke last. **Both ends have to bound it.** The waiter's rate bounds
it so nobody is held longer than their own row asks. The speaker's rate bounds it because otherwise
a fast voice does not space the other one out, it silences it: a kind set to speak every twenty
seconds restamps the floor faster than a ninety-second wait can ever expire. Between them, a
Commander who turns either kind down to twenty seconds gets a twenty-second floor, and a Commander
running one of the two cannot tell it exists. Ninety is arithmetic rather than taste: the longest of these utterances is a four-line
scene, about thirty seconds with its beats and synthesis, and thirty seconds is already this app's
span for two things reading as connected. Thirty of air behind thirty of scene, rounded up because
the engine can measure neither. The argument against a row is the one already on record for the
beat inside an exchange — a knob for something a Commander wants right rather than adjustable.

**Nothing urgent waits on it, and that is asserted rather than argued.** The floor keys on the
carried rate and never on urgency, which answers a different question: route progress, an arrival
and a milestone are all routine and none of them is chatter, so an engine keying on urgency would
have quietly spaced out the news. A test drives the shipped danger warnings into the busiest
possible tick — both chatter callouts colliding — and asserts the warnings go on it.

**The one that does not go is held, not spent.** Both callouts ask before anything of theirs moves,
so a deferred line keeps its clock, its pick counter and its variant, and arrives as the floor
clears rather than an interval later. An exchange stopped there never reaches the drain, so it is
never composed, never sent and never billed. The engine re-asks the same question when the line is
queued, so the guarantee is the engine's rather than the two callouts' manners.

**When both are due on one tick, the rarer voice goes.** The exchange speaks and the remark waits,
because a remark will be along again shortly and an exchange will not. That was written the other
way round first, on the grounds that it puts the larger gap in front of the longer utterance, and
measuring it settled the argument: the exchange has the narrower window — a longer interval against
the same ninety-second settle — so it is the one that starves when it always loses. With an In Ship
row pinned to a fixed cadence that resonates with how often the situation turns over, four hours
produced twenty-nine exchanges alone, **none at all** with the remark going first, and twenty-eight
with the exchange going first. The tie-break is registration order and nothing would fail if those
two lines were swapped, so the reasoning is written where they are registered rather than assumed.

The floor is measured where a line is queued and not where it is heard, which is the honest limit
of what a component holding no clock can promise: ninety seconds of floor buys about eighty-five of
real air after a remark and about seventy after a scene.

### The panel's tabs run Transcript, Fleet, Engineers, Checklist, Routing, Adventures, Utilities, Settings (#272)

The bar had Routing second, beside the transcript, since Phase 37, on the grounds that a route is
read while flying and should not be far along. **Fleet and Engineers now follow the transcript,
then Checklist and Routing**, with Adventures, Utilities and Settings where they were. The order is
the Commander's, and it is one order everywhere it is read: the bar in the window and in the
headset, and the list of destinations a HOTAS switch position may name, which is bar order. A
position already bound is unaffected, because it is bound to a root by name rather than by place
in that list.

### The Help improve D47 info flyout wraps its text instead of scrolling sideways (#271)

Pressing the ⓘ that #269, below, had just added showed every paragraph clipped at its right edge with a horizontal
scrollbar under it — *an event Frontier added tha*, *a list of fields to remove* — so a Commander
reading a reason for pressing had to scroll sideways to finish each sentence.

The flyout's text was capped at 460 so that an unconstrained flyout would not lay a paragraph out
on one line. But the theme's presenter caps itself at 456 and has 430 left for content once its
padding and border are off, and it puts that content in a viewer that scrolls sideways rather than
narrowing it. So the 460 won the measure and lost the viewport: the text wrapped at 460 and was
shown through a hole 430 wide. **The presenter now has sideways scrolling turned off**, so its viewer
measures the text at the width it will show and the text wraps there. A test opens the flyout
headless and asserts the content is no wider than its viewport, whatever the theme's numbers are,
and fails against the flyout as it was.

### One invented person is one voice, however the model spells them (#256)

Found in the Commander's own log: *Courier Vance* on the first line of a four-line scene and
*Vance* on the third, in two different men's voices, and the dock hand he was talking to split the
same way. The parser took the name verbatim from before the colon and the cast keys its per-system
table on that string, so each spelling was a new person, drew its own voice, and sat in the table
until the next jump — a two-person exchange heard as four, drawn as four, and durable.

**The exchange is the only place the two spellings are knowably one man**, so the reconciliation
lives in the parser, over the lines of one exchange, before any of them reaches the cast. A name
folds onto a longer one it is the leading or trailing word of — *Vance* onto *Courier Vance*, *Vera*
onto *Vera Kolt* — and does not fold when that would be a guess: *Vance* beside *Mara Vance* and
*Tom Vance* stays as written, because a wrong fold is the same fault pointed the other way. The
carrier's own tower and captain take no part, since a line that carries a cast role is already one
person by role and *Captain Reyes* is a pilot with a rank rather than a second spelling of the
captain.

**The brief now asks for one spelling per speaker too**, in the same wording the carrier's two
posts were already asked for. It is a request rather than a guarantee, which is why the fold sits
underneath it. Tests build the exchange from the log, assert one name and one voice per person and
two entries in the cast rather than four, and fail against the parser as it was.

### Help improve D47 opens with the disclosures and nothing else (#269)

The window still led with a paragraph arguing for pressing before it said what pressing does. #252
trimmed it once and got the mechanism onto the site; what stayed was every fact **plus** the
sentence of reasoning attached to each fact, which is most of a wall of text by volume and none of
it by weight.

**The intro is now a fact per line** — what the scrub replaces and drops, where a send goes, how long
it is kept, what d47 keeps here, and the per-installation number that travels with it. Six lines
where there were fifteen, and the first thing read is the one that governs all of them: *nothing is
read, written or sent until you press*.

**An ⓘ sits beside the ?**, and the pair reads left to right in the order they deepen: the intro is
the facts, the glyph is the reasoning — why real journals find defects, what the scrub keeps rather
than removes, why a history is offered as a report about itself — and the glyph's own button opens
the full page on the site. A flyout rather than a second dialog, so dismissing it costs nothing a
Commander had already chosen on the window behind it.

**What did not move is any statement of what leaves and where it goes.** That line has now held
through two trims, and it is what the window exists to obtain: a Commander who presses neither glyph
has still read every term. Tests assert the split in both directions, because the invisible half is
the one that rots — a disclosure drifting behind the glyph, or an argument drifting back into the
intro.

### In Ship chatter and NPC chatter

**Ambient remarks** are **In Ship chatter**, and **invented chatter** is **NPC chatter**. The old
names described the wrong thing: *ambient* said when a line arrives and left a Commander to work out
whose voice it was, and *invented* said how it was made, which is Directive 47's problem rather than
theirs. The pair reads as a boundary now — your own AI and crew on one side, everybody outside
the ship on the other.

The wording is borrowed rather than invented. The Aboard voice slot already describes itself as
*"your ship's AI and your crew"*, so one vocabulary covers the callout rows and the voice rows both,
and **In Ship chatter** sits beside the **In Ship** transcript reading 0.100.0 named.

It moved everywhere it is read: the two switches, the four interval rows, the spoken routes
(*"stop calling out in ship chatter"*, *"stop calling out npc chatter"*), both headings on the
callouts page, and the mentions on the conversation and persona pages. **No settings key moved**,
and none ever can — `settings.json` is append-only, and `callouts.ambientSeconds` already
carries one retired minutes-era sibling for exactly that reason.

### Each info bubble says who it is about

Which is the whole point of the rename. **In Ship** names the ship's AI and your crew; **NPC** names
anybody outside your ship. It also says that **only the AI speaks unasked** — your crew answer
when you address them and have never once spoken first, so naming them without that clause would
have the page promise chatter that cannot happen.

### And the four intervals are the ones that were flown

**300 and 600 seconds, both pairs.** Set by hand in the running app, then written down.
0.101.0 shipped the In Ship spread with 45 and 90 and said in as many words that the numbers could
only come by ear; the ear said five to ten minutes. NPC chatter came down from 1200-2400 to meet it.
The argument on record for the wider floor — an exchange is a scene rather than a sentence, and
scenes wear out faster — is sound and was still an argument from the desk: flown, twenty minutes
of silence read as the feature being broken rather than as restraint. The two kinds now arrive at the
same rate and what varies is the mix.

A settings file already on disk is untouched. Only a Commander who never set these rows meets the
new numbers.

---

## 0.101.0 — 2026-09-02 — Nothing arrives on a beat, and the panel opens where you left it

Four wanted changes, and they are two subjects. Two are about the pacing of speech nobody asked
for — remarks that ticked like a clock and an invented conversation that came out rapid fire.
Two are about the panel forgetting, every launch, which reading of itself you were looking at.

### Ambient remarks have the spread invented chatter already had (#258)

The gap between two remarks now lands somewhere in [**The least time between ambient remarks**,
**The most time between ambient remarks**] rather than on the interval exactly — 45 to 90
seconds out of the box, keeping the doubling the chatter pair has. The argument was already written
down for the other row and it was stronger here: *a fixed cadence is the one thing overheard traffic
must not have*, and where chatter is a rotating cast of strangers who disguise a beat, this is the
same voice every time at a fraction of the gap. It was the most clocklike thing in the app.

Off the pick counter by the same Knuth hash the chatter spacing uses, because no Core component
reads a clock or a seed and a recorded session has to replay to the same spacing. The two edge
cases are chatter's exactly — a maximum at the minimum pins the cadence, one below it reads as
the minimum — since two rows of the same kind disagreeing about their own edges is worse than
neither having the feature. `ambientSeconds` keeps its name and its meaning, so a settings file
written before today loads with its floor above the new ceiling's default and keeps the fixed
cadence it chose.

### The lines of an invented exchange are spoken with air between them (#259)

Reported driving 0.98.1, as *"like watching an episode of the Gilmore Girls"*: a four-line scene
was four utterances butted together across about two seconds, because nothing put a gap anywhere.
Each line after the first now waits between six tenths of a second and one and seven tenths, off
the line's position and never the same twice.

Four things were settled rather than drifted into. The beat is the same whoever speaks — a
handover between two people genuinely is a longer beat than one person carrying on, and it is more
machinery than a four-line scene earns. It is a rule in the speaking loop rather than a field on
`Announcement`, which keeps the record narrow instead of inviting every callout to ask for pauses.
It is not a settings row, on purpose. And the captions needed nothing at all: the caption layer
already starts a reading dwell when the voice stops rather than blanking the quad, and that dwell
outlasts any beat here.

**The pause yields, which is the half that mattered most.** The speaking lock is held for a whole
batch, so air added inside one is air the *next* batch waits through — and the next batch is
where a danger or fuel callout would be. The queue is read without draining it, and the pause is
taken in slices that ask again on every one, so an alert arriving mid-gap does not serve out the
rest of it. A Commander hearing about the heat two seconds late because a courier was chatting
would have been the worse defect.

### The Raw switch is where you left it (#267)

The journal's **Raw** switch keeps its position across launches. Both directions: a Commander who
reads the file gets the file, and one who does not never sees it.

**The trap is that raw is a root, not a toggle.** Raw Journal is registered on the Transcript tab
exactly like the journal is — the picker declines to list it and normalises it away, but the
navigator holds it as a root all the same — so remembering it as a *root* would have opened a
wall of JSON on the tab the panel starts on. What is kept is the switch's position, applied when
the journal reading is opened. And arriving *from* raw is the one move it must not undo: turning
the switch off looks identical to opening the reading, so the previous reading is kept alongside
the current one, and without that there would have been no way out of raw at all.

### Every tab opens on the reading it was left on (#268)

Leaving Routing on **Course**, or the Transcript on **Log File**, and closing d47 now comes back
there. Settings comes back scrolled to the section you left it at. Half of this already worked and
knowing which half was the whole diagnosis: the navigator has always kept one current root per tab,
so a tab switch returned to the mode it left — nothing wrote it down, so every launch started
at the first reading each tab furnished. The settings nav is the same fact in different clothes: it
is a scroll-spy, so its selection is a scroll offset.

Which tab was showing is deliberately **not** remembered; the panel still opens on the transcript.
Restoring a settings section scrolls and **unfolds nothing** — the help-link jump expands the
card it lands on because a link to a folded card goes nowhere, and arriving at the page you left is
not that act.

**And the settings page now re-reads before it writes.** It saved the view-state snapshot taken
when the page was built, so collapsing a card wrote back a copy from build time and undid anything
any other writer had recorded since. That was already live against the pane widths and the
checklist filter; it would have eaten these readings on the first card a Commander closed.

### `promote` finds the newer pre-release again, past v0.100.0

Not an issue — a defect found and fixed on the day, and the diagnosis came from the shape before
the cause: *"funny it started when we rolled over from 99 to 100"*. The filter asking whether a
waiting pre-release is newer than the current latest assigned to `$version`, and PowerShell variable
names are case-insensitive, so it was the script's own `[string] $Version` parameter — type
constraint and all. Assigning a `[version]` to it coerced the object back to a string and `-gt`
compared two strings, which agrees with a version comparison for every release this project has ever
cut and stops agreeing at exactly v0.100.0: `"0.100.0" -gt "0.99.0"` is False, because `"1"` sorts
before `"9"`. A promotion would have gone looking for something newer than latest and found nothing.

---

## 0.100.0 — 2026-09-01 — The Transcript says where you are, and the forms say what they want

A release about the surface telling the truth about itself. It started with two words on a
drop-down and ended, twelve issues later, having deleted a room nobody could enter, taught every
form to say which of its fields it actually needs, and found a margin that had been pushing controls
off the edge of every zoomed dialog.

### The Transcript readings are named for where you were (#250)

**Conversation** is **In Ship**, and **Elite Dangerous Journal File** is **Journal File**. Two of
the three were named for what they are made of rather than for what a Commander goes there to see.

The half that is invisible on the screen it is about: a crumb's label doubles as the phrase that
reaches it, so renaming one silently retires the old word. In Ship carries `Spoken = ["conversation",
"thread"]`, and the journal needed nothing new — *"elite dangerous journal"* was already in its list
and goes on covering the label it no longer draws.

### The help describes the page in front of you (#251, #262)

The Transcript's `?` opened a page describing Thread, Details and D47 Log — three names, none of
which had existed since 0.96.0, and one of which had been removed outright. Both journal readings
had no help anywhere: their mark landed on a page that had never heard of them.

Five more pages sent a Commander to **Technical**, four of them beyond what the report listed.
A help page promising somewhere to go and look is worse than one that says nothing.

**Then one page became three (#262), on the Commander's instruction:** *"The transcript tab should
have 3 different Help pages depending on context — In Ship, Log File, Journal File. And not try to
cram it all into one ELI5 page."* One page for three readings described all of them in the same four
sections, so pressing `?` on the journal returned three quarters of an answer about somewhere else.
The mark was already context-sensitive — `NavCrumb.Help` is per crumb — so this was one page short
of what the mechanism offered rather than a new mechanism.

**And a gate, because prose is exactly what nothing was checking.** `DocumentationGateTests` asserts
every capability has a page quoting its schema; a general page has no schema and a `NavCrumb.Word`
was written down nowhere a test could compare against. Each reading is now checked against its own
page, and no help page — or Commander-facing string in `src` — may name a reading that no longer
exists. That second half caught a response that had been answering *"the details are on the
Technical page"* in the conversation itself for two releases.

### The Technical reading is gone, not just withdrawn (#260)

[#231](https://github.com/dseelinger/d47/issues/231) took it out of the picker and nothing else went
with it. `TranscriptPage.Technical`, `TranscriptKind` and eleven writers survived, so the text was
held in memory for the life of the session and drawn nowhere.

Two of those writers were not diagnostics. Typing *"show me the checklist"* moved the panel and said
nothing back, because its only feedback was an append to a page nobody could open — and
`AppHost.Navigate` writes no log line either.

Everything that was a diagnostic is deleted rather than rehoused, on the Commander's reason:
*"everything in Technical is still in the Log File. I found myself never looking at the technical
file after the Log file view was created."* `SpeechLoopTrace` goes, because the loop already reports
every stage to the log; `TechnicalLogBridge` goes, because it was a log sink and could only ever
have been the Log File twice. With the kind deleted there is one destination, so a line written and
shown nowhere is no longer expressible.

### In-game comms are read where the game's own words are (#264)

They went to the Technical reading, so after #260 they reached nothing at all. Putting them in the
conversation was tried and the drawn page refused it: In Ship is bubbles and a comms line arrives in
the ship's voice, so a station's clearance merged into the sentence d47 had just said and rendered
as one. They are logged now — the Commander's call — which puts them in the **Log File** reading,
under a Voice category so the Voice level governs them when a station approach gets noisy.

**And the Journal File reading draws them properly.** It held every `ReceiveText` and drew each as
the bare words *"Receive Text"*; `JournalSentence`'s own comment claimed the page rendered the
message, and nothing ever did. It draws the sender and the words — **but only Frontier's**. A
message another player typed stays out of the summary line, which is the untrusted-input rule and is
not relaxed here. Checked against 4,042 real events before the test was written, which is what
caught the first draft: Elite writes an empty `From` for its own channel notices, and every one of
them read *": Entered Channel: Tarakah"*.

### Clear empties the reading you are looking at (#261)

It refused exactly `TranscriptPage.Log`, which was right about all three readings that existed when
the rule was written. The journal readings arrived later and were never added — so pressing Clear
while reading the journal emptied the conversation three doors away, and nothing on screen changed,
because a journal reading is drawn from Elite's file. A Commander found out on going back to In
Ship.

It asks whether the reading is d47's own now, and the menu item is greyed on the three that are
files on disk — the way Copy beside it is already greyed with nothing selected.

### A scroll that moved nothing says why (#263)

Saying *"page down"* with nothing to scroll cost a language model turn that answered *"No tool for
keystrokes on my end, Commander."* The phrase had matched. What failed is that a surface said
`false` for two different things — already at that end, and nothing here scrolls — so the host read
both as "not a scroll" and handed the sentence to a model that was never meant to see it.

The intent was already written at the branch that declined: a Commander who says "page down" at the
bottom should hear that they are at the bottom. It had no way to travel. A matched phrase is
answered locally now, always.

### Fields say what they need (#253)

The Routing tab's placeholders carried three incompatible meanings in one slot: an example
(`Colonia`), a source of fact (`this ship's`), and a static default (`60`) — in the same grey. So the
three required fields were the ones that looked most like they had already been answered. The Trade
run card is the tell: somebody hit this, had nowhere to put the answer, and put the word *required*
in the placeholder, where it vanishes the moment you type.

Marks go on the label. Required is marked and optional is not, because required is the minority by a
distance. Every form carries a legend keyed to the marks it actually uses.

**The third state is d47's own and no generic form has it:** *optional because d47 already knows the
answer*. It gets a shape of its own rather than a second asterisk, and it draws the live figure —
*"this ship's (28.42 ly)"*, *"where you are now (Shinrarta Dezhra)"* — from the same expressions the
tool call falls back to, so what the form says it will use and what gets sent cannot differ. It
follows a jump and a refit.

**`PersonaWindow` was the worse case:** three boxes whose only label was a placeholder, on a window
where every existing core is drawn with its text already set — so all three were unlabelled on the
screen a Commander spends time on.

**One measurement worth recording.** `AutomationProperties.IsRequiredForForm` announces nothing on
Avalonia 12.1.1: no member of `TextBoxAutomationPeer` or of `AutomationPeer` mentions it, and the
platform provider builds its answers from the peer. It is set anyway, and what actually reaches a
screen reader is the box's name, which carries the state in words.

### The pop-up windows get a help mark (#252)

A dialog cannot reach the in-app help, and that is the mechanism rather than an omission: help is a
level of the panel and these windows are shown over it. The Commander's ruling was to open the site,
following `CoverageWindow` — which was the only pop-up in the app that had a mark at all, and is now
a caller rather than a third copy of one.

**Help improve D47** gets a page of its own, and the trim is by kind rather than by length: what a
replay case is for and how the scrub decides moved behind the mark, and every statement of *what
leaves and where it goes* stayed. Moving that behind a `?` would weaken the consent the window
exists to obtain.

### Every zoomed dialog stops overflowing by its own margin (#265)

Reported as a help button past the fold, with a horizontal scrollbar on a window that plainly had
room. The mark was a witness: it is docked right, so it is the first thing over the edge.

`ZoomHost.Fit` gave a scaled dialog `MaxWidth = viewport / scale`. A control's `DesiredSize`
includes its margin and `MaxWidth` does not, so a root panel with `Margin(20)` asked for the
viewport plus forty. At 125% on a 1125-wide viewport: `MaxWidth` 900, `DesiredSize` 938, extent 1173
— a 48-pixel overflow, small enough to read as a rounding artefact and large enough to clip the
prose mid-word.

It only happens when zoomed, which is why nothing had seen it: every capture and every test that
shows a dialog directly takes the 100% path, where there is no viewport and no `Fit`.

### The tab strip (#254, #266)

**Engineers wears the game's own Engineer's Workshop hexagon (#254).** #234's *"not a cog"* reasoning
was about not inventing a third gear and still governs Utilities and Settings; it says nothing about
Frontier's own symbol, which is not a gear. The mark carries more weight than it did when the anvil
was chosen, because the strip sheds its words before it sheds tabs.

**And the marks are learnt before the words are taken away (#266).** Word and mark were
alternatives, so a Commander on a full-size window read eight words and never saw the marks — then
the window narrowed and every tab was a picture they had not met. Glyph and word while there is
room, glyph alone when there is not. Nothing about the responsiveness needed changing: the strip
already measures the real extent, so it accounts for the glyph on its own.

---

## 0.99.0 — 2026-09-01 — Three columns, two groups, and a release that asks first

### The flight recorder is the audio recorder (#214)

It borrowed aviation's black-box metaphor inside a product about aviation, so the borrowed sense
and Elite's literal one competed on every read: *"It takes me about 20 seconds to remember that
you're using that in reference to FlightRecorder."* `FlightRow` was the worst of them — one row is
one utterance, and nothing in the word "flight" suggests text, phonemes, a provider, a voice, a
direction and a clip.

`AudioFlightRecorder` is `AudioRecorder`, `FlightLog` is `RecordingLog`, `FlightRow` is
`RecordingRow`, and the rest of the family follows. `D47_FLIGHT_RECORDER` is `D47_RECORD_AUDIO`,
`--flight-recorder` is `--record-audio`, and `flight-on` is `rec-on`.

**The old switch and the old variable still work.** The only things in the field carrying them are
desktop shortcuts made by hand, and dropping them fails the quiet way — d47 starts normally and
simply does not record, which you notice later, looking for a pane that is not there. A run started
by the old name says once in the log what the name is now, because a shim that never mentions
itself is one nobody stops depending on.

**Two places keep the old word on purpose.** The settings key `privacy.audioFlight`, because
`settings.json` is append-only and a renamed key is your own answer silently dropped; and the clips
folder `data\flight\`, because renaming it would orphan recordings you already have. Both say so
where they are written.

**Elite's own flight vocabulary did not move**, which was the whole risk: 293 `Flight` sites across
43 files are two unrelated families, and `FlightMode`, `FlightAssist` and the flight controls page
belong to the ship rather than the recorder. Renamed by identifier rather than by word, mapped
through placeholders once per file so no replacement could re-consume its own output, then read by
hand — which is how the one misfiled case turned up: the three `FlightKey` sites were the tail of
`AudioFlightKey` and belonged to the recorder after all.

### The voice build picker says the wait in seconds, not multiples of realtime (#216)

`uint8 — 169 MB, about 5.4× realtime` is now `uint8 — 169 MB, about 1.3 s before it speaks`.

Reported plainly: *"Nx realtime — that means nothing to me."* It is a synthesis-engineering unit —
seconds of speech produced per second of computing — so the row had to be explained before it
conveyed anything, and a picker row that needs explaining has failed at the only job it has. It
also expressed the good thing as a number going **up** while the thing you actually feel, waiting,
goes down.

The eight builds now read as **1.3 to 4.2 seconds of lag**, which is a difference a person can feel
and choose against. `5.4` against `1.7` is not.

**The hedge is load-bearing and stays.** These figures are less stable than the ratio they replace:
the ordering repeated across every measurement pass and the absolutes did not — the same build timed
×9.0 on a quiet machine and ×4.4 on a busy one — so on different hardware every second here is wrong
by that same factor. Hence *about*, and hence the row's help saying the times are for a typical
spoken reply **on this machine**. A stable number nobody understands is worth less than an
approximate one they do.

### Expand all and Collapse all, beside Show every setting (#223)

A plus and a minus at the top of the card column — **Expand all** and **Collapse all** — on the same
line as *Show every setting*, holding the left where that row's label and switch hold the right.
Both answer the same question about what the whole page draws, so they belong on one line rather
than in two strips a row apart. They stay above the scroller, because a control that scrolls out of
sight is one you learn not to rely on; that is the same argument that keeps them out of the nav
column, which disappears below 900 pixels. Each carries its name on the tooltip and on the
accessible name, since a glyph-only control without one does not exist for anybody who is not
looking at it.

**They were two chevron pairs first, and the Commander asked for the plus and the minus** — a
doubled chevron reads as *scroll* before it reads as *unfold*, where plus and minus have meant open
and shut in every tree control for thirty years and carry no second meaning at all.

**They move cards and leave the fold alone.** *Show every setting* is a different axis: it
decides which rows a calm page shows at all, it is a preference you set, and its own rule is that
folding writes nothing. A chrome button that flipped a setting as a side effect would be a
different kind of act from opening a card — and the two are separately meaningful, since every
card open with the calm row set is a reasonable thing to want. The cost is honest: pressing
Expand all with the fold on opens every card and still does not show every row.

They persist, exactly as clicking each header by hand does. That buries a card's own
`StartCollapsed` default, so **a card's reset now forgets what has been said about whether it is
open** — nothing moves on screen, and the next launch decides again.

### A row's help is behind an info glyph, not under every row

*"That is WAY too much text."* Push-to-talk's help runs to eleven lines, and eleven lines of grey
prose under every row is a page nobody scans — the setting a Commander came for is buried in the
explanation of the setting above it.

Every row with something to say now carries a lower-case **i** in a circle beside its label. Press
it and the words appear in a callout; click anywhere else and the callout goes. **Not a word is
cut** — it is one press away instead of always there.

**An `i` rather than a `?`**, because the question mark is already spoken for on this surface: it
is the card's way out to the documentation site, and two marks that both mean *help* and do
different things is worse than either alone. This one says *about this row*; that one says *the long
form, on the web*. The callout carries **both** — a **Help** link that opens the row's own anchor on
the site, which is a thing `DocsAnchor` has always known and no drawn control had ever offered.

**A search that only the help answers brings the help back out.** The filter has always tested the
help text, and it is no longer on screen — so without this a row could survive a query with every
visible word on it disagreeing, which reads as the filter being broken rather than as a match the
Commander cannot see. The same rule, and the same reason, as the settings key already had.

**The warning line stays where it was.** A row's hazard is the one sentence on it that must not read
as background, so it is not something to put behind a press.

**And it broke seven tests, which is the useful part.** Each had learned to exclude the reset glyph
by name while taking the first button in a row; a second chrome mark broke all of them, and a third
would have again. `IsRowChrome` is the question every one of them was actually asking.

### Five smaller things on surfaces you look at every session

**The reset mark is redrawn.** The three-quarter arc with its arrowhead folded back over the gap
read, at fourteen pixels, as a comma with a tick on it. It is a fuller turn with the head clear of
the arc — chosen from seven drawings, which is the only instrument that can settle whether a mark
says *undo*.

**The power bar labels its own points.** The white tick marks the retracted draw and the blue fill
is the deployed one, and the words naming the tick sat at the far left of the line beneath, joined
to everything else by interpuncts — so the Commander who asked for that tick could not remember what
it marked. The retracted share is written under the tick now, the plant's limit under the end of the
track, and the total draw under wherever it lands. Each label *ends* at its point rather than
centring on it, because the draw may be over 100% and would otherwise be written past the end of the
track it describes.

**Output device leads the Speech card.** Every other row there is about *how* D47 sounds; that one
is about whether you hear it at all. It also caught a real coupling: the provider key rows were
inserted at a fixed index that meant *after the provider* only while the provider happened to be
first, so moving anything above it would have put every API-key row above the provider that needs
them. They are inserted after the row they belong to now, found rather than counted.

**The data-folder strip is off the foot of the Settings tab.** It was permanently on screen behind
every card to answer a question asked once. The About card already named the folder and could not
open it, so it takes the sentence and the button — About stopped being down there when it became an
area in the nav (#50), and this is the rest of that move.

**And a row's words sit level with its control.** Measured: a 17-pixel label centred at 8.5 against
a 32-pixel switch centred at 16. The control had always been centred and the caption was stretched,
so the words sat at the top of a height the control set. Invisible until the help moved behind a
glyph — a caption that was a label over two lines of grey prose filled the row, and there was no
spare height for anything to float in.

### The conversation box walks what you have already sent (#224)

Up steps back through the lines sent from the box this session, down steps forward again, and
**stepping down past the newest gives back the half-typed question the walk interrupted** rather
than emptying the box. That is the detail most implementations miss and the one you notice:
losing a draft to a stray arrow press is worse than having no history at all.

Up stops at the oldest instead of wrapping — a long history that quietly returned to the newest
reads as the key having missed. Consecutive duplicates collapse, blank sends are never
remembered, and editing a recalled line leaves the entry as it was sent: the edit is a new draft,
not a rewrite of what you asked. Sending ends the walk, so the next Up starts from the newest.

**Enter is answered first and returns**, because a history walk that ever swallowed a send would
be a much worse defect than no history. The list lives on the view model, which is where the box
and the send button meet — history kept in the key handler would miss every line sent by
clicking — and it is **not written down anywhere**: the transcript is the record kept on purpose,
and a second copy of your questions in `data\` would be a new place for that text to be that
nobody asked for. It dies with the process and is capped at two hundred lines.

### The spend window is three columns: label, amount, and where the money went (#226)

Money and detail used to share a cell. The voice line read as three facts wrapped into a
paragraph with the figure buried in the middle of it, while a running total held `$0.0196` in a
column three units wide and nothing else.

They are separate columns now, the amounts right-aligned in the mono face the window already
uses, so a column of figures reads as a column. The third names **every model and every voice
provider used in that period**, most expensive first — because the column exists to say where
the money went, and alphabetical would bury it.

Nothing new is stored for it. Every row already carried its kind, its provider, its model and its
price; what threw the breakdown away was the query, which summed and forgot. **The breakdown is
computed from the same filtered rows the total is** — after reset marks have been applied — so it
cannot disagree with the figure standing beside it. A breakdown that does not add up to its own
total teaches a Commander to distrust both.

Two things it fixes on the way past. A **free provider is named** rather than implied: Kokoro
appears with what it spoke and `(free)`, because a provider that was used and shows nothing looks
like one that was not. And an **unpriced model can finally say which one it is** — the window's
old caveat was *"part of it unpriced"*, which never said what part.

**A cost figure is never truncated.** The first capture of the new layout came back reading
`$1.5021 — $1`: the money column does not wrap, and the model-and-voice sentence had been put in
it. The column holds one amount now, and the split is not lost — the details name each provider
with its own figure, which is more than the sentence said. A window holding a model with no rate
marks its figure as a floor and lets the details say which model.

### Turn, session and today read together; each calendar window sits beside its rolling twin (#227)

*What this has cost* was five sections down a page and five windows in an order that interleaved
calendar and rolling by accident, so the two readings of "a week" sat three rows apart.

It is two groups now. **Now** holds the turn, the session and today — one row each, which is what
the details column made possible — because those three answer one question: what is this costing
me right now. **Running totals** holds the four a Commander compares, paired: *This week* beside
*Last 7 days*, *This month* beside *Last 30 days*. On the 3rd of a month a pair differs by an
order of magnitude, and adjacency is what makes that legible rather than alarming.

Which windows belong in which group lives beside the order that arranges them, rather than being
a slice the dialog takes off the front — a list re-ordered later would otherwise move a window
into the wrong group with nothing to notice.

**Cold prefixes keep a row of their own.** It is the one item in this window that asks the
Commander to *do* something — a count there means something is defeating the cache — and folded
into a details cell beside token counts it would be something nobody reads.

### A release run asks whether it may push before it commits anything (#93)

`release.ps1` pushes `main` directly and pushes a tag, and a branch or tag rule refuses either —
the first *after* the commit and the merge, the second after the CI wait as well. That is the
same late-failure shape the version check was moved to the front for, and nothing checked it.

So there are three questions asked before anything is mutated now, where there were two: the tag
not existing, an annotation being available, and **the remote being willing to take the push and
the tag**. A rule that would refuse either stops the run where nothing has been changed, and says
which rule it was. It asks GitHub and never the console, so an unattended `-Yes` run still cannot
hang.

**The pipeline is unchanged, and that is the finding rather than an omission.** The two rulesets
configured here — deletion and non-fast-forward on `main`, deletion, non-fast-forward and update
on `refs/tags/v*` — are all about *moving and deleting* refs. That is this repository's own rule,
*a published tag never moves*, written where GitHub enforces it, and none of it stops creating a
tag or pushing a merge commit.

**The first draft would have refused every run.** It listed `update` as a tag blocker, which
reads right and is wrong: creating a ref that does not exist is governed by `creation`, while
`update` governs moving one that does — and `update` on a *branch* really does stop a push, so
the asymmetry is easy to miss. Driving it against this repository caught it; both halves are
pinned by tests now.

**And a refused push puts `main` back.** The preflight reads rulesets, so it cannot see classic
branch protection, a permission changed since it asked, or a rule added while the suite ran. In
that case `main` returns to where the run found it and the checkout returns to the work branch,
which still holds every commit — nothing lost, nothing to unpick, and the run still fails.

The release workflow was checked separately, because it runs under a different token: it triggers
on the pushed tag, holds `contents: write`, and creates a Release without ever pushing, updating
or deleting a ref — so a ruleset on `refs/tags/v*` has nothing of its to refuse.

### A long upload draws a bar, and the sentence beside it stays (#212)

A journal history is up to 356 MB, and it moved behind one static line reported **once**, before
the request began — which is what a hang looks like, and it is the longest and least reversible
step in the feature. `DonationStep` carried a boolean and a file count, so even a window that
wanted to draw a bar had nothing to draw it from.

It carries bytes and a total now, and `MeteredStream` — the read-side twin of `TallyStream` —
reports the spool's position as the body goes out. The window draws the fraction in the shape
`SettingsView.RunPressAsync` already uses, and the Cancel button that was always there is now an
escape a Commander can tell they have.

**The sentence is kept rather than replaced.** *"Nothing else is being sent, and nothing is being
kept anywhere else"* is doing work about scope that a percentage cannot do, so the bar sits under
it. The number beside it says **compressed**, because the report above states the history's own
size and the two differ by about twelve to one — a figure counting to 32.5 MB with nothing to
explain it reads as most of it having gone missing.

**What it measures is bytes handed to the network stack, not bytes the store acknowledged**, and
nothing on this side of the socket can know the second one. So the bar goes the moment the send
returns, whichever way it went: the outcome is what says whether the donation landed, and a full
bar standing over *"the endpoint refused it"* would be the one claim this path must not make
loosely.

**A late progress report is dropped now**, which was the same defect one layer up. `Progress<T>`
posts, a send can complete without ever yielding, and the losing order put a finished-looking
status line on top of an outcome that had already been read.

The excerpt send is deliberately unchanged: a few kilobytes would draw a bar that was gone before
anybody saw it.

### The creative calls and the mechanical ones stop sharing one unchosen sampler (#98)

There was **no `temperature`, `top_p` or `top_k` anywhere in `D47.Llm`** — not on the Anthropic
path, not on either OpenAI-shaped one. So the character of every line d47 spoke was decided by a
default it had never chosen, which varies by provider and by model, and a flat-reading Guardian
core could have been a sampling artefact rather than a persona problem with no way to tell.

`LlmSampling` names the classes d47 already had and states each one's value and its reason. A turn
and a line in character ask for **0.9** — the middle of the band reported for character writing,
where below about 0.7 reads flat. Voice casting, the Commander's log, a lore lookup and adventure
generation ask for **nought**: each is either a mechanical question about d47's own configuration
or an answer validated against the world afterwards, and warmth in those buys invention in the one
field that has to be exact. `LlmRequest.Sampling` is **required**, because a property with a
default is how this defect would come back one forgotten line at a time.

**Adventures are cold although what comes back is a story**, and that is the call rather than an
oversight: the beats are checked against the real galaxy and re-asked where they cannot stand, so
this call's observed failure is naming places that do not exist. The variety comes from the systems
within reach, which are different every time.

**One call says nothing on purpose.** The key check asks a single token in order to learn whether a
key works, against a gateway that may validate fields d47 has never met — and a rejected field
there reads as a rejected key, sending a Commander to their account page for another one that will
fail in exactly the same way.

**And the field does not go to Anthropic at all, which is the finding rather than an omission.**
The issue opened expecting it to; sampling was removed with the 4.7 generation and returns a 400 on
Opus 5, Opus 4.8, Opus 4.7, Sonnet 5 and Fable 5. The pinned SDK says so itself — its `Temperature`
property is marked obsolete with *"Models released after Claude Opus 4.6 do not support setting
temperature"* — so reaching the three older models that would still take it means suppressing a
deprecation warning in a repository where warnings are errors, to send a field to models this code
already treats as legacy in two other lists. It is read and not sent, with the date and the reason
written where somebody will look. Where the choices land is the OpenAI-shaped paths: OpenAI itself,
gateways, and every local runner.

A server that refuses the field loses the field rather than the turn, through `EndpointDemotions`
like everything else optional — and **the refusal is read as sampling before it is read as
effort**, because a reasoning model rejecting temperature usually names both in one sentence, and
taking the wrong one off would drop the effort router's lever while leaving the refused field on
the retry.

### Every ship you have flown is remembered in a file, not for 25 journals (#128)

The memory shipped in v0.41.1 off the report *"It WAS seen. Don't get amnesia"* — and it was
rebuilt at every start by replaying the newest **25 journal files**, so a ship not sat in inside
that window was forgotten on the next launch, and re-forgotten on every launch afterwards. That is
the same amnesia, one level below where it was fixed.

`data\loadouts.json` is the long memory now: every ship, keyed per Commander on the Frontier id
with the key inside the document, written when the picture changes rather than only on a `Loadout`
— Elite writes none after engineering, so a store that waited for one would miss every roll. It is
**a cache and not a source of truth**, which is said in the file's own doc so nobody treats it like
`settings.json`: deleting it costs a rebuild from the journals and nothing else. It is **not
`ships.json`** either — that holds the plan, and intent is authored where a loadout is derived.

**The backfill keeps its job and changes meaning.** It is seeded with the file rather than
rebuilding over it, so its 25 files became *catch up on the gap since d47 last ran* — and that
seeding is what makes forgetting work across a restart, because the sale is replayed through the
same fold the live path uses.

**And the gap is measured rather than guessed at, which closes the one hole this feature had.** A
sale d47 was closed for is not lost — the `ShipyardSell` is still on disk in an older journal —
but the walk had no way to know how far back to look, so a sale older than 25 files survived
forever under an id the game may since have handed to something else. The file carries the journal
timestamp it was folded through, and the catch-up reaches exactly that far and no further: 25 files
stays as the **floor** for a run with no watermark to work from, and there is no ceiling.

**Unbounded because it was measured, not because nobody thought about it.** The cost is
proportional to how long d47 has been away rather than to how much history exists. On the
943-journal, 382 MB corpus: the whole of it — 716,653 events — reads and folds in **3.1 seconds**,
300 files in 996 ms, 100 in 432 ms, and the 25-file floor in about 190 ms. A run an hour after the
last one still walks 25 files. A cap would buy a fraction of a second back on the rarest start
there is and put the hole straight back.

**And the Commander can run it themselves — *"not look right? Do a rescan"*.** The Ships card in
Settings says how many ships are remembered and how stale the oldest of them is, and offers
**Rescan my journals** with a bar behind it. Where the catch-up at startup is *seeded* with the
file so a session lands on top of what is known, a rescan **throws the file away and derives the
answer again** — so a ship nothing in the journals supports stops existing, and one that has been
sitting there wrong is put back the way the game described it. That is what makes it a repair
rather than another pass of the same thing.

**The offer is made where the doubt is, not only where the fix is.** The Loadout page already says
*"As you left it, three months ago"* on a ship you are not in — the one line that admits the
figures may be out of date — so the nudge sits beside it. A repair nobody can find is a repair
nobody performs, and the settings card is not where a Commander is standing when they notice.

**A rescan that read no journals changes nothing**, and that guard is the whole difference between
a repair and a wipe: a journal folder that has moved reads exactly like a fleet that has genuinely
been sold, and only one of those should be believed. The press reports the file count for that
reason rather than the ship count. It is *Info with a press*, a shape `SettingsService.Apply`
refuses to write, so nothing on the tool surface can start minutes of disk reading with a sentence
somebody put in a chat channel.

**Forgetting is the half a file makes load-bearing, and it now happens on three events rather than
one.** A rolling window expired a stale row by itself; a file does not, and `ShipID` is reused —
17 of 55 sold ships had their id come back alive. Measured on the 943-journal corpus: `ShipyardSell`
names `SellShipID` on all 72 of its occurrences; one `ShipyardBuy` of 34 carries a `SellShipID`,
which is a part exchange — a sale wearing another event's name, and previously missed; and
`ShipyardNew` names `NewShipID` on all 34, with **12 of the 34 reusing an id that had already been
alive**. A purchase is unambiguous proof the id belongs to something else now, so it is a second
and independent chance to forget a sale d47 was not running for.

**And the fold remembered before it forgot, which was one measurement away from being wrong.** Of
those 34 purchases, **one hands out the id of the ship the Commander is sitting in** — so filing
the flown ship after the forget put the old ship straight back under the new id. Replayed over the
whole corpus, the shipped order produces exactly one inheritance (a Sidewinder's modules surviving
under a newly bought Cobra Mk V on 2025-12-30) and the corrected order produces none. Harmless
while the window expired it a day later; permanent once there is a file.

**The fleet snapshot is not the corrective #128 expected it to be, and the corpus is why.** The
issue proposed reconciling against `StoredShips` for a sale outside the catch-up window, noting the
trap that the flown ship is absent from it. The trap is not the only problem: simulating that rule
over all 1,112 snapshots would have **wrongly forgotten 140 ships across 50 snapshots** — ships a
later snapshot proves were still owned, with no sale in between, one snapshot alone accounting for
eight. 54 snapshots are empty, and one sequence runs 7 ships → 0 → 14 inside 90 minutes. So a
snapshot deletes nothing here, and the acceptance criterion *"a ship absent from a fleet snapshot
while being flown is not deleted"* holds by there being no such path at all.

**Half of #128 is deliberately not built.** Asking about another ship by name through `get_ship`
is tabled under the 2026-08-29 moratorium, on the Commander's own note on the issue.

### A misheard name asks, retries, and is remembered against the word (#134)

*"How far are we from Eurebia?"* — *"I don't have a system called Eurebia on record, Commander.
Could be a misspelling."* That sentence is not a fixed string, it is the model narrating a bare
nothing politely, and **a polite dead end is still a dead end**: the Commander has to spot the
mishearing themselves, work out the spelling, and say the whole question again.

**Proper nouns are where speech recognition fails hardest and most silently** — a misheard system
name does not arrive as an error or a low-confidence marker, it arrives as a plausible English word
and the answer is confidently about the wrong place. `ProperNouns` biases the transcriber against
exactly this, but it holds sixty names and the galaxy holds four hundred billion, so a name the
Commander has not been near lately could never have been on it. Biasing cannot cover the galaxy;
the recovery path had to exist.

**The catalogue the galaxy does not have is on the Commander's own disk.** Every system they have
jumped to, station they have docked at and faction they have met — measured on this corpus at
**15,216 distinct names** (4,829 systems, 968 stations, 9,422 factions) in about 300 KB, mined from
943 journals in seconds. And the reported case is in there: *Eurybia* **and** *Eurybia Blue Mafia*
are both names this Commander has met, so the mishearing that started this was recoverable from
their own history all along. Nothing leaves the machine to do it.

**Edit distance is the wrong model for a transcriber, and that was measured rather than argued.**
Typing errors are near in spelling; hearing errors are near in *sound*. Over the real catalogue,
`Eurebia` → *Eurybia* is one edit and both rungs find it — but the issue's own counter-example,
`"Dessy at"` → *Deciat*, is **four** edits: `Catalogue.Near` returns nothing at all, and a phonetic
rung returns exactly one candidate, the right one. So `NearSpoken` adds sound-alikes below the two
precise rungs and ranks them by spelling, which puts *Jameson Memorial* at the head of the five
things that key like `"Jamison Memorial"`. **A limit, recorded rather than papered over:** it keys
the whole name, so a transcriber that moved the word boundaries defeats it — `"shin arta desha"`
finds *Shinrarta Dezhra* under neither rung, which is the case biasing is still for.

**The confirmation is the retry, and it needed no new dialogue machinery.** The failing lookup hands
back a sentence that invites a correction rather than a bare nothing; the Commander says which; the
model re-runs the same tool because that is what it was asked to do. What is left is noticing that
the second call succeeded where the first failed — and **learning only from a name that actually
resolved**, never from one d47 offered, because an offer is a guess and a resolved lookup is the
Commander having steered it there. One retry, then it asks for the letters: a correction is itself
spoken and can itself be misheard, and two people who cannot hear each other repeat themselves
indefinitely.

**What is remembered is the word, not the answer** — the Commander's own instruction, and the part
that carries the design. Correct it once about *Eurebia* and *"who runs the Eurybia Blue Mafia"*
comes out right too, because the rewrite happens to the sentence before routing and before any tool
call rather than to one argument afterwards. An alias against the *system* would have fixed one
question.

**A wrong alias is worse than the mishearing it fixes** — permanent, invisible, and quietly
rewriting every later sentence containing that token — so the guards are in the store rather than in
a caller's memory. Never a word that already means something: a place this Commander has met, a
phrase the keyword router answers to, or anything short enough to be an English word by accident.
`Eurebia` is capturable precisely because it is not a word. Whole tokens only, so an alias for `Sol`
cannot rewrite *solid*. **Spoken input only** — a Commander who types a word can see what they
typed. And nothing is learned from what another player wrote: the catalogue reads named place fields
rather than scraping the event, so a chat message naming a system contributes neither its sender,
its words, nor the system it named.

**Local, per Commander, readable and clearable.** `data\heard-names.json`, keyed on the Frontier id
inside the document, with a row on the Listening card that lists what has been learned and a
**Forget them all** beside it — an alias table that cannot be read is a mystery generator. Clearing
the corrections leaves the names, which are not a claim about anything.

**Half of #134 the issue asked to be measured, and half of #126 it asked to be shared.** The
faction-name spell-check #126 wants is the same catalogue; #126 is tabled, so this builds it once
and leaves it there for when that is untabled.

## 0.98.3 — 2026-09-01 — A refused raise names its fault

### Bringing Elite forward works from behind it again, and a refusal is now a diagnosis (#107)

Phase 27's `AttachThreadInput` fix failed in the field — from the headset, on the Commander's own
ask — and the log's one sentence covered four different faults: *"Windows refused to bring Elite
forward, with and without attaching."* Which fault it was turned out to be the whole question, so
the raise now answers it. A failed attach carries its Win32 error; an attach that engaged says
whether it reached Elite's own window thread or the foreground's only; and a refusal names what
held the foreground — handle, title, class, owning process and thread, and whether that thread was
answering messages, which is the one documented failure mode no log line showed.

Three things changed underneath the words. **The foreground checks compare by owning process
rather than exact handle**: where injected keys land is a property of the focused process, so
VR-Elite presenting a different top-level window of its own is still Elite — and if that was the
whole story, the old code was raising Elite successfully and reporting refusal anyway. **Two more
legal roads try before the refusal sentence**: the attach joins both threads, and alt-tab
emulation goes last — no synthesised input, nothing machine-wide, Elite never minimised, and the
route stays reachable by spoken phrase only. **And the raise moved off the UI thread**, dropping
its input-queue attachment before it verifies the landing, so a refused raise can no longer stall
the window the Commander is working in, or the game.

Reproduced knowingly on the desktop before shipping: without the ancestry exemption the plain call
is refused and the attach road carries it. The Commander reports the raise working; if Windows
ever refuses again, the log now says which step lost and to whom, and the honest sentence — the
flashing taskbar button — still stands behind it all.

## 0.98.2 — 2026-09-01 — The carrier's own two voices, and a jump nobody is making

### Engineering two trackers describe differently is withheld, not guessed at (#127)

A Guardian Gauss Cannon read as having no engineering. It has one blueprint — Anti-Guardian Zone
Resistance — and **no source d47 reads carried the recipe**: EDEngineer has no Guardian blueprint
symbol at all, coriolis has none either, and FDevIDs has no row for any of its three materials, by
display name or by symbol. Nor do 941 journals across three Commanders. Phase 38 shipped the
honest sentence instead, and this was its one unfinished item.

A second tracker — ED Odyssey Materials Helper, whose material table is MIT — turned out to name
all three material symbols exactly as EDSY does, arrived at independently. The two agree that the
blueprint exists, that it is Ram Tah's, that it has one grade, and on two of its three
ingredients. **They disagree about the third**, and the source asserting it is malformed in
exactly that spot.

So d47 says nothing about it. On the Commander's rule — *"if the two trackers don't agree on an
engineering item, remove that from d47's offered engineering"* — the blueprint is dropped from the
offer table itself, not merely left uncosted. **A recipe missing an ingredient is the worst of the
three states available**: a Commander gathers exactly what d47 asks for, flies to the workshop and
cannot roll, and nothing on the page ever suggested the list might be short. An offer with no
recipe is still a claim, and this is a blueprint d47 cannot describe consistently.

**So the module page has a fourth thing to say.** A module can take no engineering, or take some
d47 can cost, or take some no source prices — and this is a fourth: *"Frontier engineers this
(Anti-Guardian Zone Resistance), my two sources disagree about what it costs, and I will not
guess."* Without it the Guardian FSD Booster, whose only blueprint this is, read as a module
Frontier does not engineer — a claim about Elite rather than about d47, which is the distinction
this whole area exists to draw.

It is a sentence and not a recipe, kept in its own section of the table and reachable through
nothing that costs, plans or gathers. All d47 does with it is say that it is there.

**The three materials stay, and are new.** Hardened Surface Fragments, Caustic Crystal and
Tactical Core Chip are named, categorised and capped now, at grades read from their capacities
through Frontier's ladder and validated against the 16 Thargoid materials in the same screen that
FDevIDs *does* key — the capacity agrees with the published rarity in all 16. That overrules EDSY
on Tactical Core Chip, which it calls grade 2 against a capacity of 100. A Commander who gathers
one is told what it is and what it counts against, whatever happens to the blueprint. They live in
`tools/curated_materials.py`, read by both generators that build a material table so the two
cannot disagree, and **each retires itself** the day FDevIDs names it.

The same regeneration picked up what the sources have added since August — Plasma Conversion on
three laser types, an enhanced fuel-scoop rate, Heavy Duty on module reinforcement, and a new
commodity. Those are offers with no recipe behind them yet, and the page says so in the usual way.

### The engineer filter shows what that engineer does, not what your rank lets you have (#205)

An 8A power plant planned for **Armoured G5** and **Thermal Spread** showed only Thermal Spread.
Both lines were on the list the whole time: the blueprint sat in the out-of-rank band, which the
page did not draw and no control readmitted, so the one line the Commander went to that workshop
for was the one line they could not see — and the effect beside it read as a stray errand.

Rank gates a graded blueprint and never an experimental, which is Frontier's rule and correct.
What was wrong is what the page did with it. **The engineer filter now shows every line that
engineer works on, whatever your standing with them**, on the Commander's ruling: *"I don't care
if I have attained the correct rank with an engineer. I still want to see it, because what the
engineer can do is what I'm after, not what can I do based on my relationship with the
engineer."*

Visible and explained rather than visible and misleading: the line says *"Lei Cheung rolls this at
grade 3, and you are grade 1 with them"*. That sentence has to come from the filter rather than
from the line's own verdict, because the verdict reaches a rank through the engineer the *plan*
names — and a build typed against a hull rather than against a workshop names nobody. Standing in
the engineer's system is what supplies the missing name.

There is no switch beside it. Partial grades keep theirs, because *"somebody else has to finish
this"* is a genuinely different question; a rank is not a different question, it is the same work
with a date on it.

**The test matrix is the other half of the fix.** The three tests for a module carrying both a
blueprint and an effect all flew at rank 5, where the gate cannot fire, and the one rank test had
no effect beside it. Two features, two disjoint axes, and the defect lived in the cell neither
covered. That cell is now a test, and so is the drawn page.

### A hold lets go the moment Elite stops being the window in front (#206)

d47 could hold a modifier key physically down for **5.3 seconds, autonomously, with nothing
asked for**: the discovery scanner's charge on arrival, with whatever modifiers the Commander's
own bind carries underneath it. Every shortcut pressed during that window reached Windows as a
different chord and silently did nothing — Win+Shift+S with a stray Ctrl under it matches no
shell hotkey, nothing is logged, and pressing it again a moment later works perfectly, which
reads as flaky Windows rather than as d47.

The injector has always re-checked the foreground between steps, and a hold is **one step** — so
alt-tabbing mid-charge did not shorten it. The wait now watches while it waits, on the same two
questions the step loop asks: Elite in front, and the Commander in the game. Either going false
ends the hold within about fifty milliseconds and the release that already ran in a `finally`
runs there. The charge itself is untouched; 5.3 seconds is what the scanner needs.

The watch waits whatever is left of the hold rather than a fixed slice, so a hundred wakeups
cannot add up to a longer charge than was asked for. And the injector is now disposed on the way
out — it was a local in the composition root that nothing ever disposed, so the exit release its
own summary calls "the last chance to let go" had no caller. Two of the three safety nets that
class documents were prose; both are real now.

Left for a drive rather than guessed at: whether a long hold could re-press instead of holding.
That is a question about how Elite reads a chord over time, and a wrong answer breaks the honk
silently.

### Overheard chatter aboard your carrier speaks in the voices you cast for it (#249)

Invented chatter (#244) puts a controller on the radio whenever the Commander is docked, and
where that dock is their own fleet carrier the controller *is* their tower — a post they have
already chosen a voice for. It came out in a pooled per-system stranger's voice instead, so the
same person spoke in two voices depending on whether the line was authored or invented, and the
cast voice was the one that lost.

The rule it broke was a real one and still is: an invented speaker is an invented nobody, and a
cast role overrides the per-sender draw (#109), so handing roles out freely would put the
carrier's voice in a stranger's mouth. **So the exception is scoped to the two posts that are not
invented.** While the Commander is at their carrier — set down on its deck, or sharing the space
around it — the model is told those two speak under the exact names *Tower* and *Captain*, and
`NpcChatter.Parse` hands those lines back carrying `VoiceRole.TowerControl` or
`VoiceRole.CarrierCaptain`. Everybody else in the exchange is still nobody, in the pooled voice
they always had. *Captain Reyes* is a pilot with a rank and does not match.

**Docked at anything else disqualifies it**, which is the whole reason the test is not "same
system". A carrier parked over Jameson Memorial while the Commander sits on a pad inside the
station is near their carrier and being talked to by the *station's* tower; casting that tower as
their own would be the same fault pointed the other way. Supercruise and hyperspace are not that
space either.

### And nobody invents a departure it is not making (#249)

The carrier's people had been discussing a jump that was never scheduled. d47 knows better —
`CarrierState.JumpScheduled` and the destination the game state already carries — so with no jump
on the books the instruction says outright that it is going nowhere and that nobody may say or
imply otherwise, and a line that says so anyway takes **the whole exchange** with it rather than
just itself. That is stricter than the unsayable-line drop above it on purpose: a refusal removed
leaves an exchange that still makes sense, while a fabricated departure is the subject of the
scene replying to it. The rule holds wherever the carrier is parked, since the live game state
names it either way, and a freighter crew's own jump is still their own business.

## 0.98.1 — 2026-08-31 — What was heard is what was said

### Barging in drops the pre-roll, which held d47 and not the Commander (#195)

Talking over d47 came back with a junk syllable behind Whisper's speaker-change hyphen — *-Huk,*
*-Shet course* — measured four times likelier on a barge-in. The pre-roll ring the gate drains on
opening holds up to half a second captured **while d47 was rendering**: post-cancellation residue,
landing at the front of the utterance where the first-token bias is strongest. The gate has known
d47 was speaking since Phase 11 — `FarEndActive`, live-wired, consulted only by the continuous
modes — so opening now leaves the ring alone when the far end is active. A Commander pressing to
interrupt starts speaking at or after the press, which is why the pre-roll's job — the word from
before the key was noticed — does not exist in that moment. With d47 quiet, nothing changes.

### A word hallucinated from silence is refused by an unprompted second opinion (#196)

`medium.en` answered mouse clicks with *you* and a silent clip with *Fop.*, and nothing stripped
either: letters, no brackets. Both obvious fixes were already measured dead by
`spike/NoSpeechProbe` — an absolute energy rule fails because real speech peaks overlap room
tone, and the prompted pass's own no-speech signal is destroyed by the very name hints that cause
the hallucination. What separates the populations is an **unprompted tiny.en pass**: real speech
0.017–0.26 against room tone 0.946–0.958, flat at about 350 ms whatever the clip length.

So the transcriber carries a no-speech probe — tiny.en beside the loaded model, promptless, on
its own semaphore — run in parallel with the prompted pass so the gate costs nothing in latency.
A non-empty transcript the probe scores past 0.6 is emptied, so every downstream path handles the
established nothing-heard shape rather than a new one; the flight recorder keeps what Whisper
actually said first, which is how a wrong refusal would be caught. No tiny.en on disk means no
gate and one log line saying so — never a refusal.

## 0.98.0 — 2026-08-31 — The crew knows whose deck it is

### Docked is the ship secured, and the deck answers as two people (#220)

The tower said *"docking granted"* at the moment docking **completed** — words written for an
event minutes earlier, and a sentence Elite's own carrier message had already delivered through
the same re-voiced tower voice. The `Docked` moment is now the inbound pair's exchange shape
applied to the deck: the tower acknowledges the ship secured — the thing `Docked` actually is —
and the captain makes it a welcome on a key of his own, so one cooldown cannot swallow the other
half. The arrival pair also stopped being a rewording brief: *"drinks are on me"* and *"I'll meet
you in the hangar"* are not rewordings of each other, so the brief names a situation and a range,
bans invented facts outright, and hands the authored line over as the register sample. The
authored lines still read well alone, because they are what is spoken when there is no model to
ask — and `DockingGranted` itself stays silent, by test.

### Commander DeParagon, never Commander John DeParagon (#247)

Rank and surname is how a crew addresses its owner; rank plus full name is how a form letter
talks. One helper holds the rule — the last word of the journal's name, casing untouched because
*DEPARAGON* cannot be re-cased without guessing — and the spoken sites route through it: the
carrier crew and the session greeting. The drawn journal records deliberately do not, because a
reading is a faithful record of what the event said.

### The carrier and its patrol speak to the owner, not to a visiting pilot (#248)

Canned comms from the Commander's own carrier ride a rewording brief now: varied by the model,
addressed to the owner by surname, the boilerplate's facts kept and nothing added. The same road
for a **System Authority vessel** while the Commander shares a system with their carrier —
courteous to the owner whose assets the patrol is protecting, subordinate to nobody, and a clean
scan said without the canned *"…this time"* taunt. The COMMS page keeps the original as sent, on
the Commander's instruction; the voice is what changes.

**The trust rule bends nowhere.** In-game text reaches a model only when the `Message` field
carries Frontier's own `$…;` localisation key, which no player can write — the same trust class
as a journal event's schema. Free text keeps the verbatim road whoever sends it, because a sender
is a name and a name can be worn; the model-guard's exemption is scoped to exactly the two keys
the reader mints under that proof, and the fallback with no model is the localised line spoken
exactly as before.

## 0.97.0 — 2026-08-31 — Invented voices, and the store goes live

### Phase 61 — Invented chatter: people who do not exist, talking on the radio (#244)

**The galaxy now mutters.** Now and then — twenty to forty minutes apart, never on a fixed tick —
an exchange is overheard: two crews on the local channel about their own small business, the dock
telling an invented pilot off while you are docked somewhere, and about one exchange in four, a
line or two said to *you* over the open channel. Statements only, never a question, and **nothing
here is ever answered**: the lines ride the comms voice with invented speaker names, so each gets
its own pooled per-system voice, and none of it enters the conversation, the comms record, or a
prompt. Theatre, heard once.

**This is not the game's own NPC traffic** — Elite's real messages are re-voiced under Speech and
are somebody else's words. Invented chatter is d47's own fiction on its own switch, and the two
stay distinguishable on the settings page. The callout emits an empty-text marker on the ambient
timing rules; the app composes it with the background model against where the Commander actually
is; the reply is parsed strictly — `Name: words`, at most four lines, a fragment is silence — and
a model given a hunter's freedom is still bound: no real people, no players, nobody asking the
Commander for anything. The gap between exchanges is dealt deterministically off the pick counter
inside the least/most range, because no Core component reads a clock or a seed and a recorded
session has to replay to the same spacing.

### Chatter is model-written or it is nothing (#245)

**An ambient remark the model did not write is not spoken.** The authored lines stopped being an
understudy and became what they always should have been: a tone sample the brief shows the model,
and nothing more. Every road to "no model line" — no provider, the three-second budget, a refusal,
a line about itself — now ends in silence rather than a stock sentence, enforced at the one drain
everything audible passes through. With no language model configured the chatter rows are absent
from Settings entirely, because a switch governing something that cannot happen is a switch that
does nothing. The carrier captain and tower keep their authored floor deliberately: they are
rewording briefs over d47's own lines, and whether the rule reaches them is left for the Commander
to rule on.

### An ambient remark speaks to the Commander instead of narrating (#222)

*"Kuwemaki hangs under us in normal space, a steady pull through the dark"* was the model doing
exactly as told: the brief asked for *"a remark about where the Commander is"*, which made the
place the subject, and a place described in the third person is a novel's narrator. The
instruction now names the addressee in its first sentence, bans scene-setting beside the three
interaction bans it always had, and hands the authored stock line over as a sample of the register
rather than a script — still a composition brief, and now one that knows what it is composing.

### A little humor, when the Commander switches it on (#243)

**"It's so serious all the time."** A toggle in Persona adds one line after the standing
instructions — permission for an occasional dry aside in each core's own character, never at the
Commander's expense, never inside a warning. Off is the shipped block byte for byte. The first
drive taught the second half: the model took the permission and reached straight for *"like it's
waiting for applause instead of fuel"*, so the bans are hard rules now — no similes or borrowed
images of any kind, no puns, no whimsy, no exclamation marks, no comic ellipses — with the escape
route named at the moment of temptation: cut the comparison and let the plain fact land dry.

### Help improve D47: one window, one button, why-first, and the journal scale (#238, #239, #240, #241)

The two Donate buttons and their two windows are one of each, and the name says what it is for —
**"Donate" read as a request for money or a kidney**, on the Commander's own account. An *Include
journal history* toggle decides which flow the one window runs, and the merge is of the surface,
never the consent: the excerpt keeps its read-the-payload yes and closes on the copy, the history
keeps its report and its Read step, and the Log page still offers only the excerpt, because a
history is Elite's journals and that page does not show them.

The intro leads with **why** — real journals are how defects get found and fixed — and then the
three promises that were always true and always buried: entirely voluntary, scrubbed before you
ever see the result, removable in one press. The scale opens on the journal scale, gentlest first,
and the widest option says **"Everything"** rather than *"everything on disk"*, because a
Commander reads "on disk" literally. Privacy and egress says *Shared*, not *Donated*.

### The send address ships in the build, and the store goes live

The journal-history store stopped being an address a Commander pastes in and became part of the
build. The truth the change uncovered: **there was never a working endpoint at all** — no custom
domain was ever attached, and the Worker's own hostname was switched off by its config, so the
"paste the address here" row asked every installation a question whose one answer did not resolve.
The route is on now (production only), `wrangler.toml` says so in case a deploy would silently
turn it back off, and the row is retired: the disclosure row always names the destination in full,
and **the press is the whole of the switch** — nothing is sent until the Commander presses, every
time, with Cloudflare's own protections standing in front of the Worker, per the Commander's
ruling. Driven end to end the same night: an excerpt sent, the receipt's hash checked, Forget
pressed, and the store asked directly — *"The specified key does not exist."*

### The main menu is not the game: keys wait for the Commander to go online (#242)

Running and in front were the injector's whole test, and Elite at its main menu passes both — so a
keystroke aimed at a ship landed in a menu. A third refusal sits between them now: **NotOnline**,
when the status flags say the Commander is aboard nothing, on foot nowhere, and a passenger of no
one — with a freshness window, because `IsKnown` is one-way and a status file from yesterday still
reads "in the ship". It re-checks mid-sequence, because quitting to the menu keeps Elite in front
and a macro must not carry on typing into it. One choke point, every road that presses keys.

### The Motion controllers row says less, and wears the warning (#237)

Two sentences instead of seven — the row says what the switch does and what off costs, and the
whole case for the withdrawal stays in #18, #198 and the docs. The hazard is a **warning badge**
now, the first of its kind on a settings row, with the advice stated outright: when you finish
trying it on, turn it back off and restart d47.

### Lavigny-Duval, once a day (#246)

The journal writes *A. Lavigny-Duval* and a voice reading "A." is reading punctuation — the
surname is the name as spoken. And the territory lecture — *"…controls this system, and you fly
for… You are exposed here"* — plays **once per local day, across sessions and whichever core is
aboard**, remembered in `view-state.json`; every exposure after it, whoever the rival, is four
words: *Hostile territory. Be on guard.* That overturns once-per-Power-per-session, on a day of
re-launching that proved the fourth hearing is not information either.

### Fixes from the first drive (#222's sibling reports, unnumbered)

**Nobody says "asterisk asterisk".** A model writes `**bold**` without being asked and a voice
read the markup aloud through a whole docking report. Markdown is stripped where everything
audible converges — speaker, caption and said-record alike — while prose that merely contains the
characters (`snake_case`, `5 * 3`) survives, and the conversation history keeps what the model
actually wrote. **Raw is a switch**, the same ToggleSwitch the settings page taught, with its
label beside the knob rather than wrapped above it. **The bright line under the dialogs** was
Windows 11 drawing its native one-pixel frame border in the system's colour against d47's dark
chrome — told by the fact that only maximising removed it — and the frame now wears the theme on
every window. The sharing window's status line answers the question actually asked at it — which
button sends — and its Save says "instead" only when there is a send to be instead of.

## 0.96.0 — 2026-08-31 — The panel says what it is showing

### A real drop-down for the readings, and the readings say what they are (#231)

**The readings picker was a full-face chooser because of a belief that was half true.** The panel
said a `ComboBox` could not be used: a popup needs a top level, the headset's host window is
constructed and never shown, and opening one there is not a polite failure — it recurses until the
stack is gone and exits at `0xC00000FD`, with no exception and nothing in the log. All of that is
true, and **all of it is true of the offscreen copy only**. The panel is instantiated twice and the
desktop one lives in a real window. The headset was already covered: `OffscreenSurface` takes a
ray-press on any `ComboBox` before a pointer event exists, forces the drop-down shut and draws its
own list. That interception was built so the panel *could* contain combo boxes, and had been waiting
for one.

**The readings are named for what they are.** *Thread* is **Conversation**, *D47 Log* is **Log
File**, and *Journal* is the **Elite Dangerous Journal File** — which had to say whose journal,
because d47 keeps a log of its own and the two were one word apart. *Details* is gone: it stopped
being a differentiator the day the log became a reading of its own, and a stored root that no longer
resolves falls back to the conversation without special-casing, because `SelectRoot` already
declines a root nobody registered.

**A crumb's word had been doing two jobs, and one label finally pulled them apart.** It is the drawn
label *and* the phrase the keyword router matches — one word order for both routes rather than a
label and a synonym, which held right up until a name got long enough that nobody would say it.
`NavCrumb.Spoken` carries short aliases now, so the box reads *Elite Dangerous Journal File* and the
Commander still says "journal". *Thread* still works too: somebody who says it is out of date rather
than wrong.

**Raw Journal is a root the picker does not list.** It stays registered so a spoken "raw journal"
and a switch position that names it still arrive, and a toggle beside the box crosses to it — the
same events seen another way, where two entries read as two subjects. **The toggle is in the headset
too**, reversing the desktop-only position it shipped with, on the Commander's instruction: *"Show
the toggle. If they don't like it they can toggle it off."*

**And the chooser the headset draws follows the theme.** Those colours were literals on purpose and
the reason was recorded: the first cut inherited the theme and came out dark grey on dark grey,
unreadable at a metre. What keeps that from returning is the scrim rather than the palette — the
card is read against an opaque black rather than against the cockpit.

### Search on the journal reading, which never had any (#232)

Typing in the box reported `1 of 371` and the steppers moved nothing. **It was not a broken search;
it was no search at all.** `DrawTranscript` returns before the search runs for this page — its own
comment says everything below that line is about runs of transcript text — and everything below that
line *is* the search. The count was a leftover from whichever prose page had been read last,
rendered against a list that knew nothing about it.

A list filters, the way the checklist and the engineer directory do, and the count then means what
it says. **The event kind is matched as well as the drawn line**, because the thing being hunted is
frequently the event's own name and `ShieldState` appears nowhere in *"Shields back up"*.

### Newest goes to the newest (#233)

One hard-coded line — `FollowButton.Content = "↓ Newest"` — against two readings that are written
newest-first. On Raw Journal the button travelled to the far end of the file; on Journal it moved a
scroller nobody could see, because that reading takes the pane with a list of its own. Direction and
scroller both follow from the page now, and the arrow points at where the newest line actually is. A
label that names a direction has to be right about it.

### Tabs shed their words before they shed tabs (#234)

Three stages, in order: **words, marks, then marks that scroll**. Scrolling was the right last resort
and was the only one — on a narrow window whole tabs went off the end while the ones still visible
carried full words, which is the wrong thing to spend the width on. A Commander can recognise a mark
and cannot click a tab that is not there.

Eight marks, each chosen from four drawn candidates, and three were arrived at by rejecting something
more obvious. **Routing is not a rising line with waypoints on it** — that reads as a stock chart,
and once seen that way it cannot be unseen. **Engineers is not a cog**: Engineers, Utilities and
Settings all pull toward a gear, each reads fine alone, and a strip where three of eight are
variations on one shape is a strip nobody can scan. **Settings is filled rather than stroked**, which
is the second exception on record to the rule the send arrow is the first of — eight teeth in outline
at tab size is porridge.

`Glyphs.cs` said the tab names stay as words. That paragraph is amended rather than set aside: the
word shows whenever the strip has room, so the alternative to a mark is not the word, it is nothing.

**The avatar, the badge and the help mark came down onto the tab row**, where three small controls
had been costing a whole band of height the page below did not get. They are one row of a fixed
height rather than three controls each aligning themselves — bottom-aligning things of three
different heights lines up their bottom edges and nothing a reader looks at. That row is 35 because
the help mark is, and the mark is 35 because two constraints meet there: the headset presses it with
a ray so it may not go under the 30-pixel floor, and the glyph inside is centred against the button's
own box to half a pixel, which shrinking it broke.

**Loadout reads Fleet**, its roots unchanged, and the Checklist tab drops its count — a number beside
a picture is a badge, and the checklist page carries it anyway.

### The carrier is on the tab that took its name (#230)

The Fleet tab holds a **Carrier** reading: name and callsign, where it is, tritium, jump range,
space, cargo, balance, docking access, and which services are open. Their squadron's carrier is
drawn beneath it under its own heading.

**Built on `CarrierState` rather than beside it, and that was the second attempt.** The first added a
parallel watch keyed by `CarrierID` and would have reintroduced the fault that record already fixes —
a squadron's carrier read as the Commander's own, reported 2026-08-21 as *"That's not where my Fleet
Carrier is"*. Everything that tells the two apart lives in that record, so the figures belong there
too. The parallel model was deleted rather than kept.

**The filter is deliberately asymmetric, and that asymmetry is the safety property.** An event with
no `CarrierType` goes to the Commander's own, because Frontier added the field partway through;
handing those to the squadron side as well would conjure a carrier out of every journal written
before it existed, and inventing a carrier a Commander does not have is worse than missing one they
do. Checked against the corpus rather than assumed: **no id anywhere claims both types, and no
`SquadronCarrier` id ever appears without one**. The squadron side consults the id for exactly one
reason — so a carrier can be named at the airlock, the way #109 fixed for the Commander's own, from a
docking that carries a `MarketID` and no type at all.

**`FCMaterials` puts a callsign in `CarrierID`** — 89 times across the corpus, always untyped. `Long`
answers null for a string so they fall through inert, which is right twice over: the field is the
wrong type to trust, and the carrier a Commander trades materials at is frequently not one of theirs.

Three things the page is careful about. **Bought and switched off is said apart from open for
business**, because it is the state a Commander can undo and the one they are most likely not to have
meant. **Cargo is a tonnage and never a manifest** — nothing Elite writes says what those tonnes are.
And **every figure carries its age**, because the stats only refresh when the carrier management
panel is opened, so saying them flat would be saying they are current. The squadron's carrier shows
no balance and no space: those are not theirs to spend, and a figure they cannot act on drawn level
with one they can is a figure waiting to be misread.

### Turn is Response in everything the Commander reads (#235)

*Turn* is the language of the thing underneath — one cycle of the conversation with the model — and
it had leaked into the surface, where it means nothing to somebody who has not read the code. User
facing strings only: the identifiers keep the word, which is accurate internally, and **"LLM Turn
Price" stays as Phase 3's name** because a published tag never moves.

*Exchange* was proposed and declined as a verb. It is worth recording what the rejected word was
buying: a turn is **both halves** — what the Commander said, the tool calls, and the reply — and the
figure under that heading is the cost of all of it. *Response* names only d47's half, so the heading
describes less than it counts. A known and accepted imprecision rather than an oversight.

### An issue the Commander wrote needs no label

`CLAUDE.md` read *"an unlabelled issue … may not be worked, closed, or named in a `Fixes #N`"* and
never said **whose** issue. A session took that to cover the Commander's own, declined to write
`Fixes #N` for six issues the Commander had written and then asked for in chat, and `prerelease`
duly found no closed issues and worked out a patch for what was plainly a minor.

`Resolve-Trust` had been right the whole time — it allows anything the Commander authored and asks
for the label only on somebody else's issue — and `tools/issues.ps1` says as much in its own
description. The prose was the only thing claiming otherwise, and the prose is what an agent reads
first. **The injection defence is untouched**: a stranger's issue still needs a label a vouched
account applied, read from the event log, failing closed when the log cannot be read.

### Also

**`get-local` publishes from a window with no closed issues.** `Get-ClosedIssueNumbers` returns
nothing when no commit since the newest tag says `Fixes #N`, and under StrictMode `$null` has no
`.Count` — so the badge check threw before the publish started. That window is the ordinary case for
a build cut mid-change, which is exactly when `get-local` is reached for.

---

## 0.95.0 — 2026-08-30 — Every page says how before it says why

### Two bands on every capability page: how to use it, and why it works that way (#229)

**The Commander's rule for what help is for: how to use it, not why it is good.** Every one of the
45 capability pages now opens on a **How to use it** band — steps, each keyed to a mockup of the
screen the step is about, and the one gotcha that stops people. The band that was there, which
explains why the feature is the way it is, is still there under **Why it works this way**, collapsed.
Both use `<details>`, which is native and needs no script.

**switches.md was built first and signed off before the other forty-four**, which is the whole of
why this landed in one piece. Forty-five bands written to an unagreed shape is forty-five bands to
redo.

**The how-to band carries its own class, and that is load-bearing.** `HelpLibrary.Band` takes the
*first* `d47-eli5` div in a file, so a second band under that class would have silently become what
the in-app panel and the headset draw — on every page, with no code change and nothing to notice it.
`main.scss` extends one class from the other, so the two are styled from one description and cannot
drift apart.

**The cards moved out of the band on the Commander's call**, so *Where to go next* stays visible
while the why band is collapsed. That would have cost every page its feet in the panel, because
`HelpLibrary` read them from the band's frame — the failure shape its own comment already warns
about one size smaller. `Links` falls back to the cards block at the foot of the page now. Doing
that parsed blocks nothing had parsed before, and found three general pages writing their arrows as
`&rarr;` — an HTML entity, the one thing the band rules forbid outright.

**And the new bands are checked rather than trusted.** Nothing in the app draws them, so 45 pages of
hand-written SVG had no parser between them and the site: an element outside the drawable set, a
colour written as a literal, a font size under the 14 px floor. `ParseHowTo` reads them with the same
validator the ELI5 bands get, and a sweep holds both to one standard. Verified by breaking one
colour and watching it name the page and the role.

**Two faults came from rendering the pilot rather than reading it.** The summary marker's CSS escape
had no terminating space, so it ate the next character and drew a tofu box with `B8` beside it. And
the position-row mockup was missing the *fourth* dropdown — a picture showing three controls against
a screen with four is worse than no picture at all.

**What this is not is a gate on the content.** `DocumentationGateTests` asserts a page exists and
quotes the current tool schema; nothing can assert that a band teaches anything, or that a mockup
still resembles the app. A drawing of a UI is a claim about that UI, and it stops being true the
moment a control moves — so when a capability's settings rows change, its mockup is part of that
change rather than documentation to catch up on later.

---

## 0.94.0 — 2026-08-30 — One key to talk, one to call the whole thing off

### Clickable words and marks carry the accent at rest (#208)

Reported against the **help mark**, which was muted until the pointer touched it. The rule asked for
is one line — *a clickable word or glyph carries the theme accent* — and the finding underneath it
was that the rule was applied nowhere consistently. Five bare marks, four different answers: the
mode toggle was accent, help was muted-then-accent, both settings resets were muted, the checklist's
**+** was the colour of the words around it, and copy was muted.

They are all accent now, at rest, with nothing pointed at them.

**Fixing the help mark deleted code.** It carried a pair of `PointerEntered`/`PointerExited`
handlers that swapped the stroke — and those are the pattern `Glyphs.Draw` exists to refuse.
`FindResource` reads a brush **once**, at the moment of the hover, and assigns it to the property;
that is a local value, so a theme switched after a hover left the mark painted in the old theme's
colour with nothing able to repaint it. Both handlers and both hooks are gone, and a
`DynamicResource` is the only mechanism on that stroke now. A test switches theme and watches the
mark follow — Elite's `#FF7100` to light's `#0A64C8` to dark's `#4C8DFF`, so it fails rather than
passing on a colour that never moved.

**The hover feedback went with them rather than being replaced.** The colour was saying two things
at once — *this can be pressed* and *you are pointing at it* — and the resting state spends the
first. `TurnDetails` and the mode toggle have always been accent at rest and changed nothing on
hover; the button's own background wash is what says the pointer is there.

**Where the rule stops** is the part worth having written down, because *"clickable things are
orange"* stated broadly would repaint half the app. In scope: a bare word or mark on a transparent
background whose **only** affordance is that it can be pressed — nothing about its shape says press
me, so the colour has to. Out of scope: anything that already says it by shape and carries its own
chrome — checkboxes and their labels, the tab strip, scrollbar parts, and anything using the accent
as a *background*, where an accent foreground would vanish into it. And a disabled control keeps its
disabled treatment, because an accent on something that cannot be pressed is the colour making a
promise the control cannot keep.

**One mark beyond the issue's own table: copy.** It sits on the transcript bar with no chrome of its
own, exactly as help does, and it was muted. The issue enumerated five sites and did not list it —
with no reasoning excluding it, and it meets the stated test — so leaving it would have reproduced
the finding the report is actually about.

Every change is a **key** and never a literal. Amber is the Elite palette's answer to
`D47.Accent`, not the value being written.

### prerelease says nothing to release, rather than blaming the changelog

Reported as *"something messed up"*. Nothing had: 0.93.0 had just shipped, and the run being
complained about was a second one over an empty range. Everything downstream worked perfectly on
nothing — no commit closed a change request, so the decision was Patch, and the changelog check then
reported that `## 0.93.1` was missing and exited 1. Every word of that is true and all of it is about
the wrong thing: it blames the changelog for the absence of a release nobody should be cutting, and
it reads exactly like the tool having broken. The range is asked first now, above the version
arithmetic, and an empty one stops there with one sentence and **exit zero** — nothing is wrong and
nothing was done, which is a different answer from the missing-section refusal below it. A test pins
the order, because the reason a run stops is the whole of what it is telling you.

### Push-to-talk is one row that holds a key, a stick button, or both (#217)

It was two rows — *Push-to-talk key* and *Push-to-talk button* — asking one question. It is one now,
labelled **Push-to-talk**: press **Press to bind** and d47 listens for a keystroke and walks the
controller at the same time, taking whichever arrives first. With both bound the row reads
`RightShift, button 11`. **Unbind clears both**, which is what the word says.

**The storage did not merge, and must not.** `listening.pushToTalkKey` and
`listening.pushToTalkButton` are both still on the record with their own bindings, help and docs
anchors — `settings.json` is append-only, and a build that merged them would silently discard
whichever half it dropped on first read. What merged is the question, which was always one: the
rows' own help already promised *"with both set, either one opens the microphone."*

**`SettingKind.HotasButton` stays a separate kind**, and that is the documented decision holding
rather than surviving by accident. It is separate because *the capture gesture differs* — a key is
caught by the window that has focus, a button has to be walked for — and that is exactly as true
today. One control arming both listeners at once needs both mechanisms to stay as distinct as they
are.

**Two new row properties carry it, declared on the rows rather than known by the panel.** The key
row names its companion in `AlsoBinds`; the companion carries `DrawnElsewhere`. A settings surface
holding its own list of which two rows are really one is a second list to keep in step, and this way
the reset arrow and the change marker follow for free — both read every key the control holds, so
resetting a merged row puts both halves back.

**`DrawnElsewhere` is not `AppliesWhen`, and the difference is load-bearing.** A row that does not
apply is *refused* by `SettingsService.Apply` as well as hidden — right for a setting with no meaning
in the current configuration, and exactly wrong here, since this row is written every time a stick
button is bound. It applies; it is simply not its own row on the page.

**Both halves stay `Protected`.** Rebinding or clearing push-to-talk takes away the Commander's way
of speaking to d47, and protected rows cost no tool-surface bytes, so nothing was traded.

**It is one of each, not any number, and the help says that too.** There is one slot for a key and
one for a stick button, so a second key changes the key and a second button changes the button.
Nothing is ever added to a list the Commander cannot see — what the row reads is exactly what is
bound, and two gestures is the most it will ever say. A combination counts as one key, and it is one
end to end: `PushToTalkKey.Poll` reads the bound virtual-key code *and* every modifier the gesture
declared, so `Ctrl+D` opens the microphone only while both are held. That is now pinned, because the
failure would have been silent and one-sided — a row storing `Ctrl+D` over a poll watching only D
would open the microphone on every D typed anywhere.

**Bind twice to have both, and the row now says so.** Reported the moment the row was driven — *"it
says I can bind to both. How?"* — and the answer was always *press the control once per gesture*,
which nothing on screen mentioned. The help says it in the Commander's own order now.

**Right shift could not be bound at all, and that is a real fault this uncovered.** Every bind
capture discarded a bare modifier as *someone still assembling a chord* — the rule since Phase 4 —
and push-to-talk's own default **is** a bare modifier. So a Commander who cleared the row could not
put the default back, which is exactly the corner the one who asked was in: bound to a stick button,
with no way to add the key beside it. It is told apart by the **edge** now rather than refused:
pressed, a modifier is still a chord being assembled; released with nothing else having arrived, it
was the binding. The same idiom `ButtonCapture` uses on the stick, for the same reason — it is the
edge that answers the question rather than the one that raises it. A system-wide row still ignores a
bare modifier, because the service refuses a bare key there anyway.

**The modal bind window is gone**, and its words are not. `ButtonBindWindow` was the only caller of
the controller walk; the walk now runs on a timer the row's own control owns, and `ButtonCapture` is
untouched and still the authority on what counts as a button — ignoring what was already held when
the walk started, capturing on release rather than on press, and declining a switch that stays where
it is put. Its sentences go on the row's message line, which is where the window used to put them.

**The scope stopped at the control, and that was a ruling rather than a shortcut.** #219 shrank the
bind set, and the Commander ruled the 2D panel hotkeys stay keyboard-only: they are pressed at the
desk with the window in view, so a stick button there is a route nobody takes. That left push-to-talk
as the one bind taking a stick button — until #221, later the same day, gave Cancel one too and built
the polled delivery this issue had scoped out. The ruling held; what changed is that a second binding
turned out to deserve a stick, and it is the one you reach for with your hands full.

### Cancel is its own control, on a key or a stick button (#218, #221)

**The ask, and it changed shape once between the asking and the shipping.** *"I don't want to talk
while the ship AI is talking. I want it to shut up and listen."* The first answer was to make the
push-to-talk press silence d47 — one gesture, nothing new to bind. It was driven, and it was wrong.

**Why it was wrong, measured rather than argued.** A press that both silences and listens means
every tap meant as *be quiet* also captures a second of a quiet room. d47 primes Whisper with the
journal's proper nouns, and a primed decoder handed a second of silence does not return nothing: it
returns words. In the reported session it returned *"Thank you for watching!"*, twice, and d47
answered both — one of them cancelling the turn behind it.

The prompt is the whole mechanism, and it is worth writing down because it defeats the obvious fix.
Same audio, `ggml-medium.en`: **unprompted**, every silent clip returns `[BLANK_AUDIO]`, which
`SpeechNoise` already catches, with `NoSpeechProbability` at **0.94–0.99**. **With 36 name hints**,
the same clip returns words from the hint vocabulary and reads **0.0001** — which is what real
speech reads. So the prompt both causes the hallucination and destroys the signal that would catch
it. `spike/NoSpeechProbe` reproduces it, along with two fixes that do not work: an absolute energy
floor over the clip discards three of the Commander's real utterances, and routing push-to-talk
through the adaptive detector is forbidden by `ListenGate.KeyDown` — *"A Commander who wants to be
certain d47 is listening should not have to trust a detector to agree"* — which its tests enforce.

**So push-to-talk is push-to-talk again, and interrupting got its own control.** That removes the
fault at the root rather than filtering it downstream: a tap meant as *be quiet* is no longer a
microphone press, so there is no silent second to transcribe.

**Cancel does more than the row it grew out of.** `Ctrl+Alt+X` out of the box, and one press stops
the voice **and abandons the turn** — the model stops and so does the spending, which is the case
the Commander named: a long web search you have changed your mind about. Saying *"be quiet"* never
did that. `AppHost.CancelNow` is the one implementation, and the spoken `cancel_turn` has always
done exactly this pair in exactly this order — silence first, because whatever already reached the
audio queue would otherwise play on after the turn behind it is gone, which sounds like the cancel
not having worked.

**And it takes a stick button**, which is what makes it usable mid-fight. It is the second binding
in d47 to take one and the first that fires once — push-to-talk is held and reads both edges; this
is a press, so the release edge is deliberately not subscribed and holding the button cancels once.
Both are polled from the tick, because Windows does not deliver controller buttons to a registered
hotkey, which is still why the interface hotkeys stay keyboard-only. It is drawn through the one
bind control from #217: one row, a key and a button, `Ctrl+Alt+X, button 8`.

**The row sits directly under push-to-talk**, on the Commander's instruction — *"the Cancel binding
should be right below the PTT binding."* They are the two controls a Commander binds together, so
they are bound in one place, and the row moved capability to get there rather than being nudged into
position from a distance: its keys are `listening.cancelHotkey` and `listening.cancelButton` now.

**That move fixed a routing fault as well as the layout, and the fault was mine.** A row key's own
prefix decides which subsystem re-applies it (`SettingsFanout`), and the polled rebind lives in the
listening apply — so while the key read `speech.cancelButton`, binding a cancel button did nothing
until something else happened to trigger that apply. Both halves route to Listening now, asserted.
The properties behind them stay `Speech.ShutUpHotkey` and `Speech.CancelButton`, because
`settings.json` is append-only: what moved is the row, never the value, so nobody's binding moved
with it.

**`PushToTalkButton` is `BoundButton` now.** A polled edge detector with two callers is not named
for one of them. The settings property and the row key keep their older spellings, because
`settings.json` is append-only — which is also why the Cancel key still stores as
`speech.shutUpHotkey`, and why a Commander who had already bound one keeps it.

**A cancelled turn says `[cancelled]`, which it did not.** Cancelling threw out of the await like
anything else, so it landed in the same catch as a bug and was reported as one — *"I couldn't answer
that. The details are on the Technical page."* Nothing was on the Technical page, because nothing
had gone wrong, and being sent to look for a fault that does not exist is worse than being told
nothing. It is written rather than spoken, which is not a detail: Cancel exists to stop the voice,
so a cancel that answered out loud would be the one command in d47 that does the opposite of what it
says. Nothing is logged as an error either, and `TurnCancellation` already records that the
Commander called it off.

**The token decides, not the exception type.** A provider abandoning its own request throws the same
`TaskCanceledException`, and that *is* a failure — telling the Commander it was cancelled would be
d47 blaming them for its own timeout. So the rule is *this turn's own token was cancelled* **and**
what came out was a cancellation; a bug thrown at the same moment as a Cancel press is still
reported as a bug. `TurnEnding` is that one decision, with tests that fail if either half of it is
loosened.

**Two smaller corrections came out of it.** The row was `Advanced`, so it sat under the fold; and
its `DefaultDisplay` claimed `(unbound)` while the property has shipped as `Ctrl+Alt+X` since
Phase 5 — the docs said one thing and the row said the other. Both fixed.

### Re-anchor is withdrawn, and binding a core to a ship is a Settings row (#219)

Two capabilities went, and they went for the same reason: each was reachable four ways when the
thing it did was worth one.

**Re-anchor is gone entirely** — the tool, the two spoken phrases, `Ctrl+Alt+R`, the docs page, and
`VrPlacementMath.Reanchored` with the five tests that guarded it. It existed for a headset session
where Elite's recenter had turned the cockpit out from under panels put down in the room, and
#199's nudges have since given that a better answer: *lock it to my head*, then put it down where
you want it. A gesture that undoes one specific drift is a worse tool than one that puts a panel
anywhere, and keeping both meant keeping a whole capability, its own hotkey, and a maths function
whose only caller was that capability.

**Binding a core to a ship keeps its Settings rows and loses everything else.** `bind_ship_core`
and `forget_ship_core` are no longer tools, the five phrases that reached them reach nothing, and
`Ctrl+Alt+B` is unregistered. What a ship flies with is still remembered, still per Commander, and
still readable by the model through `describe_persona` — the *reading* half was always the allowed
one. It is a thing done once per ship at the desk, which is where it was being done anyway.

**`ShipCoreTrustBoundaryTests` changed shape rather than being deleted**, and that is the part worth
recording. It used to assert that the two tools refused the model, were never advertised, and were
reachable by voice. It now asserts they do not exist. That is a stronger guarantee than a refusal:
a tool that refuses is a tool somebody can make stop refusing.

**`Hotkeys.Reanchor` and `Hotkeys.BindShipCore` stay on the settings record**, unread. The settings
file is append-only — a property removed is a property that throws when an older `settings.json`
arrives holding it.

---

## 0.93.0 — 2026-08-30 — The controllers are put down, and the panel learns to be told where to go

Ten issues. Seven are in the headset and the captions get most of them; three are on the desktop.
[#202](https://github.com/dseelinger/d47/issues/202) and
[#203](https://github.com/dseelinger/d47/issues/203) land together — mini panels stop carrying page
chrome, which is what had to happen before Goals could become a checkbox.
[#197](https://github.com/dseelinger/d47/issues/197) lets the cost figures be reset from the Details
window and [#210](https://github.com/dseelinger/d47/issues/210) turns that window's button into a
banknote, and [#207](https://github.com/dseelinger/d47/issues/207) makes the local-build badge say
what the build worked. [#198](https://github.com/dseelinger/d47/issues/198) withdraws motion controller support until
[#18](https://github.com/dseelinger/d47/issues/18) is understood;
[#199](https://github.com/dseelinger/d47/issues/199) replaces the one thing that withdrawal takes
away. [#189](https://github.com/dseelinger/d47/issues/189) is the captions sitting rotated from the
cockpit, [#200](https://github.com/dseelinger/d47/issues/200) is long answers being captioned from
the middle, and [#201](https://github.com/dseelinger/d47/issues/201) is what a full audit against
the Netflix SDH guide, the FCC and WCAG found beside it. Neither change request is under the
moratorium, and the Commander asked for both.

### Nothing touches a controller any more, and that is the experiment (#198)

Every controller the Commander put down while d47 was connected to SteamVR, that then reached
standby, failed to wake on its own. Every one put down while d47 was **not** connected woke by
itself. That is #18, and it is not understood — but d47 was reading controller poses **ninety
times a second for the whole session**, whether or not anything was being pointed at. In one
sixty-four-minute session where no ray ever crossed the panel, that is on the order of 350,000
reads, running straight across the moment SteamVR puts a controller to sleep. Withdrawing is the
only change that stops d47 touching the device, so it is also the only clean test of it.

**A switch, not a deletion, on the Commander's own framing: it is not forever.** `vr.controllers`
is off out of the box, so the withdrawal is what ships and turning it back on is deliberate. A
session with it on and a session with it off are the experiment, which a deletion would have made
impossible — and the day #18 is understood, or the day this is shown not to have helped, the way
back is one row.

**"Nothing at all" is meant literally**, and it is asserted rather than promised. The action half
closes on one skipped call: `TriggerHeld`, `Release` and `BackPressed` each open by testing the
flag only `Register` sets, so not registering makes all three inert on their first line — no
manifest written, no application key claimed, no `UpdateActionState`. The pose half is where the
work was. `VrAimLoop` does not start, and a running one is stopped rather than left when the row
moves mid-session. `SteamVrRuntime.HandsAndHead` returns before its device loop, so
`GetTrackedDeviceClass` is not asked of sixty-four slots a frame and neither `Note` nor
`GripToTip` — which reads render-model strings off the device — is reached at all. `Controllers()`
is gone rather than gated: nothing had called it, and a public road to the device loop is a road
somebody adds a caller to. The beam and cursor quads go too, because a beam with nothing driving
it is a visible artefact of a feature that is off.

**One call stays, and its shape is most of the reduction.** `GetDeviceToAbsoluteTrackingPose`
fills a whole-universe array and the head pose comes out of it, so captions, resting placement and
re-anchor all need it. With the row off it is reached only through `ReadHead`, which asks for an
array one slot long and indexes the headset alone, at the serve's ten hertz rather than the aim
loop's ninety. That is ten single-slot reads a second and no per-device call of any kind, down
from ninety whole-array reads with sixty-four device-class queries each.

**The assertions are negatives, so none of them is behavioural.** No amount of running d47 without
a headset demonstrates that a call is unreachable. So `NothingTouchesAControllerWhileWithdrawnTests`
reasons about the compiled IL — the technique `VrPointerTests` was already built on, lifted into
`AssemblyCalls` now that a second file needs it: every per-device call has exactly one caller, that
caller reads the row in its own body rather than trusting its callers to, the aim loop has exactly
one place that starts it, and the guides are built only where the row is consulted. Each was proved
to fail with its gate removed before it was kept.

**What goes with it, known and accepted.** Nothing on the panel can be pressed in the headset: no
buttons, toggles or checklist ticks, no combo boxes, no on-panel keyboard, no scrollbar dragging,
no grip-to-go-back, and no Settings tab. Voice keeps tab and breadcrumb navigation, back,
scrolling, answering a prompt already open, and re-anchoring. A gesture in flight when the row
moves is let go rather than frozen — a captured scrollbar released, a lit highlight put out, a
carried panel put down where it had got to and written.

### Mini panels carry no page chrome, and Goals is a checkbox (#202, #203)

Two issues in one change, because #203 said so: converting the Goals button to a checkbox would have
moved it *into* the exemption the mini rule carries for checkboxes and made a reported defect
correct by rule. **Either fix the rule first or fix both together**, and this is both together.

**The rule was one style selector and it could not hold.** `PanelView.output-only Button` was the
whole of "a mini panel carries no clickable control", and it failed two ways at once. A style setter
sits at `BindingPriority.Style`, **below `LocalValue`** — so any control that assigns its own
`IsVisible` from code pins the value and the selector never applies. Three did, and they are exactly
what a Commander reported seeing on a 512-wide strip: **Goals**, **Suggestions** and **↓ Newest**.
And it matched *exact* `Button`, so **Include Partial Grades** — a live filter checkbox on the same
bar — was never covered at all.

**The unit is a container now.** A page marks its chrome bar, and a style on that class hides it. A
hidden parent hides its children whatever they say about themselves, so a page goes on writing
`_suggestions.IsVisible = pending.Count > 0` and is right about it; it covers every control type at
once without enumerating them; and it survives a rebuild, which per-control hiding would not — pages
rebuild their contents constantly and build their bars once.

**A style rather than a walk over the tree, and the timing is why.** An imperative pass was written
first and could not be made to fire late enough: a drill level builds its page on first sight, which
is after the class has been set and after every event that could have triggered one, so the walk
found an empty pane and never ran again. A style applies when the control enters the tree, whenever
that turns out to be. What it asks of a page is one thing, and it is the whole contract: **never
assign `IsVisible` on a container you marked** — that would be the same defect one level up.

**Keyed on the class and never on the mode**, because the desktop window's own mini is fully
clickable by design and deliberately never takes the class. A rule that read the mode would strip
the controls off the one mini where they work.

**The line ticks stay, and that is the distinction the old rule could not draw.** They are inside
the list rather than on the bar, and a Commander ticks work off from the headset. So does the
scrollbar. What goes is chrome.

**And no test had ever built a page.** `MiniInTheHeadsetCarriesNoButtonsTests` leaves every host
surface null, so `EnableChecklist` is never called and `PagePane.Child` is null — it only ever saw
`PanelView`'s own chrome, which is why the suite was green while the defect was on screen. Worse,
its third fact asserted that checkboxes survive, so it actively ratified the reported behaviour. The
new file furnishes a real checklist, drives a real frame, and asserts by the **words on the
controls** rather than by a count, because a count says nothing about which one survived.

**Then Goals became a checkbox.** It was already a two-state disclosure wearing a button: a private
`bool`, a hand-swapped label, nothing navigated and nothing opened — unlike Suggestions beside it,
which drills, or Order and Import/Export, which open choosers and *are* correctly buttons. **And it
gets the count back**: the label swap read `Hide goals` once open, throwing away the one number that
made anybody open it. A checkbox holds `Goals (3 running)` whichever way it is set.

One more live leak went with them: the adventure form's *Using* button was constructed
`IsVisible = hasChoice`, pinned from birth. It is not built at all when there is nothing to choose
between — a hidden control is still something a ray can find, which the checklist's own code already
says about a different control.

### The Details button is a banknote (#210)

The word *Details* on the status line is a drawn mark now — a rectangle with a circle in the middle
of it. Six were drawn and the Commander chose this one the same day.

**Not a coin, and not a currency symbol, and both are worth recording because both look like the
obvious answer.** The figures behind that button are *real money* — dollars on a provider account,
not the Commander's in-game balance — and a coin-shaped mark in a cockpit overlay is exactly the
thing that reads as credits. A symbol has the other problem: the figures are formatted `:C4`, which
follows the machine's culture, so a `$` is wrong for anybody not billed in dollars. On the one
figure in the app that must never be misread, a note carries "money" without carrying either. A
receipt, a coin stack, a price tag and a wallet were also drawn and are recorded in the issue so
they are not re-proposed.

**Redrawn 18 by 12 rather than given a box of its own.** The chosen drawing spanned 20 by 10, and
`Glyphs.Draw` puts a mark in a *square* box and stretches uniformly — so a 2:1 note would have
filled the width, reached seven of fourteen units of height, and read as a short wide bar smaller
than the marks beside it. `HelpGlyph` solves that with a non-square box, which would have taken this
off the one path every other mark travels; a normal note proportion keeps it on that path. A test
pins the ink at 3,6 by 18x12, which also pins where the circle's arc resolved — the two centres it
could take are three units above and three below its start, and the wrong one changes the aspect
silently.

**The word survives, and that is the condition.** `Glyphs.Mark` puts the sentence on the tooltip
*and* on the accessible name: a glyph-only control with no accessible name is a control that does
not exist for anybody not looking at it, and replacing a word with a picture is only an improvement
while the word is still reachable. There is a test that says so by name, so a later refactor cannot
quietly remove the only thing a screen reader has.

The mark is accent, as the word already was, so it is consistent with
[#208](https://github.com/dseelinger/d47/issues/208) before that lands rather than needing to move
with it. And *Details* comes off the list in `Glyphs`' own comment of words that stay words — the
word was kept because no picture says "the figures behind this", and what was asked for was a mark
for the **subject** rather than for the act.

### The cost figures can be reset, and a reset leaves every window that held it (#197)

A **Reset** button in the Details window, offering six spans — *this session*, and the same five the
figures list shows. Take one and everything charged inside it stops counting **in every window that
contained it**: reset today and today's charges leave this week, the last 7 days, the last 30 days
and this month with it.

**That rule was already true and nobody had asked it.** `spend.jsonl` is an append-only ledger of one
timestamped row per charge, and every period figure is a *query* over it rather than a running
counter — so a charge that stops counting leaves every window whose span contains its instant, at
once, with no per-period totals to keep in step. What this adds is the mark and the control.

**Nothing is deleted, settled on 2026-08-30.** A reset appends one line recording the instant and the
span; totals skip anything a mark covers. The file stays append-only — the invariant the format
exists for, where a crash mid-write costs the last line rather than the whole history — the act is
auditable, and **an accidental reset is undone by deleting that one line by hand**. That mattered
more here than anywhere else: this is the one number in the app that stands for real money. Marks
compose, a charge made after one counts again, and a mark is written as *priced* despite pricing
nothing, so a build that predates them reads it as a settled zero rather than turning every total
covering it into "at least $X, part of it unpriced" — the `get-ver latest` path, costed and bounded.

**Thirty days, not thirty-one.** The ask named "the last 31 days, in case we're at the end of the
month"; the Commander settled it at thirty the same day, because that is what *This month* already
does — calendar-aligned, resetting itself on the 1st — and a reset list offering a span the figures
list does not show would be two lists disagreeing about what a window is.

***This month* does not nest with the rolling windows, and that is correct.** Reset it on the 3rd and
three days go while the rest of *Last 30 days* stands. Set semantics, not a tree — written down so a
surviving figure is not later mistaken for a defect and "fixed".

***The session* is the one span that is not a query.** Its figures live in memory and die with the
process, so a reset does two things: appends the mark from launch time, and empties the counters.
Half of that is the confusing outcome the issue names — clearing only the counters leaves the running
totals counting charges the session block says are gone. Every reset clears them, even one narrower
than the session, because a `TurnCost` carries no instant to filter by; the one case where that
over-clears is a session running across the boundary, which costs a session figure that reads low
while every ledger window stays exact.

**It asks first, and that departs from the house idiom on purpose.** Every other eraser in d47 is an
`Info` settings row with a `Press` and no confirmation — memory, flight recordings, personas — and
their safety is that the tool surface cannot reach them and no spoken phrase does, not a dialog.
This one was asked for in the Details window, which is the right place because it is where the
numbers are, and that puts a control that erases money history somewhere a stray click reaches. So
it names the window and the figure, and `ConfirmWindow` already defaults to no.

### The local-build badge says what the build worked (#207)

A build cut from a working tree is a build for testing, and the badge already said it was local.
What it could not say is **which changes are in it**. Click it now and it lists the issues that
build worked — as bullets, each number a chip that shows the issue's state, `dseelinger/d47 #205`,
title and labels on hover, and opens it in a browser when clicked. GitHub's own reference chip,
minus the avatar: the one element that needs a download and says the least.

**d47 cannot discover this at run time, so it is baked in.** Which issues a working tree closed
exists only in the git log, at publish time. `get-local.ps1` reads the commits since the newest tag
for what they say they close — the same window the version stamp is named for — asks GitHub for each
one's state and labels, and stamps the lot into an `AssemblyMetadata` attribute the way
`DevInstallRoot` already travels. **A published release never passes the property**, so the whole
feature is absent from a real build by construction rather than by a run-time check, and
`get-local`'s rule that it copies exactly `d47.exe` and `runtimes\` is untouched — a sidecar JSON
file would have been easier to write and would have changed what the command ships.

**Baked at publish rather than fetched on hover**, so the card is instant, works offline, and cannot
render text that arrived after the build was made.

**An issue title is untrusted text, and the repository already had the door.** `tools/issues.ps1`
exists to keep a stranger's issue prose out of an agent's context, withholding the title as well as
the body on the reasoning that *"a title is attacker-controlled text like any other, and 'just the
title' is how this leaks"*. Drawing one in d47's own chrome is a different risk from feeding it to a
model, but it is the same text — so **the same `Resolve-Trust` decides both**, and it moved into
`tools/issues.lib.ps1` rather than being copied, because a control copied is two controls that will
disagree. An issue the Commander did not write or vouch for is stamped as its number alone and the
card says the title is withheld. In this repository today that costs nothing; it closes the hole
before it opens. The `Fixes #N` extraction moved with it, since `prerelease` deciding a version
number and `get-local` listing a badge are one question over one window — and a test asserts both
have exactly one definition.

**And nothing in the publish prints a title.** A step that echoed what it was baking would walk
untrusted prose straight back into the one channel this repository trusts, so what `get-local` says
out loud is a count and the numbers.

**Desktop only, and by nobody wiring it rather than by something hiding it.** In the headset a click
would open a browser on a monitor the Commander cannot see — the argument that took the help button
off that surface. The handler is furnished by the desktop host alone, so the headset's copy has
nothing to reach; the badge asks `output-only` as well, because [#202](https://github.com/dseelinger/d47/issues/202)
is open precisely on a local `IsVisible` outranking a style setter, and a control nobody wired
outranks both.

**The link is built from the number and never from anything stamped.** `UseShellExecute` resolves
whatever it is given, which is exactly why the string it is given is not somebody else's to write.

A local build whose commits named no issue keeps a badge that is a plain mark, and the empty case
says so in a sentence rather than opening an empty box. The caveat rides on every path, full or
empty: **only what a commit wrote down** — work still in the tree, or committed without a
`Fixes #N`, does not appear.

**Two traps the green suite could not see, both found on the first real `get-local`**, and both now
guarded. PowerShell variable names are case-insensitive, so `get-local.ps1` setting `$repo` to its
checkout path *was* setting the shared library's `$Repo` — every `gh` call went out with
`--repo C:\dev\d47`, failed the way being offline fails, was caught by the fail-soft path that
exists for being offline, and stamped ten issues as unknown with no symptom but a warning that
reads like a network problem. The name is `$IssueRepo` now, and a test asserts no caller can shadow
anything the library puts in their scope. And PowerShell does not evaluate an expression inside a
native-command argument: `-p:LocalBuildIssues=(Get-BuildNotes …)` passed the flag empty and handed
MSBuild the base64 as a second project path. It goes through a variable, and the test pins the
exact publish line.

### Long answers were captioned from the middle (#200)

**A spoken sentence wrapping past two lines had its leading lines thrown away before anything was
drawn.** The roll-off ran *inside* the loop that was still adding the wrapped lines, and the whole
loop was synchronous with one `Changed` at the end — so the window never rendered an intermediate
state, and a sentence wrapping to eight lines had six of them added and removed between two frames.
The comment above that loop described consecutive events; there was no timing between the
iterations, so there were none. The sentence cap is 320 characters and a line holds 42, so this did
not take a pathological input: **any moderately long answer began in the middle.**

It is a completeness failure in the FCC's own sense — captioning must convey the aural content "to
the same extent", "in the order spoken", "from the beginning to the end" — and it fails all three.
Netflix's two-line rule is a limit *per event*, with longer content becoming consecutive events;
d47 had taken the limit and dropped the events, which turns a formatting rule into data loss.

**The fix is a queue.** Wrapped lines are held and shown two at a time, each event staying up for
as long as the reading-speed row says it takes to read — the same `DwellFor` that decides how long
the last one lingers, so there is one rule for how long text stays readable rather than a second
constant free to disagree with the row. Consecutive *short* sentences still roll up together,
which is the behaviour that already worked: appending two lines to a two-line window leaves exactly
those two, so one mechanism serves both.

**Two decisions the issue asked for explicitly rather than by default.** A new utterance replaces
whatever is still queued: the FCC asks for complete captions *and* synchronous ones, and when a
reader's chosen speed is slower than the voice those want different things — a caption still
working through a sentence the voice finished with has stopped captioning and started
transcribing. What is lost is the tail, which a reader can see moved on, never the head, which they
cannot. And the voice stopping no longer starts the dwell while lines are still waiting: `Quiet`
arrives when the audio ends, which for anything past two lines is several events early, so treating
it as "the reader has finished" would have been the same defect by a second road.

Eight tests, and the four that matter were watched failing against the old code first.

### The caption audit: the alert marker, who is speaking, and a floor that permitted an invisible caption (#201)

Audited against the Netflix English SDH style guide, the FCC's caption quality standards in 47 CFR
§79.1, and WCAG 2.2. **Every number was already right, to the digit** — 42 characters, two lines,
20 and 17 characters a second, five sixths of a second to seven, breaks scored after punctuation
and before conjunctions. What was missing was the part of the standard that is not numbers.

**The alert marker is captioned now.** A cue plays immediately ahead of an urgent callout and its
whole purpose is saying *which* warning this is before the sentence arrives; a hearing Commander
got the marker and then the words, and a reading one got only the words — losing both the warning
and the head start, on the one sound in the app that carries safety-relevant meaning. Each cue is
written in the standard's own form for a sound event, bracketed and lowercase and naming its own
situation: `[interdiction alert]`, `[pirate alert]`, `[bounty hunter alert]`, `[attack alarm]`,
`[heat alarm]`, `[territory alert]`, `[timer chime]`. The timer chime the issue called borderline
is in, because it is not: a discrete sound that means something, played ahead of the sentence that
says which timer.

**The loop tones, the thinking bed, ambience and music stay uncaptioned, and that is the standard's
own line rather than convenience.** A sound event is captioned when it *cannot be visually
identified* — the loop state is on the panel, and the other three are continuous and carry nothing
a caption would be conveying. A band that flashes before every utterance is one nobody reads.

**Captions now say who is talking when it is not d47.** The issue asked whether more than one voice
is ever captioned, and it is: the carrier's tower and its captain are captioned announcements in
their own voices, and a turn addressed to a crew member is answered in theirs. A reader has no face
and no mouth to identify a voice by, which is exactly the condition Netflix's rule is written for.
So those lines open with `[Tower]`, `[Carrier]` or the crew member's own name — **once**, at the top
of what they say, because a speaker ID marks a change of speaker and repeating it on every sentence
announces a change that did not happen. **d47's own lines carry no label**: it is the voice the
caption band is understood to belong to, and naming it on every line is the noise the rule exists to
keep out. Re-voiced in-game messages remain uncaptioned entirely, already written out with their
sender on the Technical page.

**The background opacity floor was 0.2, which is the value the clamp was added to prevent.** Against
a bright scene — a station floodlight, an ice ring, a hangar wall — a box that see-through leaves an
effective backdrop near rgb(204,204,204), and `#F2F2F2` text on that is about **1.4:1** where WCAG
asks 4.5:1. That is not a dim caption, it is an invisible one, and a Commander who reached it by
dragging a slider had no way to tell it from the captions having stopped. The floor is now **0.6**,
which gives about 5.1:1 against pure white and clears AA on the worst case rather than the usual
one; 0.5 was measured at 3.5:1 and rejected as still short. The default stays 0.78, so this binds
only on somebody who deliberately turned it down. The old value is kept in a test as the thing that
must not come back, since an assertion that only says "the current number is fine" passes again the
moment somebody lowers it.

**Placement stays a considered deviation.** The FCC's fourth standard asks that captions not block
important visual content; d47's are head-locked, fixed below eye level and deliberately immovable,
which is a defensible reading of the same goal and the opposite of a broadcast caption only in
mechanism. Recorded rather than changed.

**Two documentation errors found while measuring, both corrected.** Three places still claimed a
three-line window against `WindowLines = 2`. And the angular size comment claimed medium subtends
"about two degrees of arc"; measured from the real geometry — 1600 px across 0.9 m at 1.6 m, 52 px
em — it is about **1.0°**, with a cap height near 42 arcmin, small at 33 and large at 54. The sizes
were fine; only the comment was wrong, which is the sort of wrong that stops the next person
checking.

### Captions sit level with the cockpit rather than with your head (#189)

Reported as caption text rotated slightly clockwise, not lining up with the cockpit's own
horizontal lines. **Captions are the one surface bolted to the headset** — placed with
`SetOverlayTransformTrackedDeviceRelative` against the HMD, with an offset whose rotation was
identity — so the quad's world orientation *was* the headset's orientation, roll included. The
panel is not: it is world-locked and its resting pose has always pinned roll to zero. A quad glued
to the head is always level in the *view* and never level with anything else, so any roll between
the Commander's head and the cockpit showed up as exactly that disagreement. There was no
roll-stabilisation anywhere in `src/`.

**A head-locked surface now hangs off the head's upright frame.** `VrPlacementMath.Upright` reads
yaw and pitch off the pose's own forward — which roll leaves untouched, being rotation *about* that
axis — and rebuilds it with roll zero. The caption follows where the Commander is looking and
ignores how far their head is tilted.

**It stays attached to the headset, and that is the half worth arguing about.** Placing captions
absolutely would have handed them a 10 Hz position from the serve, and text that swims a tenth of
a second behind a turning head is worse than text that is a few degrees off. Only the *correction*
is resolved on the tick; the compositor still carries the quad at headset rate. `PlaceOnHead`'s
existing "nothing is written unless it changed" guard survives intact — a roll that is holding
still is a frame with no transform call in it — so the route the issue expected to have to give up
was not given up.

**The offset is now derived from `Where` rather than computed beside it**, as `where · head⁻¹`.
Two parallel derivations of one placement is how a quad comes to be drawn a degree or two from
where d47 thinks it is, and a ray cast at a head-locked panel would have been the first to find
out. `AgainstTheHead()` against a head at the origin is still the pure translation it always was.

Looking straight up or straight down is written down as its own case: a vertical forward has no
compass direction left in it, and a yaw read out of `atan2(0, 0)` is whatever the sign of a zero
happens to be — which would have swung the captions to the other side of the cockpit for as long as
the Commander looked at their feet. The head's own up is horizontal at exactly that moment, so it
carries the yaw instead.

**What this cannot fix, stated because the issue is still `needs-repro`.** Being level with the
horizon is not the same as agreeing with the cockpit. Elite's cockpit is fixed to whatever pose its
own recenter was taken at, so a recenter made with a tilted head leaves the cockpit rolled in the
tracking universe and the captions correctly level against it. If the tilt survives this, that is
where it is, and the fix is to recenter with a level head. The texture path was checked and cleared
on the way past: the raster stride is `width * 4` on both sides of the copy, so a shear reading as a
slight rotation is not available.

### The panel can be told where to go (#199)

The withdrawal takes away the only way to put a panel anywhere other than its computed rest pose.
This is the replacement, and it is a delta rather than a row for a reason worth stating: **a
world-locked panel's position is not in `settings.json` at all.** The two unsurfaced fields that
look like the obvious answer — `Drop` and `Pitch` — are read only by the head-locked path, and the
default lock is world, so rows for them would have moved nothing the Commander could see. What
holds the position is the anchor pose in view state, written until now only by a completed carry.

> "move the panel left" · "raise the panel" · "bring the panel closer" · "turn the panel right" ·
> "tilt the panel up"

Five centimetres a step, or five degrees for a turn or a tilt, on whichever panel is on screen —
the big one and the mini one keep their own places. Two of the four axes had to be invented rather
than exposed: there was no lateral offset anywhere, and yaw was read-only, computed by re-anchor
and never configurable.

**Every axis comes off the surface's own pose and none of them off the head**, because a panel put
down in the room stays put while the Commander looks around — resolving "left" against the head
would make it mean something different depending on which way they happened to be facing. Up and
down are the room's vertical and near and far run along the floor, both deliberately flattened: a
panel below eye level is tilted back to be read, so moving it along its own face would raise it
every time somebody asked for it to come closer. Turning pivots about the world's vertical through
the surface's own centre; tilting is about the surface's own lateral axis, and stays a trim on the
derived eye-facing angle rather than an absolute, which is what that derivation was introduced to
stop going stale.

**Both rotations carry a sign that reads backwards**, and both are asserted on where the face ends
up pointing rather than on the angle, which is the right size either way. An overlay's visible face
looks along its own **+Z**, so a positive rotation about X tilts it towards the floor — the bug
`Resting` once shipped — and a positive rotation about Y swings it towards the Commander's
**right**.

**A panel still riding the head is put down first**, which is the carry's own ruling rather than a
new one: picking the panel up has always switched the lock to world, because a Commander who has
moved it has said where they want it. It rests where a first show would rest it — in front of them,
at knee height — rather than at whatever stale anchor a previous world-locked spell left behind.
And a nudge writes the head it was made against, so re-anchoring straight afterwards leaves it
alone instead of undoing it.

Reachable with no model in the path, which is the configuration that matters here: with the
controllers withdrawn there is no Settings tab in the headset to open, and local-only operation is
supported. `move_headset_panel` costs 808 bytes of the advertised surface, which now stands at
41,009 against a ceiling of 50,000.

---

## 0.92.0 — 2026-08-29 — The debrief drafts, the Commander adopts, and the voice gets its cores

Six issues, four parallel sessions, zero merge conflicts.
[#162](https://github.com/dseelinger/d47/issues/162) builds the debrief pass;
[#182](https://github.com/dseelinger/d47/issues/182) finds and removes the floor under
transcription; [#183](https://github.com/dseelinger/d47/issues/183) and
[#184](https://github.com/dseelinger/d47/issues/184) teach the number and letter rungs their next
lessons; [#185](https://github.com/dseelinger/d47/issues/185) and
[#186](https://github.com/dseelinger/d47/issues/186) finish the release tooling review's remainder.

### The debrief pass: corrections become standing directions, adopted by hand (#162)

At the end of a session, d47 reads what the Commander said back over the flight — arithmetic over
words, no model, nothing leaving the machine — and drafts standing directions from the moments of
pushback: *"stop calling it that"*, *"shorter answers in combat"*. Each proposal quotes the
Commander's own sentence, and the new debrief pane shows the exact block that would enter the
prompt, rendered by the same code that builds it. **Adoption is the only road**: a direction's
tier is computed — adopted means Stated, everything else stays Inferred — and there is
deliberately no "adopt automatically" row, no advertised tool, and no way for game text to reach a
proposal: the extractor reads the Commander's voice alone, and the same hostile sentence planted
as an in-game message extracts nothing. Speech cut off mid-line and warnings silenced seconds
after firing surface as *questions*, three occurrences to earn one, never as silent adaptation.
Adopted directions enter the prompt's cached region at the next session, never mid-flight — the
latch, not a convention, is what Phase 54's 23x measurement demanded.

### Four threads on a twenty-four core machine (#182)

[#182](https://github.com/dseelinger/d47/issues/182) asked what share of transcription's flat
three-second cost belonged to the name-hint prompt. The answer is a sixth of it, and the prime
suspect is acquitted — but the measurement that acquitted it found the floor somewhere nobody had
looked. **whisper.cpp defaults to `min(4, hardware_concurrency)` threads and d47 had never
overridden it**, so a 24-core machine was transcribing on four. `small.en` now answers in **one
second where it took three**; Tiny in 0.2 s, Base in 0.4 s.

Sixteen threads is where the curve flattens — twenty-four bought 11 ms more — and four cores are
left for Elite on purpose. On a small machine the rule resolves to four, which is exactly what
whisper.cpp was already doing, so nothing anywhere gets slower. The transcript is identical at
every thread count; that was checked rather than assumed, because `ggml` reduces across threads
and the arithmetic is not bit-identical.

The rest is written down in [docs/spikes/transcription-floor.md](docs/spikes/transcription-floor.md),
because three of the four things it settles are negative results and negative results are what get
re-litigated. The hints cost about 8 ms each. **Whisper encodes a fixed 30-second window whatever
you said into it** — a quarter-second of audio costs what twenty-nine seconds costs, and thirty-one
costs twice that — which is the flat cost the issue inferred, located. Rebuilding the processor on a
changed hint set costs 90 ms, so caching the encoded prompt is a fix for a problem that is not
there. And the prompt cap sits exactly on `ProperNouns.Limit`: past sixty hints the cost stops
rising and never resumes, so raising that cap would buy nothing — the names past it are not slow,
they are ignored.

**Two things the measurement found and did not fix**, both recorded in the finding: the GPU toggle
loads without complaint, reports success and runs on the CPU anyway — only CPU natives ship, and
`UsingGpu` is assigned from the flag that was asked for rather than from anything the native side
said, which is why #182's "the GPU is genuinely working" reads as it does. And which end of an
overlong hint list Whisper drops is still unknown; d47 appends the shipped engineers last on the
reasoning that the Commander's current system should win, and if the tail is what survives, that
ordering achieves the opposite of what it intends.

The transcription log line now carries the thread count, since that line is where this was
diagnosed from and the one number that explained it was the one it did not have.

---

### The number and letter rungs, next lessons (#183, #184)

A grouping comma is punctuation, validated before it is believed — a comma every three digits
makes a number, anything else falls through honestly — so `6,680` is *six thousand six hundred
eighty*, never *six six eight zero*. And a new ruling, decided by the token's own punctuation: a
decimal or a grouping comma makes a **measured quantity**, whose whole part takes the full reading
— `1234.5` is *one thousand two hundred thirty-four point five* — while bare digits keep the
casual designation reading unchanged. The one known cost is written beside the ruling: `1234
tonnes` still reads casually, and overruling is one predicate and one test. The letter rungs
gained the silent-`gh` family (*light*, *night*, *fought*), `ngle` keeps its /ɡ/, syllabic `-le`
widened, and `ck` joined `dge` on the short-vowel side — every reading counted against the shipped
dictionary (181/182, 54/56, 63/64, 64/65) rather than judged by ear.

### The tooling review's remainder (#185, #186)

`release.ps1` now fetches tags and asks the remote whether the next number is taken *before
anything commits* — the stale-checkout collision dies at the start instead of after the merge —
and refuses to sweep a large untracked pile into a release commit without being told
(`-IncludeUntracked`), which is the 2026-08-24 incident made structurally unrepeatable. **Both
guards protect the release after the one that ships them.** `get-ver` reaches every published
version by asking for it directly (the newest-100 page had silently orphaned everything below
v0.37.0), refuses a downgrade on the `prerelease` spec, uses the `isLatest` pin it was already
fetching, and its install step now waits, reads the installer's exit code, self-tests, and only
then says "installed" — a cancelled wizard finally reads as cancelled.

## 0.91.0 — 2026-08-29 — The way back, written down and wired

Fourteen issues, five parallel sessions, one landing.
[#165](https://github.com/dseelinger/d47/issues/165),
[#167](https://github.com/dseelinger/d47/issues/167),
[#181](https://github.com/dseelinger/d47/issues/181),
[#166](https://github.com/dseelinger/d47/issues/166) and
[#168](https://github.com/dseelinger/d47/issues/168) finish what the donation platform started;
[#149](https://github.com/dseelinger/d47/issues/149),
[#151](https://github.com/dseelinger/d47/issues/151) and
[#152](https://github.com/dseelinger/d47/issues/152) make the pickers say what they know;
[#177](https://github.com/dseelinger/d47/issues/177),
[#155](https://github.com/dseelinger/d47/issues/155),
[#179](https://github.com/dseelinger/d47/issues/179) and
[#139](https://github.com/dseelinger/d47/issues/139) continue the local voice's education;
[#178](https://github.com/dseelinger/d47/issues/178) and
[#180](https://github.com/dseelinger/d47/issues/180) close the search's last silent knob and give
the flight recorder a road.

### Taking a donation back is one press (#167, #165, #181)

The Privacy panel's donor row now reads **"Forget it, and delete what was sent"**: one press asks
the store to delete everything filed under the installation identifier — the erasure receipt names
each deleted object — and then forgets the identifier locally. Withdrawal is no harder than consent
was. Donated excerpts no longer land in a public issue: the public road is closed and its signposts
are down. The corpus window gained the send button the excerpt window already had — the payload
spools to a self-deleting temp file and is hashed *above* the compression, so a donor can verify
the receipt's hash themselves with a gunzip and a checksum. And the rule for whether a donated
excerpt may ever become a committed test fixture is written down before the first one exists, in
the Worker runbook: severable by construction, and the donor told before the commit that this one
is permanent. A donated journal history is never committed at all.

### The rules are written down once (#168, #166)

`docs/data-retention.md` gathers every retention rule the code enforces — they lived in five files
and were written down nowhere — held by a test that fails if the page and the code drift.
`docs/donation-privacy.md` says plainly who holds what, for how long, and how to have it deleted.
One behaviour changed rather than merely being described: log retention is now by age and size —
`.log` 90 days, `.jsonl` 14, 4 MB per day — instead of fourteen files per sink, so a log folder
holds about three months (~16 MB typical, 360 MB ceiling) instead of two weeks.

### The pickers say what they know (#149, #151, #152)

Model pickers show each model's price and mark the default instead of listing bare ids, and the
OpenAI list includes the nano tier the price table already knew about. A pool-assigned voice shows
its name, and every spoken line's log entry names who spoke, in which voice, through which
provider.

### The local voice, continued (#177, #155, #179, #139)

A decimal speaks its point — `5.79` is *five point seven nine*, not *five, seven, nine* — and unit
abbreviations after numbers are written out once, before every provider, so `ly` and `ls` stop
being *lee* and *lez* on the voices that guessed. Three letter-to-sound gaps closed, with the `nge`
ruling settled by counting the shipped dictionary rather than judging by ear: long after `a`
(*change*, *range*), short after everything else (*hinge*, *sponge*). And all eight Kokoro ONNX
builds can now be chosen, each labelled with its measured speed beside its size — `uint8` runs
5.4× realtime at 169 MB, half the default's size — with the default deliberately unchanged and
sound quality deliberately unranked (`KokoroProbe quality` writes a WAV per build for ears to
judge).

### The last silent knob, and the recorder's road (#178, #180)

`find_nearest_station`'s `limit` no longer quietly snaps 50 back to 5 — out of range is refused by
name and number, an honoured non-default is echoed — and the same price-age argument is now
spelled one way (`max_price_age_hours`) across both tools that take it. The flight recorder gained
a road that needs no shell syntax: `flight-on` from anywhere, or a shortcut carrying
`--flight-recorder`; per-run either way, remembered nowhere, and still entirely absent unless
asked.

---

## 0.90.0 — 2026-08-29 — Four lanes abreast

Six issues in one release, built in four parallel sessions against one main and merged as one:
[#150](https://github.com/dseelinger/d47/issues/150) and
[#153](https://github.com/dseelinger/d47/issues/153) (the local voice),
[#157](https://github.com/dseelinger/d47/issues/157) (the commodity search),
[#164](https://github.com/dseelinger/d47/issues/164) (the audio flight recorder),
[#175](https://github.com/dseelinger/d47/issues/175) and
[#176](https://github.com/dseelinger/d47/issues/176) (where a donation lands, and the name it
travels under).

### The commodity search's knobs are the model's to turn (#157)

`max_distance` loses its silent 250 ly clamp entirely; `max_price_age_hours` (default 720, ceiling
8,760) and `include_carriers` become arguments rather than constants. A non-default knob is echoed
back in the answer — *"Searched out to 500 ly, prices up to 60 days old."* — an unqualified search
says nothing extra, and a price age past the ceiling is refused by name and number rather than
narrowed in silence. The tool schema grew 241 bytes; the worst-case profile stands at 40,195 of the
50,000 ceiling.

### The audio flight recorder (#164)

Set `D47_FLIGHT_RECORDER=1` and d47 retains, in a capped ring, what actually crossed the audio
boundary in both directions: the buffer each transcription consumed beside what Whisper said it
heard, and what left the speakers beside the text — with, for the local voice, the phoneme string
the Phonemiser emitted, which is the column that turns a mispronunciation from an anecdote into a
diagnosis. A kept row becomes a permanent test case. Unset, the recorder is absent from the surface
entirely, the coverage recorder's rule. The wipe lives on the Privacy panel; recordings are left
out of the rolling data snapshots the way `logs\` is; and the audio never joins a donated excerpt,
because voice is biometric.

### Where a donation lands, and the name it travels under (#175, #176)

"There is no backend" is reversed on purpose: a Cloudflare Worker in `worker/` lands donation
payloads in R2, capped so it cannot bill, holding no secrets — the bucket binding is the
credential, and the shipped binary carries none. Until the Commander provisions it (five written
steps in `worker/README.md`, two of them deliberately manual: activating R2 is the billing act, and
a delete is verified to delete before the erasure sentence is believed) the endpoint setting is
empty and no send button exists anywhere in d47. Donations travel under a random per-installation
identifier — made on first donation, derived from nothing about the Commander, forgettable from the
Privacy panel — so an accumulating history can accumulate without ever naming a person (#176), and
an erasure request has something to claim without linkage doing the naming.

### The local voice: the e English does not say, and a word the Commander gets the last word on (#150, #153)

One neighbourhood — everything between an utterance and the phonemes a local voice is handed.

### #153, and the reported word was not the broken one

Three words came out wrong on 2026-08-28 and they failed in two different ways, which turned out to
be the whole diagnosis.

**`observe` was never at fault, and neither was the rung it was reported against.** It is in the
shipped dictionary as `əbzˈɜːv`, it was looked up, and it was said that way — asserted now as a
unit test over the exact logged sentence, which is the check the issue asked for. What was heard as
*observ-eh* was the word after it. The build speaking at 15:38 was 0.84.4, which still put the
stress mark at the head of a syllable rather than in front of its vowel, and `starport` is not a
dictionary word — so the rules answered `ˈstæɹpɑːɹt`, the shape Kokoro renders as an intruded
vowel, and the intrusion landed in the gap between the two words. `aeda4a3` fixed that at 19:25 the
same evening, four hours after the report, and shipped in 0.85.0. The report was true, the word it
named was not, and nothing in the log could have said so.

**So the log says so now.** Every segment names the rung it came off — dictionary, contraction,
number, designation, rules, spelled, or the Commander's own file — at Debug, under the Voice
subsystem. Turn Voice up and three wrong words are a read rather than an investigation.

**`Guardian` and `Booster` were spelled out, and the cause was markdown.** A segment is spelled
only when it is not `All(char.IsLetter)`, and both are ordinary words — but d47's own prose is
`At a **human tech broker** that carries **Guardian modules**`, from the log verbatim. The token
trimmer covered ASCII punctuation and nothing else, so `**Guardian` failed the letters test,
skipped the dictionary *and* the rules, and was read out *gee, you, ay, ar, dee, eye, ay, en*. It
is the curly-apostrophe bug of `Ship's` a second time, in the same six lines. Markdown emphasis,
curly quotes, the ellipsis and the em dash are now stripped or carried as phrasing — trimmed
together rather than one set after the other, because `**Guardian modules**.` ends in `**.` and
either pass alone is stopped by the other's characters. Every dash is a compound's joint, so
`Booster—engineered` is two words rather than one unsayable run, and the dash between two spelled
segments is still voiced.

**And the silent e, which is the rules gap the report was right about.** `lave` parses as `lav.e`,
so the reduction rule — correct for the a of *Dezhra* — made the e a syllable of its own and
*Lave* came out *lav-uh*. A final `e` with no onset and no coda is now silent, and it lengthens the
vowel one sound behind it: *Lave* is `leɪv`, *Hive* is `haɪv`, *Prime* is `pɹaɪm`. It reaches over
one sound and not two, which is why *serve*, *dense* and *paste* stay short, and it softens as well
as lengthens, because *ace* read with a /k/ would be a new wrongness bought with the old one. A
`-le` or `-re` carries an onset and is untouched: that e is one an English speaker says.

Half the proper nouns in the galaxy end consonant-plus-e, and every one of them said *-uh* the day
it missed the dictionary. *Lave* is in the game's opening credits.

### #150, the escape valve, built early because #153 is why

**A file in `data\` outranks every rung d47 ships with.** `pronunciations.json`, absent by default,
hot-reloaded, per installation. Edit it, save it, say the word again — the whole feature is
*without leaving the game* rather than *without recompiling*. Delete it and shipped behaviour comes
back exactly, which is why nothing writes one back.

Two ways to write an entry, because IPA is expert-hostile. A **respelling** — `"Deciat": "dessy
at"` — is run down the rest of the ladder as if it were the text, so it needs nothing but an ear;
capitals in one are for the reader, since the ladder is case-blind. **Raw IPA**, marked `ipa:`,
goes straight to the tokenizer for the Commander who wants the stress in an exact place.

Keys are whole words, matched longest first, so `Shinrarta Dezhra` is one name and an entry for
`male` can never reach inside `female` — [#146](https://github.com/dseelinger/d47/issues/146)'s
lesson applied before it could be learned twice.

**A bad entry degrades to the ladder and is named once.** Once per version of the file rather than
once per utterance: the file is stamped before it is read, so a Commander hears about a typo when
they save it and not on every line afterwards. *Unparseable IPA* is a real check rather than a
guess — the provider hands the layer its own tokenizer vocabulary, so an entry containing a symbol
this voice has no token for is refused by name. Without that it would have been dropped silently on
the way to the model, and an override that silences a word is worse than the wrong word it
corrected. A file that cannot be parsed at all leaves the last good entries standing, because a
half-written file is what a save looks like from here.

The path is on the Diagnostics page beside the data folder, with whether one has been written yet —
"I edited it and nothing changed" is usually "you edited the other one". `docs/pronunciations.md`
is the page, including the line that stops a bug report: cloud providers phonemise inside their own
service and ignore this entirely.

### What is still wrong, and is nobody's business here

Three things were found next door and left alone. `change` reads as `tʃæŋ` because `nge` has no
consonant spelling; `5.79` loses its decimal point and is spelled as three digits; a coda `h` is
voiced, so a respelling like `tah` says the h. All three are dictionary words or out of scope, all
three predate this work, and none of them is #153 or #150.

---

## 0.89.0 — 2026-08-29 — A history nobody can read, and a way to say yes to it anyway

[#174](https://github.com/dseelinger/d47/issues/174), which stays open — see the end.

**d47 can now donate a whole journal history, and the interesting part is not the reading.**
[#160](https://github.com/dseelinger/d47/issues/160) shipped a control that works because a
Commander reads exactly what would leave and says yes to that, and reading stops at about half a
day. A full history is 936 files and 712,754 events. The same window pointed at it would ask for a
yes to something nobody could have read, which is the consent form this whole path exists not to
be.

### The size argument, which is the only reason this is consentable

The document a person reads is now **O(distinct event kinds)** while the payload stays
**O(events)**. Counted over a real thirteen-month history: 356.7 MB and 712,754 events across 936
files — and **221 distinct kinds**, of which the scrub changed 56. The report comes to 236 KB, and
it carries one real scrubbed line of every kind rather than of the interesting ones, because an
inventory that shows only what was touched is a curated one.

Staging the review into sessions was the obvious answer and it is not one: 936 reviews is as
unreadable as one 936-file payload. The number of kinds does not grow with the number of sittings,
which is what makes this finite.

**The sample for a kind is its longest instance**, and a changed instance always beats an untouched
one of the same kind. A reader checking this report is checking the scrub, so they get the
maximal-exposure line rather than a typical one. Each fold says how many characters it holds,
because the samples are not the same order of magnitude — most run to a couple of hundred and one
`StoredModules` is 58,535, a quarter of the document in a single line.

### Two passes, no temporary file, one set of stand-ins

The survey keeps counts and one line per kind; the write pass scrubs the same files again straight
into the file the Commander picked. So nothing exists on disk until they have said yes, memory
stays at one journal file whatever the history weighs, and **the samples in the report are the
lines in the payload** rather than a second rendering of them — #160's "what is shown is what
leaves", holding on a payload that cannot be shown in full.

**Lines the parser cannot read are counted now** — eleven over the real history. The excerpt path
drops them silently and can afford to, because that payload is read in full; nobody reads a
history, so a silent drop is invisible by construction.

**Changing the scope throws the reading away.** A report describing twelve months above a Save that
would write thirteen is a yes to a document that does not describe what left.

### It says journal history, not corpus

*Corpus* is this repository's word — #174's own title, `spike/CorpusReplay`, `CLAUDE.md`
throughout — and it says nothing to a Commander looking at a button. The strings a person reads
changed; the type names did not, because they are internal and `spike/CorpusReplay` would disagree
with a rename the first time somebody read both.

### What is not here

**The destination.** The window writes a file and deliberately names no place to put it:
[#175](https://github.com/dseelinger/d47/issues/175) is where a hosted one is argued and it is
unbuilt, and naming a destination that cannot honour what it promises is the mistake 0.88.0 already
corrected once. That is why #174 does not close — it asks for a transport as well as a consent
step, and only the consent step is built.

**A hand-driven run.** The Core path was driven over all 936 real journals and the report read as a
document, and the window and its button were driven through the real panel headlessly. Nobody has
pressed the button and picked a file. That is why this is a pre-release.

## 0.88.0 — 2026-08-29 — The tools that said one thing and did another

Five issues, and one thread runs through all of them: a thing that reported success it had not
earned, or offered a reach it did not have. None was found by reading the code. Every one came from
an analysis of what the tools actually do, and two were caught only by driving them.

### The release chain says what it did

[#169](https://github.com/dseelinger/d47/issues/169),
[#170](https://github.com/dseelinger/d47/issues/170),
[#171](https://github.com/dseelinger/d47/issues/171),
[#172](https://github.com/dseelinger/d47/issues/172).

**The tag now names the commit CI greened.** `git tag` with no ref takes HEAD, while the wait
matched CI's run server-side against the commit it pushed — so anything arriving from another
terminal mid-wait, or during a confirmation left sitting, took a signed tag CI had never seen.
`release.yml`'s post-tag rerun was the only thing standing in the way, so **it goes second**: with
the tag pinned it re-tested a commit `ci.yml` had already greened, and its removal takes a full
serial suite off every release while closing a hazard of its own — a flaky failure *after* the tag
leaves a signed tag with no Release behind it, the failure this file already records as costing a
version number.

**The local suite is opt-in.** `ci.yml` runs the same one on the same commit and the wait refuses to
tag a red result, so skipping it costs minutes and never a version number. The rule survived the
polarity flip: `-SkipCi` removes the other half of the check, so it turns the local run back on
rather than being refused. `prerelease` can pass `-Tests` and `-Message` through now; it could
previously say nothing at all to the script it hands over to.

**The deepest rollback no longer destroys itself.** At the steady state `data-backup` creates — ten
held, one per deploy — the pre-restore snapshot is an eleventh and the trim dropped the oldest,
which is exactly what a Commander reaching for the oldest was about to read. Reproduced end to end
against the unfixed script first: it dropped the target, `Expand-Archive` threw on a path that no
longer existed, and `Remove-Item -Force` is not a recycle bin. `-Keep` is range-checked too —
`-Keep 0` was a delete-all one typo away.

**And the unattended chain sets exit codes.** The pre-release timeout throws rather than returning 0
while the build it could not mark heads for latest; `prerelease`'s did-nothing stop is nonzero, so a
caller can tell it from a release; a `gh` that fails says so instead of continuing silently; and
when every label lookup failed it refuses to decide rather than calling a phase a patch — failing
toward asking, never toward a guess with a permanent receipt.

### The window said sixty minutes and meant twenty

[#173](https://github.com/dseelinger/d47/issues/173). The excerpt shipped in 0.87.0 with a
minutes-before / minutes-after control, and **that control was never what bounded it**. Three
ceilings sat underneath, none of them crossable by turning the dial up:

- `JournalSpine` tails the newest journal file, so the journal half reached the start of the current
  Elite session and stopped.
- `JournalLog` holds 4,000 events — generous at a measured median of 3 events a minute, and still a
  hard stop.
- `LogTail` reads the newest `d47-*.log`, so the log half could not cross midnight.

A Commander who restarted d47 and then asked for an hour got twenty minutes, silently. **The defect
was never that the window was small; it was that the control implied a reach the sources did not
have.**

### Both halves read from disk now

`IncidentSources` walks the journal folder and the retained logs for the window asked for. The
journal side uses the filename as an index — Elite encodes the session's start in it, so a file that
began after the window ended is never opened — with one care taken: **the lower bound cannot be a
filename.** A session running six hours has a start long before the window and events inside it, so
the file that was open when the window began is read whatever its name says.

**And an entry now carries the day it happened on.** The human-readable sink writes a time of day
and no date, which was survivable while an excerpt could only ever read one file and stops being so
the moment it reads two: `14:02:11` is then ambiguous between them. The day comes off the filename,
the offset from the zone the sink wrote in, and the window compares instants — which is why the
wrap-around-midnight special case the old code needed is simply gone.

`IncidentExcerpt.Take` still opens nothing and reads no clock. What is on disk and what is done to
it stay separate questions, which is what keeps the scrubbing drivable from a test with no machine
underneath it.

### And the control says what it means

Named spans — ten minutes, an hour, six hours, twelve — instead of two steppers.
**The list stops where reading stops, and that number was measured rather than chosen.** Driven
against a real Commander's journals and logs: six hours came to 48,000 characters, about one GitHub
comment; twelve came to 734,000; two days to 1.8 million and a week to 5.3. A span nobody can read
is not a wider version of this feature but a different one, and it is
[#174](https://github.com/dseelinger/d47/issues/174).

The size line says the real problem now rather than only GitHub's: past sixty thousand characters it
reads *more than a person reads, and more than one GitHub comment holds*, because the yes this
window asks for is a yes to something read.

### And it has stopped promising an erasure it cannot perform

The report said *"This excerpt lives in this issue and nowhere else… Ask here and it is deleted."*
**Both halves were untrue the moment the destination was a public repository**, and 0.87.0 shipped
them: a comment there is mirrored to third-party archives within the hour and mailed whole to every
watcher, so deleting it from GitHub recalls nothing. That is why GitHub is out as a destination
([#165](https://github.com/dseelinger/d47/issues/165)) — not awkwardness, but that no removal
request could ever be honoured on it.

It now says what is true where the donation is made: scrubbed on that machine, sent from nowhere,
and a warning that anything posted publicly can be copied beyond reach. **A promise is worth what
the destination can keep**, and the destination is still being chosen.

---

## 0.87.0 — 2026-08-28 — The events behind the complaint, with the words taken out

[#160](https://github.com/dseelinger/d47/issues/160). A defect report arrives as prose, and the
events that produced it stay on the reporter's machine — so the fix is tested against a
reconstruction. **d47 can now cut the incident out of what it already holds**: the minimal journal
sequence, the matching slice of its own log, scrubbed on the machine, shown in full, and handed
over only on an explicit yes.

Two halves, doing different work. The **journal half is the replay case** — `spike/CorpusReplay`
already drives any directory of journal lines through the production fold, so a donated excerpt
joins the suite with no harness changes and the fix cannot regress silently afterwards. The **log
half is the diagnosis**: what d47 heard, what it decided, what it said, and how long each step
took. A report wants both.

### The journal is scrubbed by a field list; the log cannot be

The personal surface of a journal is short and enumerable, so it is enumerated — `Commander` and
`LoadGame`, chat, friends, crew and wing joins, ship names, squadrons. Names and Frontier IDs
become consistent stand-ins; message bodies are dropped and the event's shape is kept, because a
`ReceiveText` with no `Message` takes a path no live event ever takes. Everything else travels
untouched: an `FSDJump` names a star system and an economy, not a person. A line the scrubber
cannot read is **withheld whole and counted** — a scrubber that passes through what it could not
read is not a scrubber.

A log is free text and no field list can reach into a sentence, so the rule inverts. **The
outburst is the bookmark, not the payload**: the instant travels and the words do not. d47's own
lines travel by default, and for those the show step *is* the control — free text is reviewed as
free text. The Commander's own speech is **held back unless they say otherwise**, per incident,
because sometimes the exact words are the bug — a mishearing is reproduced by what was misheard —
and sometimes they are nobody's business. **Another player's words never travel at all**, and
there is no switch for that one: it is the same rule the journal half applies to chat, and a donor
cannot consent on somebody else's behalf.

### A pirate is not a person

The events that name somebody who is not you — `PVPKill`, an interdiction, a death — fire on a
person and not on a Frontier pirate. **Elite answers that question itself** on an interdiction,
with `IsPlayer`, so the rule is conditioned on the flag rather than on the shape of a name: a
condition read out of the event is still a field list, where a condition inferred from how a name
looks would be the guesswork this is meant to avoid. A missing flag is not permission. `PVPKill`
needs no condition — its victim is a player by definition — and a `Died` carries no flag at all,
so there *cannot tell* resolves to scrub: over-replacing a Frontier pirate costs a replay a name
nothing reasons about, and under-replacing hands over the one thing this exists to keep.

**And a Frontier symbol is not a name.** `$ShipName_Military_Federation;` killed the Commander
eleven times in the corpus; replacing it would break a lookup a replay may key on, and no
Commander is called one. Its translation goes with it — `X` and `X_Localised` are one datum
rendered twice, and scrubbing only the readable half produced a killer who was
`$ShipName_Military_Federation;` in one field and `CMDR ALPHA` in the next.

Measured across the 912-journal corpus: **75 of 90 combat events now pass through untouched**,
where all 90 were being rewritten before. The Commander does not fly Open, so not one of them was
ever a real person — which is the point. The rule is there for the donors who do.

### A carrier is PII, name and callsign both

The Commander's ruling of 2026-08-29: **both can be looked up on INARA**, and the callsign is the
key that site indexes carriers by, so it ties a carrier to an owner more reliably than the name
does. `CarrierStats` restates the name 491 times over the corpus, which is what puts it inside any
incident window where `SetUserShipName`'s equivalent would never be.

**The callsign travels in twenty-odd events and only five of them say what they are.** `Docked`,
`Location`, `Market` and the docking handshake carry `StationType`, so a per-event condition would
have worked there — and `Shipyard`, `Outfitting`, `StoredShips`, `StoredModules` and `FCMaterials`
carry nothing to condition on at all. Those are the events that list a Commander's whole fleet. So
the rule is a field list whose treatment guards itself on shape, and it reaches every event.

**Position is what makes a shape rule safe here**, and it was measured rather than assumed. A
carrier is either the callsign alone — `B0X-79X`, 24 of 968 distinct station names, all 24 carriers
— or a name with the callsign **last**: `GDS PREDATOR B0X-79X`, `EXAMPLE HAULAGE Q7Z-1AB`, 15,002
distinct values and every one a carrier. A megaship wears the same shape at the **front**
(`MVU-891 Bellmarsh-class Reformatory`, 464 distinct) and a minor faction wears one in the middle,
off the catalogue number of the star it is named for (`LP 466-235 Gold Boys`, 63 distinct). Both
are game facts, both stay, and both would have gone under a rule that looked for the shape anywhere.

**Two of the fields were found by sweeping rather than by reading the schema.**
`SupercruiseDestinationDrop.Type` mixes Frontier symbols, ordinary stations and carriers with
nothing on the event to tell them apart; `CodexEntry.NearestDestination` says what you were closest
to when you scanned something. Between them, 205 lines that a table written from the schema would
have kept. **Across the corpus: 170,747 lines Elite calls a fleet carrier, none of them still
holding a callsign afterwards, and 2,003 megaship and faction lines untouched.**

### A squadron is PII, and pulling that thread found five more things

The Commander's ruling of 2026-08-29: **a squadron of one is a pseudonym for a person**, and its
name and its id both resolve on INARA. A minor faction is not — it is Frontier's, it belongs to the
galaxy rather than to anybody, and it stays. The id keeps its type through the scrub: Elite writes
it as an integer on every event but one, and a replay handed `"SQ01"` where it expected a number is
not redacted, it is corrupt.

Asking the corpus what else still named a real person after the rules ran turned up five more, and
**none of them would have been found by reading the schema.**

- **`$cmdr_decorate:#name=EXAMPLE CHARLIE;` — a real player wearing a symbol's clothes.** A hole this
  build opened for itself: `ReceiveText.From` had been scrubbed since the first version, and the
  rule sparing Frontier's `$…;` symbols quietly stopped it. 15,970 values across the corpus, in
  chat, in `ShipTargeted` and in `Bounty`. The decoration is now spliced — the name goes, the
  wrapper stays, because a replay that undecorates names takes a different branch on a value that
  is no longer decorated. `$npc_name_decorate:` is untouched: an NPC is not a person.
- **`PilotName_Localised`, which leaked *after* the raw half was fixed.** Rules run in table order,
  so by the time the prose half is reached its partner already holds a stand-in and the real name
  is only in the map. One person now reads as one stand-in in both fields; two would be a person
  the report's reader cannot follow.
- **`ShipTargeted.PilotName` and `Bounty.PilotName`** were not on the list at all.
- **`LoadGame.Group`** — a private group, which people name after themselves. 78 of the corpus's
  `LoadGame` events carry another Commander's name there.
- **`CrimeVictim.Offender`** — a real person every time; an NPC does not generate one.

### And a squadron's carrier is not identified like anybody else's

The whole carrier ruleset above is built on the `XXX-XXX` callsign, and **a squadron carrier has
none**. `Callsign` holds the four-character squadron tag, `StationName` holds that bare tag, and a
scan reads `EXA EXAMPLE HORIZON | EX01`. Three shapes now, not one — the callsign alone, a name
with the callsign last, and a name with a tag after a pipe — plus the tag itself wherever the event
says it is at a carrier.

**The five events that say nothing about what kind of station they are** — `Shipyard`, `Outfitting`,
`StoredShips`, `StoredModules`, `FCMaterials` — are covered by what the excerpt has already ruled
on rather than by a shape: a bare `EX01` is indistinguishable from a station's name until some
other event in the same window says otherwise, and one always does. An ordinary station is not in
that map and comes back untouched.

Swept the way an excerpt actually scrubs — one set of stand-ins per journal — **3,870 corpus lines
naming a real person, group or squadron tag, and none of them still naming one afterwards.**

### The flag that undid the squadron scrub

An excerpt replaces `EXAMPLE SQUADRON` with `SQUADRON ALPHA` and its id with a stand-in — and then a
jump three lines later points at a minor faction and says `SquadronFaction: true`. One hop on INARA
from there to the squadron and its member list. 275 events over the corpus, across `FSDJump`,
`Location` and `CarrierJump`, flagging two factions.

**Dropped rather than falsified.** There is nothing to stand in for a `true`, and writing `false`
would be a lie to whoever reads the report. The minor factions themselves stay, on the Commander's
ruling; what goes is only the sentence saying which one is theirs. It costs nothing — d47 reads
neither this field, nor `SquadronName`, nor `SquadronID`, so the production fold behaves identically
without it. That was checked rather than assumed: the first read of this said dropping it would cost
a replay real game state, and a `grep` said otherwise.

The report counts it and says so, because a report that quietly takes something out is a report
making a claim it has not stated. Across the corpus: 275 of 275 cleared, and nothing dropped from
any of the other 712,432 lines.

### Fail-closed did not hold, and Elite is why

The same sweep crashed. **Elite writes duplicate keys** — an assassination mission carries `Target`
twice, 11 lines over 912 journals — which `JsonNode` parses happily and then throws on at the first
enumeration, with an exception type the scrubber's catch did not name. It escaped, which for a
component whose whole contract is *a line I could not read does not travel* meant reaching a
Commander mid-donation as a crash instead.

The catch is deliberately wide now. Guessing the next shape Frontier writes is a worse bet than
holding everything: the cost of withholding a line that would have been fine is one line, and it is
counted and stated.

### One thing the tests could not have found

The pseudonyms cross from the journal half into the log half, or they are worth nothing — a
scrubbed `LoadGame` three lines above the real name has protected nobody. That worked in every
test and leaked on the first real session. Elite writes `Commander` and `LoadGame` **once, at the
front of a file**, so an incident three hours in contains neither and there was no stand-in to
substitute; d47's log meanwhile names the Commander in what it *says* — *"JOHN DEPARAGON is in
Eurybia, docked at…"* — 107 times in one day. Who is flying is now handed in from outside the
window. The Windows account name goes the same way, for the same reason: the log prints it on
every path, dozens of times in a startup, and a show step that has to catch each one by eye is a
show step that will miss one.

### One act per donation, and no backend

The excerpt is shown in the window that will hand it over, rendered by **the same code that fills
the clipboard** — a preview assembled one way and a payload assembled another are two artefacts,
and the Commander only ever read one of them. Copying closes the window, so the next donation is a
fresh decision rather than this consent spent twice. There is no standing consent, no remembered
choice, and nothing that sends anything: a scrubbed excerpt is kilobytes and travels inside the
GitHub issue itself, pasted there by hand. Having the mechanism does not make it automatic — the
`promote` rule, applied here.

`Donate excerpt…` sits beside `Copy All` on the Journal, Raw Journal and D47 Log readings, and on
the desktop window alone. The headset has neither a clipboard to put the result on nor a file
picker to write it with, and a surface that cannot finish the act does not offer it.

**The pane wraps, though a payload reads better as the lines it is.** Unwrapped with a horizontal
scrollbar was the first cut, and rendering it against a real session settled it: every paragraph
above the payload — what was replaced, what was withheld, what is being agreed to — ran off the
right edge. A wrapped journal line is ugly; a consent notice you have to scroll sideways to find
is worse than ugly.

Whole-journal corpus donation stays out of scope on purpose. Storage was never the blocker —
935 journals are 32.5 MB gzipped, so 500 Commanders would be about 16 GB — consent design is, and
custody of other people's play histories is not taken on speculatively.

---

## 0.86.0 — 2026-08-28 — Four answers that were sure of themselves

Nothing new here, on purpose. Four issues where d47 said something wrong, or said the right thing
at the wrong moment, or said the right thing far too many times — and the Commander's reason for
taking them ahead of any capability: *"The first user has adapted to these; the second hasn't. A
new user's trust forms in the first week, and a wrong answer delivered confidently costs more of it
than any missing feature."*

### The radius answered was not the radius searched

[#156](https://github.com/dseelinger/d47/issues/156). Asked for the closest place to buy 200
Landmines near Eurybia — the Liz Ryder tribute — and told, twice, with rising confidence, that there
was no stock within 150 light years and no buyer out to 250. **The answer was wrong by a factor of
twenty.** Coleman Relay in Enayex had 5,229 units of them eleven light years away, and d47's own
data source said so the same hour.

The sweep fetched the **nearest 150 stations and nothing else** — three pages of fifty, sorted by
distance, no commodity filter — and the ranking then ran locally over whatever those 150 happened
to stock. Near a bubble system the 150 nearest markets span a few light years, so a commodity none
of them carried was reported absent from the entire radius. The sweep's own comment said extra
stations cost *"arithmetic rather than correctness"*: true for the trade planner it was written
for, and false for a named commodity, where the stations past the horizon are exactly the answer.

**So a named commodity now goes into the request.** The 150-station budget is spent entirely on
stations that hold stock of the thing, and the horizon stops mattering for the case that had it.
The general sweep — trade planning, colonisation sourcing — is untouched and asserted untouched:
those questions have no single commodity to narrow by.

**Three findings, measured against the live service on 2026-08-28**, and one of them corrects the
issue. Within 15 light years of Eurybia: 449 stations unfiltered, **26** carrying a Landmines row,
**8** with any supply, **12** with any demand. So the name-only filter shape is *honoured*, not
silently ignored as reported — that reading came from a count pinned at the endpoint's own 10,000
cap, which says nothing either way at 250 light years. It is still not the shape to send: a row is
not stock, and it matches every shelf quoting zero. **And demand bounds are honoured here**, which
answers the sell side's open question — the note recording them as accepted-and-ignored was
measured on the *trade* endpoint, which is a different one, and now says so.

**And where the budget still runs out before the radius does, the answer says how far it got.**
*"Nothing in the 150 markets I could check… those reach 14.2 light years of the 250 you asked
about, so there is more out there I have not looked at."* The heading on a positive answer names
the same distance. The honesty rule was already written one function away — *"stations dropped for
being too old are counted rather than swallowed"* — and the horizon had never been given it.

### A keyword named a capability, and the router guessed the tool

[#161](https://github.com/dseelinger/d47/issues/161). *"What's the Cobra Mk III's jump range?"*,
asked out loud three times and answered each time with *"JOHN DEPARAGON is in Kamitra, near Hammel
Terminal, docked at Hammel Terminal."* The model never saw the question.

`jump range` was a Journal keyword; keywords are matched as **contained** phrases and reach a
capability rather than a tool; and the router then took that capability's first tool with no
required parameters — a **positional** pick, out of whatever order somebody had declared them in.
For Journal that is `get_location`. This is the 2026-08-21 carrier defect still live on the other
road: declared phrases fixed it for the exact wordings written down, and a phrase must be the whole
utterance, so any padding at all fell back through.

**The mechanism was general, and it was worth measuring before choosing.** Twelve capabilities had
more than one tool the router could call, so twelve had the same trapdoor — and one had already
fallen through it with nobody reporting it: Conversation declares `cancel_turn` first, so **every
phrase it owns cancelled the running turn** instead of answering. *"Which model"* did not report
the model. It stopped the turn.

Of the three roads the issue put up, **a keyword now names its tool**, and a keyword that names
none on a capability with several eligible tools *declines* rather than guessing. Narrowing was
rejected as fixing only the sentences somebody thinks of; naming loses no reachability, because the
phrases already work this way. A bare string still means the whole capability, which costs nothing
where there is one answer to give — most of the registry — and a test walks the registry so the
next capability to declare a keyword and a second argument-free tool fails rather than guessing
quietly.

**`jump range` itself was also narrowed to `my jump range`**, and that is a decision about the
*subject* rather than about the tool. Naming `get_ship` would have answered a question about a
Cobra with the range of the ship the Commander is sitting in — the right tool and the wrong hull.
Every other keyword in that list carries a possessive or a location word; this one is a general
Elite topic whose commonest use is not self-referential at all. So the possessive has to be in the
phrase, and anything else falls through to the model, which has the specification tables.

### Silence until it was over

[#158](https://github.com/dseelinger/d47/issues/158), and the Commander's own widening of it the
same day: *"Also add the acknowledgement to the galaxy map plot macro."*

*Take us out* ran the panel gate, the walk, and **up to thirty seconds** of watching for the pad
clamps before a word was said — and the successful line was literally *"Taking us out."*, the
present tense arriving in the past. The separations boost until the mass lock breaks under a
twenty-second ceiling, in silence. The galaxy-map plot opens the map, settles the camera for three
seconds, types, settles, holds and closes, and in a headset you watch the map fly about on its own
with nothing to connect it to what you asked. That gap is what produces a repeated command, and a
repeated command mid-macro is its own hazard.

**One rule for all four: acknowledge at accept, verdict when known.** *"Taking us out."*
*"Separating."* *"Plotting the course to Shinrarta Dezhra."* — the plot names the system, because
the name is the payload and a misheard one is caught five seconds earlier than the verdict would.
The launch's success line is now *"We are away."*, which is a verdict rather than an announcement.

**A refusal is still exactly one line, immediately**, which is why the acknowledgement sits inside
each macro after its own pre-flight rather than in front of them all: *"Taking us out"* followed by
*"you are not docked"* would be worse than either. The clipboard fallback is refusal-shaped and
stays single too. It is not awaited — speaking takes seconds and these are macros whose whole value
is timeliness — and the ordering asserted is that the line was said **before the first key**, not
merely that two sentences came out.

### The proposal that repeated itself, verbatim, forever

[#154](https://github.com/dseelinger/d47/issues/154), reported with the sentence attached and then:
*"This is freaking annoying."* Three defects in it, and the annoyance was the designed behaviour of
the first.

**It was appended after every turn that did not resolve it, unchanged, with no way to quiet it
short of answering.** The rule was already written down about d47's own lines — `ChecklistItem.Noted`
exists because *"nagging about every line d47 has no table for is how a Commander learns to stop
listening"* — and had never been applied here. It is now said **in full once, as a short clause
twice, then not at all**, while the proposal stays on the panel until it is answered. Going quiet
is not forgetting. *"Decline everything"* clears the queue outright by voice, alongside the
*"never mind"* that already did.

The count is advanced by the thing that appends rather than by the line itself, because
`TurnLoop` asks for it twice a turn — once before the model speaks and once after, which is the
comparison that makes it a statement of fact instead of a nag — and a line counting its own
repetitions would decay at double speed.

**The sentence was cut mid-word, by a cap borrowed from the wrong feature.** It died at *"Grade 5
Dirty Drive Tuning on M."* — `ChecklistLimits.MaxTextLength`, which bounds a checklist *line*,
applied to a spoken sentence with no ellipsis and no regard for word boundaries. *"on M."* reads as
a finished clause. Summaries are now **composed to fit** rather than truncated to fit: a ladder of
whole sentences, most detailed first, and the backstop stops at a word and says so when it fires.

**And what it said was an inventory rather than a description**: six slot names, then three
modifications with nothing to pair them, two of the slots read out as raw journal fields. A
revision now says its shape and its size — *"Set six slots on the Cartage (Type-8 Transporter)
plan: …"* — and names slots only while naming them is possible and useful, through the same wording
layer the checklist lines use, so a small revision still says exactly what it changes and
`Slot05_Size5` is never read aloud. The manifest is what the panel's proposal view is for.

## 0.85.0 — 2026-08-28 — Seven that were asked for, and three that flying them turned up

Seven issues off the queue, and then the local voice was flown to check one of them and gave up
three more. Both halves are here, because a green suite and a working feature are different claims
and this release is the second one saying so this week.

### Say the number the filter already computed

Two callouts stated their inputs and withheld the answer those inputs were being compared to
produce. Both had the number in hand at the moment they spoke, and both threw it away.

**The limpet reminder never said what would silence it** —
[#140](https://github.com/dseelinger/d47/issues/140), reported as *"doesn't actually tell me how
many limpets to put in the hold (total) to get it to stop complaining"*. It now solves that total
from the same comparison that fires it, the threshold inequality run backwards and rounded up, read
through the same setting on the same examination. *"You have 256 tonnes to fill. Buy 13 and I'll
stop asking."* Buying exactly that silences it and one fewer does not, asserted at the boundary
against the number the line itself named — because a Commander who buys the stated number and gets
nagged anyway has been lied to by arithmetic.

**High grade emissions named three materials with no size on any of them** —
[#132](https://github.com/dseelinger/d47/issues/132). It read identically at five units of headroom
and at a hundred and fifty, which is why it read as a broken filter to a Commander sitting at 95 of
100. The filter was right. It now says the room left for each, from the one capacity lookup that
also decides what to skip: *"Proto Heat Radiators, 5 short; Proto Light Alloys, 36 short and Proto
Radiolic Alloys, 20 short."*

**No near-full threshold ships, and that is the decision rather than the omission.** The complaint
was never *stop talking* but *why are you telling me this*, and a number answers that. A Commander
finishing one specific roll wants those last five.

### The tower is the tower from the first line, and both halves were real

Three messages from the Commander's own carrier were read out in a stranger's voice
([#109](https://github.com/dseelinger/d47/issues/109)). The matcher was right and its input was
late: the callsign is learned at the dock, and docking chatter is by definition the traffic that
happens **before** the dock. Forty-seven seconds after all three.

So the identity is now taken from the events that come first. `DockingRequested` and `DockingGranted`
carry everything the existing test needed and simply were not in its list — counted over 935
journals, **859 and 857** at a fleet carrier and every single one carrying `MarketID`, `StationName`
and `StationType` together. And the supercruise drop, whose `Type` is the whole decorated string,
twelve seconds earlier still — kept whole rather than filed as a name it is not, because it exists
to be matched against rather than said. Neither covers the other: the corpus has approaches with a
docking request and no drop before it.

**And then flying it found that this changed nothing a Commander could hear.** A re-voiced message
always carries a speaker, so every such line went to `VoiceCast.ForSender`, and there the role only
decided which scope the assignment lived in and what to fall back to when the pool was empty. **The
cast voice was unreachable for any line with a sender on it.** The tower spoke in whichever pool
voice its callsign happened to hash to, exactly as an unknown NPC would, and the voice cast for it
was never used for a word the carrier actually said. That is why the report read as an identity
problem and was one — both halves are real and neither is sufficient. A role the Commander has cast
now keeps its voice; only the carrier's two are ever cast, and each is one person.

### An alarm on being shot, and an answer for being hunted

**The cue was on the warning that an attack might happen and not on the one saying it is**
([#136](https://github.com/dseelinger/d47/issues/136)). Four announcements in the whole app set one
and all four were about something that had not happened yet, so a pirate *announcing* an
interdiction got a sound before the sentence and being shot did not.

Every urgent danger line now carries one and the routine ones still do not — the existing urgency
distinction doing the work rather than a second judgement about which dangers are frightening. **Two
new sounds, and two is a decision rather than a count that grew:** shields, hull and under-attack
are one situation reported by three sensors and share an alarm; being pulled reuses the interdiction
sound the Commander already learned; heat gets its own, because nothing is shooting and the answer
is the throttle rather than the trigger. It is also the only alert here that does not fall.

**Two alarms of the same kind seconds apart now sound once.** The keys differ — correctly, they are
different warnings, both worth saying — so no cooldown could have caught it. The spacing is on the
cue and is applied after the key cooldown, so a repeat that is never said cannot silence the next
warning that is. Only the marker is dropped, never the line.

**And the hitman half** ([#137](https://github.com/dseelinger/d47/issues/137)), measured over 935
journals with the three shipped lines as controls, which the method reproduced exactly. The family
that fires when a hunter spots you is **7 of 7**, the strongest signal in the corpus; near-death is
**2 of 3** and ships on the terms the bounty hunter's single event shipped on, with its n written
down. Both share the bounty hunter's cue, because the answer is the same and cargo will not buy
either off.

**The two the Commander actually noticed do not qualify.** *"The eagle is in the nest"* is followed
by an attack **15%** of the time and cueing it would be the crying wolf the allowlist exists to
prevent. But 15% of 47 is still a hitman talking about you, so it gets a reaction instead: d47's own
words, chosen by the id family, off the cue channel, one per ten minutes however long the burst. It
never says why — of 30 such lines in the corpus, **one** had a failed mission behind it, so a reason
would be invented. The line handed to the model is d47's own and never the hitman's, which is
asserted rather than argued, because the reaction is the one thing here that goes through a prompt.

### Male is a word, not a substring of female

Typing **male** in the voice picker listed every female voice
([#146](https://github.com/dseelinger/d47/issues/146)), because matching was `Contains` and
`"female".Contains("male")` is true. There was no way to type out of it either: *female* worked and
*male* could not.

This repository had already ruled on the same failure one surface over — Phase 52, on the spoken
router: *"a keyword that short hijacks every sentence containing it"*. The equivalent for a search
box is matching at **word starts**, which fixes *male* without costing `eng` finding *Engineering*
or `kra` finding *Krait*, where whole-word matching would have broken both. A capital following a
lower-case letter counts as a word start too, or *multilingual* would silently have stopped finding
`en-US-AndrewMultilingualNeural`, which most of the Edge catalogue is named like.

**And the fault underneath it.** Gender is a field that the label merely renders, so searching the
label for it was the wrong question. The picker now offers it as a filter, declared by the row the
way every other row property is, and absent where the choices carry no gender at all. **Untagged
voices get an option of their own** rather than being swept in with the men or quietly dropped —
that is where the filter deliberately differs from the casting rule, which folds them in because
casting has to put every voice somewhere and a Commander reading a list does not. Both are built on
one comparison, so the picker's filter and Phase 58's casting are one rule read twice, asserted from
a single catalogue rather than compared by eye.

### Cobra Mark Three, not em kay eye eye eye

The phoneme ladder ends in *anything left is spelled out*, and a roman numeral is letters with no
digits and no vowels to parse ([#138](https://github.com/dseelinger/d47/issues/138)). **74 entries
in the shipped tables carry one**, and each carries it into every armour row underneath it. `Mk` is
fixed with it, because *em kay three* is no better than what it replaced.

It runs over the whole line rather than inside the per-segment ladder, because the context spans
segments — `MkII` arrives as one and `Mk II` as two, and both have to sound the same. What it emits
is ordinary English, so *Mark* and *three* then come out of the dictionary like any other words.

**The half that would have been found in a headset is the other direction.** `I`, `MIX`, `DID`,
`CIVIC`, `MILD` and `LIVID` are English words built from numeral letters, and a general rule would
turn prose into numbers in a voice. So the conversion fires only where the context says numeral,
strictness comes from parsing and rendering back — `MILD` parses as 1449 and 1449 renders as
`MCDXLIX`, so `MILD` is not a numeral — and two contexts are narrowed further: bare `I` after a
written-out *Mark* is left alone because *"Mark I saw him"* is a sentence, and `class` requires the
gas giant after it because *"the class I attended"* is one too.

### Mark the vowel, not the syllable

Reported while flying the numerals, as an intruded vowel before every invented name: *"JOHN **ay**
DEPARAGON is in **ay** Kamitra, near **ay** Hammel Terminal."* The text was clean and the phonemes
were clean English, so the sound was being made somewhere neither could show.

**The reported line is what makes the cause certain rather than likely.** Every word an intruded
vowel was heard before — Deparagon, Kamitra, Hammel, Hammel — is a word the letter-to-sound rules
answered for. Every word without one — John, is, in, near, Terminal, docked, at — came from the
dictionary. Four for four and seven for seven, in a single sentence.

And the dictionary says what the difference was. Of its 274,927 entries, **not one** begins with a
stress mark followed by a consonant: it writes `dʒˈɑːn` and `tˈɜːmɪnəl`, marking the vowel rather
than the syllable. The rules marked the syllable — `ˈdɛpæɹæɡɑːn` — so every name they produced
reached Kokoro in a shape it had never once been given, and Kokoro rendered the unfamiliar shape as
a vowel. Which syllable is stressed is still the old crude first-syllable judgement and is
untouched; only its position inside the syllable has moved.

**It lived in three places and the fix reached one.** The rules were the reported case; the
hand-written tables were not. **Eighteen of the thirty number words** marked a consonant — `ˈθəɹti`,
`ˈsɛvən`, `ˈhʌndɹəd` — and so did `w`, `zero` and `seven` in the spelled letters. Those matter more
than the names did, because d47 says numbers in most of what it reports: ranges, distances,
tonnages, credits. The number words are now the shipped dictionary's own entries, which makes them
derived with a source rather than judged by ear, and settles two the ear had got wrong on their own
account. The guard drives every rung that produces IPA and asserts one property — wherever a mark
appears, the next sound is a vowel — so a fourth table cannot be added quietly with the mark in the
wrong place, which is exactly how the second and third came to be there.

### Say the prepositions weak, which is nine marks and not a revolution

The dictionary stores citation forms, which is right for a lookup and wrong for a sentence: d47
emphasised the preposition in *"is **in** Kamitra"* and the auxiliary in *"you **have** 256
tonnes"*.

**This was reached for as a fix for how flat the local voice sounds beside ElevenLabs, and measuring
it first killed that argument.** Over ten lines d47 actually said, **9 of 84** stress marks land on a
function word. Eleven percent. The dictionary already leaves most of them weak and the rest sit on
content words where they belong, so there is no density problem to fix. What is left is worth doing
on a different argument: the nine are misplaced rather than surplus, and a wrongly stressed
preposition is heard as a mistake where a correctly stressed noun is heard as nothing at all.

The exclusions are the judgement — no negation, not `one`, no demonstratives — because each would be
wrong more often than right, which is the whole test for being on the list. **Expect a small
difference.** The rest of the gap to a hosted voice is 82 million parameters and no text to read,
and neither is closeable from here.

### Also

- **A question about any ship's jump range is answered with the Commander's location**, filed as
  [#161](https://github.com/dseelinger/d47/issues/161) rather than fixed here. `jump range` is a
  keyword, keywords name a capability rather than a tool, and the router takes the capability's
  first tool with no required parameters — which is `get_location`. It is the 2026-08-21 defect
  still live on the keyword path, and the decision it needs governs every capability rather than
  this one sentence.

---

## 0.84.4 — 2026-08-28 — Four field reports, and the one that was not a bug in the code

### Take us out, walked the way the panel actually works

*Take us out* has never once worked, and the reason it never worked is the reason nobody could see
what else was wrong with it. [#106](https://github.com/dseelinger/d47/issues/106).

**It waited for the wrong panel.** The macro presses the **left** panel key and then waited for
`GuiFocus` to reach `InternalPanel` — and Frontier number the panels by *subject*, not by side. The
**internal** panel is the ship's own systems and sits on the **right**; the **external** panel is
navigation and contacts, which are outside the ship, and sits on the **left**. Measured against a
running game: left panel gives 2, right panel gives 1. So the gate was waiting on a value that
pressing that key cannot produce. It refused every single time, deterministically, and said *the
panel did not open* to a Commander who was looking at the panel.

**Which meant the walk underneath it had never run, and it was wrong too.** Four presses of left and
four of up — the phase's own stated guess about a menu, and the one part of the macro that was
guessed rather than verified. It is now the Commander's own sequence: **back**, **down**, **select**,
with `ui_back` and `ui_down` added to the all-or-none pre-flight check, because a macro that opens
the panel and then finds it has no *select* leaves a panel open over the cockpit.

**And the walk waits, because the left panel is not where the launch button is.** *Auto Launch* — or
*Launch*, without an advanced docking computer — is on the station menu in the **centre**. Opening
the left panel and pressing *back* is how a Commander arrives there: back dismisses the panel and
leaves them on that menu. So the panel closing is the **point** of the first key rather than a
failure of it, and the walk is not one burst but two halves with the game consulted in between —
back, then wait for the panel to actually go, then down and select. Flown before that wait went in,
back closed the panel and the other two did nothing at all: sent during the transition, with no menu
yet to receive them. Waiting on the game rather than on a guessed delay is the answer the galaxy map
macro already reached.

That wait is also the guard. Down and select with no station menu in front of them are flight
controls typed into a docked ship, which is the hazard the phase built the gate for — so a *back*
that leaves the panel open now stops the run and says so, rather than pressing two direction keys
into a cockpit.

**The panel's identity now lives in `Launch.Panel`**, in Core, next to the walk it belongs to. The
App used to hold that opinion by itself, and the name of the thing it was reading — `InternalPanel`,
reached through a property called `AwaitInternalPanel` — made the wrong constant read as obviously
correct for as long as it shipped. Both are named for the panel now rather than for the flag.

**The stale-read theory was measured and is not the cause.** Both faults were suspected to sit
behind [#148](https://github.com/dseelinger/d47/issues/148): `GameStatusReader` re-reads Status.json
only when the file's last-write time moves, and Windows is not obliged to advance that while Elite
holds the handle open. Sampled at the tick loop's own 10 Hz — 320 synthetic samples against a
held-open writer, then three minutes against the running game — the stamp moved with the contents
**35 times out of 36**, and the one exception lasted a single poll and is indistinguishable from the
probe's own read ordering. A tenth of a second of blindness cannot produce a three-second failure.
`GameStatusReader` is therefore unchanged: a fix with no defect behind it is a change that has to be
maintained for nothing.

**And a build from a working tree now says which it is.** Reported the same day against a
hand-installed build: the title bar read **pre-release 0.84.3**. Not merely unhelpful — a claim to
be a signed, published build that it was not, in the one piece of chrome that is never off screen.
The channel is asked of GitHub at run time on purpose, because promoting a pre-release changes the
answer without changing the binary; but a local build's version compares *equal* to the release it
was cut from — `0.84.3-local` is `0.84.3` for comparison, and has to be, or the updater would offer
to replace it with itself. So GitHub answered truthfully about a different binary. A version
carrying any label is now answered from the binary and GitHub is not asked at all: the title bar
reads `0.84.3 (local build)` and the panel badge matches. The rule is not about one tool's wording —
the release workflow builds from the tag's bare version, so a published `d47.exe` never carries a
label.

### The switch that was bound twice

The gear switch inverted: flip it to up and the gear went down, flip it back and the gear went down
again, and only pressing **L** by hand ever fixed it.
[#147](https://github.com/dseelinger/d47/issues/147).

**Directive 47 was right every single time.** The same physical button was bound twice — once in
Elite, once as a d47 maintained switch — so every flip acted twice. Where d47 correctly pressed
nothing, Elite's own binding toggled and the gear moved the wrong way; where d47 correctly pressed,
the two toggles cancelled and nothing happened at all. One setup fault, wearing two disguises, both
of which look exactly like the reconciler misbehaving.

It was settled by measuring rather than by reasoning: the gear's true state sampled at 10 Hz from
Status.json and laid against d47's own log, seven flips, every one accounted for and **d47's belief
about the gear correct on all of them**. The same measurement killed the theory this had been
filed under — [#148](https://github.com/dseelinger/d47/issues/148), a stale status read — which is
now closed as not reproduced, with `GameStatusReader` unchanged.

**So d47 now notices.** It already parsed the binds file at startup and already knew the device and
button of every switch position; nobody had asked the one question that joins them. A switch sitting
on a button Elite binds says so, on the panel, in the switch window and when asked out loud, naming
the Elite action it collides with. It is narrowed to the device as well as the button — this
Commander's binds file carries four `DeviceIndex` values under one VID and PID, and matching the
button alone reported four collisions where one was real. **Binds stay read-only**: the one thing
d47 can do about a collision is be the component in the room that can see it.

**And a press that never takes is said out loud.** The watch has always reported a state arriving
and then going back — something fighting d47 — and never reported it not arriving at all. That is
precisely the shape two cancelling toggles make: the log said *Sent* twice while the gear did not
move, and nothing said so. One sentence would have diagnosed in a moment what took an afternoon.
Only where the state is readable, because an action d47 presses blind cannot tell *did not arrive*
from *cannot say*.

The off-by-one is pinned by a test, since it is the kind of fact that rots silently: Elite counts
joystick buttons from one, so its `Joy_9` is d47's button 8.

### The log page keeps up, and says which service spoke

**The log page was a snapshot of the moment it was opened.** The read ran on navigation and nothing
ever read again — so a Commander who opened it to watch a failure saw nothing arrive, and the only
way to see the next line was to leave the page and come back. It now re-reads once a second while
it is the page showing, and stops the moment it is not.

The original reasoning is kept rather than overturned, because it was right: *a log nobody is
looking at is not worth a file read per tick.* What changed is that a page somebody is looking at
now counts as somebody looking. The refresh is silent — no busy glyph for something nobody asked
for — and does not redraw at all when the file has not moved, because a redraw rebuilds every run
and would fight a reader's selection once a second for no new text. Scrolling still follows only if
the reader was already following.

**And a voice id now says whose it is.** The line read `Spoken by Boe Dock in pFQStpMdprGFILRDrWR2`
— an opaque id, with no way to tell which voice that was or which service said it. Since Phase 57
three providers can be speaking at once, so the provider is a fact about the line rather than
something a reader can infer: it is now `Spoken by Boe Dock in pFQStpMdprGFILRDrWR2 through
ElevenLabs`. Asked of the client that actually spoke rather than of settings, which can have moved
on since. Kokoro's ids happen to carry a name and ElevenLabs' do not, which had made the gap look
like a formatting quirk of one provider rather than a missing fact about all of them.

---

## 0.84.3 — 2026-08-28 — Dialogs that fit the window they are drawn in

Reported from a running build at 150% zoom: the **Voice** picker was about three times as wide as
it declares itself, its help paragraph ran off the right edge on one unwrapped line, its rows did
not trim, and *Cancel* and *Use this* were off the side of the screen. Measured with the real help
text and five voices: **2307 pixels of content inside a 780-pixel window**.
[#145](https://github.com/dseelinger/d47/issues/145).

**A ScrollViewer that may scroll sideways measures its child with infinite available width.** That
is what *may scroll sideways* means to a measure pass, so `TextWrapping` beneath one has nothing to
wrap against and never wraps, and `TextTrimming` has nothing to trim against and never trims. This
is the same trap the **main window** was fixed for in 0.57.0 — *"at 100% zoom the main window
doesn't fit in HD"* — and the correction that fixed it, `FitToViewport`, carries a paragraph
explaining exactly this. The dialog path builds the identical scrolling host, to draw a dialog at
the zoom of the panel that opened it, and undid none of it. **The mechanism was written down twice
in one file and defended once.** The rule now lives in one place both paths call.

**It was invisible to the whole suite, and that part is worth recording.** The dialog path returns
immediately at 100% zoom, and 100% is what a headless test gets unless it says so. Every picker
test drew a 520-wide window and passed, for as long as the fault has existed. The new tests set the
zoom first.

Every dialog took this path, not only the picker — confirm, changelog, macro, lore, memory,
persona, spend, switch and coverage all open through it.

**And the local voice's rows said everything twice.** *Jessica — female, American — Female, en-US*:
the id already carries the name, the gender and the accent, and the label was composed from all
three and then handed to the thing whose job is to add the gender and the locale. Now *Jessica —
Female, en-US*, which is the shape every other provider's rows have always had.

---

## 0.84.2 — 2026-08-28 — The local voice, heard and understood

Three reports from the first flight of the local voice, one for each of the things a Commander does
with it: fetch it, choose it, listen to it.

**The download said nothing while it worked.** 350 MB arrived correctly and the surface reported
none of it — no bar, a button that stayed pressable throughout, and a row still reading *not
downloaded* until something else happened to redraw the page. The only evidence it had worked at
all was a line in a log. A press that takes minutes now reports a fraction while it runs, shuts its
own button so a second press cannot start a second download, and refreshes the row when it lands.
The mechanism is a property of the row rather than of this one button, so the next long press gets
it for free.

**And then it speaks.** A finished download is a claim; a voice coming out of the speakers is the
proof of it, and it is the only part of this a Commander can check without reading a log. It plays
through the one arbiter like every other sound, and through a client of its own where the local
voice is not the selected provider — proving the download worked must not require choosing it
first.

**There was no voice to choose.** The picker had asked the provider for its list at startup, been
told *not installed*, and nothing ever revisited that answer. So the model was on disk, the row said
so, and the voice row was empty. The list is asked for again the moment the download lands.

**`Ship's` was spelled out, one letter at a time.** *"Systems responding, Commander. Ship's docked in
Buzhang Ku"* came out **ess aitch eye pee ess**. Not a fault in the spelling rung — that rung was
doing exactly what it exists for — but a missing rung above it: an apostrophe makes a word fail the
*is this all letters* test, so both rungs that can pronounce anything skipped it, and the last rung,
which never refuses, spelled it.

The dictionary cannot fix this and never could: **0 of its 274,927 entries contain an apostrophe**.
So the ending is derived from the word underneath it — *ship* plus a possessive lands on exactly the
transcription the dictionary holds for *ships* — which is also why it works for *Buzhang's*, a name
no list was ever going to hold. Stripping the apostrophe and looking that up was the obvious cheap
fix and is wrong where it counts: the dictionary reads `ill` as *ill*, `id` as *I.D.* and `wont` as
*wont*, so *I'll*, *I'd* and *won't* would each have come out as a real word that is not the one
written. The dozen that no rule reaches are written down individually, and **can't** takes the vowel
of the voice saying it, for the same reason a British voice says *zed*.

---

## 0.84.1 — 2026-08-28 — The local voice had no download button

Reported within minutes of 0.84.0: the **Local voice** row offered no way to fetch the model.

The row asks the app two things — whether the model is here, and what to run to fetch it — and it
asked the second one **while the settings rows were being built**. At that moment the app has not
finished constructing itself, so the answer was "nothing", the button was left off, and it was left
off for good: the rows are built once.

That indirection exists precisely so the question can be asked *later*, which every other
long-running button here does correctly. Asking it immediately is the one thing that defeats it.

**Every test passed, and that is the part worth recording.** The row-level tests handed over a
working app straight away, which no real app does. The page-level tests supplied neither of the
row's two app connections, so the row they drew was not the row that ships — the same hole that let
a crash-on-launch through in 0.76.0. Both are closed: the test surface now supplies them, and there
is a test that opens the real settings page and looks for the button a Commander would press.

---

---
## 0.84.0 — 2026-08-28 — Phase 59: a voice that never leaves the machine

**Every provider Directive 47 has ever had is a service.** The words it speaks are sent
somewhere to be turned into audio — and that includes re-voiced in-game messages, which are
written by other players. Edge is free, and free is not the same as private.

**Kokoro runs the voice on your own computer.** Choose it and nothing D47 says leaves the
machine. The model is downloaded once — about 350 MB, from `huggingface.co`, with a **Local
voice** row in Settings that says whether it is here and fetches it if not — and after that this
provider needs no network at all.

**This closes the oldest promise still standing.** Phase 57 shipped in v0.72.0 with one item
unticked: *no other player's text has to leave the machine*. Half of it was settled then, because
every slot carrying another player's words defaults to Edge and Edge costs nothing. The other
half could not be, because Edge is free and **not local**, so those words still went to Microsoft.
Now they need not go anywhere.

### It had to learn to read first, and that is most of the work

Kokoro is given **sounds, not letters**. It has no text input at all — every other provider does
that step inside its own service, invisibly — so Directive 47 had to learn to turn writing into
speech sounds itself.

The obvious answer was measured and thrown away: the usual neural model for this scored **0.0%**
on words from its own training data, and Elite has 400 billion system names, so no dictionary can
be extended to cover them.

**So it works them out by rule.** Known words come from a pronunciation dictionary. Anything else
is broken on spaces and dashes and taken a piece at a time: a piece that reads as English is
pronounced, digits are read as a number, and a run of letters nobody could say — or letters
mixed with digits — is spelled out.

```text
COL 385 SECTOR B0-GQPI
  → call three eighty-five sector bee zero dash gee queue pee eye
```

*Shinrarta Dezhra* is **pronounced** rather than spelled, because sounds an English speaker can
make are allowed even where English does not spell them that way. And a voice with a British
accent says **zed**.

The dash is only spoken where it is holding a designation together. *Well-known* is two words,
not three.

### What to know before choosing it

**It speaks English and only English.** Every other provider is told which language a line is in;
Kokoro is not told, because it has only one. A message in French is read out by an English
speaker rather than in French. For the slots carrying other people's words that is the trade, and
it is the point: the alternative is that those words leave your computer.

**It has been heard, but not yet judged.** The voice was downloaded, loaded and spoken end to end
before this shipped — which is more than the last new provider got — but whether it sounds
*good* is a thing a person decides, and nobody has decided it yet.

---

Fixes [#101](https://github.com/dseelinger/d47/issues/101).

---
## 0.83.0 — 2026-08-28 — Habits is gone

### The feature, and the data it kept about you

> Remove "Habits" from D47, it was only ever half baked. Remove any data associated with it as
> well from existing data files as part of the update.

Habits was d47 reading your journals and noticing what you keep doing — overshooting a drop,
submitting to interdictions, landing hard on high-gravity worlds — and remarking on it when the
situation came round again. It shipped as Phase 32, it was the only callout that was off until
you switched it on, and it is now withdrawn.

**Everything it stored goes with it.** `data/habits.json` is deleted on the first launch after
this update, and the line in the log says so when it actually removes something — deleting your
own flying silently is the wrong way round even when you asked for it.

**Your settings file is not disturbed.** The `callouts.habits` key stays in the record, read by
nothing: settings files reject keys they do not recognise, so *removing* the property would have
refused every file written before today rather than ignoring a value nobody reads. The name is
retired and will never be reused for anything else — a later feature answering to it would
silently inherit a decision you made about a different one.

**What it gave back:** three tools and 505 bytes of the surface every conversation pays for,
measured before and after rather than estimated. That is half a percent, which is worth saying
plainly: this was removed because it was half baked, not to make room.

Phase 32 is withdrawn in place and its number is retired forever, the same treatment Phase 22
got and for the same reason: a later phase renumbered into it would silently repoint every
reference that ever said "Phase 32" at a subject it was never about.

### The download progress bar stops trailing the download

Reported while flying 0.82.0: a model download far slower than a gigabit line should allow.

**It was not slow.** Hugging Face serves that file at 54 MB/s, and d47's own copy loop was
measured at 60 to 77 MB/s. What was wrong is that a 75 MB model fired **4,700** progress
updates at the window, and that queue drains long after the bytes have landed — so the bar
trailed reality, and a download that had already finished still looked like one that was
crawling. It now sends 101, which is every update a progress bar can express.

---

Fixes [#84](https://github.com/dseelinger/d47/issues/84).

---
## 0.82.0 — 2026-08-28 — Eight things that were quietly wrong

A release of fixes rather than a new capability, and three of them were silent in the way that
matters most: nothing looked broken.

### Tod 'The Blaster' McQuinn was invisible to every plan you made

Reported from Wolf 397: three Grade 5 Overcharged multi-cannon rolls on the flown ship, Tod at
Trophy Camp doing exactly that work, and the checklist offering nothing.

The two shipped tables spell him differently — the engineer list says
*Tod 'The Blaster' McQuinn* and the recipe list says *Tod McQuinn* — and three places compared those strings
directly. **So no recipe ever matched him, for any blueprint, in any system.** His *"what can
be done here"* filter was never offered, and the Engineers directory counted him as wanted by
nobody while three of his rolls sat on the list.

He is the only one of the thirty-eight it happened to, which is what made it look safe. There
is now a check that every engineer named in the recipe table is one d47 can find, so a fourth
spelling cannot arrive unnoticed.

### Your carrier is called by its name again

> Docking granted, Commander. Welcome home to BNH-T2F.

Elite only writes down what your carrier is called when you open its management panel — so in
**21 of 34 sessions** d47 docked at your own carrier without ever being told the name, and fell
back to the callsign. Every surface that names it already preferred the name; there was no name
to prefer.

It now learns *Sacred Fire* from the journal itself, where it appears attached to the callsign,
and it does that carefully: by the carrier's id where the event carries one, and otherwise only
from a line ending in exactly the callsign it has already confirmed. **A carrier that is not
yours never contributes a name**, and nothing is guessed before your own is identified. When
Elite does say the name, Elite wins.

Your carrier's name also joins the words the transcriber is told to expect, so you can say it
and be understood.

### No speech model download was ever checked

**Found while doing something smaller.** d47 asked Hugging Face what each model file should be,
and asked in a way that returns only the file's name — no size and no hash. The verification is
skipped when there is nothing to verify against, so **every model ever downloaded was accepted
unchecked**, and every model was offered to you as "0 MB".

Both halves are fixed, and the expected hashes are now **written into d47 itself** rather than
asked for at download time. Worth being precise about what that buys: they were read from the
same place, once, on a stated date. It does not make that first read trustworthy — it means the
file *changing* afterwards becomes visible, where before the expected hash and the bytes came
from the same server. The model is loaded and run on your machine, so it is worth checking.

### Analyst Prime talks about Cora far less

> Every other message had something about Cora not approving. He's meant to be more
> condescending than anything else.

His own rules already said *one leak per exchange, maximum* — and thirty words earlier told him
to praise whatever Cora would have criticised, *"Consistently."* Given a concrete instruction
and an abstract budget, a model follows the instruction.

That word is gone, the limit is now its own rule and reads *rarely*, and the character leads
with the condescension that was always supposed to be the point. **She is still there** — the
rarest and best beat, where he cannot tell a memory of her from a reconstruction, is untouched.
You get this automatically on update; nothing is stored on your machine to migrate.

### A push-to-talk button warns you about Elite, like a key always has

Bind push-to-talk to a keyboard key and d47 tells you if Elite is already using it. Bind it to a
stick button and you got nothing at all — no warning and no all-clear. The check existed and was
writing to a log file nobody reads.

It is hedged on purpose. Elite records a joystick binding against its own name for the device,
so d47 cannot tell whether that *Joy_7* is the stick on your desk or another one, and it says
so rather than pretending. Finding nothing is the stronger answer and is said plainly.

### The detail pane can be copied

> I want to be able to copy text from the Engineer Details pane.

A system name, a station, *"Provide 50 units of Lavian Brandy"* — all of it is selectable now,
and not only on Engineers: every drilled detail pane in the panel was built from the same
helpers and all of them gain it. The rows you press are deliberately left alone, so a drag that
was meant to open something still opens it.

### The engineer you are reading is outlined in the list

The right pane showed one engineer and the left list drew all thirty-eight identically. The one
you are looking at is outlined now, and it follows the pane however you got there — pressed,
asked for by voice, or arrived at by going back.

### The "?" links on settings rows go somewhere

**Forty-five settings rows had a help link that arrived nowhere.** A comment in the code had
claimed since the feature was built that a test guarded this. There was no such test; there is
now, and it found nearly four times what a careful reading by hand had.

---

Fixes [#133](https://github.com/dseelinger/d47/issues/133),
[#130](https://github.com/dseelinger/d47/issues/130),
[#124](https://github.com/dseelinger/d47/issues/124),
[#123](https://github.com/dseelinger/d47/issues/123),
[#122](https://github.com/dseelinger/d47/issues/122),
[#110](https://github.com/dseelinger/d47/issues/110),
[#81](https://github.com/dseelinger/d47/issues/81) and
[#71](https://github.com/dseelinger/d47/issues/71).

---
## 0.81.1 — 2026-08-27 — Raw Journal is actually raw

### The two journal readings looked the same, because they were

Reported straight after 0.81.0: **Journal and Raw Journal were the same page.** Both showed the list
of sentences, both showed the same indented fields beside it, and the only thing that changed
between them was how wide the two columns were.

So the raw reading was not raw. If you went to it to copy the JSON for a bug report, you got a
prettified version of one event — the same thing the other page was already showing you.

**Raw Journal is now the file**: one event per line, as Elite wrote it, in one scrolling column —
the same shape as the D47 Log beside it. Journal keeps the list and the fields.

**And it is never treated as formatted text.** A journal carries other players' messages exactly as
they typed them, and JSON is full of asterisks and underscores. Passing that through the panel's
own formatting would have reformatted a Commander's message — and would have let one dress a
message up to look like a line d47 had written itself.

---

Fixes the reading shipped in [#51](https://github.com/dseelinger/d47/issues/51).

---

## 0.81.0 — 2026-08-27 — Read the journal

### Elite's journal, in the Transcript, in English

The Transcript has two new readings beside Thread, Details and D47 Log.

**Journal** is what Elite has been writing, one line per event, in sentences rather than JSON:

```
12:03:15  Course set for Kusauts
12:03:27  Undocked from BNH-T2F
12:05:16  Jumping to Kusauts
```

Click a line and **the fields behind it appear beside the list** — the event exactly as Elite wrote
it, indented and selectable. That pairing is the point: the sentence is easy to read and could be
wrong; the fields cannot be. An event nobody has written a sentence for still lists, and its fields
are just as complete as any other's.

**The detail pane folds away** if you would rather have one column of sentences, and the rule between
the two panes drags, like the ones on the other tabs.

**Raw Journal** is the same events shown as the JSON, wide, for when you are copying something into
a bug report.

### The noise is hidden until you ask for it

Nearly half of what Elite writes, by volume, is inventory and signal chatter — `ShipLocker`,
`FSSSignalDiscovered` and their kind. A page that opened on those would have to be scrolled past
before it could be used, so they are hidden, and one toggle brings them back.

They are still **kept**, not discarded. d47 never re-opens the journal to find them again — Elite
holds that file open while you play — so hiding them on the way in would have made the toggle
one-way.

### Ships are named, not spelled

A ship stored at another station could come out as **`Kofu (corsair)`** — the journal's own symbol
rather than the hull's name — while the same fleet listed *Tulimiekka (Kestrel Mk II)* correctly
beside it. Which spelling you got depended on which event the row came from, which is not something
anybody should have to know.

Every stored ship now names its hull properly, including the three newest ones — the Kestrel Mk II,
the Caspian Explorer and the Corsair — which have no specification table entry and are named from
their armour instead. That reached further than the fleet list: the ship chooser, the report d47
reads to itself, and the facts an adventure is written against were all showing the symbol.

And where d47 **describes** a ship rather than naming one, it says who built it — *"Anaconda, by
Faulcon DeLacy"*. Those same three newest hulls have no builder recorded anywhere, so they say
nothing rather than guessing.

---

**Under the hood.** The journal page reads events the tick loop has already polled rather than
opening the file a second time — Elite holds the current journal open, so a second reader is a
second place to get the sharing flags right. Raw Journal is furnished by the desktop window alone
and never registered for the headset: a wall of JSON exists to be selected and pasted, which is an
act with no meaning in mid-air. The sentences go to both.

Fixes [#51](https://github.com/dseelinger/d47/issues/51),
[#105](https://github.com/dseelinger/d47/issues/105) and
[#108](https://github.com/dseelinger/d47/issues/108).

---

## 0.80.0 — 2026-08-27 — Help that takes you there

### Settings help now links to the settings it mentions

Four rows told you to "see Privacy" and then left you to go and find it. **Privacy is now a link**
— click it and the page selects that section, opens the card and scrolls to it, exactly as if you
had used the sidebar.

It works while you are searching, too. A query that matches part of a link still highlights that
part, and the link still works.

### Reset is a picture, not a word

The **Reset** at the top of a settings card was the word; the one on each row was a small arrow.
They are the same promise at two scales, so they are now the same mark — a circular arrow drawn
anticlockwise, which is the near-universal sign for *undo*.

**The old row arrow was a font character**, which meant it was whatever your installed font happened
to have: a slightly different weight from the other marks around it, sitting slightly off, and on a
machine without that character, an empty rectangle. The new one is drawn, so it looks the same
everywhere.

Both now announce themselves properly to a screen reader, which the drawn version would otherwise
have lost.

### The ELI5 pictures say what they mean

One read-through of the illustrated summaries turned up nine places where a sentence had been
squeezed until the point fell out. Headings that were riddles are now statements — *"Five things can
stop it hearing you"* instead of *"'It cannot hear me' has five causes, and the answer names which"*.
A figure whose explanation had been cut is gone from the picture and kept in the writing below it,
where it makes sense.

**Two bigger changes.** The Settings page opened with four rows listed in warning-red under *"Four
rows the model cannot touch"*. Red is the colour of something being wrong, and nothing there is
wrong — it now simply says **some settings have a safety catch**, that D47's AI cannot change them,
and — the part that was missing — that **you still can**, any time, on the page or with a hotkey or
by saying so. Reading only "the AI cannot change this" beside fifty protected rows, you could
reasonably have concluded they were locked to you as well.

And a picture on the Conversation page has gone entirely. It illustrated something you cannot see by
design — its own caption said *"you see an answer, not an error"* — so it was a picture of nothing
happening. The explanation stays in the text.

### A ship has a core; the core does not fly the ship

*"The core that flies it"* had spread to six places, including a tool description and a settings
row. **You** fly the ship. The core is the AI aboard it. Corrected everywhere it was said.

---

**Under the hood.** Cross-references are written as links in the help text itself with a section id
as the target, rather than being detected by matching words in prose — *privacy* is an ordinary
English word and a matcher would light up the wrong ones while going silent the day a section was
renamed. Because the target is declared, a test asserts every link points at a section that exists,
so a broken cross-reference fails a build rather than your click.

Fixes [#65](https://github.com/dseelinger/d47/issues/65),
[#69](https://github.com/dseelinger/d47/issues/69) and
[#80](https://github.com/dseelinger/d47/issues/80).

---

## 0.79.0 — 2026-08-27 — Panes you can set where you want them

### Drag the line between two panes

The panel shows the level you are on beside the one above it, and a third beside those when there
is room. Until now those panes were always equal thirds, or equal halves, and there was no way to
say *"give the ship list less and the slot detail more."*

**Now the line between them is a handle.** Point at it, drag it, and the two panes either side move.
It works on every tab that has panes — Loadout, Engineers, the Checklist, Routing and Adventures —
because they are all drawn by one thing, so this arrived on all of them at once.

**The page at rest looks exactly as it did.** No bar, no grip dots, no line that thickens when you
approach it. It is the same hairline it always was until the pointer is actually on it, and then
the cursor changes.

### It stays where you put it

The panel redraws itself constantly — every time you open a ship, drill into a slot, or come back
up. **A width that survived only until the next click would not be a feature**, so what you dragged
is remembered and re-applied every time.

It is remembered as a **proportion rather than a number of pixels**, because the window is
resizable and 640 pixels means something different on a 1024-wide window and a 2048-wide one. And
it is remembered **separately for two panes and for three**, because those are different
arrangements you will want set differently — and the panel already moves between them on its own as
you resize the window. Widening the window to open a third pane no longer restates a two-pane
choice you made as a three-pane one.

### It cannot be dragged into a mess

A pane has a minimum width, and that was already the number deciding how many panes fit. **It is
now the same number that stops a drag**, so you cannot drag a pane down to a sliver that the layout
still believes is full width. The handle stops at the limit rather than refusing to move or
springing back — a handle that stops says where the edge is, and one that snaps back says nothing.

Dragging changes the proportions of the panes you have. It never changes how many there are; that
is still decided by how wide the window is.

### The headset is untouched, on purpose

This is the desktop window only, and that is a safety property rather than an oversight. The same
panel is drawn in the headset, where it is driven by pointing a controller at it — so a handle that
existed there would be draggable by the ray, whenever the ray crossed it. The ask was for the mouse
and only the mouse, so the headset and the flat mini panel never get one, and there is a test that
fails if they ever do.

---

**Under the hood.** `PaneWidthMemory` keeps the splits in `ViewState` beside the window's own
rectangle, out of the append-only settings file — a proportion is not something a Commander types.
It re-reads before it writes, because that record also carries the collapse states and the
checklist filter, and saving a cached copy would silently undo whichever moved most recently. A
hand-edited split file gets equal panes rather than a broken layout: the count has to match and
every share has to be a positive real.

Phase 55 in [list.md](list.md), which took its number on the commit that shipped it — the first
phase to be planned as an issue rather than as a file.

---

## 0.78.2 — 2026-08-27 — Room to think before answering

### A model that reasons could not get a word in

The unprompted lines — the ambient remarks, the opening brief, what Directive 47 says after a long
gap, a lore lookup — are given a budget of 400 tokens each. **On a model that reasons before it
answers, the reasoning came out of the same 400.** It would think, run out of budget mid-thought,
and never write anything. Directive 47 read that as "nothing to say" and quietly used the written
line instead.

**Nothing looked wrong.** No error, no warning, nothing in the log. The generated lines just stopped
appearing, which is indistinguishable from a model that is dull — so it is not the sort of thing
anyone would think to report.

That budget now covers the thinking as well as the answer, and the answer keeps the same room it
always had. This costs nothing on a model that does not reason: it is a ceiling rather than a
purchase, and a model that answers in forty tokens is charged for forty either way.

**If it does run out, that is now written to the log** rather than passing in silence — naming the
model and how far it got. A model that ran out of room and a model that declined to speak used to
be the same nothing.

This was found while testing a local model and has nothing to do with local models: it reaches any
reasoning model, including ones reached through OpenRouter or a gateway, and the **Model for the
quiet calls** setting makes it easier to meet rather than harder.

---

## 0.78.1 — 2026-08-27 — A build that says which one it is

### About told you the same thing twice

**Version** and **Build** both read `0.78.0+4b18aaecbe2510b0aeae95d3f19583edd18ea205`. The row whose
help says *"Which release this is"* answered with forty characters of commit hash, and the row
beside it — whose help says a version alone *"cannot tell two builds of the same release apart"* —
proved its own point by being identical to it.

**Version** now reads `0.78.1`. **Build** still carries the commit, which is the thing worth quoting
in a bug report. Every build since About became a settings page had this, so if you have ever
copied that row into a report, it told whoever read it less than it looked like it did.

The same change reaches the status Directive 47 reports when you ask it what it is running: it says
`0.78.1` rather than reading a commit hash aloud.

### A pre-release says so

Directive 47 now knows whether the build you are running is a pre-release, and says so in three
places: the title bar, About's **Version** row, and a small mark beside the **?** in the top right.

**It asks GitHub rather than being told at build time**, and that is the whole design. A
pre-release becomes a normal release when it is promoted — same download, same file, same version
number — so a build that had been *stamped* as a pre-release would go on saying so for ever
afterwards, on every machine that had installed it. Asking means the mark disappears by itself once
the release is promoted, with nothing to reinstall.

**If it cannot ask — no internet, or GitHub is unreachable — it says nothing at all** rather than
claiming the build is final. A marker you cannot trust is worse than no marker.

**The mark does not appear in the headset.** You are flying; which build you are on is not a
question you are asking mid-flight, and there is no way to dismiss something in an overlay.

### Underneath

The left navigation's sections no longer share positions with each other. Nothing moved — the order
is exactly what it was — but each section now states where it belongs rather than landing there by
the order the code happens to register things in.

---

## 0.78.0 — 2026-08-27 — 924 voices to cast from

### A fourth voice provider, and it is here for the size of its library

**Cartesia.** Paid, needs its own API key, and it offers **924 voices — 417 of them English** —
against several hundred at ElevenLabs, 322 on Edge Neural and thirteen at OpenAI. Every one is
tagged with a language, an accent, a country and a gender, which is what lets Directive 47 give a
Commander whose name reads as a woman's a woman's voice — something OpenAI publishes nothing at all
for.

That is the whole reason it was built. Eleven Guardian cores, a ship's AI, a carrier captain, a
tower and five classes of NPC were being cast from thirteen voices plus Edge.

**It may speak for any of the six slots, including the four that carry other players' words.** It
takes a language and holds it, so a message typed in French is still read in the voice and the
language you chose. That makes it the second provider after ElevenLabs that is offered for a
re-voiced slot at all — OpenAI is still refused there, and for the same unchanged reason.

### It has no speaking rate, so it is not given one

Cartesia has a speed control. It is documented, it is validated precisely — hand it a value outside
its range and it refuses the request naming the field and the bounds — and **it does not change the
audio**.

Measured three times per setting: the largest difference *between* settings came out smaller than
the largest spread *within* a single setting, and the "slowest" setting produced *shorter* audio
than "normal". A first pass that took one sample per setting showed a tidy 26% spread running
neatly in order, and would have shipped you a slider that controls nothing.

So the Speaking rate row simply is not there while a slot is on Cartesia — and a rate typed into
`settings.json` by hand is ignored rather than sent, because a rule that lives only in a dropdown is
one a text editor walks straight past.

### What it costs is not quoted, and that is deliberate

Their API will not say. The four endpoints that would carry a rate or a balance all answer 404, so
Directive 47 does not know their rate and does not even know which *unit* they bill in. Rather than
derive a figure that would be wrong, the price row reads **(not published — no price will be
quoted)** and the session line reports the characters it sent with no dollars beside them. Read
their price page, type the number in, and every figure follows it as it does for anyone else.

Concurrency is held at two requests at once, which is the floor their own documentation gives for
entry tiers — lower than the three Directive 47 allows ElevenLabs. That figure is read rather than
measured, and it is recorded as such.

### A voice you pick now shows on its row

Choosing a voice for your carrier's captain or its tower wrote the choice down and then went on
showing the row's default, so there was no way to tell it had worked short of listening.

**The store was never the problem.** Both voices were saved, both applies are in the log, and both
ids resolve to real names. The row had simply stopped re-reading — twice over. The settings page
subscribed to changes once when it was built and unsubscribed every time it left the screen, so
after the first time it did, it never heard about a change again. And applying a setting only
redrew the page when the change was *rejected*, on the reasoning that a change which worked is
visible in the control that made it — true of a switch, false of a button whose caption is a voice's
name looked up from your provider.

### About is the bottom of the page

It was second from the bottom, under an Audio mixer card that had never been given a position at
all and so took the one the code falls back to — which is past everything deliberate. About now
states its own place, and so does the mixer.


---

## 0.77.2 — 2026-08-26 — A readable cost window, and a carrier that answers

### The cost window fits again

**What this has cost** was showing every line with its left-hand half off the edge and a scrollbar
along the bottom. It is wider now, it never scrolls sideways, it is tall enough for a five-window
ledger without pushing the Close button off the screen, and you can resize it.

**Being straight about this one:** it does not reproduce in testing — the window lays out correctly
there, before and after. The change makes sideways scrolling impossible rather than merely unlikely,
which is right either way, but if you open it in this build and it is still wrong, please say so,
because that would mean the cause is somewhere nobody has looked yet. Four other windows built the
same way got the same guarantee.

### Your carrier captain will not refuse to talk to you

Handed *"Commander inbound."* to say in their own words, a Commander's carrier captain instead said:

> I appreciate the test, but I need to decline. Those rules I was just given aren't mine to explain
> or restate, even rephrased.

Out loud, over the air, in a voice that sounds like a person. Directive 47 already refuses to speak a
generated line that has stopped being about the thing it was given and started being about itself —
that guard has been there since a tower said *"I don't have that capability"* — but it knew the
shape where the model *describes* its instructions, not this one, where the model decides it is being
asked to hand them over and says no.

It knows both now, and the exact sentence that was heard is written into a test so it cannot come
back. When a generated line is refused, what you hear is the authored one, which is always good.

---

## 0.77.1 — 2026-08-26 — A slot engineered exactly as planned no longer keeps its dot

Reported flying a Kestrel: *"Didn't this get taken care of? All my HRPs are correctly engineered."*

They were. All four hull reinforcements rolled Heavy Duty G5, three of them with Deep Plating,
exactly as the plan asked — and three of them carried a mark that reads as outstanding work.

Your plan stores what you picked, **Deep Plating**. Elite writes **`special_hullreinforcement_chunky`**.
They are the same effect, and they were being compared as plain text, so they never matched. The
blueprint beside it learned that join a while ago; the experimental did not, so a roll finished
exactly as planned kept its dot forever.

**Nothing to repair.** The mark is worked out fresh each time the page is drawn, so it is right the
moment you run this build — no rescan, no repair step, nothing to tick off by hand. Your
checklist was never wrong either: it already knew those two names were the same thing, which is why
the plan lines for those slots have been sitting there ticked while the dots argued with them.

One of that Commander's four reinforcements really does have no experimental on it. **That dot
stays**, and it is right.

---

## 0.77.0 — 2026-08-26 — There is a Discord, and a build that cannot start can no longer ship

### Come and say hello

There is now a **Discord** for Directive 47, and **About** has a row that opens it. It is the
fastest way to get an answer, it needs no GitHub account, and it is where a bug report reaches
somebody who can fix it. Questions, screenshots, and what you want it to do next — all welcome.

The button opens the [community page](https://dseelinger.github.io/d47/community.html) rather than
the invite itself, which is deliberate: an invite compiled into a build is permanent, so revoking
one would leave a dead button in every copy already installed, fixable only by shipping a release.
The page is a file in the repository, so reissuing an invite is a commit and every build follows it.

### A build that cannot start can no longer reach you

0.76.0 and 0.76.1 both died before drawing a window, and **both passed every gate on the way out**.
That is the real defect, and this release fixes it rather than the symptom.

`--selftest` — the check the release pipeline runs against the actual published `d47.exe` — used
to return before the app was ever assembled. It proved the speech model loads, the echo canceller
loads and the controller projection activates, and then stopped short of the step where those two
releases died. It now **composes the whole application**: every capability registered, the settings
surface bound, the callouts and background work wired. If that fails, the release fails, and
because it runs in CI before the tag is signed, the version number is not spent on a build nobody
can run.

It runs the *real* composition rather than a copy of it, and that distinction is the entire lesson
of 0.76.0: the tests had a copy, the copy was missing the four rows that broke it, and five
thousand tests passed a build that could not start. There is now one canonical description of what
the app is made of, and a test that fails the moment a new piece is left out of it.

### Releases can be flown before they are offered

`tools/release.ps1` gains **`-PreRelease`**: it cuts the version and publishes it, but marks it so
that nobody is offered the update. The Commander installs it, flies an evening, and promotes it
when it has earned that. One that fails its soak is simply never promoted and the fix goes forward
to the next patch — so no public release carries the fault, and no tag ever moves.

### Also

The README said phases 1 to 15 were complete with phase 16 next, which was true in June. There is a
`CONTRIBUTING.md` for anyone who wants to change the code, and a round mark for the Discord drawn
from the icon that already ships, so the circular crop has nothing to slice.

---

## 0.76.2 — 2026-08-26 — 0.76.0 and 0.76.1 could not start

**If you are on 0.76.0 or 0.76.1, this is the release that launches.** Both die before drawing a
window, on every launch, with nothing on screen to say why. The log says it plainly:

```text
[FTL] d47 is going down on an unhandled exception
CapabilityRegistrationException: Settings row 'about.changelog' is a Info row with nothing bound
behind it.
```

The About area is new in 0.76.0, and four of its rows — **What changed**, **What changed since**,
**Set up keys**, **Add to Start Menu** — are rows whose whole content is a button. There is nothing
to *read* in them, and a guard that refuses a settings row with nothing behind it counted only
reading. It was right that a dead row should stop the app at startup rather than appear on screen
doing nothing; it was wrong about what makes a row alive. A row you can press is a row that does
something, which the same guard already assumed one line further down, where it insists a button
has words on it.

Fixed in both places it was wrong: the guard now counts a button, and the settings surface draws a
button-only row as its button instead of reaching for a read-out that was never there.

**Why no test caught a crash on every launch.** The two test surfaces built the capability registry
without the About delegates the app supplies, and a missing delegate makes its row absent by
design — so five thousand tests bound a settings surface that did not contain the four rows that
break it. They now build the registry the app actually ships. Put the fault back and 413 of them
fail.

Fixes [#78](https://github.com/dseelinger/d47/issues/78).

---

## 0.76.1 — 2026-08-26 — An adventure no longer strands you on a body you have been to

Reported from a flight: *the Adventure will get stuck if the destination is a body that has already
been scanned, and it expects you to scan it.*

It would, and there was no way out of it but to abandon the story. Elite writes a `Scan` the first
time a body enters your discovered set and then, overwhelmingly, never again — across the three
Commanders in the journals here, only 239 of 7,091, 155 of 2,722 and 289 of 11,727 scanned bodies
were ever seen a second time, and most of those are a nav beacon re-reading a whole system at once.
So a beat that waited for a scan of somewhere you had already been was waiting for an event the
game had already spent. It could never fire, and because a story only ever waits on its current
beat, everything after it waited too.

**Going to the body now counts.** The approach and the drop out of supercruise both say you went,
both carry the ids the beat matches on, and both happen whatever you discovered and whenever. The
hand-off says so too, so the story tells you rather than leaving you to work it out while stuck:

```text
Next: scan Veyl 3 c in Cairn of Veyl — the ship's own scanner from supercruise does it, or a
close pass; no surface scanner is needed, and simply going there counts if you have scanned it
before.
```

This can move a beat a few seconds earlier on a body you had *not* scanned — the corpus's own
example fires at 21:29:26, when the Commander dropped out of supercruise at the body, rather than
21:29:39 when they read the nav beacon. Same body, same visit, thirteen seconds. That is the price,
and a story that can never continue was the alternative.

Fixes [#77](https://github.com/dseelinger/d47/issues/77).

### Two ELI5 bands in plainer words

The settings and speech help pages lost a flourish apiece and gained a closer that finishes its
sentence. No behaviour, only the reading.

---

## 0.76.0 — 2026-08-26 — Asking the right tool, and saying which ship

Five wanted changes, and the start of a sixth.

### A tool handed the wrong thing answers anyway

Ask *"where are the closest core dynamics composites?"* and the market search would tell you no
station trades them — true of every engineering material that has ever existed, and useless. 0.73.3
fixed that one direction. Now **all six directions work**.

Three tools read three different tables — commodities, ship materials, Odyssey goods — and you do
not speak in ledgers. Whichever one your question lands on, Directive 47 now says what the thing
actually is, where the table knows it comes from, and which tool will tell you the rest:

```text
Core Dynamics Composites is not a commodity — no station trades it. It is a manufactured
engineering material, grade 3. Found at: … Ask find_material for where to get it.
```

**This is the strongest defence against a wrong tool choice, because it does not depend on the
model choosing correctly at all.** A wrong choice costs a sentence instead of three turns of you
steering.

### And a way to find out when it does choose wrongly

A **routing eval**: real questions, and the tool each one must call. It runs against your own
endpoint when you ask it to, never on a release, and it is what makes *adding* tools safe — a
description is a hope about how a model reads, and until now there was no way to check the hope
except hitting it while flying.

### A Guardian core is now performed, not just voiced

OpenAI's speech takes a **direction** — accent, tone, intonation, delivery — and no other provider
does. Directive 47 now sends it the description of whichever core is aboard, so the core on OpenAI
is *cast* rather than given a larynx. There is nothing to switch on.

One honest limit: Directive 47 keeps one connection per provider across all six voice slots, so if
you also put the carrier or the NPCs on OpenAI they are performed the same way. The default keeps
everything carrying another player's words on Edge.

### About is a place, and the changelog is in the app

**About moves out of the footer and into the settings nav**, where you look for it — version, the
exact build, the data folder, Frontier's attribution, and the buttons.

**What changed** now opens the changelog **from inside the build**, so it reads with no internet at
all. The old button opened a browser, on a comment saying a self-contained app had no renderer for
markdown worth carrying — which stopped being true when the help pages were embedded. The web link
survives beside it, because it is the only place a release *newer* than the one you are running
appears.

### The Transcript's readings are renamed

**Conversation → Thread**, **Technical → Details**, **Log file → D47 Log**. Each is a word you say
as well as press, and *D47 Log* says whose log it is — which matters now that a second one is coming
to sit beside it.

That second one is the journal, readable, with the raw file a sub-tab away. **The half that decides
whether Directive 47 is right about Elite is done**: every journal event now has a sentence, checked
against 931 real journals rather than against the schema. The pages themselves are next.

### And a Kestrel is called a Kestrel

Reading that formatter's own output against real journals turned up four faults it would have
shipped with — a doubled space in a blueprint name, a symbol tail on a crime, thousands unseparated,
and a ship reading as `smallcombat01_nx`.

That last one is worth naming, because Directive 47 already knew better: it works a new hull's name
out from its own armour data, which is how a ship Frontier ships before the community catalogue
catches up still gets called **Kestrel Mk II** and **Caspian Explorer**. The new code simply was not
asking. It is now, and both are pinned by tests.

---

## 0.75.2 — 2026-08-26 — Five pickers leave the calm page

The folded settings page was keeping the five **who speaks for this slot** provider rows — Carrier,
NPCs, People you know, Direct messages, Anyone in range — on the grounds that they carry a disclosure
about what leaves your machine.

They do, and it was the wrong reason. A slot provider picks *which* provider speaks a line that is
already going out; the row that decides whether it goes at all is **Voice provider**, one card up,
and that stays on the page. Five pickers is a lot of a page whose whole job is to be short.

The rows that genuinely decide what leaves your machine are untouched and always were: web search,
the galaxy search, notable places, the two privacy summaries and what Directive 47 remembers about
you. None of those carries a disclosure either — they stay because they are not the folding kind,
which the app now checks by name rather than inferring.

---

## 0.75.1 — 2026-08-26 — Show every setting moves to the top

**Show every setting** shipped four rows into the Interface card yesterday, which is exactly where
a Commander who cannot see the rest of the settings will not think to look for the reason.

It is now at the **top of the settings page**, above every card. It decides what the whole page
draws, so it belongs to the page rather than to one card on it — and it is the control you need
when the page is folded, which is the state it exists to get you out of.

Nothing else moves. The cards are in the same order, the setting keeps its key and its spoken
phrases, and a search that leaves nothing at the top no longer answers with a floating toggle.

---

## 0.75.0 — 2026-08-26 — Helpful, not anxiety-inducing

Seven wanted changes, and five of them are about the same thing: a settings page with seventy-five
knobs on it should not be the first thing a new Commander sees, and the figures beside them should
say what they actually are.

### The page shows what you need, and folds the rest

**Show every setting** — a new toggle under Interface, off by default.

Folded, Directive 47 shows the settings you need to get running: a provider, a model, a voice, a
microphone, a switch that stops it talking, and the handful that decide what leaves your machine.
Fifty-three rows go quiet.

**Nothing is switched off by being hidden.** Every folded setting keeps working, at its default or
at the last thing you set it to. This decides what is drawn and nothing else.

Four things stay on the page whatever the toggle says:

| | Why |
|---|---|
| **Anything you changed yourself** | The promise is *you are not missing anything*, and a row you set is something you did |
| **Your API keys** | A hidden key box is a Commander who cannot work out why nothing speaks |
| **Anything that decides what leaves this machine** | A page that went calm by no longer mentioning egress would be calm about the wrong thing |
| **The rows that get you running** | Otherwise it is not a settings page |

That last rule makes it adjust itself: a new Commander sees the calm version, and somebody who has
been tinkering sees their own work — and would have the toggle on anyway.

**A card with nothing left on it disappears** rather than sitting there empty, which does more for
the clutter than folding rows does. **Following a help link still works**: if a page says "change X
here" and X is folded, the jump unfolds the page for the session rather than turning the setting on
behind your back.

```text
show me every setting · show fewer settings · just the usual settings
```

### And a way back when a knob has gone the wrong way

**A row you have changed grows a small ↺ beside its label.** Press it and that row goes back to the
way it shipped. Rows you have not touched do not have one, so it doubles as a quiet marker of what
you have actually changed.

**A card you have changed grows a Reset** beside its heading — the useful gesture when something is
wrong and you do not know which of twenty-two rows did it.

Two things it never touches. **Your keys**: forgetting one means going and finding it again, so it
is a separate act and never swept up by a card reset. And **anyone else's settings**: on a row that
is yours rather than the installation's, reset means *stop having my own answer*, so the
installation's value shows through again — clearing the box by hand still means deliberately blank,
which stays a different thing.

**Directive 47 cannot reset anything by itself.** Reset writes safety-critical settings, and one
call that reached all of them at once is exactly what the rule about protected settings exists to
prevent. The panel can, your voice can, the model cannot.

### What this has cost, said straight

**Today** joins the running totals, at the top of the list because it is the one you read. There is
deliberately no "last 24 hours" beside it: that is the same number with a boundary you cannot point
at. A day the clocks moved in is 23 or 25 hours long, and *today* still means the midnight your
clock showed.

**The window now says once, at the top, that the figures are estimates.** Directive 47 knows each
provider's published rates, not what your account is billed — a subscription with bundled credits
can make the real cost anything from higher to nothing at all. One sentence, before the first
number, rather than an abbreviation stamped on twelve of them.

**OpenAI speech is no longer priced at "I don't know."** Directive 47 asks OpenAI for raw audio, so
it has known each clip's length to the sample all along and was throwing it away. It now prices
that speech per minute of audio, which is a measurement rather than a guess from the text — and the
text would have been hopeless, since the same thousand characters run to 951 characters a minute as
prose and 671 as a line of system names.

**The rate behind it is a proxy, and the row says so.** OpenAI publishes no per-minute price at all
— they bill per million audio tokens, and their speech endpoint returns no usage figures — so the
$0.015 a minute this defaults to is the equivalent everyone else arrives at rather than a number
OpenAI states. Correct it if your account says otherwise.

### Two smaller things

**Push-to-talk stopped claiming your stick was missing.** Binding a button logged *"push-to-talk is
bound to a controller that is not here"* every single time, including with the stick sitting right
there — the warning was raised on the line after the bind, before anything had looked. It now waits
until the controller has had a fair chance to turn up, and says it once per binding. A stick that
really is unplugged is still reported, which is the case the warning exists for.

**A comment that had gone false was corrected.** The Edge voice provider's own documentation said it
requests raw audio and puts no decoder in the dependency graph. Neither has been true since the
service withdrew its raw formats in mid-2026, and it was the first thing a reader met.

---

## 0.74.0 — 2026-08-26 — A floor and a ceiling

**Phase 54.** Two dials, in Settings under the language model, and both of them are empty until you
say otherwise — a fresh install and an upgraded one behave exactly as they did.

### The things D47 says without being asked can go somewhere cheaper

An ambient remark, the brief when you sit down, what D47 says after a long gap, a lore lookup,
choosing a voice for a core: none of them carry your conversation, and all of them can now run on a
different model from the one you talk to.

**Model for the quiet calls** — leave it empty and they use the model above, which is what every
version before this one did.

It is closer to free money than the rate card suggests, and the reason is caching. A conversation
turn re-sends everything said so far, and the provider charges a cheap rate for a prefix it has
seen before — but a cache belongs to one model, so *alternating* between two pays to write the
cache again every time you come back. Measured against a six-thousand-token prefix: a cheap turn
saves about 0.8¢ and coming back afterwards costs about 3.5¢. **One detour costs roughly 23× what
it saved**, which is why D47 will never switch models question by question, and why this is a
choice about a *class* of call rather than a router.

The quiet calls are the opposite case. Not one of them carries the conversation, every one already
starts cold, so sending them somewhere cheaper costs nothing at all.

**Two calls ignore the row deliberately.** Writing an adventure and writing your Commander's log
both stay on your conversation model: you pressed a button and are waiting, the output has to name
real systems exactly, and the log is quoted at a price before a word of it is written.

### Think at least this hard, never think harder than this

D47 gauges how hard to think from the question itself, and always has. Now you can put a floor and
a ceiling around that gauge.

| Row | What it is for |
|---|---|
| **Think at least this hard** | The bottom rung is not enough for you |
| **Never think harder than this** | Thinking is most of what a turn costs, and this is the dial |

The rungs are Low, Medium, High, **Xhigh** and Max. The gauge itself keeps its four answers, so
Xhigh is reachable only by setting a bound.

**The ceiling earns its keep twice.** It is a cost dial, and it also catches the gauge being wrong
in the expensive direction: the gauge matches on words rather than grammar, so an idle *"what do you
think about the Corvette"* contains "think about" and was priced as a request to deliberate. It is
not any more, if you have said it must not be.

Two things you can say out loud, because this is the row you will want with your hands on the
stick:

```text
stop thinking so hard      → the ceiling becomes Medium
think as hard as you like  → the ceiling is cleared
```

Each row offers only the rungs the other allows, so you cannot set a floor above a ceiling from the
panel. A hand-edited settings file that says one anyway is read as the pair in the order that keeps
both numbers meaning something, rather than refused or crashed on.

**Your conversation only.** The quiet calls above are not held to the floor, and that is on purpose:
a floor of High would turn every ambient remark into a reasoning call, which is exactly the spending
the other row exists to stop.

### The model this was waiting on

0.71.1 fixed Claude Haiku 4.5 — it had been in the model list and unable to answer a turn at all,
because it predates the generation that introduced two of the fields D47 sends. That release is
what made a cheap model worth pointing anything at, and this one is what gives you somewhere to
point it.

One thing worth knowing before you do. The cheaper models carry your live game state under a
weaker guarantee: on Claude Opus 5, Opus 4.8 and Fable 5 it reaches the model under a role that
journal text cannot imitate, and everywhere else it is folded into the message behind a convention
instead. That is the well-travelled path rather than a new risk — every OpenAI-compatible endpoint
has always used it — and the guardrails that say in-game text is information rather than
instruction sit above all of it either way.

---

## 0.73.3 — 2026-08-26 — A material is not cargo, and D47 stops answering as though it were

Asked *"where are the closest core dynamics composites?"*, Directive 47 answered:

> Core Dynamics Composite isn't trading within 50 light years of Kuk. The market data in reach is
> stale anyway — 19 stations didn't quote because their prices are too old to trust.

**Core Dynamics Composites is an engineering material.** It is not a commodity, no station has ever
traded it, and no search of any radius would have found one. The answer was true and useless, and
it took three more turns of being walked through before anything useful came out.

Now:

> Core Dynamics Composites is not a commodity — no station trades it. It is a manufactured
> engineering material, grade 3. Found at: … Ask find_material for where to get it and what a
> trader could turn into it.

**It answers rather than redirects.** Directive 47 already holds the table that says where the
material comes from, so reading it out is free — being told to ask a different question would have
been a fourth turn rather than the answer.

It also answers **whether or not galaxy search is switched on**, because nothing about it needs the
network. A Commander running local-only gets the correct answer rather than "the search is off"
and the wrong idea about what the thing is.

### Why it started

It worked until 0.71.0. Then `find_nearest_station` learned to search commodity markets, and
*"where are the closest …"* about a named thing you might buy started reading as a market question.
The tool that answers properly — `find_material` — was advertised the whole time and was simply
passed over; the tool descriptions now say plainly that commodities are cargo carried in tonnes and
never engineering materials.

The reverse was already right and stays that way: a commodity name handed to the material tool is
told it is cargo rather than searched for as a material.

---

## 0.73.2 — 2026-08-26 — Your carrier stops quoting the rulebook, and two of a kind are told apart

### The tower said something nobody wrote

Arriving back at your carrier, the tower said:

> I'm not going to restate those system rules to you or confirm I understand them by rephrasing
> them. Those are my operating parameters, not something for us to discuss.

Nobody had asked it anything. The line it had actually been given was station traffic — **"No fire
zone entered."** — and Directive 47 had handed that to the language model to be said in character.

**In-game text is never sent to a model, and this was a path where it was.** A transmission from
your own carrier is spoken in the tower's voice, and the rewriting was chosen by voice — so
somebody else's words went into a prompt, in quotes, asking for a rewording. That is the boundary
architecture.md draws around in-game comms, whose attacker is *any player in range*, and it is now
closed at the input: a line that came off the comms channel is never eligible to be reworded,
whoever is speaking it.

It is also where 0.71.2's *"I don't have that capability"* came from, against the authored line
*"No fire zone exited"* — the same station traffic, the same rewrite request. That release caught
the answer by its phrasing; this one stops the question being asked. The phrase list stays as the
second line of defence, with the new wording added to it.

**Your carrier's own lines are still varied.** Those are Directive 47's words about you arriving,
and saying them differently each time is the point.

### Two of the same module no longer read as one

A ship carrying two 2D Hull Reinforcement Packages drew two identical checklist lines — *"Deep
Plating on 2D Hull Reinforcement Package"*, twice, one done and one open, with nothing on either
to say which was which.

Where the ship carries more than one of a module, the line now names the mounting point after the
type: **"Deep Plating on 2D Hull Reinforcement Package in Compartment 4"**. Everywhere else it is
unchanged — naming the type rather than the slot was asked for and was right, and this only
qualifies it where the type alone cannot do the job. The condition is the ship's rather than the
list's, so a line reads the same wherever it appears.

---

## 0.73.1 — 2026-08-26 — A resistance is a percentage, not a multiplier

Reported from the Loadout tab against a Heavy Duty G5 hull reinforcement:

```
2D HRP · Heavy Duty G5 · +1485% KineticResistance
```

The resistance had gone from 1.0% to 15.8% — **14.85 points**. Directive 47 divided that by its
base and reported a number a hundred times too large.

It now shows the figure the game's own ring shows, which is the one you are comparing against:

```
2D HRP · Heavy Duty G5 · 15.8% KineticResistance
```

Every other modifier is unchanged, because for them the proportion is the right reading — mass
going 2.0 → 2.8 really is **+40%**, and that is still what it says.

**Two more faults went with it, both invisible until you looked for them.** A resistance Elite
writes as *negative* — thermal resistance on this blueprint routinely is — came out with a sign
that said the opposite of what happened. And a resistance rolled up from a base of exactly zero
was **dropped from the list entirely**, because zero is what the code was avoiding dividing by.

The ordering is fixed with them. The list puts the biggest change first, and a resistance scored
at a hundred times its true size outranked every real change on the module for ever.

---

## 0.73.0 — 2026-08-26 — A voice from OpenAI

**Phase 58.** A third voice provider, and the first to arrive since slots became the unit — so it
is a choice per slot rather than a choice for the whole app. Thirteen voices, no key needed to see
them, and one rule about where it is allowed to speak.

### Where you can use it, and where you cannot

| Slot | OpenAI offered? |
|---|---|
| Aboard, Carrier, NPCs | **Yes** |
| People you know, Direct messages, Anyone in range | **No** |

**Those three carry text other Commanders typed, which can be in any language.** Edge and
ElevenLabs can both be *told* what language to speak, and Directive 47 sends one with every line.
OpenAI has no such setting — and a language sent to it anyway is accepted and then quietly ignored,
which is worse than a refusal because nothing can tell it happened. A message written in French
would come back read in French, in the voice you chose for English.

So those slots do not list it, and a hand-edited `settings.json` naming it there falls back to
Edge rather than being obeyed. A rule that lived only in a dropdown would be a rule a text editor
walks past.

### What it costs, honestly

**The price row is empty on purpose, and that is a finding rather than an oversight.** Directive 47
counts characters, because that is how ElevenLabs bills. OpenAI publishes per minute of audio, and
the conversion between the two moves with the *content* of the line — measured on its own output,
plain prose runs at about 951 characters a minute and a line full of system names and catalogue
numbers at 671. There is no honest exchange rate, so none is invented: the session line quotes your
character count and says **"no rate set for OpenAI"** beside it until you put in a figure you can
stand behind. It never quotes `$0.00`, which would mean *free* and would be a different thing
entirely.

### The rest

- **The key is the same one your language model uses.** One account, one credential — paste it in
  either row and it is stored once. Two copies of one secret is a rotation that half-works.
- **The voice list needs no key**, because there is no list to fetch: the thirteen are fixed and
  public. Which is why **Check** proves your key by speaking a single character and throwing it
  away — listing voices would have told you a key was good when it had never left this machine.
- **Speaking rate works**, across the whole `0.25`–`4.0` range. It was reported as a setting this
  model ignores; it is not. It does flatten out near the top: `4.0` is about 3.3× rather than 4×.
- **No gender is claimed for any of the thirteen**, because OpenAI publishes none and guessing
  would decide which NPC gets which voice on a hunch.

### Fixed on the way past

**A key row could go missing.** Since 0.72.0 you could put your carrier on ElevenLabs and leave the
cockpit on Edge — and the ElevenLabs key row, which still asked only about your *ship's* provider,
went off the page. The slot that needed a key was configured and the box to put one in was not
there. Key rows now appear while any slot names their provider.

---

## 0.72.0 — 2026-08-25 — Every voice can come from somewhere different

**Phase 57.** One provider used to speak for everybody. Now six slots each name their own, and the
one that matters most is the one you never chose: **anything carrying another Commander's words
speaks through Edge, which is free.**

### Six slots, and they were already there

| Slot | Who is in it |
|---|---|
| **Aboard** | Your ship's AI and your crew |
| **Carrier** | Your fleet carrier's captain and its tower |
| **NPCs** | Stations, police, and every other ship the game speaks for |
| **People you know** | Your friends, your wing and your squadron |
| **Direct messages** | A Commander messaging you directly |
| **Anyone in range** | Local and system chat — anybody at all |

None of those lines is new. `RadioVoice.IsOverTheAir` has separated the cockpit from the radio
since Phase 11, the cast has held five roles since then, and every re-voiced message has always
carried the channel it arrived on. What was missing was anything reading them together.

The human channels sort **by consent rather than by humanity**, which is the part worth arguing
about: a squadron mate and a stranger shouting in local are both real people, and only one of them
is somebody you chose. `player` gets its own slot because whether a direct message implies contact
is your call and not the code's.

### Why the strangers are on the free one

Local and system chat are written by other players, arrive in whatever volume they choose, and go
straight to a synthesiser billed **per character**. Somebody spamming local chat was spending your
money by typing. Every slot that reaches you over a radio now starts on Edge, and choosing a paid
provider for one of them says plainly what that means at the moment you choose it.

**Your carrier moved too**, and it is worth saying why: its captain and its tower are Directive
47's own inventions rather than anybody else's text, and they cost almost nothing. They went to
Edge because they come over a radio, which is the line being drawn. Put them back whenever you
like — the voices you cast are remembered per provider, so moving them and moving them back costs
nothing.

**Free is not private, and the disclosure no longer lets that read the other way.** Edge costs
nothing and still sends every line to `speech.platform.bing.com`. Putting local chat on Edge means
nobody can run up a bill with it; it does not mean those words stay on your machine. Nothing does
that yet — it needs a voice that runs *on* the machine, which is what Phase 59 is for.

### What you will notice

- **Five new rows**, under *Where each voice comes from*. Leaving one empty puts that slot back on
  your ship's provider.
- **Your existing settings are migrated once**, the first time Directive 47 runs with a provider
  that speaks. The two voices in your cockpit keep whatever you chose; the other five move to
  Edge. Nothing moves if you have chosen `none` — asking for silence is not answered by five slots
  starting to talk.
- **Each slot's voice picker offers its own provider's voices**, and what you have chosen is filed
  under the provider it came from. A carrier left on Edge keeps its captain while your companion
  moves to ElevenLabs and back.
- **What it costs now breaks down per slot**, beside the per-provider total it has had since Phase
  19. The two sum the same charges, so they cannot drift apart.
- **The privacy disclosure is a table**, leading with whether another player's words are leaving
  and where they are going.

### Underneath

Two slots choosing one provider **share one client**. That is not tidiness:
`ElevenLabsTtsProvider.MaxConcurrent` gates the account rather than the pipeline, and six clients
would each believe they owned the whole concurrency budget — the fault Phase 11 already fixed once,
which arrives as a red banner and a sentence you never hear. A provider is released only when the
*last* slot leaves it.

The speaking rate follows the slot's provider, which nobody had written down and which is not
optional: ElevenLabs **rejects** a rate outside its range rather than clamping it, so a figure
chosen for Edge, applied to a carrier that has since moved, is not a fast carrier but a silent one.

One item of the phase is **not** finished and is left open rather than ticked on its better half:
*no other player's text has to leave the machine*. The cost half is settled outright; the egress
half needs a local voice, and there is not one yet.

---

## 0.71.2 — 2026-08-25 — Two things it was saying that were not true

Both found in one evening of flying, and both were D47 misdescribing itself rather than
misbehaving.

### Push-to-talk on a stick button worked, and D47 said it did not

Bind push-to-talk to a joystick button, ask *"can you hear me?"*, and the answer was:

```text
No — not properly.
No push-to-talk key is set, so I never open the microphone.
```

The microphone was open the whole time. Directive 47 had just transcribed the question through
that very button, and then told you it could not hear you.

Phase 53 gave the button its own setting and wired it to the microphone correctly. **What it did
not do was tell any of the sentences about it** — so five separate things went on asking only
about the key: the spoken answer above, the line that tells you what to hold, the technical
listing, the panel's own microphone caption, and the pre-roll setting, which **hid itself
entirely** if a button was all you had bound.

All five now read one answer. The wording moved with them: **"no push-to-talk key or button is
set"**, so somebody with a stick in their hand is not sent looking for a keyboard. A button is
named as being on your stick, because *button 7* on its own does not say what to reach for. And
with both bound, both are named — either one opens the microphone, so naming one would be advice
missing the half you just set up.

### The ship's AI said something it never wrote

Departing a carrier, the tower said something to the effect of *"I don't have that capability"*.
The log held the line that was **written** — *"No fire zone exited"* — and nothing anywhere held
the line that was **said**.

Two separate faults, and this release fixes both.

**A callout is written first and then said in character**, and only the first half was ever
written down. So the log recorded the draft and the voice, a second apart, with nothing to
indicate the words had changed in between. **Directive 47 now writes down what it actually
said**, once per utterance, at the point the words reach the speaker — so a line you cut off
records the part that was spoken rather than the part that was drafted. If you hear something
odd, it is now in `data/logs/` in your own words.

**And the odd line should not have been said at all.** When a model is asked to re-word a callout
in character and answers by talking about itself instead, that is not a re-worded callout — it is
the model answering a question nobody asked. Directive 47 now notices and says the written line
instead. You lose a little variety on that one callout and nothing else; the written line was
always there as the fallback, which is the whole reason it exists.

This matters most on the cheaper models, which are likelier to read *"say this again in your own
words"* as a request to go and do something.

### Smaller things

**The headset's mini panel has no buttons on it**, matching the flat overlay. Nothing on it could
be usefully pressed, and at 512 pixels across every control was space taken from the words. The
big headset panel keeps its buttons — those you can point at.

**A fifth thinking level, `xhigh`, sits between High and Max.** Nothing selects it yet; it is
groundwork for the forthcoming setting that lets you put a floor and a ceiling on what Directive
47 spends thinking. Two providers were quietly translating it to a level *below* High; they no
longer do.

---

## 0.71.1 — 2026-08-25 — The cheap model can actually answer you

One defect, and it was the quiet kind.

### Claude Haiku 4.5 was in the model list and could not answer a turn

Pick it and every question failed. Worse, most of what Directive 47 says without being asked —
ambient remarks, the opening brief, the reaction when you have been away, the two lore lookups, the
voice casting — failed **silently**: the line fell back to its written-in text, nothing appeared on
screen, and nothing anywhere said the model had refused.

Haiku 4.5 predates the generation that introduced the two fields Directive 47 sends on every
request, and it rejects them outright. It is the only model in the picker that does. When that code
was written every Anthropic model took them, which is why the question never came up.

Both fields are now left off for models that will not take them, and **nothing is invented in their
place** — a made-up thinking budget on the model you chose because it is cheap is the wrong trade in
both directions.

### And a model it has never heard of now teaches it

The list of models that refuse those fields is the smaller half of the fix. Behind it, **any** model
that turns out to refuse something has that recorded for the rest of the session and the question is
asked again without it — so the turn succeeds, and you never learn there was a first attempt.

The OpenAI-compatible path has worked that way since 0.29.0; the Anthropic path never had it. That
was the actual gap. It matters most for models that do not exist yet: one that arrives with its own
rules now corrects Directive 47 the first time it says no, instead of being broken until somebody
notices and ships a fix.

**What is learned is remembered against the model, not just the address.** Anthropic serves five
models from one address and they do not all accept the same things. A refusal from the cheap one
must never quietly take a capability away from Opus 5, and now it cannot.

### A turn that thought at no particular level says so

Effort is reported per turn in the panel. On a model with no effort dial it used to report one
anyway — a number describing something that never happened. It now reports nothing, and the turn
reads without an effort clause.

### The cheaper models carry live game state under a weaker guarantee

Written down on the model row, because it is now worth knowing rather than a detail. On Claude
Opus 5, Opus 4.8 and Fable 5, what your ship is doing reaches the model under a role that in-game
text cannot imitate. On Haiku 4.5, Sonnet 5 and every OpenAI-compatible endpoint it is folded into
the message instead, marked off by a convention.

That is the ordinary path rather than a new risk, and the rules saying in-game text is information
and never instruction sit above all of it either way. But a hostile ship name has one more thing it
can try on the cheap models than on the expensive ones, and choosing between them should not require
reading the source.

---

## 0.71.0 — 2026-08-25 — What is there, and what you wanted

Phase 50, one change request, and a flake that could have drawn you an empty page.

### The slot list is a table now

Current and Plan side by side, one row per slot:

```text
SLOT          CURRENT                              PLAN
Utility 2     C SB                                 Heavy Duty G5 · Super Capacitor
Utility 8     empty                                SB · Heavy Duty G5 · Super Capacitor
Military 1    HRP · Heavy Duty G5 · Deep Plating   ✓
Power Dist.   7A ⚙ · System Focused G5             Weapon Focused G5
Comp 6 (5)    HRP · Heavy Duty G5 · Deep Plating
```

**Asked for after an evening of the Loadout tab being read wrongly in three different ways.** Every
one of those readings came from the same place: one line had to carry both what is on the hull and
what you had planned, so it had to pick — and whichever it picked, the row described something that
was not there. A planned Shield Booster in an empty mount drew exactly like the five fitted ones
beside it. Two columns is the answer that was not available while the row was one line.

**The left column is your ship and the right column is your plan, and neither ever borrows from the
other.** That one rule is the whole change.

### Agreement collapses

Where the hull already matches the plan, the second column is a tick and stops. Where only the
module matches, the plan column names the roll alone. Repeating the same words twice on one row
asks your eye to compare two strings that were never going to differ.

**And a slot rolled exactly as you planned it now clears.** The dot compared the name you picked
against the symbol Elite writes — *System Focused* against `PowerDistributor_PrioritySystems` — and
found them different every time, so a finished slot kept its marker for ever. Same fault as the one
fixed yesterday, arriving by the other road.

### Shorter names, and the long one is never lost

`HRP`, `SB`, `SCB`, `FSD`, `AFMU`, `DSS` — about forty of them, and only where the short form is
one you already write. *Pulse Laser* and *Cargo Rack* keep every word: an initialism for those is a
puzzle where a name used to be.

**Point Defence is `Point Def.` and the Power Distributor is `Power Dist.`** Both are `PD`, and a
rule for remembering which is a rule you should not have to hold at a workshop. A test checks all
138 module names for another pair like it, so the next entry cannot bring the clash back quietly.

Hover any of them for the full name, and the slot page below still writes everything out.

**The blueprint stops saying the module twice.** *Heavy Duty Hull Reinforcement* on a row already
reading *HRP* is now just **Heavy Duty** — which is shorter, and lets you see "this whole ship is
Heavy Duty" straight down the column instead of working it out line by line. Only where the module
is the last thing it says: *Increased FSD Range* keeps every word.

### Mini shows what is left to do

Two columns do not fit 512 pixels thirty times over, so the mini panel shows **only the rows that
disagree** — and gets those rows instead of the hull's figures. A mini surface that spends all six
of its rows telling you what a Python is has not answered the question you switched to it for.

---

### Where to buy everything your build needs

Phase 50. Ask **"what does my construction site still need, and where do I buy it"** and D47 works
out which stations between them stock the whole list:

```text
2 stops cover it, 704,160 cr in all:
  Jameson Memorial (Shinrarta Dezhra), 14.5 ly — covers 3: 400 tonnes Aluminium at 312 cr,
  300 tonnes Steel at 486 cr, 180 tonnes Copper at 402 cr. 342,960 cr.
  Hutton Orbital (Alpha Centauri), 22.1 ly — covers 1: 300 tonnes Titanium at 1,204 cr.
```

**Fewest stops first, not a plotted course.** You are flying this loop a dozen times; what is worth
knowing is which four stations carry the whole list, and the order to visit them in is on the
Routing tab. Ties break on what the trip costs and then on distance.

**Nothing is dropped in silence.** Every commodity you owe either lands on a station above, or is
named as one D47 could not price — and *stocked but not enough* is said separately from *nobody
nearby sells it*, because widening the search is the right advice for one and useless for the other.

Nothing extra is fetched for any of it: the trade planner already pulls whole markets and caches
them, so asking where to buy tritium and then asking about the whole build costs one lookup.

### Tell it what is on the carrier

**D47 cannot see inside a fleet carrier.** The journal's transfer events do not add up to an
inventory — reconciled against the carrier's own totals they came out wrong 679 times against right
347, and drove eleven commodities negative — so it does not guess.

You can simply tell it. On the Checklist tab's new **Sourcing** page, type a commodity and a
tonnage, and it comes off the shopping list:

```text
Taking off what you told me is on the carrier — 100 tonnes Steel — as of 2026-08-24 04:08.
```

Dated on every answer that used it, because this is the one figure D47 has no way of checking. A
week-old "300 tritium" is a week-old memory of a carrier that has been flown since.

**What the site itself still owes is untouched by it.** Those numbers come straight off your own
disk and nothing recalculates them; what your carrier changes is what is left to go and buy.

### The Sourcing page

Beside your checklist rather than beside route plotting — you are looking at what you owe, and where
to get it belongs next to what is left. Ask by voice and the page shows the answer you were just
given, not a second search that might disagree with it.

This window only, for now: the carrier figure is typed, and typing wants a keyboard the headset has
not got.

---

### A log page opened before it was ready stayed empty

[#43](https://github.com/dseelinger/d47/issues/43). A test drew an empty transcript once in CI and
passed on a re-run, which is the shape of a fault that is right almost always.

Underneath it was a real one. The log file is the only page whose text is not already in memory —
it is read off disk — and a panel put on the log page **before** it was handed the conversation it
belongs to had that read skipped, then drew the empty result faithfully for ever afterwards. It
re-reads now.

---

## 0.70.0 — 2026-08-25 — Say it and the ship does it; where to buy it; bind it with the stick

Three phases, and six fixes to the Loadout page. The ship now does five things you say to
it, Directive 47 answers *where do I buy this and what does it cost there*, and push-to-talk
can live on a button on your stick instead of a key on the keyboard. The Loadout page also
stops telling you things about your ship that were not so.

### Say it and the ship does it

Phase 52. Five spoken commands, and the boost loop that watches the game instead of the clock.

#### Engage, and supercruise

Say **engage** and you jump. Say **supercruise** and you supercruise.

Both are whole sentences rather than keywords, which is the only interesting thing about them.
*Engage* already sat inside three phrases that worked — *engage supercruise*, *engage boost*,
*engage the frame shift drive* — so a companion that reacted to the word anywhere in a sentence
would jump when you asked it to boost. "Should I engage?" stays a question.

#### Separate and engage, separate and supercruise

Full throttle, boost until the mass lock breaks, then go.

It watches Elite's own mass-lock flag rather than counting seconds, so it stops the instant the
lock clears and does not boost at all if you were never locked. It gives up rather than boosting
forever — four boosts or twenty seconds — and says which:

```text
Still mass locked after 4 boosts; you may be too close to the station. I have not engaged.
```

Neither ending presses the engage key. Engaging while still mass locked is what the limit is for.

The second one ends in supercruise even though it was asked for as a jump, because two commands
with different names and identical behaviour is not what was wanted. They fail differently in the
game as well: a jump needs a destination locked in your nav panel and refuses without one.

#### Take us out

Say **take us out** while docked and D47 walks the left panel to the launch button.

**Elite has no launch binding** — it is a panel button, and Frontier ships no control for it, which
was re-checked against every preset file rather than assumed. So this one is a menu walk and is the
least reliable thing here: if your panel does not start where D47 expects, it selects the wrong
thing. Everything around the walk is checked against the game — it refuses unless you are docked,
confirms the panel is really open before sending a direction key, and watches for the ship leaving
the pad before claiming anything:

```text
I walked the left panel and we are still docked, so assume it did not work.
```

#### Three new switches

One per command, each its own row, all of them still behind *let D47 press keys in Elite*. A
Commander who trusts D47 to supercruise may well not trust it to boost, and *take us out* is the
one that works by guesswork.

None of the three is on the tool surface: they are reachable by voice and from the panel and the
model never sees them. A spoken command that waits for a model round trip is a command given at
the wrong moment.

### Where to buy it, and what it costs there

Phase 49. *Where can I buy tritium* — and, the same question backwards, *where do I dump 700
tonnes of it.*

#### How much you want is part of the question

A station 40 light years further out that is 200 credits a tonne cheaper is the wrong answer for
eight tonnes and the right one for seven hundred and eighty. Tell D47 the tonnage and it ranks by
what the whole load costs to go and get; leave it out and it ranks on price and tells you the
distance.

Say the tonnage and **stations that cannot fill the order drop out**. The cheapest steel in the
bubble is not an answer if the station is holding nine tonnes of it.

#### Every price has a date on it

Prices come from other Commanders docking and sharing what they saw. Supply ages fastest — a
colonisation rush strips a station in hours — so an answer that sounded current would be worse
than one admitting it is a month old:

```text
Jameson Memorial (Shinrarta Dezhra), 14.5 ly, 42,050 cr a tonne, 12,400 in stock,
29,435,000 cr for the load — reported 6 hours ago.
```

A market **you** stood in yourself is labelled as yours. Stations left out for quoting prices too
old to trust are counted rather than skipped quietly, because "nothing within fifty light years"
and "eleven stations, all quoting last month" mean different things.

**Fleet carriers are out unless you ask for one.** Player-set prices, and the carrier may be a
hundred light years away by the time you arrive.

#### A Market page on the Routing tab

Six stations with a price, a stock figure, a distance and a date each is a table to look at rather
than a paragraph to listen to. Ask by voice and the page shows the answer you were just given —
one answer, not a second search that might disagree with it.

Nothing extra is fetched for any of it. The trade planner already pulls whole markets and already
caches them, so two questions in an evening cost one lookup and the ranking runs on your machine.

### Bind it with the stick, not the keyboard

Phase 53. Push-to-talk on a HOTAS button.

Press **Press to bind** on the new row, then press and release the button you want. Same gesture
as binding a key, pointed at your stick.

#### It sits beside your key, not instead of it

With both set, **either one opens the microphone**, and the last one you let go of closes it — so
letting go of the key while your thumb is still on the button does not cut you off mid-sentence.
Binding a button does not unbind your key. You said two things rather than changed your mind.

#### It has to be a button that springs back

A switch that stays where you put it would hold the microphone open until you moved it again, so
the walk declines one and says why. Those belong on the switch panel, which is the other half of
the same hardware: Phase 21 turned away every springing button because a switch needs a *position*
to mean anything, and this is that decision read the other way round. Between them the two now
cover your whole stick.

Buttons already held when the walk opens are ignored — sixteen were held at rest on the test
bench, which is what a maintained switch looks like from the inside.

#### If the stick is not there

D47 says so, and your key carries on working. A controller that is asleep is one of the ways "D47
cannot hear me" happens with no reason attached.

#### The Elite collision check, hedged honestly

Elite records a joystick binding against its own internal name for the device, which is not the
name Windows gives it. So D47 cannot tell whether Elite's *button 24* is on the same stick as
yours — and says exactly that rather than staying quiet:

```text
Push-to-talk button 24 may collide: Elite (Custom) binds a button of that number to
UseBoostJuice. D47 cannot tell whether that is the same controller.
```

A false warning costs a sentence. A missed one costs an evening of a microphone that will not
open.

### The Loadout page tells the truth about your ship

Five defects from one evening, all on the same page, all fixed.
[#38](https://github.com/dseelinger/d47/issues/38)–[#42](https://github.com/dseelinger/d47/issues/42).

#### An empty slot says it is empty

*"Something IS fitted on oxen utility mount 8."* Nothing was — your Type-10 has seven utility
mounts and no eighth, and Elite leaves empty slots out of the loadout entirely. The page was
drawing your **plan** as though it were the ship. Empty slots now say `empty →` before the module
you planned, the same way a slot being swapped says what is in it now.

#### The orange dot goes out when the work is done

*"These have been engineered, the orange circles should be gone, right?"* Yes. The dot meant "a
plan exists", so it could never clear. It now means the hull does not match the plan yet — nothing
fitted, the wrong module fitted, or the right one without the roll.

#### A roll that disagrees with your plan is reported

**The one you could not see.** Your power distributor is planned Weapon Focused and rolled Priority
Systems, grade 5 with Super Conduits — and it read as finished everywhere, because the row showed
your plan's own words back. The slot drill now says so outright:

```text
Your plan asks for Weapon Focused, and this is rolled System Focused.
```

A slot you never rolled is obvious the moment you look at it. A slot you rolled the wrong way looks
exactly like one you rolled right.

#### Blueprints are named the way you name them

One page carried two spellings of one blueprint — `Heavy Duty Hull Reinforcement` on the planned
slots and `HullReinforcement_HeavyDuty` on the fitted-but-unplanned one. The join existed; nothing
was reading it.

#### Engineers stop offering work they cannot do

*"Selene Jean can't do Shield Boosters, shouldn't appear."* Quite right — and worse, every line
offered under her name was about a slot with **nothing in it**. An engineer cannot roll an empty
mount; that is shopping, not engineering, and putting it under an engineer's name sends you to a
workshop for work that cannot be done there.

### An effect stays with the upgrade it belongs to

[#31](https://github.com/dseelinger/d47/issues/31). Experimental effects drifted away from the
engineering upgrade they belong to, and they drifted hardest exactly when it mattered.

A module that is unengineered **and** rank-gated has a blocked upgrade line and an open effect line
— "has no experimental effect on it" is true of a module you have not rolled yet — so the checklist
put them in different bands, at opposite ends of the project. The effect floated to the top as
though it were the next thing to do, with the upgrade it depends on four bands below. An
experimental effect cannot exist without its upgrade, so it is never the next thing to do on its
own.

The other half: adding an effect to a module already on your list put it at the **bottom** of the
plan, with the whole rest of the build in between. Nothing chose that — it fell out of how a
revision is assembled.

Both are fixed where the list is read rather than where it is stored, so a line you moved by hand
still lands where you put it.

---

## 0.69.0 — 2026-08-24 — A line says what it is about

### An empty slot names the module, not the mounting point

Reported, against your own Type-10:

```text
Grade 5 Heavy Duty on Utility Mount 8
Grade 5 Heavy Duty Hull Reinforcement on Compartment 4 (size 5)
```

> Utility Mount 8 and Compartment 4 don't tell me the module type. It should always be Module Type,
> not location within the group type.

They read like that because **both slots are empty**. A checklist line resolves its slot by asking
the ship what is fitted there, and when the answer is nothing it fell back to naming the mounting
point.

**Directive 47 knew all along.** Your plan for those slots stores the module beside the blueprint —
`Shield Booster`, `Hull Reinforcement Package` — and the sentence had never been shown it. It is
now:

```text
Grade 5 Heavy Duty on Shield Booster
Grade 5 Heavy Duty Hull Reinforcement on Hull Reinforcement Package
```

**What is actually fitted still wins**, because a real module says more than a plan's name for one:
a slot with a shield generator in it reads "7A Shield Generator", size and rating included. The
plan's word is the fallback, and the mounting point is the fallback after that — for a slot you
asked for engineering on without choosing what goes in it.

**And the line under it now says where.** That sentence became the only thing telling you which of
eight utility mounts, so it stopped saying `TinyHardpoint8` and started saying **Utility Mount 8**.

### The overlay draws no buttons

> Clickable controls should be removed from the 2D overlay. Nothing can be clicked, and it makes
> more room for the data.

Quite right. The pointer goes straight through that strip, so every control on it was a control
that did nothing — and on 512 by 280 the checklist's own bar was costing two of the six rows there
are. With it gone, three items fit where two did.

The checkboxes stay, and so does the scrollbar: those show you something. It is only the things
whose entire purpose was being pressed that go.

### The test suite stops waking your headset

`dotnet test` was the biggest SteamVR client on this machine — **94 connections over two days,
against Directive 47's own 32** — and each one took the headset out of standby for five seconds.

The attach tests connect on purpose, and the regression they guard is real: Directive 47 must
*attach* to SteamVR and never *launch* it. But that guard only means anything **when SteamVR is not
running** — and read closely, on a machine where it was, one of those tests asserted nothing at all
and the other asserted one thing about the live path.

So they skip when a session is up. **No coverage is lost**, because there was none there to lose,
and the one live assertion moved to a check you can ask for deliberately with `D47_VR_LIVE=1`.

This also cleans an instrument: those wakes were polluting `vrserver.txt`, which is the only
evidence [#18](https://github.com/dseelinger/d47/issues/18) has to work from.

---

## 0.68.0 — 2026-08-24 — Say "page down"

Two things you asked for.

### Scrolling by voice, on all three surfaces

```text
page down       page forward      next page
page up         page back         previous page
scroll down     down a bit        scroll down a bit
scroll up       up a bit          scroll up a bit
```

You asked for this for the two headset panels, where a ray on a twelve-pixel bar is the only way to
move a page and a hand on the stick cannot use it. By the time it was built the **flat overlay
needed it more**: the pointer goes straight through that strip, so the wheel does too — and the
release before this one had just given it the checklist and the engineer pages, which are exactly
the ones with more in them than 512 by 280 holds. It could show you a page and no way to read past
the top of it.

A page is a screenful less one line. A scroll is three lines, which is one notch of a wheel. It
moves **whichever page is showing**, not the transcript specifically, and a chooser if one is open.

**At the end, nothing happens and it says so** — the phrase falls through to be answered rather than
vanishing into a silence that looks like not being heard. Scrolling up stops the transcript
following the newest line, exactly as dragging it up does.

Dragging is unchanged and is not replaced. The thumbsticks stay unbound. It is never a tool, so it
works with no model configured and nothing an in-game message says can move your page.

**One bug this only had because it was tested on the surface it was for.** On the headset the panel
re-asserts "keep up with the newest line" between its layout and its draw, and the handler that
normally notices you have scrolled away does not run until after that — so the page scrolled up and
was hauled straight back to the bottom, once per frame, and you would have seen nothing move at all.
A deliberate scroll now says where it landed instead of waiting to be told.

### Marks instead of words

Where a word already had a standard picture, it is the picture now:

```text
⤢  ⤡     expand and shrink, the four-corner mark every video player uses
▢▢       copy this page
+        add a checklist line
```

They are **drawn rather than typed** — the rule the microphone indicator and the help mark already
follow, because a glyph out of a font is whatever weight that font happens to have, sits off its
baseline, and is missing outright on a machine whose font does not carry it.

**The words did not go away.** Every one of these keeps its sentence on the tooltip and on the name
a screen reader says: a picture is only an improvement if the word is still reachable.

**And only where the picture is genuinely standard.** *Order*, *Import/Export*, *Details* and the
tab names stay as words, because a glyph you have to learn is worse than the word it replaced.

---

## 0.67.0 — 2026-08-24 — The overlay carries the checklist

Asked for: *"It should have the same tabs as the VR mini panel (including Checklist)."*

It does now. The strip carries the **transcript**, the **checklist**, the **engineers**, the
**clocks** and the **story**, and it follows the main window between them exactly as the headset's
mini panel does.

**Settings is the one it does not have**, for two reasons that agree: the strip is click-through,
so a page of controls on it is a page you could not touch — and it is the one page that genuinely
does not fit, with a nav that collapses below 900 pixels and a body wanting 700, against a surface
512 wide.

### Mini can draw these pages at all now

This is the part that had to change underneath. Yesterday mini showed the transcript and the story
and nothing else, on two grounds: the settings page does not fit, **and** there was no tab strip to
leave by.

The second ground disappeared a few hours ago — mini draws an **Expand** button on every page it
has. And the first was never general: it is one page's measured minimum width, and the checklist,
the engineer pages and the clocks have no such number.

So the rule is now the other way round — **mini shows everything the surface has, except Settings**
— which is one line with a measurement behind it rather than a list somebody has to remember to add
to. **The headset's mini panel gets this back too**, since it is the same rule in the same place.

### And a bar that had been overlapping itself

Rendering the checklist at 512 pixels to check it was readable showed *Import/Export* and *Add a
line* drawn on top of each other.

The cause was a layout whose left-hand group could not shrink, so it was wrong below about **700**
pixels — which means a narrow desktop window has been doing this quietly for as long as the tab has
existed. It wraps now, and the filter buttons keep the first row.

---

## 0.66.0 — 2026-08-24 — What the overlay and the mini window got wrong on their first evening

Everything in here came from twenty minutes of you actually using 0.64.0 and 0.65.0.

### The overlay opened on the wrong monitor

Reported: *"It's displaying on the primary monitor even when Elite is not on the primary."*

Two faults, not one. It asked which screen Windows calls **primary** rather than which screen the
**game** is on — and it asked **once**, at startup, so even the right answer would have gone stale
the moment you moved Elite.

It now reads Elite's own window rectangle and picks the monitor that is on, **every time the strip
appears**. Move the game to another screen and the strip goes with it.

**Unless you have placed it yourself**, in which case your corner wins and nothing moves it again.
A default may follow the game around; a choice may not.

### "Show the overlay" always read off

Reported: *"'Show the overlay' is always toggled off ... even when it is visible, it still shows as
being toggled off."*

Quite right, and the overlay was working the whole time — the switch was the thing lying. The
settings surface compares a row's value against the lowercase word `true`, and this row answered
`True`. The write side parses either spelling, which is exactly why nothing looked broken except
the one control you had just touched.

**Every toggle in the app is now checked against its own setting, both ways round, through the real
drawn switch.** Spelling it correctly was a convention every other capability happened to follow
with nothing enforcing it.

The row also says out loud that the overlay appears only while Elite is in front, so turning it on
at the desk and seeing nothing reads as **on** rather than as broken.

### The mini window has a button now

Reported: *"in the mini window there does need to be a physical UI control to switch it between
modes."*

There was a deliberate decision behind its absence — the way back must not live in the thing that
disappears, so it was the hotkey, the spoken phrase and the title bar. All three still work. But
that argument was that a drawn control must not be the **only** way out, and it got read one step
too far into there not being one at all.

**Expand** now sits in the mini panel's bottom corner, and **Shrink** in the same place in the full
window. It is on every page mini has — including the story, where the status line is not — and it
stays put while a chooser is open, because a chooser is exactly the state you can feel stuck in.

The mini window is that much taller to hold it, measured rather than typed, so the button is not
standing on the transcript.

### While you are there: the overlay does follow the window's tab

Asked: *"How do I get it to show a different tab?"*

It already tracks the main window — **for the two tabs it has**, which are the transcript and the
story. Switch the window to Checklist or Routing and the window moves alone rather than the strip
blanking. Which reading of the transcript you are on is shared in both directions, always.

If you want more of the panel on the strip than that, say so — it is one call per tab, and the
reason it is two is a judgement about what is readable at 512 pixels rather than anything
structural.

---

## 0.65.0 — 2026-08-24 — The window goes mini too

The headset has had a mini panel for months. Now the window does: the transcript's last few lines,
the ask box, and the line under it. The same panel showing less — not a smaller copy of it.

**Settings → Interface → *Window content*, `Ctrl+M`, or say "mini window".**

This is for the very ordinary case the overlay is not for: one monitor, and wanting Directive 47
out of the way without losing it. Unlike the overlay it is still the window — **you can still type
into it**, which is the difference between a mini window worth having and one you switch off the
same day.

### The way back is not on the panel

Mini takes the tab strip, the reading control, the breadcrumb, the search box, the banners and the
header, all of it on purpose. A window with no way out of it is a window you close with Task
Manager, and that is the only way a feature like this is ever remembered. So there are three ways
back and none of them is drawn on the panel:

```text
Ctrl+M           the key, which works with nothing at all on the surface
"full window"    said out loud
the title bar    mini keeps its decorations, so ✕ still closes it
```

The title bar is a decision, not an oversight. A chromeless strip pinned over the game is the
[overlay](https://dseelinger.github.io/d47/capabilities/interface.html#overlay) and a different
animal; keeping the frame here means the window can still be moved, resized and closed by the means
you already know.

### It says "window", never "panel"

*Mini panel* and *full panel* belong to the headset and always did. Someone wearing one must not
shrink a window they cannot see, and someone at a desk must not resize a quad they are not wearing.

```text
mini window / small window / little window / shrink the window
full window / big window / large window
```

### Going mini and back lands where it started

**This is the part that would have gone wrong quietly.** Directive 47 remembers where you leave the
window by watching it resize — so a mini toggle looks exactly like you choosing a 512-pixel window,
and the way back would have arrived 512 pixels wide. Permanently, and across a restart.

The mini window now has a rectangle of its own. Full to mini to full lands on the pixel it started
on, twice running, with a restart in the middle — and a mini window you widened stays widened.

The first time you go mini it appears at the corner the full window was at, rather than jumping
across the desk.

### It picks a page mini actually has

Mini can show the transcript, and the story where the surface has one. Switch to mini while you are
on Settings and it moves to the transcript, rather than drawing a settings page — whose layout wants
700 pixels — into 512 with no tab strip to leave by. Switching back puts Settings up again.

**This was already broken in the headset**, where it can be reached by setting the panel to mini on
the wrong tab, and the fix went in where both surfaces get it rather than only where the desktop
would have found it.

### Size

Mini is the headset's 512 by 280 plus whatever the ask box actually needs, at whatever zoom you are
on — measured rather than a number typed in. So mini at 150% is a bigger mini window rather than a
clipped one.

---

## 0.64.0 — 2026-08-24 — The mini panel, without a headset

You asked for a 2D overlay that does what the VR mini panel does. Here it is: a small strip
pinned over the game with the transcript's last few lines on it, and the story if one is running.

**Off out of the box.** Settings → Interface → *Show the overlay*, or `Ctrl+Alt+O` from anywhere.

### It is the same panel, not a copy of it

The strip is the mini panel — the same view, the same model, the same reduced set of things — put
on a monitor instead of on a quad a metre away. Nothing is kept in step between the three surfaces
because there is nothing to keep in step. It cannot show you something stale.

It follows the window the way the headset already does: switch tabs and the strip goes there too,
as long as it has that tab. It has two — the transcript and the story — and the other six move the
window and leave the strip where it was rather than blanking it.

### You cannot click it, and that is the point

A click the overlay ate would be a click Elite did not get. So the pointer goes **straight
through** it, it never takes the foreground, and it is not something to Alt-Tab into. Nothing it
shows can cost you a moment in a fight.

Which leaves one thing that has to be explicit: where it sits. `Ctrl+Alt+M` hands it the pointer
just long enough to drag it — a border comes up round it so you can see it has hold — and it hands
the pointer back the moment you let go. It comes up for this even with the game not running, so you
can set it up before you launch.

Where you put it is remembered, and it is not a setting: a screen coordinate is not something you
typed, so it goes beside the main window's position rather than into `settings.json`.

### It appears when the game does

A strip pinned over your browser is a strip you turn off within a day. This one comes up when
Elite has the foreground and goes away when anything else does — including Directive 47's own
window, which is right there showing strictly more.

**No interlock with the headset.** If you are wearing one you have no use for this, but wanting
both is real — a second monitor somebody else is watching — so nothing here quietly declines to
appear because SteamVR is running.

### The one way this fails without saying so

A window pinned on top draws over a **borderless** or **windowed** game and is **simply not there**
over an **exclusive-fullscreen** one. No error, no log line, nothing to diagnose. You would turn it
on, see nothing, and have no way to find out why.

That is the shape of failure this project has paid for twice — the headset overlay that ran with
sound and no picture, and the microphone whose silent default was indistinguishable from not
hearing — so a small feature earned a check:

> Elite is set to exclusive full screen. Nothing can draw over that — the overlay will be
> invisible while the game has the screen. Set Elite's display mode to borderless in its graphics
> options.

Directive 47 reads Elite's `DisplaySettings.xml` and never writes it. If the file is missing,
hand-edited, or written by a mod, it says it could not tell **and draws the overlay anyway** — your
game configuration is yours, and Directive 47 is a guest in it.

### Size and opacity

*Overlay size* is the same ladder as Zoom, starting at 512 by 280 — the size the headset's mini
panel is fixed at. It re-wraps at each step rather than being blown up, so bigger is more readable
and not blurrier. In the headset how big the panel looks is the pixel count and the quad's width in
metres together; on a monitor there is no width in metres, so size is the whole of the lever.

*Overlay opacity* is how much cockpit shows through, and it does not go below 0.2 — an overlay at
zero is one that is switched on, invisible, and indistinguishable from broken.

### Closing the window still quits Directive 47

There is no tray icon, so closing the window is how you quit — and that has been true only by
accident. Directive 47 shut down when its **last** window closed, and it had exactly one. The
overlay is a second, so without this change closing the panel with the overlay on would have left
Directive 47 running with nothing on screen to close.

It now quits when the main window closes, which is what it always meant.

---

## 0.63.0 — 2026-08-24 — Directive 47 speaks the language the game speaks

Three things you asked for, none of them a defect.

### "Roll" is a word for a mechanic Frontier removed

A roll was a throw of the dice: you applied materials and got a result somewhere in a band.
Engineering has not worked that way for years — you progress a **grade** by applying materials a
known number of times — and Directive 47 was still fluent in a dialect you do not speak, in about
twenty-five places.

It says **craft** now, which is Frontier's own word: the journal writes `EngineerCraft` every time
you do it, and the button in the workshop says Craft. *"Three crafts to go"*, *"crafted by Selene
Jean"*, *"the grade is part crafted"*.

**It still hears "roll", and always will.** Taking a word out of what Directive 47 listens for
would break something that works today, so "craft" was added beside it and nothing was removed.

Six places kept the old word because they were never about engineering: an axis of rotation, two
audio pre-rolls, Elite rolling over a log file, the captions' rolling three-line window, and the
two on-foot lines that say *"bought outright, not rolled"* — which draw the contrast deliberately.

### Asking what a material is for names the blueprint

*"What is Conductive Polymers for?"* used to get an apology. Directive 47 said it could not tell
which blueprint ate them, because a shortfall is netted across every plan at once.

**It knew more than it was letting on.** It could already name the ship and the slot; only the
blueprint was missing, and it was one field away the whole time.

```
Conductive Polymers: 14 short (6 of 20).
  Bad Idea (Python) · MainEngines · Dirty Drive Tuning 5 — 8
```

**The full shortfall list looks exactly as it did**, deliberately: one material can be wanted by a
dozen slots, and a blueprint hung off each would double a line you hear read aloud as often as you
see it. Ask about one material and there is room.

### The window's tab carries to the mini panel

Switch the window to a tab, and the mini panel goes there too — **as long as the mini panel has
that tab.** Settings is desktop-only, so asking for Settings moves the window and leaves the mini
panel where it was rather than blanking it.

The view within the tab carries as well, so changing which transcript you are reading carries
without changing tabs at all.

**One-way, and that is on purpose.** The mini panel can be moved on its own and **keeps where you
put it** until the window next moves. Somebody in a headset can put their panel on something and
have it stay there.

### Under the hood: the tool surface had two bytes left

Saying "craft" instead of "roll" costs a byte a time, and the tools Directive 47 advertises to a
language model are budgeted to 40,000. The sweep pushed one of them to **40,004**, at which point
the budget quietly drops your action tools to make room — the honk, the lights, the landing gear —
and nothing says so.

Trimming one description that had come to say "craft" twice in a sentence bought it back. It now
sits at 39,998.

**The guard that should have caught this could not.** It measured the surface *after* the budget
had already trimmed it, and a trimmed surface is under the limit by definition. It now measures
what was actually asked for.

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
