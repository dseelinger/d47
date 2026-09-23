using Avalonia.Controls;
using Avalonia.Layout;

namespace D47.App.Controls;

/// <summary>A grid that lays its cells in reading order in as many equal columns as fit its width.</summary>
public static class Reflow
{
    /// <summary>
    /// Lays <paramref name="cells"/> in up to <paramref name="maxColumns"/> equal columns, as many as fit at
    /// <paramref name="minColumn"/> each, and re-lays them when the width changes.
    /// </summary>
    public static Grid Grid(
        IReadOnlyList<Control> cells, double minColumn, double columnGap, double rowGap, int maxColumns)
    {
        var grid = new Grid { ColumnSpacing = columnGap, RowSpacing = rowGap };

        foreach (var cell in cells)
        {
            cell.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(cell);
        }

        var laid = 0;

        void Lay(double width)
        {
            var columns = Columns(width, minColumn, columnGap, maxColumns);

            if (columns == laid)
            {
                return;
            }

            laid = columns;
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();

            for (var c = 0; c < columns; c++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            }

            for (var r = 0; r < (cells.Count + columns - 1) / columns; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            for (var i = 0; i < cells.Count; i++)
            {
                Avalonia.Controls.Grid.SetColumn(cells[i], i % columns);
                Avalonia.Controls.Grid.SetRow(cells[i], i / columns);
            }
        }

        Lay(minColumn * maxColumns + columnGap * (maxColumns - 1));
        grid.SizeChanged += (_, e) => Lay(e.NewSize.Width);

        return grid;
    }

    /// <summary>How many columns of at least <paramref name="minColumn"/> fit in <paramref name="width"/>.</summary>
    public static int Columns(double width, double minColumn, double columnGap, int maxColumns) =>
        Math.Clamp((int)Math.Floor((width + columnGap) / (minColumn + columnGap)), 1, maxColumns);
}
