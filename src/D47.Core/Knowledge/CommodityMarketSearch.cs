namespace D47.Core.Knowledge;

/// <summary>Which way round the question is.</summary>
public enum TradeSide
{
    /// <summary>Where can the Commander buy it. Supply and buy price; cheapest is best.</summary>
    Buying,

    /// <summary>Where can the Commander sell it. Demand and sell price; highest is best.</summary>
    Selling,
}

/// <summary>
/// Where to buy a commodity, or where to dump one (Phase 49).
/// <para>
/// <b>Both sides of the board, one query.</b> <em>Where do I dump 700 tonnes of tritium</em> is
/// the same question as <em>where do I buy it</em> read the other way round, and a search that
/// answered only the buy half would be asked the sell half anyway — by a model that would then
/// reach for the trade planner and return a circuit nobody wanted.
/// </para>
/// </summary>
/// <param name="Commodity">The market's own spelling, folded case-insensitively when compared.</param>
/// <param name="Side">Buying or selling.</param>
/// <param name="Tonnes">
/// How much, if the Commander said. <b>Part of the question rather than a refinement of it</b>: a
/// station 40 ly further out that is 200 credits cheaper is the wrong answer for 8 tonnes and the
/// right one for 780. Null means they asked what a thing is worth, and the ranking is by price
/// with the distance reported beside it.
/// </param>
/// <param name="MaxDistance">Light years from the reference system.</param>
/// <param name="LargePadOnly">Only stations with a large pad.</param>
/// <param name="IncludeCarriers">
/// Fleet carriers, which are excluded unless asked for. Their prices are player-set and can be a
/// joke, and the station itself may be a hundred light years away by the time anybody arrives.
/// </param>
/// <param name="Limit">How many to return.</param>
/// <param name="MaxStationDistance">
/// Light seconds from the star, or null for any (#296). The half of "how far" the Commander
/// feels once they arrive: a pad 60,000 Ls out is a longer trip than a system two jumps further
/// on. Filtered here off <see cref="MarketSnapshot.DistanceToArrival"/>, which the sweep already
/// carries; a station whose distance is unknown is kept, because unknown is not far.
/// </param>
/// <param name="MinAvailable">
/// The least the station must have — supply when buying, demand when selling — or null for any
/// (#296). Different from <paramref name="Tonnes"/>: that says how much the Commander wants and
/// caps what a station can do for them; this excludes a station that has less than a floor, which
/// is the shape INARA's "min supply" has and the one a Community Goal run wants. It also goes into
/// the request, because the station search honours a supply bound (measured 2026-09-05: 347
/// stations within 40 ly of Ega at supply ≥ 1, 80 at ≥ 1,000, 34 at ≥ 10,000, 4 at ≥ 50,000).
/// </param>
/// <param name="SurfaceStations">
/// Also planetary ports, outposts and settlements (#296). Off by default, as INARA's search has it:
/// a surface pad is a descent and a climb the Commander did not ask about. The index's own words
/// for a surface station are <c>Planetary Outpost</c>, <c>Planetary Port</c>, <c>Settlement</c>
/// and <c>Surface Settlement</c>, measured 2026-09-05.
/// </param>
/// <param name="OrderBy">
/// Nearest first, or best price first. <see cref="CommodityOrder.Price"/> is the ranking this
/// search has always had; <see cref="CommodityOrder.Distance"/> is what a Commander running
/// INARA's search orders by, and what "the best place to go next" means to them (#296).
/// </param>
public sealed record CommodityQuery(
    string Commodity,
    TradeSide Side = TradeSide.Buying,
    int? Tonnes = null,
    double MaxDistance = 50,
    bool LargePadOnly = false,
    bool IncludeCarriers = false,
    int Limit = 5,
    double? MaxStationDistance = null,
    int? MinAvailable = null,
    bool SurfaceStations = false,
    CommodityOrder OrderBy = CommodityOrder.Price);

/// <summary>Which way a commodity answer is ranked (#296).</summary>
public enum CommodityOrder
{
    /// <summary>Price against distance when a tonnage was given, price alone when not.</summary>
    Price,

    /// <summary>Nearest first. The price is reported beside it and decides nothing.</summary>
    Distance,
}

/// <summary>
/// One station's answer to a commodity question.
/// </summary>
/// <param name="Market">The station, and everything else known about it.</param>
/// <param name="UnitPrice">What one tonne costs, or fetches.</param>
/// <param name="Tonnes">
/// How many the station can actually do — its supply when buying, its demand when selling, capped
/// at what was asked for.
/// </param>
/// <param name="Distance">Light years from the reference system.</param>
public sealed record CommodityOffer(
    MarketSnapshot Market,
    int UnitPrice,
    int Tonnes,
    double Distance)
{
    /// <summary>What the whole load costs, or fetches.</summary>
    public long Total => (long)UnitPrice * Tonnes;

    /// <summary>The Commander's own eyes rather than somebody's report.</summary>
    public bool IsTheirs => Market.Source == PriceSource.Seen;
}

/// <summary>
/// The local ranking (Phase 49).
/// <para>
/// <b>Local is the arrangement rather than a workaround.</b> Spansh will not sort on a commodity's
/// price server-side — every sort shape tried answered HTTP 400 — and its demand bounds are
/// accepted and ignored, so the same 203 stations come back for <c>demand &gt;= 1</c>, for
/// <c>demand &gt;= 50000</c> and for no bound at all. <b>That last finding is about the
/// <em>trade</em> endpoint and does not carry to the station search</b>, which is a different one
/// and honours both bounds — 12 stations within 15 light years of Eurybia for
/// <c>demand &gt;= 1</c> against 449 unfiltered, measured 2026-08-28. The ranking still happens
/// here, because sorting is what is refused; the <em>narrowing</em> moved server-side in #156, and
/// it had to, since the sweep's station budget was being spent on markets that did not carry the
/// commodity at all. The sweep returns whole markets anyway (342
/// priced commodities per station, measured 2026-08-19) and answers in about the same time
/// whatever it returns, so the bill is the number of requests rather than their size. Ranking here
/// costs one pass over a list that was already in memory.
/// </para>
/// <para>
/// <b>Pure, and that is what makes the arithmetic assertable.</b> No clock, no socket, no idea
/// where the markets came from. Ageing them out belongs to the caller that knows what time it is.
/// </para>
/// </summary>
public static class CommodityMarketSearch
{
    /// <summary>
    /// What d47 will fly one more light year for, in credits of total difference.
    /// <para>
    /// <b>A stated assumption rather than a fact</b>, and the only invented number here. It exists
    /// because the question needs one: ranking a load by price alone makes a 200-credit saving
    /// worth a 40 light year detour for eight tonnes, which is wrong, and ranking by distance alone
    /// makes it worthless for seven hundred, which is also wrong. At a thousand credits per light
    /// year, a saving of 200 a tonne moves the answer 1.6 ly for eight tonnes and 156 ly for seven
    /// hundred and eighty — which is the behaviour the checklist describes, arrived at by naming
    /// the exchange rate rather than by hiding it in a comparison.
    /// </para>
    /// <para>
    /// It only ever applies when the Commander said how much. Without a tonnage there is no total
    /// to trade against distance, so the ranking is by price and the distance is reported.
    /// </para>
    /// </summary>
    public const int CreditsPerLightYear = 1_000;

    /// <summary>
    /// Ranks the markets that can answer the question.
    /// </summary>
    /// <param name="query">What was asked.</param>
    /// <param name="markets">Everything gathered, already aged out by whoever knew the time.</param>
    /// <param name="origin">
    /// Where to measure from. Null where the sweep found nothing at the reference system, in which
    /// case every distance is unknown and the ranking falls back to price alone.
    /// </param>
    public static IReadOnlyList<CommodityOffer> Rank(
        CommodityQuery query,
        IEnumerable<MarketSnapshot> markets,
        MarketSnapshot? origin)
    {
        var offers = new List<CommodityOffer>();

        foreach (var market in markets)
        {
            if (market.IsCarrier && !query.IncludeCarriers)
            {
                continue;
            }

            if (query.LargePadOnly && !market.HasLargePad)
            {
                continue;
            }

            // A surface pad is a descent the Commander did not ask for (#296). Kept when they
            // did; a station with no type at all is kept too, because unknown is not surface.
            if (!query.SurfaceStations && market.IsSurface)
            {
                continue;
            }

            // Unknown is not far: a station the index gives no arrival distance for stays in.
            if (query.MaxStationDistance is { } furthest
                && market.DistanceToArrival is { } arrival
                && arrival > furthest)
            {
                continue;
            }

            if (market.Quote(query.Commodity) is not { } quote)
            {
                continue;
            }

            // Supply is a filter and not a footnote. A station listing the cheapest steel in the
            // bubble and holding nine tonnes of it is not an answer to "where do I buy 700".
            var (price, available) = query.Side == TradeSide.Buying
                ? (quote.BuyPrice, quote.Supply)
                : (quote.SellPrice, quote.Demand);

            if (price <= 0 || available <= 0)
            {
                continue;
            }

            if (query.Tonnes is { } wanted && available < wanted)
            {
                continue;
            }

            // A floor rather than a load (#296): the station is excluded for having less than
            // this, whatever the Commander means to carry away.
            if (query.MinAvailable is { } floor && available < floor)
            {
                continue;
            }

            var distance = origin?.DistanceTo(market) ?? 0;

            if (origin is not null && distance > query.MaxDistance)
            {
                continue;
            }

            offers.Add(new CommodityOffer(
                market,
                price,
                query.Tonnes is { } asked ? Math.Min(asked, available) : available,
                distance));
        }

        // Nearest first when asked (#296), the price breaking a tie between two stations in one
        // system. Without an origin every distance is zero, so this falls through to price — the
        // same fallback the price ranking already makes when it cannot place the Commander.
        IEnumerable<CommodityOffer> ranked = query.OrderBy == CommodityOrder.Distance && origin is not null
            ? offers.OrderBy(offer => offer.Distance).ThenBy(offer => Score(query, offer))
            : offers.OrderBy(offer => Score(query, offer));

        return [.. ranked.Take(Math.Max(1, query.Limit))];
    }

    /// <summary>
    /// Lower is better, both ways round. Buying is what the load costs plus what the trip is worth;
    /// selling is the negative of what it fetches, so the same comparison sorts both.
    /// </summary>
    private static double Score(CommodityQuery query, CommodityOffer offer)
    {
        if (query.Tonnes is null)
        {
            // No tonnage, so nothing to weigh the distance against. Price decides and the distance
            // is reported — a Commander who did not say how much is asking what a thing is worth.
            return query.Side == TradeSide.Buying ? offer.UnitPrice : -offer.UnitPrice;
        }

        var trip = offer.Distance * CreditsPerLightYear;

        return query.Side == TradeSide.Buying
            ? offer.Total + trip
            : -offer.Total + trip;
    }
}
