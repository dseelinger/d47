using System.Globalization;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Capabilities.Builtin;

/// <summary>
/// Plotting a route (Phase 14, "Route Planning").
/// <para>
/// Its own capability rather than more tools on <see cref="GalaxyCapability"/>, because plotting
/// is not searching: a search is a request and a reply, and a plot is a job that is queued and
/// waited on — three seconds for a neutron route across the galaxy, forty-eight for a four-hop
/// trade chain. It shares the galaxy search's one setting and its one disclosure, because it is
/// the same host and the same decision a Commander is making.
/// </para>
/// <para>
/// <b>Mining routes are not here, and the reason is measured.</b> There is no mining route
/// endpoint: <c>api/mining/route</c> is a 404. What the service does have is the ring index, and
/// that is <c>find_body</c> on the galaxy search — a hotspot material, how many overlap, and how
/// rich the reserves are. Naming a mining route tool that quietly did a body search would be a
/// worse answer than not having one.
/// </para>
/// <para>
/// <b>The trade route is no longer a plot at all (Phase 36).</b> Three of these four tools
/// hand the question to a service and read back the answer; <c>plot_trade_route</c> now asks for
/// markets and does the arithmetic here, which is what lets it hold cargo past a station and come
/// home again. It kept its name because it answers the same question, and it replaced the borrowed
/// planner rather than standing beside it: two tools that both plan trade routes and disagree is
/// worse than either alone.
/// </para>
/// </summary>
public static class RouteCapability
{
    public const string Id = "routes";

    /// <param name="routes">
    /// The plotter, or null where none is composed — under the designer and in a test that is not
    /// about it. Null and switched off answer the same way, for the reason Phase 3 gives.
    /// </param>
    /// <param name="commander">
    /// Where the Commander is and what they are flying. Read for the origin, the station a trade
    /// route starts from, and the ship's jump range and hold — the three things every plot needs
    /// and nobody wants to say out loud.
    /// </param>
    /// <param name="trade">
    /// d47's own trade planner, or null where none is composed. Separate from
    /// <paramref name="routes"/> because it is a different protocol — lookups and arithmetic
    /// rather than a job that is queued and polled for — and the seam says so.
    /// </param>
    /// <param name="plans">
    /// Where a plan is kept once it is made (Phase 37), or null where nothing is drawing
    /// them. The capability describes a route and forgets it, which is correct for speech and
    /// leaves a surface with nothing to show — so what it worked out is written here, and the
    /// Routing tab and this path stay one answer rather than two.
    /// </param>
    /// <param name="now">The clock, injected. Nothing in Core reads it directly.</param>
    public static CapabilityDescriptor Create(
        IRouteService? routes,
        ITradePlanService? trade,
        Func<CommanderGameState?> commander,
        Configuration.SettingsService settings,
        RoutePlanBook? plans = null,
        Func<DateTimeOffset>? now = null) => new()
    {
        Id = Id,
        Group = "Knowledge",
        Name = "Route planning",
        Summary = "Plot a neutron route, a Road to Riches loop, or a trade run.",
        Examples =
        [
            "plot me a route to Colonia",
            "plan a road to riches loop",
            "find me a trade route with fifty million",
        ],

        // No keywords, for the reason the galaxy search has none: every question here carries the
        // thing being asked about, and a router that cannot extract a destination cannot route
        // "plot me a route to Colonia" anywhere useful.
        Tools =
        [
            new ToolDefinition
            {
                Name = "plot_route",
                Description =
                    "Plot a jump route between two star systems, using neutron star boosts where they "
                    + "help. This is how a long trip is planned — the in-game plotter cannot reach across "
                    + "the galaxy and does not know about neutron boosting.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "to",
                        Type = ToolParameterType.String,
                        Description = "The destination system.",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "from",
                        Type = ToolParameterType.String,
                        Description = "Where to plot from. Defaults to where the Commander is now.",
                    },
                    new ToolParameter
                    {
                        Name = "jump_range",
                        Type = ToolParameterType.Number,
                        Description =
                            "The ship's jump range in light years. Defaults to this ship's, from the "
                            + "journal. Must be over 10.",
                    },
                    new ToolParameter
                    {
                        Name = "efficiency",
                        Type = ToolParameterType.Integer,
                        Description =
                            "How strictly to hold to the direct line, 1 to 99. Lower finds more neutron "
                            + "stars and so fewer jumps, at the cost of flying further off course. "
                            + "Defaults to 60.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    PlotAsync(routes, commander, settings, plans, now, arguments, cancellationToken),
            },
            new ToolDefinition
            {
                Name = "plot_exploration_route",
                Description =
                    "Plan a Road to Riches loop: a tour of nearby systems holding bodies worth scanning "
                    + "and mapping, ordered so the trip is short and the payout is high.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "from",
                        Type = ToolParameterType.String,
                        Description = "Where to start. Defaults to where the Commander is now.",
                    },
                    new ToolParameter
                    {
                        Name = "jump_range",
                        Type = ToolParameterType.Number,
                        Description = "The ship's jump range in light years. Defaults to this ship's.",
                    },
                    new ToolParameter
                    {
                        Name = "radius",
                        Type = ToolParameterType.Number,
                        Description = "How far from the start to look, in light years. Defaults to 500.",
                    },
                    new ToolParameter
                    {
                        Name = "stops",
                        Type = ToolParameterType.Integer,
                        Description = "How many systems to visit, 1 to 50. Defaults to 10.",
                    },
                    new ToolParameter
                    {
                        Name = "minimum_value",
                        Type = ToolParameterType.Integer,
                        Description =
                            "The least a body must be worth mapping to be worth stopping for, in credits. "
                            + "Defaults to 500,000.",
                    },
                    new ToolParameter
                    {
                        Name = "max_distance_to_arrival",
                        Type = ToolParameterType.Number,
                        Description =
                            "How far in-system a body may sit, in light seconds. Defaults to 10,000.",
                    },
                    new ToolParameter
                    {
                        Name = "loop",
                        Type = ToolParameterType.Boolean,
                        Description = "Come back to where the route started. Defaults to true.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    PlotRichesAsync(routes, commander, settings, plans, now, arguments, cancellationToken),
            },
            new ToolDefinition
            {
                Name = "plot_trade_route",
                Description =
                    "Work out a chain of profitable trade runs from the station the Commander is docked "
                    + "at, holding cargo past a station where a later one pays better. Needs to be told "
                    + "how many credits to trade with; their balance is never used unless they say it.",
                Parameters =
                [
                    new ToolParameter
                    {
                        Name = "capital",
                        Type = ToolParameterType.Integer,
                        Description = "How many credits to trade with. Required; never inferred.",
                        Required = true,
                    },
                    new ToolParameter
                    {
                        Name = "cargo_capacity",
                        Type = ToolParameterType.Integer,
                        Description =
                            "The hold's size in tonnes. Defaults to this ship's, from the journal.",
                    },
                    new ToolParameter
                    {
                        Name = "hops",
                        Type = ToolParameterType.Integer,
                        Description = "How many legs to plan, 1 to 10. Defaults to 5.",
                    },
                    new ToolParameter
                    {
                        Name = "loop",
                        Type = ToolParameterType.Boolean,
                        Description = "End back where it started. Defaults to false.",
                    },
                    new ToolParameter
                    {
                        Name = "max_hop_distance",
                        Type = ToolParameterType.Number,
                        Description = "The longest single leg, in light years. Defaults to 40.",
                    },
                    new ToolParameter
                    {
                        Name = "max_station_distance",
                        Type = ToolParameterType.Number,
                        Description =
                            "How far in-system a station may sit, in light seconds. Defaults to 1,000.",
                    },
                    new ToolParameter
                    {
                        Name = "large_pad",
                        Type = ToolParameterType.Boolean,
                        Description = "Only stations with a large landing pad.",
                    },
                    new ToolParameter
                    {
                        Name = "max_price_age",
                        Type = ToolParameterType.Integer,
                        Description =
                            "How stale a reported price may be, in hours. Defaults to 720, one month.",
                    },
                ],
                Handler = (arguments, cancellationToken) =>
                    PlanTradeAsync(trade, commander, settings, plans, now, arguments, cancellationToken),
            },
        ],
        Display = new CapabilityDisplay { PanelTitle = "Route planning", Order = 48 },
    };

    private const string Unavailable =
        "Route planning is switched off, so I can't plot that. It shares the galaxy search setting, "
        + "which the Commander can turn on in settings.";

    private static bool Ready(IRouteService? routes, Configuration.SettingsService settings) =>
        routes is not null && settings.Current.Knowledge.GalaxySearch;

    private static async Task<ToolResult> PlotAsync(
        IRouteService? routes,
        Func<CommanderGameState?> commander,
        Configuration.SettingsService settings,
        RoutePlanBook? plans,
        Func<DateTimeOffset>? now,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!Ready(routes, settings))
        {
            return ToolResult.Error(Unavailable);
        }

        var active = commander();

        arguments.TryGetString("to", out var to);

        var from = arguments.TryGetString("from", out var explicitFrom) && !string.IsNullOrWhiteSpace(explicitFrom)
            ? explicitFrom
            : active?.Location.StarSystem;

        if (!RouteQuery.TryParse(
                from,
                to,
                Number(arguments, "jump_range") ?? active?.Ship.MaxJumpRange,
                arguments.TryGetInt32("efficiency", out var efficiency) ? efficiency : null,
                out var query,
                out var failure))
        {
            return ToolResult.Error(failure);
        }

        try
        {
            var route = await routes!.PlotAsync(query, cancellationToken).ConfigureAwait(false);

            if (route is null)
            {
                return ToolResult.Ok(
                    $"No route from {query.From} to {query.To} at {query.JumpRange:N1} light years a jump. "
                    + "A longer jump range, or a lower efficiency, gives the plotter more to work with.");
            }

            // Kept before it is described, so a surface has the whole route while the sentence
            // keeps its five waypoints (Phase 37).
            plans?.Record(route, $"{route.Origin} to {route.Destination}", now?.Invoke() ?? default);

            return ToolResult.Ok(Describe(route));
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// A plotted route as prose, cut to the next few waypoints.
    /// <para>
    /// Sol to Colonia is 131 waypoints. Reading them out is not an answer, and neither is a
    /// summary that hides how many there were — so the total goes first and the next handful
    /// follow, which is how the route is flown anyway: you plot the next waypoint when you get to
    /// this one.
    /// </para>
    /// </summary>
    private static string Describe(PlottedRoute route)
    {
        if (route.Waypoints.Count == 0)
        {
            return $"{route.Destination} is already where the Commander is.";
        }

        var report = new StringBuilder();

        report.AppendLine(
            $"{route.Origin} to {route.Destination}: {route.TotalDistance.ToString("N0", CultureInfo.InvariantCulture)} "
            + $"light years, {route.TotalJumps} jumps across {route.Waypoints.Count} "
            + $"waypoint{(route.Waypoints.Count == 1 ? "" : "s")}.");

        var shown = route.Waypoints.Take(WaypointsSpoken).ToArray();

        report.AppendLine();
        report.AppendLine(shown.Length == route.Waypoints.Count ? "The waypoints:" : $"The first {shown.Length}:");

        foreach (var waypoint in shown)
        {
            report.Append($"  {waypoint.System} — {waypoint.Jumps} jump{(waypoint.Jumps == 1 ? "" : "s")}");

            if (waypoint.IsNeutron)
            {
                // The only line that changes what the Commander does when they arrive.
                report.Append("; neutron, supercharge here");
            }

            if (waypoint.DistanceLeft is { } left)
            {
                report.Append($"; {left.ToString("N0", CultureInfo.InvariantCulture)} ly left");
            }

            report.AppendLine();
        }

        if (shown.Length < route.Waypoints.Count)
        {
            report.AppendLine();
            report.AppendLine(
                $"{route.Waypoints.Count - shown.Length} more after that. Ask again from further along "
                + "and I will plot the rest.");
        }

        return report.ToString().TrimEnd();
    }

    /// <summary>
    /// How much of a route is worth saying. Five is roughly what a Commander can act on before
    /// the next one is due, and every line past that is billed on every turn it stays in history.
    /// </summary>
    private const int WaypointsSpoken = 5;

    private static async Task<ToolResult> PlotRichesAsync(
        IRouteService? routes,
        Func<CommanderGameState?> commander,
        Configuration.SettingsService settings,
        RoutePlanBook? plans,
        Func<DateTimeOffset>? now,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!Ready(routes, settings))
        {
            return ToolResult.Error(Unavailable);
        }

        var active = commander();

        var from = arguments.TryGetString("from", out var explicitFrom) && !string.IsNullOrWhiteSpace(explicitFrom)
            ? explicitFrom
            : active?.Location.StarSystem;

        if (!RichesQuery.TryParse(
                from,
                Number(arguments, "jump_range") ?? active?.Ship.MaxJumpRange,
                Number(arguments, "radius"),
                arguments.TryGetInt32("stops", out var stops) ? stops : null,
                arguments.TryGetInt32("minimum_value", out var minimum) ? minimum : null,
                Number(arguments, "max_distance_to_arrival"),
                Flag(arguments, "loop"),
                out var query,
                out var failure))
        {
            return ToolResult.Error(failure);
        }

        try
        {
            var route = await routes!.PlotRichesAsync(query, cancellationToken).ConfigureAwait(false);

            if (route is null || route.Stops.Count == 0)
            {
                return ToolResult.Ok(
                    $"Nothing within {query.Radius:N0} light years of {query.From} is worth "
                    + $"{query.MinimumValue:N0} credits to map. A wider radius or a lower minimum will "
                    + "find something.");
            }

            plans?.Record(
                route,
                $"{route.Stops.Count} stops from {query.From}",
                now?.Invoke() ?? default);

            return ToolResult.Ok(Describe(route, query));
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    private static string Describe(RichesRoute route, RichesQuery query)
    {
        var report = new StringBuilder();

        var bodies = route.Stops.Sum(stop => stop.Bodies.Count);

        report.AppendLine(
            $"{route.Stops.Count} stop{(route.Stops.Count == 1 ? "" : "s")} from {query.From}, "
            + $"{bodies} bodies worth mapping, about "
            + $"{route.TotalValue.ToString("N0", CultureInfo.InvariantCulture)} credits over "
            + $"{route.TotalJumps} jumps{(query.Loop ? " and back" : "")}.");

        foreach (var stop in route.Stops.Take(StopsSpoken))
        {
            report.AppendLine();
            report.AppendLine($"{stop.System} — {stop.Jumps} jump{(stop.Jumps == 1 ? "" : "s")}, "
                              + $"{stop.Bodies.Count} bod{(stop.Bodies.Count == 1 ? "y" : "ies")}:");

            // The bodies at each stop are the part that gets long: a single system can carry
            // twenty. The best few are what decides whether to stop at all.
            foreach (var body in stop.Bodies.OrderByDescending(body => body.MappingValue ?? 0).Take(BodiesSpoken))
            {
                report.Append($"  {body.Name}");

                if (body.Subtype is not null)
                {
                    report.Append($", {body.Subtype.ToLowerInvariant()}");
                }

                if (body.Terraformable)
                {
                    report.Append(", terraformable");
                }

                if (body.MappingValue is { } value)
                {
                    report.Append($" — {value.ToString("N0", CultureInfo.InvariantCulture)} cr");
                }

                if (body.DistanceToArrival is { } arrival)
                {
                    report.Append($", {arrival.ToString("N0", CultureInfo.InvariantCulture)} ls out");
                }

                report.AppendLine();
            }

            if (stop.Bodies.Count > BodiesSpoken)
            {
                report.AppendLine($"  and {stop.Bodies.Count - BodiesSpoken} more here.");
            }
        }

        if (route.Stops.Count > StopsSpoken)
        {
            report.AppendLine();
            report.AppendLine($"{route.Stops.Count - StopsSpoken} more stops after those.");
        }

        return report.ToString().TrimEnd();
    }

    private const int StopsSpoken = 3;

    private const int BodiesSpoken = 4;

    private static async Task<ToolResult> PlanTradeAsync(
        ITradePlanService? trade,
        Func<CommanderGameState?> commander,
        Configuration.SettingsService settings,
        RoutePlanBook? plans,
        Func<DateTimeOffset>? now,
        ToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (trade is null || !settings.Current.Knowledge.GalaxySearch)
        {
            return ToolResult.Error(Unavailable);
        }

        var active = commander();

        if (!TradeQuery.TryParse(
                active?.Location.StarSystem,
                active?.Location is { Docked: true, StationName: { } station } ? station : null,
                arguments.TryGetInt32("capital", out var capital) ? capital : null,

                // The hold is the ship's, not the Commander's, and the plot means nothing without
                // it — so it comes from the journal where the balance deliberately does not.
                arguments.TryGetInt32("cargo_capacity", out var cargo) ? cargo : active?.Ship.CargoCapacity,
                arguments.TryGetInt32("hops", out var hops) ? hops : null,
                Number(arguments, "max_hop_distance"),
                Number(arguments, "max_station_distance"),
                arguments.TryGetBoolean("large_pad", out var largePad) && largePad,
                arguments.TryGetInt32("max_price_age", out var age) ? age : null,
                Flag(arguments, "loop"),
                out var query,
                out var failure))
        {
            return ToolResult.Error(failure);
        }

        try
        {
            var route = await trade.PlanAsync(query, cancellationToken).ConfigureAwait(false);

            // Null and empty are different answers and are worded differently. Null is "I could
            // not read the board you are standing in front of" — the origin's own market was not
            // among anything fetched — and empty is "nothing near here pays".
            if (route is null)
            {
                return ToolResult.Ok(
                    $"I can't see the commodity market at {query.Station}. Nobody has reported its "
                    + "prices, and the Commander has not opened it while I was watching — dock and open "
                    + "the commodity board once and I will have it.");
            }

            if (route.Stops.Count == 0)
            {
                return ToolResult.Ok(
                    $"No trade run out of {query.Station} pays with {query.Capital:N0} credits and "
                    + $"{query.CargoCapacity} tonnes, across {route.MarketsConsidered} markets within "
                    + $"{query.MaxHopDistance:N0} light years. More capital, a longer hop distance or a "
                    + "wider price age will find something.");
            }

            plans?.Record(
                route,
                $"{route.Stops.Count} stops from {query.Station}",
                now?.Invoke() ?? default);

            return ToolResult.Ok(Describe(route, query));
        }
        catch (GalaxyUnavailableException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }

    /// <summary>
    /// A trade route as prose. Written as a sequence of stops rather than of legs, because that is
    /// what the Commander flies: dock, sell these, keep those, buy that, go.
    /// </summary>
    private static string Describe(TradeRoute route, TradeQuery query)
    {
        var report = new StringBuilder();
        var legs = route.Stops.Count - 1;

        report.AppendLine(
            $"{legs} hop{(legs == 1 ? "" : "s")} from {query.Station}"
            + $"{(route.Loop ? " and back" : "")}, "
            + $"{route.TotalProfit.ToString("N0", CultureInfo.InvariantCulture)} credits on "
            + $"{query.Capital.ToString("N0", CultureInfo.InvariantCulture)} over "
            + $"{route.TotalDistance.ToString("N0", CultureInfo.InvariantCulture)} light years.");

        var capped = false;

        foreach (var stop in route.Stops)
        {
            report.AppendLine();
            report.Append($"{stop.Station} in {stop.System}");

            if (stop.Distance is { } distance)
            {
                report.Append($" — {distance.ToString("N1", CultureInfo.InvariantCulture)} ly");
            }

            if (stop.DistanceToArrival is { } arrival)
            {
                report.Append($", {arrival.ToString("N0", CultureInfo.InvariantCulture)} ls in");
            }

            report.AppendLine();

            foreach (var lot in stop.Sell)
            {
                report.AppendLine(
                    $"  sell {lot.Amount} × {lot.Commodity} at "
                    + $"{lot.UnitPrice.ToString("N0", CultureInfo.InvariantCulture)} — "
                    + $"{lot.Value.ToString("N0", CultureInfo.InvariantCulture)} cr");
            }

            // The line that is the whole phase. A Commander who is not told why they are flying
            // past a buyer will sell there, and the plan they were given stops being the plan.
            foreach (var lot in stop.Hold)
            {
                report.AppendLine(
                    lot.UnitPrice > 0
                        ? $"  keep {lot.Amount} × {lot.Commodity} — this station only pays "
                          + $"{lot.UnitPrice.ToString("N0", CultureInfo.InvariantCulture)}"
                        : $"  keep {lot.Amount} × {lot.Commodity} — no buyer here");
            }

            foreach (var lot in stop.Buy)
            {
                report.AppendLine(
                    $"  buy {lot.Amount} × {lot.Commodity} at "
                    + $"{lot.UnitPrice.ToString("N0", CultureInfo.InvariantCulture)} — "
                    + $"{lot.Value.ToString("N0", CultureInfo.InvariantCulture)} cr");
            }

            capped |= stop.CappedByDemand;

            // Prices are crowd-reported unless the Commander read them off the board themselves,
            // and a route that is arithmetically perfect against a four-year-old market is worth
            // nothing at all.
            if (stop.PricesSeen is { } seen)
            {
                report.AppendLine(
                    stop.PricesAreYours
                        ? $"  your own prices, read {seen:yyyy-MM-dd}"
                        : $"  prices reported {seen:yyyy-MM-dd}");
            }
        }

        report.AppendLine();
        report.Append($"Worked out over {route.MarketsConsidered} market");
        report.Append(route.MarketsConsidered == 1 ? "" : "s");

        if (route.MarketsSeenInPerson > 0)
        {
            report.Append($", {route.MarketsSeenInPerson} of them yours");
        }

        if (route.CarriersIgnored > 0)
        {
            report.Append(
                $"; {route.CarriersIgnored} fleet carrier{(route.CarriersIgnored == 1 ? "" : "s")} left "
                + "out, because they set their own prices and then move");
        }

        report.AppendLine(".");

        // The honest half of the saturation answer, and it is said every time rather than only
        // when it bites: d47 does not model the price drop from dumping past demand, so it never
        // plans past demand — and a profit that assumed otherwise would be wrong in a way that
        // reads exactly like the feature working (Phase 36).
        report.Append(
            capped
                ? "No leg sells more than a station asked for, and some were cut short by that. I "
                  + "don't model what dumping past demand does to the price, so I never plan past it."
                : "I don't model what dumping past demand does to the price, so no leg here sells "
                  + "more than a station asked for.");

        return report.ToString().TrimEnd();
    }

    private static double? Number(ToolArguments arguments, string name) =>
        arguments.Values.TryGetValue(name, out var raw)
        && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static bool? Flag(ToolArguments arguments, string name) =>
        arguments.Values.ContainsKey(name) && arguments.TryGetBoolean(name, out var value) ? value : null;
}
