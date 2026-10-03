---
title: Exobiology route
group: Knowledge
nav_order: 206
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">A circuit of nearby systems where somebody has already found valuable biology, with what each species pays.</p>
<section>
<h2><span class="num">1</span> It sends you to biology that is already known.</h2>
<svg viewBox="0 0 880 226" role="img" aria-label="Each stop is a system with surveyed biology; each body lists its species and what they pay">
 <rect x="20" y="30" width="400" height="130" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="66" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">A STOP</text>
 <text x="220" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">a system, its jumps,</text>
 <text x="220" y="122" text-anchor="middle" font-size="15" fill="var(--text-muted)">and how many bodies to land on</text>
 <rect x="460" y="30" width="400" height="130" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="66" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">A BODY</text>
 <text x="660" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">what its biology is worth,</text>
 <text x="660" y="122" text-anchor="middle" font-size="15" fill="var(--text-muted)">and each species on it with its price</text>
 <text x="440" y="200" text-anchor="middle" font-size="16" fill="var(--text)">The most valuable body at each stop is listed first.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> None of it is a first footfall.</h2>
<svg viewBox="0 0 880 200" role="img" aria-label="The route only knows what somebody has already surveyed, so the first-footfall bonus is already taken">
 <rect x="20" y="30" width="840" height="100" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="70" text-anchor="middle" font-size="16" fill="var(--text)">The route only knows biology that somebody has already surveyed and reported.</text>
 <text x="440" y="100" text-anchor="middle" font-size="15" fill="var(--text-muted)">The first-footfall bonus goes to whoever landed first, so it is not on offer here.</text>
 <text x="440" y="172" text-anchor="middle" font-size="16" fill="var(--text)">For undiscovered biology, read a system name instead.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> Stops, radius and a floor, as on Road to Riches.</h2>
<svg viewBox="0 0 880 200" role="img" aria-label="Stops, radius and the least a body's biology must be worth, with their defaults">
 <rect x="20" y="30" width="270" height="120" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="155" y="68" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">STOPS</text>
 <text x="155" y="100" text-anchor="middle" font-size="15" fill="var(--text-muted)">how many systems</text>
 <text x="155" y="130" text-anchor="middle" font-size="15" fill="var(--text)">10 out of the box</text>
 <rect x="305" y="30" width="270" height="120" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="68" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">RADIUS</text>
 <text x="440" y="100" text-anchor="middle" font-size="15" fill="var(--text-muted)">how far out to look</text>
 <text x="440" y="130" text-anchor="middle" font-size="15" fill="var(--text)">200 ly out of the box</text>
 <rect x="590" y="30" width="270" height="120" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="725" y="68" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">WORTH STOPPING</text>
 <text x="725" y="100" text-anchor="middle" font-size="15" fill="var(--text-muted)">the least a body must pay</text>
 <text x="725" y="130" text-anchor="middle" font-size="15" fill="var(--text)">1,000,000 cr out of the box</text>
 <text x="440" y="186" text-anchor="middle" font-size="15" fill="var(--text-muted)">Coming back to the start is on out of the box, and can be turned off.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="road-to-riches.html"><span class="ct">Road to Riches →</span><span class="cd">A circuit of bodies worth scanning and mapping, rather than landing on.</span></a>
<a class="card" href="capabilities/exobiology.html"><span class="ct">Exobiology →</span><span class="cd">What your own scans found, and the tool behind this card.</span></a>
<a class="card" href="capabilities/routes.html"><span class="ct">Route planning →</span><span class="cd">The service, the waiting, and plotting the next stop.</span></a>
</div>
</div>
</div></div>

## The details

The card called **Exobiology** on the Navigation tab's Plan page, and the `plot_exobiology_route` tool
behind it.

### What you fill in

| Box | What it means | Out of the box |
|---|---|---|
| **Stops** | How many systems the circuit visits | 10 |
| **Radius (ly)** | How far from the start it may look | 200 |
| **Least worth stopping for (cr)** | The least a body's biology must be worth | 1,000,000 |
| **Come back to the start** | Whether the circuit ends where it started | On |

It starts from where you are, at this ship's jump range.

### What the plan shows

Each stop shows its system, the jumps to reach it and how many bodies are worth landing on. Under
it, each body shows what its biology is worth, most valuable first, and each species on it with its
price. Arriving at a stop marks it reached, and "plot next exobiology stop" puts the next system in
the galaxy map.

### None of it is a first footfall

The route is built from biology somebody has already surveyed and reported. The first-footfall
bonus is paid only where nobody has landed, so it is never part of these figures. For undiscovered
systems, [read a system name](capabilities/system-names.html) instead.

The tool schema is on [Exobiology](capabilities/exobiology.html), and the next-stop command on
[Route planning](capabilities/routes.html).
