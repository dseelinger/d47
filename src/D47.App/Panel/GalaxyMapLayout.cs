using Avalonia;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Where a <see cref="GalaxyMapView"/> draws and selects each part of a scene, at one viewport and size.</summary>
internal sealed class GalaxyMapLayout
{
    /// <summary>The side of an unvisited system's outline.</summary>
    public const double UnvisitedSide = 5;

    private const double NearestSameMark = 8;
    private const double MarkSpacing = 14;
    private const double RowLift = 22;
    private const double SameSystem = 3;

    private readonly Size _size;

    /// <summary>One marker as drawn: its kind, its centre, how many names it holds and the system it selects.</summary>
    public readonly record struct Mark(GalaxyMapMarkKind Kind, Point At, int Count, GalaxyMapStar Star);

    private GalaxyMapLayout(GalaxyViewport viewport, Size size, bool detailed)
    {
        Viewport = viewport;
        _size = size;
        Detailed = detailed;
    }

    public static GalaxyMapLayout Empty { get; } = new(GalaxyViewport.WholeGalaxy, default, false);

    /// <summary>The viewport drawn: the one given, or the route fitted to the size when a fit inset is given.</summary>
    public GalaxyViewport Viewport { get; }

    /// <summary>True while route stops, system names, engineers and unvisited systems are drawn.</summary>
    public bool Detailed { get; }

    public IReadOnlyList<(GalaxyMapStar Star, Point At)> Unvisited { get; private set; } = [];

    /// <summary>Each route stop with a position, in order; empty for a route of fewer than two.</summary>
    public IReadOnlyList<Point> Route { get; private set; } = [];

    /// <summary>The index in <see cref="Route"/> of the stop the Commander is on, or -1.</summary>
    public int HereIndex { get; private set; } = -1;

    public IReadOnlyList<(RouteStar Kind, Point At)> Stars { get; private set; } = [];

    public Point? You { get; private set; }

    public string? Destination { get; private set; }

    public IReadOnlyList<Mark> Marks { get; private set; } = [];

    /// <summary>
    /// The systems a tap can select: unvisited ones drawn, visited ones while travels are drawn, route stops while their
    /// marks are, and every mark drawn. Unvisited ones come first, so a tie goes to what is drawn over them.
    /// </summary>
    public IReadOnlyList<(GalaxyMapStar Star, Point At)> Targets { get; private set; } = [];

    public GalaxyMapDrawn Drawn { get; private set; } = GalaxyMapDrawn.Nothing;

    /// <summary>Lays out <paramref name="scene"/>; a <paramref name="fitInset"/> fits the route with that many px clear each side, each axis separately when <paramref name="fitEachAxis"/>.</summary>
    public static GalaxyMapLayout Of(GalaxyMapScene scene, GalaxyViewport viewport, Size size, double? fitInset = null, bool fitEachAxis = false)
    {
        var stops = scene.Route?.Where(stop => stop.Position is not null).ToList() ?? [];

        if (fitInset is { } inset)
        {
            var points = stops.Select(stop => stop.Position!.Value).ToList();

            if ((fitEachAxis
                    ? GalaxyViewport.FittedIn(points, size.Width, size.Height, inset)
                    : GalaxyViewport.Fitted(points, Math.Min(size.Width, size.Height), inset)) is { } fitted)
            {
                viewport = fitted;
            }
        }

        var layout = new GalaxyMapLayout(viewport, size, fitInset is not null || viewport.LightYearsAcross <= GalaxyMapView.DetailAcross);
        layout.Build(scene, stops);

        return layout;
    }

    public Point Screen(StarPosition at)
    {
        var (x, y) = Viewport.ToScreen(at, _size.Width, _size.Height);

        return new Point(x, y);
    }

    public bool InView(Point at, double slack) =>
        at.X >= -slack && at.Y >= -slack && at.X <= _size.Width + slack && at.Y <= _size.Height + slack;

    private void Build(GalaxyMapScene scene, List<RouteMapStop> stops)
    {
        Unvisited = Detailed && scene.Unvisited is { } unvisited
            ? [.. unvisited.Select(star => (star, Screen(star.At))).Where(star => InView(star.Item2, UnvisitedSide))]
            : [];

        Route = stops.Count >= 2 ? [.. stops.Select(stop => Screen(stop.Position!.Value))] : [];
        HereIndex = scene.Route is { } all && scene.Reached >= 0 && scene.Reached < all.Count && all[scene.Reached].Position is not null
            ? all.Take(scene.Reached).Count(stop => stop.Position is not null)
            : -1;

        var stars = new List<(RouteStar Kind, Point At)>();

        if (Route.Count > 0 && Detailed)
        {
            for (var index = 0; index < stops.Count; index++)
            {
                if (RouteMarks.StarOf(stops[index].Marks) is not { } kind
                    || !InView(Route[index], 8)
                    || stars.Any(mark => mark.Kind == kind && Distance(mark.At, Route[index]) <= NearestSameMark))
                {
                    continue;
                }

                stars.Add((kind, Route[index]));
            }
        }

        Stars = stars;
        You = scene.Commander is { } commander ? Screen(commander) : null;
        Destination = stops.Count >= 2 ? stops[^1].System : null;
        Marks = MarksOf(scene);
        Targets = TargetsOf(scene, stops);

        var wide = Viewport.LightYearsAcross >= GalaxyMapView.DetailAcross;

        Drawn = new GalaxyMapDrawn(
            Travels: scene.Travels is { Lines.Count: > 0 },
            Route: Route.Count > 0,
            You: You is { } you && InView(you, 0),
            Start: Route.Count > 0 && Detailed && InView(Route[0], 0),
            Destination: Route.Count > 0 && Detailed && InView(Route[^1], 0),
            StopsHidden: Route.Count > 0 && !Detailed,
            RegionNames: scene.Regions && wide,
            Stars: [.. stars.Select(star => star.Kind).Distinct()],
            EngineersHidden: !Detailed && scene.Marks is { } marked && marked.Any(mark => mark.Kind == GalaxyMapMarkKind.Engineer),
            Unvisited: Unvisited.Count > 0);
    }

    /// <summary>
    /// Each system's markers as one row centred on it, a kind per mark in <see cref="GalaxyMapMarkKind"/> order; the row
    /// sits above the system where You, a route mark or the destination is drawn there.
    /// </summary>
    private List<Mark> MarksOf(GalaxyMapScene scene)
    {
        var drawn = new List<Mark>();

        if (scene.Marks is not { Count: > 0 } placed)
        {
            return drawn;
        }

        var taken = new List<Point>();

        if (You is { } you)
        {
            taken.Add(you);
        }

        if (Route.Count > 0 && Detailed)
        {
            taken.Add(Route[0]);
            taken.Add(Route[^1]);
            taken.AddRange(Stars.Select(star => star.At));
        }

        var systems = placed
            .Where(mark => mark.Kind != GalaxyMapMarkKind.Engineer || Detailed)
            .GroupBy(mark => mark.System, StringComparer.OrdinalIgnoreCase);

        foreach (var system in systems)
        {
            var first = system.First();
            var centre = Screen(first.At);

            if (!InView(centre, RowLift + MarkSpacing))
            {
                continue;
            }

            var star = new GalaxyMapStar(first.System, first.At);
            var kinds = system.OrderBy(mark => mark.Kind).DistinctBy(mark => mark.Kind).ToList();
            var lift = taken.Any(at => Distance(at, centre) <= SameSystem) ? -RowLift : 0;

            for (var index = 0; index < kinds.Count; index++)
            {
                var across = (index - ((kinds.Count - 1) / 2.0)) * MarkSpacing;

                drawn.Add(new Mark(kinds[index].Kind, new Point(centre.X + across, centre.Y + lift), kinds[index].Names.Count, star));
            }
        }

        return drawn;
    }

    private List<(GalaxyMapStar Star, Point At)> TargetsOf(GalaxyMapScene scene, List<RouteMapStop> stops)
    {
        var targets = new List<(GalaxyMapStar, Point)>(Unvisited);

        if (scene.Travels is not null && scene.Visited is { } visited)
        {
            targets.AddRange(visited.Select(star => (star, Screen(star.At))));
        }

        if (Route.Count > 0 && Detailed)
        {
            targets.AddRange(stops.Select((stop, index) => (new GalaxyMapStar(stop.System, stop.Position!.Value), Route[index])));
        }

        targets.AddRange(Marks.Select(mark => (mark.Star, mark.At)));

        return targets;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
