---
title: GalNet
group: Knowledge
nav_order: 162
---

<!--
  Authoring rules for both bands are in the comment on engineers.md. The how-to band uses
  d47-howto because HelpLibrary.Band takes the first d47-eli5 div in the file.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two questions: what is new, and read me that one.</p>
<section>
<h2><span class="num">1</span> Ask what is on GalNet.</h2>
<p class="say">Say "what's on galnet".</p>
<p class="expect">D47 names the three newest articles with their in-game dates.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what's on galnet</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It answers from the articles kept on this PC and needs no model.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">"Galnet headlines" and "any galnet news" ask the same thing.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Have the newest read out.</h2>
<p class="say">Say "read the latest galnet article".</p>
<p class="expect">D47 reads the title, the in-game date and the whole article.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a request to read the latest article">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">read the latest galnet article</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"Read galnet" does the same.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">To search by word or pick an article by title, ask the model.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Turn on GalNet news under Galaxy search in Settings.</p>
<p class="expect">With nothing kept yet, D47 says so; the articles arrive after the setting is on.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="With GalNet news off and nothing kept there is nothing to read.">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">Off and empty means nothing to read.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Articles already kept can still be read with the setting off.</text>
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
<p class="intro">D47 keeps its own copy of GalNet and answers from that copy.</p>
<section>
<h2><span class="num">1</span> The model is sent matches, not the archive.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="A search sends three full articles and seven titles to the model, not the whole archive">
 <rect x="20" y="44" width="250" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">THE ARCHIVE</text>
 <text x="145" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">two years, never sent whole</text>
 <rect x="310" y="44" width="250" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="435" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">A SEARCH</text>
 <text x="435" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">title matches rank first</text>
 <rect x="600" y="44" width="260" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="730" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">THREE ARTICLES</text>
 <text x="730" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">and seven titles</text>
 <text x="440" y="190" text-anchor="middle" font-size="16" fill="var(--text)">The model asks for any other article by title.</text>
</svg>
<p class="body">Two years of GalNet is about forty-five thousand tokens, which would ride along with every question. The model gets article text only as the result of a tool it called for your question.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="privacy.html"><span class="ct">Privacy →</span><span class="cd">What the GalNet request sends, and what reaches your model.</span></a>
<a class="card" href="callouts.html"><span class="ct">Callouts →</span><span class="cd">The headline said when a new article arrives.</span></a>
</div>
</div>
</div></div>

## The details

D47 keeps the GalNet articles it fetched in `data\galnet.json`. These two tools read that file. They
make no network request.

### Ask for it

> "what's on galnet"
> "galnet headlines"
> "any galnet news"
> "read galnet"
> "read the galnet article"
> "read the latest galnet article"

```text
The latest on GalNet: Faulcon DeLacy Announces Anaconda Mk II, 12 MAR 3310; Aegis Reports Thargoid Activity, 11 MAR 3310; and Pilots Federation Updates Rank Titles, 10 MAR 3310.
```

Both tools answer from the kept articles whether **GalNet news** is on or off; off only stops new
articles arriving. With nothing kept, D47 says "No GalNet articles are kept yet." when the setting is
on, and "GalNet news is off. Turn it on under Galaxy search in Settings to keep the articles." when
it is off.

### What Directive 47 can do about it

#### `search_galnet`

With no query, the three newest by title and in-game date. With a query, articles ranked by how many
query words they contain: a match in the title counts more than one in the body, newer wins a tie,
case is ignored, a query word matches the start of an article word, and words under three letters are
ignored. The result is the full text of the top three and the title and date of up to seven more.

```json
{"type":"object","properties":{"query":{"type":"string","description":"Words to look for in the article titles and text."}},"required":[],"additionalProperties":false}
```

#### `get_galnet_article`

With no title, the newest article. With a title, the article whose title matches best, ignoring case.
The answer is the title, the in-game date and the full text.

```json
{"type":"object","properties":{"title":{"type":"string","description":"All or part of the article\u0027s title."}},"required":[],"additionalProperties":false}
```
