namespace D47.Core.Knowledge;

/// <summary>Which way round the question is.</summary>
public enum TradeSide
{
    /// <summary>Where can the Commander buy it.</summary>
    Buying,

    /// <summary>Where can the Commander sell it.</summary>
    Selling,
}

/// <summary>Where to buy a commodity, or where to dump one (Phase 49).</summary>
/// <param name="Commodity">
/// The market's own spelling, folded case-insensitively when compared.
/// </param>
/// <param name="Side">Buying or selling.</param>
/// <param name="Tonnes">How much, if the Commander said.</param>
/// <param name="MaxDistance">Light years from the reference system.</param>
/// <param name="LargePadOnly">Only stations with a large pad.</param>
/// <param name="IncludeCarriers">
/// Fleet carriers, which are excluded unless asked for.
/// </param>
/// <param name="Limit">How many to return.</param>
/// <param name="MaxStationDistance">
/// Light seconds from the star, or null for any (#296).
/// </param>
/// <param name="MinAvailable">
/// The least the station must have — supply when buying, demand when selling — or null for any (#296).
/// </param>
/// <param name="SurfaceStations">
/// Also planetary ports, outposts and settlements (#296).
/// </param>
/// <param name="OrderBy">Nearest first, or best price first.</param>
/// <param name="Tag">Who baked this question, or null for an ordinary one (#303).</param>
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
    CommodityOrder OrderBy = CommodityOrder.Price,
    string? Tag = null);

/// <summary>Which way a commodity answer is ranked (#296).</summary>
public enum CommodityOrder
{
    /// <summary>Price against distance when a tonnage was given, price alone when not.</summary>
    Price,

    /// <summary>Nearest first.</summary>
    Distance,
}

/// <summary>One station's answer to a commodity question.</summary>
/// <param name="Market">The station, and everything else known about it.</param>
/// <param name="UnitPrice">What one tonne costs, or fetches.</param>
/// <param name="Tonnes">
/// How many the station can actually do — its supply when buying, its demand when selling, capped at
/// what was asked for.
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

/// <summary>The local ranking (Phase 49).</summary>
public static class CommodityMarketSearch
{
    /// <summary>What d47 will fly one more light year for, in credits of total difference.</summary>
    public const int CreditsPerLightYear = 1_000;

    /// <summary>Ranks the markets that can answer the question.</summary>
    /// <param name="query">What was asked.</param>
    /// <param name="markets">
    /// Everything gathered, already aged out by whoever knew the time.
    /// </param>
    /// <param name="origin">Where to measure from.</param>
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

            // A surface pad is a descent the Commander did not ask for (#296).
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

            // Supply is a filter and not a footnote.
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

            // A floor rather than a load (#296): the station is excluded for having less than this, whatever
            // the Commander means to carry away.
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

        // Nearest first when asked (#296), the price breaking a tie between two stations in one system.
        IEnumerable<CommodityOffer> ranked = query.OrderBy == CommodityOrder.Distance && origin is not null
            ? offers.OrderBy(offer => offer.Distance).ThenBy(offer => Score(query, offer))
            : offers.OrderBy(offer => Score(query, offer));

        return [.. ranked.Take(Math.Max(1, query.Limit))];
    }

    /// <summary>Lower is better, both ways round.</summary>
    private static double Score(CommodityQuery query, CommodityOffer offer)
    {
        if (query.Tonnes is null)
        {
            // No tonnage, so nothing to weigh the distance against.
            return query.Side == TradeSide.Buying ? offer.UnitPrice : -offer.UnitPrice;
        }

        var trip = offer.Distance * CreditsPerLightYear;

        return query.Side == TradeSide.Buying
            ? offer.Total + trip
            : -offer.Total + trip;
    }
}
