---
title: Star charts
group: Knowledge
nav_order: 167
---

<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to the Guardian beacons on the Galaxy map. No AI, key or network needed.</p>
<section>
<h2><span class="num">1</span> Ask for the beacons.</h2>
<p class="say">Say "show me all known Guardian beacons".</p>
<p class="expect">D47 says how many beacon systems it knows and which is nearest you, and the Galaxy map opens.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The ask row with a question typed into it">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">show me all known Guardian beacons</text>
 <text x="836" y="57" text-anchor="end" font-size="15" fill="var(--text-muted)">Ask</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">"show the Guardian beacons" - "where are the Guardian beacons"</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Read the map.</h2>
<p class="say">Look at Astrometrics, Galaxy map.</p>
<p class="expect">The Guardian beacons layer is ticked and the view is fitted to the beacons; your other layers are unchanged.</p>
<svg viewBox="0 0 880 176" role="img" aria-label="The Galaxy map with the Guardian beacons layer ticked">
 <rect x="20" y="24" width="840" height="52" fill="var(--surface)" stroke="var(--accent)" stroke-width="2"/>
 <text x="44" y="57" font-size="17" fill="var(--text)">Guardian beacons</text>
 <text x="20" y="118" font-size="16" fill="var(--text-muted)">Untick the layer in Layers to take it off again.</text>
</svg>
</section>
</div></div>
</details>

<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">D47 knows where the Guardian beacons are and adds them to the map when you ask.</p>
<section>
<h2><span class="num">1</span> The list of beacons ships with D47.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="The beacon list is part of D47">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="16" fill="var(--text-muted)">You say "where are the Guardian beacons".</text>
 <text x="440" y="90" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">D47 answers from its own list.</text>
</svg>
<p class="body">Nothing is fetched. The nearest beacon is measured from your last known position; with no position, D47 gives the count only.</p>
</section>
<section>
<h2><span class="num">2</span> The map follows the answer.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="The answer opens the map">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">The answer points at the Galaxy map.</text>
 <text x="440" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">AstrometricsCapability.cs</text>
</svg>
<p class="body">The window and the headset big panel both add the layer. It changes nothing else, so it is not protected.</p>
</section>
</div></div>
</details>

## The details

Adds a chart to the Galaxy map and says what it holds. Part of every run.

### Ask for it

> "show me all known Guardian beacons"
> "show the Guardian beacons"
> "where are the Guardian beacons"
> "Guardian beacons on the map"
> "show the farming sites"
> "material farming sites on the map"
> "show exobiology by region"
> "show my missions on the map"
> "show my first discoveries"
> "first discoveries on the map"
> "show where I died"
> "my deaths on the map"

The farming phrases add the Farming sites layer instead: a hexagon mark for each system with a material farming site. Select one and the strip lists each site there with its material, method and body. The exobiology phrase adds the Exobiology layer and shows the whole galaxy: each region's ground is filled in one of four steps by how many species d47's exobiology catalogue lists for that region, and D47 says the fewest and the most a region has and, when it knows where you are, the count for your region. The mission phrase adds the Missions layer: a diamond mark for each system an active mission is bound for. Select one and the strip lists each mission there by name, with its station where it has one. The first-discovery phrases add the First discovered layer: a blue four-pointed star for each system where you scanned a body nobody had discovered, placed by your travel log. Wider than 6,400 light years across, the system's visited square is drawn blue in place of the star. Select one and the strip says First discovery and how many bodies. The last two add the Deaths layer: a red crossed mark for each system you died in, placed by your travel log at the arrival before the death. Select one and the strip says Died here and, for more than one death, how many. None of these needs an AI configured.

<details markdown="1">
<summary>The tool surface, for contributors</summary>

#### `show_star_chart`

Adds a chart layer to the Galaxy map and opens the map: the Guardian beacons, the material farming sites, each region filled by how many exobiology species are known there, the destinations of the Commander's active missions, the systems where the Commander was the first to discover a body or the systems the Commander has died in. Answers with how many systems the chart marks and, when the Commander's position is known, the nearest one and its distance; for exobiology, the fewest and most species a region has and the count for the Commander's region.

```json
{"type":"object","properties":{"chart":{"type":"string","description":"The chart to show.","enum":["guardian_beacons","farming_sites","exobiology","mission_destinations","first_discovered","deaths"]}},"required":["chart"],"additionalProperties":false}
```

</details>
