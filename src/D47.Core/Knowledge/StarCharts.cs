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

public static class StarCharts
{
    public const string Yours = "Yours";
    public const string Galaxy = "Galaxy";

    /// <summary>Every kind, in the order the switches are listed.</summary>
    public static IReadOnlyList<StarChartKind> All { get; } = Enum.GetValues<StarChartKind>();

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
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
