using System.Globalization;

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
/// <param name="Descending">
/// Whether the order is reversed: highest figure first when true, lowest when false, and the order's usual
/// direction when null — nearest first for distance, best price first for price.
/// </param>
/// <param name="More">Further commodities every answering station must also trade.</param>
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
    string? Tag = null,
    bool? Descending = null,
    IReadOnlyList<CommodityWant>? More = null)
{
    /// <summary>Every commodity asked for, the first one first.</summary>
    public IReadOnlyList<CommodityWant> Wants => [new(Commodity, Tonnes), .. More ?? []];

    /// <summary>Ranked nearest first, so the winner is a place to go.</summary>
    public bool NearestFirst => OrderBy == CommodityOrder.Distance && Descending != true;

    /// <summary>Whether the ranking is turned round from the order's usual direction.</summary>
    public bool Reversed => OrderBy == CommodityOrder.Distance
        ? Descending == true
        : Descending is { } descending && descending != (Side == TradeSide.Selling);
}

/// <summary>One commodity in a question, and how many tonnes of it, if the Commander said.</summary>
public sealed record CommodityWant(string Commodity, int? Tonnes)
{
    /// <summary>The most commodities one search asks about.</summary>
    public const int Most = 5;

    /// <summary>
    /// Reads <c>"Painite: 40, Platinum: 12, Gold"</c>: items split on commas, a tonnage after a colon,
    /// <paramref name="tonnes"/> for an item without one, duplicate names merged with their tonnages summed.
    /// Null with <paramref name="refusal"/> set when the list cannot be searched.
    /// </summary>
    public static IReadOnlyList<CommodityWant>? Parse(string text, int? tonnes, out string? refusal)
    {
        refusal = null;

        var items = new List<(string Name, int? Tonnes)>();

        foreach (var raw in text.Split(','))
        {
            var item = raw.Trim();
            int? said = null;

            if (item.LastIndexOf(':') is var colon and >= 0)
            {
                var figure = item[(colon + 1)..].Trim();

                if (figure.Length == 0)
                {
                    figure = "0";
                }

                if (!int.TryParse(figure, NumberStyles.Integer, CultureInfo.InvariantCulture, out var load))
                {
                    refusal = $"I could not read \"{figure}\" as tonnes of {item[..colon].Trim()}.";
                    return null;
                }

                said = load > 0 ? load : null;
                item = item[..colon];
            }

            var name = string.Join(' ', item.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            if (name.Length == 0)
            {
                continue;
            }

            var seen = items.FindIndex(known => string.Equals(known.Name, name, StringComparison.OrdinalIgnoreCase));

            if (seen < 0)
            {
                items.Add((name, said));
            }
            else if (said is { } more)
            {
                items[seen] = (items[seen].Name, (items[seen].Tonnes ?? 0) + more);
            }
        }

        if (items.Count > Most)
        {
            refusal = $"Five commodities is the most I search for at one station, and that was {items.Count}.";
            return null;
        }

        return [.. items.Select(item => new CommodityWant(item.Name, item.Tonnes ?? tonnes))];
    }
}

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
/// <remarks><see cref="UnitPrice"/> and <see cref="Tonnes"/> are the first commodity asked for.</remarks>
public sealed record CommodityOffer(
    MarketSnapshot Market,
    int UnitPrice,
    int Tonnes,
    double Distance)
{
    /// <summary>One lot per commodity asked for, in the order asked.</summary>
    public IReadOnlyList<CommodityLot> Lots { get; init; } = [];

    /// <summary>What the whole load costs, or fetches: the sum of the lots where there are any.</summary>
    public long Total => Lots.Count > 0 ? Lots.Sum(lot => lot.Total) : (long)UnitPrice * Tonnes;

    /// <summary>The Commander's own eyes rather than somebody's report.</summary>
    public bool IsTheirs => Market.Source == PriceSource.Seen;
}

/// <summary>
/// One commodity's part of an offer: the tonnage asked for, or a single tonne where none was.
/// </summary>
public sealed record CommodityLot(string Commodity, int UnitPrice, int Tonnes)
{
    public long Total => (long)UnitPrice * Tonnes;
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
        var wants = query.Wants;

        foreach (var market in markets)
        {
            if (!Admits(query, market, origin, out var distance))
            {
                continue;
            }

            var lots = new List<CommodityLot>(wants.Count);
            var available = 0;

            foreach (var want in wants)
            {
                if (Lot(query, want, market) is not { } lot)
                {
                    break;
                }

                if (lots.Count == 0)
                {
                    available = lot.Available;
                }

                lots.Add(new CommodityLot(want.Commodity, lot.Price, want.Tonnes ?? 1));
            }

            // A station that cannot answer for every commodity does not answer.
            if (lots.Count < wants.Count)
            {
                continue;
            }

            offers.Add(new CommodityOffer(
                market,
                lots[0].UnitPrice,
                query.Tonnes is { } asked ? Math.Min(asked, available) : available,
                distance)
            {
                Lots = lots,
            });
        }

        // Nearest first when asked (#296), the price breaking a tie between two stations in one system.
        IEnumerable<CommodityOffer> ranked = query.OrderBy == CommodityOrder.Distance && origin is not null
            ? offers.OrderBy(offer => offer.Distance).ThenBy(offer => Score(query, offer))
            : offers.OrderBy(offer => Score(query, offer));

        var order = ranked.ToList();

        if (query.Reversed)
        {
            order.Reverse();
        }

        return [.. order.Take(Math.Max(1, query.Limit))];
    }

    /// <summary>
    /// How many of the markets could answer for one commodity on its own, under every other filter the
    /// question carries.
    /// </summary>
    public static int Stocking(
        CommodityQuery query,
        CommodityWant want,
        IEnumerable<MarketSnapshot> markets,
        MarketSnapshot? origin) =>
        markets.Count(market => Admits(query, market, origin, out _) && Lot(query, want, market) is not null);

    /// <summary>The filters on the station itself, whatever it trades.</summary>
    private static bool Admits(
        CommodityQuery query,
        MarketSnapshot market,
        MarketSnapshot? origin,
        out double distance)
    {
        distance = 0;

        if (market.IsCarrier && !query.IncludeCarriers)
        {
            return false;
        }

        if (query.LargePadOnly && !market.HasLargePad)
        {
            return false;
        }

        // A surface pad is a descent the Commander did not ask for (#296).
        if (!query.SurfaceStations && market.IsSurface)
        {
            return false;
        }

        // Unknown is not far: a station the index gives no arrival distance for stays in.
        if (query.MaxStationDistance is { } furthest
            && market.DistanceToArrival is { } arrival
            && arrival > furthest)
        {
            return false;
        }

        distance = origin?.DistanceTo(market) ?? 0;

        return origin is null || distance <= query.MaxDistance;
    }

    /// <summary>The station's price and its supply, or demand when selling, or null where it cannot answer.</summary>
    private static (int Price, int Available)? Lot(CommodityQuery query, CommodityWant want, MarketSnapshot market)
    {
        if (market.Quote(want.Commodity) is not { } quote)
        {
            return null;
        }

        // Supply is a filter and not a footnote.
        var (price, available) = query.Side == TradeSide.Buying
            ? (quote.BuyPrice, quote.Supply)
            : (quote.SellPrice, quote.Demand);

        if (price <= 0 || available <= 0)
        {
            return null;
        }

        if (want.Tonnes is { } wanted && available < wanted)
        {
            return null;
        }

        // A floor rather than a load (#296): the station is excluded for having less than this, whatever
        // the Commander means to carry away.
        if (query.MinAvailable is { } floor && available < floor)
        {
            return null;
        }

        return (price, available);
    }

    /// <summary>Lower is better, both ways round.</summary>
    private static double Score(CommodityQuery query, CommodityOffer offer)
    {
        if (query.Wants.All(want => want.Tonnes is null))
        {
            // No tonnage, so nothing to weigh the distance against: every lot is a single tonne.
            return query.Side == TradeSide.Buying ? offer.Total : -offer.Total;
        }

        var trip = offer.Distance * CreditsPerLightYear;

        return query.Side == TradeSide.Buying
            ? offer.Total + trip
            : -offer.Total + trip;
    }
}
