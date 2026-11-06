using System.Globalization;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>One option of the Assets kind filter.</summary>
public sealed record FactionKind(string Id, string Label, int Count);

/// <summary>The stations a faction controls, as the Assets section lists, filters and sorts them.</summary>
public static class FactionAssets
{
    public const string AllId = "all";
    public const string NoneId = "none";
    public const string AllLabel = "All kinds";
    public const string NoneLabel = "No type given";
    public const string NoMatchText = "No station of this faction matches.";

    private static readonly (string Match, string Row, string Option)[] Kinds =
    [
        ("Outpost", "Outpost", "Outposts"),
        ("Settlement", "Settlement", "Settlements"),
        ("Planetary Outpost", "Planetary outpost", "Planetary outposts"),
        ("Coriolis Starport", "Coriolis", "Coriolis"),
        ("Planetary Construction Depot", "Construction depot", "Construction depots"),
        ("Space Construction Depot", "Construction depot", "Construction depots"),
        ("Asteroid base", "Asteroid base", "Asteroid bases"),
        ("Orbis Starport", "Orbis", "Orbis"),
        ("Dodec Starport", "Dodec", "Dodec"),
        ("Ocellus Starport", "Ocellus", "Ocellus"),
        ("Planetary Port", "Planetary port", "Planetary ports"),
    ];

    /// <summary>The stations in the default order: system, then station name.</summary>
    public static IReadOnlyList<FactionStation> Of(FactionReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return
        [
            .. report.Stations
                .OrderBy(station => station.System, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(station => station.Name, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    /// <summary>The station type as a row shows it, or null when the service gave none.</summary>
    public static string? TypeLabel(FactionStation station) => Describe(station.Type)?.Row;

    /// <summary>The filter id of the station's kind.</summary>
    public static string KindId(FactionStation station) => Describe(station.Type)?.Option ?? NoneId;

    private static (string Row, string Option)? Describe(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        var trimmed = type.Trim();

        foreach (var (match, row, option) in Kinds)
        {
            if (string.Equals(match, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return (row, option);
            }
        }

        return (trimmed, trimmed);
    }

    /// <summary>All kinds first, then each kind present, largest count first, and No type given last.</summary>
    public static IReadOnlyList<FactionKind> Options(IReadOnlyList<FactionStation> stations)
    {
        var counts = stations.GroupBy(KindId).Select(group => new FactionKind(group.Key, group.Key == NoneId ? NoneLabel : group.Key, group.Count()));

        return
        [
            new FactionKind(AllId, AllLabel, stations.Count),
            .. counts.Where(kind => kind.Id != NoneId).OrderByDescending(kind => kind.Count).ThenBy(kind => kind.Label, StringComparer.CurrentCultureIgnoreCase),
            .. counts.Where(kind => kind.Id == NoneId),
        ];
    }

    /// <summary>The stations of one kind whose name or system contains <paramref name="query"/>, ignoring case.</summary>
    public static IReadOnlyList<FactionStation> Matching(IReadOnlyList<FactionStation> stations, string? query, string kindId)
    {
        var text = query?.Trim();

        return
        [
            .. stations.Where(station =>
                (kindId == AllId || KindId(station) == kindId)
                && (string.IsNullOrEmpty(text)
                    || station.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || station.System.Contains(text, StringComparison.OrdinalIgnoreCase))),
        ];
    }

    /// <summary>The Assets columns, each sortable from its heading.</summary>
    public static IReadOnlyList<SortColumn<FactionStation>> Heads { get; } =
    [
        SortColumn<FactionStation>.Text("Station", station => station.Name),
        SortColumn<FactionStation>.Text("System", station => station.System, station => station.Name),
        SortColumn<FactionStation>.Text("Type", TypeLabel),
        SortColumn<FactionStation>.Figure<int>("Pad", station => station.LargestPad is { } pad ? (int)pad : null, SortDirection.Descending) with { Second = "Largest" },
        SortColumn<FactionStation>.Figure("Distance", station => station.DistanceToArrival, SortDirection.Ascending) with { Second = "From arrival" },
    ];

    /// <summary>The order the rows come in, drawn as System ▲.</summary>
    public static readonly SortOrder DefaultOrder = new(1, SortDirection.Ascending);

    /// <summary>"650 STATIONS", or "177 OF 650 STATIONS" while filtered.</summary>
    public static string CountText(int shown, int all) =>
        (shown == all ? $"{all} stations" : $"{shown} of {all} stations").ToUpperInvariant();

    /// <summary>L, M or S, or null when the service lists no pads.</summary>
    public static string? PadLetter(PadSize? pad) => pad switch
    {
        PadSize.Large => "L",
        PadSize.Medium => "M",
        PadSize.Small => "S",
        _ => null,
    };

    /// <summary>"1,240 ls".</summary>
    public static string Distance(double lightSeconds) =>
        Math.Round(lightSeconds, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture) + " ls";
}
