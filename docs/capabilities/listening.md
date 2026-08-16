---
title: Listening
group: Voice
nav_order: 118
---

Talking to Directive 47 instead of typing at it. Hold a key, speak, let go, and what you said is
handled exactly as if you had typed it — or put your hands back on the stick and let it decide for
itself when you are talking to it.

**No audio and no transcript leaves your machine.** Speech is turned into words by a model
running on your own computer. The model file itself is downloaded once, from `huggingface.co`;
after that, nothing about your speech goes anywhere.

## Ask for it

> "can you hear me"
> "what microphone are you using"
> "is my push to talk key bound twice"

```text
Microphone: Yeti Nano, capturing.
Gate: hands free, opening when you say my name.
Right now: the microphone is open and I am waiting to hear you start.
Push-to-talk: CapsLock (hold).
Warning: CapsLock is also bound in Elite (KeyboardMouseOnly) to HeadLookToggle. One of the two
will not work, and neither will say so — pick another key for one of them.
I answer to: D47.
Echo cancellation: running, so you can talk over me.
Transcription: base.en loaded.
```

Everything comes back together on purpose. "It cannot hear me" has five possible causes — no key
bound, no microphone, the microphone gone, the key clashing with Elite, or no speech model — and
you should not have to guess which.

## Getting set up

Three things, in this order:

**1. Bind a key.** Already done — **right shift**, out of the box. Clear the row if you would
rather Directive 47 never opened the microphone at all.

**2. Download a speech model.** Also already done, or under way. Directive 47 ships with **Tiny
(English only)** selected and fetches it from `huggingface.co` the first time it starts — about
75 MB, once. Until it lands, Directive 47 captures your voice and tells you plainly that it
cannot understand it yet.

**3. Check the key is yours alone.** If Elite is already using it, say so is exactly what
Directive 47 does — see below.

**Optional: put the key away entirely.** [How D47 decides you are talking to it](#mode) has two
hands-free settings. They are off out of the box, deliberately — that section says what turning
one on actually means.

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

The key you hold to talk. **Right shift out of the box** — a Commander on a stick and throttle has
a spare thumb and not much else, and it is the right-hand shift specifically, so the left one you
may already be using in the game is not this.

Clear the row and Directive 47 never opens the microphone — unless you have also put
[the row below](#mode) into one of its hands-free settings, which is the whole point of those and
the only case where an unbound key still leaves a live microphone.

Clearing it was the old default, and it meant a voice companion that could not hear anything until
you found this row.

In the two key-driven settings, nothing is kept unless the key is held.

Unlike the stop key, this one does not need a modifier — a bare key is the normal arrangement for
push-to-talk, which is exactly why the collision check above matters. If right shift is bound to
something in Elite on your setup, the status report above says so by name.

**The model cannot change this.** A model that could unbind your microphone key has taken away
how you talk to it.

### How D47 decides you are talking to it {#mode}

Four settings, and the key works in all four — a policy that decides for itself is not a reason to
take away the one that does not.

| | What it does |
|---|---|
| **Press to talk (PTT)** | Hold the key while you speak. The shipped default. |
| **Toggle on and off** | Press once to start, again to stop. |
| **Listen whenever I speak** | Directive 47 opens the microphone itself when it hears somebody start talking, and closes it when they stop. |
| **Listen when I say its name** | The same, except what you said is thrown away unless you addressed it by name. |

**What the last two actually mean.** The microphone was already open — it always is, so the
pre-roll has something in it — but until now nothing was ever *kept* unless you were holding the
key. In the two hands-free settings, every stretch of speech in the room is captured and
transcribed before Directive 47 can decide whether it was meant for it. That is a real change and
it is why these are off out of the box.

What does not change: **none of it leaves your machine and none of it is written to disk.** The
speech model runs locally, a stretch that was not addressed to Directive 47 is discarded without
reaching the transcript, and the panel says the microphone is open the whole time it is — see
[Seeing that the microphone is open](#indicator).

The cost is CPU. Everything said near you is transcribed and then mostly thrown away, so run these
on one of the smaller models unless you have cycles to spare.

**The model cannot change this row.** A model that could put Directive 47 into continuous
listening could start capturing on your machine — and anything the model can call, a hostile
in-game message can try to invoke. You can still say *"listen for your name"* or *"stop listening
all the time"*, which go through the keyword router rather than through the model.

### How much louder than the room speech has to be {#sensitivity}

Only applies hands free. Directive 47 measures your room continuously — falling to a new quiet
almost at once, rising to a new loud slowly — so this is a **margin above whatever your room
happens to be**, not a fixed loudness. That is what makes one number work across a headset boom, a
desk condenser and a laptop array, whose levels differ by tens of decibels.

Lower hears more, and will open on a cough or a keyboard. Higher waits until you are clearly
talking. **9 dB** out of the box.

### Quiet that ends a sentence {#silence}

How long you have to stop talking before Directive 47 decides you have finished. **700 ms** out of
the box, which is deliberately generous: people pause mid-sentence to look at something, and an
utterance cut at the first gap reaches the model as half a question. Being wrong the other way
costs a little dead air on the end of the clip, which the speech model ignores.

### What D47 answers to {#wake-words}

Only applies in **Listen when I say its name**. Leave it unset and Directive 47 answers to whatever
you call your ship's AI, so renaming the core renames the wake word with it.

Set it — comma-separated — when the speech model keeps hearing the name as something else. It is
already forgiving: the comparison ignores punctuation and spacing, so `D47`, `D 47`, `d-47` and
`D47.` are all the same word. Adding a spelling is for when it comes out as something genuinely
different.

The name has to be near the front of what you said. Talking *about* Directive 47 is not talking
*to* it.

### Seconds D47 keeps listening after you say its name {#wake-window}

Say the name on its own, Directive 47 sounds its listening cue, and the next thing you say is the
request — the way you would address a person. **12 seconds** out of the box, and the follow-up
closes the window again: it is one reply, not an open microphone. Nothing is said back and nothing
reaches the transcript; being called by name is not conversation.

Set it to zero if you would rather the name and the request always arrived in the same breath.

### Cancel D47's own voice out of the microphone {#echo-cancellation}

On, in every setting rather than only the hands-free ones — holding the key while a callout is
being read out otherwise transcribes the callout.

On speakers, this is what lets you **talk over Directive 47**. Measured against a simulated room,
it removes upwards of 25 dB of its own voice while leaving yours essentially intact.

If it cannot start — the native library missing, usually — it says so and the microphone keeps
working. Hands-free listening then goes deaf while Directive 47 is speaking, rather than risk the
loop where it hears itself, transcribes itself and answers itself. Ask *"can you hear me"* and it
will tell you which of the two you are in.

On headphones none of this matters much, because there is no echo to cancel.

### Take the room out of what D47 hears {#noise-suppression}

On. Suppresses steady background noise — fans, a headset's own hiss — before the speech model sees
it. It also makes the hands-free decision easier, since that decision is entirely about how far a
sound sits above the room.

### Capture before the key {#pre-roll}

How much audio from just before the gate opened is kept. **500 ms** out of the box.

It exists because the key is sampled ten times a second, so a key-down is noticed up to 100 ms
after it happened — and without this the first syllable of every sentence is clipped, which is
where the proper nouns are. It does the same job hands free, where what it covers is the moment
before Directive 47 was willing to call the sound speech.

### Speech model {#model}

Which Whisper model turns your speech into words. The row marks which are already on disk and
what the others would cost to fetch, so you can see which choices are already paid for.

**Tiny (English only)** is what a fresh install has selected — the smallest one, and enough for
the short push-to-talk clips this is actually asked to transcribe. Move up to Base or Small if
you want the accuracy and can spend the download.

**Choosing a model downloads it.** Selecting one you do not have starts the transfer there and
then, with the size and progress on the row; the same thing happens at startup for a model that
is selected and missing. The choice is the go-ahead — the size is on the row before you make it,
and `huggingface.co` is listed under [Privacy](privacy.md) for as long as a model is selected.

`none` stays a real choice. Pick it and Directive 47 hears you and says, honestly, that it cannot
turn what it heard into words.

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

## Seeing that the microphone is open {#indicator}

Bottom left of the panel, on the desktop **and** in the headset, in mini as well as full. Three
states:

| | |
|---|---|
| **Microphone open, nothing kept** | Push-to-talk at rest. Audio runs into the half-second ring and is overwritten. |
| **Listening for you** | Hands free. Directive 47 is deciding for itself when to listen. |
| **Listening** *(filled, ringed)* | The gate is open. What is arriving now will be transcribed. |

Filled or hollow is the state, not only the colour — a glance reads a shape first, and a difference
that is only colour is not a difference for everybody. Hover it for the gesture, or the name, that
would open the gate.

Nothing is drawn when no device is open at all.

## Downloading a model {#download}

A selected model that is not on disk is fetched — at startup, or the moment you choose it. There
is no prompt to answer: the selection is the go-ahead, the size is on the row before you make the
choice, and the shipped default is the smallest model in the list.

The download is checked against the checksum `huggingface.co` publishes for the file, as it
arrives, and thrown away rather than kept if it does not match: a file that fails its checksum is
either a broken transfer or something that should not be loaded, and both answers are the same.
The size Directive 47 reports is the one the host actually gave, not a figure written into the
app that would go stale the first time a model was republished.

If the download fails — no network, the host refusing — the selection stays where it is and
Directive 47 says it has no speech model loaded when you ask. It tries again the next time it
starts. Choose `none` if you would rather it stopped trying.

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

Nothing captured is written to disk or sent anywhere. Audio sits in a small buffer and is
overwritten within about half a second unless the gate is open — because you are holding the key,
or because Directive 47 heard somebody start talking. Only that stretch goes any further, and
"further" means a speech model on your own machine.

A few small kindnesses:

- A press shorter than a quarter of a second is treated as a mis-press. Transcribing 80
  milliseconds of room tone produces a confident wrong word, which is worse than nothing.
- If your microphone is unplugged mid-sentence, what was captured is dropped rather than
  transcribed — half a sentence you did not finish is worse than none.
- A key stuck down stops at 60 seconds and **keeps** the audio. You said something; better late
  than lost.
- Hands free, a stretch that turns out to have been somebody else talking is dropped without
  reaching the transcript, the panel or the log.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

### `get_listening_status`

Read-only. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

**One gate policy over a continuous stream.** The microphone runs whenever D47 runs, into a small
ring buffer; the gate decides which part of that stream was speech addressed to D47. It also means
the key-down path awaits nothing — opening a WASAPI capture device takes tens of milliseconds.

Phase 13 is what that sentence was written for, and it held. Voice activity is a detector on the
same `Write` path opening the same gate; the wake word does not touch the gate at all, because it
decides about *words* rather than about audio — the detector segments the stream, Whisper turns a
segment into text, and `WakeWordGate` decides whether that text was addressed to D47. So the wake
word is a settings row rather than a second model to ship, a second download, and a fixed
vocabulary chosen at build time that could never be the name the Commander gave their ship's AI.
Its cost is stated rather than hidden: everything said in the room is transcribed before it can be
discarded.

**The detector decides on the audio thread and the tick thread acts on it.** Opening the gate
plays a cue and closing it hands an utterance to a transcriber, and neither belongs on a real-time
callback. The cost is up to one tick at each end — which the pre-roll already covers at the front
and the 700 ms hangover dwarfs at the back.

**AEC3 consumes the arbiter's render reference tap, not a loopback capture.** The tap has existed
since Phase 5 with nothing subscribed to it, precisely so this could be one subscription rather
than surgery on the component every voice path depends on (architecture.md D7). A loopback capture
would be a second WASAPI stream, on a device that may not be the one D47 is rendering to, arriving
late with a clock of its own. Both directions are re-framed to the 10 ms the module takes, and the
remainder is carried rather than padded — padding tells the canceller the Commander stopped
talking, mid-word, several times a second. Measured against a simulated room with a 60 ms path:
upwards of 25 dB removed, and the near end kept within a few dB during double-talk. A *harmonic*
far end defeats the delay estimator entirely, which is worth knowing before writing a test with a
sine wave in it.

Whether the canceller is running is read from the canceller, never from the row that asked for it:
that boolean is what decides whether hands-free listening stays open while D47 speaks, and one
that was asked for and failed to load its native library must not be believed in.

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

The fetch is started from the composition root rather than the settings panel, so it happens
however the model came to be selected — the panel, the keyword router, or a hand-edited settings
file — and one at a time, since listening settings are applied on every change. The download is
hashed as it lands and the write is atomic; a half-downloaded model under its real name loads and then fails
mid-transcription. Transcription runs on the thread pool, never the audio thread. A transcript
that is entirely a bracketed annotation — `[BLANK_AUDIO]`, `(wind blowing)` — is treated as
silence. Rebinding while the key is held forces a release first, or the gate stays open with
nothing able to close it.

</details>
