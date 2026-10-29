using Avalonia;

namespace D47.App.Windowing;

/// <summary>One monitor as the overlay's placement sees it: physical-pixel rectangles and the scaling.</summary>
public readonly record struct OverlayScreen(PixelRect Bounds, PixelRect WorkingArea, double Scaling);

/// <summary>Where the overlay strip sits relative to the screens, as plain values.</summary>
public static class OverlayGeometry
{
    /// <summary>The strip's rectangle in physical pixels, from its position and its size in device-independent pixels.</summary>
    public static PixelRect StripRect(PixelPoint position, double width, double height, double scaling) =>
        new(position.X, position.Y, (int)Math.Round(width * scaling), (int)Math.Round(height * scaling));

    /// <summary>Whether the rectangle lies inside the bounds of a single screen.</summary>
    public static bool IsWhollyOnAScreen(PixelRect strip, IReadOnlyList<OverlayScreen> screens)
    {
        foreach (var screen in screens)
        {
            var bounds = screen.Bounds;

            if (strip.X >= bounds.X
                && strip.Y >= bounds.Y
                && strip.Right <= bounds.Right
                && strip.Bottom <= bounds.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The size and position that put the strip's middle at the middle of the screen's working area.
    /// A strip larger than the working area is reduced to fit it, keeping its proportions.
    /// </summary>
    public static (PixelPoint Position, double Width, double Height) Centre(
        double width, double height, OverlayScreen screen)
    {
        var area = screen.WorkingArea;
        var scaling = screen.Scaling;

        var fit = Math.Min(1.0, Math.Min(area.Width / (width * scaling), area.Height / (height * scaling)));

        width *= fit;
        height *= fit;

        var position = new PixelPoint(
            area.X + (int)Math.Round((area.Width - (width * scaling)) / 2),
            area.Y + (int)Math.Round((area.Height - (height * scaling)) / 2));

        return (position, width, height);
    }
}
