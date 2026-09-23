using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>What a stat tile's value is, which sets its ink.</summary>
public enum StatInk
{
    /// <summary>A figure or a system name, in A.</summary>
    Value,

    /// <summary>The name of a ship, engineer or carrier, in White.</summary>
    Name,

    /// <summary>The Commander's current system, in Cyan.</summary>
    Here,
}

/// <summary>A read-only label and value on Slab, and the grid that holds a row of them.</summary>
public static class StatTile
{
    /// <summary>The gap between tiles in a grid.</summary>
    public const double Gap = 2;

    /// <summary>The narrowest a tile in a grid is laid out before the grid drops a column.</summary>
    public const double MinWidth = 150;

    public const double ValueSize = 17;

    /// <summary>One tile. It has no border and no hover state.</summary>
    public static Border Build(string label, string value, StatInk ink = StatInk.Value)
    {
        var caption = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(caption, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var figure = new TextBlock
        {
            Text = value,
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = ValueSize,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(figure, TextBlock.ForegroundProperty, InkKey(ink));

        var tile = new Border
        {
            Padding = new Thickness(14, 10),
            Child = new StackPanel { Spacing = 2, Children = { caption, figure } },
        };
        Themed(tile, Border.BackgroundProperty, ThemeManager.SlabKey);

        return tile;
    }

    /// <summary>Tiles in up to <paramref name="maxColumns"/> columns, dropping columns as the width falls.</summary>
    public static Grid Grid(IReadOnlyList<Control> tiles, int maxColumns = 4) =>
        Reflow.Grid(tiles, MinWidth, Gap, Gap, maxColumns);

    public static string InkKey(StatInk ink) => ink switch
    {
        StatInk.Name => ThemeManager.WhiteKey,
        StatInk.Here => ThemeManager.CyanKey,
        _ => ThemeManager.AKey,
    };

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
