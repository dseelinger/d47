# Remediation 17

Being recorded from 2026-08-19 against **v0.39.0**. Each item is checked off as it ships, and
**checked only once it has been seen to work** — a change that compiles is not a fixed item.

**Remediation 16 is finished and its record has moved.** All six items shipped in
[v0.38.2](CHANGELOG.md), and the line each got there is its permanent record. This file is the
current batch and not a growing archive, which is why 16 is gone from it.

**This batch is still open for items.** What is here is the starting set; more will be added as
they are reported.

## Where this batch came from

**The batch opens with everything standing in [bugs.md](bugs.md)** — two open defects, both of
which have now outlived several batches without being scheduled into one. That is the reason to
start here: neither is going to be fixed by being written down again, and one of them has now
cost a release run.

They differ in what is missing. The aim ray has **no diagnosis and a named experiment that has
not been run**; the headless-session failure has three occurrences, a settled shape and **no
reproduction off the CI runner**. In both cases the first job is not a fix.

**bugs.md stays the record until each ships.** Scheduling an item into a batch does not move it
out of `bugs.md` — an entry leaves that file when it ships and its permanent record is the line
it gets in `CHANGELOG.md`. So the entries below say what is to be done and point at `bugs.md`
for the diagnosis rather than copying it, because a second copy of that reasoning is one nobody
reads and one that rots.

## The original asks

Recorded in the Commander's own words, as remediation 15 established. **Where an item and an ask
disagree, the ask wins.**

| # | The ask, as made | Item |
|---|---|---|
| 1 | The Settings left nav menu is too long and needs a scrollbar. | 3 |
| 2 | LLM is unaware of what is said during callouts (or something)? — with the transcript: *"Commander. Elvira Martuuk is one stop away."* / *"Why would I care about that?"* / *"Care about what, Commander? I have no record of what I said before this. A gap — the conversation resumes and I am mid-sentence with no sentence."* | 4 |
| 3 | Clicking on the Checklist tab and then on the Loadout tab prevents navigating to ships. Clicking on a ship does nothing. | 5 |
| 4 | "Shields are down." — no need to announce this on a ship without shields. | 6 |
| 5 | Ship module list still says "not seen" even after switching to the ship in Elite Dangerous and going into the outfitting menu in Elite for that ship. | 7 |
| 6 | Module drag and copy is not working. It might be because "Click" already has something connected to it. In that case, we can use the "copy item" convention in Windows of CTRL+Left Click. | 8 |
| 7 | Oxen, Military 2 Optional Internal, "Keep the 5D Hull…" → shows the wrong engineering options (Ammo Capacity was one listed). But selecting "5D Hull Reinforcement Package" does show the correct engineering options. | 9 |
| 8 | "Gear Glyph" should appear to the right of the Module Name, not leftmost. | 10 |
| 9 | It is not clear that the step controls for the engineering grade are associated with it. Put Grade last (rightmost) on the line, followed by the stepper. | 11 |
| 10 | *"You have dropped short of where you were going and had to come back — 14 of 490 approaches, 2 of them in the last month."* This was announced on a perfect approach to landing on a planet. | 12 |

## What runs through this batch

**A. A lead is not a diagnosis, and neither of these has been reproduced.** Both opening items
are blocked on the same thing: an observation nobody has made yet. Item 1 has a spike written
that has never been run against a headset; item 2 has failed three times on a runner and zero
times anywhere it can be watched. Per the standing rule, the fix that lands has to be preceded
by a failing test or a watched reproduction, and afterwards the fault gets reintroduced and the
new test watched to fail.

**B. Subscribe in the constructor, unsubscribe on detach — for the third time.** Item 5 is the
same fault remediation 11 fixed in `ChecklistPage` and remediation 13 fixed in `LoadoutPage`,
in the last class in the panel that still had it. It is worth naming as a thread because of how
it presents: nothing throws, nothing logs, the control is on screen and looks right, and it has
simply stopped hearing. **A subscription made anywhere but on attach is a defect waiting for a
reparent**, and the panel now reparents on every tab switch.

## The items

- [ ] **1. The VR aim ray does not follow the hand.** Reported from the headset against 0.22.1:
  the ray appears where the controller was when it first showed, and stays there while the
  controller moves.

  The channel around it works — 0.22.1 replaced the whole grab path, and the two faults that
  showed up next, flicker under a live carry and a lock that did not follow the grab, shipped in
  0.22.2. The trigger arrives and the panel can be carried. It is the ray alone that is frozen.

  **Ruled out so far**: the "nothing is pushed unless it changed" guard is not suppressing the
  transform — its tolerance is a tenth of a millimetre — and the pose call is the same one four
  working implementations under `C:/dev` use.

  **First job is the spike, not a fix.** `spike/GrabSpike` prints each controller's live position
  and how far it has strayed from where it was first seen. A range that stays at zero while the
  controllers are being waved is the fault; anything else puts the freeze downstream of the pose
  and the search starts somewhere else entirely. That run needs a headset and has not happened.

  **Attempted 2026-08-19, and the spike is now its own obstacle.** The run captured the
  controllers and did not give them back — the Commander could not get into VR at all, and the
  capture outlived the process, so nothing was observed and the headset was unusable until
  SteamVR was restarted. **It took them while the Commander was still in Virtual Desktop**, which
  is the part that makes it a defect rather than an inconvenience: the spike grabs the controllers
  the moment it starts rather than when somebody is ready to be watched, so it takes them out from
  under whatever environment the Commander is actually standing in — before they have reached
  SteamVR, let alone the thing being diagnosed. **So the first job is now the spike, not the ray**: it registers an
  action set and leaves it registered, and a diagnostic that costs a SteamVR restart to run is
  one nobody will run twice. It needs to release what it took on the way out, and to say in its
  own output how to stop it. Item 1 stays open, and this is why it is not in 0.39.1.

- [ ] **2. A headless test session fails late, and the test that reports it is arbitrary.** Three
  occurrences now, the third on 2026-08-19 during the **v0.39.0 release run**, which is the first
  time this has cost anything beyond a retry.

  Three different tests have carried it — `AuditionDoesNotCommitTests.PlayingASecondVoiceCancels-`
  `TheFirst` (a five-second timeout), then `RowWidthTests` and `PickerShowsEverythingTests` (both
  as *cleanup* failures with `InvalidOperationException: The calling thread cannot access this
  object because a different thread owns it`, thrown inside `AvaloniaTestRunner` rather than
  inside the test body). They have nothing in common beyond running late in one headless session.

  **The third occurrence settled the shape**, and it is worth stating because it redirects the
  search: the throw landed inside `HeadlessUnitTestSession.EnsureIsolatedApplication` →
  `AvaloniaHeadlessPlatform.Initialize` → `DefaultRenderLoop.Add`. The failing call is the
  headless platform being **stood up** on a thread that does not own the dispatcher. A session
  being re-initialised at all, that late in a run, is already the anomaly — `EnsureIsolated-`
  `Application` should have nothing left to do by then. So the suspect is the session, and the
  named test is a bystander.

  **Treat the two symptoms as one investigation.** The audition timeout is still the best
  candidate for what puts the session in a bad state, since what it leaves behind when it goes
  wrong is an infinite delay awaiting a token nobody cancelled. Its own untested hypothesis is a
  stale control lookup: pressing a row's glyph makes that row rebuild, and the test fetches the
  second row's glyph after that rebuild is queued but perhaps before it has run — a button
  detached from the visual tree raises its Click into nothing, which is exactly the observed
  symptom and exactly the shape a slower runner would expose.

  **It does not reproduce off the runner.** Three consecutive local Release runs of the whole App
  suite are clean, `ci` went green on the identical commit minutes before the failing release
  run, and every occurrence cleared on a re-run. 586 of 589 App tests passed in the failing run.
  Reproducing it is the first job; a fix landed without a failing test to watch is a fix nobody
  can check.

  **A fourth occurrence, 2026-08-19, on this batch's own release run.** `VrSurfaceTests.`
  `EachModeReadsItsOwnPlacementSlot` failed exactly as the third did — cleanup framing, the same
  `InvalidOperationException`, and the same frame: `HeadlessUnitTestSession.EnsureIsolated-`
  `Application` → `AvaloniaHeadlessPlatform.Initialize` → `Compositor..ctor` →
  `DefaultRenderLoop.Add`. **Four different tests have now carried it**, which closes the question
  of whether the named test means anything: it does not.

  It has now cost **two** release runs — 0.39.0's and 0.39.1's — and both cleared on a re-run of
  the identical commit. Three consecutive local Release runs of the whole suite were clean before
  this one was pushed, which is the fourth time that has been true and the fourth time it has not
  predicted the runner.

  **The compositor frame is the thing to read next.** `EnsureIsolatedApplication` is building a
  *whole new* `Compositor` mid-run, and `DefaultRenderLoop.Add` then asserts dispatcher ownership
  from whichever thread xUnit happened to schedule the cleanup on. So the question is not why the
  thread is wrong — it is why a session that has already been initialised is being initialised
  again at all, that late in a run.

  **Twice patched already, and both patches are still correct.** `670997f` closed a dispatcher
  race that had failed a release build, `5066025` closed a second one a seventh test project
  exposed. Both were about the *first* press being genuinely underway before the second arrives.
  This is a third window, later in the same sequence, and neither existing fix should be
  disturbed by whatever closes it.

- [x] **3. The Settings nav is longer than the window and cannot be scrolled.** Reported against
  0.39.0: the left-hand nav menu runs off the bottom of the page, and the entries past the bottom
  edge cannot be reached at all.

  `SettingsView.axaml` puts the entries in a bare `StackPanel` — `NavItems`, docked under the
  heading block inside a `DockPanel`, with no `ScrollViewer` anywhere in that column. The card
  column beside it has one (`Scroller`); the nav never needed one when it was written and was
  never given one as sections accumulated. There are now more than twenty, built one per
  capability that declares any settings rows, and the main window opens at 820×640.

  So this is growth, not a regression: nothing changed except the number of sections, and the
  layout has no answer for the count exceeding the height.

  **A scrollbar alone does not finish it, and that is the part worth stating.** The nav is a
  scroll-spy index — it highlights whichever card is topmost in the column beside it. Give it its
  own scroller and the highlight will move to an entry that is off-screen, which leaves the nav
  exactly as useless as it is now for anything below the fold, with the added confusion that
  nothing appears to be selected. **The active entry has to be brought into view** when the
  selection changes, which is `SetActiveSection` doing one thing more than it does today.

  **And bringing it into view is not the same as scrolling to it.** The Commander scrolling the
  nav by hand, and the nav following the cards, are the same scroller being driven by two
  parties; a nav that yanks itself back on every card that passes is worse than one that clips.
  Move it only when the active entry is not already visible.

- [x] **4. Nothing d47 says of its own accord reaches the model, so it cannot be asked about.**
  Reported against 0.39.0 with the transcript: a route callout said *"Elvira Martuuk is one stop
  away"*, the Commander asked *"Why would I care about that?"*, and d47 answered *"Care about
  what, Commander? I have no record of what I said before this."*

  **Justified, and the transcript is the exact predicted output.** `TurnLoop` writes to the
  conversation history in exactly two places — `_history.AddRange(pending)` and the assistant
  reply beside it — and both sit inside the `turnOutcome == TurnOutcome.Answered` branch of a
  model turn. Every other thing d47 says goes to the synthesiser and to the panel and stops
  there. `AppHost.SayAsync` raises `Said`, whose only subscriber appends to `PanelViewModel` —
  the readable page, a `StringBuilder`, a different object from the list the model is sent. The
  panel and the prompt are two transcripts, and only one of them is the conversation.

  So it is not the route callout. It is **every** callout, plus the Phase 31 continuity line, the
  Phase 32 habit remark, ambient lines, and reminders.

  **Autonomous speech is worse and belongs in the same fix.** `CarryOutPendingActions` calls
  `Voice.AnnounceAsync` directly rather than going through `SayAsync`, so it never raises `Said`
  either: an action d47 took on its own is missing from the model's history *and* from the
  Commander's own conversation page, and exists only in the log.

  **The set that may enter history is already computed, which is the good news.**
  `Announcement.ConversationLine` is `Transcript is null && Voice == VoiceRole.ShipAi ? Text :
  null` — d47's own words, and nothing else. That is not a convenience: `IncomingMessages` says
  outright that every string in it was written by somebody else and that there is deliberately no
  path from there into a prompt, because architecture.md §7 names in-game comms as the source
  whose attacker is *any player in range*. A blanket "append every announcement" would launder
  hostile text into the assistant's own voice, which is the most trusted position in the
  transcript. **The existing discriminator is the gate**, and it already has tests.

  **Cache-safe, and worth saying so the objection is not raised twice.** History is position 7,
  *below* the breakpoint, so appending to it invalidates no prefix. The cost is tokens, not a
  cold 39 KB of tool schema.

  **Two things to decide, and neither is obvious.**

  *How a spontaneous line is represented.* An assistant message with no user message before it is
  not a shape every provider accepts, and Phase 29 means d47 talks to more than one. Consecutive
  assistant entries may need merging, or the line may need to arrive as a labelled note in the
  following user turn. Whichever it is, it has to be decided at the provider seam and not left to
  whichever endpoint is selected.

  *What it costs over an evening.* The transcript is **unbounded** — nothing in `TurnLoop` or
  `PersonaHost` trims it — and ambient lines fire on a timer. Six hours of flying would put
  hundreds of assistant lines into a history that is re-sent every turn. Either ambient is
  excluded, or this batch is where the transcript gets a bound; growth that was tolerable when
  only answered turns accumulated is not obviously tolerable when idle chatter does.

  **A related silence, same shape, flagged rather than assumed into scope.** The keyword router's
  own answers do not reach history either — `SettingCommand`, `ActionCommand`, `KeywordRouter`
  and `NoCapability` each return without appending. So *"stop calling things out"* → *"done"* →
  *"why did you do that?"* reproduces the reported transcript by a second road. It is the same
  defect and probably the same fix; say whether it is in.

  **And a third thing this explains.** `FlavourTurn`, which re-voices a callout in the persona's
  words, builds a prompt whose history is a single message and throws it away. So d47 is not only
  unable to be asked what it said — it composes each spontaneous line knowing nothing about the
  last one.

  **Nothing pins the current behaviour in either direction.** No test asserts that callouts do or
  do not enter history, so nothing fails when this changes. Per the standing rule, the test comes
  first and the fault gets reintroduced afterwards to watch it fail.

- [x] **5. A tab that has been left once goes deaf, so clicking a ship does nothing.** Reported
  against 0.39.0: Checklist, then Loadout, and the fleet no longer opens — a ship can be clicked
  and nothing happens.

  **Diagnosed, and it is the third occurrence of a fault this repo has already fixed twice.**
  `DrillView` subscribes to the navigator **in its constructor** and unsubscribes on detach
  (`DrillView.cs:94` against `DrillView.cs:135`), which is not a pair. Switching tab reparents
  `PagePane.Child`, which detaches the outgoing page and drops its subscription for the rest of
  the session. Coming back re-attaches and calls `Draw()` once, so the fleet index looks
  perfectly correct — and then a click calls `Nav.Drill`, which changes the trail and raises
  `Changed` to a handler nobody is listening with. The pane never redraws. **Nothing is broken
  about the click; the page has stopped hearing.**

  The same fault, in the same words, is already documented at `ChecklistPage.cs:268` (*"it
  detached, unsubscribed, and was deaf for the rest of the session"*, remediation 11 item 3) and
  at `LoadoutPages.cs:398` (*"Attach to detach, and catch up on the way in"*, remediation 13 item
  1). `EngineersPages.cs:145` has it right too. `DrillView` is the last class in
  `src/D47.App/Panel/` still using the unpaired form, which is why this is the fix shape rather
  than a new invention: subscribe on attach, unsubscribe on detach, keep the catch-up `Draw()`.

  **It is wider than the report.** Everything that reaches the pane through the navigator dies
  with it on that tab — the Ships/Suits/Gap dropdown (`Nav.SelectRoot`), the breadcrumbs
  (`Nav.JumpTo`), Back, pressing the selected tab to return to its root, and the Checklist tab's
  own Suggestions drill. The breadcrumb row still updates, because it is drawn by `PanelView`
  rather than by the page, so the trail visibly moves while the content does not follow it.

  **Two decisive checks before the fix.** Resizing the window across the 380 px pane boundary
  forces `ArrangeOverride` → `Draw()`, so a "lost" ship page should appear at that moment; and
  by this mechanism a genuinely cold Checklist → Loadout works, because Loadout has to have been
  on screen once to have been detached. If a cold first visit really does fail, there is a second
  mechanism nobody has found yet — worth confirming with the Commander which order it was.

  **No test covers a tab round-trip.** `LoadoutTabTests` and `ChecklistTabTests` each select
  their tab once and drill. The regression test is Loadout → Checklist → Loadout → click a ship
  → assert the ship page is showing, and per the standing rule it gets watched failing first.

- [x] **6. "Shields are down" on a ship that has no shields.** Reported against 0.39.0. A hull
  without a shield generator — mining, exploration and racing builds routinely fly one — reports
  the shields-up flag as false forever, which is not a transition into danger. It is the ship.

  `DangerCallout` announces on the edge, and the edge is against a field that starts optimistic:
  `_shieldsWereUp` is initialised `true` (`DangerCallout.cs:31`) and the announcement fires the
  first time a known, in-ship status says otherwise (`DangerCallout.cs:147`). Boarding an
  unshielded ship after flying a shielded one crosses that edge on the swap, with nothing
  dangerous having happened. The journal branch beside it (`DangerCallout.cs:102`) is a second
  road to the same line if Elite writes `ShieldState` on boarding.

  **The data to answer with is already in hand and is not being asked.** `CalloutContext.State`
  carries `ShipLoadout.Modules`, so whether a shield generator is fitted is a fact d47 holds —
  every generator, Bi-Weave and Prismatic included, is an `int_shieldgenerator_*` symbol. The
  callout simply never looks.

  **It must fail towards warning, and that is the whole design of the item.** A stale, unknown
  or not-yet-read loadout has to keep the warning, because a missed *real* shields-down call is
  far worse than one spurious line — the suppression applies only where a loadout is known and
  positively contains no generator. Anything less certain says it.

  **Verify against the corpus rather than by reasoning.** 915 journals are local and
  `spike/CorpusReplay` runs the lot through Core in seconds. The question to put to them is
  whether Elite writes `ShieldState` at all for a ship whose `Loadout` has no generator, and what
  a shipyard swap onto one looks like in sequence. That decides whether the fix is one branch or
  two, and it is the first job.

- [x] **7. A ship's slots read "not seen" while the Commander is sitting in it.** Reported against
  0.39.0: switched to the ship in Elite, opened outfitting, and the module list still says *not
  seen*.

  `Vacant` says "empty" only when the live loadout's `ShipId` equals the build's, and "not seen"
  otherwise (`ShipsMode.cs:388`). The same join is made in four places — `Modules`, `Flying`,
  `FittedIn`, `KnownEmpty` — and it is the whole of what d47 knows about a ship's contents:
  **there is no per-ship module store.** One `ShipLoadout` exists per Commander, always the last
  `Loadout` event seen, and `ships.json` persists plans only.

  **Three candidate mechanisms, and they are distinguishable.** This one is not diagnosed, and
  the report does not on its own separate them.

  *No refresh path.* `ShipsMode.Changed` forwards `ShipBuildStore.Changed`, which is raised only
  when the plans file is saved or reloaded — journal changes never raise it. The Engineers tab
  has a game-state tick (`PanelView.TickEngineers`); the Loadout tab has nothing equivalent. So a
  page left open while the Commander swaps ships keeps its first answer indefinitely. If the page
  was open across the swap and never left, this alone is the whole bug.

  *A build with no `ShipId`.* `ShipPlanService.Intend` creates a plan with `ShipId: null`, and the
  only automatic binding to a real ship is `Observe`, which reacts to `ShipyardNew` alone —
  `ShipyardBuy` and `ShipyardSwap` bind nothing. Such a build reads "not seen" forever, on every
  refresh, while the Commander sits in the ship. **Cheap to tell apart**: its details block says
  the "only known while you are in it" line rather than *You are flying it*.

  *A stale loadout after a swap.* `ShipLoadout.Apply` handles `Loadout` and `SetUserShipName` and
  ignores everything else, so between a `ShipyardSwap` and the next `Loadout` the live record
  still describes the previous ship — id included. Whether that window is real depends on whether
  Elite writes `Loadout` on the swap, which is a corpus question and not a reasoning one.

  **First job is telling them apart, not fixing.** Ask the Commander whether the page was already
  open, and whether the details block said *You are flying it*; then put the swap sequence to the
  915 journals. The three fixes are different — a state tick, a binding on `ShipyardSwap`, and
  invalidating the loadout on a swap — and shipping the wrong one leaves the report standing.

  **No test asserts "not seen" at all**, and none covers a `Loadout` arriving after a page is
  built. The existing fixtures apply `Loadout` before the panel opens, so the join is satisfied
  at first draw and the failing order is the one nothing exercises.

- [ ] **8. Ctrl-drag between slots copies nothing.** Reported against 0.39.0, with the suggestion
  that Click may already be taking the press and that Ctrl+Left is the Windows convention for
  copy.

  **The convention asked for is what is already implemented**, which is the useful part of the
  report: `LoadoutPage.Draggable` requires the left button *and* `KeyModifiers.Control` on
  `PointerPressed`, and both handlers are registered on the tunnel (`LoadoutPages.cs:719-772`).
  So the ask is satisfied by the design and the defect is that the design does not work — a
  different fix from adding a modifier.

  **Not diagnosed.** The press handler sets `args.Handled` in the tunnel, which is intended to
  keep the row's `Click` from firing on a copy; what it also does is decide whether Avalonia's
  `Button` ever runs its own press handling, and a `Button` that handles a press **captures the
  pointer**. If it captures, the release is delivered to the row the drag started on rather than
  the row under the cursor, `from == slot`, and the handler returns having done nothing — which
  is exactly the reported symptom. If it does not capture, the release should land on the target
  row and the copy should work. The two possibilities are opposite conclusions about the same
  line, and neither has been observed.

  **The gesture has no test at all, which is how it shipped broken.** `SlotCopyTests` covers
  `SlotCopy` in Core — the rules, deliberately without a window, and they are not what failed.
  Nothing raises a press, a move and a release over two rows. The reproduction is the first job,
  and whatever closes it needs a test at the pointer level rather than at the rule level.

  **Watch the hover feedback while reproducing.** A target row dims to 0.4 when it would refuse
  the plan, so whether the destination row reacts at all separates "the drag is not in flight" from
  "the drop is going to the wrong row". That is a free diagnostic already in the code.

- [x] **9. "Keep what is fitted" offers every blueprint in the game.** Reported against 0.39.0:
  Oxen, Military 02, the *Keep the 5D Hull Reinforcement Package* row — and the blueprint list
  included Ammo Capacity, which no hull reinforcement has ever taken. Choosing the module by name
  instead gives the right list.

  **Diagnosed, and it is one line.** The keep option is built with an **empty** value:
  `Keeping` returns `new ChoiceOption(string.Empty, $"Keep the {named} — I only want the
  engineering")` (`ShipsMode.cs:1033-1040`). `AskBlueprint` then does `var wanted = module ??
  plan?.Module` (`ShipsMode.cs:789`) — and `??` does not fall back on an empty string, only on
  null. So `wanted` is `""`, `Offered` bails at `module is not { Length: > 0 }`, returns null, and
  the null branch is the documented "d47 does not know this module's type" fallback that offers
  **every modification blueprint in the game** (`ShipsMode.cs:806-809`).

  So the row that names the module in its own label is precisely the row that throws the name
  away. **The option that says *keep the 5D Hull Reinforcement Package* is the one path that does
  not tell the chooser it is a hull reinforcement.**

  **The fix is to answer from what is fitted, which d47 already has**: `Keeping` only offers the
  row at all when `FittedIn` returned a module, so the symbol is in hand at the moment the label
  is written. Either the option carries it, or `AskBlueprint` re-reads it — and the empty-string
  hole gets closed rather than papered over, because `module ?? plan?.Module` treating `""` as a
  decision also means a keep silently discards a module the plan already named.

  **This is remediation 15 item 6's other half.** That item split "takes no engineering" from
  "d47 does not know the type", so a fuel tank is not asked about at all. What it did not catch
  is a third case reaching the same fallback: a module d47 knows perfectly well, whose name was
  dropped on the way to the question. Same wrong output — *a Type-10's armour offered Dirty Drive
  Tuning* is the line already in that comment — by a different road.

- [x] **10. The gear glyph sits left of the module name.** Reported against 0.39.0: it should be
  to the right of the name, not leftmost.

  `LoadoutPages.Row` builds a three-column grid — marks, name, note — and puts the marks in
  column 0 (`LoadoutPages.cs:196-236`), so both glyphs share a gutter down the left edge of every
  row.

  **"Right of the name" and "a right-hand column" are different things, and the star column makes
  the difference visible.** The name lives in the star column, so a glyph in a fourth column
  would sit against the note on the far side of the row, with a gap that grows as the window
  widens — attached to the row rather than to the name. To read as *this module is engineered*
  it has to travel with the last word of the name, which means inside the name's cell: an inline
  run, or the glyph and the name in one panel. The name wraps, so whatever is chosen has to wrap
  with it rather than push it.

  **One thing to confirm: the dot.** The ask names the gear. The dot beside it is the other mark
  — remediation 15 item 10 made them deliberately distinct, gear for *a roll has been done* and
  dot for *a plan exists* — and moving only the gear leaves a row with a mark on each side, which
  is worse than either arrangement. The reading taken here is that both move together and stay
  adjacent; say if it should be the gear alone.

- [x] **11. The grade stepper is not visibly attached to the grade.** Reported against 0.39.0:
  it is not clear the step controls belong to the engineering grade. Asked for as *Grade last
  (rightmost) on the line, followed by the stepper.*

  The plan line is one sentence — `5D Hull Reinforcement Package, grade 3 Heavy Duty, Deep
  Plating, with Selene Jean` — and `Stepped` appends ▲▼ after the whole of it
  (`LoadoutPages.cs:289-313`). So the number sits in the middle of a sentence and the buttons sit
  at the far right end of it, with a blueprint name, an experimental effect and an engineer in
  between. Nothing says they are the same fact.

  **The obstacle is that the sentence is also spoken.** `SlotPlan.Describe` is *"one line, as the
  slot index shows it and as d47 says it"* (`ShipBuild.cs:59`), and `Stepped`'s own comment gives
  that as the reason the control sits beside the text rather than inside it. Reordering it to put
  the grade last would change what d47 **says** — *"Heavy Duty, Deep Plating, with Selene Jean,
  grade 3"* — which is worse aloud, and is changing the voice as a side effect of a panel fix.
  That is the exact failure remediation 16 item 6 named and pinned with a test.

  **So the drawn line becomes a projection, as it did there.** The panel renders module,
  blueprint, effect and engineer, then the grade as its own trailing fragment against the
  stepper; `Describe()` keeps its sentence and keeps saying it. **Built from the plan's fields,
  never by editing `Describe()`'s output** — splitting that string to lift "grade 3" out of it
  would be a second parser of d47's own prose, wrong the day a blueprint name contains the word.

  **The single-grade rule has to come with it.** A blueprint offering one grade prints no grade
  and draws no stepper — three of 160 module-and-blueprint pairs, already correct at
  `ShipsMode.cs:517-525`. The trailing fragment inherits that or a Point Defence acquires a
  "Grade 1" the sentence deliberately does not have.

  **And the spoken form gets a test saying it did not move**, for the same reason item 6 has one.

- [x] **12. A habit worth 2.9% is announced as a habit.** Reported against 0.39.0: *"You have
  dropped short of where you were going and had to come back — 14 of 490 approaches, 2 of them in
  the last month"*, said **on a perfect approach to a landing**.

  **Diagnosed: there is no rate floor.** `HabitFloor` gates on three counts — 20 journals, 5
  occurrences, 10 opportunities — and `HabitEvidence.ClearsTheFloor` checks exactly those. 14 of
  490 clears all three comfortably while being a thing the Commander does **once in every
  thirty-five approaches**. Nothing anywhere asks whether the proportion is large enough to be
  about the person.

  **The design already knew.** `HabitEvidence.Opportunities` documents itself as *"the difference
  between a habit and a Tuesday: fifty submissions out of fifty-two is a thing about a person, and
  two out of two is a fortnight with two interdictions in it"* — and then the floor it feeds only
  counts the numerator. A denominator that is recorded, printed, and never tested is the whole
  defect in one line.

  **Which is also why it landed on a perfect approach.** At 2.9% almost every approach is a good
  one, so the moment the callout fires is almost always a moment when nothing is going wrong. A
  remark of the form *you do this* arriving while the Commander demonstrably is not doing it reads
  as an accusation the evidence does not support — and it will keep reading that way 97 times in
  100, which no wording can fix.

  **The fix is a floor on `Rate`, and the number is a judgement call**: too high and a real habit
  goes unsaid, too low and this ships again with a different denominator. It belongs in
  `HabitFloor` beside the other three, as a constant rather than a setting, for the reason stated
  there — a Commander who could lower it would use it to confirm something they already believed.

  **And every existing detector has to be re-measured against it**, because the floor is shared:
  whatever number goes in, the corpus can say which claims survive it. That is the check, not a
  unit test with a made-up count.

  **Measured, with the floor in.** 20% was chosen against the corpus rather than by taste, and
  the three Commanders' claims fall either side of a wide empty gap — nothing lands between 2.8%
  and 50%:

  | Claim | Rate | Spoken |
  |---|---|---|
  | *You submit rather than run* | 100.0%, 90.5% | yes |
  | Settlement security killing you on foot | 50.0% | yes |
  | Overshooting and going round again | 2.8%, 2.3%, 1.0% | no |
  | Putting the hull into something on the way in | 0.4% | no |

  So the reporting Commander now has **no spoken claim at all**, which is the right answer: of the
  five things d47 watches for, the only one it could show about them was a thing they do once in
  thirty-five approaches. `spike/HabitProbe` prints `SAID` or `kept` per claim, so the next
  detector can be held to the same line before it ships.

- [x] **13. ElevenLabs famous voices are offered, and cannot be spoken.** Reported against 0.39.0
  from the installed build's log:

  ```
  D47.Core.Audio.TtsException: ElevenLabs could not speak "We don't have to do this!":
  Famous voices can only be used within the Reader App.
  ```

  **The catalogue takes whatever the service lists.** `ElevenLabsTtsProvider.ListVoicesAsync`
  maps every entry of `GET /v1/voices` into the picker, keyed on `voice_id`, and the `ElevenVoice`
  record it deserialises into carries **`voice_id`, `name` and `labels` only**. The `category`
  field — which is how ElevenLabs distinguishes `premade` from a cloned, professional or famous
  voice — is read by `spike/ElevenLabsProbe` and thrown away by the provider. So a voice the
  account may list but the API will not speak is offered to the Commander exactly like one it will.

  **What it costs is silence.** `SpeechPipeline` logs *"could not synthesise a sentence; it will
  not be spoken"* and drops it. Nothing on the surface says the chosen voice cannot be used, so
  from the Commander's side d47 simply stops talking — the failure mode this batch has now met
  three times in different clothes.

  **`category` is not the discriminator, and the probe run is what proved it.** That was the
  obvious fix and it cannot work: the ™ voices come back **`professional`**, the same category as
  the several hundred ordinary voices listed beside them — *Burt Reynolds™*, *John Wayne™*,
  *Judy Garland™*, *Stan Lee™*, *Sir Michael Caine™*, *Richard Feynman™* all sit in the same bucket
  as *"Brian - Clean, Professional and Balanced"*. A filter on category would have silenced
  hundreds of voices the Commander can actually use and still let the famous ones through.

  **The trademark in the name is the only thing that separates them**, which the Commander named
  outright. So that is what the listing filters on — and a match on somebody else's display text
  is exactly the kind of rule this codebase distrusts, which is why it is not load-bearing alone.

  **Two halves, and the second does not depend on the name.** The listing drops a ™ voice so it is
  never offered; and `FaultFor` now reads *"Reader App"* in a refusal as `TtsFault.VoiceRejected`,
  which is the fault the pipeline already knows how to recover from — it forgets the voice, lets
  the provider choose, and tells whoever chose it. Matched on the message rather than the status,
  because the status this arrives with was never captured. So if ElevenLabs renames the convention
  tomorrow, it costs one sentence rather than a session of silence.

  **`spike/ElevenLabsProbe` gained a fifth question** that prints every field differing between a
  ™ voice and an ordinary one, and the status the refusal actually arrives with. Neither was needed
  to ship this, and both would sharpen it — the filter could key on a field instead of a character
  if one turns out to exist.

- [x] **14. The Utilities tab flickers in the headset.** Reported against 0.39.0: *"in VR, the
  Utilities tab flickers a lot. Is it because we're trying to repaint too often (clock seconds) or
  something? Other tabs are fine."*

  **The guess was right about the tick and wrong about the cost.** Writing four clock strings ten
  times a second is nothing. What `UtilitiesPage.Refresh` also did on every one of those ticks was
  `_running.Children.Clear()` and build every timer row again — new `Border`, new `DockPanel`, new
  `Button`, new bindings — so the running list was destroyed and recreated continuously.

  **Why only the headset sees it.** The window composes its frame after the tick has finished, so
  the torn-down moment never reaches the screen. The VR path rasterises the tree on **its own**
  cadence, which is not the tick's, so it lands between the clear and the re-add and sends a frame
  with an empty list on it. That is the flicker, and it is why no other tab has it: no other tab
  rebuilds itself unprompted.

  **The countdown is written, the row is not rebuilt.** A key over the running reminders' ids says
  whether the *list* changed; only then is anything constructed. Otherwise each row's countdown
  block is written in place. The comment that stood over this — *a clock that only redraws when
  something changed is a clock that stopped* — is still true and is still why the tick runs: what
  changed is a string, not a row.

  The test asserts reference identity across a tick that moves the clock eleven minutes, because
  identity is precisely what was wrong.

## Where item 8 stands

**Not ticked, and deliberately.** The gesture was found to work: a test now drives a real press
and release across two rows and the plan copies, and the row does not capture the pointer, which
is what lets the release land on the target. The Commander confirms Ctrl was held. So the reported
failure is not explained, and a fix that cannot be pointed at the report is not a fix.

What shipped is the thing that made it unreportable: **a refused drag now says why.** The release
handler used to return without a word, and the success message was overwritten by the redraw that
followed it — so neither outcome has ever said anything, and every refusal looked exactly like a
broken feature. The most likely refusal is dragging a row that has nothing *planned* in it, since
a row shows what is fitted as well, and a module is not a plan.

**The next attempt is now diagnostic.** Try the drag again against 0.39.1: either it copies, or it
says which rule turned it down. Either answer closes the item.

