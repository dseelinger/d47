using D47.Core.Journal;

namespace D47.Core.Knowledge;

[Flags]
public enum RouteStopMarks
{
    None = 0,
    Neutron = 1,
    WhiteDwarf = 2,
    BlackHole = 4,
    NotScoopable = 8,
    Restock = 16,
}

/// <summary>One stop of a route to draw; <see cref="Position"/> is null where the source has none.</summary>
public sealed record RouteMapStop(string System, StarPosition? Position, RouteStopMarks Marks)
{
    /// <summary>The stop reached; off the route, the Commander only inside the route's extent; otherwise null.</summary>
    public static StarPosition? CommanderMark(IReadOnlyList<RouteMapStop> stops, int reached, StarPosition? commander)
    {
        var positioned = stops.Where(stop => stop.Position is not null).Select(stop => stop.Position!.Value).ToList();

        if (positioned.Count < 2 || reached < -1 || reached >= stops.Count)
        {
            return null;
        }

        if (reached >= 0)
        {
            return stops[reached].Position;
        }

        if (commander is not { } from)
        {
            return null;
        }

        var minX = positioned.Min(at => at.X);
        var maxX = positioned.Max(at => at.X);
        var minZ = positioned.Min(at => at.Z);
        var maxZ = positioned.Max(at => at.Z);

        return Math.Abs(from.X - (minX + maxX) / 2) <= Math.Max(maxX - minX, 1) / 2 &&
            Math.Abs(from.Z - (minZ + maxZ) / 2) <= Math.Max(maxZ - minZ, 1) / 2
            ? from
            : null;
    }

    public static IReadOnlyList<RouteMapStop> From(NavRoute route) =>
        [.. route.Hops.Select(hop => new RouteMapStop(
            hop.StarSystem,
            hop.Position is { } at ? new StarPosition(at.X, at.Y, at.Z) : null,
            NavMarks(hop.StarClass)))];

    public static IReadOnlyList<RouteMapStop> From(CarrierRoute route) =>
        [.. route.Waypoints.Select(waypoint => new RouteMapStop(
            waypoint.Name,
            waypoint.Position,
            waypoint.MustRestock ? RouteStopMarks.Restock : RouteStopMarks.None))];

    public static IReadOnlyList<RouteMapStop> From(PlottedRoute route) =>
        Around(
            route.Start,
            route.Waypoints.Select(waypoint => new RouteMapStop(
                waypoint.System,
                waypoint.Position,
                waypoint.IsNeutron ? RouteStopMarks.Neutron : RouteStopMarks.None)),
            null);

    public static IReadOnlyList<RouteMapStop> From(RichesRoute route) =>
        Around(
            route.Start,
            route.Stops.Select(stop => new RouteMapStop(stop.System, stop.Position, RouteStopMarks.None)),
            route.End);

    public static IReadOnlyList<RouteMapStop> From(ExobiologyRoute route) =>
        Around(
            route.Start,
            route.Stops.Select(stop => new RouteMapStop(stop.System, stop.Position, RouteStopMarks.None)),
            route.End);

    /// <summary>The plan's stops to draw; null for a trade plan or a plan with no route recorded.</summary>
    public static IReadOnlyList<RouteMapStop>? From(StoredRoutePlan plan) => plan switch
    {
        { Jump: { } jump } => From(jump),
        { Riches: { } riches } => From(riches),
        { Exobiology: { } exobiology } => From(exobiology),
        { Carrier: { } carrier } => From(carrier),
        _ => null,
    };

    /// <summary>The index in the map's stop list of the stop <c>plan.Reached</c> names, or -1.</summary>
    public static int ReachedIndex(StoredRoutePlan plan)
    {
        if (plan.Reached is not { } reached)
        {
            return -1;
        }

        var start = plan switch
        {
            { Jump: { } jump } => jump.Start,
            { Riches: { } riches } => riches.Start,
            { Exobiology: { } exobiology } => exobiology.Start,
            _ => null,
        };

        return start is null ? reached : reached + 1;
    }

    private static IReadOnlyList<RouteMapStop> Around(
        RouteEndpoint? start,
        IEnumerable<RouteMapStop> stops,
        RouteEndpoint? end)
    {
        List<RouteMapStop> all = [];

        if (start is not null)
        {
            all.Add(new RouteMapStop(start.System, start.Position, RouteStopMarks.None));
        }

        all.AddRange(stops);

        if (end is not null)
        {
            all.Add(new RouteMapStop(end.System, end.Position, RouteStopMarks.None));
        }

        return all;
    }

    private static RouteStopMarks NavMarks(string? starClass)
    {
        var marks = RouteStopMarks.None;

        if (StarClasses.IsNeutron(starClass))
        {
            marks |= RouteStopMarks.Neutron;
        }

        if (StarClasses.IsWhiteDwarf(starClass))
        {
            marks |= RouteStopMarks.WhiteDwarf;
        }

        if (StarClasses.IsBlackHole(starClass))
        {
            marks |= RouteStopMarks.BlackHole;
        }

        if (StarClasses.IsScoopable(starClass) == false)
        {
            marks |= RouteStopMarks.NotScoopable;
        }

        return marks;
    }
}
