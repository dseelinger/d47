namespace D47.Core.Knowledge;

/// <summary>One system on a plotted route.</summary>
/// <param name="System">The system to set as the next destination.</param>
/// <param name="Jumps">How many jumps to reach it from the previous waypoint.</param>
/// <param name="DistanceLeft">Light years still to go after arriving.</param>
/// <param name="IsNeutron">
/// Whether this one is a neutron star, which is the reason for the waypoint: it is where the
/// Commander has to fly into the jet cone rather than just plot onward.
/// </param>
public sealed record RouteWaypoint(string System, int Jumps, double? DistanceLeft, bool IsNeutron)
{
    /// <summary>
    /// The remaining distance where there is one worth reporting, and null where there is not (#405).
    /// </summary>
    public double? DistanceLeftToReport => DistanceLeft is >= 0.5 ? DistanceLeft : null;
}

/// <summary>A plotted route between two systems.</summary>
public sealed record PlottedRoute(
    string Origin,
    string Destination,
    double TotalDistance,
    int TotalJumps,
    IReadOnlyList<RouteWaypoint> Waypoints);

/// <summary>One body worth scanning, on a Road to Riches stop.</summary>
public sealed record RichesBody(string Name, string? Subtype)
{
    public long? MappingValue { get; init; }

    /// <summary>Light seconds from the entry point.</summary>
    public double? DistanceToArrival { get; init; }

    public bool Terraformable { get; init; }
}

public sealed record RichesStop(string System, int Jumps, IReadOnlyList<RichesBody> Bodies);

public sealed record RichesRoute(IReadOnlyList<RichesStop> Stops)
{
    public long TotalValue => Stops.SelectMany(stop => stop.Bodies).Sum(body => body.MappingValue ?? 0);

    public int TotalJumps => Stops.Sum(stop => stop.Jumps);
}

/// <summary>
/// One species the plotter says is on a body, with what it pays (Phase 18, "Find the exobiology").
/// </summary>
/// <param name="Value">Taken from the response, never computed.</param>
public sealed record ExobiologySpecies(string Genus, string Name, int Count, long Value);

/// <summary>One body on an exobiology route, and what is growing on it.</summary>
public sealed record ExobiologyBody(string Name, string? Subtype)
{
    /// <summary>Light seconds from the entry point.</summary>
    public double? DistanceToArrival { get; init; }

    /// <summary>What the biology on this body is worth, as the service totals it.</summary>
    public long LandmarkValue { get; init; }

    public IReadOnlyList<ExobiologySpecies> Species { get; init; } = [];
}

public sealed record ExobiologyStop(string System, int Jumps, IReadOnlyList<ExobiologyBody> Bodies)
{
    public long Value => Bodies.Sum(body => body.LandmarkValue);
}

public sealed record ExobiologyRoute(IReadOnlyList<ExobiologyStop> Stops)
{
    public long TotalValue => Stops.Sum(stop => stop.Value);

    public int TotalJumps => Stops.Sum(stop => stop.Jumps);
}

/// <summary>A validated exobiology plot.</summary>
public sealed record ExobiologyQuery
{
    private ExobiologyQuery()
    {
    }

    public required string From { get; init; }

    public double JumpRange { get; init; } = 50;

    /// <summary>How far from the origin to look, in light years.</summary>
    public double Radius { get; init; } = 200;

    /// <summary>How many systems to visit.</summary>
    public int MaxResults { get; init; } = 10;

    /// <summary>The least a body's biology must be worth to be worth stopping for.</summary>
    public long MinimumValue { get; init; } = 1_000_000;

    /// <summary>Light seconds.</summary>
    public double MaxDistanceToArrival { get; init; } = 10_000;

    /// <summary>Whether the route should come back to where it started.</summary>
    public bool Loop { get; init; } = true;

    public static bool TryParse(
        string? from,
        double? jumpRange,
        double? radius,
        int? maxResults,
        long? minimumValue,
        double? maxDistanceToArrival,
        bool? loop,
        out ExobiologyQuery query,
        out string failure)
    {
        query = null!;
        failure = string.Empty;

        if (string.IsNullOrWhiteSpace(from))
        {
            failure = "I don't know where the Commander is right now, so I need a system to start from.";
            return false;
        }

        query = new ExobiologyQuery
        {
            From = from.Trim(),
            JumpRange = Math.Clamp(jumpRange ?? 50, 10.01, 500),
            Radius = Math.Clamp(radius ?? 200, 10, 10_000),

            // Bounded low for the same reason the Road to Riches plot is: every extra stop is another line to
            // be read out, and the route is flown one system at a time regardless.
            MaxResults = Math.Clamp(maxResults ?? 10, 1, 50),
            MinimumValue = Math.Clamp(minimumValue ?? 1_000_000, 0, 100_000_000),
            MaxDistanceToArrival = Math.Clamp(maxDistanceToArrival ?? 10_000, 100, 1_000_000),
            Loop = loop ?? true,
        };

        return true;
    }
}

/// <summary>Some tonnes of one commodity, at the price they moved at.</summary>
public sealed record TradeLot(string Commodity, int Amount, int UnitPrice)
{
    public long Value => (long)Amount * UnitPrice;
}

/// <summary>
/// One station on a trade route, and everything that happens while the Commander is standing on it
/// (Phase 36).
/// </summary>
public sealed record TradeStop(string System, string Station)
{
    /// <summary>Light years flown to get here from the previous stop.</summary>
    public double? Distance { get; init; }

    /// <summary>Light seconds from this system's entry point to the pad.</summary>
    public double? DistanceToArrival { get; init; }

    /// <summary>Sold on arrival, at this station's price.</summary>
    public IReadOnlyList<TradeLot> Sell { get; init; } = [];

    /// <summary>Landed with and kept aboard — the twist.</summary>
    public IReadOnlyList<TradeLot> Hold { get; init; } = [];

    /// <summary>Bought here before leaving, at this station's price.</summary>
    public IReadOnlyList<TradeLot> Buy { get; init; } = [];

    /// <summary>Credits in hand on leaving this stop, after everything above.</summary>
    public long Credits { get; init; }

    /// <summary>When these prices were reported.</summary>
    public DateTimeOffset? PricesSeen { get; init; }

    /// <summary>Whether this market is one the Commander stood in themselves.</summary>
    public bool PricesAreYours { get; init; }

    /// <summary>Whether a lot leaving here was cut short by what the destination will take.</summary>
    public bool CappedByDemand { get; init; }
}

/// <summary>A trade route d47 worked out (Phase 36).</summary>
public sealed record TradeRoute(IReadOnlyList<TradeStop> Stops)
{
    /// <summary>What the Commander said they were trading with.</summary>
    public long Capital { get; init; }

    /// <summary>Credits at the end, less the capital.</summary>
    public long TotalProfit { get; init; }

    /// <summary>Light years over the whole route.</summary>
    public double TotalDistance => Stops.Sum(stop => stop.Distance ?? 0);

    /// <summary>Whether the route ends where it began.</summary>
    public bool Loop { get; init; }

    /// <summary>How many markets the arithmetic had to choose between.</summary>
    public int MarketsConsidered { get; init; }

    /// <summary>Fleet carriers left out.</summary>
    public int CarriersIgnored { get; init; }

    /// <summary>How many markets came from the Commander's own <c>Market.json</c>.</summary>
    public int MarketsSeenInPerson { get; init; }
}

/// <summary>A validated neutron or long-range plot.</summary>
public sealed record RouteQuery
{
    private RouteQuery()
    {
    }

    public required string From { get; init; }

    public required string To { get; init; }

    public double JumpRange { get; init; } = 50;

    /// <summary>How strictly to insist on the direct line, 1 to 100.</summary>
    public int Efficiency { get; init; } = 60;

    public static bool TryParse(
        string? from,
        string? to,
        double? jumpRange,
        int? efficiency,
        out RouteQuery query,
        out string failure)
    {
        query = null!;
        failure = string.Empty;

        if (string.IsNullOrWhiteSpace(from))
        {
            failure = "I don't know where the Commander is right now, so I need a system to plot from.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(to))
        {
            failure = "No destination was named.";
            return false;
        }

        if (jumpRange is not null && jumpRange <= 10)
        {
            // The service's own floor, and worth its own sentence: a Commander whose ship really does jump 8
            // light years has asked a question the plotter cannot answer, and telling them that is more use
            // than "range must be greater than 10 LY".
            failure =
                $"The route plotter needs a jump range over 10 light years and this ship has "
                + $"{jumpRange.Value:N1}. It cannot plot for a ship that short-ranged.";
            return false;
        }

        query = new RouteQuery
        {
            From = from.Trim(),
            To = to.Trim(),
            JumpRange = Math.Clamp(jumpRange ?? 50, 10.01, 500),

            // Never 100, which the service accepts and then cannot route with.
            Efficiency = Math.Clamp(efficiency ?? 60, 1, 99),
        };

        return true;
    }
}

/// <summary>A validated Road to Riches plot.</summary>
public sealed record RichesQuery
{
    private RichesQuery()
    {
    }

    public required string From { get; init; }

    public double JumpRange { get; init; } = 50;

    /// <summary>How far from the origin to look for bodies, in light years.</summary>
    public double Radius { get; init; } = 500;

    /// <summary>How many systems to visit.</summary>
    public int MaxResults { get; init; } = 10;

    /// <summary>The least a body must be worth to be worth stopping for.</summary>
    public long MinimumValue { get; init; } = 500_000;

    /// <summary>Light seconds.</summary>
    public double MaxDistanceToArrival { get; init; } = 10_000;

    /// <summary>Whether the route should come back to where it started.</summary>
    public bool Loop { get; init; } = true;

    public static bool TryParse(
        string? from,
        double? jumpRange,
        double? radius,
        int? maxResults,
        long? minimumValue,
        double? maxDistanceToArrival,
        bool? loop,
        out RichesQuery query,
        out string failure)
    {
        query = null!;
        failure = string.Empty;

        if (string.IsNullOrWhiteSpace(from))
        {
            failure = "I don't know where the Commander is right now, so I need a system to start from.";
            return false;
        }

        query = new RichesQuery
        {
            From = from.Trim(),
            JumpRange = Math.Clamp(jumpRange ?? 50, 10.01, 500),
            Radius = Math.Clamp(radius ?? 500, 10, 10_000),

            // Bounded low, because every extra stop is another line the Commander has to be read and the
            // route is flown one system at a time regardless.
            MaxResults = Math.Clamp(maxResults ?? 10, 1, 50),
            MinimumValue = Math.Clamp(minimumValue ?? 500_000, 0, 100_000_000),
            MaxDistanceToArrival = Math.Clamp(maxDistanceToArrival ?? 10_000, 100, 1_000_000),
            Loop = loop ?? true,
        };

        return true;
    }
}

/// <summary>A validated trade plot.</summary>
public sealed record TradeQuery
{
    private TradeQuery()
    {
    }

    public required string System { get; init; }

    public required string Station { get; init; }

    public long Capital { get; init; }

    public int CargoCapacity { get; init; }

    /// <summary>How many stations to visit.</summary>
    public int MaxHops { get; init; } = 5;

    public double MaxHopDistance { get; init; } = 40;

    /// <summary>
    /// Whether the route comes back to the station it started from, so an evening's trading ends at the
    /// Commander's own base rather than four systems away.
    /// </summary>
    public bool Loop { get; init; }

    /// <summary>Light seconds from the entry point that a station may sit at.</summary>
    public double MaxSystemDistance { get; init; } = 1_000;

    public bool LargePadOnly { get; init; }

    /// <summary>How stale a reported price may be, in hours.</summary>
    public int MaxPriceAge { get; init; } = 720;

    public static bool TryParse(
        string? system,
        string? station,
        long? capital,
        int? cargoCapacity,
        int? maxHops,
        double? maxHopDistance,
        double? maxSystemDistance,
        bool largePadOnly,
        int? maxPriceAge,
        bool? loop,
        out TradeQuery query,
        out string failure)
    {
        query = null!;
        failure = string.Empty;

        if (string.IsNullOrWhiteSpace(system) || string.IsNullOrWhiteSpace(station))
        {
            // The service keys the whole plot on a starting station id, so there is no version of this
            // question that can be asked from supercruise.
            failure =
                "A trade route is plotted from a station, and the Commander is not docked at one. "
                + "Ask which station to start from, or plot it once they have landed.";
            return false;
        }

        if (capital is not > 0)
        {
            failure = "How many credits should the route be planned around? I don't send the Commander's "
                      + "balance unless they say a figure.";
            return false;
        }

        if (cargoCapacity is not > 0)
        {
            failure = "I don't know this ship's cargo capacity, so I need to be told how many tonnes it "
                      + "can carry.";
            return false;
        }

        query = new TradeQuery
        {
            System = system.Trim(),
            Station = station.Trim(),
            Capital = Math.Clamp(capital.Value, 1, 1_000_000_000_000),
            CargoCapacity = Math.Clamp(cargoCapacity.Value, 1, 100_000),
            MaxHops = Math.Clamp(maxHops ?? 5, 1, 10),
            MaxHopDistance = Math.Clamp(maxHopDistance ?? 40, 1, 500),
            MaxSystemDistance = Math.Clamp(maxSystemDistance ?? 1_000, 1, 1_000_000),
            LargePadOnly = largePadOnly,
            MaxPriceAge = Math.Clamp(maxPriceAge ?? 720, 1, 8_760),
            Loop = loop ?? false,
        };

        return true;
    }
}

/// <summary>The seam to whatever plots routes (Phase 14, "Route Planning").</summary>
public interface IRouteService
{
    Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken);

    Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken);

    /// <summary>The fourth plot type (Phase 18, "Find the exobiology").</summary>
    Task<ExobiologyRoute?> PlotExobiologyAsync(ExobiologyQuery query, CancellationToken cancellationToken);
}

/// <summary>The seam to whatever gathers markets and plans a trade route over them (Phase 36).</summary>
public interface ITradePlanService
{
    Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken);

    /// <summary>Where to buy one commodity, or where to dump it (Phase 49).</summary>
    Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken);

    /// <summary>Where to buy everything one construction site still needs (Phase 50).</summary>
    Task<SourcingAnswer> SourceConstructionAsync(SourcingSearch search, CancellationToken cancellationToken);
}

/// <summary>One build's sourcing question (Phase 50).</summary>
/// <param name="System">Where to search out from.</param>
/// <param name="Station">Where the Commander is standing, if they are.</param>
/// <param name="Outstanding">What the site still needs, already taken as given.</param>
/// <param name="MaxDistance">How far to sweep, in light years.</param>
/// <param name="LargePadOnly">Whether to insist on a large pad.</param>
/// <param name="MaxStops">
/// A ceiling on stations, so the answer stays one a Commander can act on.
/// </param>
/// <param name="MaxPriceAge">How stale a reported price may be, in hours.</param>
public sealed record SourcingSearch(
    string System,
    string? Station,
    IReadOnlyList<Journal.ConstructionResource> Outstanding,
    double MaxDistance = 50,
    bool LargePadOnly = false,
    int MaxStops = 6,
    int MaxPriceAge = 720);

/// <summary>The covering plan, and what a Commander is owed alongside it.</summary>
/// <param name="Plan">The stops, best first, and what could not be priced or filled.</param>
/// <param name="Considered">How many markets the sweep returned at all.</param>
/// <param name="DroppedAsStale">How many carried a price older than the bound.</param>
/// <param name="OriginKnown">Whether the sweep found the reference system at all.</param>
public sealed record SourcingAnswer(
    SourcingPlan Plan,
    int Considered,
    int DroppedAsStale,
    bool OriginKnown)
{
    public static readonly SourcingAnswer Empty = new(SourcingPlan.Empty, 0, 0, false);
}

/// <summary>One commodity question, with everything the sweep needs beside it (Phase 49).</summary>
/// <param name="System">Where to search out from.</param>
/// <param name="Station">Where the Commander is standing, if they are.</param>
/// <param name="Query">What was asked, and how to rank it.</param>
/// <param name="MaxPriceAge">How stale a reported price may be, in hours.</param>
public sealed record CommoditySearch(
    string System,
    string? Station,
    CommodityQuery Query,
    int MaxPriceAge = 720);

/// <summary>What came back, and what a Commander is owed alongside it.</summary>
/// <param name="Offers">The ranking, best first.</param>
/// <param name="Considered">How many markets the sweep returned at all.</param>
/// <param name="DroppedAsStale">How many carried a price older than the bound.</param>
/// <param name="OriginKnown">Whether the sweep found the reference system at all.</param>
public sealed record CommodityAnswer(
    IReadOnlyList<CommodityOffer> Offers,
    int Considered,
    int DroppedAsStale,
    bool OriginKnown)
{
    public static readonly CommodityAnswer Empty = new([], 0, 0, false);

    /// <summary>
    /// How far the search actually reached, in light years, or null when it reached the whole radius it
    /// was asked for (#156).
    /// </summary>
    public double? Horizon { get; init; }

    /// <summary>
    /// Whether the search can speak for the radius it was asked about, rather than for ground it never
    /// covered (#350).
    /// </summary>
    public bool Complete { get; init; } = true;
}
