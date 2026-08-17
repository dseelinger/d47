---
title: HOTAS switches
group: Acting on the game
nav_order: 132
---

A maintained toggle on your stick or throttle that means a **state** rather than a press.

Flip the gear switch down and Directive 47 asks Elite whether the gear is already down. If it is,
nothing happens at all. If it is not, it presses your own landing-gear binding once. Between
flips it touches nothing.

That one difference is the whole feature. Every other way of getting a switch into Elite is
edge-triggered: it sends a toggle on the flip and has no idea what the game was doing. So the
first time the game changes its own mind — you dock, you relog, you lower the gear by voice —
the switch is upside down and stays that way until you notice.

## Why your switches cannot just be bound in Elite

Because a maintained switch is *held*, not pressed. On the bench here, **sixteen buttons were
held down with nothing being touched**. Elite sees a switch left in the "on" position as a button
you are leaning on forever, and there is no way to express "this position means gear down" in a
bindings file.

## Turning it on

Two rows, and the second only appears once the first is on:

- **Let D47 press keys in Elite** — the master switch for all key injection.
- **Let a HOTAS switch operate the ship** — this feature.

Both are off until you turn them on, and neither can be turned on by the AI. They are reachable
from the settings panel, from the settings hotkey, and by saying one of the fixed phrases:

> "let my switches operate the ship"
> "stop reading my switches"

Keys are only ever sent while Elite is the window in front.

## Assigning a switch

There is no list of sticks to pick from, and there never can be. Windows reports **every**
controller as `HID-compliant game controller` — a WinWing throttle, a Virpil base and a
twenty-year-old Saitek are all the same string. Nothing exists for a device profile to be keyed
to, so Directive 47 learns your switch by watching you use it.

Press **Assign** and you are asked to:

> Move the switch to each position in turn, and pause at each one.

Walk it all the way through. That instruction is doing four jobs at once:

| What the walk discovers | Why it cannot be assumed |
|---|---|
| How many positions the switch has | Two, three and four-position switches all exist |
| Which button index each position holds | Indices were consecutive on the bench; that is a hint, not a rule |
| Whether every position holds a button *at all* | A centre detent that holds nothing is a legitimate design |
| Whether the switch stays where you put it | A spring-return switch cannot mean a state |

The pause is what tells a maintained switch from a spring-return one. Hold duration cannot: the
measured ranges overlap outright — switch flips ran 407–1611 ms and deliberate button presses ran
206–1751 ms. But after a second and a half, a maintained switch is still held and a spring-return
one has gone home, and that is not a close call.

### When a walk is declined

Directive 47 declines rather than guessing, and says which of these it was:

- **"That is a hat, not a switch."** A hat returns to centre, so it can only mean a press.
- **"That control went back on its own."** Spring-return, or a push button.
- **"Button 5 moved but was never held still long enough to be a position."** You walked past a
  position. Try again, pausing at each.
- **"Buttons 8 and 9 are held together at one position."** Not a switch this can read.

Every walk — including a declined one — can be exported as a capture report. Most of the devices
this has to work on are ones nobody here will ever hold, so if your switch is declined and you
think it should not have been, that file is the whole of the evidence.

### Assigning what each position means

Each captured position gets an action and a state. Only the actions Elite **reports the state
of** can be assigned, because those are the only ones that can be asked *are you already there*:

landing gear, ship lights, cargo scoop, hardpoints, flight assist, silent running, analysis mode,
SRV turret view, SRV handbrake, SRV drive assist.

A position may also mean nothing, which is what the centre of a three-position switch usually is.

## When a mapping stops fitting

A mapping is stored against the device's `NonRoamableId` — never against its vendor and product
ids. This matters more than it sounds.

Turning **4x32 mode** on or off on a WinWing throttle renumbers every button on it, while leaving
the vendor and product ids completely unchanged. A mapping keyed on those would survive the
change and quietly press button 15 of a block that is no longer the same block. The non-roamable
id changes, so instead the switch **fails closed**: the row says it needs reassigning, and
nothing is pressed.

## When something else is driving the same switch

If Directive 47 sets a state and it immediately goes back the other way, something else is bound
to that action — a leftover Elite binding, or a vJoy device SimApp Pro or Joystick Gremlin is
publishing. That cannot be detected by looking: Elite's binds name the virtual device, and
nothing connects it back to the physical switch.

So the symptom is watched instead. One unexplained reversal and Directive 47 **stops reconciling
that switch** and says so, rather than fighting whatever else is there. The panel offers a
Resume button; a restart clears it too.

A mode change is not counted as unexplained. Hardpoints retract by themselves when you enter
supercruise, and that is the game doing its job.

## Switches that disagree with the game

A stale switch is harmless — it costs one extra flip — but only if you can see it coming. So the
panel and the VR surface show which assigned switches currently sit against the game's state:

> gear switch: the landing gear is up

This is the answer real aviation reached with annunciator lights rather than motorised switches,
for the same reason: showing the state is cheap and moving the switch is not.

You can also ask.

## Ask for it

> "which switches disagree"
> "check my switches"

### `report_switches`

Reports only. It cannot assign, change, pause or clear a switch — assignment is reachable from
the panel and from nowhere else, because the AI reads untrusted text and a hostile in-game
message must not be able to remap your throttle.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

## Where it is stored

`data/switches.json`, beside the executable like everything else Directive 47 writes. It is
hand-editable, and it is re-read while running — a mapping edited in a text editor is live
without a restart. Anything in it that does not make sense is refused **by name, with the
reason**, and the rest of the file still loads.

```json
{
  "switches": [
    {
      "name": "gear switch",
      "deviceId": "{wgi/nrid/X8QoL-2>1[]-...-mA7IX7-Ac}",
      "device": "VID 0x4098 PID 0xBD65, 32 buttons, 0 hats, 7 axes",
      "positions": [
        { "button": 8, "action": "landing_gear", "state": "on" },
        { "button": 9, "action": "landing_gear", "state": "off" }
      ]
    }
  ]
}
```

## What it does not do

- **It does not read axes.** A throttle axis is not a switch and is not in scope.
- **It does not write your Elite bindings.** Those are read-only, always.
- **It does not move your switches.** Nothing can; they are lumps of metal.
- **It does not act on a switch it has just found.** On startup, and after a device reconnects,
  Directive 47 learns where the switch is sitting and presses nothing. The next flip is the next
  question.
