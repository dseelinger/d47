# Open bugs

Defects only. Feature and polish work lives in [list.md](list.md).

An item leaves this file when it ships, and its record from then on is the line it gets
in `CHANGELOG.md` under the release that fixed it. There is deliberately no
`fixed-bugs.md`: a second copy of that history is one nobody reads and one that rots.

Each entry states what was seen, what was verified in the code, and what is still only a
hypothesis. **A lead is not a diagnosis** — reproduce before fixing, and per the standing
rule, reintroduce the fault afterwards and watch the new test fail.

---

## Four open, one fixed and awaiting its release, and one partly confirmed.

The four that were here shipped in 0.16.2, and the log-routing one in 0.21.1. Their record is
those sections of the changelog.

The VR grab that 0.16.2 recorded as "fixed but not confirmed" was not fixed. The two flags it
called are the wrong road entirely — they opt the quad in to SteamVR's own laser, which only runs
over SteamVR's dashboard, so the events they unlock never arrive while Elite holds the headset.
0.22.1 replaced the whole channel; see its changelog section.

**Partly confirmed.** The trigger does arrive and the panel can be carried — reported from the
headset against 0.22.1. Two faults it then showed, flicker under a live carry and a lock that did
not follow the grab, shipped in 0.22.2.

## Open: the aim ray does not follow the hand

Reported against 0.22.1: the ray appears where the controller was when it first showed and does not
move with it. Not diagnosed. Ruled out so far — the transform is not being suppressed by the
"nothing is pushed unless it changed" guard, whose tolerance is a tenth of a millimetre, and the
pose call itself is the same one four working implementations under `C:/dev` use.

**A lead is not a diagnosis.** `spike/GrabSpike` prints each controller's live position and how far
it has strayed from where it was first seen; a range that stays at zero while the controllers are
being waved is the fault, and anything else means the freeze is downstream of the pose.

**Run 2026-08-20, and it ruled the pose out.** `--poses` against the headset: the held controller
moved on 837 of ~1,350 frames, over a 0.22 m range, and the ray's landing point on the panel
tracked it continuously — `0.94,0.17` → `0.13,0.06` → `0.65,0.99`. The other controller was on the
desk and the run ends with head and both hands frozen because the headset was taken off; both are
explained and neither is a fault. **The Commander confirms the ray followed the hand in the spike.**

That kills the recorded hypothesis. The spike drives the *real* `SteamVrRuntime`, `VrRay`,
`VrActionInput` and the real beam and cursor quads — so the pose read, the ray arithmetic and
`AimBeam` are all correct, and the fault is in **how the app drives them**, which is a far smaller
search than the one this entry used to describe.

**The difference between the two, and the new lead.** The spike updates the beam from its own tight
loop, roughly every 11 ms. `VrHost` updates it from `Carry()`, which runs inside `Serve` on the
**10 Hz tick**, dispatched to the **UI thread** through a `Dispatcher.UIThread.Post` that
deliberately *coalesces* — `_pending` drops a frame rather than queueing it when the previous post
has not run. So the app's ray is at best nine times slower than the one that visibly works, and at
worst it stops entirely for as long as the UI thread is busy. **A ray that "appears where the
controller was when it first showed" is what a Commander sees when those posts stop arriving.**

Worth knowing beside it: until 0.39.1 the Utilities tab rebuilt every timer row on that same UI
thread ten times a second (remediation.md 17, item 14). That is the kind of load this lead
predicts would freeze the ray, and it is now gone — so **the first thing to do is look again on
0.39.1** before changing anything, and say which tab was showing when it froze.

**Still not a diagnosis.** What would settle it: whether the beam moves at all when it looks frozen
— a 10 Hz ray is choppy and a stopped one is not — and whether it recovers when the desktop window
is idle.

## Fixed, awaiting release: the headless-session cleanup failure

*Diagnosed and fixed 2026-08-21, after ten recorded occurrences across five months and five more
reproduced on the diagnosis day. The fix is merged to `main`; this entry leaves the file
with the release that carries it, and the history it compresses is in this file's git log and in
[docs/plans/flake-hunt.md](docs/plans/flake-hunt.md).*

**The mechanism, verified against the shipped Avalonia 12.1.1 binaries and the tagged sources.**
`Avalonia.Headless` runs every `[AvaloniaFact]` at per-test isolation: `EnsureIsolatedApplication`
calls `Dispatcher.ResetBeforeUnitTests()` — which nulls the process-global `s_uiThread` — and then
`SetupUnsafe()`. `Dispatcher.UIThread` is created lazily, its constructor captures whatever thread
is running it, and the first construction after a reset wins the global slot (`s_uiThread ??= this`).
So **the first thread to read `Dispatcher.UIThread` after a reset becomes the UI thread**. If any
background thread reads it — even a bare `CheckAccess()` — in the window between the reset and the
session thread's own first read inside `SetupUnsafe`, that thread hijacks the UI thread's identity,
and the session thread's `DefaultRenderLoop.Add` → `VerifyAccess` throws: the recorded stack, as a
*cleanup* failure on whichever test was being stood up. The next test's own reset wipes the poison,
which is why every failing run lost exactly one test. The carrier was always arbitrary, and the
change under release never mattered.

**The leak, named.** The only paths in the assembled system that read the static dispatcher from a
foreign thread are Avalonia's `WeakEvents.ThreadSafePropertyChanged` handler — the subscription
every XAML binding takes on an INPC view model, run on whatever thread raises `PropertyChanged` —
and app code touching the static directly. The suite had exactly one live background raiser:
`TheLogPageSaysItIsWorkingTests.TheGlyphIsUpForTheDrawAndNotOnlyForTheRead` releases its
deliberately-held log read as its last line and exits, and the freed threadpool thread then ran
`RefreshLog` — `LogText = read()` inside the `Task.Run` — raising `PropertyChanged` into the shown
panel's eleven bindings, off-thread, a few milliseconds after its test had ended.

**The evidence, in the order it was gathered.**

- Reproduced on demand: 4 failures in 24 vanilla Release runs of the App suite (~1 in 6 that day).
- In **all five** locally caught failures, the victim test began 13–44 ms after
  `TheGlyphIsUpForTheDrawAndNotOnlyForTheRead` ended — five different victims, one predecessor,
  read from the trx timestamps.
- The instrumentation the third occurrence asked for was finally written
  (`tests/D47.App.Tests/FlakeInstrumentation.cs`, armed only when `D47_FLAKE_LOG` names a file),
  and the fifth catch carried it: at the moment of the throw the session thread was id 41 and
  `Dispatcher.UIThread` was owned by **id 4, an anonymous threadpool worker**. The hijack,
  observed rather than inferred.
- With the fix in and the fault reintroduced by hand, the new regression test failed
  deterministically, naming the property: *"raised off the drawing thread: LogText"*. Restored,
  it passes.
- 40 consecutive Release runs of the whole App suite with the fix: **zero failures**, and the
  instrumentation — still armed — recorded not one `VerifyAccess` violation. The odds of 40 clean
  runs with the fault still present were under 0.1%.

**The fix.** `PanelViewModel.RefreshLog` is split: `ReadLog()` is the file work with no property
set, safe on any thread; `ShowLog(text)` is the property set, UI thread like every other setter
there; and `PanelView.ReadLogAsync` reads on the worker, then tells the page after the await, on
the drawing thread. A read a test abandons is now structurally inert — its continuation posts into
that test's dead dispatcher instance and is dropped, and nothing in the tail can touch the global.
Guarded by `TheLogPageSaysItIsWorkingTests.TheReadTellsThePageOnTheThreadThatDraws`, which fails
on the exact reintroduced fault with no flake-looping required.

**Upstream, and the pattern to keep out of the tree.** The enabling behaviour — the global
dispatcher silently rebinding to any thread that reads it mid-reset — is Avalonia's, reported as
[AvaloniaUI/Avalonia#22021](https://github.com/AvaloniaUI/Avalonia/issues/22021); nothing shipped
or committed upstream addresses it, so the local fix is the fix, and the hazard class remains:
**any** `PropertyChanged` raised off-thread on a bound view model, and any direct
`Dispatcher.UIThread` touch from a worker, can re-arm this. One production copy of the shape
existed, untested and therefore inert: `LogbookWindow`'s background estimate ended in a
`Dispatcher.UIThread.Post` from its worker — and its `Changed` subscription read the global from
whatever thread the book raised on, which the worker did. Both were reshaped the same day on the
same branch: the worker computes, the window is told after the await, and `OnChanged` posts
through the window's own dispatcher instance, which a leaked test copy can only ever post into a
dead queue.

## Open: the audition pair's five-second timeouts are a separate fault

Split out of the entry above, because the diagnosis disproved "treat them as one investigation":
`PlayingASecondVoiceCancelsTheFirst` (three appearances, once alongside
`TheGlyphBecomesStopWhileItIsTalkingAndStopsWhenPressed`) times out awaiting a cancellation, and
that is **not** the dispatcher hijack. A test that dies this way leaves an un-cancelled token and
an infinite delay that nothing will ever complete — a pure leak that *cannot* later touch the
dispatcher, so it cannot cause the cleanup failure. Occurrence 9 in the old record — both timeouts,
no cleanup failure in the run — was already evidence of independence.

**The recorded "stale detached button" lead is dead**, three ways, from the code as written:
pressing a glyph does not rebuild the row — `PickerChoice.Playing` raises INPC on the same object,
and rows rebuild only when filter text changes, which these tests never type; `Glyph()` walks
`GetVisualDescendants()`, which cannot return a detached control — a vanished button would make
`.First()` throw, a different failure; and a detached button still raises `Click` into its own
handler, so "Click into nothing" has no mechanism.

**The surviving lead is threadpool starvation.** The second press's cancellation callbacks run on
a threadpool work item (`CancelAsync`), the wait is five seconds, and the suite loads the pool
while the session runs: `EchoCancellationTests` spins three near-100% CPU threads for about a
second as a plain `[Fact]`, in parallel with the Avalonia session. All three appearances were on
busy CI runners. Unproven — a lead is not a diagnosis. What would settle it: a wall-clock trace
around the second press on a loaded runner, or starving the pool deliberately and watching the
same timeout arrive on demand.

## Open: an engineer was offered as a material trader, with rates attached

Reported 2026-08-20 from the running app. Recorded as scratch — **not diagnosed, not fixed.**

What the Commander heard, across two turns:

> You were 12 Conductive Polymers short. Lei Cheung is here, at Trader's Rest, and can do 52
> items on your list.
>
> *"What do we need the Conductive Polymers for?"*
>
> Conductive Polymers are a grade 4 manufactured material. You hold 3 of 150. Your plans call for
> 15 of them, so you are 12 short. I can't tell you from here which single blueprint eats them —
> the shortfall is netted across every live plan at once, and there are a great many. They come
> from transport ship salvage, surface points of interest, or mission rewards. **Lei Cheung, here
> at Trader's Rest, would trade for them: 2 Core Dynamics Composites or 2 Proto Heat Radiators
> buys one, or 1 Biotech Conductor buys three.**

**Lei Cheung is not a material trader.** The Commander says he is not a Manufactured Materials
Trader, and the table agrees on something stronger: `Engineers.tsv:35` has him as an *engineer* —
Laksak, Trader's Rest, Sensors/Shield Generator/Surface Scanner grade 5, unlocked with 200 Gold.
He is not a trader of any category. The app named a person who cannot do the thing it offered.

**The lead, and it is only that.** The two halves of that sentence look like they come from
different places and were fused by the model, not by code:

- *"Lei Cheung is here, at Trader's Rest, and can do 52 items on your list"* is grounded —
  `EngineerAtHand` says exactly this shape, off `Engineers.tsv`.
- The rates are grounded too, but in a different subject: `EngineeringRules.TradeRate` and
  `PlanGap` compute what *a* material trader would charge to cover a shortfall, and `PlanGap.cs:41`
  says outright that it is "what a trader could cover it with" — an anonymous one. Nothing in that
  path names a station or a person.

So the suspicion is that both facts arrived in one context, the only name present was the
engineer's, and the model attached the rates to him. If that is right, the defect is **not** in the
rates and **not** in the engineer callout; it is that the gap analysis hands over a trade offer with
no owner, in the same breath as a named person who is not that owner.

**What would settle it.** Read the actual turn: whether the trade rates reached the model through
the gap tool with no trader named, and whether the engineer-at-hand callout was in the same window.
The installed build's logs are the place to look, not a re-run — this is reproducible from the
record rather than from the game.

**Two adjacent claims from the same turn, unverified, worth checking while there:**

- "can do 52 items on your list" — whether 52 is real or also invented.
- "2 Core Dynamics Composites or 2 Proto Heat Radiators buys one, or 1 Biotech Conductor buys
  three" — whether those are what `TradeRate` actually returns for grade 4 from grade 4 and
  grade 5, or the model's arithmetic on top of them.

**Not a defect, but noted from the same turn:** "I can't tell you from here which single blueprint
eats them — the shortfall is netted across every live plan at once." That is honest and correct
about what the tool returns, and it is also a capability the Commander asked for and did not get.
If it is worth having, it is a `list.md` item, not this file.

## Open: two Commanders share one ship id, in the core bindings and in the builds

Found by inspection on 2026-08-21, while scoping `list.md` Phase 44. **Verified in the code and
not yet observed in a running app** — which is the honest state of it, and the preamble's rule
applies: reproduce before fixing.

**Elite's `ShipID` is per Commander and starts small.** Two stores key on it and carry no
Commander at all:

- `ShipCoreStore.cs:25` — `ShipCoreBinding(int ShipId, string Core, …)`, looked up by
  `For(int shipId)` at `ShipCoreStore.cs:107`, which is `Bindings.FirstOrDefault(b => b.ShipId
  == shipId)` and nothing else.
- `ShipBuild.cs:162` — `ShipBuild(string Id, string Hull, int? ShipId, …)`, looked up by
  `ShipBuildStore.ForShip(int)` at `ShipBuildStore.cs:88`, and matched on `ShipId` again in
  `ShipPlanService.cs:125`, `:147`, `:152` and `:486`.

So one Commander's ship 7 and another's ship 7 are the same row. A core bound under one answers
for the other, and `ShipDriftWatch` compares one Commander's build against the other's actual
ship and asks about a drift that is not real.

**The load-time guard makes it louder than a wrong answer.** Both files enforce one-per-ship
where they are *read*, deliberately, because a hand edit is the route that would otherwise
produce two — `ShipCoreStore.cs:274` refuses with *"ship 7 is bound twice, and a ship has one
core"* and `ShipBuildStore.cs:237` with *"ship 7 already has a build, and a ship has one"*. Both
sentences are true of a ship and false of two ships that share an id. The second Commander is
therefore not merely given the wrong answer; **they cannot record their own** while the first
Commander's entry exists, and the refusal names a rule that is not the one being broken.

**The precedent was followed everywhere else.** `ChecklistScope.Ship(int)` is ship-id keyed too,
but `ChecklistDocument` carries `CommanderFid`, so a checklist is per Commander *and* per ship.
`SamplingStore`, `MemoryStore`, `GoalStore`, `HabitStore` and `LoreStore` all key per Commander
with the key inside the document. These two stores are the ones that missed it. The identity is
available at the point of use: `GameStateStore` has kept a bucket per Frontier id since Phase 2.

**There is a live half, and keying the stores does not reach it.** Two objects hold the ship they
have already acted on as a bare `int?` — `ShipCoreService._aboard` (`ShipCoreService.cs:57`) and
`ShipDriftWatch._aboard`. Two Commanders both sitting in ship 7 read as *no change*, so the first
Commander's core stays aboard even once the stores tell them apart. `ShipCoreService._started`
has the mirror-image problem: left set across a switch, the second Commander's first ship reads
as a swap and spends a gap reaction — a model round trip — on a boarding that never happened.

**What would settle it.** Two Commanders on one installation, each flying a ship whose id the
other also owns; bind a core under the first and read `data/`'s bindings file and the second
Commander's panel. Ship ids are small and dense, so two Commanders each flying their main is
enough — this does not need contrivance.

**Fix and phase.** The keying is a defect and can ship on its own, ahead of `list.md` Phase 44,
which assumes throughout that these two stores tell Commanders apart. The live half wants the
switch signal Phase 44 describes and is better done there.
