---
title: On foot controls
group: Acting on the game
nav_order: 135
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to using your suit and weapons by voice.</p>
<section>
<h2><span class="num">1</span> Use the suit.</h2>
<p class="say">Say "shields on", "use a medkit" or "energy cell".</p>
<p class="expect">D47 presses your own on-foot binding once.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">use a medkit</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"shields on" — "energy cell" — "mission help"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">One request is one press of your own key.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Switch tool or weapon.</h2>
<p class="say">Say "primary weapon", "sidearm", "suit tool" or "holster".</p>
<p class="expect">D47 presses the matching selection binding once.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">primary weapon</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"frag grenade" — "energylink" — "profile analyser"</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">Each one selects the item; it does not fire it.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">The suit's single-press actions, each one a key press.</p>
<section>
<h2><span class="num">1</span> Shields have one key and no report.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="Elite binds one key for the suit shields and reports no state, so on and off both press it">
 <rect x="20" y="30" width="400" height="90" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">"SHIELDS ON" OR "OFF"</text>
 <text x="220" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">either way</text>
 <rect x="460" y="30" width="400" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">ONE TOGGLE PRESS</text>
 <text x="660" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">Status.json has no suit shield state</text>
</svg>
<p class="body">D47 cannot check the shields first, so it presses the toggle.</p>
</section>
<section>
<h2><span class="num">2</span> The suit tool depends on the suit.</h2>
<svg viewBox="0 0 880 150" role="img" aria-label="One binding selects the genetic sampler on Artemis, the arc cutter on Maverick and nothing on Dominator">
 <rect x="20" y="30" width="840" height="90" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="70" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">ONE KEY, THREE SUITS</text>
 <text x="440" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">genetic sampler · arc cutter · nothing on Dominator</text>
</svg>
<p class="body">"Suit tool", "genetic sampler" and "arc cutter" all press the same binding.</p>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="on-foot.html"><span class="ct">On foot →</span><span class="cd">What Directive 47 knows about suits and their upgrades.</span></a>
<a class="card" href="flight-controls.html"><span class="ct">Flight and navigation →</span><span class="cd">The switch that has to be on first.</span></a>
</div>
</div>
</div></div>

## The details

Uses the suit's shields, medkit and energy cell, selects tools, weapons and grenades, and opens
the mission help panel. These work on foot only. See [On foot](on-foot.md) for what Directive 47
knows about suits.

### Ask for it

> "shields on"
> "use a medkit"
> "energy cell"
> "energylink"
> "profile analyser"
> "suit tool"
> "primary weapon"
> "sidearm"
> "holster"
> "frag grenade"
> "mission help"

### Shields

Elite binds one key for the suit shields and Status.json reports no suit shield state, so "shields
on" and "shields off" both press the toggle.

### The suit tool

"Suit tool", "genetic sampler" and "arc cutter" press one binding, which selects whichever tool the
suit carries: the genetic sampler on Artemis, the arc cutter on Maverick, none on Dominator.

### You have to turn it on

Everything here needs **Let Directive 47 press keys in Elite**, off by default and unreachable by
the AI. See [Flight and navigation](flight-controls.md).

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `control_on_foot`

Use the suit's shields, medkit and energy cell, switch tools and weapons, and open the mission help panel. Only the actions listed as
reachable in the current game state will work; anything else comes back with the reason it did
not.

```json
{"type":"object","properties":{"action":{"type":"string","description":"Which action to perform.","enum":["suit_shields","medkit","energy_cell","energylink","profile_analyser","genetic_sampler","primary_weapon","secondary_weapon","utility_weapon","holster","frag_grenade","emp_grenade","shield_grenade","mission_help"]},"state":{"type":"string","description":"What to leave it in. Elite binds a single toggle, so asking for \u0022on\u0022 or \u0022off\u0022 checks the game\u0027s own report first and does nothing if it is already there. Defaults to toggling.","enum":["on","off","toggle"]}},"required":["action"],"additionalProperties":false}
```

</details>
