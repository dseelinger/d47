using Avalonia;

namespace D47.App.Theming;

/// <summary>The space d47 leaves between neighbouring elements.</summary>
public static class Gaps
{
    /// <summary>
    /// Between two tiles side by side or stacked: buttons, list rows, checkbox tiles, chips, segments,
    /// stat tiles. Not between a tile group and text.
    /// </summary>
    public const double Tile = 2;

    /// <summary>A tile gap on the leading edge, for a tile placed after another by margin.</summary>
    public static readonly Thickness TileBefore = new(Tile, 0, 0, 0);
}
