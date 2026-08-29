---
title: Route planning
group: Knowledge
nav_order: 105
---

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<div class="d47-eli5"><div class="d47-frame">
<p class="lede">A neutron route, a Road to Riches loop, or a trade run that does not have to empty the hold.</p>
<section>
<h2><span class="num">1</span> Plotting is not searching. It is a job.</h2>
<svg viewBox="0 0 880 240" role="img" aria-label="A search is a request and a reply; a plot is submitted and waited on; a trade route is computed locally">
 <rect x="20" y="44" width="250" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="145" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text-muted)">A SEARCH</text>
 <text x="145" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">a request and a reply</text>
 <text x="145" y="130" text-anchor="middle" font-size="14" fill="var(--text-muted)">waits 15 seconds</text>
 <rect x="315" y="44" width="250" height="100" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">A PLOT</text>
 <text x="440" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">submitted, queued, waited on</text>
 <text x="440" y="130" text-anchor="middle" font-size="14" fill="var(--text-muted)">waits 90 seconds</text>
 <rect x="610" y="44" width="250" height="100" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="735" y="80" text-anchor="middle" font-size="17" font-weight="800" fill="var(--text)">A TRADE ROUTE</text>
 <text x="735" y="108" text-anchor="middle" font-size="14" fill="var(--text-muted)">not a plot at all —</text>
 <text x="735" y="130" text-anchor="middle" font-size="14" fill="var(--text-muted)">the arithmetic happens here</text>
 <text x="440" y="190" text-anchor="middle" font-size="16" fill="var(--text)">A Commander who asks for a route across the galaxy has knowingly asked for arithmetic.</text>
 <text x="440" y="220" text-anchor="middle" font-size="15" fill="var(--text-muted)">Past ninety seconds it says so, and says what to change: a shorter route, fewer hops, a smaller radius.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Your ship fills in its own numbers. You are not your ship.</h2>
<svg viewBox="0 0 880 236" role="img" aria-label="Jump range, origin and cargo capacity come from the journal, but the credit balance is always asked for">
 <rect x="20" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="220" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">READ FROM THE JOURNAL</text>
 <text x="220" y="110" text-anchor="middle" font-size="14" fill="var(--text-muted)">jump range · origin · cargo capacity</text>
 <text x="220" y="134" text-anchor="middle" font-size="14" fill="var(--text-muted)">all properties of the hull</text>
 <rect x="460" y="40" width="400" height="112" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="660" y="78" text-anchor="middle" font-size="16" font-weight="800" fill="var(--text)">ASKED FOR, EVERY TIME</text>
 <text x="660" y="110" text-anchor="middle" font-size="15" fill="var(--text)">your credit balance</text>
 <text x="660" y="134" text-anchor="middle" font-size="14" fill="var(--text-muted)">even though it is in the journal</text>
 <text x="440" y="196" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">The route means nothing without the hold. What you are worth is nobody’s business.</text>
 <text x="440" y="226" text-anchor="middle" font-size="15" fill="var(--text-muted)">It is the one figure here that is about you rather than about your ship.</text>
</svg>
</section>
<section>
<h2><span class="num">3</span> The hold does not have to be emptied.</h2>
<svg viewBox="0 0 880 252" role="img" aria-label="A trade plan reads as stops rather than legs, and a keep line says what declining to sell is worth">
 <rect x="20" y="30" width="840" height="140" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="46" y="66" text-anchor="start" font-size="16" fill="var(--text)">Abraham Lincoln in Sol</text>
 <text x="76" y="96" text-anchor="start" font-size="15" fill="var(--text-muted)">buy 384 × Gold at 9,400</text>
 <text x="46" y="128" text-anchor="start" font-size="16" fill="var(--text)">Diaz Chemical Holdings in RR Caeli — 20.9 ly</text>
 <text x="76" y="156" text-anchor="start" font-size="15" fill="var(--accent)">keep 384 × Gold — this station only pays 11,200</text>
 <text x="440" y="208" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">A keep line always says what declining to sell here is worth.</text>
 <text x="440" y="240" text-anchor="middle" font-size="15" fill="var(--text-muted)">A Commander not told why they are flying past a buyer will sell there — and then the plan stops being the plan.</text>
</svg>
<p class="body">This is the thing no other planner does. Every planner assumes a leg sells everything, and that is not always the best move: holding a commodity past a station that pays poorly, to a later one that pays well, can beat taking the money now.</p>
</section>
<section>
<h2><span class="num">4</span> Efficiency is backwards from how it sounds.</h2>
<svg viewBox="0 0 880 244" role="img" aria-label="Lower efficiency values produce fewer jumps, and 100 finds no route at all">
 <rect x="30" y="44" width="190" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="125" y="80" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">10</text>
 <text x="125" y="108" text-anchor="middle" font-size="15" fill="var(--text)">156 jumps</text>
 <rect x="240" y="44" width="190" height="86" rx="10" fill="var(--surface)" stroke="var(--border)" stroke-width="2"/>
 <text x="335" y="80" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">25</text>
 <text x="335" y="108" text-anchor="middle" font-size="15" fill="var(--text)">157 jumps</text>
 <rect x="450" y="44" width="190" height="86" rx="10" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="545" y="80" text-anchor="middle" font-size="20" font-weight="800" fill="var(--text)">60</text>
 <text x="545" y="106" text-anchor="middle" font-size="15" fill="var(--text)">168 jumps</text>
 <text x="545" y="126" text-anchor="middle" font-size="14" fill="var(--text-muted)">the default</text>
 <rect x="660" y="44" width="190" height="86" rx="10" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="755" y="80" text-anchor="middle" font-size="20" font-weight="800" fill="var(--danger)">100</text>
 <text x="755" y="108" text-anchor="middle" font-size="15" fill="var(--text)">no route at all</text>
 <text x="440" y="180" text-anchor="middle" font-size="17" font-weight="700" fill="var(--accent)">Lower lets it wander further, find more neutron stars, and finish in fewer jumps.</text>
 <text x="440" y="212" text-anchor="middle" font-size="16" fill="var(--text)">So Directive 47 clamps the parameter to 99.</text>
 <text x="440" y="238" text-anchor="middle" font-size="15" fill="var(--text-muted)">At 100 the service accepts the job and then fails to route, which reads as “there is no way to get there”.</text>
</svg>
</section>
<div class="next">
<div class="next-title">Where to go next</div>
<div class="cards">
<a class="card" href="navigation.html"><span class="ct">Navigation →</span><span class="cd">Getting the first waypoint out of the plan and into the galaxy map.</span></a>
<a class="card" href="galaxy.html"><span class="ct">Galaxy search →</span><span class="cd">The same service and the same switch, asked a different kind of question.</span></a>
<a class="card" href="exobiology.html"><span class="ct">Exobiology →</span><span class="cd">The fourth plot, and the one that says what it structurally cannot contain.</span></a>
</div>
</div>
</div></div>

## The details

Plot a neutron route, a Road to Riches loop, or a trade run.

> "plot me a route to Colonia"
> "plan a road to riches loop"
> "find me a trade route with fifty million"

This asks [spansh.co.uk](https://spansh.co.uk), the same service the
[galaxy search](galaxy.md) uses, and it shares that one setting. Turn on **Look things up in the
galaxy** and plotting works; leave it off and you get "route planning is switched off", which is a
capability that is off rather than an error.

### Plotting is not searching

A search is a request and a reply. A plot is a **job**: it is submitted, queued, and waited on.
Measured on 2026-08-14, a Sol-to-Colonia neutron route came back in about three seconds and a Road
to Riches loop on the first poll.

So the wait here is longer than a search gets — ninety seconds, against fifteen. A Commander who
asks for a route across the galaxy has knowingly asked for arithmetic. Past that, the answer says
so and says what to change: a shorter route, fewer hops, a smaller radius.

**The trade route is the exception, and it is not a plot at all.** It asks for markets and then
does the arithmetic here, which is what lets it hold cargo past a station and come home again — see
[`plot_trade_route`](#plot_trade_route) below.

### Your ship fills in its own numbers

Nobody says "plot me a route to Colonia from Sol at 52.31 light years a jump". Jump range, origin
and cargo capacity come from the journal.

**Your credit balance does not.** It is in the journal, and it is the one figure here that is
about you rather than about your ship — so a trade route asks for the number instead of reading
it. Cargo capacity is a property of the hull and the route means nothing without it; what you are
worth is nobody's business but yours.

### What comes back

A Sol-to-Colonia plot is 131 waypoints and 168 jumps. Reading that out is not an answer, so the
totals come first and the next handful of waypoints follow — which is how the route is flown
anyway. You plot the next waypoint when you reach this one.

**That cap belongs to speech, not to the plan.** The whole route is kept, and the **Routing** tab
in the window draws all of it — see [the panel](interface.md#panel). The same tab plots without
speaking, and a route plotted by voice appears there the moment it is worked out, because both
paths write and read one plan rather than each keeping their own.

```text
Sol to Colonia: 22,000 light years, 168 jumps across 131 waypoints.

The first 5:
  PSR J1752-2806 — 10 jumps; neutron, supercharge here; 21,629 ly left
  Nova Aquila No 3 — 6 jumps; neutron, supercharge here; 21,350 ly left
  ...

126 more after that. Ask again from further along and I will plot the rest.
```

The neutron flag is the only line that changes what you do on arrival, so it is on the waypoint
rather than in a preamble.

Trade stops carry **when the market was last reported**, for the same reason outfitting stock does.
A route can be arithmetically perfect against a four-year-old price and worth nothing at all.

### Mining routes

There are none, and that is a measurement rather than an omission: `api/mining/route` is a **404**.
The service has no mining route planner.

What it does have is the ring index, and that is `find_body` on the [galaxy search](galaxy.md) —
a hotspot material, how many overlap, the ring's composition and how rich its reserves are. Naming
a mining-route tool here that quietly ran a body search would be a worse answer than not having
one.

### Tools

#### `plot_route`

A route between two systems that detours through neutron stars where they help — the **Neutron
Plotter** card on the Routing tab, and [a page of its own](../neutron-plotter.html).

```json
{"type":"object","properties":{"efficiency":{"type":"integer","description":"How strictly to hold to the direct line, 1 to 99. Lower finds more neutron stars and so fewer jumps, at the cost of flying further off course. Defaults to 60."},"from":{"type":"string","description":"Where to plot from. Defaults to where the Commander is now."},"jump_range":{"type":"number","description":"The ship\u0027s jump range in light years. Defaults to this ship\u0027s, from the journal. Must be over 10."},"to":{"type":"string","description":"The destination system."}},"required":["to"],"additionalProperties":false}
```

**Efficiency is backwards from how it sounds.** It is how strictly the plotter holds to the direct
line, so a *lower* number lets it wander further, find more neutron stars, and finish in fewer
jumps. Measured Sol to Colonia at a 50 ly range on 2026-08-14:

| Efficiency | Jumps |
|---|---|
| 10 | 156 |
| 25 | 157 |
| 60 (the default) | 168 |
| 100 | no route found at all |

That last row is why d47 clamps the parameter to 99. At 100 the service accepts the job and then
fails to route, which reads to a Commander as "there is no way to get there".

A jump range of 10 light years or less is refused here rather than there. The service answers
`range must be greater than 10 LY`, which is a sentence about a parameter; d47 says the ship is too
short-ranged for the plotter, which is a sentence about the ship.

#### `plot_exploration_route`

A Road to Riches loop — nearby systems holding bodies worth scanning and mapping, ordered so the
trip is short and the payout is high.

```json
{"type":"object","properties":{"from":{"type":"string","description":"Where to start. Defaults to where the Commander is now."},"jump_range":{"type":"number","description":"The ship\u0027s jump range in light years. Defaults to this ship\u0027s."},"loop":{"type":"boolean","description":"Come back to where the route started. Defaults to true."},"max_distance_to_arrival":{"type":"number","description":"How far in-system a body may sit, in light seconds. Defaults to 10,000."},"minimum_value":{"type":"integer","description":"The least a body must be worth mapping to be worth stopping for, in credits. Defaults to 500,000."},"radius":{"type":"number","description":"How far from the start to look, in light years. Defaults to 500."},"stops":{"type":"integer","description":"How many systems to visit, 1 to 50. Defaults to 10."}},"required":[],"additionalProperties":false}
```

Stops with nothing to scan are dropped. The plotter includes the system you started from, and a
loop includes the return leg; both come back with an empty body list and would read as "stop here
and scan nothing". Bodies within a stop are read out worth-most first, because that is the number
that decides whether the stop is worth making at all.

#### `plot_trade_route`

A chain of buy-and-sell runs starting from the station you are docked at — **worked out here**,
over markets d47 fetched, rather than handed to somebody else's planner.

```json
{"type":"object","properties":{"capital":{"type":"integer","description":"How many credits to trade with. Required; never inferred."},"cargo_capacity":{"type":"integer","description":"The hold\u0027s size in tonnes. Defaults to this ship\u0027s, from the journal."},"hops":{"type":"integer","description":"How many legs to plan, 1 to 10. Defaults to 5."},"large_pad":{"type":"boolean","description":"Only stations with a large landing pad."},"loop":{"type":"boolean","description":"End back where it started. Defaults to false."},"max_hop_distance":{"type":"number","description":"The longest single leg, in light years. Defaults to 40."},"max_price_age_hours":{"type":"integer","description":"How stale a reported price may be, in hours. Defaults to 720, one month."},"max_station_distance":{"type":"number","description":"How far in-system a station may sit, in light seconds. Defaults to 1,000."}},"required":["capital"],"additionalProperties":false}
```

It cannot be planned from supercruise. The whole plan is anchored on the market you are standing
in, so there is no version of this question that can be asked in flight — and d47 says that rather
than making a request it knows is pointless.

##### The hold does not have to be emptied

The thing no other planner does. A leg that sells everything is what every planner assumes, and it
is not always the best move: holding a commodity past a station that pays poorly for it, to a later
one that pays well, can beat taking the money now.

That is why the plan reads as a sequence of **stops** rather than of legs — sell these, keep those,
buy that, go:

```text
3 hops from Abraham Lincoln, 412,800 credits on 50,000,000 over 44 light years.

Abraham Lincoln in Sol
  buy 384 × Gold at 9,400 — 3,609,600 cr
  your own prices, read 2026-08-19

Diaz Chemical Holdings in RR Caeli — 20.9 ly, 120 ls in
  keep 384 × Gold — this station only pays 11,200
  prices reported 2026-08-18
```

A `keep` line always says what declining to sell here is worth. A Commander who is not told why
they are flying past a buyer will sell there, and then the plan they were given stops being the
plan.

##### Round trips

`loop` ends the route back at the station it started from, so an evening's trading finishes at your
own base rather than four systems away. A shorter loop that pays better than the long one you asked
for is a better answer, not a shortfall, so it is taken.

##### What it will not promise

**It does not model market saturation.** Selling far more of a commodity than a station has demand
for drops what the rest of it fetches, and by how much is *not known*. A constant guessed here
would make every profit in every plan wrong in a way that reads exactly like the feature working,
so no leg ever sells more than a station asked for, and the plan says so every time. The honest
version of that figure is derivable from your own `MarketSell` events, and that is a different
piece of work.

**Fleet carriers are left out**, and the plan says how many. They set their own prices and then
move: measured on 2026-08-19, the best Gold price within 50 light years of Sol was 4,760,900
credits at a carrier against 52,282 at the best station. A planner that ranks on price and does not
know what a carrier is builds every plan around one, and half of them have jumped by the time you
get there.

**Rares are left out.** They are priced per station, capped at a handful of tonnes by the game
itself, and worth less the closer they are sold to home — none of which is modelled, so they are
not planned with rather than mispriced.

##### Where the prices come from

Two places, and each stop says which.

Everything beyond where you have been is [spansh.co.uk](https://spansh.co.uk) — the same host, the
same setting and the same disclosure as the searches, because it is the same decision you are
making. Every station in a search result carries its whole market, so this needs no galaxy dump and
no extra permission; it is third-party data and it is treated as untrusted, like the journal and
in-game comms.

Where you have docked and opened the commodity board, Elite wrote `Market.json` and d47 kept it.
Those are your own prices — exact, free, and covering almost nothing of the galaxy. The rule is
**newer wins**: your reading of the board an hour ago beats a report from last week, and a report
from this morning beats what you saw a month ago. The last 25 markets you have seen live in
`data/markets.json`, which is readable and hand-editable like everything else in `data/`.

Every stop carries when its prices were reported, for the same reason outfitting stock does. A
route can be arithmetically perfect against a four-year-old market and worth nothing at all.

##### How long it takes

Ten hops, and in seconds rather than minutes.

The arithmetic was never the problem. Holding cargo makes the state carried between hops *credits
and cargo* rather than credits, so a plain search over ten hops is the reachable set raised to the
tenth power — the version that does not finish. d47 runs a bounded one instead: a beam of the best
200 partial routes carried hop to hop, with each leg's buy-and-sell decision solved inside it.
Measured at the real shape — ten hops, 150 markets, 30 commodities — that is 300,000 leg
evaluations and tens of milliseconds.

What costs is asking. A station search answers in 1.1 to 1.3 seconds whatever it returns, so the
bill is the number of requests and hardly the size of them: d47 asks for the largest pages the
service gives, and caches what comes back, because a market does not move between two plans made a
minute apart.

For scale: the planner this replaced took **forty-eight seconds** to answer four hops.

### Notes for anyone reading the code

The route endpoints have a property the search endpoints do not: **they echo back the parameters
they understood, and only those.** That is a local oracle for which keys are real, and it is how
these were established rather than guessed. A dropped key is not an error — the plot runs with that
parameter at its default, which is the quietest way to be given a wrong answer.

**The borrowed trade planner used to be behind this tool and is not.** `api/trade/route` is still
real and still answers; d47 stopped asking. It could not hold cargo between legs, it silently
dropped `loop` and `capital` alike — a trade plot with no capital finds nothing affordable and
reports an empty route, which looks exactly like a correct answer about a Commander with no money —
and it took forty-eight seconds over four hops. Those are not parameters of somebody else's planner
but a different problem. It was **replaced rather than kept as a fallback**: two tools that both
plan trade routes and disagree is worse than either alone.

What the station search will and will not do, measured on 2026-08-19, because both halves shaped
the design:

| | |
|---|---|
| Narrowable server-side | distance from a reference system, pad size, station type, which commodities a station deals in |
| Accepted and **silently ignored** | any bound on price or demand — 203 stations for `demand >= 1`, 203 for `demand >= 50000`, 203 for no bound at all |
| Rejected outright | every sort shape tried against a commodity's price, HTTP 400 |

So the shortlist arrives unranked and d47 ranks it. Two more habits worth not rediscovering: a
commodity filter must be a list of **objects** (`[{"name":"Gold"}]`) — a bare string list is
ignored and returns everything — and the commodity filter is coarse, matching what a station
*deals in* rather than what it currently pays for. 49 of 200 "Gold importers" near Sol had any Gold
demand at all.

"No route exists" arrives as a **completed** job whose status is `failed`, not as an outage. It is
reported as an answer, because retrying will fail identically every time. An outage, a rate limit
and a refusal are all separate sentences, and a refusal quotes the service's own reason — unlike
the searches, the plotters actually say what was wrong ("Could not find finishing system"). That
text is third-party and on its way into a prompt, so it is single-lined and length-capped before it
goes anywhere.

The poll interval is fixed rather than backing off. The wait is bounded and short, jobs that finish
quickly finish on the first or second poll anyway, and a backoff would leave the slowest case
waiting well past finishing before anybody noticed. The delay is injected, so the whole loop —
including the ninety-second budget — is tested without a second of real time passing.

**The galaxy plotter is not wired up.** `api/generic/route` is real and it does not take a jump
range: it wants the drive's physics — `optimal_mass`, `fuel_power`, `fuel_multiplier`,
`max_fuel_per_jump`, `range_boost` — and answers `ship details are invalid` without them. Some of
that is in the journal's `Loadout` engineering modifiers and some needs a module specification
table. It is a real feature and it belongs behind the one that provides that table, not bolted on
with numbers d47 would have to invent.
