---
title: Podcasts
group: Voice
nav_order: 168
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it, which are in the comment
  on engineers.md. The class is d47-howto rather than d47-eli5: HelpLibrary.Band takes the first
  d47-eli5 div in the file, so a second band under that class would become what the in-app panel
  draws on this page. No rationale in here; every "because" belongs in the band below.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Three steps to a podcast by voice. Adding a show is yours alone; playing it is not.</p>
<section>
<h2><span class="num">1</span> Find a show and add it.</h2>
<p class="say">Ask the model to search for a show, then say "add podcast one".</p>
<p class="expect">D47 lists up to three shows, numbered. After you add one it says how many episodes the feed holds.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a podcast search typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">add podcast one</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"add podcast one" - "add podcast two" - "add podcast three"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">The number is the show's place in the last search.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Play it, and carry on where you stopped.</h2>
<p class="say">Say "play the podcast", then "pause", then "play".</p>
<p class="expect">The newest unfinished episode starts, the ambient music stands aside, and "pause" and "play" now act on the episode.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a play command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">play the podcast</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"next episode" - "skip back thirty seconds" - "stop the podcast"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Each episode starts again where you left it.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Use the media keys.</h2>
<p class="say">Press the keyboard's play/pause key, then its next key.</p>
<p class="expect">The key pauses or resumes the episode, and next plays the next unfinished episode; the volume flyout shows the episode and its show.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The volume flyout showing an episode and its show">
 <rect x="20" y="24" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="64" font-size="17" font-weight="700" fill="var(--text)">Episode title</text>
 <text x="44" y="96" font-size="16" fill="var(--text-muted)">Show title</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">With nothing playing the keys go to the ambient music.</text>
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
<p class="intro">D47 plays the shows you add and remembers where you stopped. Only you can add a show.</p>
<section>
<h2><span class="num">1</span> Only you add a show.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="Adding a feed is the Commander's, playing is not">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="16" fill="var(--text-muted)">Each show you add is an address D47 contacts.</text>
 <text x="440" y="90" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">So the model can search, but it cannot add.</text>
</svg>
<p class="body">The <code>add_podcast</code> tool is protected: you reach it by saying a result number, and the model is refused. The model has no way to type an address.</p>
</section>
<section>
<h2><span class="num">2</span> Pause and play go to one thing at a time.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="Bare pause goes to the story, then the podcast, then the music">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">story playback, then podcast, then music</text>
 <text x="440" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">PodcastCapability.cs</text>
</svg>
<p class="body">A bare "pause" or "play" goes to an open story playback first, then to a podcast that is playing or paused by you, and otherwise to the music.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="audio.html"><span class="ct">Audio mixer →</span><span class="cd">The ambient music, and the level of the Podcasts channel.</span></a>
<a class="card" href="privacy.html"><span class="ct">Privacy and egress →</span><span class="cd">Which hosts a feed and its episodes contact.</span></a>
</div>
</div>
</div></div>

## The details

Podcasts you add, played by voice, the media keys and the feed list. Each episode resumes where you left it.

### Ask for it

> "play the podcast"
> "pause the podcast", "resume the podcast", "stop the podcast"
> "next episode"
> "skip back thirty seconds", "back thirty seconds"
> "skip forward thirty seconds"
> "refresh podcasts"

"Play the podcast" carries on the episode in the slot, or plays the newest unfinished episode of any show.
Asking the model to play a named show plays that show's newest unfinished episode.

While an episode is playing or paused by you, a bare "pause" or "play" acts on it. A story playback that is
open takes them first; with no episode in the slot they go to the music.

"Next episode" plays the earliest unfinished episode of the same show that was published after the current
one. With none it says so and stops. With no episode playing it says "No podcast is playing."

### Find and add a show {#adding}

> "add podcast one", "add podcast two", "add podcast three"

`find_podcasts` searches Apple's podcast directory and numbers up to three results. The last search's results
are kept until the next search. Adding takes the number, not an address.

### The feed list

With Show every setting on, open Settings › Hearing and sound › Sounds and levels, group **Podcasts**. **Podcast
feeds** holds the feed addresses, separated by spaces. Saving it fetches any feed that is new. Only http and
https addresses are accepted. The model cannot change this row.

### The media keys

While an episode is playing or paused by you, the keyboard's play/pause key and the volume flyout's buttons act
on it: next is the next episode and previous is skip back 30 seconds. The flyout shows the episode and its show.
Otherwise they act on the ambient music.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `manage_podcast`

Plays, pauses, resumes or stops a podcast episode, skips 30 seconds back or forward, plays the next episode of
the same show, or refreshes the added feeds. Play with a show plays that show's newest unfinished episode;
without one it carries on the current episode or plays the newest unfinished episode of any show. Each episode
resumes where it was left.

```json
{"type":"object","properties":{"action":{"type":"string","description":"What to do.","enum":["play","pause","resume","stop","next","skip_back","skip_forward","refresh"]},"show":{"type":"string","description":"With play: the show title, or part of it. Optional."}},"required":["action"],"additionalProperties":false}
```

#### `list_podcasts`

Lists the added podcast shows and each one's unfinished episodes, newest first. A spoken answer names at most
three shows.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `find_podcasts`

Searches the podcast directory for shows and returns up to three, numbered one to three. The words go to
Apple's podcast directory ([Privacy and egress](privacy.html)).

```json
{"type":"object","properties":{"words":{"type":"string","description":"What to search for."}},"required":["words"],"additionalProperties":false}
```

#### `add_podcast`

Adds search result one, two or three from the last podcast search, then fetches its feed.

```json
{"type":"object","properties":{"result":{"type":"integer","description":"The number of the search result, one to three."}},"required":["result"],"additionalProperties":false}
```

**Protected**: the model is not offered it. The keyword router reaches it with "add podcast one", "add podcast
two" and "add podcast three".

</details>
