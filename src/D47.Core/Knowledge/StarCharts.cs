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

/// <summary>A chart's switch title, the word for one of its marks (null when it has none) and its shape.</summary>
public sealed record StarChartInfo(string Title, string? Mark, StarChartShape Shape);

public static class StarCharts
{
    /// <summary>Every kind, in the order the switches are listed.</summary>
    public static IReadOnlyList<StarChartKind> All { get; } = Enum.GetValues<StarChartKind>();

    public static StarChartInfo Info(StarChartKind kind) => kind switch
    {
        StarChartKind.Travels => new("Travels", null, StarChartShape.Points),
        StarChartKind.Route => new("Route", null, StarChartShape.Line),
        StarChartKind.Regions => new("Regions", null, StarChartShape.Area),
        StarChartKind.Engineers => new("Engineers", "Engineer", StarChartShape.Points),
        StarChartKind.Bookmarks => new("Bookmarks", "Bookmark", StarChartShape.Points),
        StarChartKind.Carrier => new("Carrier", "Fleet carrier", StarChartShape.Points),
        StarChartKind.Ships => new("Ships", "Ship", StarChartShape.Points),
        StarChartKind.Unvisited => new("Unvisited", null, StarChartShape.Points),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
