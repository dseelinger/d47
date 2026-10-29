using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace D47.App.Theming;

/// <summary>Wrapping <see cref="TextBlock"/>s inked from a theme resource key.</summary>
public static class ThemedText
{
    public static TextBlock Text(string text, double size, string key, bool mono = false, bool chrome = false)
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };

        if (mono)
        {
            block.FontFamily = new FontFamily(Fonts.MonoFamily);
        }
        else if (chrome)
        {
            block.FontFamily = new FontFamily(Fonts.ChromeFamily);
            block.FontWeight = FontWeight.Medium;
            block.LetterSpacing = size * Fonts.ChromeTracking;
        }

        block[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(key);

        return block;
    }

    public static TextBlock Muted(string text, string key = ThemeManager.GreyKey) =>
        Text(text, TypeScale.Secondary, key);
}
