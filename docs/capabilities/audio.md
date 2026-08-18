---
title: Audio mixer
group: Voice
nav_order: 120
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

## Your own sounds

Drop 16-bit mono 48 kHz `.wav` files into `data/audio` beside the executable. The folders are
made for you on first run, so opening the data folder is enough to find out what goes where:

```text
data/audio/
  cues/<loop-state>.wav      replaces a shipped sound cue
  beds/<name>.wav            adds a thinking bed to the picker
  music/<situation>/*.wav    ambience — see below
```

A cue file is named for the loop state it belongs to: `idle`, `listening`, `transcribing`,
`thinking`, `speaking`, `answered`, `unsure`, `failed`. A bed file is named whatever you like, and
the name is what appears in the **Thinking bed sound** picker, marked as yours.

Files are picked up while D47 is running. Drop one in and it appears in the picker without a
restart, and a reload never cuts a clip that is already playing.

A file that will not load is skipped rather than fatal, and the **Your own audio** row says which
one and why — a skipped file is silent in exactly the way a missing one is, so without that the
only symptom of a wrong sample rate is a cue that never plays:

```text
2 files picked up from data/audio.
Skipped: stereo-bed: it is 48000 Hz / 2ch; audio must be 48000 Hz mono 16-bit.
```

## Ambience

`music/<situation>/*.wav` is a background layer of its own, with its own level and its own
ducking, separate from the cues and the thinking bed. There are five situations, and they are the
five Elite's `Status.json` can state without anyone guessing:

| Folder | When |
|---|---|
| `docked` | Docked at a station or an outpost. |
| `supercruise` | In supercruise. |
| `normal-space` | In a ship, a fighter or an SRV, and neither of the above — including landed. |
| `on-foot` | Out of the ship. |
| `general` | Anything else, and the fallback for a situation with no files of its own. |

There is no `combat` folder and no `exploring` folder. Neither is something the game reports, and
a situation D47 has to infer is a situation that plays the wrong music at the worst moment.

Tracks are shuffled within a folder and the whole folder plays before any of them repeats — you
did not number your files, and hearing the same one every time you dock is what happens if D47
plays them in name order. A situation with nothing in it falls back to `general`; `general` with
nothing in it is quiet, which is what every Commander gets until they drop something in. D47
ships with no music of its own.

Changing situation stops the old track rather than letting it play out: arriving at a station is
the moment the docking music is wanted, not thirty seconds later. Muting the category stops it,
and unmuting starts one — a switch whose effect waits for the next time you dock is a switch that
reads as broken.

## Not reachable by the model

There is no tool here. Directive 47 cannot turn its own voice down, and it cannot turn the danger
callouts down either — those are the two things that would make it harder to hear exactly when
hearing it matters, and no request needs them.

Every row is still reachable by voice, through the model-free keyword router, and from the
Settings tab. "By voice" never quietly means "by the language model" (see
[architecture](https://github.com/dseelinger/d47/blob/main/architecture.md) §7).
