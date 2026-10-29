using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace D47.App.Theming;

/// <summary>Text blocks in each typeface, inked from a theme resource key. A parameter left null leaves
/// the property unset, so it is inherited.</summary>
public static class Typeset
{
    /// <summary>Text in the default font.</summary>
    public static TextBlock Text(
        string text, double size, string key, FontWeight? weight = null, double? tracking = null,
        bool wrap = false, TextTrimming? trimming = null, VerticalAlignment? vertical = null,
        HorizontalAlignment? horizontal = null, TextAlignment? align = null, Thickness? margin = null) =>
        Build(text, null, size, key, weight, tracking, wrap, trimming, vertical, horizontal, align, margin);

    /// <summary>Saira.</summary>
    public static TextBlock Chrome(
        string text, double size, string key, FontWeight? weight = null, double? tracking = null,
        bool wrap = false, TextTrimming? trimming = null, VerticalAlignment? vertical = null,
        HorizontalAlignment? horizontal = null, TextAlignment? align = null, Thickness? margin = null) =>
        Build(text, Fonts.ChromeFamily, size, key, weight, tracking, wrap, trimming, vertical, horizontal, align, margin);

    /// <summary>JetBrains Mono.</summary>
    public static TextBlock Mono(
        string text, double size, string key, FontWeight? weight = null, double? tracking = null,
        bool wrap = false, TextTrimming? trimming = null, VerticalAlignment? vertical = null,
        HorizontalAlignment? horizontal = null, TextAlignment? align = null, Thickness? margin = null) =>
        Build(text, Fonts.MonoFamily, size, key, weight, tracking, wrap, trimming, vertical, horizontal, align, margin);

    /// <summary>Sintony.</summary>
    public static TextBlock Prose(
        string text, double size, string key, FontWeight? weight = null, double? tracking = null,
        bool wrap = false, TextTrimming? trimming = null, VerticalAlignment? vertical = null,
        HorizontalAlignment? horizontal = null, TextAlignment? align = null, Thickness? margin = null) =>
        Build(text, Fonts.ProseFamily, size, key, weight, tracking, wrap, trimming, vertical, horizontal, align, margin);

    private static TextBlock Build(
        string text, string? family, double size, string key, FontWeight? weight, double? tracking,
        bool wrap, TextTrimming? trimming, VerticalAlignment? vertical, HorizontalAlignment? horizontal,
        TextAlignment? align, Thickness? margin)
    {
        var block = new TextBlock { Text = text, FontSize = size };

        if (family is not null)
        {
            block.FontFamily = new FontFamily(family);
        }

        if (weight is { } w)
        {
            block.FontWeight = w;
        }

        if (tracking is { } t)
        {
            block.LetterSpacing = t;
        }

        if (wrap)
        {
            block.TextWrapping = TextWrapping.Wrap;
        }

        if (trimming is { } trim)
        {
            block.TextTrimming = trim;
        }

        if (vertical is { } v)
        {
            block.VerticalAlignment = v;
        }

        if (horizontal is { } h)
        {
            block.HorizontalAlignment = h;
        }

        if (align is { } a)
        {
            block.TextAlignment = a;
        }

        if (margin is { } m)
        {
            block.Margin = m;
        }

        ThemeInk.Bind(block, TextBlock.ForegroundProperty, key);
        return block;
    }
}
