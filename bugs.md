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
being waved is the fault, and anything else means the freeze is downstream of the pose. That run
has not happened yet.

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
