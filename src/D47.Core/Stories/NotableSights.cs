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

    /// <summary>The first <see cref="Most"/> places of a sight type, in input order, outside <paramref name="seen"/> systems and, with a <paramref name="band"/>, within it of Sol.</summary>
    public static IReadOnlyList<NotablePlace> Pick(IReadOnlyList<NotablePlace> near, IReadOnlyCollection<string> seen, ChapterBand.Range? band)
    {
        ArgumentNullException.ThrowIfNull(near);
        ArgumentNullException.ThrowIfNull(seen);

        return
        [
            .. near
                .Where(place => Types.Contains(place.Type, StringComparer.Ordinal)
                    && !seen.Contains(place.System, StringComparer.OrdinalIgnoreCase)
                    && (band is not { } range || place.DistanceFrom(StarPosition.Origin) is var distance && distance >= range.Min && distance <= range.Max))
                .Take(Most),
        ];
    }
}
