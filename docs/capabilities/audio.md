---
title: Audio mixer
group: Voice
nav_order: 123
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and the class decides behaviour, not just
  appearance. HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under
  that class would silently become what the in-app panel draws on this page. The docs site styles
  the two identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to a mix you can hear over the ship.</p>
<section>
<h2><span class="num">1</span> Open the Audio mixer and move the five sliders.</h2>
<svg viewBox="0 0 880 268" role="img" aria-label="The five volume sliders: speech, cues, music, ambience and alerts">
 <rect x="20" y="16" width="840" height="236" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Audio mixer</text>
 <text x="44" y="98" font-size="16" fill="var(--text)">Speech</text>
 <rect x="180" y="86" width="560" height="8" rx="4" fill="var(--border)"/>
 <rect x="180" y="86" width="440" height="8" rx="4" fill="var(--accent)"/>
 <circle cx="620" cy="90" r="11" fill="var(--accent)"/>
 <text x="770" y="98" font-size="15" fill="var(--text-muted)">80%</text>
 <text x="44" y="142" font-size="16" fill="var(--text)">Cues</text>
 <rect x="180" y="130" width="560" height="8" rx="4" fill="var(--border)"/>
 <rect x="180" y="130" width="336" height="8" rx="4" fill="var(--accent)"/>
 <circle cx="516" cy="134" r="11" fill="var(--accent)"/>
 <text x="770" y="142" font-size="15" fill="var(--text-muted)">60%</text>
 <text x="44" y="186" font-size="16" fill="var(--text)">Music</text>
 <rect x="180" y="174" width="560" height="8" rx="4" fill="var(--border)"/>
 <rect x="180" y="174" width="224" height="8" rx="4" fill="var(--accent)"/>
 <circle cx="404" cy="178" r="11" fill="var(--accent)"/>
 <text x="770" y="186" font-size="15" fill="var(--text-muted)">40%</text>
 <text x="44" y="230" font-size="15" fill="var(--text-muted)">Ambience and Alerts sit below these two.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Set how far everything else drops while D47 talks.</h2>
<svg viewBox="0 0 880 156" role="img" aria-label="The ducking row, which lowers every other category while Directive 47 speaks">
 <rect x="20" y="20" width="840" height="60" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">Duck everything else while D47 speaks</text>
 <text x="836" y="57" text-anchor="end" font-size="16" fill="var(--accent)">-12 dB</text>
 <text x="20" y="124" font-size="16" fill="var(--text-muted)">Move a slider while something is playing and you hear the change as you make it.</text>
 <text x="20" y="148" font-size="16" fill="var(--text-muted)">Nothing has to be re-triggered to audition it.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="The mixer is unreachable by the model, so asking it to turn something down does nothing">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">The AI cannot touch the mixer.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Asking it to turn itself down does nothing. Move the slider, or use your Windows mixer.</text>
</svg>
</section>
</div></div>
</details>

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">How loud each kind of sound is, and how far it drops out of the way while Directive 47 speaks.</p>
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
<h2><span class="num">3</span> Ambience follows the music Elite is playing.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Elite's music track picks the folder; an empty folder falls back to the Status.json situation, then general">
 <rect x="21" y="40" width="250" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="146" y="74" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">Elite's music track</text>
 <text x="146" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">combat-dogfight, galaxy-map…</text>
 <rect x="315" y="40" width="250" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="74" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">where you are</text>
 <text x="440" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">docked, supercruise, on-foot…</text>
 <rect x="609" y="40" width="250" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="734" y="74" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">general</text>
 <text x="734" y="102" text-anchor="middle" font-size="14" fill="var(--text-muted)">the last fallback</text>
 <text x="293" y="90" text-anchor="middle" font-size="18" fill="var(--text-muted)">→</text>
 <text x="587" y="90" text-anchor="middle" font-size="18" fill="var(--text-muted)">→</text>
 <rect x="20" y="150" width="840" height="52" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2.5"/>
 <text x="440" y="182" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">an empty folder changes nothing</text>
 <text x="440" y="234" text-anchor="middle" font-size="15" fill="var(--text-muted)">Set Elite's music volume to zero (Options, Audio) to hear yours instead of the game's.</text>
</svg>
<p class="body">Directive 47 ships with no music of its own — drop your own <code>.mp3</code>, <code>.m4a</code>, <code>.flac</code> or <code>.wav</code> files into <code>data/audio</code> and they are picked up while it runs. Tracks shuffle within a folder and the whole folder plays before any repeats, because you did not number your files.</p>
</section>
<section>
<h2><span class="num">4</span> No tool sets a level or a mute.</h2>
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
<p class="body">“By voice” never silently means “by the language model”. Every row here is reachable through the model-free keyword router, which is a different path with a different caller. The one tool, <code>manage_music</code>, only pauses, resumes and skips the ambient music.</p>
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

### What silences what

Ranking decides who goes next, not who gets cut off. Something already playing is normally left to
finish, and there are exactly three exceptions:

| What arrives | What it cuts | What it leaves alone |
|---|---|---|
| **An alert** | Anything being said, mid-word | Nothing; only "stop" overrules it |
| **You starting to talk to it** | Invented NPC chatter, playing or queued | Everything else, including the sound cue |
| **"Stop"** | Everything audible, including the alert | Nothing |

The middle row is the narrow one, and it fires when you open the microphone rather than when the
answer arrives. Transcription and the model together take several seconds, and a four-line exchange
finishes inside them, so waiting for the answer would mean hearing the whole exchange out anyway.

It stays shut until the loop goes quiet again, rather than only cutting what was audible at that
moment. An exchange is written and voiced ahead of what you are hearing, so the next speaker is
already on their way when the first is cut off, and would otherwise arrive a second later.

It reaches invented chatter and nothing else: a message the game sent you, a callout you switched
on, and a sound cue are all left where they are. And it is only ever you who does it — an
unprompted line of Directive 47's own queues up behind the chatter like anything else, because
nobody was waiting on it.

The sound cue is the reason the rule is written this way. A cue is a fifth of a second and it plays
immediately before the sentence it introduces, so anything allowed to cut in on rank alone would
truncate the cue that announced it, every single turn.

### Ducking

Speech and alerts are what everything else gets out of the way of, so the other three carry a
**Duck while D47 speaks** number as well. It is a fraction of that category's level rather than a
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

Drop audio files into `data/audio` beside the executable. `.mp3`, `.m4a`, `.aac`, `.wma`,
`.flac` and `.wav` are read, at any sample rate and channel count; D47 converts them to 48 kHz
mono as it loads them. The folders are made for you on first run, and **Open audio folder** on
the **Your own audio** row opens `data\audio` in Explorer:

```text
data/audio/
  cues/<state>/        idle, listening, transcribing, thinking, speaking, answered, unsure, failed
  alerts/<alert>/      interdiction, piracy, bounty-hunter, under-fire, overheating,
                       rival-territory, timer-elapsed
  beds/                the loop under a working turn
  music/<situation>/   ambience — see below
```

Windows does the decoding. A Windows N edition has no MP3 or AAC decoder until the Media Feature
Pack is installed; `.wav` works without it.

A sound goes in the folder for where it is used, under any file name. A cue or alert folder with
files in it plays only those, one picked each time the cue plays; an empty one plays the shipped
sound. `beds/` works the same way, with one file picked when a turn starts and looped for that
turn. Each folder plays every file once, in shuffled order, before any repeats, and never the same
file twice in a row. A file loose in `cues/` or `alerts/`, outside a named folder, is not read.

Files are picked up while D47 is running, without a restart, and a reload never cuts a clip that
is already playing.

A file that will not load is skipped rather than fatal, and the **Your own audio** row says which
one and why — a skipped file is silent in exactly the way a missing one is, so without that the
only symptom of a file Windows cannot read is a cue that never plays:

```text
2 files picked up from data/audio.
Skipped: engine-room: Windows has no decoder for this file. On a Windows N edition, install the Media Feature Pack.
```

### Ambience

`music/<situation>/` is a background layer of its own, with its own level and its own
ducking, separate from the cues and the thinking bed. Elite writes a `Music` event naming the
situation it is scoring, and D47 plays from the folder for that track:

| Folder | Elite track | Without a track, from Status.json |
|---|---|---|
| `docked` | `Starport` | Docked at a station or an outpost. |
| `supercruise` | `Supercruise` | In supercruise. |
| `normal-space` | `Exploration` | In a ship, a fighter or an SRV, and neither of the above — including landed. |
| `on-foot` | `OnFoot` | Out of the ship. |
| `general` | — | Anything else, and the last fallback. |
| `main-menu` | `MainMenu` |  |
| `docking-computer` | `DockingComputer` |  |
| `galaxy-map` | `GalaxyMap` |  |
| `system-map` | `SystemMap` |  |
| `scanner` | `SystemAndSurfaceScanner` |  |
| `codex` | `Codex` |  |
| `combat-dogfight` | `Combat_Dogfight` |  |
| `combat-large` | `Combat_LargeDogFight` |  |
| `combat-srv` | `Combat_SRV` |  |
| `combat-unknown` | `Combat_Unknown` |  |
| `interdiction` | `Interdiction` |  |
| `capital-ship` | `CapitalShip` |  |
| `unknown-encounter` | `Unknown_Encounter` |  |
| `unknown-exploration` | `Unknown_Exploration` |  |
| `unknown-settlement` | `Unknown_Settlement` |  |
| `guardian-sites` | `GuardianSites` |  |
| `fog-cloud` | `Lifeform_FogCloud` |  |
| `arrival-from-supercruise` | `DestinationFromSupercruise` |  |
| `arrival-from-hyperspace` | `DestinationFromHyperspace` |  |
| `fleet-carrier` | `FleetCarrier_Managment` |  |
| `squadrons` | `Squadrons` |  |
| `powerplay` | `GalacticPowers` |  |
| `cqc-menu` | `CQCMenu` |  |
| `cqc` | `CQC` |  |

When Elite's track has a folder with files in it, that folder plays. Otherwise the folder comes
from what `Status.json` states — the five in the last column — and then `general`. An empty
folder changes nothing. `NoTrack`, `NoInGameMusic` and any track not listed have no folder.

To hear this music rather than Elite's, set Elite's music volume to zero (Options, Audio).

Tracks are shuffled within a folder and the whole folder plays before any of them repeats — you
did not number your files, and hearing the same one every time you dock is what happens if D47
plays them in name order. Two seconds of silence separate a track that ends from the next one,
the pause a CD puts between tracks. A situation with nothing in it falls back to `general`; `general` with
nothing in it is quiet, which is what every Commander gets until they drop something in. D47
ships with no music of its own.

Changing situation stops the old track rather than letting it play out: arriving at a station is
the moment the docking music is wanted, not thirty seconds later. Muting the category stops it,
and unmuting starts one — a switch whose effect waits for the next time you dock is a switch that
reads as broken.

### Pause, resume and next track

The keyboard's play/pause and next-track keys control the ambient music. D47 registers as a Windows
media session, so it also appears in the volume flyout with the current track's file name and
working buttons. Nothing else D47 plays — speech, cues, alerts or the thinking bed — answers to
these keys.

- **Pause** holds the track where it is. **Resume** continues it from that point.
- **Next** ends the track and starts the next one in the shuffle. It also ends a pause.
- While paused, a change of situation starts nothing. Resume starts the new situation's track.
- Pause lasts until you resume or restart D47. It is not the **Mute** setting, which is saved and
  unchanged by these keys.

Windows sends the media keys to whichever media session was used last. With Spotify or another
player also open, the keys may go to that player instead.

The same three actions work by voice — "pause the music", "resume the music", "next track" or
"skip track" — through `manage_music`, which the model can also call:

```json
{"type":"object","properties":{"action":{"type":"string","description":"What to do to the music.","enum":["pause","resume","next"]}},"required":["action"],"additionalProperties":false}
```

### Not reachable by the model

No tool sets a level or a mute; `manage_music` only pauses, resumes and skips the music.
Directive 47 cannot turn its own voice down, and it cannot turn the danger callouts down either — those are the two things that would make it harder to hear exactly when
hearing it matters, and no request needs them.

Every row is still reachable by voice, through the model-free keyword router, and from the
Settings tab. "By voice" never silently means "by the language model".
