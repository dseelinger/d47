using D47.Core.Journal;

namespace D47.Core.Adventures;

/// <summary>The distance band a story chapter's last stop must fall in when the chapter starts outside the bubble.</summary>
public static class ChapterBand
{
    /// <summary>Light years from Sol beyond which a chapter works toward Sol.</summary>
    public const double OutsideBubble = 500;

    /// <summary>The least a chapter moves the Commander nearer Sol.</summary>
    public const double LeastNearer = 100;

    /// <summary>The most a chapter moves the Commander nearer Sol.</summary>
    public const double MostNearer = 300;

    /// <summary>The distance from Sol a chapter starting <paramref name="start"/> light years out must end within.</summary>
    public readonly record struct Range(double Min, double Max);

    /// <summary>The band for a story chapter starting at <paramref name="start"/>, or null where the rule does not apply.</summary>
    public static Range? Of(StarPosition? start, AdventureAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);

        return ask is { Story: { Beacon: null, FinaleChapter: null, Home: null }, Rewrite: null, Length: not AdventureLength.Short }
            ? Of(start)
            : null;
    }

    /// <summary>The band for a chapter starting at <paramref name="start"/>, or null within the bubble.</summary>
    public static Range? Of(StarPosition? start) =>
        start is { } at ? Of(at.DistanceTo(StarPosition.Origin)) : null;

    /// <summary>The band for a chapter starting <paramref name="lightYears"/> from Sol.</summary>
    public static Range? Of(double lightYears) =>
        lightYears > OutsideBubble
            ? new Range(Math.Round(lightYears - MostNearer), Math.Round(lightYears - LeastNearer))
            : null;
}
