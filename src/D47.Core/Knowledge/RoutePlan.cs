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

public sealed record TradeCommodity(string Name, int Amount, long TotalProfit);

/// <summary>One buy-and-sell leg of a trade route.</summary>
public sealed record TradeHop(
    string FromSystem,
    string FromStation,
    string ToSystem,
    string ToStation)
{
    public double? Distance { get; init; }

    /// <summary>Light seconds from the destination system's entry point to the station.</summary>
    public double? DistanceToArrival { get; init; }

    public long TotalProfit { get; init; }

    public long CumulativeProfit { get; init; }

    public IReadOnlyList<TradeCommodity> Commodities { get; init; } = [];

    /// <summary>
    /// When the destination's market was last reported. Crowd-sourced like outfitting stock, and
    /// the reason a route can be arithmetically perfect and still worthless: a price from four
    /// years ago is not a price.
    /// </summary>
    public DateTimeOffset? MarketSeen { get; init; }
}

public sealed record TradeRoute(IReadOnlyList<TradeHop> Hops)
{
    public long TotalProfit => Hops.Count == 0 ? 0 : Hops[^1].CumulativeProfit;
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

    public int MaxHops { get; init; } = 4;

    public double MaxHopDistance { get; init; } = 40;

    /// <summary>Light seconds from the entry point that a station may sit at.</summary>
    public double MaxSystemDistance { get; init; } = 1_000;

    public bool LargePadOnly { get; init; }

    /// <summary>How stale a reported price may be, in hours.</summary>
    public int MaxPriceAge { get; init; } = 720;

    /// <summary>Whether a station may be visited more than once on the route.</summary>
    public bool Unique { get; init; } = true;

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
            MaxHops = Math.Clamp(maxHops ?? 4, 1, 8),
            MaxHopDistance = Math.Clamp(maxHopDistance ?? 40, 1, 500),
            MaxSystemDistance = Math.Clamp(maxSystemDistance ?? 1_000, 1, 1_000_000),
            LargePadOnly = largePadOnly,
            MaxPriceAge = Math.Clamp(maxPriceAge ?? 720, 1, 8_760),
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

    Task<TradeRoute?> PlotTradeAsync(TradeQuery query, CancellationToken cancellationToken);
}
