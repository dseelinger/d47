using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>The seam to whatever holds one system's full record.</summary>
public interface IStarSystemService
{
    /// <summary>One system in full, or null when the service has no record of it.</summary>
    Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken);

    /// <summary>Systems named like what was typed, the exact match first, at most five.</summary>
    Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken);

    /// <summary>Systems whose names start with what was typed, for suggesting names while the Commander types.</summary>
    Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken);

    /// <summary>Minor faction names matching what was typed, at most eight, for suggesting names while the Commander types.</summary>
    Task<IReadOnlyList<string>> FactionNamesAsync(string typed, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    /// <summary>Stations whose names have a word starting with what was typed, in the service's order.</summary>
    Task<IReadOnlyList<StationNameMatch>> StationNamesAsync(string typed, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StationNameMatch>>([]);

    /// <summary>Bodies whose names match what was typed, in the service's order, for suggesting names while the Commander types.</summary>
    Task<IReadOnlyList<BodyNameMatch>> BodyNamesAsync(string typed, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BodyNameMatch>>([]);

    /// <summary>Powerplay systems near a system, nearest first, the system itself left out.</summary>
    Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken);
}

/// <summary>A system whose name matched what was typed.</summary>
public sealed record SystemNameMatch(string Name, long SystemAddress, StarPosition Position);

/// <summary>A station whose name matched what was typed, with the position of its system.</summary>
public sealed record StationNameMatch(string Name, long MarketId, long SystemAddress, StarPosition Position);

/// <summary>A body whose name matched what was typed, with the position of its system.</summary>
public sealed record BodyNameMatch(string Name, long BodyAddress, long SystemAddress, StarPosition Position);

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

    /// <summary>The barycentres, which are not in <see cref="Bodies"/>.</summary>
    public IReadOnlyList<BarycentreProfile> Barycentres { get; init; } = [];

    public DateTimeOffset? ReportedAt { get; init; }

    /// <summary>The flagged main star, else the star with the lowest <c>BodyId</c>; null with no star.</summary>
    public BodyProfile? MainStar =>
        Bodies.FirstOrDefault(body => body.IsMainStar)
        ?? Bodies.Where(body => body.Type == "Star").OrderBy(body => body.BodyId).FirstOrDefault();
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

    /// <summary>The dump's <c>id</c>; equals the <c>MarketId</c> a station search result carries.</summary>
    public long? MarketId { get; init; }

    public string? Allegiance { get; init; }

    public string? ControllingFactionState { get; init; }

    /// <summary>Each economy with its share as a percentage, largest first.</summary>
    public IReadOnlyList<EconomyShare> Economies { get; init; } = [];

    /// <summary>Null when the station reports no pad counts.</summary>
    public PadCounts? Pads { get; init; }

    /// <summary>Who may dock at a fleet carrier; null for any other station.</summary>
    public string? CarrierDockingAccess { get; init; }

    /// <summary>Null when the dump holds no market for the station.</summary>
    public StationMarket? Market { get; init; }

    /// <summary>Null when the dump holds no outfitting for the station.</summary>
    public StationOutfitting? Outfitting { get; init; }

    /// <summary>Null when the dump holds no shipyard for the station.</summary>
    public StationShipyard? Shipyard { get; init; }
}

/// <summary>One economy of a station and its share as a percentage.</summary>
public sealed record EconomyShare(string Name, double Share);

/// <summary>Landing pads by size; a size the station lacks is 0.</summary>
public sealed record PadCounts(int Small, int Medium, int Large);

/// <summary>One commodity row of a station's market.</summary>
public sealed record MarketCommodity(
    string Name,
    string? Symbol,
    string? Category,
    long BuyPrice,
    long SellPrice,
    long Supply,
    long Demand);

/// <summary>A station's market as last reported.</summary>
public sealed record StationMarket(IReadOnlyList<MarketCommodity> Commodities, DateTimeOffset? ReportedAt);

/// <summary>One module row of a station's outfitting.</summary>
public sealed record StationModule(string Name, string? Symbol, string? Category, int? Class, string? Rating, string? Ship);

/// <summary>A station's outfitting as last reported.</summary>
public sealed record StationOutfitting(IReadOnlyList<StationModule> Modules, DateTimeOffset? ReportedAt);

/// <summary>One ship row of a station's shipyard.</summary>
public sealed record StationShip(string Name, string? Symbol);

/// <summary>A station's shipyard as last reported.</summary>
public sealed record StationShipyard(IReadOnlyList<StationShip> Ships, DateTimeOffset? ReportedAt);

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

    /// <summary>The whole <c>parents</c> chain, nearest first; kind is Star, Planet or Null as written.</summary>
    public IReadOnlyList<(string Kind, int BodyId)> Parents { get; init; } = [];

    /// <summary>A star's class and subclass, "K7".</summary>
    public string? SpectralClass { get; init; }

    /// <summary>A star's luminosity class, "V".</summary>
    public string? Luminosity { get; init; }

    /// <summary>Whether this is the system's main star.</summary>
    public bool IsMainStar { get; init; }

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

    /// <summary>Surface materials by percentage share.</summary>
    public IReadOnlyDictionary<string, double> Materials { get; init; } = new Dictionary<string, double>();

    /// <summary>Atmosphere gases by percentage share.</summary>
    public IReadOnlyDictionary<string, double> AtmosphereComposition { get; init; } = new Dictionary<string, double>();

    /// <summary>Rock, metal and ice by percentage share.</summary>
    public IReadOnlyDictionary<string, double> SolidComposition { get; init; } = new Dictionary<string, double>();

    /// <summary>Biological genera reported on the body, by <see cref="CodexGenus.Name"/>.</summary>
    public IReadOnlyList<string> Genera { get; init; } = [];

    /// <summary>In days.</summary>
    public double? OrbitalPeriod { get; init; }

    /// <summary>In astronomical units.</summary>
    public double? SemiMajorAxis { get; init; }

    public double? OrbitalEccentricity { get; init; }

    /// <summary>In degrees.</summary>
    public double? OrbitalInclination { get; init; }

    /// <summary>In degrees.</summary>
    public double? ArgOfPeriapsis { get; init; }

    /// <summary>In degrees.</summary>
    public double? AscendingNode { get; init; }

    /// <summary>In degrees, as of <see cref="MeanAnomalyAt"/>.</summary>
    public double? MeanAnomaly { get; init; }

    public DateTimeOffset? MeanAnomalyAt { get; init; }

    /// <summary>In days.</summary>
    public double? RotationalPeriod { get; init; }

    public bool? TidallyLocked { get; init; }

    /// <summary>In degrees.</summary>
    public double? AxialTilt { get; init; }

    /// <summary>A star's age in millions of years.</summary>
    public double? Age { get; init; }

    public double? AbsoluteMagnitude { get; init; }

    public DateTimeOffset? ReportedAt { get; init; }

    /// <summary>Whether a fuel scoop works on this star; null for a planet or an unknown class.</summary>
    public bool? Scoopable => Type == "Star" ? StarClasses.IsScoopable(SpectralClass?.TrimEnd("0123456789".ToCharArray())) : null;
}

/// <summary>The centre two or more bodies orbit; it has no physical body.</summary>
public sealed record BarycentreProfile
{
    /// <summary>The journal's <c>BodyID</c>.</summary>
    public required int BodyId { get; init; }

    /// <summary>What it orbits, nearest first; empty when no body names it.</summary>
    public IReadOnlyList<(string Kind, int BodyId)> Parents { get; init; } = [];

    /// <summary>In astronomical units.</summary>
    public double? SemiMajorAxis { get; init; }

    public double? OrbitalEccentricity { get; init; }

    /// <summary>In degrees.</summary>
    public double? OrbitalInclination { get; init; }

    /// <summary>In degrees.</summary>
    public double? ArgOfPeriapsis { get; init; }

    /// <summary>In degrees.</summary>
    public double? AscendingNode { get; init; }

    /// <summary>In days.</summary>
    public double? OrbitalPeriod { get; init; }

    /// <summary>In degrees, as of <see cref="MeanAnomalyAt"/>.</summary>
    public double? MeanAnomaly { get; init; }

    public DateTimeOffset? MeanAnomalyAt { get; init; }
}
