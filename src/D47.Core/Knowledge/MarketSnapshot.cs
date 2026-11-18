namespace D47.Core.Knowledge;

/// <summary>Where a price came from, which is the difference between a fact and a report.</summary>
public enum PriceSource
{
    /// <summary>Somebody else docked here and shared what they saw.</summary>
    Reported,

    /// <summary>
    /// The Commander's own <c>Market.json</c>, written by the game while they stood at the commodity
    /// board.
    /// </summary>
    Seen,
}

/// <summary>One commodity's standing at one station.</summary>
/// <param name="Commodity">
/// The market's own spelling — <c>Gold</c>, <c>Low Temperature Diamonds</c>.
/// </param>
public sealed record MarketQuote(string Commodity)
{
    /// <summary>What the station charges to sell it to the Commander.</summary>
    public int BuyPrice { get; init; }

    /// <summary>What the station pays the Commander for it.</summary>
    public int SellPrice { get; init; }

    /// <summary>How many tonnes the station wants.</summary>
    public int Demand { get; init; }

    /// <summary>How many tonnes the station has to sell.</summary>
    public int Supply { get; init; }

    /// <summary>A rare good.</summary>
    public bool IsRare { get; init; }

    /// <summary>The index's stock bracket, 0 to 3.</summary>
    public int? StockBracket { get; init; }

    /// <summary>The index's demand bracket, 0 to 3.</summary>
    public int? DemandBracket { get; init; }

    /// <summary>The galaxy-wide mean price.</summary>
    public int? MeanPrice { get; init; }

    /// <summary>The market's group for the commodity, such as <c>Metals</c>.</summary>
    public string? Category { get; init; }
}

/// <summary>One station's whole commodity market at one moment (Phase 36).</summary>
public sealed record MarketSnapshot
{
    public required string Station { get; init; }

    public required string System { get; init; }

    /// <summary>The station's market id; null from a source that does not send one.</summary>
    public long? MarketId { get; init; }

    /// <summary>The system's id64, what the journal writes as <c>SystemAddress</c>.</summary>
    public long? SystemAddress { get; init; }

    /// <summary>The body the station orbits or stands on, where the index names one.</summary>
    public string? Body { get; init; }

    /// <summary>The journal's <c>BodyID</c> of <see cref="Body"/>.</summary>
    public int? BodyId { get; init; }

    /// <summary>Galactic coordinates of the system, which is what makes a leg measurable.</summary>
    public double X { get; init; }

    public double Y { get; init; }

    public double Z { get; init; }

    /// <summary>Light seconds from the entry point.</summary>
    public double? DistanceToArrival { get; init; }

    public bool HasLargePad { get; init; }

    /// <summary>The index's own word for what this is — <c>Coriolis Starport</c>, <c>Outpost</c>.</summary>
    public string? Type { get; init; }

    /// <summary>When these prices were reported.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    public PriceSource Source { get; init; } = PriceSource.Reported;

    public IReadOnlyDictionary<string, MarketQuote> Quotes { get; init; } =
        new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The Power controlling the station's system, in the service's spelling.</summary>
    public string? ControllingPower { get; init; }

    /// <summary>The system's Powerplay state, in the service's spelling.</summary>
    public string? PowerState { get; init; }

    /// <summary>Light years from the reference system of the search.</summary>
    public double? Distance { get; init; }

    /// <summary>Pads of each size; null where the search does not give the count.</summary>
    public int? SmallPads { get; init; }

    public int? MediumPads { get; init; }

    public int? LargePads { get; init; }

    public IReadOnlyList<string> Services { get; init; } = [];

    public string? ControllingFaction { get; init; }

    public string? ControllingFactionState { get; init; }

    /// <summary>The controlling minor faction's influence as a fraction of 1.</summary>
    public double? ControllingFactionInfluence { get; init; }

    /// <summary>The station's own state, in the service's spelling.</summary>
    public string? State { get; init; }

    /// <summary>When the service last had a report of the station, as opposed to its prices (<see cref="UpdatedAt"/>).</summary>
    public DateTimeOffset? StationUpdatedAt { get; init; }

    public string? Allegiance { get; init; }

    public string? Government { get; init; }

    public string? PrimaryEconomy { get; init; }

    public string? SecondaryEconomy { get; init; }

    public IReadOnlyList<EconomyShare> Economies { get; init; } = [];

    public bool? HasMarket { get; init; }

    public bool? HasOutfitting { get; init; }

    public bool? HasShipyard { get; init; }

    public bool? IsPlanetary { get; init; }

    public string? BodyType { get; init; }

    public string? BodySubtype { get; init; }

    /// <summary>The body's id64.</summary>
    public long? BodyAddress { get; init; }

    /// <summary>Surface gravity in g.</summary>
    public double? BodyGravity { get; init; }

    public bool? BodyHasAtmosphere { get; init; }

    public bool? BodyHasRings { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public IReadOnlyList<string> ExportCommodities { get; init; } = [];

    public IReadOnlyList<string> ImportCommodities { get; init; } = [];

    public IReadOnlyList<string> ProhibitedCommodities { get; init; } = [];

    public long? SystemPopulation { get; init; }

    public IReadOnlyList<string> SystemPowers { get; init; } = [];

    public string? SystemPrimaryEconomy { get; init; }

    public string? SystemSecondaryEconomy { get; init; }

    public bool? SystemIsColonised { get; init; }

    public bool? SystemIsBeingColonised { get; init; }

    /// <summary>A Stronghold Carrier: one named so in a Stronghold system, or one Ardent types so.</summary>
    public bool IsStrongholdCarrier =>
        StrongholdCarrier.IsNamedOne(Station, PowerState)
        || string.Equals(Type, StrongholdCarrier.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>A Stronghold Carrier in a system controlled by any Power but <paramref name="pledgedPower"/>.</summary>
    public bool IsOtherPowersStrongholdCarrier(string? pledgedPower) =>
        StrongholdCarrier.IsOtherPowers(IsStrongholdCarrier, ControllingPower, pledgedPower);

    /// <summary>A fleet carrier, which sets its own prices and then moves. A Stronghold Carrier is not one.</summary>
    public bool IsCarrier => !IsStrongholdCarrier && IsCarrierType(Type);

    /// <summary>A pad on a planet's surface (#296).</summary>
    public bool IsSurface => IsSurfaceType(Type);

    /// <summary>
    /// <see cref="IsCarrier"/>'s rule over a bare type string, so the request that excludes carriers
    /// server-side can be built from the same words the local ranking drops them by (#308) rather than
    /// from a copy of them.
    /// </summary>
    public static bool IsCarrierType(string? type) =>
        type is { } named && named.Contains("Carrier", StringComparison.OrdinalIgnoreCase);

    /// <summary><see cref="IsSurface"/>'s rule over a bare type string, for the same reason.</summary>
    public static bool IsSurfaceType(string? type) =>
        type is { } named
        && (named.Contains("Planetary", StringComparison.OrdinalIgnoreCase)
            || named.Contains("Settlement", StringComparison.OrdinalIgnoreCase));

    public MarketQuote? Quote(string commodity) =>
        Quotes.TryGetValue(commodity, out var quote) ? quote : null;

    /// <summary>Light years to another market, from the coordinates both carry.</summary>
    public double DistanceTo(MarketSnapshot other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;

        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>Whether two snapshots describe the same place.</summary>
    public bool IsSamePlaceAs(string station, string system) =>
        string.Equals(Station, station, StringComparison.OrdinalIgnoreCase)
        && string.Equals(System, system, StringComparison.OrdinalIgnoreCase);
}
