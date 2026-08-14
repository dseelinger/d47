---
title: Audio mixer
---

How loud each kind of sound is, whether it is muted, and how far it drops out of the way while
Directive 47 is speaking.

## The five categories

Everything audible goes through one queue, and everything on that queue belongs to exactly one
of these:

| Category | What it is |
|---|---|
| **Speech** | Everything D47 says out loud. |
| **Alerts** | The danger callouts — interdiction, shields down, low fuel. The one thing that cuts in mid-sentence. |
| **Sound cues** | The short markers for listening, thinking and answering. |
| **Thinking bed** | The loop that plays underneath a turn while D47 works. |
| **Ambient music** | The background layer that follows what you are doing. |

Each has a **Level** from 0 to 1 and a **Mute**. They are separate on purpose: a level of zero
and a mute sound identical and mean different things, and flicking the mute back should not cost
you the level you had it at.

## Ducking

Speech and alerts are what everything else gets out of the way of, so the other three carry a
**Duck while speaking** number as well. It is a fraction of that category's level rather than a
level of its own — turn music down and its ducked form goes down with it.

`1` does not duck at all. `0` goes silent until the sentence ends. Out of the box the thinking
bed sits at `0.35`, which is enough to stay audible as evidence the turn is still running and
quiet enough not to compete with the words, and music sits at `0.2`.

Speech and alerts have no duck row, because there is nothing for them to duck under. A row that
does not apply is absent rather than greyed out.

## What you hear if you never touch it

The defaults are what D47 sounded like before there was a mixer, with one addition: ambient music
arrives at half level and ducking to a fifth, because a background layer that turns up at full
volume is one you switch off rather than turn down.

```json
{
  "bed":    { "level": 1,   "muted": false, "duckUnderSpeech": 0.35 },
  "music":  { "level": 0.5, "muted": false, "duckUnderSpeech": 0.2 },
  "cue":    { "level": 1,   "muted": false, "duckUnderSpeech": 1 },
  "speech": { "level": 1,   "muted": false, "duckUnderSpeech": 1 },
  "alert":  { "level": 1,   "muted": false, "duckUnderSpeech": 1 }
}
```

That block is the `audio` section of `data/settings.json`. You never have to edit it — every
value has a row — but it is a plain file and it reads the way the rows do.

## It takes effect while you are listening to it

Moving a level re-levels whatever is playing at that moment rather than waiting for the next
clip. Turn the bed down while a turn is running and it goes quiet under your hand, which is the
only way to set a level by ear.

## Not reachable by the model

There is no tool here. Directive 47 cannot turn its own voice down, and it cannot turn the danger
callouts down either — those are the two things that would make it harder to hear exactly when
hearing it matters, and no request needs them.

Every row is still reachable by voice, through the model-free keyword router, and from the
Settings tab. "By voice" never quietly means "by the language model" (see
[architecture](https://github.com/dseelinger/d47/blob/main/architecture.md) §7).
