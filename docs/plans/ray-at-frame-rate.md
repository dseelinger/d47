# The aim ray at frame rate

The plan of record for [#19](https://github.com/dseelinger/d47/issues/19), written 2026-08-24 after
the Commander confirmed the symptom from the headset against 0.61.0:

> the ray is choppy, which tracks with 10 updates a second — on the **Transcript** tab.

This is not a phase. It is one defect with a known mechanism and a fix that is a small design
rather than a small patch, which is why it is written down before it is written.

---

## The finding, and why the tab is the load-bearing part

`bugs.md` offered two mechanisms for a ray that does not follow the hand, and they wanted telling
apart: the ray runs at 10 Hz, **and** the posts that drive it are dropped when the UI thread is
busy. The original report — *"appears where the controller was when it first showed and does not
move"* — is the second one. Steppiness is the first.

**Transcript is a quiet tab, and that separates them.** Nothing was starving the drawing thread and
the ray was still visibly stepped. So the everyday symptom is not contention. It is the plain rate,
and it is there all the time on every tab.

Confirmed in the source rather than inferred. `VrHost.OnTick` coalesces onto the UI thread:

```csharp
// Coalesced. At 10 Hz a dispatcher that fell behind would otherwise accumulate a queue
// of frames it will draw late and then draw all at once.
if (Interlocked.Exchange(ref _pending, 1) == 1)
{
    return;
}
```

and `Serve` calls `Carry()`, which is what moves the beam. So the beam is told where the hand is
**ten times a second at most**, and fewer whenever a frame is dropped. `spike/GrabSpike` drives the
same `SteamVrRuntime`, `VrRay`, `VrActionInput` and the same beam and cursor quads from its own
loop at roughly 11 ms, and the Commander confirms the ray follows the hand there. Same code, nine
times the rate, opposite verdict. **That comparison is the measurement and nothing else needs
taking.**

**The freeze is a separate matter and is not what this plan fixes.** It is not currently
reproducing, most likely because the load it needed is gone — until 0.39.1 the Utilities tab
rebuilt every timer row on the UI thread ten times a second. A symptom that stops appearing has not
been explained, so #19 keeps it as something to watch. If this plan lands and the freeze returns,
the freeze was never the rate.

## What is actually wrong

**A pointer is not content.** The panel's text, the clocks and the compose animation are all
correctly served at 10 Hz, and the tick deliberately marks the surface dirty only when something
moved — a transcript nobody scrolled does not get redrawn, which is the whole of list.md Phase 24.
Ten hertz is the right rate for furniture.

An aim ray is the one thing on the surface that has to keep up with a hand. Ten updates a second is
about where motion stops reading as motion and starts reading as a fault, which is exactly how it
was reported.

**So the lever is not the tick rate.** Raising the one clock would drag the journal poll, the key
sampling and the whole world-reading loop up with it, and none of those want to run faster —
`TickDriver`'s own comment says why it is a dedicated thread doing *a small amount of work each
time*. The lever is which clock the ray is on.

## The shape

Two loops instead of one.

| | rate | owns |
|---|---|---|
| **fast** | frame rate | the controller pose read, the ray arithmetic, and the position of the beam and cursor quads |
| **slow** (today's tick, unchanged) | 10 Hz | panel content, clocks, adventures, the journal, the lifecycle, and **every decision** — clicks, grips, carries, back |

**The fact that makes this cheap: the beam and the cursor are not part of the widget tree.**
`SteamVrRuntime.Beam` and `.Cursor` are `VrOverlay` — SteamVR quads placed by `AimBeam` and
`ShowCursor`. Moving them never touches Avalonia, the dispatcher or the UI thread, so the fast loop
has no business with the drawing thread at all and the headless-dispatcher rule does not reach it.

**And `Guide` is already the seam.** It reads `hands`, `found`, `resting`, `extent`, `head` and
`_carrying`, and it positions two quads. It decides nothing and writes no interaction state. The
split runs straight through the call that already exists.

## The rule that keeps this safe

**Only the drawing moves. Every decision stays on the 10 Hz thread.**

`Carry()` does two jobs in one method — it places the ray *and* runs the trigger, grip, back and
carry logic, which has state (`_carrying`, the press record, the panel's own mode). Moving the
decisions as well would put that state on two threads for the sake of latency nobody has
complained about. So the carve is: `Guide` and the pose read go fast; everything else in `Carry`
stays exactly where it is and keeps reading its own sample.

The cost is that the drawn beam can be a few milliseconds ahead of the sample a click resolves
against. At these rates that is invisible, and it buys single-threaded interaction state, which is
worth far more.

## What has to be got right

Five things, each of which is a way this goes wrong quietly.

**1. One owner for the pose read.** `Controllers()` calls `Note()`, which mutates
`_controllersSeen` — a plain `Dictionary`. Two threads calling `Controllers()` is a corrupted
dictionary and, being a race, one that will not show up in a test run. The fast loop takes
ownership of the read and publishes its last sample; the slow loop consumes that sample rather
than calling in itself.

**2. `Note()` is #18's instrumentation and must keep working.** It logs only on *change*, so a
higher rate does not flood the log — it makes the transition timestamps eleven milliseconds
accurate instead of a hundred, which is a small gift to the controller-standby investigation
rather than a cost. What it does do is call `GetTrackedDeviceActivityLevel` per device per frame;
if that measures badly, the activity read drops to a slower cadence and the connected/valid flags
stay per-frame.

**3. `Controllers()` allocates per call.** It news a `TrackedDevicePose_t[k_unMaxTrackedDeviceCount]`
— 64 entries — every time. Ten times a second that is nothing; ninety times a second in a process
sharing a GPU with Elite it is worth not doing. The buffer becomes a field.

**4. The head pose.** `AimBeam` takes `head` and uses its position to orient the beam. `Head` is a
property the slow `Serve` sets, so the fast loop reading it gets a value up to 100 ms old — and a
stale head while the head is turning misplaces the beam in exactly the way this plan exists to fix.
Either the fast loop reads the head in the same call it reads the hands, or this is measured and
written down as acceptable. **It should not be inherited by accident.**

**5. `_carrying` is read across threads.** One field, read by `Guide`, written by the slow loop. A
one-frame-stale read places the beam by last frame's carry state, which is harmless; an unpublished
write is not. Make the sharing explicit rather than relying on it being one field.

## Clicks stay at 10 Hz, and that is a decision rather than an oversight

A press is resolved on the slow loop, so it can wait up to 100 ms. That has never been reported and
is not what this plan is about — but it is now a *choice*, and the reason it is defensible is that
a click is an event and a ray is a motion. The eye tracks motion continuously and cannot see 100 ms
on a discrete event it caused itself.

If the press ever does read as laggy, it is a second plan and it is a harder one, because that is
where the interaction state lives.

## The items

1. **A second loop, on the `TickDriver` pattern.** A dedicated thread with a `PeriodicTimer`, not a
   pool timer — `TickDriver` already argues why, and a starved pool presenting as a stuttering ray
   would be the same undiagnosable symptom by another road. Started and stopped with the VR
   lifecycle rather than with the app, because there is nothing to point at when no headset is
   running.
2. **The pose read moves to it, with a reused buffer and one owner**, publishing a sample the slow
   loop reads instead of calling `Controllers()` itself.
3. **`Guide` moves to it**, with the head-pose question of item 4 above answered explicitly.
4. **`Carry` keeps everything else**, and gains a comment saying why the split is where it is —
   because the next person to read it will otherwise assume the ray logic was left behind by
   accident.
5. **The rate is settled by measurement, not by 90.** Whatever the loop runs at, the cost against a
   running Elite is what decides it, and the number chosen gets written down with what it cost.

## Accepted when

- **From the headset**: waving the controller produces a beam that reads as continuous rather than
  stepped. This is the acceptance. There is no test that can stand in for it, and the report that
  opened this was a Commander's eye.
- The 10 Hz serve is untouched for everything that is not the pointer — no clock, transcript or
  adventure redraw runs faster as a side effect, and the dirty-marking rule still holds.
- `spike/GrabSpike` and the app agree. That comparison identified this, so it is the one that
  closes it.
- Nothing calls `Controllers()` from two threads, and that is asserted rather than reviewed.
- The panel carry is decided either way — moved to the fast loop or deliberately left slow, with
  the reason written down.
- The existing VR suite still passes: `VrPointerTests`, `TheVrPanelIsClickableTests`,
  `VrSurfaceTests`, `VrOverlayCaptureTests`, `MinimiseSafetyTests`.

## What this does not do

It does not fix the freeze, which is not reproducing and is not the same fault. It does not touch
the tick rate, the journal poll or anything a Commander can see on the panel. It does not make
clicks faster. And it adds no capability, no settings row and no tool — this is a defect fix, so it
is a **patch release** and not a minor.
