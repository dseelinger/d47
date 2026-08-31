---
title: Acting on its own
group: Acting on the game
nav_order: 132
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
<p class="lede">One switch, one action, and nothing else happens without you.</p>
<section>
<h2><span class="num">1</span> Turn on the arrival honk, if you want it.</h2>
<svg viewBox="0 0 880 192" role="img" aria-label="The setting that lets Directive 47 fire the discovery scanner on arrival, off out of the box">
 <rect x="20" y="24" width="840" height="66" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="46" y="63" font-size="17" fill="var(--text)">Honk on arrival</text>
 <rect x="746" y="42" width="68" height="30" rx="15" fill="var(--border)"/>
 <circle cx="761" cy="57" r="11" fill="var(--surface)"/>
 <text x="836" y="63" text-anchor="end" font-size="15" fill="var(--text-muted)">off</text>
 <text x="20" y="132" font-size="16" fill="var(--text-muted)">Off out of the box. This is the only thing D47 does to your ship unasked.</text>
 <text x="20" y="168" font-size="16" fill="var(--text-muted)">It needs key injection on as well — the master switch on the Flight page.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Jump somewhere. It fires once, after the drop.</h2>
<svg viewBox="0 0 880 132" role="img" aria-label="After a jump completes, one press of the discovery scanner, and nothing further">
 <rect x="20" y="20" width="270" height="66" rx="8" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="60" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">JUMP COMPLETES</text>
 <line x1="302" y1="53" x2="326" y2="53" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="340,53 324,45 324,61" fill="var(--accent-muted)"/>
 <rect x="354" y="20" width="506" height="66" rx="8" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="607" y="60" text-anchor="middle" font-size="16" font-weight="700" fill="var(--text)">ONE PRESS OF THE DISCOVERY SCANNER</text>
 <text x="440" y="120" text-anchor="middle" font-size="15" fill="var(--text-muted)">Once per arrival. Never in supercruise, never twice.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="Elite must be the window in front, and the discovery scanner must be bound to a key">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">Elite must be in front, with the scanner on a key.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">D47 presses your own binding, and never presses anything into another window.</text>
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
<p class="lede">Things Directive 47 does to your ship without being asked. There is one so far, and it is off.</p>
<section>
<h2><span class="num">1</span> Nothing said it, so nothing permitted it.</h2>
<svg viewBox="0 0 880 236" role="img" aria-label="Every other action is permitted by the sentence you said, while an action firing on a journal event has none">
 <rect x="20" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text-muted)">EVERYTHING ELSE</text>
 <text x="220" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">happens because you said so</text>
 <text x="220" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">the sentence is the permission</text>
 <rect x="460" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">AN ACTION ON ITS OWN</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text)">has no sentence behind it</text>
 <text x="660" y="134" text-anchor="middle" font-size="15" fill="var(--text)">nobody asked, nobody is watching</text>
 <text x="440" y="198" text-anchor="middle" font-size="17" font-weight="700" fill="var(--text)">A different kind of thing to agree to, so it gets a different kind of agreement.</text>
 <text x="440" y="226" text-anchor="middle" font-size="15" fill="var(--text-muted)">Off by default, switched on one at a time, and never switchable by the AI.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> One switch per action, not one for the category.</h2>
<svg viewBox="0 0 880 220" role="img" aria-label="A single toggle for the whole category would enable future actions automatically, so each gets its own">
 <rect x="20" y="36" width="400" height="118" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="74" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text-muted)">IF THERE WERE ONE SWITCH</text>
 <text x="220" y="106" text-anchor="middle" font-size="14" fill="var(--text-muted)">the next thing added here</text>
 <text x="220" y="130" text-anchor="middle" font-size="14" fill="var(--text-muted)">would arrive already enabled</text>
 <rect x="460" y="36" width="400" height="118" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="74" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">SO THERE IS ONE EACH</text>
 <text x="660" y="106" text-anchor="middle" font-size="14" fill="var(--text-muted)">permission for one thing</text>
 <text x="660" y="130" text-anchor="middle" font-size="14" fill="var(--text-muted)">is not spent on another</text>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">There is one autonomous action so far, and it is off.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> The arrival honk, and the three things it needs.</h2>
<svg viewBox="0 0 880 244" role="img" aria-label="The discovery scanner needs analysis mode, the scanner in your fire group, and a fire button it can press">
 <rect x="20" y="36" width="270" height="118" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="70" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">ANALYSIS MODE</text>
 <text x="155" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">switching you into it would</text>
 <text x="155" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">be a second thing acting alone</text>
 <rect x="305" y="36" width="270" height="118" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="66" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">SCANNER IN YOUR</text>
 <text x="440" y="88" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">FIRE GROUP</text>
 <text x="440" y="124" text-anchor="middle" font-size="14" fill="var(--text-muted)">it cannot see your fire groups</text>
 <rect x="590" y="36" width="270" height="118" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="725" y="70" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">A FIRE BUTTON</text>
 <text x="725" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">Mouse_1 works</text>
 <text x="725" y="132" text-anchor="middle" font-size="14" fill="var(--text-muted)">a stick does not</text>
 <rect x="20" y="178" width="840" height="52" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="210" text-anchor="middle" font-size="16" fill="var(--text)">“I did not honk: the scanner only fires in analysis mode, and you are in combat mode.”</text>
</svg>
<p class="body">Elite has no honk binding, because the discovery scanner is not a button — it is a fire-group weapon. So this holds <em>your</em> fire button for the six seconds the scan takes. It arms on the jump and fires once you are actually in normal space, and if thirty seconds pass without that, the arm expires rather than waiting to surprise you later.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="flight-controls.html"><span class="ct">Flight and navigation →</span><span class="cd">The keyboard switch underneath this one, and why weapons are excluded.</span></a>
<a class="card" href="callouts.html"><span class="ct">Callouts →</span><span class="cd">The other things that happen without you asking — all of them words, not keys.</span></a>
<a class="card" href="settings.html"><span class="ct">Settings →</span><span class="cd">Where the row lives, and what protected means for a row like it.</span></a>
</div>
</div>
</div></div>

## The details

Things Directive 47 does to your ship without being asked. There is one so far, and it is off.

### Why this is its own page

Everything else Directive 47 does happens because you said something. The sentence you said is
the permission — you asked for the gear, so the gear is what it presses.

An action that fires on a journal event has no sentence behind it. Nobody asked, nobody is
watching for it, and if it goes wrong at the wrong moment you find out from the game rather than
from Directive 47. That is a different kind of thing to agree to, so it gets a different kind of
agreement: **each one is off by default and switched on individually**, and none of them can be
switched on by the AI.

One switch per action, not one for the category. If there were a single "act on your own" toggle,
the next thing added here would arrive already enabled for everyone who wanted the first one —
and that is permission for one thing being spent on another.

### The arrival honk

Fires the discovery scanner after each hyperspace jump.

Elite has no "honk" binding, because the discovery scanner is not a button — it is a fire-group
weapon. It goes off when you hold primary fire in analysis mode with the scanner in your current
fire group. So this holds **your** fire button, for the six seconds the scan takes.

Three things have to be true, and it tells you when they are not:

```text
I did not honk: the discovery scanner only fires in analysis mode, and you are in combat mode.
```

- **Analysis mode.** Switching you into it would be a second thing acting on its own, wearing
  the first one's permission, so it says this instead.
- **The scanner in your current fire group.** Directive 47 cannot see your fire groups. If the
  honk seems to do nothing, this is the reason.
- **A fire button it can press.** On Elite's default keyboard preset that is `Mouse_1`, which
  works. On a stick it does not, and you get told which device it is on.

It arms on the jump and fires once you are actually in normal space, because during the
witchspace tunnel the game has the controls and a held button goes nowhere. If thirty seconds
pass without that happening — you dropped straight into supercruise and left — the arm expires
rather than waiting to surprise you later.

It fires once per jump. It never fires for jumps that happened before Directive 47 started, which
would otherwise mean a honk for every system you visited that afternoon.

### Turning it on

> "turn on the arrival honk"
> "stop honking on arrival"

Or the **Honk on arriving in a system** row in settings. Like every switch that reaches your
keyboard, the AI cannot touch it — it can tell you whether it is on, and that is all.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `describe_autonomous_actions`

Reports which autonomous actions exist and which the Commander has switched on. Reports only; it
cannot change any of them. Takes no arguments.

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

The capability registers this one reporting tool and nothing else. The actions themselves run
from journal events on the tick loop, and the rows that arm them are protected, so there is
deliberately no path from a tool call to an autonomous action being enabled or fired.

</details>
