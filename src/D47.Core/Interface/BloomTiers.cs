namespace D47.Core.Interface;

/// <summary>How far and how bright a glow reaches.</summary>
public enum BloomTier
{
    Low,
    Normal,
    High,
}

/// <summary>One layer of a glow: a CSS blur radius in pixels and an opacity from 0 to 1.</summary>
public readonly record struct BloomStop(double Radius, double Alpha);

/// <summary>The three glow tiers, each a stack of stops drawn widest last.</summary>
public static class BloomTiers
{
    /// <summary>The amount every glow is drawn at.</summary>
    public const double DefaultAmount = 1.1;

    /// <summary>No stop's alpha is raised past this multiple of the table's, whatever the amount.</summary>
    public const double MostBoost = 1.35;

    private static readonly BloomStop[] High =
        [new(2, 0.95), new(9, 0.84), new(26, 0.64), new(60, 0.38), new(112, 0.20)];

    private static readonly BloomStop[] Normal =
        [new(2, 0.88), new(6, 0.66), new(16, 0.42), new(38, 0.22)];

    private static readonly BloomStop[] Low =
        [new(2, 0.70), new(7, 0.40), new(18, 0.18)];

    /// <summary>A tier's stops at amount 1.</summary>
    public static IReadOnlyList<BloomStop> Table(BloomTier tier) => tier switch
    {
        BloomTier.High => High,
        BloomTier.Normal => Normal,
        BloomTier.Low => Low,
        _ => throw new ArgumentOutOfRangeException(nameof(tier)),
    };

    /// <summary>
    /// A tier's stops at <paramref name="amount"/>: radius scaled by it, alpha scaled by it but
    /// capped at <see cref="MostBoost"/> times the table's and at 1. None at an amount of 0 or less.
    /// </summary>
    public static IReadOnlyList<BloomStop> Stops(BloomTier tier, double amount)
    {
        if (amount <= 0)
        {
            return [];
        }

        return
        [
            .. Table(tier).Select(stop => new BloomStop(
                stop.Radius * amount,
                Math.Min(Math.Min(stop.Alpha * amount, stop.Alpha * MostBoost), 1))),
        ];
    }
}
