---
title: Powerplay ranks
group: Knowledge
nav_order: 149
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Ask what a Powerplay rank gives, for your own Power or any other.</p>
<section>
<h2><span class="num">1</span> Ask about your next rank.</h2>
<p class="say">Say "what does my next Powerplay rank give me".</p>
<p class="expect">D47 answers from your pledge in the journal and the table of ranks.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question about the next Powerplay rank typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what does my next Powerplay rank give me</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">D47 answers from the pledge in your journal and the table of ranks.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">It needs a Powerplay event in the journal; load the game once if it has none.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Ask about another Power.</h2>
<p class="say">Say "what would Edmund Mahon give me".</p>
<p class="expect">You hear everything that Power gives at rank one hundred, and the first module it unlocks with its rank.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="A question naming another Power">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">what would Edmund Mahon give me</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Without a rank, you hear everything that Power gives at rank 100.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">The first module it unlocks is named with its rank.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>Why it works this way</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">One table of ranks, read three ways: what you hold, what is next, what is far off.</p>
<section>
<h2><span class="num">1</span> Merits only for your own Power.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="Merits to a rank are given for the pledged Power only">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="62" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">Your Power: "Rank 9, 1,708 merits away."</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">Another Power: the rank alone, because your merits count towards your own.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Three modules named, the rest counted.</h2>
<svg viewBox="0 0 880 176" role="img" aria-label="Three modules read out and the others counted">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="62" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">Modules unlocked: A, B, C and 4 more.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">A longer list is more than anyone listens to.</text>
</svg>
</section>
</div></div>
</details>

<div class="d47-eli5"><div class="d47-frame">
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="galaxy.html"><span class="ct">Galaxy search →</span><span class="cd">Ask how to get a Powerplay module.</span></a>
<a class="card" href="goals.html"><span class="ct">Goals →</span><span class="cd">Track a Powerplay rank as a goal.</span></a>
</div>
</div>
</div></div>

## The details

What a Powerplay rank gives, read from the table of ranks. Part of every run once a game is loaded.

### Ask for it

> "what does my next Powerplay rank give me"
> "what would Mahon give me"

```text
Rank 8 with Li Yong-Rui. In Li Yong-Rui's territory your rebuy is 40% lower. Rank 9, 1,708 merits away, gives a mini care package. The next perk is at rank 11, 17,708 merits away: 33% off rebuy when a rival Power's ship kills you outside Li Yong-Rui's territory. The first module is the Pack-Hound Missile Rack at rank 34, 201,708 merits away.
```

Perks and rebuy reductions are said at their value at that rank. Merits to a rank are given only for your
own Power and only once D47 has read your merits. With no pledge and no Power named, the answer asks for a
Power. At rank 100 it says every further rank gives a full care package.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `get_powerplay_rewards`

Reads what a Power's rank gives: the perks held, the modules unlocked, the next rank, the next perk and the
next module. It changes nothing and sends nothing off the machine.

```json
{"type":"object","properties":{"power":{"type":"string","description":"The Power to ask about. Defaults to the Power the Commander is pledged to.","enum":["Zemina Torval","Felicia Winters","Li Yong-Rui","Archon Delaine","Nakato Kaine","Jerome Archer","Denton Patreus","Pranav Antal","Yuri Grom","A. Lavigny-Duval","Edmund Mahon","Aisling Duval"]},"rank":{"type":"integer","description":"A rank from 1 to 100. Defaults to the Commander\u0027s rank for their own Power, and to 100 for any other."}},"required":[],"additionalProperties":false}
```

</details>
