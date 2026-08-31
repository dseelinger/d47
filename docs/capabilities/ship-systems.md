---
title: Ship systems
group: Acting on the game
nav_order: 129
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">Moving power around, and the two panic buttons: silent running and heat sinks.</p>
<section>
<h2><span class="num">1</span> One request is one press, so ask for four.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="Four pips to engines is sent as four separate presses of your own power key">
 <rect x="20" y="44" width="210" height="88" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="125" y="80" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">“FOUR PIPS</text>
 <text x="125" y="106" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">TO ENGINES”</text>
 <line x1="242" y1="88" x2="266" y2="88" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="280,88 264,80 264,96" fill="var(--accent-muted)"/>
 <rect x="296" y="56" width="84" height="64" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="338" y="94" text-anchor="middle" font-size="15" fill="var(--text)">press</text>
 <rect x="392" y="56" width="84" height="64" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="434" y="94" text-anchor="middle" font-size="15" fill="var(--text)">press</text>
 <rect x="488" y="56" width="84" height="64" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="530" y="94" text-anchor="middle" font-size="15" fill="var(--text)">press</text>
 <rect x="584" y="56" width="84" height="64" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="626" y="94" text-anchor="middle" font-size="15" fill="var(--text)">press</text>
 <line x1="680" y1="88" x2="704" y2="88" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="718,88 702,80 702,96" fill="var(--accent-muted)"/>
 <rect x="730" y="44" width="130" height="88" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="795" y="80" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">ENGINES</text>
 <text x="795" y="108" text-anchor="middle" font-size="16" fill="var(--text-muted)">+4</text>
 <text x="440" y="178" text-anchor="middle" font-size="16" fill="var(--text)">Each request moves power one step — the same as one press of your own key.</text>
 <text x="440" y="210" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">So ask for it the way you would press it.</text>
</svg>
<p class="body">In the SRV the same words reach the SRV's own power bindings, because Elite binds those separately and you meant the vehicle you are sitting in.</p>
</section>
<section>
<h2><span class="num">2</span> The panic buttons are yours to misuse.</h2>
<svg viewBox="0 0 880 220" role="img" aria-label="Heat sinks and silent running work only while flying, and silent running is a toggle">
 <rect x="20" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="78" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">ONLY WHILE FLYING</text>
 <text x="220" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">refused when docked or landed,</text>
 <text x="220" y="134" text-anchor="middle" font-size="15" fill="var(--text-muted)">with the reason</text>
 <rect x="460" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="660" y="76" text-anchor="middle" font-size="15" font-weight="800" fill="var(--danger)">SILENT RUNNING IS A TOGGLE</text>
 <text x="660" y="108" text-anchor="middle" font-size="15" fill="var(--text-muted)">asking twice turns it back off,</text>
 <text x="660" y="132" text-anchor="middle" font-size="15" fill="var(--text-muted)">and nothing here will stop you</text>
 <text x="440" y="198" text-anchor="middle" font-size="16" fill="var(--text-muted)">Directive 47 will not stop you cooking your own ship, and does not pretend otherwise.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> The fuel scoop is not a switch at all.</h2>
<svg viewBox="0 0 880 220" role="img" aria-label="Elite has no fuel scoop binding, because scooping starts by itself in the scoop zone">
 <rect x="20" y="40" width="400" height="104" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">THE FUEL SCOOP</text>
 <text x="220" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">Elite has no binding for it</text>
 <line x1="432" y1="92" x2="448" y2="92" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="462,92 446,84 446,100" fill="var(--accent-muted)"/>
 <rect x="474" y="40" width="386" height="104" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="667" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">SO NOTHING TO PRESS</text>
 <text x="667" y="112" text-anchor="middle" font-size="15" fill="var(--text-muted)">scooping is not something you turn on</text>
 <text x="440" y="180" text-anchor="middle" font-size="16" fill="var(--text)">Fly a scoop-fitted ship into a star’s scoop zone and it starts by itself.</text>
 <text x="440" y="210" text-anchor="middle" font-size="15" fill="var(--text-muted)">It can tell you whether you are scooping. It cannot start it, and neither can you.</text>
</svg>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="flight-controls.html"><span class="ct">Flight and navigation →</span><span class="cd">The switch that has to be on first, and why an action sometimes refuses.</span></a>
<a class="card" href="srv.html"><span class="ct">SRV →</span><span class="cd">The same words, reaching the buggy’s own bindings.</span></a>
<a class="card" href="switches.html"><span class="ct">HOTAS switches →</span><span class="cd">Putting pips and heat sinks on a physical switch instead.</span></a>
</div>
</div>
</div></div>

## The details

Moves power around and reaches the two panic buttons: silent running and heat sinks.

### Ask for it

> "pips to engines"
> "balance the power"
> "heat sink"
> "silent running"

### Pips

Each request moves power one step, the same as one press of your own key. "Four pips to engines"
is four presses, so ask for it the way you would press it.

```text
Pressed Numpad_7 for power to engines.
```

In the SRV the same words reach the SRV's own power bindings, because Elite binds those
separately and you meant the vehicle you are sitting in.

### Heat sinks and silent running

Both work only while you are flying — they are refused when docked or landed, with the reason.
Silent running is worth knowing about before you use it by voice: it is a toggle, so asking twice
turns it back off, and Directive 47 will not stop you cooking your own ship.

### The fuel scoop is not a switch

Elite has no fuel scoop binding, because scooping is not something you turn on. Fly a
scoop-fitted ship into a star's scoop zone and it starts by itself.

So there is nothing here to press, and asking for it gets an answer rather than a keystroke —
Directive 47 can tell you whether you are currently scooping, because the game reports that, but
it cannot start or stop it and neither can you.

### You have to turn it on

Everything here needs **Let Directive 47 press keys in Elite**, which is off by default and
cannot be switched on by the AI. See [Flight and navigation](flight-controls.md) for why, and for
what happens when an action is on your stick rather than your keyboard.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `control_systems`

Move power between engines, weapons and systems, and reach silent running and heat sinks. Only
the actions listed as reachable in the current game state will work; anything else comes back
with the reason it did not.

```json
{"type":"object","properties":{"action":{"type":"string","description":"Which action to perform.","enum":["power_to_engines","power_to_weapons","power_to_systems","balance_power","silent_running","heat_sink","analysis_mode"]},"state":{"type":"string","description":"What to leave it in. Elite binds a single toggle, so asking for \u0022on\u0022 or \u0022off\u0022 checks the game\u0027s own report first and does nothing if it is already there. Defaults to toggling.","enum":["on","off","toggle"]}},"required":["action"],"additionalProperties":false}
```

`silent_running` resolves to Elite's `ToggleButtonUpInput`, which is the game's own name for it
and has been since long before silent running had a HUD indicator. It is spelled that way here
because that is the only spelling that resolves against a bindings file.

</details>
