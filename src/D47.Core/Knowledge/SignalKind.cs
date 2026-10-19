namespace D47.Core.Knowledge;

/// <summary>A kind of FSS signal d47 searches for, with the rule that classifies a journal signal as it.</summary>
public sealed record SignalKind(string Name)
{
    public static readonly SignalKind NavBeacon = new("Nav beacon");

    public static readonly SignalKind CompromisedNavBeacon = new("Compromised nav beacon");

    public static readonly SignalKind ResourceExtractionSite = new("Resource extraction site");

    public static readonly SignalKind LowRes = new("Low RES");

    public static readonly SignalKind HighRes = new("High RES");

    public static readonly SignalKind HazardousRes = new("Hazardous RES");

    public static readonly SignalKind ColonisationBeacon = new("Colonisation beacon");

    public static readonly SignalKind TouristBeacon = new("Tourist beacon");

    public static readonly SignalKind Installation = new("Installation");

    public static readonly SignalKind Megaship = new("Megaship");

    public static readonly SignalKind Titan = new("Titan");

    public static readonly SignalKind ListeningPost = new("Listening post");

    public static readonly SignalKind UnregisteredCommsBeacon = new("Unregistered comms beacon");

    public static readonly SignalKind NotableStellarPhenomena = new("Notable stellar phenomena");

    public static IReadOnlyList<SignalKind> All { get; } =
    [
        NavBeacon, CompromisedNavBeacon, ResourceExtractionSite, LowRes, HighRes, HazardousRes,
        ColonisationBeacon, TouristBeacon, Installation, Megaship, Titan, ListeningPost,
        UnregisteredCommsBeacon, NotableStellarPhenomena,
    ];

    /// <summary>What "any RES" matches.</summary>
    public static IReadOnlyList<SignalKind> AnyRes { get; } =
        [ResourceExtractionSite, LowRes, HighRes, HazardousRes];

    private static readonly string[] DroppedTypes = ["fleetcarrier", "squadroncarrier", "outpost", "combat", "uss"];

    private static readonly string[] DroppedNamePrefixes = ["$operations_", "test signal"];

    /// <summary>Whether a signal is one the index leaves out: stations, carriers, combat and the like.</summary>
    public static bool Drops(string? type, string? name)
    {
        var t = type ?? string.Empty;
        var n = name ?? string.Empty;

        if (DroppedTypes.Contains(t, StringComparer.OrdinalIgnoreCase)
            || t.StartsWith("station", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (DroppedNamePrefixes.Any(prefix => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return t.Equals("generic", StringComparison.OrdinalIgnoreCase)
            ? n.StartsWith("$fixed_event_", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("$multiplayer_scenario81_title", StringComparison.OrdinalIgnoreCase)
            : t.Equals("installation", StringComparison.OrdinalIgnoreCase)
                && n.StartsWith("planetary construction site:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The kind a signal belongs to, or null where it has none.</summary>
    public static SignalKind? Classify(string? type, string? name)
    {
        var t = type ?? string.Empty;
        var n = name ?? string.Empty;

        if (n.Equals("$MULTIPLAYER_SCENARIO42_TITLE;", StringComparison.OrdinalIgnoreCase)
            || (t.Equals("NavBeacon", StringComparison.OrdinalIgnoreCase)
                && n.Equals("Nav Beacon", StringComparison.OrdinalIgnoreCase)))
        {
            return NavBeacon;
        }

        var byName = n.ToUpperInvariant() switch
        {
            "$MULTIPLAYER_SCENARIO80_TITLE;" => CompromisedNavBeacon,
            "$MULTIPLAYER_SCENARIO14_TITLE;" => ResourceExtractionSite,
            "$MULTIPLAYER_SCENARIO77_TITLE;" => LowRes,
            "$MULTIPLAYER_SCENARIO78_TITLE;" => HighRes,
            "$MULTIPLAYER_SCENARIO79_TITLE;" => HazardousRes,
            _ => null,
        };

        if (byName is not null)
        {
            return byName;
        }

        if (n.StartsWith("$EXT_PANEL_ColonisationBeacon_", StringComparison.OrdinalIgnoreCase))
        {
            return ColonisationBeacon;
        }

        if (n.StartsWith("$ListeningPost", StringComparison.OrdinalIgnoreCase))
        {
            return ListeningPost;
        }

        if (n.StartsWith("$NumberStation", StringComparison.OrdinalIgnoreCase))
        {
            return UnregisteredCommsBeacon;
        }

        if (t.Equals("Codex", StringComparison.OrdinalIgnoreCase)
            && n.Equals("$Fixed_Event_Life_Cloud;", StringComparison.OrdinalIgnoreCase))
        {
            return NotableStellarPhenomena;
        }

        return t.ToUpperInvariant() switch
        {
            "TOURISTBEACON" => TouristBeacon,
            "INSTALLATION" => Installation,
            "MEGASHIP" => Megaship,
            "TITAN" => Titan,
            _ => null,
        };
    }

    /// <summary>
    /// The kind and the name to show for a journal signal, or null where it is dropped. A <c>$</c> code is
    /// shown as its kind's name; one with no kind is dropped rather than shown raw.
    /// </summary>
    public static (SignalKind? Kind, string Name)? Describe(string? type, string? name, string? localised)
    {
        if (string.IsNullOrWhiteSpace(name) || Drops(type, name))
        {
            return null;
        }

        var kind = Classify(type, name);

        if (name.StartsWith('$'))
        {
            return kind is null ? null : (kind, kind.Name);
        }

        return (kind, string.IsNullOrWhiteSpace(localised) ? name : localised);
    }
}
