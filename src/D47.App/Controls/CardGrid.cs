using Avalonia;
using Avalonia.Controls;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// Equal-width cards in rows, <see cref="Gaps.Tile"/> apart, as many across as fit with each at least
/// <see cref="MinCardWidth"/> wide. A row is as tall as its tallest card.
/// </summary>
public sealed class CardGrid : Avalonia.Controls.Panel
{
    public const double MinCardWidth = 260;

    private const double Gap = Gaps.Tile;

    /// <summary>How many cards across at this width.</summary>
    public static int ColumnsAt(double width) =>
        double.IsInfinity(width) ? 1 : Math.Max(1, (int)Math.Floor((width + Gap) / (MinCardWidth + Gap)));

    private static double CardWidth(double width, int columns) =>
        double.IsInfinity(width) ? MinCardWidth : Math.Max(0, (width - ((columns - 1) * Gap)) / columns);

    protected override Size MeasureOverride(Size availableSize)
    {
        var shown = Children.Where(child => child.IsVisible).ToList();
        var columns = ColumnsAt(availableSize.Width);
        var card = CardWidth(availableSize.Width, columns);

        foreach (var child in shown)
        {
            child.Measure(new Size(card, double.PositiveInfinity));
        }

        var rows = RowHeights(shown, columns);
        var width = double.IsInfinity(availableSize.Width) ? (columns * card) + ((columns - 1) * Gap) : availableSize.Width;

        return new Size(width, rows.Sum() + (Math.Max(0, rows.Count - 1) * Gap));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var shown = Children.Where(child => child.IsVisible).ToList();
        var columns = ColumnsAt(finalSize.Width);
        var card = CardWidth(finalSize.Width, columns);
        var rows = RowHeights(shown, columns);
        var y = 0.0;

        for (var i = 0; i < shown.Count; i++)
        {
            var row = i / columns;

            if (i % columns == 0 && row > 0)
            {
                y += rows[row - 1] + Gap;
            }

            shown[i].Arrange(new Rect((i % columns) * (card + Gap), y, card, rows[row]));
        }

        return finalSize;
    }

    private static List<double> RowHeights(List<Control> shown, int columns) =>
        [.. shown.Chunk(columns).Select(row => row.Max(child => child.DesiredSize.Height))];
}
