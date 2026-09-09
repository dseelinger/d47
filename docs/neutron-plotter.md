---
title: Neutron Plotter
group: Knowledge
nav_order: 201
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.

  One planner per page, asked for 2026-08-23. routes.html describes all three at once, which
  is right for the capability and wrong for the card: a Commander asking what Efficiency does
  read three planners' worth of prose and had to work out which third was theirs.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">A route across the galaxy that detours through neutron stars — the one thing the in-game galaxy map will not plot for you.</p>
<section>
<h2><span class="num">1</span> The galaxy map plots this too. Badly, and not very far.</h2>
<svg viewBox="0 0 880 262" role="img" aria-label="The galaxy map plots a straight line in short hops; the Neutron Plotter detours through neutron stars and reaches across the galaxy">
 <rect x="20" y="30" width="840" height="94" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="120" y="62" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text-muted)">GALAXY MAP</text>
 <circle cx="240" cy="92" r="7" fill="var(--text-muted)"/>
 <circle cx="330" cy="92" r="7" fill="var(--text-muted)"/>
 <circle cx="420" cy="92" r="7" fill="var(--text-muted)"/>
 <circle cx="510" cy="92" r="7" fill="var(--text-muted)"/>
 <circle cx="600" cy="92" r="7" fill="var(--text-muted)"/>
 <line x1="240" y1="92" x2="600" y2="92" stroke="var(--text-muted)" stroke-width="2.5" stroke-linecap="round"/>
 <text x="730" y="88" text-anchor="middle" font-size="15" fill="var(--text-muted)">a straight line,</text>
 <text x="730" y="110" text-anchor="middle" font-size="15" fill="var(--text-muted)">in short hops</text>
 <rect x="20" y="138" width="840" height="94" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="120" y="170" text-anchor="middle" font-size="15" font-weight="800" fill="var(--text)">THIS ONE</text>
 <circle cx="240" cy="200" r="7" fill="var(--text-muted)"/>
 <circle cx="360" cy="176" r="10" fill="var(--accent)"/>
 <circle cx="480" cy="212" r="10" fill="var(--accent)"/>
 <circle cx="600" cy="184" r="10" fill="var(--accent)"/>
 <line x1="240" y1="200" x2="360" y2="176" stroke="var(--accent-muted)" stroke-width="2.5" stroke-linecap="round"/>
 <line x1="360" y1="176" x2="480" y2="212" stroke="var(--accent-muted)" stroke-width="2.5" stroke-linecap="round"/>
 <line x1="480" y1="212" x2="600" y2="184" stroke="var(--accent-muted)" stroke-width="2.5" stroke-linecap="round"/>
 <text x="730" y="196" text-anchor="middle" font-size="15" fill="var(--text)">detours through neutron</text>
 <text x="730" y="218" text-anchor="middle" font-size="15" fill="var(--text)">stars, and reaches</text>
 <text x="440" y="256" text-anchor="middle" font-size="15" fill="var(--text-muted)">A supercharged drive jumps about four times as far, so the longer way round is the shorter way there.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Efficiency is backwards from how it sounds.</h2>
<svg viewBox="0 0 880 258" role="img" aria-label="A lower efficiency number wanders further from the direct line and finishes in fewer jumps; a higher one holds the line and takes more">
 <rect x="20" y="34" width="400" height="132" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="72" text-anchor="middle" font-size="19" font-weight="800" fill="var(--text)">LOWER — say 25</text>
 <text x="220" y="106" text-anchor="middle" font-size="15" fill="var(--text-muted)">wanders further off the line</text>
 <text x="220" y="130" text-anchor="middle" font-size="15" fill="var(--text-muted)">finds more neutron stars</text>
 <text x="220" y="154" text-anchor="middle" font-size="15" font-weight="700" fill="var(--accent)">finishes in FEWER jumps</text>
 <rect x="460" y="34" width="400" height="132" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="72" text-anchor="middle" font-size="19" font-weight="800" fill="var(--text)">HIGHER — say 95</text>
 <text x="660" y="106" text-anchor="middle" font-size="15" fill="var(--text-muted)">holds the direct line</text>
 <text x="660" y="130" text-anchor="middle" font-size="15" fill="var(--text-muted)">passes neutron stars by</text>
 <text x="660" y="154" text-anchor="middle" font-size="15" font-weight="700" fill="var(--text-muted)">takes MORE jumps</text>
 <rect x="20" y="184" width="840" height="52" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="216" text-anchor="middle" font-size="16" fill="var(--text)">60 out of the box. It is not a quality dial — it is how much detour you will tolerate.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> It is a job, not a question. And the answer is the next few stops.</h2>
<svg viewBox="0 0 880 260" role="img" aria-label="A plot is submitted and queued; a Sol to Colonia route is 131 waypoints, of which the first five are read out">
 <rect x="20" y="30" width="256" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="148" y="66" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">SUBMITTED</text>
 <text x="148" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">and queued, not asked</text>
 <line x1="288" y1="78" x2="324" y2="78" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="338,78 322,70 322,86" fill="var(--accent-muted)"/>
 <rect x="348" y="30" width="256" height="96" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="476" y="66" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">UP TO 90s</text>
 <text x="476" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">Sol to Colonia took 3</text>
 <line x1="616" y1="78" x2="652" y2="78" stroke="var(--accent-muted)" stroke-width="3" stroke-linecap="round"/>
 <polygon points="666,78 650,70 650,86" fill="var(--accent-muted)"/>
 <rect x="676" y="30" width="184" height="96" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="768" y="66" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">131 STOPS</text>
 <text x="768" y="96" text-anchor="middle" font-size="15" fill="var(--text-muted)">168 jumps</text>
 <rect x="20" y="146" width="840" height="88" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="178" text-anchor="middle" font-size="16" fill="var(--text)">Spoken, you get the totals and the next handful — which is how a route is flown anyway.</text>
 <text x="440" y="206" text-anchor="middle" font-size="15" fill="var(--text-muted)">The whole thing is kept. The Routing tab draws every waypoint, however it was plotted.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="road-to-riches.html"><span class="ct">Road to Riches →</span><span class="cd">The other reason to fly a long way: a loop of bodies worth scanning.</span></a>
<a class="card" href="trade-run.html"><span class="ct">Trade run →</span><span class="cd">The third card on that page, and the one that is not a plot at all.</span></a>
<a class="card" href="capabilities/routes.html"><span class="ct">Route planning →</span><span class="cd">What all three share: the service, the waiting, and what a plan is.</span></a>
</div>
</div>
</div></div>

## The details

The card called **Neutron Plotter** on the Routing tab's Plan page, and the `plot_route` tool
behind it. It was called *Jump route* until 2026-08-23, which described what the galaxy map already
does and said nothing about the one thing this does that the map cannot.

### What you fill in

| Box | What it means | Left blank |
|---|---|---|
| **Destination** | Where you are going | Required — nothing is plotted without it |
| **From** | Where to start | Where you are now, from the journal |
| **Jump range (ly)** | Your laden jump range | This ship's, from the journal |
| **Efficiency** | How far off the direct line you will go | 60 |

**Your ship fills in its own numbers.** Nobody says *"plot me a route to Colonia from Sol at 52.31
light years a jump"*, so jump range and origin come from the journal unless you overrule them.
Typing a range is for planning a trip in a ship you are not sitting in.

### Efficiency, again, because it reads backwards

A **lower** number produces a **shorter** route. Efficiency is how strictly the plotter holds to the
straight line between you and your destination; loosening it lets the route wander to pick up more
neutron stars, and each supercharge is worth about four jumps. So:

- **25** — a wandering route with many supercharges, and the fewest jumps.
- **60** — the default, and a sensible one.
- **95** — near enough the straight line, few supercharges, most jumps.

There is no setting that means "best". There is only how much detour you are willing to fly.

### Supercharging costs you something

Each neutron waypoint means dropping into the jet cone, which takes a slice of your hull integrity
if you linger and leaves your drive needing repair sooner. Directive 47 does not model that, and a
route with ninety supercharges in it is ninety opportunities to get it wrong. Carry an AFMU.

### What comes back

A Sol-to-Colonia plot is **131 waypoints and 168 jumps**. Spoken, you get the totals and the next
handful of stops; the neutron flag rides on the waypoint rather than in a preamble, because it is
the only part that changes what you do on arrival.

```text
Sol to Colonia: 22,000 light years, 168 jumps across 131 waypoints.

The first 5:
  PSR J1752-2806 — 10 jumps; neutron, supercharge here; 21,629 ly left
  Nova Aquila No 3 — 6 jumps; neutron, supercharge here; 21,350 ly left
  ...

126 more after that. Ask again from further along and I will plot the rest.
```

**The cap is on the speaking, not on the plan.** The whole route is kept, and the Routing tab draws
every waypoint of it — including one plotted by voice, because both paths write and read one plan
rather than each keeping their own.

### It is a job, and it can be refused

A plot is submitted and queued rather than asked and answered. The budget is ninety seconds, against
fifteen for an ordinary lookup, because a Commander asking for a route across the galaxy has
knowingly asked for arithmetic. Past that the answer says so, and says what to change: a shorter
route, or a higher efficiency.

The tool schema and the service it calls are on
[Route planning](capabilities/routes.html) — one page for all three planners, because that part
really is shared.
