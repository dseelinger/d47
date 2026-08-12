---
title: Listening
---

# Listening

**Group:** Voice
**Capability id:** `listening`

Hearing the Commander: the microphone, the push-to-talk key, and whether that key collides with
something Elite is already using.

**Status: audio capture works; transcription does not exist yet.** d47 opens the microphone,
runs it continuously, and captures exactly the stretch the Commander held the key for. Turning
that audio into words needs a speech-to-text model, which is not yet wired up — so
`get_listening_status` reports transcription as unavailable, with the reason stated, in the same
shape as every other unconfigured capability.

## Try it

> "can you hear me"
> "what microphone are you using"
> "is my push to talk key bound twice"

## Tool

### `get_listening_status`

Read-only. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

```text
Microphone: Yeti Nano, capturing.
Push-to-talk: CapsLock (hold).
Warning: CapsLock is also bound in Elite (KeyboardMouseOnly) to HeadLookToggle. One of the two
will not work, and neither will say so — pick another key for one of them.
Transcription: unavailable. No speech-to-text model is configured yet, so I capture audio but
cannot turn it into words.
```

Everything is reported together on purpose. "d47 cannot hear me" has five possible causes — no
key bound, no device, the device gone away, the key colliding with Elite, no model loaded — and
the Commander should not have to guess which one it is.

## Push-to-talk is one gate policy over a continuous stream

The microphone runs whenever d47 runs, into a small ring buffer. The gate decides which part of
that stream was speech addressed to d47. That is the checklist's own wording and it is load
bearing in two directions: continuous listening and a wake word become later *policies* over the
same buffer rather than a rewrite, and "toggle instead of hold" is already just a value rather
than a second mechanism.

It also means the key-down path awaits nothing. Opening a WASAPI capture device takes tens of
milliseconds; doing that on key-down would clip the start of every utterance on top of everything
else.

## Why the key is polled, not hooked {#push-to-talk-key}

`RegisterHotKey` — which the silence key uses — delivers `WM_HOTKEY` on **press only**. There is
no release edge, and push-to-talk is defined by its release edge. It is not a candidate.

A `WH_KEYBOARD_LL` hook has both edges and is **forbidden**: it is a global input chokepoint, so
a stall in d47 becomes a stall in the Commander's controls mid-fight (architecture.md D4, rule
1). That rule is written about injecting keys and applies at least as strongly to reading them.

Raw Input with `RIDEV_INPUTSINK` would work — both edges, event-driven, delivered to a background
window, and *not* a hook, so D4 rule 1 survives. It was rejected on privacy: `RIDEV_INPUTSINK`
delivers **every keystroke on the system** to d47's window, including passwords typed into other
applications. Receiving the Commander's entire keyboard and discarding it is a worse posture than
reading one key, for an app whose repository is public and whose selling point is that nothing
leaves the machine.

`GetAsyncKeyState`, polled from the tick loop, reads **exactly the one bound virtual-key code**,
never the keyboard. That distinction is the whole argument.

Its cost is latency: the tick samples at 10 Hz, so a key-down is seen up to 100 ms after it
happened. That is also why the tick runs at the top of architecture.md §4's 4–10 Hz band rather
than the middle — push-to-talk sets the floor, and nothing else in the loop would notice 4 Hz.

The edge is computed by comparing against the previous sample rather than trusting
`GetAsyncKeyState`'s low "pressed since last call" bit, which is shared process-wide and is
unreliable when anything else in the process also calls it.

### The pre-roll is what makes polling viable {#pre-roll}

A 100 ms detection delay would clip the front of every utterance, and the front of the utterance
is where the proper nouns are. So the gate opens **retroactively** into the ring buffer: audio
captured before the key was noticed is still part of the utterance.

500 ms by default, which covers the worst-case polling delay plus the gap between a Commander
pressing the key and starting to speak — often negative, since people start talking as they
press.

The ring never grows past its window. Ten minutes of room tone while nobody is talking costs
half a second of memory.

## Settings

### Microphone {#microphone}

Which input to listen on. Unset uses the system default.

The checklist is specific about why this row exists: a blank selection produces a silent default
and a turn reporting no speech detected, with nothing indicating why. When the chosen device is
gone, the status answer names it rather than reporting generic silence.

A missing microphone is a capability being off, not a startup failure — d47 stays fully usable
typed.

### Push-to-talk key {#push-to-talk-key-setting}

Unset by default, and unset means d47 never opens the microphone. A microphone that opens on a
key nobody chose is a microphone opening by surprise.

Unlike the silence key, this does **not** require a modifier: push-to-talk on a bare key is the
normal arrangement. That makes the collision check below matter more, not less.

Protected — reachable from the panel and the model-free keyword router, not from the tool
surface. A model that can rebind or unbind the microphone key has taken away how the Commander
talks to it.

Rebinding while the key is held forces a release first. Without that the gate stays open with
nothing able to close it, which is the listening equivalent of a stranded key.

### Key behaviour {#mode}

Hold to talk, or press once to start and again to stop. Same gate, different policy.

## Reporting a key that is bound twice

A double-bound push-to-talk key has no symptom other than not working — in one direction or the
other, depending on which application sees the key first. So the collision is stated outright, at
startup in the log and on request through the tool.

The check reads the Commander's actual Elite bindings, resolved per architecture.md D4:

- The active preset is named in `StartPreset.<major>.start`, **not assumed to be `Custom`**.
  Parsing `Custom.*.binds` on a machine running `KeyboardMouseOnly` reports the wrong keys with
  total confidence.
- Shipped presets live in the **game install directory**, not the user profile. A Commander who
  never customised their controls — the common case — has no file under `Options\Bindings\` at
  all.
- Version suffixes are compared **numerically per segment**, so `4.10` sorts above `4.2`. A
  string comparison gets that backwards and selects a file two revisions stale.

Gestures are normalised before comparing, so `Ctrl+Alt+X` from the settings file and
`LeftControl+LeftAlt+X` from the bindings file are recognised as the same key.

Not having read the binds produces silence rather than an all-clear: never having looked is not
the same as having looked and found nothing.

**Binds are read-only.** d47 never writes the Commander's bindings file.

## What is captured, and what happens to it

Nothing captured here leaves the machine or is written to disk. Audio enters the ring buffer, is
overwritten within the pre-roll window unless the key is held, and would reach a transcriber only
for the stretch the Commander actually asked for.

The device stopping — usually being unplugged mid-session — abandons anything the gate had open
rather than emitting it. Transcribing half a sentence the Commander did not finish saying is
worse than losing it.

A press shorter than 250 ms is discarded as a mis-press. Transcribing 80 ms of room tone produces
a confident wrong word, which is worse than producing nothing.

A stuck key is closed at a 60-second ceiling, and that audio is **kept** rather than discarded —
the Commander said something, and it is better transcribed late than lost.
