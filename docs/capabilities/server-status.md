---
title: Server status
group: Knowledge
nav_order: 163
---

<!--
  Authoring rules for both bands are in the comment on engineers.md. The how-to band uses
  d47-howto because HelpLibrary.Band takes the first d47-eli5 div in the file.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">One question: are the servers up?</p>
<section>
<h2><span class="num">1</span> Ask about the servers.</h2>
<p class="say">Say "are the servers up".</p>
<p class="expect">D47 says Online, In maintenance, Offline or Unknown, and how long ago it checked.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">are the servers up</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">It answers from the last check and needs no model.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">"Is elite down" and "server status" ask the same thing.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Turn on Server status under Outside sources in Settings.</p>
<p class="expect">With the setting off, D47 says so and checks nothing.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="With Server status off there is no answer.">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">Off means no answer.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">D47 only checks while the setting is on.</text>
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
<p class="intro">D47 answers from the check it already makes, not a new one.</p>
<section>
<h2><span class="num">1</span> Asking sends nothing.</h2>
<svg viewBox="0 0 880 200" role="img" aria-label="The question reads the last check; only the timed check goes to Frontier">
 <rect x="20" y="44" width="250" height="96" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">TIMED CHECK</text>
 <text x="145" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">goes to Frontier</text>
 <rect x="310" y="44" width="250" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="435" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">LAST ANSWER</text>
 <text x="435" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">kept in memory</text>
 <rect x="600" y="44" width="260" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="730" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">YOUR QUESTION</text>
 <text x="730" y="114" text-anchor="middle" font-size="14" fill="var(--text-muted)">reads it, sends nothing</text>
</svg>
<p class="body">Only the state word is given, never Frontier's own text.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="privacy.html"><span class="ct">Privacy →</span><span class="cd">What the server check sends.</span></a>
<a class="card" href="callouts.html"><span class="ct">Callouts →</span><span class="cd">The line said when the servers go down or come back.</span></a>
</div>
</div>
</div></div>

## The details

D47 reads the state from its server status check. These tools make no network request.

### Ask for it

> "are the servers up"
> "is elite down"
> "server status"

```text
Online. Checked 3 minutes ago.
```

With **Server status** off, D47 says "Server status is off. Turn it on under Outside sources in Settings
to check the servers." Before the first check it says "Server status has not been checked yet."

### What Directive 47 can do about it

#### `get_server_status`

The state of Elite's servers, Online, In maintenance, Offline or Unknown, and how long ago it was
checked. Unknown means the last request failed or its answer could not be read.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```
