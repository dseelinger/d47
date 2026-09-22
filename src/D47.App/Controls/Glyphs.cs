using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

// System.IO is implicitly imported, and System.IO.Path is not the one meant anywhere here.
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Controls;

/// <summary>
/// The window caption's drawn marks, and the words and text glyphs every other affordance carries
/// instead of a picture.
/// </summary>
public static class Glyphs
{
    /// <summary>Reset to default, as a text glyph.</summary>
    public const string ResetText = "↺";

    /// <summary>Minimise a window: a filled bar.</summary>
    public const string Minimize =
        "M 6,11 L 18,11 A 1,1 0 0 1 18,13 L 6,13 A 1,1 0 0 1 6,11 Z";

    /// <summary>Maximise a window: an empty box.</summary>
    public const string Maximize = "M 5,5 L 19,5 L 19,19 L 5,19 Z";

    /// <summary>Restore a maximised window: two overlapping boxes.</summary>
    public const string Restore =
        "M 5,9 L 16,9 L 16,20 L 5,20 Z  M 9,7 L 9,4 L 20,4 L 20,15 L 17,15";

    /// <summary>Close a window: an X.</summary>
    public const string Close = "M 6,6 L 18,18  M 18,6 L 6,18";

    /// <summary>One mark, stroked or filled with the theme key <paramref name="brush"/> so a theme switch repaints it.</summary>
    public static Path Draw(string data, string brush, double size = 14, bool filled = false)
    {
        var geometry = Geometry.Parse(data);
        var bounds = geometry.Bounds;

        // Both sides, for a geometry with no area at all.
        var longest = Math.Max(Math.Max(bounds.Width, bounds.Height), 0.001);

        var glyph = new Path
        {
            Width = size * bounds.Width / longest,
            Height = size * bounds.Height / longest,
            Stretch = Stretch.Uniform,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Data = geometry,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };

        if (filled)
        {
            glyph.StrokeThickness = 0;
            glyph.Bind(Shape.FillProperty, glyph.GetResourceObservable(brush));
        }
        else
        {
            glyph.Bind(Shape.StrokeProperty, glyph.GetResourceObservable(brush));
        }

        return glyph;
    }

    /// <summary>Puts a mark on a button, with <paramref name="says"/> as its tooltip and accessible name.</summary>
    public static void Mark(
        Button button, string data, string brush, string says, double size = 14, bool filled = false)
    {
        button.Content = Draw(data, brush, size, filled);

        ToolTip.SetTip(button, says);
        AutomationProperties.SetName(button, says);
    }

    /// <summary>
    /// Makes <paramref name="button"/> a quiet word button: <paramref name="word"/> as its content, which the
    /// caller writes in capitals, and <paramref name="says"/> as its tooltip and accessible name.
    /// </summary>
    public static Button Quiet(Button button, string word, string says)
    {
        button.Classes.Add("quiet");
        button.Content = word;

        ToolTip.SetTip(button, says);
        AutomationProperties.SetName(button, says);

        return button;
    }

    /// <summary>A text glyph for a glyph button, untracked so it sits centred, its ink following the button's.</summary>
    public static TextBlock Text(string glyph, double size) => new()
    {
        Text = glyph,
        FontSize = size,
        LetterSpacing = 0,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };
}
