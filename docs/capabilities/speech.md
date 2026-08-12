---
title: Speech
---

# Speech

**Group:** Voice
**Capability id:** `speech`

Everything d47 makes audible: spoken replies, the short cue that marks each state of the
conversation loop, the quiet bed under a working turn, and the one control that outranks all of
them.

Every audible thing goes through a single priority queue. Ducking, interruption, supersede and
caption timing are properties of that queue rather than four separate mechanisms that would have
to agree with each other. That is also why silence is instant: stopping is a queue operation, not
a feature layered on top of one.

> **Currently silent.** The free Edge Neural endpoint began refusing synthesis requests with
> HTTP 403 in August 2026. Voice *listing* still works; synthesis does not. Everything else on
> this page — cues, the bed, ducking, retry, and stopping — works regardless, because none of it
> goes near a voice provider. See [Voice provider](#provider).

## Try it

> "shut up"
> "be quiet"
> "stop talking"

## Tool

### `stop_speaking`

Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

This is the only part of the speech capability the model can reach. Changing the voice, the
output device or the cue settings is not exposed as a tool: a model able to make itself harder to
hear has no request that needs it. Stopping is the exception, because it is the one thing the
Commander must always be able to ask for.

Asking for silence never has to reach the model at all. `shut up` and its siblings are keyword
phrases, so the model-free router answers them; and the hotkey below reaches the same queue
operation with nothing in between. A spoken stop request that had to wait for a model would be
gated behind the very thing it is trying to interrupt.

## Settings

### Voice provider {#provider}

Where spoken replies are synthesised.

| Value | Meaning |
|---|---|
| `edge` | Edge Neural, the free voices Microsoft Edge's Read Aloud uses. |
| `none` | Do not speak. Cues and the bed still play. |

`none` is a first-class choice rather than a way of turning something off. It is what makes
local-only operation reachable: with no voice provider, no language model and no update check,
nothing leaves the machine at all.

### Voice {#voice}

Which voice speaks. The list is fetched from the selected provider, so it reflects what that
provider actually offers rather than a list baked into d47. An unrecognised value is still
accepted — the picker's contract is that an empty list lets you keep the current value or type
one.

### Speaking rate {#rate}

`1.0` is the voice's natural pace; `1.2` is a fifth faster. Held normalised and converted at the
provider boundary, because providers disagree about both the units and the range.

### Output device {#output-device}

Where d47 speaks, defaulting to whatever Windows is using. Stored as a device id rather than a
name, because a friendly name is not stable across driver updates. Changing it reopens the device
immediately; a device that has been unplugged falls back to the default rather than going silent.

d47 opens the device in shared mode and never takes exclusive hold of it. The game is what matters
on that output.

### Loop-state cues {#cues}

One short sound per state of the conversation loop.

| State | Sound |
|---|---|
| `idle` | A soft falling fourth — back to nothing in flight. |
| `listening` | A rising fourth. The microphone is open. |
| `transcribing` | Two quick ticks. |
| `thinking` | A single low tick, followed by the bed. |
| `speaking` | A brief high blip. |
| `answered` | A rising fifth. |
| `unsure` | A falling minor third — an explicit "unsure", which is a state rather than an error. |
| `failed` | A falling whole tone. |

The cue file is named for the state it belongs to, and d47 checks the shipped set against the list
of states at startup. A state added in code without a cue committed alongside it fails immediately,
with the state named — rather than going wrong as one state that silently never makes a sound.

The shipped cues are generated and reproducible:

```text
$ python tools/gen-cues.py
cues/idle.wav                                 0.35s
cues/listening.wav                            0.39s
cues/transcribing.wav                         0.15s
cues/thinking.wav                             0.12s
cues/speaking.wav                             0.09s
cues/answered.wav                             0.40s
cues/unsure.wav                               0.44s
cues/failed.wav                               0.51s
beds/thinking-hum.wav                         3.00s
beds/thinking-pulse.wav                       2.40s
```

### Thinking bed {#thinking-bed}

A quiet loop while a turn runs, so that a slow answer is audibly d47 working rather than d47
having ignored you. It ducks under speech instead of stopping, and it stops the moment the first
words arrive — not when the turn ends.

Two are shipped: `thinking-hum` and `thinking-pulse`. Both loop seamlessly, which is arithmetic
rather than luck — the carrier and its amplitude modulation each complete a whole number of cycles
over the buffer, so the last sample joins the first with no step. Getting that wrong produces a
tick once per loop, which sounds like a broken sound card rather than a broken table of numbers.

### Stop speaking {#shut-up}

Silences d47 instantly: the queue is flushed, the current sentence is cut off mid-word, and any
sentence still being synthesised is abandoned rather than allowed to arrive a moment later and
speak into the silence it was supposed to end.

The binding is system-wide, not window-scoped. The case this exists for is Elite holding the
foreground, and a key that only works when d47 has focus is gated by definition. This one is never
gated — not behind a turn completing, not behind the model, not behind focus.

This row is **protected**: it can be changed from the panel, by hotkey, or through the model-free
keyword router, but never through a tool the model can call. A model able to unbind the
Commander's stop button has removed the one control that outranks it.

### When a turn fails {#retry}

A turn that stalls is answered out loud rather than left as silence, because silence is
indistinguishable from d47 having ignored you.

| Setting | Default | Meaning |
|---|---|---|
| Attempts | `3` | Total tries, not retries. `1` disables retrying. |
| Wait between attempts | `2` | Seconds before the first retry. |
| Backoff | `sequential` | `sequential` adds the base each time (2s, 4s, 6s); `logarithmic` grows but decelerates. |
| Give up after | `45` | Seconds one attempt may run before it counts as failed. |

Two rules shape what actually gets retried:

- **A failure that already produced words is never retried.** Streamed text has usually been
  spoken already, and there is no such thing as un-saying it.
- **A configuration failure is never retried.** A bad model name fails identically next time, so
  waiting on it only spends the Commander's silence. Only transient failures are worth a retry.

When the attempts run out, d47 says so in the current voice:

```text
I couldn't reach the model after 3 tries. Overloaded.
```

A provider that simply hangs is the case the timeout exists for. Left alone it produces no events
at all — not an error, just a turn that never ends — and that is the worst thing this app can do,
because the Commander cannot tell it apart from having been ignored.

### What the voice provider receives {#egress}

```text
Edge Neural: the text of every reply d47 speaks is sent to Microsoft to be turned into
audio. No game state, no journal content and no keys are sent. Choosing "None" sends
nothing and leaves d47 silent.
```

Text is escaped before it is sent. That is a security control rather than tidiness: a Commander can
name a ship anything, journal content is untrusted, and an unescaped name containing markup would
otherwise close the speech element and choose the voice for the rest of the sentence.
