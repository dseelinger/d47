---
title: Audio mixer
group: Voice
nav_order: 122
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">How loud each kind of sound is, and how far it drops out of the way while Directive 47 speaks.</p>
<section>
<h2><span class="num">1</span> Five categories, and everything audible is one of them.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Speech, alerts, sound cues, the thinking bed and ambient music, each with its own level and mute">
 <rect x="21" y="40" width="158" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="100" y="76" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">SPEECH</text>
 <text x="100" y="106" text-anchor="middle" font-size="14" fill="var(--text-muted)">everything it</text>
 <text x="100" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">says out loud</text>
 <rect x="191" y="40" width="158" height="100" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="270" y="76" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">ALERTS</text>
 <text x="270" y="106" text-anchor="middle" font-size="14" fill="var(--text-muted)">the one thing that</text>
 <text x="270" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">cuts in mid-sentence</text>
 <rect x="361" y="40" width="158" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="76" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">SOUND CUES</text>
 <text x="440" y="106" text-anchor="middle" font-size="14" fill="var(--text-muted)">listening, thinking,</text>
 <text x="440" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">answering</text>
 <rect x="531" y="40" width="158" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="610" y="76" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">THINKING BED</text>
 <text x="610" y="106" text-anchor="middle" font-size="14" fill="var(--text-muted)">the loop under</text>
 <text x="610" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">a running turn</text>
 <rect x="701" y="40" width="158" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="780" y="76" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">AMBIENCE</text>
 <text x="780" y="106" text-anchor="middle" font-size="14" fill="var(--text-muted)">follows what</text>
 <text x="780" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">you are doing</text>
 <text x="440" y="186" text-anchor="middle" font-size="16" fill="var(--text)">Everything audible goes through one queue, and belongs to exactly one of these.</text>
 <text x="440" y="218" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">Each has a Level and a Mute, and they are separate on purpose.</text>
 <text x="440" y="246" text-anchor="middle" font-size="15" fill="var(--text-muted)">A level of zero and a mute sound identical and mean different things.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Ducking is a fraction, not a second level.</h2>
<svg viewBox="0 0 880 236" role="img" aria-label="The duck value is a fraction of a category's own level, so lowering the level lowers its ducked form too">
 <rect x="20" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">DUCK IS A FRACTION</text>
 <text x="220" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">of that category’s own level</text>
 <text x="220" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">not a level of its own</text>
 <rect x="460" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">SO TURNING MUSIC DOWN</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">takes its ducked form</text>
 <text x="660" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">down with it</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">1 does not duck at all. 0 goes silent until the sentence ends.</text>
 <text x="440" y="226" text-anchor="middle" font-size="15" fill="var(--text-muted)">The bed sits at 0.35 — audible enough to prove the turn is running, quiet enough not to compete.</text>
</svg>
<p class="body">Speech and alerts have no duck row, because there is nothing for them to duck under. A row that does not apply is absent rather than greyed out. And moving a level re-levels whatever is playing at that moment, because setting a level by ear is the only way to set one.</p>
</section>
<section>
<h2><span class="num">3</span> Ambience follows only what the game actually states.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Five ambience folders matching states Elite reports, with no combat or exploring folder">
 <rect x="21" y="40" width="158" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="100" y="74" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">docked</text>
 <text x="100" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">at a station</text>
 <rect x="191" y="40" width="158" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="270" y="74" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">supercruise</text>
 <text x="270" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">in supercruise</text>
 <rect x="361" y="40" width="158" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="74" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">normal-space</text>
 <text x="440" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">and landed too</text>
 <rect x="531" y="40" width="158" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="610" y="74" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">on-foot</text>
 <text x="610" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">out of the ship</text>
 <rect x="701" y="40" width="158" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="780" y="74" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">general</text>
 <text x="780" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">and the fallback</text>
 <rect x="20" y="150" width="840" height="52" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="182" text-anchor="middle" font-size="16" font-weight="700" fill="var(--danger)">no combat folder, and no exploring folder</text>
 <text x="440" y="234" text-anchor="middle" font-size="15" fill="var(--text-muted)">Neither is something the game reports, and a situation it has to infer plays the wrong music at the worst moment.</text>
</svg>
<p class="body">Directive 47 ships with no music of its own — drop 16-bit mono 48 kHz <code>.wav</code> files into <code>data/audio</code> and they are picked up while it runs. Tracks shuffle within a folder and the whole folder plays before any repeats, because you did not number your files.</p>
</section>
<section>
<h2><span class="num">4</span> There is no tool here at all.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="The model cannot lower Directive 47's own voice or the danger callouts, though you still can">
 <rect x="20" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="220" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--danger)">IT CANNOT TURN DOWN</text>
 <text x="220" y="110" text-anchor="middle" font-size="15" fill="var(--text)">its own voice</text>
 <text x="220" y="134" text-anchor="middle" font-size="15" fill="var(--text)">or the danger callouts</text>
 <rect x="460" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">YOU STILL CAN</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">by voice, through the router</text>
 <text x="660" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">and from the Settings tab</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">Those are the two things that would make it harder to hear exactly when hearing it matters.</text>
</svg>
<p class="body">“By voice” never quietly means “by the language model”. Every row here is reachable through the model-free keyword router, which is a different path with a different caller.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="speech.html"><span class="ct">Speech →</span><span class="cd">The voice the speech category is carrying, and what it costs.</span></a>
<a class="card" href="callouts.html"><span class="ct">Callouts →</span><span class="cd">What the alert category is for, and what earns the right to interrupt.</span></a>
<a class="card" href="settings.html"><span class="ct">Settings →</span><span class="cd">Where every level and mute lives, beside everything else.</span></a>
</div>
</div>
</div></div>

## The details

How loud each kind of sound is, whether it is muted, and how far it drops out of the way while
Directive 47 is speaking.

### The five categories

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

### Ducking

Speech and alerts are what everything else gets out of the way of, so the other three carry a
**Duck while speaking** number as well. It is a fraction of that category's level rather than a
level of its own — turn music down and its ducked form goes down with it.

`1` does not duck at all. `0` goes silent until the sentence ends. Out of the box the thinking
bed sits at `0.35`, which is enough to stay audible as evidence the turn is still running and
quiet enough not to compete with the words, and music sits at `0.2`.

Speech and alerts have no duck row, because there is nothing for them to duck under. A row that
does not apply is absent rather than greyed out.

### What you hear if you never touch it

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

### It takes effect while you are listening to it

Moving a level re-levels whatever is playing at that moment rather than waiting for the next
clip. Turn the bed down while a turn is running and it goes quiet under your hand, which is the
only way to set a level by ear.

### Your own sounds

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

### Ambience

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

### Not reachable by the model

There is no tool here. Directive 47 cannot turn its own voice down, and it cannot turn the danger
callouts down either — those are the two things that would make it harder to hear exactly when
hearing it matters, and no request needs them.

Every row is still reachable by voice, through the model-free keyword router, and from the
Settings tab. "By voice" never quietly means "by the language model" (see
[architecture](https://github.com/dseelinger/d47/blob/main/architecture.md) §7).
