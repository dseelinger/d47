namespace D47.Core.Interface;

/// <summary>
/// How many pixels the big headset panel is rendered at (Phase 25, "The panel resizes and zooms").
/// </summary>
public static class PanelResolution
{
    /// <summary>
    /// What the panel has always been, and what it stays for a Commander who never touches this.
    /// </summary>
    public static readonly (int Width, int Height) Default = (1024, 640);

    /// <summary>The rungs, smallest first.</summary>
    public static readonly IReadOnlyList<(int Width, int Height)> Steps =
    [
        (800, 500),
        (1024, 640),
        (1280, 800),
        (1600, 1000),
        (1920, 1200),
        (2560, 1600),
    ];

    /// <summary>"1280x800" — how a settings file holds one and how a row labels it.</summary>
    public static string Describe((int Width, int Height) size) => $"{size.Width}x{size.Height}";

    /// <summary>Every rung, as the strings a choice row offers.</summary>
    public static IReadOnlyList<string> Choices => [.. Steps.Select(Describe)];

    /// <summary>The nearest rung to whatever a file happens to hold, by pixel count.</summary>
    public static (int Width, int Height) Snap(int width, int height)
    {
        var wanted = (long)Math.Max(0, width) * Math.Max(0, height);
        var nearest = Steps[0];

        foreach (var step in Steps)
        {
            var area = (long)step.Width * step.Height;

            if (Math.Abs(area - wanted) < Math.Abs((long)nearest.Width * nearest.Height - wanted))
            {
                nearest = step;
            }
        }

        return nearest;
    }

    /// <summary>
    /// The rung a "1280x800" names, or the default when it names nothing — which is what a hand-edited
    /// file, an older settings revision and an empty string all arrive as.
    /// </summary>
    public static (int Width, int Height) Parse(string? value)
    {
        if (value is null)
        {
            return Default;
        }

        var at = value.IndexOf('x', StringComparison.OrdinalIgnoreCase);

        if (at <= 0
            || !int.TryParse(value[..at], out var width)
            || !int.TryParse(value[(at + 1)..], out var height))
        {
            return Default;
        }

        return Snap(width, height);
    }

    /// <summary>What a mini panel presents at, which is not on this ladder and is not meant to be.</summary>
    public static readonly (int Width, int Height) Mini = (512, 280);
}
