namespace D47.Core.Knowledge;

/// <summary>
/// Where a price came from, which is the difference between a fact and a report.
/// </summary>
public enum PriceSource
{
    /// <summary>
    /// Somebody else docked here and shared what they saw. Everything beyond the stations this
    /// Commander has personally visited is this, and it is untrusted input on the same footing as
    /// the journal and in-game comms (CLAUDE.md).
    /// </summary>
    Reported,

    /// <summary>
    /// The Commander's own <c>Market.json</c>, written by the game while they stood at the
    /// commodity board. Exact, free, and covers almost nothing of the galaxy — which is the whole
    /// reason <see cref="Reported"/> exists beside it (Phase 36).
    /// </summary>
    Seen,
}

/// <summary>
/// One commodity's standing at one station.
/// </summary>
/// <param name="Commodity">
/// The market's own spelling — <c>Gold</c>, <c>Low Temperature Diamonds</c>. This is the join key
/// between two stations' markets and between a market and the Commander's hold, so it is folded
/// case-insensitively everywhere it is compared and never re-spelled.
/// </param>
public sealed record MarketQuote(string Commodity)
{
    /// <summary>What the station charges to sell it to the Commander. Zero means it does not.</summary>
    public int BuyPrice { get; init; }

    /// <summary>What the station pays the Commander for it. Zero means it does not buy it.</summary>
    public int SellPrice { get; init; }

    /// <summary>
    /// How many tonnes the station wants. <b>The cap on a leg</b>, and the reason a plan does not
    /// promise a profit it cannot deliver: selling far past demand drops what the rest of it
    /// fetches, by a factor nobody here is willing to invent (Phase 36).
    /// </summary>
    public int Demand { get; init; }

    /// <summary>How many tonnes the station has to sell.</summary>
    public int Supply { get; init; }

    /// <summary>
    /// A rare good. Priced per-station, capped at a handful of tonnes by the game itself and worth
    /// less the closer it is sold to home — none of which this planner models, so rares are left
    /// out of a plan rather than mispriced inside one.
    /// </summary>
    public bool IsRare { get; init; }
}

/// <summary>
/// One station's whole commodity market at one moment (Phase 36).
/// <para>
/// The unit the planner works in. It is deliberately a flat record of numbers with no idea where
/// it came from beyond <see cref="Source"/> — the Spansh station search fills one in, the
/// Commander's own <c>Market.json</c> fills one in, and the arithmetic downstream cannot tell
/// which is which except by asking.
/// </para>
/// </summary>
public sealed record MarketSnapshot
{
    public required string Station { get; init; }

    public required string System { get; init; }

    /// <summary>
    /// Galactic coordinates of the system, which is what makes a leg measurable. Without them a
    /// station cannot be routed to and is dropped before the planner ever sees it.
    /// </summary>
    public double X { get; init; }

    public double Y { get; init; }

    public double Z { get; init; }

    /// <summary>
    /// Light seconds from the entry point. The half of "how far" a Commander actually feels: a
    /// station four light years away and 300,000 light seconds in-system is a worse trip than one
    /// twice as far with a pad at the star.
    /// </summary>
    public double? DistanceToArrival { get; init; }

    public bool HasLargePad { get; init; }

    /// <summary>The index's own word for what this is — <c>Coriolis Starport</c>, <c>Outpost</c>.</summary>
    public string? Type { get; init; }

    /// <summary>
    /// When these prices were reported. <b>Load-bearing rather than decoration</b>: a route built
    /// on a price nobody checked the age of is a route to an empty pad.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    public PriceSource Source { get; init; } = PriceSource.Reported;

    public IReadOnlyDictionary<string, MarketQuote> Quotes { get; init; } =
        new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A fleet carrier, which sets its own prices and then <em>moves</em>.
    /// <para>
    /// Measured 2026-08-19: the best Gold price within 50 light years of Sol was 4,760,900 credits
    /// at a carrier against 52,282 at the best station. A planner that ranks on price and does not
    /// know what a carrier is will build every plan around one, and half of them will have jumped
    /// by the time the Commander arrives. So carriers are named here and dropped by the planner.
    /// </para>
    /// </summary>
    public bool IsCarrier =>
        Type is { } type && type.Contains("Carrier", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A pad on a planet's surface (#296). The index's own words for one, measured against the
    /// live station search on 2026-09-05: <c>Planetary Outpost</c>, <c>Planetary Port</c>,
    /// <c>Settlement</c> and <c>Surface Settlement</c>. A station with no type is not surface —
    /// unknown is not a reason to exclude it.
    /// </summary>
    public bool IsSurface =>
        Type is { } type
        && (type.Contains("Planetary", StringComparison.OrdinalIgnoreCase)
            || type.Contains("Settlement", StringComparison.OrdinalIgnoreCase));

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

    /// <summary>
    /// Whether two snapshots describe the same place. Station names repeat across the galaxy —
    /// there are dozens of "Jameson Memorial" jokes and hundreds of real collisions — so the
    /// system is half of the identity and never dropped from it.
    /// </summary>
    public bool IsSamePlaceAs(string station, string system) =>
        string.Equals(Station, station, StringComparison.OrdinalIgnoreCase)
        && string.Equals(System, system, StringComparison.OrdinalIgnoreCase);
}
