using System.Globalization;
using D47.Core.Knowledge;

namespace D47.Core.Checklists;

/// <summary>What the live plans still need, for every list or for one ship.</summary>
public sealed record PlanShortfall
{
    /// <summary>Short materials, then construction deliveries, which carry a site.</summary>
    public IReadOnlyList<ShortfallRow> Rows { get; init; } = [];

    public IReadOnlyList<ShortfallTrip> Trips { get; init; } = [];

    public IReadOnlyList<PlanBlock> Blocks { get; init; } = [];

    public IReadOnlyList<OneTripSite> OneTrip { get; init; } = [];

    /// <summary>Live engineering items costed.</summary>
    public int PlanCount { get; init; }

    /// <summary>Outstanding construction resources, one per site and commodity.</summary>
    public int DeliveryCount { get; init; }

    public IReadOnlyList<string> Uncovered { get; init; } = [];

    public IReadOnlyList<string> Assumed { get; init; } = [];
}

/// <summary>One material or delivery still short.</summary>
/// <param name="Site">The construction site, for a delivery; null for a plan's material.</param>
public sealed record ShortfallRow(
    string Name,
    MaterialLedger Ledger,
    int? Grade,
    int Held,
    int Needed,
    string? Site = null)
{
    public int Short => Math.Max(0, Needed - Held);
}

public enum ShortfallTripKind
{
    /// <summary>The plans need more of a material than its grade cap holds.</summary>
    Storage,

    /// <summary>A delivery is larger than the ship's hold.</summary>
    Cargo,
}

/// <summary>A need that cannot be met in one trip: trips is need over cap, rounded up.</summary>
public sealed record ShortfallTrip(ShortfallTripKind Kind, string Label, string Sentence, int Need, int Cap, int Trips)
{
    public static ShortfallTrip Storage(string material, int need, int cap)
    {
        var trips = TripsFor(need, cap);

        return new ShortfallTrip(
            ShortfallTripKind.Storage,
            material,
            $"{material}: your plans need {need} and you can only hold {cap}. "
            + $"That is at least {trips} trips whatever happens.",
            need,
            cap,
            trips);
    }

    public static ShortfallTrip Cargo(string commodity, string site, int need, int cap)
    {
        var trips = TripsFor(need, cap);

        return new ShortfallTrip(
            ShortfallTripKind.Cargo,
            commodity,
            $"{commodity}: {need} t still to deliver to {site} and the hold takes {cap} t. That is {trips} trips.",
            need,
            cap,
            trips);
    }

    private static int TripsFor(int need, int cap) => cap <= 0 ? 0 : (need + cap - 1) / cap;
}

/// <summary>A grade no unlocked engineer reaches.</summary>
/// <param name="Engineer">
/// The unlocked candidate with the highest rank, or the first candidate when none is unlocked.
/// </param>
/// <param name="Rank">That engineer's rank; null when no candidate is unlocked.</param>
public sealed record PlanBlock(string Item, int Grade, string? Engineer, int? Rank)
{
    public bool Unlocked => Rank is not null;

    /// <summary>"Felicity Farseer, rank 3", or "not unlocked".</summary>
    public string Reach => Rank is { } rank && Engineer is { } engineer
        ? $"{engineer}, rank {rank.ToString(CultureInfo.InvariantCulture)}"
        : "not unlocked";

    public string Sentence =>
        $"{Item} — no engineer you have unlocked offers grade "
        + $"{Grade.ToString(CultureInfo.InvariantCulture)} yet; counted at the most rolls it can take.";
}

/// <summary>Several short materials that one place gives.</summary>
/// <param name="Origin">The origin as the materials table writes it, or a building code on foot.</param>
/// <param name="Kind">The origin's text before its first parenthesis.</param>
public sealed record OneTripSite(string Origin, string Kind, IReadOnlyList<string> Materials, bool OnFoot = false)
{
    public static string KindOf(string origin) =>
        origin.IndexOf(" (", StringComparison.Ordinal) is var at and > 0 ? origin[..at] : origin;
}
