using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>The seam to whatever holds one system's full record.</summary>
public interface IStarSystemService
{
    /// <summary>One system in full, or null when the service has no record of it.</summary>
    Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken);

    /// <summary>Systems named like what was typed, the exact match first, at most five.</summary>
    Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken);

    /// <summary>Powerplay systems near a system, nearest first, the system itself left out.</summary>
    Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken);
}

/// <summary>A system whose name matched what was typed.</summary>
public sealed record SystemNameMatch(string Name, long SystemAddress, StarPosition Position);

/// <summary>One system as last reported: its politics, factions, Powerplay, stations and bodies.</summary>
public sealed record StarSystemProfile
{
    public required string Name { get; init; }

    public required long SystemAddress { get; init; }

    public StarPosition? Position { get; init; }

    public string? Allegiance { get; init; }

    public string? Government { get; init; }

    public string? PrimaryEconomy { get; init; }

    public string? SecondaryEconomy { get; init; }

    public string? Security { get; init; }

    public long? Population { get; init; }

    public bool NeedsPermit { get; init; }

    public string? ControllingFaction { get; init; }

    /// <summary>Most influence first.</summary>
    public IReadOnlyList<FactionStanding> Factions { get; init; } = [];

    /// <summary>Null when the system has no Powerplay presence.</summary>
    public PowerplayStanding? Powerplay { get; init; }

    /// <summary>Every station, the ones on bodies included.</summary>
    public IReadOnlyList<StationProfile> Stations { get; init; } = [];

    /// <summary>Stars and planets; barycentres are not bodies.</summary>
    public IReadOnlyList<BodyProfile> Bodies { get; init; } = [];

    public DateTimeOffset? ReportedAt { get; init; }
}

/// <summary>A minor faction in a system, its influence as a fraction of 1.</summary>
public sealed record FactionStanding(
    string Name,
    string? Government,
    string? Allegiance,
    double? Influence,
    IReadOnlyList<string> ActiveStates,
    IReadOnlyList<string> PendingStates);

/// <summary>A system's Powerplay state.</summary>
public sealed record PowerplayStanding
{
    public string? ControllingPower { get; init; }

    /// <summary>"Exploited", "Fortified", "Stronghold" and so on.</summary>
    public string? State { get; init; }

    /// <summary>Progress towards the next control state, as a fraction of 1.</summary>
    public double? ControlProgress { get; init; }

    public long? Reinforcement { get; init; }

    public long? Undermining { get; init; }

    /// <summary>Every power present, the controlling one included.</summary>
    public IReadOnlyList<string> Powers { get; init; } = [];
}

/// <summary>An Exploited, Fortified or Stronghold system near another.</summary>
public sealed record PowerplayNeighbour(
    string Name,
    double Distance,
    string? ControllingPower,
    string? State,
    double? ControlProgress,
    IReadOnlyList<string> Powers);

/// <summary>The Powerplay systems near one system: at most <see cref="Limit"/>, and how many matched.</summary>
public sealed record PowerplayNeighbourhood(int Total, IReadOnlyList<PowerplayNeighbour> Systems)
{
    public const int Limit = 100;

    /// <summary>Light years a system in this state reaches: 30 for a Stronghold, 20 otherwise.</summary>
    public static double PowerplayReach(string? state) => state == "Stronghold" ? 30 : 20;
}

/// <summary>What sort of place a station is.</summary>
public enum StationKind
{
    Starport,
    Outpost,
    SurfacePort,
    Settlement,
    Megaship,
    FleetCarrier,
    Other,
}

/// <summary>One station in a system.</summary>
public sealed record StationProfile
{
    public required string Name { get; init; }

    public StationKind Kind { get; init; }

    /// <summary>The service's own spelling of the type, or null when it gave none.</summary>
    public string? Type { get; init; }

    /// <summary>The body it stands on or orbits, or null when the service filed it under the system.</summary>
    public string? Body { get; init; }

    /// <summary>Light seconds from the arrival star.</summary>
    public double? DistanceToArrival { get; init; }

    public string? ControllingFaction { get; init; }

    public string? Government { get; init; }

    public string? PrimaryEconomy { get; init; }

    /// <summary>Null when the station reports no pads.</summary>
    public PadSize? LargestPad { get; init; }

    public IReadOnlyList<string> Services { get; init; } = [];

    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>A ring and the signals reported in it.</summary>
public sealed record RingProfile(string Name, string? Type, IReadOnlyDictionary<string, int> Signals);

/// <summary>One star or planet.</summary>
public sealed record BodyProfile
{
    /// <summary>The journal's <c>BodyID</c>.</summary>
    public required int BodyId { get; init; }

    public required string Name { get; init; }

    /// <summary>"Star" or "Planet".</summary>
    public required string Type { get; init; }

    /// <summary>"K (Yellow-Orange) Star", "Water world" and so on.</summary>
    public string? SubType { get; init; }

    /// <summary>Light seconds from the arrival star.</summary>
    public double? DistanceToArrival { get; init; }

    /// <summary>The star or planet it orbits, past any barycentre, or null for the primary.</summary>
    public int? ParentId { get; init; }

    /// <summary>A star's class and subclass, "K7".</summary>
    public string? SpectralClass { get; init; }

    public double? SolarMasses { get; init; }

    public double? SolarRadius { get; init; }

    public double? EarthMasses { get; init; }

    /// <summary>In kilometres.</summary>
    public double? Radius { get; init; }

    /// <summary>In g.</summary>
    public double? Gravity { get; init; }

    /// <summary>In kelvin.</summary>
    public double? SurfaceTemperature { get; init; }

    /// <summary>In atmospheres.</summary>
    public double? SurfacePressure { get; init; }

    public string? Volcanism { get; init; }

    public string? Atmosphere { get; init; }

    public string? TerraformingState { get; init; }

    public bool IsLandable { get; init; }

    public string? ReserveLevel { get; init; }

    public IReadOnlyList<RingProfile> Rings { get; init; } = [];

    /// <summary>Signal counts by the service's signal name.</summary>
    public IReadOnlyDictionary<string, int> Signals { get; init; } = new Dictionary<string, int>();

    /// <summary>Whether a fuel scoop works on this star; null for a planet or an unknown class.</summary>
    public bool? Scoopable => Type == "Star" ? StarClasses.IsScoopable(SpectralClass?.TrimEnd("0123456789".ToCharArray())) : null;
}
