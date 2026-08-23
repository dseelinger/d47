---
title: Trade run
group: Knowledge
nav_order: 203
---

<!--
  The ELI5 band. Editing rules, both about kramdown rather than taste: no blank lines inside
  this block, and never indent a line by four spaces or more — either can end the raw HTML
  span early and leave half a diagram rendered as text. The site needs Ruby to build, which
  is not available here, so a mistake shows up published.

  Colours are the nine Palette roles and nothing else — see .d47-eli5 in assets/main.scss.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">A circuit of markets worked out here, from the station you are docked at — and the one number Directive 47 will not read from your journal.</p>
<section>
<h2><span class="num">1</span> Your balance is asked for, never taken.</h2>
<svg viewBox="0 0 880 250" role="img" aria-label="Jump range and cargo capacity come from the journal; the credit balance is typed every time and never saved">
 <rect x="20" y="30" width="400" height="130" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="66" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">FROM THE JOURNAL</text>
 <text x="220" y="100" text-anchor="middle" font-size="15" fill="var(--text-muted)">where you are docked</text>
 <text x="220" y="124" text-anchor="middle" font-size="15" fill="var(--text-muted)">your cargo capacity</text>
 <text x="220" y="148" text-anchor="middle" font-size="15" fill="var(--text-muted)">— both are facts about the ship</text>
 <rect x="460" y="30" width="400" height="130" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="66" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">TYPED, EVERY TIME</text>
 <text x="660" y="100" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">what you are worth</text>
 <text x="660" y="130" text-anchor="middle" font-size="15" fill="var(--text-muted)">never read, never saved,</text>
 <text x="660" y="154" text-anchor="middle" font-size="15" fill="var(--text-muted)">never inferred</text>
 <text x="440" y="200" text-anchor="middle" font-size="16" fill="var(--text)">Your balance is in the journal. It is the one figure here that is about you rather than your ship.</text>
 <text x="440" y="232" text-anchor="middle" font-size="15" fill="var(--text-muted)">Cargo capacity is a property of the hull. What you are worth is nobody's business but yours.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> This one is not a plot at all.</h2>
<svg viewBox="0 0 880 244" role="img" aria-label="The other planners submit a job to the service; the trade run asks for markets and does the arithmetic on this machine">
 <rect x="20" y="30" width="400" height="118" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="220" y="66" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text-muted)">THE OTHER TWO</text>
 <text x="220" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">submit a job, wait for it,</text>
 <text x="220" y="122" text-anchor="middle" font-size="15" fill="var(--text-muted)">and read back the answer</text>
 <rect x="460" y="30" width="400" height="118" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="660" y="66" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">THIS ONE</text>
 <text x="660" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">asks what the markets hold,</text>
 <text x="660" y="122" text-anchor="middle" font-size="15" fill="var(--text-muted)">then does the sums here</text>
 <rect x="20" y="168" width="840" height="60" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="196" text-anchor="middle" font-size="16" fill="var(--text)">Which is what lets it hold cargo past a poor buyer and still come home.</text>
 <text x="440" y="220" text-anchor="middle" font-size="15" fill="var(--text-muted)">A planner that sold everything at every stop would be a simpler planner and a worse one.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> A price is a report, and it has a date on it.</h2>
<svg viewBox="0 0 880 234" role="img" aria-label="Every trade stop carries when its market was last reported; an old price can make a perfect route worthless">
 <rect x="20" y="30" width="270" height="104" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="155" y="66" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">YESTERDAY</text>
 <text x="155" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">somebody was there</text>
 <text x="155" y="120" text-anchor="middle" font-size="15" fill="var(--text-muted)">and reported it</text>
 <rect x="305" y="30" width="270" height="104" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="66" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text-muted)">LAST MONTH</text>
 <text x="440" y="98" text-anchor="middle" font-size="15" fill="var(--text-muted)">the arithmetic still</text>
 <text x="440" y="120" text-anchor="middle" font-size="15" fill="var(--text-muted)">works perfectly</text>
 <rect x="590" y="30" width="270" height="104" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="725" y="66" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">FOUR YEARS AGO</text>
 <text x="725" y="98" text-anchor="middle" font-size="15" font-weight="700" fill="var(--danger)">and is worth nothing</text>
 <text x="725" y="120" text-anchor="middle" font-size="15" fill="var(--text-muted)">at all</text>
 <rect x="20" y="152" width="840" height="60" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="440" y="180" text-anchor="middle" font-size="16" fill="var(--text)">So every stop says when its market was last reported, beside what it says the price is.</text>
 <text x="440" y="204" text-anchor="middle" font-size="15" fill="var(--text-muted)">The same reason outfitting stock carries a date. A figure with no date invites you to trust it.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="neutron-plotter.html"><span class="ct">Neutron Plotter →</span><span class="cd">Getting somewhere far away, through neutron stars.</span></a>
<a class="card" href="road-to-riches.html"><span class="ct">Road to Riches →</span><span class="cd">Earning on the way out rather than by carrying cargo.</span></a>
<a class="card" href="capabilities/routes.html"><span class="ct">Route planning →</span><span class="cd">What all three share: the service, the waiting, and what a plan is.</span></a>
</div>
</div>
</div></div>

## The details

The card called **Trade run** on the Routing tab's Plan page, and the `plot_trade_route` tool
behind it.

### What you fill in

| Box | What it means | Out of the box |
|---|---|---|
| **Credits to trade with** | Your working capital | **Required** — never inferred |
| **Hops** | How many buy-and-sell legs | 5 |
| **Longest leg (ly)** | The furthest one hop may reach | 40 |
| **End where it started** | Whether the circuit closes | Unticked |
| **Large pads only** | Skip stations your ship cannot land at | Unticked |

It plans **from the station you are docked at**. There is no origin box, because a trade run that
starts somewhere you are not is a trade run that starts with an empty leg.

### Why the balance is typed every time

Your credit balance *is* in the journal, and Directive 47 does not read it. It is the one figure on
this page that is about you rather than about your ship — cargo capacity is a property of the hull
and the route means nothing without it, but what you are worth is nobody's business but yours. It
is not saved either, so it is typed each time.

If that annoys you, it is meant to be a small annoyance in exchange for a promise that is easy to
check: search the code for your balance and it is not there.

### It is arithmetic here, not a job over there

The other two planners submit a job and wait. This one asks the service what the markets around you
hold, and then works out the circuit on your machine. That is why it can do the thing a
sell-everything-at-every-stop planner cannot: **hold cargo past a poor buyer** and carry both
credits and cargo between hops, because sometimes the move is not to sell.

It is also why it is quicker, and why the waiting message is different — *"Working it out…"*
rather than the queued-job wording the others use.

### Large pads, and the stop you cannot land at

**Large pads only** is not a preference. A route with a medium-pad station in it is a route with a
leg you cannot fly in a Type-9, and the plan will not tell you at the point of plotting which stop
that is. Tick it if your ship needs it.

### Every stop carries a date

A trade stop says when its market was last reported, for the same reason outfitting stock does. A
route can be arithmetically perfect against a four-year-old price and worth nothing at all. There
is a bound on how old a price may be — `max_price_age` on the tool — and it is the same idea the
rest of the market answers inherit.

### Fleet carriers

A carrier's market is player-set and can be a joke, and the carrier itself may be a hundred light
years away by the time you arrive. Treat any leg that names one with more suspicion than the rest.

The tool schema and the service it calls are on
[Route planning](capabilities/routes.html) — one page for all three planners, because that part
really is shared.
