---
title: Listening
---

**Group:** Voice
**Capability id:** `listening`

Hearing the Commander: the microphone, the push-to-talk key, and whether that key collides with
something Elite is already using.

Hold the key, speak, release, and the words run the same turn as if they had been typed.
Transcription is Whisper, running locally on a model file the Commander chose and agreed to
download. **No audio and no transcript ever leaves the machine.**

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
Transcription: base.en loaded.
```

Everything is reported together on purpose. "D47 cannot hear me" has five possible causes — no
key bound, no device, the device gone away, the key colliding with Elite, no model loaded — and
the Commander should not have to guess which one it is.

## Push-to-talk is one gate policy over a continuous stream

The microphone runs whenever D47 runs, into a small ring buffer. The gate decides which part of
that stream was speech addressed to D47. That is the checklist's own wording and it is load
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
a stall in D47 becomes a stall in the Commander's controls mid-fight (architecture.md D4, rule
1). That rule is written about injecting keys and applies at least as strongly to reading them.

Raw Input with `RIDEV_INPUTSINK` would work — both edges, event-driven, delivered to a background
window, and *not* a hook, so D4 rule 1 survives. It was rejected on privacy: `RIDEV_INPUTSINK`
delivers **every keystroke on the system** to D47's window, including passwords typed into other
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

A missing microphone is a capability being off, not a startup failure — D47 stays fully usable
typed.

### Push-to-talk key {#push-to-talk-key-setting}

Unset by default, and unset means D47 never opens the microphone. A microphone that opens on a
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

### Speech model {#model}

Which Whisper model turns speech into words. **`none` is the default**, and a real choice: D47
captures audio and says plainly that it cannot understand it. A default that downloaded several
hundred megabytes at first launch would be exactly what the consent gate exists to prevent,
arranged by the default rather than by a bug.

The row marks which models are already on disk and what the rest would cost to fetch, because a
Commander comparing options needs to know which choices are already paid for.

### Running on the GPU {#gpu}

Off by default, and opt-in with the cost stated on the row — which the checklist asks for by
name.

In VR the GPU is already the scarce resource, so a large model running there surfaces as dropped
frames and reprojection rather than as anything that looks like a speech problem. That is the
hardest kind of setting to diagnose: the symptom appears somewhere entirely unconnected to the
cause. A short push-to-talk clip on the small English models absorbs CPU inference fine, which is
what makes CPU a sensible default rather than a compromise.

The GPU path needs the CUDA runtime present. When it is not, D47 **says so** and leaves
transcription unavailable rather than quietly falling back to the CPU — a GPU toggle that
silently does nothing is the same undiagnosable class of problem in the other direction.

## Downloading a model {#download}

**Nothing is fetched without explicit consent, and nothing is fetched at startup.** Selecting a
model that is not on disk raises a request; the panel asks; declining leaves the setting alone,
since the Commander may want it later and silently reverting their choice would be answering for
them.

The request is raised from the composition root rather than from the settings panel, so it fires
however the model came to be selected — the panel, the keyword router, or a hand-edited settings
file. A consent prompt that only one surface knows to show is a surface that can be gone around.

Before asking, D47 makes one metadata request to find out what the file actually is:

```text
Download the Base (English only) — the usual choice speech model? 141.1 MB from huggingface.co,
saved to your data folder. d47 will verify the download against the checksum the host published.
```

That size is the one the host reported, not an estimate written into D47 — a hardcoded figure is
a number D47 asserts about a file it has never seen, and it goes stale the first time the model
is republished. Where the host publishes no checksum, the prompt says so rather than claiming a
verification that is not happening.

The download is hashed as it lands and verified before the file is moved into place. A mismatch
is **discarded**, not kept: a model that does not match its published hash is either a truncated
transfer or something D47 should not load, and both answers are "do not use this file". The
write is atomic — a half-downloaded model under its real name is one that loads and then fails
mid-transcription.

The dialog defaults to **no**. The button that costs nothing is focused, so pressing Enter out of
habit declines rather than starting a large download, and dismissing the window is a decline
rather than consent.

This is disclosed as its own row in the egress report. The row reads active whenever a model is
*selected*, not only while a transfer is happening — a disclosure that lit up mid-download would
tell the Commander nothing they could act on beforehand.

## Proper nouns from the journal

Every utterance is transcribed with a list of names drawn from the journal: the current system,
the station, the body, the next jump, the ship's name and type, the carrier, the route ahead, and
the Commander's own fleet.

**Proper nouns are where speech recognition fails hardest and most silently.** A misheard system
name does not come back as an error or a low-confidence marker — it comes back as a plausible
English phrase. "Shinrarta Dezhra" becomes "shin arta desha", the turn proceeds confidently on
the wrong system, and nothing anywhere reports a problem.

The list is journal-derived and network-free, ranked by proximity to what the Commander is
doing, deduplicated, and capped. The cap matters: an overlong bias list does not fail loudly, it
silently displaces the model's own context and makes transcription worse than no biasing at all.

## What happens to what you say

Spoken input runs **the same turn as typed input**, deliberately. A second path would be a second
place for the in-flight gate, the interrupt vocabulary and the cancellation slot to be got wrong,
and "where am I" should mean the same thing however it was said.

Transcription runs on the thread pool, never on the audio thread that produced the samples —
Whisper on a CPU takes hundreds of milliseconds for a short clip, and doing that inline would
stall capture and drop the next utterance.

A transcript that is entirely a bracketed annotation — `[BLANK_AUDIO]`, `(wind blowing)` — is
treated as silence. Those are descriptions of the audio rather than things the Commander said,
and handing one to the turn loop as a question is how D47 ends up answering the sound of a fan.

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

**Binds are read-only.** D47 never writes the Commander's bindings file.

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
