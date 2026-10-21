using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>What a <c>Scan</c> said about one body, and whether a footfall has since been taken there.</summary>
public sealed record BodyScan(string BodyName)
{
    public long? SystemAddress { get; init; }

    public int? BodyId { get; init; }

    public string? PlanetClass { get; init; }

    public string? Atmosphere { get; init; }

    public string? Volcanism { get; init; }

    public double? SurfaceGravity { get; init; }

    public double? SurfaceTemperature { get; init; }

    public double? SurfacePressure { get; init; }

    public bool Landable { get; init; }

    /// <summary>Null when the scan carried no <c>WasFootfalled</c> field at all.</summary>
    public bool? WasFootfalled { get; init; }

    public DateTimeOffset SeenAt { get; init; }

    /// <summary>When a Commander first disembarked onto this body, once its scan said no footfall yet.</summary>
    public DateTimeOffset? FootfallTakenAt { get; init; }
}

/// <summary>Every body this Commander has scanned this session, keyed by system and body ID (#202).</summary>
public sealed record BodyScans
{
    public static readonly BodyScans Empty = new();

    private readonly record struct BodyKey(long SystemAddress, int BodyId);

    private IReadOnlyDictionary<BodyKey, BodyScan> Bodies { get; init; } = new Dictionary<BodyKey, BodyScan>();

    private IReadOnlySet<long> Populated { get; init; } = new HashSet<long>();

    /// <summary>Whether <paramref name="journalEvent"/> is a jump or location event, which says if its system is populated.</summary>
    public static bool IsSystemEntry(JournalEvent journalEvent) => journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump";

    /// <summary>The <c>WasFootfalled</c> flag of a scan, true wherever the system is populated, since no first footfall is awarded there.</summary>
    public static bool? FootfallFlag(JournalEvent scan, bool populated) =>
        populated ? true : NullableBool(scan, "WasFootfalled");

    /// <summary>The population of a system-entry event, or null when it carries no system address.</summary>
    public static (long SystemAddress, bool Populated)? PopulationOf(JournalEvent journalEvent) =>
        IsSystemEntry(journalEvent) && journalEvent.Long("SystemAddress") is { } address
            ? (address, journalEvent.Long("Population") is > 0)
            : null;

    public BodyScan? For(long systemAddress, int bodyId) =>
        Bodies.GetValueOrDefault(new BodyKey(systemAddress, bodyId));

    public BodyScans Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "Scan" => ApplyScan(journalEvent),
        "Disembark" => ApplyDisembark(journalEvent),
        "FSDJump" or "Location" or "CarrierJump" => ApplyEntry(journalEvent),
        _ => this,
    };

    private BodyScans ApplyEntry(JournalEvent journalEvent)
    {
        if (PopulationOf(journalEvent) is not { } entry || Populated.Contains(entry.SystemAddress) == entry.Populated)
        {
            return this;
        }

        var set = new HashSet<long>(Populated);
        _ = entry.Populated ? set.Add(entry.SystemAddress) : set.Remove(entry.SystemAddress);

        return this with { Populated = set };
    }

    private BodyScans ApplyScan(JournalEvent journalEvent)
    {
        if (journalEvent.Long("SystemAddress") is not { } systemAddress ||
            journalEvent.Int("BodyID") is not { } bodyId ||
            journalEvent.String("BodyName") is not { } bodyName)
        {
            return this;
        }

        var key = new BodyKey(systemAddress, bodyId);
        Bodies.TryGetValue(key, out var existing);

        var scan = new BodyScan(bodyName)
        {
            SystemAddress = systemAddress,
            BodyId = bodyId,
            PlanetClass = journalEvent.String("PlanetClass"),
            Atmosphere = journalEvent.String("Atmosphere"),
            Volcanism = journalEvent.String("Volcanism"),
            SurfaceGravity = journalEvent.Double("SurfaceGravity"),
            SurfaceTemperature = journalEvent.Double("SurfaceTemperature"),
            SurfacePressure = journalEvent.Double("SurfacePressure"),
            Landable = journalEvent.Bool("Landable"),
            WasFootfalled = FootfallFlag(journalEvent, Populated.Contains(systemAddress)),
            SeenAt = journalEvent.Timestamp,
            FootfallTakenAt = existing?.FootfallTakenAt,
        };

        return this with { Bodies = new Dictionary<BodyKey, BodyScan>(Bodies) { [key] = scan } };
    }

    private BodyScans ApplyDisembark(JournalEvent journalEvent)
    {
        if (journalEvent.Bool("SRV") || !journalEvent.Bool("OnPlanet"))
        {
            return this;
        }

        if (journalEvent.Long("SystemAddress") is not { } systemAddress ||
            journalEvent.Int("BodyID") is not { } bodyId)
        {
            return this;
        }

        var key = new BodyKey(systemAddress, bodyId);

        if (!Bodies.TryGetValue(key, out var scan) || scan.WasFootfalled != false || scan.FootfallTakenAt is not null)
        {
            return this;
        }

        return this with
        {
            Bodies = new Dictionary<BodyKey, BodyScan>(Bodies)
            {
                [key] = scan with { FootfallTakenAt = journalEvent.Timestamp },
            },
        };
    }

    /// <summary>True or false where the journal wrote the flag, null where it omitted it entirely.</summary>
    private static bool? NullableBool(JournalEvent journalEvent, string property) =>
        journalEvent.Raw.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => (bool?)null,
            }
            : null;
}
