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
}

/// <summary>One station's whole commodity market at one moment (Phase 36).</summary>
public sealed record MarketSnapshot
{
    public required string Station { get; init; }

    public required string System { get; init; }

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

    /// <summary>A fleet carrier, which sets its own prices and then moves.</summary>
    public bool IsCarrier => IsCarrierType(Type);

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

    /// <summary>What the station pays for a commodity, or zero where it does not buy it.</summary>
    public int Pays(string commodity) => Quote(commodity) is { Demand: > 0 } quote ? quote.SellPrice : 0;

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
