# Route planning

Plot a neutron route, a Road to Riches loop, or a trade run.

> "plot me a route to Colonia"
> "plan a road to riches loop"
> "find me a trade route with fifty million"

This asks [spansh.co.uk](https://spansh.co.uk), the same service the
[galaxy search](galaxy.md) uses, and it shares that one setting. Turn on **Look things up in the
galaxy** and plotting works; leave it off and you get "route planning is switched off", which is a
capability that is off rather than an error.

## Plotting is not searching

A search is a request and a reply. A plot is a **job**: it is submitted, queued, and waited on.
Measured on 2026-08-14, a Sol-to-Colonia neutron route came back in about three seconds, a Road to
Riches loop on the first poll, and a four-hop trade route took forty-eight seconds.

So the wait here is longer than a search gets — ninety seconds, against fifteen. A Commander who
asks for a route across the galaxy has knowingly asked for arithmetic. Past that, the answer says
so and says what to change: a shorter route, fewer hops, a smaller radius.

## Your ship fills in its own numbers

Nobody says "plot me a route to Colonia from Sol at 52.31 light years a jump". Jump range, origin
and cargo capacity come from the journal.

**Your credit balance does not.** It is in the journal, and it is the one figure here that is
about you rather than about your ship — so a trade route asks for the number instead of reading
it. Cargo capacity is a property of the hull and the route means nothing without it; what you are
worth is nobody's business but yours.

## What comes back

A Sol-to-Colonia plot is 131 waypoints and 168 jumps. Reading that out is not an answer, so the
totals come first and the next handful of waypoints follow — which is how the route is flown
anyway. You plot the next waypoint when you reach this one.

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

Trade hops carry **when the market was last reported**, for the same reason outfitting stock does.
A route can be arithmetically perfect against a four-year-old price and worth nothing at all.

## Mining routes

There are none, and that is a measurement rather than an omission: `api/mining/route` is a **404**.
The service has no mining route planner.

What it does have is the ring index, and that is `find_body` on the [galaxy search](galaxy.md) —
a hotspot material, how many overlap, the ring's composition and how rich its reserves are. Naming
a mining-route tool here that quietly ran a body search would be a worse answer than not having
one.

## Tools

### `plot_route`

A jump route between two systems, using neutron boosts where they help.

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

### `plot_exploration_route`

A Road to Riches loop — nearby systems holding bodies worth scanning and mapping, ordered so the
trip is short and the payout is high.

```json
{"type":"object","properties":{"from":{"type":"string","description":"Where to start. Defaults to where the Commander is now."},"jump_range":{"type":"number","description":"The ship\u0027s jump range in light years. Defaults to this ship\u0027s."},"loop":{"type":"boolean","description":"Come back to where the route started. Defaults to true."},"max_distance_to_arrival":{"type":"number","description":"How far in-system a body may sit, in light seconds. Defaults to 10,000."},"minimum_value":{"type":"integer","description":"The least a body must be worth mapping to be worth stopping for, in credits. Defaults to 500,000."},"radius":{"type":"number","description":"How far from the start to look, in light years. Defaults to 500."},"stops":{"type":"integer","description":"How many systems to visit, 1 to 50. Defaults to 10."}},"required":[],"additionalProperties":false}
```

Stops with nothing to scan are dropped. The plotter includes the system you started from, and a
loop includes the return leg; both come back with an empty body list and would read as "stop here
and scan nothing". Bodies within a stop are read out worth-most first, because that is the number
that decides whether the stop is worth making at all.

### `plot_trade_route`

A chain of buy-and-sell runs starting from the station you are docked at.

```json
{"type":"object","properties":{"capital":{"type":"integer","description":"How many credits to trade with. Required; never inferred."},"cargo_capacity":{"type":"integer","description":"The hold\u0027s size in tonnes. Defaults to this ship\u0027s, from the journal."},"hops":{"type":"integer","description":"How many legs to plan, 1 to 8. Defaults to 4."},"large_pad":{"type":"boolean","description":"Only stations with a large landing pad."},"max_hop_distance":{"type":"number","description":"The longest single leg, in light years. Defaults to 40."},"max_price_age":{"type":"integer","description":"How stale a reported price may be, in hours. Defaults to 720, one month."},"max_station_distance":{"type":"number","description":"How far in-system a station may sit, in light seconds. Defaults to 1,000."}},"required":["capital"],"additionalProperties":false}
```

It cannot be plotted from supercruise. The service keys the whole plot on a starting **station**
id, so there is no version of this question that can be asked in flight — and d47 says that rather
than sending a request it knows will be refused.

There is no round-trip option. `loop` is a parameter the Road to Riches planner honours and the
trade planner drops; a trade chain comes back as a loop when the arithmetic makes one, and it
cannot be asked for.

## Notes for anyone reading the code

The route endpoints have a property the search endpoints do not: **they echo back the parameters
they understood, and only those.** That is a local oracle for which keys are real, and it is how
these were established rather than guessed. Measured on a trade plot:

| Sent | In the echo |
|---|---|
| `starting_capital` | kept |
| `max_cargo` | kept |
| `capital`, `cargo_capacity`, `capacity`, `cargo`, `credits` | dropped |
| `requires_large_pad`, `max_price_age`, `unique`, `permit` | kept |
| `loop` | dropped |

A dropped key is not an error. The plot runs with that parameter at its default — and for capital
that means nothing is affordable and the route comes back empty, which looks exactly like a
correct answer about a Commander with no money.

"No route exists" arrives as a **completed** job whose status is `failed`, not as an outage. It is
reported as an answer, because retrying will fail identically every time. An outage, a rate limit
and a refusal are all separate sentences, and a refusal quotes the service's own reason — unlike
the searches, the plotters actually say what was wrong ("Could not find finishing system"). That
text is third-party and on its way into a prompt, so it is single-lined and length-capped before it
goes anywhere.

The poll interval is fixed rather than backing off. The wait is bounded and short, jobs that finish
quickly finish on the first or second poll anyway, and a backoff would leave the forty-eight-second
case waiting well past finishing before anybody noticed. The delay is injected, so the whole loop —
including the ninety-second budget — is tested without a second of real time passing.

**The galaxy plotter is not wired up.** `api/generic/route` is real and it does not take a jump
range: it wants the drive's physics — `optimal_mass`, `fuel_power`, `fuel_multiplier`,
`max_fuel_per_jump`, `range_boost` — and answers `ship details are invalid` without them. Some of
that is in the journal's `Loadout` engineering modifiers and some needs a module specification
table. It is a real feature and it belongs behind the one that provides that table, not bolted on
with numbers d47 would have to invent.
