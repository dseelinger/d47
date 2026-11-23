---
title: Watch
group: Voice
nav_order: 170
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
<p class="intro">Three steps to a video played by voice. The picture is on Media › Movies; the sound is D47's video channel.</p>
<section>
<h2><span class="num">1</span> Start a video.</h2>
<p class="say">Say watch, then the title of a video in Your list, such as Big Buck Bunny.</p>
<p class="expect">D47 says "Watching Big Buck Bunny." and the video starts from where you left it.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a watch command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">watch Big Buck Bunny</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">D47 looks in Your list first, then in the sources you ticked.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">A title that is in neither is answered with that.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Pause it and carry on.</h2>
<p class="say">Say "pause the video", then "carry on".</p>
<p class="expect">D47 says "Paused." and the picture stops; "carry on" says "Resumed." and plays on from the same second.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a pause command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">pause the video</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">While a video is playing or paused, "pause" and "play" act on it.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">"Continue watching" plays the newest title in Continue watching, or tries a stopped video again.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Move within it, or keep it.</h2>
<p class="say">Say "back thirty seconds" or "forward thirty seconds", or "add this to my watch list".</p>
<p class="expect">D47 says "Back 30 seconds." and the picture moves; the last one says "Added" and the title, which then shows in Your list.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a skip command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">back thirty seconds</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">With no video playing D47 says "No video is playing."</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">A title already in Your list is answered with that.</text>
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
<p class="intro">D47 plays the videos on Media › Movies when you ask, and keeps your place in each. One video plays at a time.</p>
<section>
<h2><span class="num">1</span> Anyone may ask for a video.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="Watching a video deletes nothing and spends nothing, so the model may use the tools">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="16" fill="var(--text-muted)">No video tool is protected.</text>
 <text x="440" y="90" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">You say it, or the model calls it.</text>
</svg>
<p class="body">The five video tools, such as <code>watch_video</code>, delete and spend nothing, so the model may call them as well as the phrases above.</p>
</section>
<section>
<h2><span class="num">2</span> Pause and play go to one thing at a time.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="Bare pause goes to the story, then the one book, podcast or video, then the music">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">story playback, then one book, podcast or video, then music</text>
 <text x="440" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">BuiltinCapabilities.cs</text>
</svg>
<p class="body">A bare "pause" or "play" goes to an open story playback first, then to the book, podcast or video that is playing, else the one that is paused, and otherwise to the music.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="podcasts.html"><span class="ct">Podcasts →</span><span class="cd">Shows you add, played by voice.</span></a>
<a class="card" href="books.html"><span class="ct">Books →</span><span class="cd">Books on your shelf, read aloud.</span></a>
</div>
</div>
</div></div>

## The details

The videos on Media › Movies. A title is looked for in Your list first, then in the sources ticked on the Watch
page; with none ticked every source is searched ([Privacy and egress](privacy.html)). A video resumes where it
was left. In the headset, a video plays on the panel the headset is showing.

### Ask for it

> watch, then a title
> "pause the video", "pause video"
> "carry on", "resume the video", "resume video"
> "continue watching", "carry on watching"
> "back thirty seconds", "skip back thirty seconds"
> "forward thirty seconds", "skip forward thirty seconds"
> "add this to my watch list", "add to my watch list", "add to watch list"

Watch followed by the title of anything in Your list or Continue watching is a phrase of its own and needs no
model. Any other title is reached by asking the model, which calls `watch_video`.

"Carry on" plays the current video on, or tries it again when it stopped on an error. "Continue watching" does
the same for a video that is playing, paused or stopped, and otherwise plays the newest title in Continue
watching from where it was left. The mini panel's notice for a stopped video tells you to say it.

The media keys and the volume flyout's buttons follow the same order as the bare words; on a video, Next and
Previous skip 30 seconds forward and back, and the flyout shows its title.

While a video is playing or paused, a bare "pause" or "play" acts on it. A story playback that is open takes
them first. Of a book, a podcast and a video, the one that is playing takes them, and the skip phrases; when
none plays, the paused one does, a book before a podcast before a video. With none of them, they go to the music.

Only one of a video, a book and a podcast plays at a time. Starting a video pauses the book or episode, and
starting either pauses the video, as if you had paused it; neither resumes by itself. A video does not start or
resume while a story plays: D47 says "A story is playing." A video pauses while D47 speaks, and plays on once it
has finished.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `watch_video`

Plays a video by title, looking in Your list first and then in the ticked movie sources.

```json
{"type":"object","properties":{"title":{"type":"string","description":"The title of the video, or part of it."}},"required":["title"],"additionalProperties":false}
```

#### `pause_video`

Pauses the video that is playing.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `resume_video`

Plays the paused video on, or with carry_on set the newest title in Continue watching, or tries a stopped video again.

```json
{"type":"object","properties":{"carry_on":{"type":"boolean","description":"True to play the newest title in Continue watching, or try a stopped video again. Optional."}},"required":[],"additionalProperties":false}
```

#### `skip_video`

Moves the video back or forward by a number of seconds.

```json
{"type":"object","properties":{"seconds":{"type":"integer","description":"Seconds to move: negative for back, positive for forward."}},"required":["seconds"],"additionalProperties":false}
```

#### `add_to_watch_list`

Adds a video to Your list. With no title it adds the video the player is on.

```json
{"type":"object","properties":{"title":{"type":"string","description":"The title of the video, or part of it. Optional."}},"required":[],"additionalProperties":false}
```

</details>
