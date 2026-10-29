﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>What a stat tile's value is, which sets its ink.</summary>
public enum StatInk
{
    /// <summary>A word or a system name, in A.</summary>
    Value,

    /// <summary>A number, cost, time or date, in A mono.</summary>
    Number,

    /// <summary>The name of a ship, engineer or carrier, in White.</summary>
    Name,

    /// <summary>The Commander's current system, in Cyan.</summary>
    Here,

    /// <summary>A figure read from a setting — money, a count — in White mono.</summary>
    Figure,
}

/// <summary>A read-only label and value on Slab, and the grid that holds a row of them.</summary>
public static class StatTile
{
    /// <summary>The gap between tiles in a grid.</summary>
    public const double Gap = Gaps.Tile;

    /// <summary>The narrowest a tile in a grid is laid out before the grid drops a column.</summary>
    public const double MinWidth = 150;

    public const double ValueSize = 17;

    /// <summary>One tile. It has no border and no hover state.</summary>
    public static Border Build(string label, string value, StatInk ink = StatInk.Value)
    {
        var figure = new TextBlock
        {
            Text = value,
            FontFamily = new FontFamily(ink is StatInk.Figure or StatInk.Number ? Fonts.MonoFamily : Fonts.ChromeFamily),
            FontSize = ValueSize,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(figure, TextBlock.ForegroundProperty, InkKey(ink));

        return Tile(label, figure);
    }

    /// <summary>A tile around <paramref name="value"/> under an upper-case grey caption.</summary>
    public static Border Tile(string label, Control value)
    {
        var caption = Caption(label, ThemeManager.GreyKey);
        caption.FontWeight = FontWeight.Medium;

        var tile = new Border
        {
            Padding = new Thickness(14, 10),
            Child = new StackPanel { Spacing = 2, Children = { caption, value } },
        };
        ThemeInk.Bind(tile, Border.BackgroundProperty, ThemeManager.SlabKey);

        return tile;
    }

    /// <summary>Upper-case tracked chrome at meta size.</summary>
    public static TextBlock Caption(string text, string key)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, key);
        return block;
    }

    /// <summary>
    /// Tiles in up to <paramref name="maxColumns"/> columns, dropping columns as the width falls; tiles in
    /// one row share its height.
    /// </summary>
    public static Grid Grid(IReadOnlyList<Control> tiles, int maxColumns = 4)
    {
        var grid = Reflow.Grid(tiles, MinWidth, Gap, Gap, maxColumns);

        foreach (var tile in tiles)
        {
            tile.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        }

        return grid;
    }

    public static string InkKey(StatInk ink) => ink switch
    {
        StatInk.Name or StatInk.Figure => ThemeManager.WhiteKey,
        StatInk.Here => ThemeManager.CyanKey,
        _ => ThemeManager.AKey,
    };

}
