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
  the two identically (main.scss extends one from the other); the app draws this band first and the How it works band after it.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to a mix you can hear over the ship.</p>
<section>
<h2><span class="num">1</span> Set a level on the mixer.</h2>
<p class="say">Turn on Show every setting, open Settings › Hearing and sound › Sounds and levels, and move Level on the Speech row.</p>
<p class="expect">The value changes in steps of 0.05, and what is playing gets louder or quieter at once.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The Levels group with a row per channel and a Level value">
 <rect x="20" y="20" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="44" y="53" font-size="17" font-weight="700" fill="var(--text)">Speech</text>
 <text x="836" y="53" text-anchor="end" font-size="17" fill="var(--text)">Level 1</text>
 <rect x="20" y="84" width="840" height="52" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="117" font-size="17" font-weight="700" fill="var(--text)">Sound cues</text>
 <text x="836" y="117" text-anchor="end" font-size="17" fill="var(--text)">Level 1</text>
 <text x="20" y="166" font-size="16" fill="var(--text-muted)">Mute is its own tick, so the level survives it.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Set how far music drops while D47 talks.</h2>
<p class="say">On the Ambient music row, set Duck while D47 speaks to 0.</p>
<p class="expect">The music goes silent until D47 finishes the sentence. At 1 it does not drop at all.</p>
<svg viewBox="0 0 880 150" role="img" aria-label="The Duck while D47 speaks value on the Ambient music row">
 <rect x="20" y="20" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="44" y="53" font-size="17" font-weight="700" fill="var(--text)">Ambient music</text>
 <text x="836" y="53" text-anchor="end" font-size="17" fill="var(--text)">Duck while D47 speaks 0</text>
 <text x="20" y="112" font-size="16" fill="var(--text-muted)">Thinking bed and Sound cues have a Duck value too.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Pause the music by voice.</h2>
<p class="say">With music playing, say "pause the music", then "resume the music", "next track" or "previous track".</p>
<p class="expect">Pause holds the track, resume carries on, next track starts another from the same folder, and previous track plays the one before the current track.</p>
<svg viewBox="0 0 880 140" role="img" aria-label="The ask row with a music command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">pause the music</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Needs Elite running and a file in a music folder.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> No music plays until you add some or download the library.</h2>
<p class="say">Put an audio file in data/audio/music/general, with Elite running.</p>
<p class="expect">The file plays, quietly. D47's own music library is a separate download, off until you turn on <b>Download the D47 music library</b>.</p>
<svg viewBox="0 0 880 150" role="img" aria-label="A file in the general music folder">
 <rect x="20" y="24" width="840" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">data/audio/music/general</text>
 <text x="440" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">any .mp3, .m4a, .aac, .wma, .flac or .wav file</text>
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
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Every sound D47 makes belongs to one of six categories, each with its own level, mute and duck.</p>
<section>
<h2><span class="num">1</span> Each category has a level and a mute.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="Six categories: speech, alerts, overheard chatter, sound cues, thinking bed and ambient music">
 <rect x="20" y="24" width="840" height="46" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="53" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">SPEECH   ALERTS   OVERHEARD CHATTER</text>
 <rect x="20" y="80" width="840" height="46" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="109" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">SOUND CUES   THINKING BED   AMBIENT MUSIC</text>
</svg>
<p class="body">Each has a Level from 0 to 1 and a Mute. Muting keeps the level, so unmuting returns to the loudness you had.</p>
</section>
<section>
<h2><span class="num">2</span> While D47 speaks, three categories drop.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="Duck is the fraction of its level a category drops to while D47 speaks">
 <rect x="20" y="24" width="840" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">Duck while D47 speaks</text>
 <text x="440" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">thinking bed, sound cues, ambient music</text>
</svg>
<p class="body">Each of those has a Duck value: the fraction of its level it drops to. 1 never drops. 0 is silent until the sentence ends.</p>
</section>
<section>
<h2><span class="num">3</span> Docking changes the music.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="Elite writes a Music event naming Starport, and D47 plays from the docked folder">
 <rect x="20" y="24" width="250" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">Music: Starport</text>
 <text x="145" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">from Elite</text>
 <line x1="282" y1="69" x2="296" y2="69" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="310,69 294,61 294,77" fill="var(--accent-muted)"/>
 <rect x="320" y="24" width="250" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="445" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">music/docked</text>
 <text x="445" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">one file, shuffled</text>
 <line x1="582" y1="69" x2="596" y2="69" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="butt"/>
 <polygon points="610,69 594,61 594,77" fill="var(--accent-muted)"/>
 <rect x="620" y="24" width="240" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="740" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">Level and Duck</text>
 <text x="740" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">as set</text>
</svg>
<p class="body">When you dock, Elite writes a Music event naming Starport. D47 holds the old track and plays from music/docked, continuing the docked track it held if you left less than 30 minutes ago, at the music Level, dropping to its Duck while D47 speaks.</p>
</section>
<section>
<h2><span class="num">4</span> Pause, resume, next and previous control the music only.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="One tool controls the ambient music and nothing else">
 <rect x="20" y="24" width="840" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="65" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">manage_music</text>
 <text x="440" y="91" text-anchor="middle" font-size="15" fill="var(--text-muted)">pause, resume, next, previous</text>
</svg>
<p class="body">The tool is declared as <code>Pauses, resumes or skips D47's ambient music.</code> Levels and mutes are separate rows.</p>
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

### The six categories

Everything audible goes through one queue, and everything on that queue belongs to exactly one
of these:

| Category | What it is |
|---|---|
| **Speech** | Everything D47 says out loud. |
| **Alerts** | The danger callouts — interdiction, shields down, low fuel. The one thing that cuts in mid-sentence. |
| **Overheard chatter** | Invented radio traffic between other pilots and controllers, not meant for you. |
| **Sound cues** | The short markers for listening, thinking and answering. |
| **Thinking bed** | The loop that plays underneath a response while D47 works. |
| **Ambient music** | The background layer that follows what you are doing. |

Each has a **Level** from 0 to 1 and a **Mute**. A level of zero and a mute sound the same, but
unmuting returns to the level the row had.

### What silences what

Ranking decides who goes next, not who gets cut off. Something already playing is normally left to
finish. Four things cut it, and a new turn also drops what the previous turn had left to say:

| What arrives | What it cuts | What it leaves alone |
|---|---|---|
| **An alert** | Anything being said, mid-word | Nothing; only "stop" overrules it |
| **You starting to talk to it** | Invented NPC chatter, playing or queued | Everything else, including the sound cue |
| **Elite closing** | Invented chatter and Narrator lines, playing or queued | Everything else |
| **"Stop"** | Everything audible, including the alert | Nothing |

The middle row fires when the microphone opens, and also when a turn starts without you having
spoken, rather than when the answer arrives. It stays shut until the loop goes quiet again, so
chatter that is written ahead of what you are hearing is cut too.

It reaches invented chatter and nothing else: a message the game sent you, a callout you switched
on, and a sound cue are all left where they are. An unprompted line of Directive 47's own queues up
behind the chatter like anything else.

A sound cue is not cut by rank. A shipped cue runs from about 0.1 to 0.5 seconds and plays
immediately before the sentence it introduces.

### Ducking

Speech, alerts and overheard chatter are what everything else ducks under, so the thinking bed,
the sound cues and ambient music carry a **Duck while D47 speaks** number as well. It is a fraction of that category's level rather than a
level of its own — turn music down and its ducked form goes down with it.

`1` does not duck at all. `0` goes silent until the sentence ends. Out of the box the thinking
bed sits at `0.35`, the sound cues at `1`, and music at `0.2`.

Speech, alerts and overheard chatter have no duck row. A row that does not apply is absent rather
than greyed out.

### What you hear if you never touch it

Ambient music and overheard chatter arrive at half level, and music ducks to `0.2`.

```json
{
  "bed":    { "level": 1,   "muted": false, "duckUnderSpeech": 0.35 },
  "music":  { "level": 0.5, "muted": false, "duckUnderSpeech": 0.2 },
  "cue":    { "level": 1,   "muted": false, "duckUnderSpeech": 1 },
  "overheard": { "level": 0.5, "muted": false, "duckUnderSpeech": 1 },
  "speech": { "level": 1,   "muted": false, "duckUnderSpeech": 1 },
  "alert":  { "level": 1,   "muted": false, "duckUnderSpeech": 1 }
}
```

That block is the `audio` section of `data/settings.json`. Every level and mute has a row; a duck
value has a row only for the thinking bed, music and sound cues.

### It takes effect while you are listening to it

Moving a level re-levels whatever is playing at that moment rather than waiting for the next
clip.

### Your own sounds

Drop audio files into `data/audio` beside the executable. `.mp3`, `.m4a`, `.aac`, `.wma`,
`.flac` and `.wav` are read, at any sample rate and channel count; D47 converts them to 48 kHz
mono as it loads them. The folders are created at startup whenever they are missing, and **Open audio folder** on
the **Audio folder** row, and at the foot of the **Your own audio** page, opens `data\audio` in Explorer:

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
file twice in a row. A file loose in `cues/`, `alerts/` or `music/`, outside a named folder, is not
read.

Files are picked up while D47 is running, without a restart, and a reload never cuts a clip that
is already playing.

The **Your own audio** group is four status rows. **Music** counts the files and the situations and
names the three `music/` folders with the most files; **Your audio** beside it opens the page.
**Sound cues** counts what `cues/` and `alerts/` hold, and **Thinking beds** what `beds/` holds; with
none, each says so in white and, in grey, how to add one. **Audio folder** shows `data\audio` with
**Open audio folder**, and an amber notice under it counts the files that were ignored or skipped.

```text
Music           159 files for 38 situations: main menu 24, docked 23, supercruise 23 and 35 more   [Your audio]
Sound cues      None of your own. D47 plays its own cues. A file in cues/<state> replaces one.
Thinking beds   None of your own. A file in beds/ adds one.
Audio folder    data\audio                                                    [Open audio folder]
                1 file skipped
```

The page that **Your audio** opens, headed Your own audio, lists every folder with files under
Music, Sound cues, Alerts and Thinking beds, most files first, and every ignored and skipped file in
full. `get_setting audio.drops` still returns the whole account, one line per folder and per file:

```text
cues/listening: 5 files
alerts/under-fire: 1 file
music/combat-dogfight: 12 files
Ignored: klaxon.wav is directly in alerts; put it in a folder such as alerts/under-fire.
Ignored: cues/listening/notes.txt is not an audio format D47 reads.
Skipped: engine-room: Windows has no decoder for this file. On a Windows N edition, install the Media Feature Pack.
```

### Ambience

`music/<situation>/` is a background layer of its own, with its own level and its own
ducking, separate from the cues and the thinking bed. Elite writes a `Music` event naming its
track, and D47 maps that track to a folder:

| Folder | Elite track | Without a track, from Status.json |
|---|---|---|
| `docked` | `Starport` | Docked at a station or an outpost. |
| `supercruise` | `Supercruise` | In supercruise. |
| `normal-space` | `Exploration` | In a ship, a fighter or an SRV, and neither of the above — including landed. |
| `on-foot` | `OnFoot` | Out of the ship. |
| `on-foot-station` |  | On foot in a station, a hangar or a social space. |
| `settlement` |  | On foot or in the SRV at a settlement, from the journal's settlement approach until supercruise. |
| `general` | — | Anything else, and the last fallback. |
| `main-menu` | `MainMenu` |  |
| `docking-computer` | `DockingComputer` |  |
| `galaxy-map` | `GalaxyMap` |  |
| `system-map` | `SystemMap` |  |
| `scanner` | `SystemAndSurfaceScanner` |  |
| `codex` | `Codex` |  |
| `combat-dogfight` | `Combat_Dogfight` |  |
| `combat-large` | `Combat_LargeDogFight`, `CombatLargeDogFight` |  |
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
| `titan-encounter` | `Titan_Encounter` |  |
| `titan-graveyard` | `Titan_Graveyard` |  |
| `thargoid-factory` | `TGFactory` |  |
| `combat-thargoid-ground` | `Thargoid_GroundCombat` |  |
| `combat-hunters` | `Combat_Hunters` |  |
| `combat-ground` |  | On foot in a ground conflict zone: from the dropship's deploy, or the first kill bond on foot at a settlement, until the retreat, a taxi booking or supercruise. |
| `damaged-starport` | `Damaged_Starport` |  |

Elite's general tracks (`OnFoot`, `Starport` and `Exploration`) give way to `on-foot-station`,
`settlement` or `combat-ground`, when that folder has files in it. Every other Elite track whose folder has files
in it plays that folder. Otherwise the folder comes from what `Status.json` and the journal state — the
eight in the last column — and then `general`. An empty
folder changes nothing. `NoTrack`, `NoInGameMusic` and any track not listed have no folder.

`arrival-from-supercruise`, `arrival-from-hyperspace` and `interdiction` each play one track and then hand
back. The track is not looped, and a change of Elite's track while it plays does not cut it: when it ends, the
folder for the situation at that moment continues or starts. A track still playing after 60 seconds fades out.

With **Download the D47 music library** on, D47 also plays its music library from `data/music-pack/<folder>/`,
with the same folder names; see [The music library](#the-music-library). Files of your own in
`data/audio/music/<folder>/` play alongside the library's tracks for that folder, shuffled together. The Music row of **Your own audio** counts only your files.

To hear this music rather than Elite's, set Elite's music volume to zero (Options, Audio).

Ambience plays only while Elite is running. D47 checks for the game's window every two seconds:
when the game closes, crashed or not, the track stops, and when it starts again the music picks up
for the situation it is in, unless you paused or muted it. Until Elite is detected, nothing plays.

Tracks are shuffled within a folder and the whole folder plays before any of them repeats. Two
seconds of silence separate a track that ends from the next one. A situation with nothing in it falls back to `general`; `general` with
nothing in it is quiet, which is what every Commander gets until they drop something in or download the music
library.

Changing situation fades the old track out and the new one in, quickly (under a second) when either is a map or a menu or the new one is combat, and over a few seconds between the others. The old track is held where it is, and coming back to that folder within 30
minutes continues it from that point, so a run of short galaxy-map visits plays as one track. The
hold is per folder, not per situation: two situations that both fall back to `general` share one
track. Up to four tracks are held; a fifth drops the one held longest. Muting the category, saying
"stop" or closing Elite stops every held track, and unmuting starts a new one.

After ten minutes in one situation with nothing happening, the music fades out over ten seconds and the
track is held where it stopped. When something happens it fades back in over three seconds from that
point. Something happening is any journal event except `Music`, `ReceiveText`, `NpcCrewPaidWage`,
`CommunityGoal` and `FSSSignalDiscovered`, which Elite writes while you sit docked; a change of situation;
a turn with D47; or a media key. A track held this way is not dropped after 30 minutes. While it is held,
the volume flyout shows it paused and **Now playing** reads Paused, and Resume or the play key brings it back.

### The music library

**Download the D47 music library** (Settings › Hearing and sound › Sounds and levels, group Ambient music) is off
by default. The library is several gigabytes; the line under the setting gives its size once D47 has read the list of
folders. On, D47 downloads it from GitHub in the background, one folder at a time in the order the library lists
them, the most-used first, into `data/music-pack/<folder>/`. Each folder is checked against its SHA-256 and unpacked under
another name before it is renamed into place, so a folder never plays half-written. A folder already on disk at the
current version is not downloaded again, and a newer version replaces it.

Before each folder D47 checks for twice its download size free on the drive, and stops with a message on the setting's
line when there is less. The line also counts the folders in, names the one being fetched and says when one failed. A
failed folder is tried again at the next start.

Off, nothing more is downloaded, the music restarts without the library, and the files stay on disk. Deleting
`data/music-pack` reclaims the space.

### Pause, resume, next and previous track

The keyboard's play/pause, next-track and previous-track keys control the ambient music. D47 registers as a Windows
media session, so it also appears in the volume flyout with the current track's file name, minus its
extension, and working buttons. Nothing else D47 plays — speech, cues, alerts or the thinking bed — answers to
these keys.

- **Pause** holds the track where it is. **Resume** continues it from that point.
- **Next** ends the track and starts the next one in the shuffle. It also ends a pause.
- **Previous** steps back one place in the current folder's shuffle and plays that track from the start; it does not restart the playing track first. A track queued for deletion is stepped over. On the first track of a shuffle it restarts the playing track. During a one-shot cue it ends the cue and continues the situation's shelved track. It also ends a pause.
- While paused, a change of situation starts nothing, and the paused track is held for its folder.
  Resume continues the held track of the folder you are in now, if it has one within 30 minutes,
  and otherwise starts that folder's next track.
- With Elite closed, resume, next and previous reply "Elite is not running." and play nothing. Pause still
  works.
- Pause lasts until you resume or restart D47. It is not the **Mute** setting, which is saved and
  unchanged by these keys.

The same controls are on the page. With Show every setting on, open Settings › Hearing and sound ›
Sounds and levels; the **Ambient music** group, above Levels, holds three rows. **Now playing** shows the
track's file name, or Paused, Muted or Nothing playing, and its button reads Pause while a track plays and
Resume while paused. **Next track** has a Next button that starts another track from the same folder. **Previous track**
has a Previous button that plays the track before the current one. All
buttons are greyed out when nothing plays or the music is muted.

Windows sends the media keys to whichever media session was used last. With Spotify or another
player also open, the keys may go to that player instead.

The same four actions work by voice — "pause the music", "resume the music", "next track",
"skip track" or "previous track" — through `manage_music`, which the model can also call:

```json
{"type":"object","properties":{"action":{"type":"string","description":"What to do to the music.","enum":["pause","resume","next","previous"]}},"required":["action"],"additionalProperties":false}
```

### Thumbs down a track {#thumbs-down-music}

This is for everyone on your own tracks, and for the maintainer of the music library, who has a Music
publish token, on library tracks. With one of your own tracks playing, "thumbs down this track" or "thumbs
down" moves the file to the Recycle Bin, skips to the next track and replies with the track's name and that it
was deleted. With a library track playing and a token, it adds the track's folder and file name to
`data/music-rejects.tsv`, skips to the next track and replies with the number of tracks queued. The same
happens from the **Thumbs down the playing track** hotkey, which has no default and is bound in Settings ›
Hearing and sound › Sounds and levels, group Ambient music, or from a stick button.

- A track already in the file is not added twice, and a queued track is not played again.
- A track from your own `data/audio/music` folder is moved to the Recycle Bin and the next track starts. A
  file that cannot be deleted gets a reply saying so, and the track is still skipped.
- With no music playing, the reply says so.
- Without a token, a library track is refused and keeps playing; nothing is written to the file.

The phrase reaches `thumbs_down_music`, which only you can reach: the model is never offered it.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Delete the thumbs-down tracks {#delete-music-rejects}

This is for the maintainer of the music library, and works only with a Music publish token. "Delete the
thumbs-down tracks" deletes every track in `data/music-rejects.tsv` from the library's download and from this PC.
There is no hotkey or button for it. The reply says how many tracks in how many folders are being deleted, and a
second reply, when the work ends, says what was deleted and what failed.

For each folder with queued tracks, one at a time:

1. A new `<folder>.zip` is built from the folder's tracks in `data/music-pack/<folder>/`, without the queued ones.
2. It replaces `<folder>.zip` on the `music-1` release of `dseelinger/d47-music`. Then `manifest.json` is replaced,
   with that folder's version increased by one and its size, SHA-256 and track count updated.
3. The new version is recorded in the folder, so the library download does not fetch it again.
4. The queued tracks are deleted from `data/music-pack/<folder>/`.
5. With `D47_MUSIC_SOURCE` set to the music generator's data folder, each track is deleted from
   `<source>/tracks/<folder>/` and its id, the file name without `.mp3`, is added to `<source>/rejected.tsv`.
   Without it, the reply says the generator's copies were left.
6. The folder's rows are removed from `data/music-rejects.tsv`.

Nothing is deleted from this PC until GitHub has the folder's new zip and manifest.

- A folder whose download here is older than the published version is left, with a reply saying to let the
  download finish first, because a zip built from it would drop tracks.
- A folder whose every track is queued is left, since a folder cannot be emptied.
- When an upload fails, that folder keeps its rows and its files, and the work stops. The reply names the folder,
  GitHub's message, and the folders that were not tried. Saying the phrase again continues with the folders left.
- One deletion runs at a time.
- Without a token the phrase is not matched.

The phrase reaches `delete_music_rejects`, which only you can reach: the model is never offered it.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

### Music publish token {#music-publish-token}

This is for the maintainer of the music library, the public repository `dseelinger/d47-music`. Create a
fine-grained personal access token on GitHub with access to that repository only and the permission **Contents:
Read and write**. Paste it into **Music publish token** in the advanced settings, where it is stored encrypted
for this Windows account and is never shown again. Without a stored token, Directive 47 reads the environment
variable `D47_MUSIC_PUBLISH_TOKEN`.

### Who can change a level

`manage_music` pauses, resumes and skips the music and sets no level or mute. The Level, Mute and
Duck rows are protected: the model's `set_setting` tool refuses them, so D47 cannot turn its own
voice or the danger callouts down. They are changed from the Settings tab. No row declares a
keyword-router phrase, so no fixed spoken command sets one.
