using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// The COLUMNS tile and the bar it opens over an <see cref="ArrangedTable{TRow}"/>, the same on the desktop and in
/// the headset. The page redraws on the table's <see cref="ArrangedTable{TRow}.Changed"/> and places what these return.
/// </summary>
public static class ColumnArranger
{
    public const string Choosing = "Press a column's name to choose it. Sorting is off until you press DONE.";

    public const string ChosenTail = " is chosen. Move it, remove it, or press another column's name. Sorting is off until you press DONE.";

    public const string PanelLabel = "Every column this table can show";

    private const double Gap = 8;

    /// <summary>The control name of a part of the arranger for table <paramref name="id"/>.</summary>
    public static string NameOf(string id, string part) => $"{id}.Columns.{part}";

    /// <summary>The COLUMNS tile, selected while the bar is open; null for a table with one column.</summary>
    public static Button? Tile<TRow>(ArrangedTable<TRow> table)
    {
        if (!table.CanArrange)
        {
            return null;
        }

        var tile = Part(table.Id, "Tile", "Columns", table.ToggleArranging);
        tile.Classes.Set("selected", table.Arranging);

        return tile;
    }

    /// <summary>The COLUMNS tile on a 44px line of its own, at the right; null for a table with one column.</summary>
    public static Control? Line<TRow>(ArrangedTable<TRow> table) =>
        Tile(table) is { } tile
            ? new Border
            {
                MinHeight = TypeScale.MinimumTarget,
                Child = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { tile } },
            }
            : null;

    /// <summary>The bar, the grey line under it and the ADD A COLUMN panel when open; null while the bar is closed.</summary>
    public static Control? Bar<TRow>(ArrangedTable<TRow> table)
    {
        if (!table.Arranging)
        {
            return null;
        }

        var left = new WrapPanel
        {
            ItemSpacing = Gaps.Tile,
            LineSpacing = Gaps.Tile,
            Children =
            {
                Part(table.Id, "MoveLeft", "◄ Move left", () => table.Move(left: true), table.CanMove(left: true)),
                Part(table.Id, "MoveRight", "Move right ►", () => table.Move(left: false), table.CanMove(left: false)),
                Part(table.Id, "Remove", "Remove", table.Remove, table.CanRemove),
                Part(table.Id, "Add", "Add a column ▼", table.ToggleAdding),
            },
        };
        left.Children[^1].Classes.Set("selected", table.Adding);

        var right = new WrapPanel
        {
            ItemSpacing = Gaps.Tile,
            LineSpacing = Gaps.Tile,
            Margin = Gaps.TileBefore,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                Part(table.Id, "Default", "Default columns", table.Default, !table.IsDefault),
                Part(table.Id, "Done", "Done", table.ToggleArranging),
            },
        };
        Grid.SetColumn(right, 1);

        var bar = new StackPanel
        {
            Spacing = Gap,
            Margin = new Thickness(0, Gap, 0, 0),
            Children =
            {
                new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { left, right } },
                Hint(table),
            },
        };

        if (table.Adding)
        {
            bar.Children.Add(Every(table));
        }

        return bar;
    }

    private static TextBlock Hint<TRow>(ArrangedTable<TRow> table)
    {
        var hint = Typeset.Text(string.Empty, TypeScale.Small, ThemeManager.GreyKey, wrap: true);
        hint.Name = NameOf(table.Id, "Hint");

        if (table.ChosenLabel is { } label)
        {
            var chosen = new Run(label.ToUpperInvariant());
            ThemeInk.Bind(chosen, TextElement.ForegroundProperty, ThemeManager.WhiteKey);
            hint.Inlines = [chosen, new Run(ChosenTail)];
        }
        else
        {
            hint.Text = Choosing;
        }

        return hint;
    }

    private static StackPanel Every<TRow>(ArrangedTable<TRow> table)
    {
        var grid = new TileGrid { Columns = 4 };

        foreach (var (key, label, shown, locked) in table.Choices)
        {
            var (box, text) = LabeledCheckBox.Build(label);
            text.TextWrapping = TextWrapping.Wrap;
            box.Name = NameOf(table.Id, "Show." + key);
            box.IsChecked = shown;
            box.IsEnabled = !locked;
            box.HorizontalAlignment = HorizontalAlignment.Stretch;
            box.VerticalAlignment = VerticalAlignment.Stretch;
            AutomationProperties.SetName(box, label);

            box.IsCheckedChanged += (_, _) =>
            {
                if (box.IsChecked == true)
                {
                    table.Show(key);
                }
                else
                {
                    table.Hide(key);
                }
            };

            grid.Children.Add(box);
        }

        return new StackPanel
        {
            Name = NameOf(table.Id, "Panel"),
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 6),
            Children = { StatTile.Caption(PanelLabel, ThemeManager.GreyKey), grid },
        };
    }

    private static Button Part(string id, string part, string text, Action press, bool enabled = true)
    {
        var button = new Button
        {
            Name = NameOf(id, part),
            Content = text,
            MinHeight = TypeScale.MinimumTarget,
            IsEnabled = enabled,
            VerticalAlignment = VerticalAlignment.Top,
        };
        button.Click += (_, _) => press();

        return button;
    }
}
