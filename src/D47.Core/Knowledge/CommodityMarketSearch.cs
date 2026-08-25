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
/// Where to buy a commodity, or where to dump one (list.md Phase 49).
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
public sealed record CommodityQuery(
    string Commodity,
    TradeSide Side = TradeSide.Buying,
    int? Tonnes = null,
    double MaxDistance = 50,
    bool LargePadOnly = false,
    bool IncludeCarriers = false,
    int Limit = 5);

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
/// The local ranking (list.md Phase 49).
/// <para>
/// <b>Local is the arrangement rather than a workaround.</b> Spansh will not sort on a commodity's
/// price server-side — every sort shape tried answered HTTP 400 — and its demand bounds are
/// accepted and ignored, so the same 203 stations come back for <c>demand &gt;= 1</c>, for
/// <c>demand &gt;= 50000</c> and for no bound at all. The sweep returns whole markets anyway (342
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

        return [.. offers.OrderBy(offer => Score(query, offer)).Take(Math.Max(1, query.Limit))];
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
