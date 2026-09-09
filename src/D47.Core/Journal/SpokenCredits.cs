using System.Globalization;

namespace D47.Core.Journal;

/// <summary>Bands a credit figure the way a Commander says one out loud (#336).</summary>
public static class SpokenCredits
{
    private const long Million = 1_000_000;
    private const long Billion = 1_000_000_000;
    private const long Trillion = 1_000_000_000_000;

    // Smallest to largest, so escalating one step up (see Band) is simply the next entry.
    private static readonly (long Unit, string Name)[] Bands =
    [
        (Million, "million"),
        (Billion, "billion"),
        (Trillion, "trillion"),
    ];

    /// <summary>
    /// <paramref name="amount"/> as it is said out loud: exact digits below a million, otherwise banded
    /// at millions, billions or trillions to one decimal with the trailing zero dropped.
    /// </summary>
    public static string Band(long amount)
    {
        var size = Math.Abs(amount);

        if (size < Million)
        {
            return size.ToString("N0", CultureInfo.InvariantCulture);
        }

        // The largest band the figure reaches, which is never -1: a million is the first entry and anything
        // below it has already returned.
        var index = Array.FindLastIndex(Bands, band => size >= band.Unit);

        var (unit, name) = Bands[index];
        var scaled = Math.Round((double)size / unit, 1, MidpointRounding.AwayFromZero);

        // One decimal, trailing zero dropped — but rounding a figure like 999,960,000 to one decimal reaches
        // 1000.0, which belongs to the band above and not this one ("2100 million" is the defect this helper
        // exists to fix), so a rounded-up 1000 climbs a band instead of being printed.
        if (scaled >= 1000 && index < Bands.Length - 1)
        {
            (unit, name) = Bands[index + 1];
            scaled = Math.Round((double)size / unit, 1, MidpointRounding.AwayFromZero);
        }

        return scaled.ToString("0.#", CultureInfo.InvariantCulture) + " " + name;
    }
}
