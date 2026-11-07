using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>What the mini panel's galaxy map shows: the route ahead from the Commander's position, or the words shown in its place.</summary>
internal sealed record RouteAhead(GalaxyMapScene? Scene, string? Note)
{
    public const string NoRoute = "No route is plotted.";

    public const string Reached = "The destination is reached.";

    /// <summary>The stops of <paramref name="plan"/> when it has two with positions, else the plotted route, from the stop reached on.</summary>
    public static RouteAhead Of(NavRoute route, string? here, StarPosition? position, StoredRoutePlan? plan)
    {
        var planStops = plan is null ? null : RouteMapStop.From(plan);
        var planned = planStops is not null && planStops.Count(stop => stop.Position is not null) >= 2;
        var stops = planned ? planStops! : route.IsPlotted ? RouteMapStop.From(route) : [];
        var reached = planned ? RouteMapStop.ReachedIndex(plan!) : RouteProgress.For(route, here).Index;

        if (stops.Count == 0)
        {
            return new RouteAhead(null, route.LiveTarget is { } target ? target.NextJump : NoRoute);
        }

        if (reached >= stops.Count - 1)
        {
            return new RouteAhead(null, Reached);
        }

        var ahead = stops.Skip(reached + 1).ToList();
        var origin = reached >= 0 && stops[reached].Position is { } onRoute ? onRoute : position;

        IReadOnlyList<RouteMapStop> drawn = origin is { } from
            ? [new RouteMapStop(here ?? string.Empty, from, RouteStopMarks.None), .. ahead]
            : ahead;

        return new RouteAhead(
            new GalaxyMapScene(
                Regions: false,
                Travels: null,
                Route: drawn,
                Reached: origin is null ? -1 : 0,
                Commander: origin,
                Here: here),
            null);
    }
}
