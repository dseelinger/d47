---
title: Focus the game
group: Interface
nav_order: 129
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
<p class="intro">Two steps to getting the game back in front.</p>
<section>
<h2><span class="num">1</span> Say it, from anywhere.</h2>
<p class="say">Say "focus the game" or "bring up Elite".</p>
<p class="expect">Elite comes to the front even while D47 has the keyboard.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">focus the game</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"bring up Elite" — "put the game in front"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">It works while D47 has the keyboard, which is exactly when you need it.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Read the reply.</h2>
<p class="say">Say "switch to Elite" and read what D47 answers.</p>
<p class="expect">"Elite is in front." or, when it already was, "Elite is already in front."</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a phrase typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">switch to Elite</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"Elite is in front." — "Elite is already in front."</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">If Windows refuses, the reply says so and the taskbar button flashes.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Close Elite, then say "focus the game".</p>
<p class="expect">With Elite closed, the reply is "I cannot find Elite — it does not look like it is running."</p>
<svg viewBox="0 0 880 152" role="img" aria-label="It cannot start Elite for you.">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">It cannot start Elite for you.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Focus brings a running game forward. With Elite closed there is nothing to bring.</text>
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
<p class="intro">Saying a phrase puts Elite back in front, so flight commands work again.</p>
<section>
<h2><span class="num">1</span> Flight keys go out only while Elite is in front.</h2>
<svg viewBox="0 0 880 162" role="img" aria-label="You alt-tab to a browser, say the phrase, and Elite returns">
 <rect x="20" y="16" width="840" height="130" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="16" fill="var(--text-muted)">You alt-tab to a browser.</text>
 <text x="440" y="90" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">You say "focus the game".</text>
 <text x="440" y="124" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">"Elite is in front."</text>
</svg>
<p class="body">D47 will not press a key unless Elite is the window in front, so alt-tabbing away turns flight commands off. The spoken phrase brings Elite back without the mouse.</p>
</section>
<section>
<h2><span class="num">2</span> Windows may refuse, and the reply says so.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="A refused request is reported, not silent">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="16" fill="var(--text-muted)">Windows would not let a background program take the foreground.</text>
 <text x="440" y="90" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">The reply says the taskbar button should be flashing.</text>
</svg>
<p class="body">A program in the background can only ask for the foreground. When Windows refuses, D47 tells you, so you do not mistake the silence for a dead microphone.</p>
</section>
<section>
<h2><span class="num">3</span> Only your spoken phrase can do this.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="The reply is chosen by one line per outcome">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">"Windows would not let me bring Elite forward from the background."</text>
 <text x="440" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">FocusCapability.cs</text>
</svg>
<p class="body"><code>FocusCapability.Describe</code> picks one line per outcome, such as "Elite is already in front." The model cannot call this: a hostile in-game message could otherwise move your focus.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="flight-controls.html"><span class="ct">Flight and navigation →</span><span class="cd">The rule this exists to work around, and why it is not negotiable.</span></a>
<a class="card" href="interface.html"><span class="ct">The window →</span><span class="cd">Where Directive 47 lives when it is not behind Elite.</span></a>
</div>
</div>
</div></div>

## The details

Brings Elite Dangerous back to the front, so flight commands work again.

### Ask for it

> "set focus to game"
> "focus the game"
> "switch to Elite"
> "back to the game"

```text
Elite is in front.
```

### Why this exists

Directive 47 will not press a key unless Elite is the window in front. That check is the one
thing standing between a voice command and typing into your browser, and it is not negotiable.

The awkward consequence is that alt-tabbing away turns every flight command off, and the only
way back was the mouse — the one thing you were trying not to reach for. This is the single
action that could not be delegated to the thing already refusing to act.

### Windows may refuse, and it will say so

**Windows does not let a background application take the foreground.** A program that does not
already have it can only ask, and what usually happens is the taskbar button flashes instead.
Directive 47 has no way around this that does not involve faking keyboard input at the operating
system, which is exactly the thing it promises never to do outside Elite.

So the accurate version: this works when you ask from Directive 47's own window, and Windows will
often refuse it when you ask from somewhere else. When that happens you are told, rather than
left with silence that reads like the microphone having failed:

```text
Windows would not let me bring Elite forward from the background. Its taskbar button should be
flashing; click that, or alt-tab.
```

It also says when there was nothing to do, for the same reason:

```text
Elite is already in front.
```

### The model cannot do this

Only the spoken phrases above reach it, through the keyword router. **It is not a tool the model
can call**, and that is deliberate: your journal, in-game messages, web search results and INARA
are all untrusted text, and anything the model can call, a hostile in-game message can try to
invoke. A message that could yank your focus while you were typing is a nuisance at best.

The phrases are all more than one word on purpose. "Elite" on its own would have been convenient
and is not safe: the router matches phrases anywhere in what you said and runs before the model,
so a bare "Elite" would swallow *"what is my Elite rank in combat"* and answer it by moving a
window. Elite is the top rank in every career the game has.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `focus_the_game`

Brings Elite Dangerous to the foreground. Takes no arguments, and is **protected** — reachable
from the keyword router and never from the model.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

Its own capability rather than a tool on an existing one: the keyword router reaches a
capability's *first* argument-free tool, so hanging this on a capability that already has one would
make it unreachable without a model — which is the configuration it exists for.

`SingleInstance` calls `SetForegroundWindow` too and discards the result. That is not evidence
this works: a second copy of d47 being launched is one of the cases Windows exempts, and this is
not. The foreground is re-read after the call rather than trusting the return value, because it
can report success for a call that only flashed.

</details>
