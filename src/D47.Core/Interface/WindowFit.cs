namespace D47.Core.Interface;

/// <summary>A rectangle in device-independent pixels.</summary>
public readonly record struct FitRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    /// <summary>Whether enough of this rectangle overlaps <paramref name="area"/> to be grabbable.</summary>
    public bool IsUsablyWithin(FitRect area, double minimumVisible = 80)
    {
        var overlapX = Math.Min(Right, area.Right) - Math.Max(X, area.X);
        var overlapY = Math.Min(Bottom, area.Bottom) - Math.Max(Y, area.Y);

        return overlapX >= minimumVisible && overlapY >= minimumVisible;
    }
}

/// <summary>
/// Where the window opens, given how big the screen actually is (Phase 9, "Open at a size that fits the
/// screen").
/// </summary>
public static class WindowFit
{
    /// <summary>How much of the working area the opening window may take.</summary>
    public const double Fraction = 0.90;

    /// <summary>The size to open at.</summary>
    public static (double Width, double Height) Clamp(
        double desiredWidth,
        double desiredHeight,
        double workAreaWidth,
        double workAreaHeight)
    {
        // A zero-sized or unreported work area means something is wrong with the screen enumeration, not with
        // the window.
        if (workAreaWidth <= 0 || workAreaHeight <= 0)
        {
            return (desiredWidth, desiredHeight);
        }

        return (
            Math.Min(desiredWidth, workAreaWidth * Fraction),
            Math.Min(desiredHeight, workAreaHeight * Fraction));
    }

    /// <summary>How much of the working area a window with nothing remembered opens at.</summary>
    public const double DefaultWidthFraction = 0.55;

    public const double DefaultHeightFraction = 0.75;

    /// <summary>Never narrower than this, whatever the screen says.</summary>
    public const double MinimumWidth = 640;

    public const double MinimumHeight = 480;

    /// <summary>
    /// The size to open at when nothing is remembered, as a proportion of the work area rather than a
    /// number written down once.
    /// </summary>
    public static (double Width, double Height) Opening(double workAreaWidth, double workAreaHeight)
    {
        if (workAreaWidth <= 0 || workAreaHeight <= 0)
        {
            return (MinimumWidth, MinimumHeight);
        }

        return (
            Math.Min(Math.Max(workAreaWidth * DefaultWidthFraction, MinimumWidth), workAreaWidth * Fraction),
            Math.Min(Math.Max(workAreaHeight * DefaultHeightFraction, MinimumHeight), workAreaHeight * Fraction));
    }

    /// <summary>The position to restore a remembered window at, or null to let the platform centre it.</summary>
    public static FitRect? Reposition(FitRect remembered, IReadOnlyList<FitRect> screens)
    {
        foreach (var screen in screens)
        {
            if (remembered.IsUsablyWithin(screen))
            {
                return remembered;
            }
        }

        return null;
    }
}
