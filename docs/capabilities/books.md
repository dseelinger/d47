---
title: Books
group: Voice
nav_order: 169
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
<p class="intro">Three steps to a book read aloud by voice. Books are read by a voice on this computer or by Edge Neural.</p>
<section>
<h2><span class="num">1</span> Start a book.</h2>
<p class="say">Say "read the book", or say read followed by a title from your shelf.</p>
<p class="expect">D47 says which book and chapter it is reading, such as "Reading The War of the Worlds, chapter 1."</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a read command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">read the book</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Read followed by a shelf title starts that book.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">A book starts again where you left it.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Pause it and carry on.</h2>
<p class="say">Say "pause the book", then "resume the book".</p>
<p class="expect">D47 says "Paused." and stops at the sentence it was on; "resume the book" says "Resumed." and reads that sentence again.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a pause command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">pause the book</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">While a book is playing or paused, "pause" and "play" act on it.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">A story playback that is open takes them first.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Move through the book.</h2>
<p class="say">Say "next chapter", then "back a paragraph".</p>
<p class="expect">D47 says "Chapter 2. The Falling Star." and reads it from its start; "back a paragraph" says "Back a paragraph." and reads the paragraph before again.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a next chapter command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">next chapter</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">In the last chapter D47 says so.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">With no book being read it says "No book is being read."</text>
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
<p class="intro">D47 reads the books on your shelf aloud, sentence by sentence, and remembers your place. Only you start, pause or move a book.</p>
<section>
<h2><span class="num">1</span> Only you drive the book.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="Reading a book is the Commander's, not the model's">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="16" fill="var(--text-muted)">Each book tool is protected.</text>
 <text x="440" y="90" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">You say it; the model is refused.</text>
</svg>
<p class="body">The five book tools, such as <code>read_book</code>, are reached by the phrases above and refused to the model, as <code>play_story</code> is.</p>
</section>
<section>
<h2><span class="num">2</span> Pause and play go to one thing at a time.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="Bare pause goes to the story, then the one book or podcast, then the music">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">story playback, then one book or podcast, then music</text>
 <text x="440" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">BookCapability.cs</text>
</svg>
<p class="body">A bare "pause" or "play" goes to an open story playback first, then to the book or podcast that is playing, and otherwise to the music.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="podcasts.html"><span class="ct">Podcasts →</span><span class="cd">Shows you add, played by voice.</span></a>
<a class="card" href="audio.html"><span class="ct">Audio mixer →</span><span class="cd">The ambient music, and the level of the speech channel.</span></a>
</div>
</div>
</div></div>

## The details

The books on the shelf at Media › Books, read aloud by Kokoro or Chatterbox, which run on this computer, or by Edge Neural,
which needs an internet connection and sends the text to Microsoft. A book resumes where it was left.

### Ask for it

> "read the book", "read book"
> read, then the title of a book on the shelf
> "pause the book", "resume the book"
> "next chapter"
> "back a paragraph"

"Read the book" carries on the book being read, else the book last read this session, else the first book on
the shelf with a saved place. Read followed by a title reads that book from where it was left, or from its start once
finished. The title may be part of the full title.

While a book is playing or paused, a bare "pause" or "play" acts on it. A story playback that is open takes
them first. A podcast that is playing takes them over a paused book; otherwise the book takes them. With
neither, they go to the music.

The media keys and the volume flyout's buttons follow the same order. On a book, Next is the next chapter and
Previous is back a paragraph; the flyout shows the book's title and the chapter title.

Only one of a book and a podcast plays at a time. Starting a book pauses the episode, and playing or resuming an
episode pauses the book, as if you had paused it; neither resumes by itself. A book does not start or resume
while a story plays: D47 says "A story is playing." A story starting pauses the book.

Speech or an alert pauses the book at its sentence, and once it ends the book reads that sentence again, as it
does after you speak to D47. Invented chatter is not played while the book is read, and the ambient music fades
out and stays silent until the book stops or you pause it.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `read_book`

Reads a book on the shelf aloud, from where it was left or from the start once finished. With no title it reads the book last read.

```json
{"type":"object","properties":{"title":{"type":"string","description":"The shelf title, or part of it. Optional."}},"required":[],"additionalProperties":false}
```

**Protected**: the model is not offered it. The keyword router reaches it with "read the book" and read followed by a shelf title.

#### `pause_book`

Pauses the book being read.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

**Protected**: reached with "pause the book".

#### `resume_book`

Resumes the book that was paused.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

**Protected**: reached with "resume the book".

#### `next_chapter`

Reads the book being read from the start of its next chapter.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

**Protected**: reached with "next chapter".

#### `back_a_paragraph`

Reads the book being read again from the start of the paragraph before the one it is in.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

**Protected**: reached with "back a paragraph".

</details>
