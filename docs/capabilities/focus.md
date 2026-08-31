---
title: Focus the game
group: Interface
nav_order: 127
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and that is load-bearing rather than cosmetic.
  HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under that class
  would silently become what the in-app panel draws on this page. The docs site styles the two
  identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. That separation is the
  whole point of there being two, and it is the thing that will erode first.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="lede">Two steps to getting the game back in front.</p>
<section>
<h2><span class="num">1</span> Say it, from anywhere.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">focus the game</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"bring Elite back" — "put the game in front"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">It works while D47 has the keyboard, which is exactly when you need it.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Or press the hotkey.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="Hotkeys">
 <rect x="20" y="16" width="840" height="212" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">Hotkeys</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Show or hide the overlay</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">Ctrl+Alt+O</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Move the overlay</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text-muted)">Ctrl+Alt+M</text>
 <text x="44" y="222" font-size="15" fill="var(--text-muted)">These are claimed system-wide, so they work with Elite in front.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="It cannot start Elite for you.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
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
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">Bringing Elite back to the front, so flight commands work again.</p>
<section>
<h2><span class="num">1</span> The one action that could not be left to the rule.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="Keys go out only while Elite is in front, so alt-tabbing away turns every flight command off">
 <rect x="20" y="44" width="380" height="100" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="210" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">ELITE IN FRONT</text>
 <text x="210" y="114" text-anchor="middle" font-size="15" fill="var(--text-muted)">keys go out</text>
 <rect x="460" y="44" width="400" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="84" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">ANYTHING ELSE IN FRONT</text>
 <text x="660" y="114" text-anchor="middle" font-size="15" fill="var(--text-muted)">every flight command is off</text>
 <text x="440" y="192" text-anchor="middle" font-size="16" fill="var(--text)">That check is the one thing standing between a voice command and your browser.</text>
 <text x="440" y="222" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So this is the one action that could not be left to the thing already refusing to act.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Windows may refuse, and it says so.</h2>
<svg viewBox="0 0 880 232" role="img" aria-label="A background application cannot take the foreground, and Directive 47 reports that rather than going quiet">
 <rect x="20" y="36" width="840" height="90" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="46" y="74" text-anchor="start" font-size="16" fill="var(--text)">“Windows would not let me bring Elite forward from the background.</text>
 <text x="46" y="104" text-anchor="start" font-size="16" fill="var(--text)">Its taskbar button should be flashing; click that, or alt-tab.”</text>
 <text x="440" y="166" text-anchor="middle" font-size="16" fill="var(--text)">A background application cannot take the foreground. It can only ask.</text>
 <text x="440" y="198" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So you are told, rather than left with a silence that reads like a dead microphone.</text>
 <text x="440" y="226" text-anchor="middle" font-size="15" fill="var(--text-muted)">It works from Directive 47’s own window, and Windows will often refuse it from anywhere else.</text>
</svg>
<p class="body">There is no way around this that does not involve faking keyboard input at the operating system — which is exactly the thing Directive 47 promises never to do outside Elite.</p>
</section>
<section>
<h2><span class="num">3</span> Every phrase is more than one word, on purpose.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="A bare Elite would match inside a question about Elite rank, so the phrases are all longer">
 <rect x="20" y="40" width="400" height="110" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="84" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text-muted)">“Elite”</text>
 <text x="220" y="116" text-anchor="middle" font-size="15" fill="var(--text-muted)">would have been convenient</text>
 <text x="220" y="138" text-anchor="middle" font-size="14" fill="var(--text-muted)">and is not safe</text>
 <line x1="432" y1="86" x2="448" y2="86" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="462,86 446,78 446,94" fill="var(--accent-muted)"/>
 <rect x="474" y="40" width="386" height="110" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="667" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--danger)">IT WOULD SWALLOW</text>
 <text x="667" y="110" text-anchor="middle" font-size="15" fill="var(--text)">“what is my Elite rank</text>
 <text x="667" y="134" text-anchor="middle" font-size="15" fill="var(--text)">in combat”</text>
 <text x="440" y="194" text-anchor="middle" font-size="16" fill="var(--text)">Elite is the top rank in every career the game has — and the router runs before the model.</text>
</svg>
<p class="body">Only the spoken phrases reach this. It is not a tool the model can call: your journal, in-game messages, search results and Inara are all untrusted text, and a message that could yank your focus while you were typing is a nuisance at best.</p>
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

So the honest version: this works when you ask from Directive 47's own window, and Windows will
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
make it unreachable without a model — which is the configuration it exists for. Re-anchor was the
other capability shaped that way, and it was retired in 0.94.0.

`SingleInstance` calls `SetForegroundWindow` too and discards the result. That is not evidence
this works: a second copy of d47 being launched is one of the cases Windows exempts, and this is
not. The foreground is re-read after the call rather than trusting the return value, because it
can report success for a call that only flashed.

</details>
