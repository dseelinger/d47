# Open bugs

Defects only. Feature and polish work lives in [list.md](list.md).

An item leaves this file when it ships, and its record from then on is the line it gets
in `CHANGELOG.md` under the release that fixed it. There is deliberately no
`fixed-bugs.md`: a second copy of that history is one nobody reads and one that rots.

Each entry states what was seen, what was verified in the code, and what is still only a
hypothesis. **A lead is not a diagnosis** — reproduce before fixing, and per the standing
rule, reintroduce the fault afterwards and watch the new test fail.

---

## Two open, and one partly confirmed.

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

## Open: PlayingASecondVoiceCancelsTheFirst fails on CI for the third time

`D47.App.Tests.AuditionDoesNotCommitTests.PlayingASecondVoiceCancelsTheFirst` timed out on the CI
runner during the 0.38.0 release, at the `cancelled.Task.WaitAsync` on line 210 — the second press
did not cancel the first audition inside five seconds. It passes locally in Release, every time,
and the failing run was a green re-run away from tagging.

**Twice patched already, and the patches are the record.** `670997f` closed a dispatcher race that
had failed a release build, and `5066025` closed a second one "a seventh test project exposed". Both
fixes were about the *first* press being genuinely underway before the second arrives, and both are
still in place and still correct — this is a third window, later in the same sequence.

**A lead is not a diagnosis.** The untested hypothesis is the control lookup: pressing a row's
glyph makes that row rebuild (its glyph becomes stop while it is talking), and the test fetches the
*second* row's glyph after that rebuild is queued but perhaps before it has run. A stale button
detached from the visual tree raises its Click into nothing, and nothing then cancels — which is
exactly the observed symptom, and exactly the shape a slower runner would expose. Pumping the
dispatcher immediately before the second lookup would close it, if that is what it is.

Reproducing it is the first job and has not been done: the runner's timing is what triggers it, and
a fix landed without a failing test to watch is a fix nobody can check. Per the standing rule,
reintroduce the fault afterwards and watch the new test fail.

**And a second one, on the release build of the same version.** `RowWidthTests.TheWholeChoiceLabel-
IsOnTheTooltipWhenTheBoxClipsIt` failed as a *cleanup* failure —
`InvalidOperationException: The calling thread cannot access this object because a different thread
owns it`, thrown inside `Avalonia.Headless.XUnit.AvaloniaTestRunner` rather than inside the test.
That test has no flake history of its own and its body is synchronous, so the suspicion is leaked
state from an earlier test in the same headless session rather than anything about this one — the
audition test above being the obvious candidate, since what it leaves behind when it goes wrong is
an infinite delay awaiting a token nobody cancelled.

Both cleared on a re-run of the same commit, and 583 of 584 App tests passed in the failing runs.
Three consecutive Release runs of the whole App suite locally are clean, so nothing about this
reproduces off the runner yet. **Treat them as one investigation**: two symptoms, one session, and
a shared suspect.

**A third occurrence, 2026-08-19, and it moved again.** The release run for v0.39.0 failed on
`PickerShowsEverythingTests.EveryChoiceIsListedAndTheIdIsNotInTheBox` — same exception, same
*cleanup* framing, same `AvaloniaTestRunner` frame, and this time with the throw landing inside
`HeadlessUnitTestSession.EnsureIsolatedApplication` → `AvaloniaHeadlessPlatform.Initialize` →
`DefaultRenderLoop.Add`. So the failing call is the headless platform being **stood up** on a thread
that does not own the dispatcher, rather than anything the named test does.

That third data point is worth more than the two above put together, because it settles the part
that was still a suspicion: the test that reports the failure is **arbitrary**. Three different
tests have now carried it — `RowWidthTests`, and now this one — and none of them has anything in
common beyond running late in one headless session. The suspect is the session, and the specific
frame now names where to look: a session that is being re-initialised at all, mid-run, is already
the anomaly, since `EnsureIsolatedApplication` should have nothing left to do by then.

586 of 589 App tests passed in the failing run, `ci` had gone green on the identical commit minutes
earlier, and two consecutive local Release runs of the whole suite were clean. It cleared on a
re-run of the same tag. **This has now cost a release run**, which is the first time it has cost
anything beyond a retry, and it is the reason to stop treating it as noise.

**A fourth, on the release run for v0.39.1, hours later.** `VrSurfaceTests.EachModeReadsItsOwn-
PlacementSlot` — a fourth test, unrelated to the other three, failing as cleanup with the same
exception and the same frame, now readable in full: `HeadlessUnitTestSession.EnsureIsolated-`
`Application` → `AvaloniaHeadlessPlatform.Initialize` → `Compositor..ctor` → `ServerCompositor..ctor`
→ `DefaultRenderLoop.Add`.

**Four tests have now carried it and none of them is the subject.** That is settled and should not
be re-investigated. What the fourth adds is the middle of the stack: the session is constructing a
*whole new* `Compositor` mid-run, and `DefaultRenderLoop.Add` then asserts dispatcher ownership
from whichever thread xUnit scheduled that cleanup on. **The question is not why the thread is
wrong. It is why an already-initialised session is being initialised again at all**, that late in
a run — `EnsureIsolatedApplication` should have had nothing to do.

**It has now cost two release runs**, 0.39.0's and 0.39.1's, and cleared on a re-run of the
identical commit both times. Three clean consecutive local Release runs preceded this one, as they
did the last one; local runs have now failed to predict the runner four times, and should stop
being offered as evidence that it is fixed.
