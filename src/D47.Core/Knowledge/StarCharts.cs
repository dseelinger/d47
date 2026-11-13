using D47.Core.Journal;
using D47.Core.Persona;

namespace D47.Core.Knowledge;

/// <summary>A set of things the Galaxy map can draw, switched on and off as one.</summary>
public enum StarChartKind
{
    Travels,
    Route,
    Regions,
    Engineers,
    Bookmarks,
    Carrier,
    Ships,
    Unvisited,
    GuardianBeacons,
    FarmingSites,
    MissionDestinations,
    SystemsSearch,
    StationsSearch,
    BodiesSearch,
    SignalsSearch,
    OutfittingSearch,
    RaresSearch,
}

/// <summary>How a chart is drawn on the map.</summary>
public enum StarChartShape
{
    Points,
    Line,
    Area,
}

/// <summary>A chart's switch title, the word for one of its marks (null when it has none), its shape and the group the Layers picker lists it under.</summary>
public sealed record StarChartInfo(string Title, string? Mark, StarChartShape Shape, string Group);

/// <summary>One system a fixed chart draws.</summary>
public sealed record StarChartSystem(string Name, long? Address, StarPosition At);

public static class StarCharts
{
    public const string Yours = "Yours";
    public const string Galaxy = "Galaxy";
    public const string Guardian = "Guardian";
    public const string LastSearch = "Last search";

    /// <summary>Every kind, in the order the switches are listed.</summary>
    public static IReadOnlyList<StarChartKind> All { get; } = Enum.GetValues<StarChartKind>();

    /// <summary>The kinds on a map with nothing saved: every kind but the fixed charts, the mission destinations and the search answers, which a request adds.</summary>
    public static IReadOnlyList<StarChartKind> Default { get; } =
        [.. All.Where(kind => !IsFixed(kind) && kind != StarChartKind.MissionDestinations && SearchOf(kind) is null)];

    /// <summary>Whether <paramref name="kind"/> draws a fixed list of systems that d47 carries.</summary>
    public static bool IsFixed(StarChartKind kind) => kind is StarChartKind.GuardianBeacons or StarChartKind.FarmingSites;

    /// <summary>The galaxy search whose last answer <paramref name="kind"/> draws, or null for a kind that draws no search.</summary>
    public static GalaxySearchKind? SearchOf(StarChartKind kind) => kind switch
    {
        StarChartKind.SystemsSearch => GalaxySearchKind.Systems,
        StarChartKind.StationsSearch => GalaxySearchKind.Stations,
        StarChartKind.BodiesSearch => GalaxySearchKind.Bodies,
        StarChartKind.SignalsSearch => GalaxySearchKind.Signals,
        StarChartKind.OutfittingSearch => GalaxySearchKind.Outfitting,
        StarChartKind.RaresSearch => GalaxySearchKind.Rares,
        _ => null,
    };

    /// <summary>The systems a fixed kind draws, by name; empty for any other kind.</summary>
    public static IReadOnlyList<StarChartSystem> Systems(StarChartKind kind) => kind switch
    {
        StarChartKind.GuardianBeacons => BeaconSystems,
        StarChartKind.FarmingSites => FarmingSystems,
        _ => [],
    };

    private static readonly IReadOnlyList<StarChartSystem> BeaconSystems =
    [
        .. GuardianCores.Beacons
            .Select(pair => new StarChartSystem(pair.Value, pair.Key, GuardianCores.BeaconPositions[pair.Key]))
            .OrderBy(system => system.Name, StringComparer.Ordinal),
    ];

    private static readonly IReadOnlyList<StarChartSystem> FarmingSystems =
    [
        .. FarmingSites.All
            .GroupBy(site => site.System, StringComparer.OrdinalIgnoreCase)
            .Select(group => new StarChartSystem(group.Key, null, group.First().Position))
            .OrderBy(system => system.Name, StringComparer.Ordinal),
    ];

    public static StarChartInfo Info(StarChartKind kind) => kind switch
    {
        StarChartKind.Travels => new("Travels", null, StarChartShape.Points, Yours),
        StarChartKind.Route => new("Route", null, StarChartShape.Line, Yours),
        StarChartKind.Regions => new("Regions", null, StarChartShape.Area, Galaxy),
        StarChartKind.Engineers => new("Engineers", "Engineer", StarChartShape.Points, Galaxy),
        StarChartKind.Bookmarks => new("Bookmarks", "Bookmark", StarChartShape.Points, Yours),
        StarChartKind.Carrier => new("Carrier", "Fleet carrier", StarChartShape.Points, Yours),
        StarChartKind.Ships => new("Ships", "Ship", StarChartShape.Points, Yours),
        StarChartKind.Unvisited => new("Unvisited", null, StarChartShape.Points, Galaxy),
        StarChartKind.GuardianBeacons => new("Guardian beacons", "Guardian beacon", StarChartShape.Points, Guardian),
        StarChartKind.FarmingSites => new("Farming sites", "Farming site", StarChartShape.Points, Galaxy),
        StarChartKind.MissionDestinations => new("Missions", "Mission destination", StarChartShape.Points, Yours),
        StarChartKind.SystemsSearch => new("Systems search", "System result", StarChartShape.Points, LastSearch),
        StarChartKind.StationsSearch => new("Stations search", "Station result", StarChartShape.Points, LastSearch),
        StarChartKind.BodiesSearch => new("Bodies search", "Body result", StarChartShape.Points, LastSearch),
        StarChartKind.SignalsSearch => new("Signals search", "Signal result", StarChartShape.Points, LastSearch),
        StarChartKind.OutfittingSearch => new("Outfitting search", "Outfitting result", StarChartShape.Points, LastSearch),
        StarChartKind.RaresSearch => new("Rares search", "Rares result", StarChartShape.Points, LastSearch),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
