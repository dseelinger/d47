using System.Globalization;
using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>
/// One place the maintainer's own journals confirm a material at, chosen by hand rather than read from
/// Frontier's data (#243).
/// </summary>
/// <param name="TopsGroup">
/// The trade group (a <see cref="MaterialEntry.Line"/>) this site is the fastest site for, or null where
/// the site names no group at all (Sensor Fragment) or is an alternate to another site.
/// </param>
public sealed record FarmingSite(
    string System,
    StarPosition Position,
    string Body,
    string Method,
    string MaterialSymbol,
    string? TopsGroup,
    double? Latitude,
    double? Longitude,
    bool RespawnsOnRelog,
    bool JumpRangeWarning,
    bool IsAlternate = false)
{
    public string Coordinates =>
        Latitude is { } lat && Longitude is { } lon
            ? $"{lat.ToString("0.####", CultureInfo.InvariantCulture)}, {lon.ToString("0.####", CultureInfo.InvariantCulture)}"
            : "coordinates unrecorded";
}

/// <summary>
/// The hand-maintained farming table (#243): every body a <c>MaterialCollected</c> after a
/// <c>Touchdown</c> confirmed, plus the two Thargoid sites. Not generated — Frontier defines no such
/// list, so this is written and reviewed like <see cref="EmissionRules"/>.
/// </summary>
public static class FarmingSites
{
    /// <summary>
    /// The jump range a Diamondback Explorer confirmed reaching HIP 36601 and Outotz LS-K d8-3 with, on 2
    /// July 2026 — an unengineered size 5 class A SCO frame shift drive and a size 4 Guardian FSD Booster.
    /// </summary>
    public const double ConfirmedJumpRange = 48.62;

    /// <summary>The longest single jump either leg of that confirmed route needed.</summary>
    public const double LongestConfirmedJump = 45.74;

    /// <summary>What the confirmed trip needed, said once rather than at every site that warns about it.</summary>
    public const string Prerequisite =
        "The cheapest ship to start with is the Diamondback Explorer, and it still needs one of: FSD "
        + "engineering, FSD Injection synthesis, or a Guardian FSD Booster, unlocked at a Guardian tech "
        + "broker. FSD Injection is +25% at grade 1 (Carbon, Vanadium, Germanium) and +50% at grade 2 "
        + "(adds Cadmium, Niobium); the +100% grade 3 recipe needs Yttrium and Polonium.";

    /// <summary>Every site, in the order the issue's table gave them — the fallback order with no position.</summary>
    public static readonly IReadOnlyList<FarmingSite> All =
    [
        new(
            "HIP 12099", new StarPosition(-101.90625, -95.46875, -165.59375), "1 b", "Jameson crash site",
            "adaptiveencryptors", "encoded-encryption-files", -54.36, -50.34,
            RespawnsOnRelog: true, JumpRangeWarning: false),

        new(
            "HIP 17403", new StarPosition(-93.6875, -158.96875, -367.625), "A 4 a", "crashed Thargoid ship",
            "unknownenergysource", null, -7.3362, -6.2770,
            RespawnsOnRelog: true, JumpRangeWarning: false),

        new(
            "HR 3230", new StarPosition(394.15625, 19.25, -152.375), "3 a a", "brain trees",
            "selenium", "raw-4", 45.66, -30.49,
            RespawnsOnRelog: false, JumpRangeWarning: false),

        new(
            "HIP 36601", new StarPosition(337.8125, 562.96875, -1457.84375), "C 1 a", "shards",
            "polonium", "raw-6", -57.4599, 126.9543,
            RespawnsOnRelog: false, JumpRangeWarning: true),

        new(
            "HIP 36601", new StarPosition(337.8125, 562.96875, -1457.84375), "C 1 d", "shards",
            "ruthenium", "raw-3", -51.2639, 14.7291,
            RespawnsOnRelog: false, JumpRangeWarning: true),

        new(
            "HIP 36601", new StarPosition(337.8125, 562.96875, -1457.84375), "C 3 b", "shards",
            "tellurium", "raw-5", -17.6769, -55.1800,
            RespawnsOnRelog: false, JumpRangeWarning: true),

        new(
            "HIP 36601", new StarPosition(337.8125, 562.96875, -1457.84375), "C 5 a", "shards",
            "technetium", "raw-2", 3.5500, 111.5415,
            RespawnsOnRelog: false, JumpRangeWarning: true),

        new(
            "Outotz LS-K d8-3", new StarPosition(526.3125, 238.46875, -1613.75), "B 5 a", "shards",
            "yttrium", "raw-1", -1.9216, -145.7013,
            RespawnsOnRelog: false, JumpRangeWarning: true),

        new(
            "Outotz LS-K d8-3", new StarPosition(526.3125, 238.46875, -1613.75), "B 5 c", "shards",
            "antimony", "raw-7", -62.4905, 80.2426,
            RespawnsOnRelog: false, JumpRangeWarning: true),

        // An alternate to HIP 36601 C 1 d — same material, not the one the fastest tier names.
        new(
            "Outotz LS-K d8-3", new StarPosition(526.3125, 238.46875, -1613.75), "B 7 b", "shards",
            "ruthenium", null, -12.5, 83.8,
            RespawnsOnRelog: false, JumpRangeWarning: true, IsAlternate: true),
    ];

    /// <summary>The one site named as the fastest way to the top grade of a trade group.</summary>
    public static FarmingSite? FastestFor(string? tradeGroup) =>
        tradeGroup is null
            ? null
            : All.FirstOrDefault(site => !site.IsAlternate
                && string.Equals(site.TopsGroup, tradeGroup, StringComparison.Ordinal));

    /// <summary>A site that farms this exact material, for the materials no trade group ever reaches.</summary>
    public static FarmingSite? DirectSite(string materialSymbol) =>
        All.FirstOrDefault(site => !site.IsAlternate
            && string.Equals(site.MaterialSymbol, materialSymbol, StringComparison.Ordinal));

    /// <summary>What a site's category is, for <c>get_material_farming_route</c>'s <c>type</c> filter.</summary>
    public static string? CategoryOf(FarmingSite site) => site.TopsGroup switch
    {
        { } group when group.StartsWith("raw-", StringComparison.Ordinal) => "Raw",
        { } group when group.StartsWith("manufactured-", StringComparison.Ordinal) => "Manufactured",
        { } group when group.StartsWith("encoded-", StringComparison.Ordinal) => "Encoded",
        _ => MaterialCatalogue.Find(site.MaterialSymbol)?.Category,
    };

    /// <summary>
    /// What is true about the ship that would make this trip, compared against the one the confirmed
    /// route actually used. Never says a lesser range cannot make it — nothing shorter has been tried and
    /// failed.
    /// </summary>
    public static string JumpRangeAdvice(double? shipRange)
    {
        var comparison = shipRange switch
        {
            { } range when range >= ConfirmedJumpRange =>
                $"A ship with {range.ToString("0.##", CultureInfo.InvariantCulture)} ly of jump range has "
                + "made this trip.",

            { } range =>
                $"Your ship's jump range is {range.ToString("0.##", CultureInfo.InvariantCulture)} ly; the "
                + $"confirmed route's longest jump was "
                + $"{LongestConfirmedJump.ToString("0.##", CultureInfo.InvariantCulture)} ly.",

            null =>
                $"The confirmed route used a ship with "
                + $"{ConfirmedJumpRange.ToString("0.##", CultureInfo.InvariantCulture)} ly of range and no "
                + $"jump longer than {LongestConfirmedJump.ToString("0.##", CultureInfo.InvariantCulture)} ly.",
        };

        return $"{comparison} {Prerequisite}";
    }
}
