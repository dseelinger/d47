---
title: Combat
group: Acting on the game
nav_order: 134
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to picking targets and ordering a fighter by voice.</p>
<section>
<h2><span class="num">1</span> Pick a target.</h2>
<p class="say">Say "next hostile", "highest threat" or "next subsystem".</p>
<p class="expect">D47 presses your own targeting binding once.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">next hostile</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"highest threat" — "next subsystem"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">One request is one press of your own targeting key.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Order the fighter.</h2>
<p class="say">Say "fighter orders", "recall the fighter" or "fighter hold fire".</p>
<p class="expect">D47 presses the matching order binding once.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">recall the fighter</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"fighter follow me" — "fighter hold position"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Each order is one press, and does nothing with no fighter out.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">Targeting and fighter orders, each one a single key press.</p>
<section>
<h2><span class="num">1</span> One request is one press.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="Each target request is one press of your own targeting key">
 <rect x="20" y="30" width="400" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">"NEXT HOSTILE"</text>
 <text x="220" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">said once</text>
 <rect x="460" y="30" width="400" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">ONE KEY PRESS</text>
 <text x="660" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">the next hostile is targeted</text>
</svg>
<p class="body">Cycling is a press per step, so say it again for the next one.</p>
</section>
<section>
<h2><span class="num">2</span> Fighter orders need a fighter.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="The order key is pressed whether or not a fighter is out">
 <rect x="20" y="30" width="840" height="90" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--danger)">NO FIGHTER OUT, NO EFFECT</text>
 <text x="440" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">D47 still presses the key; the game ignores it</text>
</svg>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="ship-systems.html"><span class="ct">Ship systems →</span><span class="cd">Chaff, shield cells, ECM and heat sinks.</span></a>
<a class="card" href="flight-controls.html"><span class="ct">Flight and navigation →</span><span class="cd">The switch that has to be on first.</span></a>
</div>
</div>
</div></div>

## The details

Selects and cycles targets, targets wingmen, and gives fighter orders.

### Ask for it

> "target ahead"
> "next hostile"
> "highest threat"
> "next subsystem"
> "target wingman one"
> "wingman nav lock"
> "fighter orders"
> "recall the fighter"
> "fighter hold fire"

### Targeting

"Target ahead" also works in the SRV. Cycling targets, hostiles and wingmen works in normal space
and supercruise; cycling subsystems works in normal space only.

### Fighter orders

The orders work in normal space only. They do nothing in the game while no fighter is out, and
Directive 47 does not check.

### You have to turn it on

Everything here needs **Let Directive 47 press keys in Elite**, off by default and unreachable by
the AI. See [Flight and navigation](flight-controls.md).

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `control_combat`

Select and cycle targets, target wingmen, and give fighter orders. Only the actions listed as
reachable in the current game state will work; anything else comes back with the reason it did
not.

```json
{"type":"object","properties":{"action":{"type":"string","description":"Which action to perform.","enum":["select_target","next_target","previous_target","next_hostile","previous_hostile","highest_threat","next_subsystem","previous_subsystem","target_wingman_1","target_wingman_2","target_wingman_3","wingman_target","wingman_nav_lock","fighter_orders","fighter_dock","fighter_defend","fighter_engage","fighter_focus","fighter_hold_fire","fighter_hold_position","fighter_follow"]},"state":{"type":"string","description":"What to leave it in. Elite binds a single toggle, so asking for \u0022on\u0022 or \u0022off\u0022 checks the game\u0027s own report first and does nothing if it is already there. Defaults to toggling.","enum":["on","off","toggle"]}},"required":["action"],"additionalProperties":false}
```

</details>
