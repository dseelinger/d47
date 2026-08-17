# Phase 21 — HOTAS switches

The plan of record for list.md Phase 21. Written 2026-08-16, **after**
[hotas-switch-read.md](../spikes/hotas-switch-read.md), which answered the read-path question and
closed off two designs that had already been written down.

`list.md` reads top to bottom as a description of the product. This is the order the work happens
in, and the reasoning the order cannot carry on its own.

---

## What the spike had already decided

Three things arrived settled, and each removes a choice rather than adding one.

1. **`Windows.Gaming.Input.RawGameController` is the read path**, from a plain desktop process with
   no driver, no window and no elevation. It needs the SDK projections, so `D47.App` moves to a
   versioned target framework — the one cost the spike left unmeasured, measured below.
2. **Learn-by-flip is forced, not chosen.** Every device on the bench reports `HID-compliant game
   controller`, so a per-device profile table cannot be *authored* — there is no key a human could
   write one against.
3. **A mapping is keyed on `NonRoamableId`.** VID+PID survives a 4x32 mode change that renumbers
   every button underneath it; the non-roamable id does not, which is what makes a stale mapping
   fail closed instead of pressing button 15 of a block that is no longer the same block.

## The one thing the spike could not decide, and this plan does

The spike observed that every maintained switch on the bench held **exactly one button at all
times, including at centre**. list.md already refuses to let that become a constant. This plan
makes the refusal structural rather than remembered: a captured position is `int?`, and
**no-button-held is a position with a name** (`nothing held`) rather than the absence of one. There
is no branch anywhere that treats zero held buttons as "unchanged" — the reconciler cannot express
that idea, because the type it reads does not have it.

## The second thing this plan settles: which actions a switch may drive

**Only the ten actions Elite reports the state of.** `GameAction.Reports` already carries the status
flag for each, and `AlreadyIn` already answers *is it there already* — the exact question item 3 is
written around. An action with no reported flag cannot be reconciled, only pressed, and a switch
that presses is the edge-triggered remapper this phase exists to replace. So the assignable set is
`GameActions.All.Where(a => a.Reports is not null)`: landing gear, lights, cargo scoop, hardpoints,
flight assist, silent running, analysis mode, SRV turret, SRV handbrake, SRV drive assist.

That is not a restriction the phase pays for — it is the list a HOTAS switch panel is for.

## The order, and why

**1. `Read the Commander's controllers`** — leads, because nothing below can be tested against a
real stick until it exists, and because it is the item that carries the framework change. The
reader waits for the device count to *settle* rather than to become non-empty (spike finding 1, the
one most likely to become a shipped bug) and stays subscribed afterwards.

**2. `Assign a switch by walking its positions`** — the capture. It is second because everything
after it consumes what it produces, and because the walk is the part with no shortcut: it is where
the number of positions, the button index at each, and the maintained/spring-return classification
are all discovered rather than assumed.

**3. `A switch position means a state, not a press`** — the reconciler. Third because it is the
payoff and needs both of the above.

**4. `A mapping that no longer fits its device says so`** — folded into 3's data path rather than
built after it. It is one lookup that fails, and the honest failure is cheaper to build in than to
retrofit; the row that reports it is the same row 2 writes.

**5. `Stop reconciling a switch that something else is driving`** — after 3, because it watches 3's
own output. It cannot be built earlier and would not be believed if it were.

**6. `Show which switches disagree with the game`** — last, and it is a projection of 3's state
rather than new state. It goes in the status row, beside the microphone indicator, because that is
the one region both surfaces show in **both** modes — a banner would be invisible in the headset,
which is where a Commander who cannot see their own switches actually is.

---

## Decisions this plan makes that list.md does not

### A pause is runtime, not stored

Item 5 pauses a switch that something else is driving. That pause lives in memory and is gone at
the next start. A stored pause would outlive the leftover binding that caused it and would need its
own way to be cleared — a setting nobody knows exists, silently not reconciling a switch that has
worked for a month. Re-detecting costs one flip and says so out loud, which is the same price the
Commander already pays for a stale switch under item 6.

### Contest detection ignores a mode change

The literal rule — *a reconcile followed immediately by an unexplained change back* — has one false
positive that is not hypothetical: hardpoints retract by themselves on entering supercruise. That
is a `ControlContext` change, and the game changing its own mind during one is explained. So the
window is discarded when the context moved inside it. Everything else inside the window counts.

### A position may mean nothing

A three-position switch used as two states needs a centre that does nothing. An unassigned position
reconciles nothing and is not a disagreement, and it is the default a captured position starts in.

### The capture declines two different rushed walks differently

Both are *a candidate button that was never held at a stop*, and they are not the same problem:

| What was seen | What it was | What is said |
|---|---|---|
| pressed, released, back to the **previous** position | spring-return or a push button | it goes home on its own, so it cannot mean a state |
| pressed, released, now a **different** position | the Commander walked past without pausing | pause at each position and walk it again |

Told apart by what is held after the release, which is the only signal that separates them —
duration cannot, and finding 5 is why.

---

## Measured here, because the spike left it open

The spike's *Not answered here* listed the publish-size cost of moving `D47.App` to a versioned
target framework. Measured against the same tree with and without the change, `dotnet publish
-c Release` with the settings the csproj already carries:

| | `d47.exe` |
|---|---|
| `net10.0-windows` | 64.0 MB |
| `net10.0-windows10.0.26100.0` | 70.4 MB |
| **cost** | **+6.4 MB, +10%** |

That is `Microsoft.Windows.SDK.NET.dll` and `WinRT.Runtime.dll` entering the single-file bundle,
compressed. Accepted: it is the only read path that needs no driver, no window and no elevation,
and the alternatives are the vJoy/ViGEmBus row architecture.md §10 already rejected.

`D47.App.Tests` moves with it, because a project cannot reference one targeting a platform
version above its own.

### And a licence the gate could not see

`Microsoft.Windows.SDK.NET.Ref` arrives as a **framework reference**, so it lands in
`downloadDependencies` rather than in `libraries` — and `PackageLicenceGateTests` walks
`libraries`. It ships `Microsoft.Windows.SDK.NET.dll` and `WinRT.Runtime.dll` into the exe, and
its nuspec declares **no SPDX expression**: a `licenseUrl` pointing at the Windows SDK terms and
`requireLicenseAcceptance` set.

Not copyleft, and it is Microsoft's own grant for code that runs on Windows — the same terms that
already govern every Windows API d47 calls. But it is a redistributable binary in the shipped
executable that the gate did not examine and would not have named, which is precisely the shape
the invariant warns about. It is **a maintainer's call, recorded rather than made silently**, and
the blind spot itself is now written into the gate's own documentation.

## And one thing the spike could not have found

The projection is a managed assembly plus a COM activation, and it reaches a published build
through the single-file bundle rather than through `bin\`. That is a third layout the automated
tests cannot tell apart — the same shape as the bug that shipped a d47 whose transcriber had
never once worked. So `--selftest` gained a third check, and it is the cheapest of the three:
activate `Windows.Gaming.Input`, and fail only if the projection itself could not be reached. A
machine with nothing plugged in still passes, which is why `HotasControllers.Fault` is separate
from `Unavailable` — one string for both cannot say which happened.

Run against the published build, it passes and enumerates the bench hardware: `VID 0x4098 PID
0xBD65` four times over and `PID 0xBEA1` once. It also reproduced finding 1 in the wild — the
fifth interface arrived *after* the first read.
