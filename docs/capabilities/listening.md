---
title: Listening
---

Talking to Directive 47 instead of typing at it. Hold a key, speak, let go, and what you said is
handled exactly as if you had typed it.

**No audio and no transcript ever leaves your machine.** Speech is turned into words by a model
running on your own computer, on a file you chose and agreed to download.

## Ask for it

> "can you hear me"
> "what microphone are you using"
> "is my push to talk key bound twice"

```text
Microphone: Yeti Nano, capturing.
Push-to-talk: CapsLock (hold).
Warning: CapsLock is also bound in Elite (KeyboardMouseOnly) to HeadLookToggle. One of the two
will not work, and neither will say so — pick another key for one of them.
Transcription: base.en loaded.
```

Everything comes back together on purpose. "It cannot hear me" has five possible causes — no key
bound, no microphone, the microphone gone, the key clashing with Elite, or no speech model — and
you should not have to guess which.

## Getting set up

Three things, in this order:

**1. Bind a key.** Nothing is bound out of the box, and nothing bound means the microphone is
never opened at all. A microphone that opens on a key nobody chose is a microphone opening by
surprise.

**2. Download a speech model.** Also nothing by default. Directive 47 will capture your voice and
tell you plainly that it cannot understand it until you pick one. Choosing one asks first, and
tells you the real size and where it comes from before anything is fetched.

**3. Check the key is yours alone.** If Elite is already using it, say so is exactly what
Directive 47 does — see below.

## Your key might already be Elite's

A key bound in both places has no symptom other than not working, in one direction or the other,
depending on which application sees it first. Nothing tells you; it simply does nothing.

So Directive 47 reads your actual Elite bindings — the preset you are really using, including the
built-in ones — and says outright when your push-to-talk key is already spoken for:

```text
Warning: CapsLock is also bound in Elite (KeyboardMouseOnly) to HeadLookToggle. One of the two
will not work, and neither will say so — pick another key for one of them.
```

**Your bindings are never written to.** Directive 47 only ever reads them.

If it has not managed to read them, it says nothing rather than giving you an all-clear — never
having looked is not the same as having looked and found nothing.

## Settings

### Microphone {#microphone}

Which input to listen on. Leave it unset for whatever Windows is using.

If the one you chose disappears, the status answer names it rather than reporting generic
silence. No microphone at all is a feature being off, not a failure — Directive 47 stays fully
usable typed.

### Push-to-talk key {#push-to-talk-key}

The key you hold to talk. Unset by default.

Unlike the stop key, this one does not need a modifier — a bare key is the normal arrangement for
push-to-talk, which is exactly why the collision check above matters.

**The model cannot change this.** A model that could unbind your microphone key has taken away
how you talk to it.

### Key behaviour {#mode}

**Press to talk (PTT)** — hold it while you speak — or **Toggle on and off**, press once to
start and again to stop.

### Speech model {#model}

Which Whisper model turns your speech into words. The row marks which are already on disk and
what the others would cost to fetch, so you can see which choices are already paid for.

`none` is the default and a real choice, not an oversight. A default that downloaded several
hundred megabytes the first time you opened the app would be exactly the surprise the consent
prompt exists to prevent.

### Running on the GPU {#gpu}

Off by default.

Worth knowing before you turn it on: in VR your GPU is already the scarce thing, and a large
model running there shows up as dropped frames and reprojection rather than as anything that
looks like a speech problem — a symptom nowhere near its cause. A short push-to-talk clip on the
small English models runs fine on the CPU, which is why that is the default rather than a
compromise.

If the CUDA runtime is not installed, Directive 47 **says so** and leaves transcription
unavailable rather than quietly using the CPU. A GPU switch that silently does nothing is the
same undiagnosable problem in the other direction.

## Downloading a model {#download}

**Nothing is downloaded without you agreeing, and nothing is downloaded at startup.** Choosing a
model you do not have asks first:

```text
Download the Base (English only) — the usual choice speech model? 141.1 MB from huggingface.co,
saved to your data folder. D47 will verify the download against the checksum the host published.
```

The size is the one the host actually reported, not a figure written into Directive 47 that would
go stale the first time the model was republished. The dialog defaults to **no**, and closing it
counts as declining.

Declining leaves your choice alone — you may want it later, and silently undoing what you picked
would be answering for you. It asks again next time you start, so a model you meant to fetch is
never stranded.

The download is checked against the published checksum as it arrives and thrown away if it does
not match, rather than being kept: a file that fails its checksum is either a broken transfer or
something that should not be loaded, and both answers are the same.

## It knows what things are called

Every utterance is transcribed knowing the names around you: the system you are in, the station,
the body, your next jump, your ship and its type, your carrier, the route ahead, and your fleet.

This matters more than it sounds. **Proper nouns are where speech recognition fails hardest and
most quietly.** A misheard system name does not come back as an error — it comes back as a
plausible English phrase. "Shinrarta Dezhra" becomes "shin arta desha", everything proceeds
confidently about the wrong system, and nothing anywhere reports a problem.

The names come from your journal. Nothing is looked up.

## What happens to what you say

Spoken and typed questions run exactly the same path, so "where am I" means the same thing
however you said it.

Nothing captured is written to disk or sent anywhere. Audio sits in a small buffer, is overwritten
within about half a second unless you are holding the key, and only the stretch you actually
asked for goes anywhere at all.

A few small kindnesses:

- A press shorter than a quarter of a second is treated as a mis-press. Transcribing 80
  milliseconds of room tone produces a confident wrong word, which is worse than nothing.
- If your microphone is unplugged mid-sentence, what was captured is dropped rather than
  transcribed — half a sentence you did not finish is worse than none.
- A key stuck down stops at 60 seconds and **keeps** the audio. You said something; better late
  than lost.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `get_listening_status`

Read-only. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

**One gate policy over a continuous stream.** The microphone runs whenever D47 runs, into a small
ring buffer; the gate decides which part of that stream was speech addressed to D47. Continuous
listening and a wake word become later *policies* over the same buffer rather than a rewrite, and
"toggle instead of hold" is already a value rather than a second mechanism. It also means the
key-down path awaits nothing — opening a WASAPI capture device takes tens of milliseconds.

**Why the key is polled rather than hooked.** `RegisterHotKey`, which the silence key uses,
delivers `WM_HOTKEY` on press only; push-to-talk is defined by its release edge, so it is not a
candidate. A `WH_KEYBOARD_LL` hook has both edges and is forbidden — a global input chokepoint
means a stall in D47 is a stall in the Commander's controls mid-fight. Raw Input with
`RIDEV_INPUTSINK` works and is not a hook, but delivers *every keystroke on the system* to D47's
window, including passwords typed elsewhere; rejected on privacy. `GetAsyncKeyState` polled from
the tick loop reads exactly the one bound virtual-key code and never the keyboard.

Its cost is latency — 10 Hz sampling means a key-down is seen up to 100 ms late, which is why the
tick runs at the top of the 4–10 Hz band. The pre-roll absorbs it: the gate opens *retroactively*
into the ring buffer, 500 ms by default, so audio captured before the key was noticed is still
part of the utterance. The edge is computed against the previous sample rather than trusting
`GetAsyncKeyState`'s low bit, which is shared process-wide.

**Binds resolution has three traps.** The active preset is named in `StartPreset.<major>.start`
and must not be assumed to be `Custom` — parsing `Custom.*.binds` on a machine running
`KeyboardMouseOnly` reports the wrong keys with total confidence. Shipped presets live in the
game install directory rather than the user profile, so a Commander who never customised their
controls has no file under `Options\Bindings\` at all. Version suffixes compare numerically per
segment, so `4.10` sorts above `4.2`. Gestures are normalised before comparing, so `Ctrl+Alt+X`
and `LeftControl+LeftAlt+X` are the same key.

The consent request is raised from the composition root rather than the settings panel, so it
fires however the model came to be selected — and re-raised once a window exists to receive it,
because the startup request would otherwise reach nobody. The download is hashed as it lands and
the write is atomic; a half-downloaded model under its real name loads and then fails
mid-transcription. Transcription runs on the thread pool, never the audio thread. A transcript
that is entirely a bracketed annotation — `[BLANK_AUDIO]`, `(wind blowing)` — is treated as
silence. Rebinding while the key is held forces a release first, or the gate stays open with
nothing able to close it.

</details>
