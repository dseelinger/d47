using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>What a gauge measures, which sets its fill.</summary>
public enum GaugeFill
{
    /// <summary>Progress toward something, in A.</summary>
    Progress,

    /// <summary>How full a store is, in Yellow.</summary>
    Capacity,

    /// <summary>Over its limit, in Red.</summary>
    Over,
}

/// <summary>A label with its value right-aligned on the same line, over a 6px track with a solid fill.</summary>
public static class Gauge
{
    public const double TrackHeight = 6;

    /// <summary>The label line and the track.</summary>
    public static Control Build(string label, string value, double fill, GaugeFill kind = GaugeFill.Progress) =>
        new StackPanel
        {
            Spacing = 4,
            Children = { Heading(label, value, FillKey(kind)), Track(fill, FillKey(kind)) },
        };

    /// <summary>The label in Grey on the left and the value in <paramref name="valueKey"/> on the right; the value wraps.</summary>
    public static DockPanel Heading(string label, string value, string valueKey)
    {
        var heading = new DockPanel();

        var name = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.Meta,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Meta * Fonts.ChromeTracking,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Themed(name, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        DockPanel.SetDock(name, Dock.Left);

        var reading = new TextBlock
        {
            Text = value,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Small,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        Themed(reading, TextBlock.ForegroundProperty, valueKey);

        heading.Children.Add(name);
        heading.Children.Add(reading);

        return heading;
    }

    /// <summary>A Tile track filled with <paramref name="fillKey"/> to <paramref name="fill"/>, clamped to 0–1.</summary>
    public static Grid Track(double fill, string fillKey)
    {
        var clamped = Math.Clamp(double.IsFinite(fill) ? fill : 0, 0, 1);

        var track = new Grid
        {
            Height = TrackHeight,
            ColumnDefinitions = new ColumnDefinitions
            {
                new(new GridLength(clamped, GridUnitType.Star)),
                new(new GridLength(1 - clamped, GridUnitType.Star)),
            },
        };

        var ground = new Border();
        Themed(ground, Border.BackgroundProperty, ThemeManager.TileKey);
        Grid.SetColumnSpan(ground, 2);
        track.Children.Add(ground);

        var filled = new Border();
        Themed(filled, Border.BackgroundProperty, fillKey);
        track.Children.Add(filled);

        return track;
    }

    public static string FillKey(GaugeFill kind) => kind switch
    {
        GaugeFill.Capacity => ThemeManager.YellowKey,
        GaugeFill.Over => ThemeManager.RedKey,
        _ => ThemeManager.AKey,
    };

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
