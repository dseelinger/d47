using Avalonia;
using Avalonia.Controls;

namespace D47.App.Theming;

/// <summary>Dresses a <see cref="Border"/> as a HUD card, in place of its own fill, border and
/// corner radius (#288).</summary>
public static class CardChrome
{
    /// <summary>A card: a filled Tile with no border. Selected: filled A, and the caller inks the name
    /// Knock and the meta Brown.</summary>
    public static void Card(Border border, bool selected = false) => Card(border, selected, track: null);

    /// <summary>As <see cref="Card(Border, bool)"/>, adding the binding disposable to
    /// <paramref name="track"/> for a control whose bindings are torn down on rebuild.</summary>
    public static void Card(Border border, bool selected, List<IDisposable>? track)
    {
        border.BorderThickness = default;

        var background = Themed(border, Border.BackgroundProperty, selected ? ThemeManager.AKey : ThemeManager.TileKey);

        track?.Add(background);
    }

    /// <summary>The current row in a list: Tile2 behind a 3px A bar on the leading edge. Wraps the row's existing child behind the bar.</summary>
    public static void CurrentRow(Border row)
    {
        Themed(row, Border.BackgroundProperty, ThemeManager.Tile2Key);

        var bar = new Border { Width = 3 };
        Themed(bar, Border.BackgroundProperty, ThemeManager.AKey);

        var child = row.Child!;
        row.Child = null;

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Left);
        layout.Children.Add(bar);
        layout.Children.Add(child);
        row.Child = layout;
    }

    private static IDisposable Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
