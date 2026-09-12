using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>
/// The one button that puts a system name on the clipboard, through the seam every draw site shares
/// instead of reaching for a mechanism of its own (#157).
/// </summary>
public static class CopyGlyph
{
    private const double Size = 24;
    private const double GlyphSize = 12;
    private static readonly TimeSpan Shown = TimeSpan.FromSeconds(2);

    /// <summary>A button that copies <paramref name="value"/> through <paramref name="copy"/>.</summary>
    public static Button For(string value, Func<string, Task<bool>> copy)
    {
        var button = new Button
        {
            Width = Size,
            Height = Size,
            Padding = new Thickness(4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        Reset(button, value);

        button.Click += async (_, _) =>
        {
            bool worked;

            try
            {
                worked = await copy(value);
            }
            catch (Exception)
            {
                worked = false;
            }

            Glyphs.Mark(
                button,
                worked ? Glyphs.Tick : Glyphs.Cross,
                worked ? ThemeManager.AccentKey : ThemeManager.DangerKey,
                $"Copy {value}",
                size: GlyphSize);

            var reset = new DispatcherTimer { Interval = Shown };

            reset.Tick += (_, _) =>
            {
                reset.Stop();
                Reset(button, value);
            };

            reset.Start();
        };

        return button;
    }

    private static void Reset(Button button, string value) =>
        Glyphs.Mark(button, Glyphs.Copy, ThemeManager.AccentKey, $"Copy {value}", size: GlyphSize);
}
