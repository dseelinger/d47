# Phase 36 — Trade routes that d47 works out

The plan of record for list.md Phase 36. Written 2026-08-19, with the phase's own measurements
already on the page and remediation 16 merged the same day.

`list.md` reads top to bottom as a description of the product. This is the order the work happened
in, the four calls the phase demanded be made out loud, and the reasoning the order cannot carry on
its own.

---

## The phase in one sentence

`plot_trade_route` handed the question to somebody else's planner and read back the answer; this
phase makes it **d47's own arithmetic over markets d47 fetched**, which is the only way it can do
the three things that planner cannot — hold cargo past a station, come home again, and answer ten
hops in seconds.

## The four calls, made before any code

The phase itself posed these. Each was put to the Commander on 2026-08-19 and each was answered.

1. **Replace, not stand beside.** The borrowed `api/trade/route` call is *gone*, not kept as a
   fallback. The phase says plainly that two tools which both plan trade routes and disagree is
   worse than either alone, and a fallback is that failure with a delay on it: two profit models
   behind one tool name, and a Commander with no way to tell which one answered. It also costs
   nothing on the tool surface, which mattered — see the byte count below.
2. **Reach out, and prefer local.** Spansh supplies the galaxy; the Commander's own `Market.json`
   supplies the stations they have stood in. The rule is **newer wins** rather than mine-wins,
   because a report from this morning really is better than what they saw a month ago.
3. **Cap at demand, and say so.** No saturation constant is invented. The measured answer exists on
   disk in 915 journals and deriving it is a different piece of work; until it is done, no leg sells
   past demand and every plan says that it does not model the price drop.
4. **Build on the recorded numbers.** The 2026-08-19 measurements in `list.md` are a day old and
   carry their request shapes; no fresh probe was run before building on them.

## What already existed to build on

- **The station search already returns the whole market.** This is the fact the phase rests on and
  it was measured rather than hoped: `POST api/stations/search` carries `market` per result —
  `commodity`, `buy_price`, `sell_price`, `demand`, `supply`, `is_rare` — plus `market_updated_at`,
  `system_x/y/z`, `distance_to_arrival`, pads and `type`. So no galaxy dump, and no new permission.
- **`SpanshRequest` and `SpanshResponse` already own the request shapes and the tolerant reading.**
  A market sweep is one more body and one more reader beside the four already there.
- **`CargoManifestReader` fixed the shape for reading a file the game rewrites in place** — share
  flags, stat-then-parse, stamp only after a successful parse. `MarketReader` is that class again
  with a different payload.
- **`ShipCoreStore` and `ShipBuildStore` fixed the shape for a hand-editable file in `data/`.**
  `MarketBook` is the same discipline: `AtomicFile`, readable JSON, stable ordering, a damaged file
  reported and survived rather than fatal.
- **`StarPosition` is the one distance formula in Core**, and legs are Euclidean over the
  coordinates the search returns.

## Order of work

1. **`MarketSnapshot`** — the unit the planner works in, and `PriceSource` to say where a price came
   from. Flat numbers with no idea what Spansh is.
2. **`TradePlanner`** — the beam search. Written and tested against markets typed out by hand,
   before anything could fetch one.
3. **The records** — `TradeStop` replacing `TradeHop`, because a stop can express a keep and a leg
   cannot.
4. **The seam** — `ITradePlanService`, out of `IRouteService`, because the trade plot stopped being
   a job that is queued and polled for.
5. **`MarketBook` and `MarketReader`** — the Commander's own prices.
6. **`SpanshTradePlanService`** — the sweep, the paging, the cache, the merge.
7. **The capability and the page** — the prose a Commander reads, and the schema the docs gate
   pins.

---

## The algorithm, and why it is this one

The state carried between hops is **credits and cargo**. That is the whole phase in one line, and it
is what makes the naive formulation unrunnable: a plain search over ten hops is the reachable set
raised to the tenth power.

So: a beam of the best 200 partial routes, carried hop to hop, each leg's buy-and-sell decision
solved inside it. Per state and destination, two successors are generated —

- **sell everything this station pays for**, which is what every planner does; and
- **keep any lot the destination pays more for**, which is the phase.

Both go into the beam and the search decides downstream. A keep that looked clever and then had
nowhere to go is beaten by the sell, and none of it needs a heuristic about which is "usually"
right. The beam ranks on *credits plus what the hold would fetch here*, which is the only ranking
that does not quietly punish a state for having its money still in cargo.

Buying is ranked by margin a tonne against **the better of what the destination pays and the best
price on the whole board**. That second term is what makes retention searchable rather than
accidental: without it nothing would ever be loaded that the next station does not itself want, so
the keep variant would have nothing to keep.

### Three rules that fell out of building it

- **A leg that carries cargo and does nothing else is a plan.** The first version dropped any leg
  that neither sold nor bought, as an empty ship moving for no reason — which silently deleted the
  pure retention leg, the exact thing the phase is about. Caught by the test written for item 4
  before anything else could have caught it, and the guard is now on the hold rather than on the
  trading.
- **The origin survives every filter.** A pad rule has nothing left to protect a Commander from when
  they are already docked there, and dropping the origin makes d47 answer "I cannot see your market"
  about the market they are standing in. This is also why the sweep sends **no pad filter**: the
  server-side one would drop an outpost origin before the planner ever got a chance to exempt it.
- **Null and empty are different answers.** No origin market is "I cannot see the board you are
  standing in front of"; no route is "nothing near here pays". They are worded differently because
  a Commander can act on the first and not on the second.

## What the phase deliberately does not do

- **Saturation.** Capped at demand, stated in every plan. The derivation from `MarketSell` is real
  work and `list.md` defers it on purpose.
- **Fleet carriers.** Left out, and counted so the omission is visible. They set their own prices
  and then move.
- **Rares.** Left out. Per-station pricing, a cap of a few tonnes, and a payout that falls the
  closer they are sold to home — none of it modelled, so they are not planned with.
- **The hold you are actually carrying.** A plan starts from an empty ship. Seeding it from
  `Cargo.json` would mean deciding what to do about limpets, mission cargo and stolen goods, and
  each of those is its own wrong answer.

## The cost

- **Tool surface: the SRV profile ends at 39,918 of 40,000**, so 82 bytes free. `plot_trade_route`
  kept its name and gained one parameter (`loop`); two descriptions were trimmed to pay for it. The
  remark on `ToolProfiles.ComfortableBytes` still stands: raising the number a fourth time is the
  wrong answer, and deferred tool loading is the right one.
- **Wall clock: three requests, about four seconds, then tens of milliseconds of arithmetic.**
  Against the forty-eight seconds the borrowed planner charged for four hops.
- **One new file in `data/`**, `markets.json`, holding the last 25 markets the Commander has seen.
