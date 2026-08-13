# Spike: reading HOTAS switch positions

Answers the read-path question behind a proposed Phase 10 item — a **switch reconciler** that
makes a maintained toggle on a HOTAS mean an absolute state rather than a toggle press.

Spike code lives in [`spike/HotasProbe`](../../spike/HotasProbe). It is deliberately **not** in
`d47.slnx` — it is throwaway.

---

## Verdict

**`Windows.Gaming.Input.RawGameController` is the read path. No driver, no window, no
elevation.**

| Question | Result |
|---|---|
| Can a plain desktop process read HOTAS buttons at all? | ✅ yes, from a console app with no message pump and no `CoreWindow` |
| Does it need a kernel driver, vJoy, or elevation? | ✅ none of the three |
| Do the throttle base and handle enumerate as one device? | ❌ no — the throttle alone presents **four** interfaces |
| Is there a stable per-device identity to persist a mapping against? | ✅ `NonRoamableId`, paired with VID+PID |
| Can a device be identified by name? | ❌ every device reports `HID-compliant game controller` |
| Is a single enumeration at startup a device list? | ❌ **no** — the list fills in over time |
| Can maintained and spring-return switches be told apart by hold duration? | ❌ **no** — the ranges overlap completely |
| Does a stored mapping survive a 4x32 mode change? | ❌ no — **and that is the right answer**: it fails closed, see finding 7 |

Nothing copyleft enters the graph: the read path is the Windows SDK projection, already a
transitive dependency of a `net10.0-windows` target.

---

## Environment

| | |
|---|---|
| OS | Windows 11, `10.0.26200` |
| .NET SDK | 10.0.302 |
| Target | `net10.0-windows10.0.26100.0` (SDK projections; the real projects use plain `net10.0-windows`) |
| API | `Windows.Gaming.Input.RawGameController` |
| Hardware | WinWing Orion 2 throttle + base, Orion 2 Multifunction Joystick (F18 grip), Virpil rudders |
| Device software | SimApp Pro, 4x32 mode **on** for the primary capture |

---

## What the hardware presents

The same three physical products enumerate as six interfaces or three, depending on one
setting in SimApp Pro.

**4x32 on — six interfaces:**

| VID | PID | Buttons | Switches | Axes | What it is |
|---|---|---|---|---|---|
| 0x4098 | 0xBD65 | 32 | 0 | 0 | throttle, button-only block |
| 0x4098 | 0xBD65 | 32 | 0 | 0 | throttle, button-only block |
| 0x4098 | 0xBD65 | 32 | 0 | 0 | throttle, button-only block |
| 0x4098 | 0xBD65 | 32 | 0 | 7 | throttle, buttons + axes |
| 0x4098 | 0xBEA1 | 21 | 1 (`EightWay`) | 7 | F18 grip |
| — | — | — | — | — | a sixth appeared late; the Virpil, per the run below |

**4x32 off — three interfaces:**

| VID | PID | Buttons | Switches | Axes | What it is |
|---|---|---|---|---|---|
| 0x4098 | 0xBEA1 | 21 | 1 (`EightWay`) | 7 | F18 grip — *unchanged* |
| 0x4098 | 0xBD65 | **128** | 0 | 7 | throttle, one device |
| 0x3344 | 0x01FA | 0 | 0 | 3 | Virpil rudders |

The mode is exactly what it says: one 128-button throttle split into four 32-button blocks.
4 × 32 = 128. It touches the throttle only — the grip and the rudders are identical either
way.

The rudders are worth noting separately: **zero buttons, three axes, and a different vendor**
(0x3344). They never appear in a switch capture because they have nothing to capture, which is
the correct outcome reached for free rather than by special-casing.

---

## Findings

### 1. A single enumeration is not a device list

Across three runs `RawGameController.RawGameControllers` returned 3, then 5, then 6 devices.
The first version of the probe waited for the list to become *non-empty* and reported **three
of six**.

This is the finding most likely to become a shipped bug. An implementation that enumerates once
at startup will silently miss devices, and the symptom — "d47 can't see my throttle" — will
be intermittent and unreproducible on the developer's machine.

The reader must wait for the count to stop changing, **and** stay subscribed to
`RawGameControllerAdded` / `Removed` afterwards. Hot-plug is the same code path as slow
enumeration, so getting this right costs nothing extra.

### 2. Identity needs two fields, and the readable one is useless

`DisplayName` is `HID-compliant game controller` for all six devices. Windows will not tell us
which lump of metal a device is.

- **VID + PID** identifies the *product* — `0xBD65` throttle, `0xBEA1` grip.
- **`NonRoamableId`** identifies the *interface*, and is the only field separating the
  throttle's four otherwise-identical blocks.

A persisted switch mapping therefore stores both. This also settles a design question: a static
per-device profile table is not merely inadvisable, it is **impossible to author**, because the
platform exposes nothing a human could key it to. Learn-by-flip is not the convenient option,
it is the only one.

### 3. Maintained switches hold one button per position — including centre

The encoding is not "two buttons with centre meaning neither". Every maintained switch observed
holds **exactly one button at all times**:

| Switch | Buttons | Observed rest |
|---|---|---|
| two-position | `#1` 8 / 9 | one of the pair always held |
| three-position | `#1` 4 / 5 / 6 | rest at 4, walked 4→5→6→5→4 |
| three-position | `#1` 28 / 29 / 30 | rest at 29 |
| three-position | `#2` 24 / 25 / 26 | rest at 25 |

This makes the reconciler *simpler* than designed. A switch position is "which of these N
buttons is currently held" — a total function with no null state to special-case, mapping
straight to a desired state.

Button indices within one switch were consecutive in every case observed. Useful for grouping
during capture; not load-bearing, and not to be relied on.

### 4. Sixteen buttons are held with nothing touched

At rest: nine held on `#1`, one on `#2`, six on `#3`.

This is the "some button is always pressed" problem as a number, and it is why these switches
cannot usefully be bound in Elite directly.

### 5. Hold duration cannot classify a switch

The proposed capture step was to tell a maintained switch from a spring-return one by how long
its button stays down. The data closes that off:

- maintained switch flips: 407, 734, 821, 902, 1031, 1057, 1237, 1611 ms
- momentary grip buttons: 206, 220, 248, 250, **1751** ms

A deliberate press on a push button outlasts every switch flip in the capture.

**The fix is the instruction, not the algorithm.** Capture says *"flip it and leave it there"*
and samples after ~1.5 s: a maintained switch is still held, a spring-return one has already
gone home. Reliable, and one word different in the prompt.

### 6. Hats are a third input class

The grip's 8-way reports as a `GameControllerSwitchPosition` — `Center->Up`, `Up->UpLeft` —
not as buttons, and always returns to `Center`. Capture must recognise switches as distinct
from buttons and decline them, rather than reading a hat as a two-position toggle.

### 7. A mode change invalidates a mapping, and does so detectably

Diffing `NonRoamableId` either side of the 4x32 change:

| Device | 4x32 on | 4x32 off | Verdict |
|---|---|---|---|
| F18 grip (`0xBEA1`) | `{wgi/nrid/X8QoL-2>1[]-…-mA7IX7-Ac}` | **byte-identical** | stable |
| Throttle (`0xBD65`) | four ids, one per block | one id, matching **none** of them | invalidated |

So the id is stable for a device whose configuration did not change, and changes completely for
one that did. That is the best possible outcome, because it means a stored mapping **fails
closed**: after a mode change d47 cannot find the device it learned the switch on, so it reports
*reassign this switch* rather than confidently driving button 15 of a block that is no longer
the same block.

The alternative — an identity that survived the change while the button indices underneath it
shifted — would have been the dangerous result, and it is the one a naive VID+PID key would
have produced, since VID+PID is unchanged across the mode switch in both directions.

This is the guard the design needed, supplied by the platform rather than by us.

---

## What this means for the design

1. **The read path adds no dependency and no install friction.** It is the Windows SDK
   projection against a target framework the app can already reach.
2. **Learn-by-flip is forced, not chosen** — see finding 2.
3. **The reconciler's input is an index, not a boolean** — see finding 3.
4. **Capture must instruct, not infer** — see finding 5.
5. **The device reader is a long-lived subscriber, not a startup query** — see finding 1. That
   shape matters for where it sits: it is a hardware component, so it belongs in App behind an
   interface, with Core seeing only settled switch positions through a `Poll()` in the same
   shape as the journal reader.
6. **A mapping is keyed on `NonRoamableId`, never on VID+PID** — see finding 7. VID+PID survives
   a mode change that moves every button index underneath it, so keying on it would produce
   silent misfires where the `NonRoamableId` key produces an honest "reassign this switch".
7. **d47 works in either 4x32 mode and should not care which** — the Commander picks that
   setting for Elite's sake, not ours, and the reconciler's only obligation is to notice when it
   moves.

## Not answered here

- Whether the reconciler's key press actually lands in Elite. That path already exists and is
  covered by architecture.md D4; nothing in this spike touched it.
- How button indices renumber across the mode change. The mapping fails closed either way, so
  the answer is not needed — recorded only so a future reader knows it was a choice.
- Whether the Windows SDK projection changes the app's publish size. `D47.App` targets plain
  `net10.0-windows`; adopting this read path means moving it to a versioned target, and the
  effect on the ~64 MB single file is unmeasured.
