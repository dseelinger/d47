using Avalonia;
using Avalonia.Controls;

namespace D47.App.Theming;

/// <summary>Dresses a <see cref="Border"/> as a HUD card, in place of its own fill, border and
/// corner radius (#288).</summary>
public static class CardChrome
{
    /// <summary>A card: no corner radius, CardFill, a 1px Rule border. Selected or open:
    /// CardFillSelected with a 1px Accent border.</summary>
    public static void Card(Border border, bool selected = false) => Card(border, selected, track: null);

    /// <summary>As <see cref="Card(Border, bool)"/>, adding the two binding disposables to
    /// <paramref name="track"/> for a control whose bindings are torn down on rebuild.</summary>
    public static void Card(Border border, bool selected, List<IDisposable>? track)
    {
        border.CornerRadius = default;
        border.BorderThickness = new Thickness(1);

        var background = Themed(border, Border.BackgroundProperty, selected ? ThemeManager.CardFillSelectedKey : ThemeManager.CardFillKey);
        var borderBrush = Themed(border, Border.BorderBrushProperty, selected ? ThemeManager.AccentKey : ThemeManager.RuleKey);

        track?.Add(background);
        track?.Add(borderBrush);
    }

    /// <summary>The current row in a list: FillHigh behind a 3px Accent bar on the leading edge,
    /// no corner radius. Wraps the row's existing child behind the bar.</summary>
    public static void CurrentRow(Border row)
    {
        row.CornerRadius = default;
        Themed(row, Border.BackgroundProperty, ThemeManager.FillHighKey);

        var bar = new Border { Width = 3 };
        Themed(bar, Border.BackgroundProperty, ThemeManager.AccentKey);

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Left);
        layout.Children.Add(bar);
        layout.Children.Add(row.Child!);
        row.Child = layout;
    }

    private static IDisposable Themed(StyledElement target, AvaloniaProperty property, string key) =>
        target.Bind(property, target.GetResourceObservable(key));
}
