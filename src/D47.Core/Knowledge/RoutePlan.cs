namespace D47.Core.Knowledge;

/// <summary>One system on a plotted route.</summary>
/// <param name="System">The system to set as the next destination.</param>
/// <param name="Jumps">How many jumps to reach it from the previous waypoint.</param>
/// <param name="DistanceLeft">Light years still to go after arriving.</param>
/// <param name="IsNeutron">
/// Whether this one is a neutron star, which is the whole point of the waypoint: it is where the
/// Commander has to fly into the jet cone rather than just plot onward.
/// </param>
public sealed record RouteWaypoint(string System, int Jumps, double? DistanceLeft, bool IsNeutron);

/// <summary>
/// A plotted route between two systems.
/// <para>
/// <see cref="Waypoints"/> is deliberately not the whole route. A Sol to Colonia plot is 131
/// waypoints and 168 jumps, which is a spreadsheet rather than an answer — the capability reads
/// out the first few and says how many there are, and the Commander plots the next one when they
/// get there, which is how the route is flown anyway.
/// </para>
/// </summary>
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

    /// <summary>Light seconds from the entry point. The number that decides whether it is worth it.</summary>
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
/// One species the plotter says is on a body, with what it pays (list.md Phase 18, "Find the
/// exobiology").
/// <para>
/// <b>The species, not the genus</b> — and that distinction is the whole reason this half of the
/// item is worth having. The game's own <c>SAASignalsFound</c> names <em>Bacterium</em> and stops,
/// while <em>Bacterium Alcyoneum</em> and <em>Bacterium Acies</em> are different money. So the
/// plotter can quote a figure where the journal can only name a genus, and d47 must never let the
/// two look like the same kind of answer.
/// </para>
/// </summary>
/// <param name="Value">
/// Taken from the response, never computed. The Phase 16 spike established that a Commander's own
/// sale history cannot price a species — 30 of 31 sold exactly once, with the row total covering an
/// unstated number of specimens — so the service's figure is the only sourced one.
/// </param>
public sealed record ExobiologySpecies(string Genus, string Name, int Count, long Value);

/// <summary>One body on an exobiology route, and what is growing on it.</summary>
public sealed record ExobiologyBody(string Name, string? Subtype)
{
    /// <summary>Light seconds from the entry point. The number that decides whether it is worth it.</summary>
    public double? DistanceToArrival { get; init; }

    /// <summary>
    /// What the biology on this body is worth, as the service totals it. Kept as reported rather
    /// than summed from <see cref="Species"/>: they agree today — measured against a live plot,
    /// where four species at 1,808,900, 1,766,600, 1,670,100 and 1,658,500 came to exactly the
    /// 6,904,100 reported — and if they ever stop agreeing, that is worth being able to see.
    /// </summary>
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

/// <summary>
/// A validated exobiology plot.
/// <para>
/// <b>What this can and cannot promise is set by where the data comes from.</b> A crowd-fed index
/// knows only what somebody has already scanned and uploaded — so a plotted route is a tour of
/// <em>known</em> biology, and by definition none of it is a first footfall, which is the thing that
/// pays five times over. That is not a defect to fix; it is the reason
/// <see cref="SystemName"/> exists beside it.
/// </para>
/// </summary>
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

    /// <summary>Light seconds. A body a quarter of a million light seconds out is not on the way.</summary>
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

            // Bounded low for the same reason the Road to Riches plot is: every extra stop is
            // another line to be read out, and the route is flown one system at a time regardless.
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
/// One station on a trade route, and everything that happens while the Commander is standing on
/// it (list.md Phase 36).
/// <para>
/// <b>A stop rather than a hop, and that is the phase in one record.</b> A hop is a leg with a
/// buy at one end and a sell at the other, which is the shape every planner assumes and the shape
/// that cannot express holding a commodity past a station that pays poorly for it. A stop can:
/// <see cref="Sell"/> is what was landed with and sold here, <see cref="Hold"/> is what was
/// landed with and <em>kept</em>, and <see cref="Buy"/> is what leaves with the ship. The state
/// carried between stops is credits and cargo, not credits.
/// </para>
/// </summary>
public sealed record TradeStop(string System, string Station)
{
    /// <summary>Light years flown to get here from the previous stop. Null at the first.</summary>
    public double? Distance { get; init; }

    /// <summary>Light seconds from this system's entry point to the pad.</summary>
    public double? DistanceToArrival { get; init; }

    /// <summary>Sold on arrival, at this station's price.</summary>
    public IReadOnlyList<TradeLot> Sell { get; init; } = [];

    /// <summary>
    /// Landed with and kept aboard — the twist. Priced at what this station <em>would</em> have
    /// paid, so the Commander can see what declining to sell here is worth.
    /// </summary>
    public IReadOnlyList<TradeLot> Hold { get; init; } = [];

    /// <summary>Bought here before leaving, at this station's price.</summary>
    public IReadOnlyList<TradeLot> Buy { get; init; } = [];

    /// <summary>Credits in hand on leaving this stop, after everything above.</summary>
    public long Credits { get; init; }

    /// <summary>
    /// When these prices were reported. Crowd-sourced unless <see cref="PricesAreYours"/>, and the
    /// reason a route can be arithmetically perfect and worth nothing: a price from four years ago
    /// is not a price.
    /// </summary>
    public DateTimeOffset? PricesSeen { get; init; }

    /// <summary>Whether this market is one the Commander stood in themselves.</summary>
    public bool PricesAreYours { get; init; }

    /// <summary>
    /// Whether a lot leaving here was cut short by what the destination will take. The honest half
    /// of the saturation answer: the plan never sells past demand, and says which legs that bit.
    /// </summary>
    public bool CappedByDemand { get; init; }
}

/// <summary>
/// A trade route d47 worked out (list.md Phase 36).
/// </summary>
public sealed record TradeRoute(IReadOnlyList<TradeStop> Stops)
{
    /// <summary>What the Commander said they were trading with. Never inferred, ever.</summary>
    public long Capital { get; init; }

    /// <summary>Credits at the end, less the capital. The whole point.</summary>
    public long TotalProfit { get; init; }

    /// <summary>Light years over the whole route.</summary>
    public double TotalDistance => Stops.Sum(stop => stop.Distance ?? 0);

    /// <summary>Whether the route ends where it began.</summary>
    public bool Loop { get; init; }

    /// <summary>How many markets the arithmetic had to choose between.</summary>
    public int MarketsConsidered { get; init; }

    /// <summary>
    /// Fleet carriers left out. Reported rather than hidden: a Commander who knows there were
    /// forty of them nearby knows why d47's best price is not the best price on the board.
    /// </summary>
    public int CarriersIgnored { get; init; }

    /// <summary>How many markets came from the Commander's own <c>Market.json</c>.</summary>
    public int MarketsSeenInPerson { get; init; }
}

/// <summary>
/// A validated neutron or long-range plot.
/// <para>
/// The bounds are the service's own, learned by breaking them: a jump range below 10 light years
/// is a 400 reading "range must be greater than 10 LY", and an efficiency of 100 is accepted and
/// then fails to route at all. Both are checked here so a Commander gets a sentence about what
/// they asked for rather than a rejection about a parameter they never mentioned.
/// </para>
/// </summary>
public sealed record RouteQuery
{
    private RouteQuery()
    {
    }

    public required string From { get; init; }

    public required string To { get; init; }

    public double JumpRange { get; init; } = 50;

    /// <summary>
    /// How strictly to insist on the direct line, 1 to 100. <b>Lower finds more neutron stars and
    /// so fewer jumps</b>, which is the opposite of what the name suggests: measured Sol to
    /// Colonia at a 50 ly range on 2026-08-14, efficiency 10 took 156 jumps, 25 took 157, 60 took
    /// 168, and 100 could not find a route at all.
    /// </summary>
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
            // The service's own floor, and worth its own sentence: a Commander whose ship really
            // does jump 8 light years has asked a question the plotter cannot answer, and telling
            // them that is more use than "range must be greater than 10 LY".
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

    /// <summary>Light seconds. A body a quarter of a million light seconds out is not on the way.</summary>
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

            // Bounded low, because every extra stop is another line the Commander has to be read
            // and the route is flown one system at a time regardless.
            MaxResults = Math.Clamp(maxResults ?? 10, 1, 50),
            MinimumValue = Math.Clamp(minimumValue ?? 500_000, 0, 100_000_000),
            MaxDistanceToArrival = Math.Clamp(maxDistanceToArrival ?? 10_000, 100, 1_000_000),
            Loop = loop ?? true,
        };

        return true;
    }
}

/// <summary>
/// A validated trade plot.
/// <para>
/// <b>Capital is never inferred.</b> The Commander's balance is in the journal and it is not sent
/// unless they say a number: it is the one figure here that is about them rather than about their
/// ship, and a route planner quietly reporting a Commander's net worth to a third party is not a
/// trade it was asked to make. Cargo capacity is a property of the hull and the route means
/// nothing without it, so that one is filled in from the ship.
/// </para>
/// </summary>
public sealed record TradeQuery
{
    private TradeQuery()
    {
    }

    public required string System { get; init; }

    public required string Station { get; init; }

    public long Capital { get; init; }

    public int CargoCapacity { get; init; }

    /// <summary>
    /// How many stations to visit. <b>Ten is the target and not a holy number</b> (list.md Phase
    /// 36): the naive formulation of a route that carries cargo between hops does not finish at
    /// that depth, and the bounded one does — measured at 300,000 leg evaluations for ten hops
    /// against a beam of 200, which is tens of milliseconds of arithmetic.
    /// </summary>
    public int MaxHops { get; init; } = 5;

    public double MaxHopDistance { get; init; } = 40;

    /// <summary>
    /// Whether the route comes back to the station it started from, so an evening's trading ends
    /// at the Commander's own base rather than four systems away.
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
            // The service keys the whole plot on a starting station id, so there is no version of
            // this question that can be asked from supercruise.
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

/// <summary>
/// The seam to whatever plots routes (list.md Phase 14, "Route Planning").
/// <para>
/// Separate from <see cref="IGalaxyService"/> even though the same host answers both, because the
/// protocol is a different one: a search is a request and a reply, and a plot is a job that is
/// submitted, queued and polled for — Sol to Colonia came back in three seconds and a four-hop
/// trade route took forty-eight. Mixing a call that returns and a call that has to be waited on
/// behind one interface would hide the difference that matters most to the caller.
/// </para>
/// <para>
/// Every method answers <c>null</c> for "there is no such route", which is a real answer rather
/// than a failure: an efficiency the plotter cannot satisfy, or a trade chain nobody can afford,
/// both come back empty and both are worth saying plainly.
/// </para>
/// </summary>
public interface IRouteService
{
    Task<PlottedRoute?> PlotAsync(RouteQuery query, CancellationToken cancellationToken);

    Task<RichesRoute?> PlotRichesAsync(RichesQuery query, CancellationToken cancellationToken);

    /// <summary>The fourth plot type (list.md Phase 18, "Find the exobiology").</summary>
    Task<ExobiologyRoute?> PlotExobiologyAsync(ExobiologyQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// The seam to whatever gathers markets and plans a trade route over them (list.md Phase 36).
/// <para>
/// <b>It left <see cref="IRouteService"/> when the planner became d47's own.</b> That interface
/// exists to describe one protocol — a job submitted, queued and polled for — and the trade plot
/// is no longer that: it is a handful of lookups and then arithmetic that runs here, which is a
/// different shape of wait and a different set of failures. Keeping it behind the plotting
/// interface would have hidden exactly the difference the interface exists to state.
/// </para>
/// <para>
/// Null means the origin's market could not be read at all, which is a different answer from a
/// route with no stops — the second is "nothing near here pays", and the first is "I could not
/// see the board you are standing in front of".
/// </para>
/// </summary>
public interface ITradePlanService
{
    Task<TradeRoute?> PlanAsync(TradeQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Where to buy one commodity, or where to dump it (list.md Phase 49).
    /// <para>
    /// On this interface rather than on its own, because it fetches nothing new: the same sweep
    /// the planner runs already returns whole markets, and the same cache already holds them. Two
    /// questions about the same evening's trading cost one pull.
    /// </para>
    /// </summary>
    Task<CommodityAnswer> FindCommodityAsync(CommoditySearch search, CancellationToken cancellationToken);
}

/// <summary>
/// One commodity question, with everything the sweep needs beside it (list.md Phase 49).
/// </summary>
/// <param name="System">Where to search out from.</param>
/// <param name="Station">
/// Where the Commander is standing, if they are. Only used to find the origin's own coordinates,
/// which is exact for the station under their feet and the one case that matters.
/// </param>
/// <param name="Query">What was asked, and how to rank it.</param>
/// <param name="MaxPriceAge">
/// How stale a reported price may be, in hours. Inherited from <see cref="TradeQuery.MaxPriceAge"/>
/// rather than invented a second time — a price bound that meant one thing to the planner and
/// another to this would be two answers to one question.
/// </param>
public sealed record CommoditySearch(
    string System,
    string? Station,
    CommodityQuery Query,
    int MaxPriceAge = 720);

/// <summary>
/// What came back, and what a Commander is owed alongside it.
/// </summary>
/// <param name="Offers">The ranking, best first.</param>
/// <param name="Considered">How many markets the sweep returned at all.</param>
/// <param name="DroppedAsStale">
/// How many carried a price older than the bound. <b>Said rather than swallowed</b>: "nothing
/// within fifty light years" and "eleven stations, all of them quoting last month" are different
/// answers, and only one of them means the Commander should widen the search.
/// </param>
/// <param name="OriginKnown">
/// Whether the sweep found the reference system at all. False means every distance is unknown and
/// the ranking fell back to price.
/// </param>
public sealed record CommodityAnswer(
    IReadOnlyList<CommodityOffer> Offers,
    int Considered,
    int DroppedAsStale,
    bool OriginKnown)
{
    public static readonly CommodityAnswer Empty = new([], 0, 0, false);
}
