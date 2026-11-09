using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Stories;

/// <summary>The notable places a sightseeing chapter can send the Commander to.</summary>
public static class NotableSights
{
    public const int Most = 8;

    public static IReadOnlyList<string> Types { get; } =
    [
        "Sights and Scenery",
        "Planetary Features",
        "Stellar Features",
        "Green Gas Giants",
        "Nebulae",
        "Notable Stellar Phenomena",
    ];

    /// <summary>The <see cref="Most"/> places of a sight type nearest <paramref name="from"/> and within <paramref name="within"/> light years of it, outside <paramref name="seen"/> systems and, with a <paramref name="band"/>, within it of Sol; nearest first.</summary>
    public static IReadOnlyList<NotablePlace> Pick(IReadOnlyList<NotablePlace> near, IReadOnlyCollection<string> seen, ChapterBand.Range? band, StarPosition from, double within)
    {
        ArgumentNullException.ThrowIfNull(near);
        ArgumentNullException.ThrowIfNull(seen);

        return
        [
            .. near
                .Where(place => Types.Contains(place.Type, StringComparer.Ordinal)
                    && !seen.Contains(place.System, StringComparer.OrdinalIgnoreCase)
                    && place.DistanceFrom(from) <= within
                    && (band is not { } range || place.DistanceFrom(StarPosition.Origin) is var distance && distance >= range.Min && distance <= range.Max))
                .OrderBy(place => place.DistanceFrom(from))
                .Take(Most),
        ];
    }

    /// <summary>One of <paramref name="offered"/> drawn by <paramref name="seed"/> in proportion to its rating; uniformly when none is rated. Null when none is offered.</summary>
    public static NotablePlace? Choose(IReadOnlyList<NotablePlace> offered, uint seed)
    {
        ArgumentNullException.ThrowIfNull(offered);

        return offered.Count == 0 ? null : offered[new Random32(seed).Weighted([.. offered.Select(place => Math.Max(0, place.Rating ?? 0))])];
    }
}
