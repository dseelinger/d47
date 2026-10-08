---
title: Mining
group: Knowledge
nav_order: 153
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Four steps to a prospector that reads out only what you are after.</p>
<section>
<h2><span class="num">1</span> Set a target material.</h2>
<p class="say">Say "mining target painite".</p>
<p class="expect">Prospector results read out only painite, and say whether the rock holds enough of it.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a mining target typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">mining target painite</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The material on its own needs no AI.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Add a percentage if you want one.</h2>
<p class="say">Say "mining target platinum above twenty five percent".</p>
<p class="expect">Prospector results say "Above your target" or "Below your target" against that percentage.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a percentage target typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">mining target platinum above twenty five percent</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">A percentage goes through the language model.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Ask how many limpets a load needs.</h2>
<p class="say">Say "how many limpets for ten tonnes of osmium".</p>
<p class="expect">D47 gives the collectors and prospectors to launch, rounded up to whole limpets.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a limpet question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">how many limpets for ten tonnes of osmium</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The count comes from your own finished mining runs.</text>
 <text x="20" y="152" font-size="16" fill="var(--text-muted)">This one needs the language model.</text>
</svg>
</section>
<section>
<h2><span class="num">4</span> Clear it when you are done.</h2>
<p class="say">Say "clear the mining target".</p>
<p class="expect">Prospector results go back to naming every material in the rock, richest first.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a clear command typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">clear the mining target</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">The target stays set across restarts until you clear it.</text>
</svg>
</section>
</div></div>
</details>

## The details

A mining target is one material and, optionally, a percentage. While one is set, the
[prospector results](callouts.md#prospector) callout reads out only that material, and says whether
the rock holds enough of it:

```text
Platinum, 31%. Above your target.
Platinum, 18%. Below your target.
No platinum.
```

A rock at or above the percentage is above it. Without a percentage, the line is the material and
its share of the rock, with "Best you have found this session" when it beats every earlier rock this
session. A rock someone has already mined from ends its line with "Already mined".

The target is kept per Commander in `data/mining.json` until you clear it, so it is still set after a
restart.

### Setting one

Say the material on its own, which needs no language model:

> "mining target painite"

A percentage goes through the language model:

> "mining target platinum above twenty five percent"

The materials are the ring hotspot materials plus Gold, Silver, Osmium, Palladium and Haematite.

### Clearing it

> "clear the mining target"

Prospector results go back to naming every material in the rock, richest first.

### How many limpets

> "how many limpets for ten tonnes of osmium"

The answer comes from your own finished mining runs, read from your journals when d47 starts: the
tonnes you refined against the collectors and prospectors you launched. Each count is rounded up to
whole limpets.

```text
About 2 collectors and 4 prospectors for 10 tonnes, from your 36 past runs of any material: a collector every 5.7 tonnes, a prospector every 3.3 tonnes.
```

With a material, the ratio comes from the runs where that material was the one refined most, once
there are at least three of them. Otherwise it comes from every run, and the answer says so. A run
counts once it ends by docking or by the ship being destroyed, and only if it refined something. Until
d47 has finished reading your older journals it says so and gives no number.

This needs the language model, since the number of tonnes is in the sentence.

### The rest of mining

- **Prospector results** and **Core asteroids** are rows on the [Callouts](callouts.md) page, each
  with its own switch.
- **Which ring to mine** is the `hotspot` filter of `find_body` in [Galaxy search](galaxy.md).
- **Where to sell** is `find_nearest_station` with `selling` in [Galaxy search](galaxy.md).

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `set_mining_target`

```json
{"type":"object","properties":{"material":{"type":"string","description":"The material to mine.","enum":["Alexandrite","Bauxite","Benitoite","Bertrandite","Bromellite","Cobalt","Coltan","Gallite","Gold","Grandidierite","Haematite","Hydrogen Peroxide","Indite","Lepidolite","Liquid oxygen","Lithium Hydroxide","Low Temperature Diamonds","Methane Clathrate","Methanol Monohydrate Crystals","Monazite","Musgravite","Osmium","Painite","Palladium","Platinum","Praseodymium","Rhodplumsite","Rutile","Samarium","Serendibite","Silver","Tritium","Uraninite","Void Opal","Water"]},"percent":{"type":"number","description":"The proportion of the rock, 0 to 100, at or above which it is worth mining. Leave out for any amount."}},"required":["material"],"additionalProperties":false}
```

#### `clear_mining_target`

```json
{"type":"object","properties":{},"required":[],"additionalProperties":false}
```

#### `estimate_limpets`

```json
{"type":"object","properties":{"material":{"type":"string","description":"The material to mine. Leave out for any.","enum":["Alexandrite","Bauxite","Benitoite","Bertrandite","Bromellite","Cobalt","Coltan","Gallite","Gold","Grandidierite","Haematite","Hydrogen Peroxide","Indite","Lepidolite","Liquid oxygen","Lithium Hydroxide","Low Temperature Diamonds","Methane Clathrate","Methanol Monohydrate Crystals","Monazite","Musgravite","Osmium","Painite","Palladium","Platinum","Praseodymium","Rhodplumsite","Rutile","Samarium","Serendibite","Silver","Tritium","Uraninite","Void Opal","Water"]},"tonnes":{"type":"number","description":"The tonnes to mine."}},"required":["tonnes"],"additionalProperties":false}
```

</details>
