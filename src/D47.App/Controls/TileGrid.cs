using Avalonia;
using Avalonia.Controls;

namespace D47.App.Controls;

/// <summary>
/// Equal-width tiles in rows, <see cref="Gap"/> apart, laying out only the visible children so a hidden
/// tile closes up. Drops a column while a tile would be narrower than <see cref="MinTileWidth"/>; each row
/// is as tall as its tallest tile.
/// </summary>
public sealed class TileGrid : Avalonia.Controls.Panel
{
    /// <summary>The space between tiles, across and down.</summary>
    public const double Gap = 2;

    /// <summary>The narrowest a tile is drawn before the grid drops a column.</summary>
    public const double MinTileWidth = 150;

    /// <summary>How many tiles across where the width allows.</summary>
    public int Columns { get; init; } = 2;

    /// <summary>How many tiles across at this width.</summary>
    public int ColumnsAt(double width)
    {
        var columns = Math.Max(1, Columns);

        if (double.IsInfinity(width))
        {
            return columns;
        }

        while (columns > 1 && TileWidth(width, columns) < MinTileWidth)
        {
            columns--;
        }

        return columns;
    }

    private static double TileWidth(double width, int columns) =>
        Math.Max(0, (width - ((columns - 1) * Gap)) / columns);

    protected override Size MeasureOverride(Size availableSize)
    {
        var shown = Children.Where(child => child.IsVisible).ToList();
        var columns = ColumnsAt(availableSize.Width);
        var tile = double.IsInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : TileWidth(availableSize.Width, columns);

        foreach (var child in Children)
        {
            child.Measure(new Size(tile, double.PositiveInfinity));
        }

        var height = RowHeights(shown, columns).Sum() + (Math.Max(0, RowCount(shown.Count, columns) - 1) * Gap);
        var width = double.IsInfinity(tile)
            ? (shown.Count == 0 ? 0 : (shown.Max(child => child.DesiredSize.Width) * columns) + ((columns - 1) * Gap))
            : availableSize.Width;

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var shown = Children.Where(child => child.IsVisible).ToList();
        var columns = ColumnsAt(finalSize.Width);
        var tile = TileWidth(finalSize.Width, columns);
        var heights = RowHeights(shown, columns);

        var y = 0.0;

        for (var i = 0; i < shown.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;

            if (column == 0 && row > 0)
            {
                y += heights[row - 1] + Gap;
            }

            shown[i].Arrange(new Rect(column * (tile + Gap), y, tile, heights[row]));
        }

        return finalSize;
    }

    private static int RowCount(int count, int columns) => (count + columns - 1) / columns;

    private static List<double> RowHeights(List<Control> shown, int columns)
    {
        var heights = new List<double>();

        for (var i = 0; i < shown.Count; i += columns)
        {
            heights.Add(shown.Skip(i).Take(columns).Max(child => child.DesiredSize.Height));
        }

        return heights;
    }
}
