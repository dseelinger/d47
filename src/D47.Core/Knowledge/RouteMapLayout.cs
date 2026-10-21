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

public enum RouteLeg
{
    Behind,
    Here,
    Ahead,
}

/// <summary>One stop of a route to draw; <see cref="Position"/> is null where the source has none.</summary>
public sealed record RouteMapStop(string System, StarPosition? Position, RouteStopMarks Marks)
{
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

/// <summary>A stop placed in a 0..1 square; Y up is galactic +Z.</summary>
public sealed record RouteMapPoint(string System, double X, double Y, RouteStopMarks Marks, RouteLeg Leg);

/// <summary>A route projected top-down into a 0..1 square, with no drawing in it.</summary>
public sealed record RouteMapLayout(IReadOnlyList<RouteMapPoint> Points, (double X, double Y)? Commander)
{
    /// <summary>The smallest extent on either axis, in light years.</summary>
    private const double MinimumExtent = 1;

    /// <summary>
    /// Projects the positioned stops with one scale on both axes. <paramref name="reached"/> is the index in
    /// <paramref name="stops"/> of the stop the Commander is on, or -1. Null when fewer than two stops have a position.
    /// </summary>
    public static RouteMapLayout? For(IReadOnlyList<RouteMapStop> stops, int reached, StarPosition? commander)
    {
        var positioned = stops.Where(stop => stop.Position is not null).ToList();

        if (positioned.Count < 2)
        {
            return null;
        }

        var minX = positioned.Min(stop => stop.Position!.Value.X);
        var maxX = positioned.Max(stop => stop.Position!.Value.X);
        var minZ = positioned.Min(stop => stop.Position!.Value.Z);
        var maxZ = positioned.Max(stop => stop.Position!.Value.Z);

        var halfX = Math.Max(maxX - minX, MinimumExtent) / 2;
        var halfZ = Math.Max(maxZ - minZ, MinimumExtent) / 2;
        var centreX = (minX + maxX) / 2;
        var centreZ = (minZ + maxZ) / 2;
        var half = Math.Max(halfX, halfZ);

        (double X, double Y) Project(StarPosition at) =>
            (0.5 + ((at.X - centreX) / (2 * half)), 0.5 + ((at.Z - centreZ) / (2 * half)));

        var points = new List<RouteMapPoint>(positioned.Count);

        for (var index = 0; index < stops.Count; index++)
        {
            if (stops[index].Position is not { } at)
            {
                continue;
            }

            var (x, y) = Project(at);
            var leg = index < reached ? RouteLeg.Behind : index == reached ? RouteLeg.Here : RouteLeg.Ahead;
            points.Add(new RouteMapPoint(stops[index].System, x, y, stops[index].Marks, leg));
        }

        (double X, double Y)? commanderAt = null;

        if (reached == -1 && commander is { } from &&
            Math.Abs(from.X - centreX) <= halfX && Math.Abs(from.Z - centreZ) <= halfZ)
        {
            commanderAt = Project(from);
        }

        return new RouteMapLayout(points, commanderAt);
    }
}
