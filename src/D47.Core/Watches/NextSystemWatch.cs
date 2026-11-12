using System.Globalization;
using D47.Core.Knowledge;

namespace D47.Core.Watches;

/// <summary>The names and lines of a next-system watch, shared by the runner, the page and the voice.</summary>
public static class NextSystemWatch
{
    public const string MaterialTrader = "Material Trader";

    public const string TechnologyBroker = "Technology Broker";

    /// <summary>The services a next-system watch is offered for, in the order the form lists them.</summary>
    public static IReadOnlyList<string> Services { get; } =
    [
        "Black Market",
        "Interstellar Factors",
        MaterialTrader,
        "Outfitting",
        "Refuel",
        "Repair",
        "Shipyard",
        TechnologyBroker,
        "Universal Cartographics",
        "Vista Genomics",
    ];

    /// <summary>The <see cref="StationQuery.ServiceNames"/> entry for <paramref name="service"/>, ignoring case; null when there is none.</summary>
    public static StationServiceName? ServiceFor(string? service) =>
        string.IsNullOrWhiteSpace(service)
            ? null
            : StationQuery.ServiceNames.FirstOrDefault(known => Same(known.Name, service.Trim()));

    /// <summary>The kinds a service can be narrowed to: trader types, broker types, or none.</summary>
    public static IReadOnlyList<string> KindsOf(string? service) =>
        ServiceFor(service)?.Name switch
        {
            MaterialTrader => StationQuery.TraderTypes,
            TechnologyBroker => StationQuery.BrokerTypes,
            _ => [],
        };

    /// <summary>The kind as <see cref="KindsOf"/> spells it, ignoring case; null when it is not one of them.</summary>
    public static string? KindFor(string? service, string? kind) =>
        string.IsNullOrWhiteSpace(kind) ? null : KindsOf(service).FirstOrDefault(known => Same(known, kind.Trim()));

    /// <summary>"Manufactured material trader", "Technology broker" or "Interstellar Factors"; also the default name.</summary>
    public static string ServiceName(string service, string? kind)
    {
        ArgumentNullException.ThrowIfNull(service);

        var name = ServiceFor(service)?.Name ?? service.Trim();

        if (name is not (MaterialTrader or TechnologyBroker))
        {
            return name;
        }

        var lower = name.ToLowerInvariant();

        return string.IsNullOrWhiteSpace(kind) ? char.ToUpperInvariant(lower[0]) + lower[1..] : $"{kind.Trim()} {lower}";
    }

    /// <summary>The service with "a" or "an" before it; a name ending in s, such as Interstellar Factors, takes none.</summary>
    public static string Said(string service, string? kind)
    {
        var name = ServiceName(service, kind);

        if (name.Length == 0)
        {
            return "a service";
        }

        if (name.EndsWith('s'))
        {
            return name;
        }

        return ("AEIOUaeiou".Contains(name[0], StringComparison.Ordinal) ? "an " : "a ") + name;
    }

    /// <summary>"any pad", "a medium or large pad" or "a large pad".</summary>
    public static string Pad(PadSize? minPad) => minPad switch
    {
        PadSize.Large => "a large pad",
        PadSize.Medium => "a medium or large pad",
        _ => "any pad",
    };

    /// <summary>"a station with a Manufactured material trader, any pad, in the next system".</summary>
    public static string Condition(Watch watch)
    {
        ArgumentNullException.ThrowIfNull(watch);

        return $"a station with {Said(watch.Service ?? string.Empty, watch.ServiceKind)}, {Pad(watch.MinPad)}, in the next system";
    }

    /// <summary>
    /// "Kremainn has a Manufactured material trader, Commander. Wohler Terminal, 240 light seconds, large pad. That watch is off now."
    /// A distance or pad the station does not report is left out.
    /// </summary>
    public static string Line(string system, Watch watch, StationSummary station)
    {
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(station);

        return $"{system} has {Said(watch.Service ?? string.Empty, watch.ServiceKind)}, Commander. {Place(station)}. That watch is off now.";
    }

    /// <summary>
    /// "Your route passes a Manufactured material trader, Commander: Kremainn, jump 6 of 14. Wohler Terminal, 240 light seconds, large pad."
    /// </summary>
    public static string RouteLine(string system, int jump, int jumps, Watch watch, StationSummary station)
    {
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(station);

        return $"Your route passes {Said(watch.Service ?? string.Empty, watch.ServiceKind)}, Commander: {system}, "
               + $"jump {jump.ToString(CultureInfo.InvariantCulture)} of {jumps.ToString(CultureInfo.InvariantCulture)}. {Place(station)}.";
    }

    /// <summary>"Wohler Terminal, 240 light seconds, large pad"; a distance or pad the station does not report is left out.</summary>
    public static string Place(StationSummary station)
    {
        ArgumentNullException.ThrowIfNull(station);

        var parts = new List<string> { station.Name };

        if (station.DistanceToArrival is { } distance)
        {
            parts.Add($"{Math.Round(distance).ToString("N0", CultureInfo.InvariantCulture)} light seconds");
        }

        if (LargestPad(station) is { } pad)
        {
            parts.Add($"{pad.ToString().ToLowerInvariant()} pad");
        }

        return string.Join(", ", parts);
    }

    private static PadSize? LargestPad(StationSummary station) =>
        station.KnownPads.Count > 0 ? station.KnownPads[^1]
        : station.HasLargePad ? PadSize.Large
        : null;

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
