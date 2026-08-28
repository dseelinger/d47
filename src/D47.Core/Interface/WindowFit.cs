namespace D47.Core.Interface;

/// <summary>
/// A rectangle in device-independent pixels. Deliberately not Avalonia's <c>Rect</c>: this is
/// Core, and the arithmetic below is the part worth testing without a screen attached.
/// </summary>
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
/// Where the window opens, given how big the screen actually is (Phase 9, "Open at a
/// size that fits the screen").
/// <para>
/// The bug this exists for is a scaling bug wearing a sizing bug's clothes. Avalonia's
/// <c>Width</c> and <c>Height</c> are device-independent pixels and the monitor's scale factor
/// is applied on top, so the 820x640 default is 1230x960 real pixels at 150% — chosen once at
/// 100%, never checked against a work area that had been scaled underneath it. Clamping is the
/// fix that only has to be right once, because after the first session the remembered size is
/// what opens.
/// </para>
/// </summary>
public static class WindowFit
{
    /// <summary>
    /// How much of the working area the opening window may take. Not 100%: a window filling
    /// the work area exactly reads as maximised rather than as a window, and 1230x960 on a
    /// 1080p display is already 93% of the usable height — technically fitting and still the
    /// thing being complained about.
    /// </summary>
    public const double Fraction = 0.90;

    /// <summary>
    /// The size to open at. Both arguments are device-independent pixels, which is the whole
    /// point: the caller converts the screen's physical work area by its scale factor once,
    /// and everything after that is in the units the window is sized in.
    /// </summary>
    public static (double Width, double Height) Clamp(
        double desiredWidth,
        double desiredHeight,
        double workAreaWidth,
        double workAreaHeight)
    {
        // A zero-sized or unreported work area means something is wrong with the screen
        // enumeration, not with the window. Leaving the size alone is better than opening a
        // window with no area at all.
        if (workAreaWidth <= 0 || workAreaHeight <= 0)
        {
            return (desiredWidth, desiredHeight);
        }

        return (
            Math.Min(desiredWidth, workAreaWidth * Fraction),
            Math.Min(desiredHeight, workAreaHeight * Fraction));
    }

    /// <summary>
    /// How much of the working area a window with nothing remembered opens at.
    /// <para>
    /// Narrower than it is tall in proportion, because the panel is a column of text and a
    /// transcript wider than about ninety characters is harder to read rather than easier -
    /// the extra width buys nothing and costs the eye the return sweep.
    /// </para>
    /// </summary>
    public const double DefaultWidthFraction = 0.55;

    public const double DefaultHeightFraction = 0.75;

    /// <summary>
    /// Never narrower than this, whatever the screen says. A proportion of a small screen is
    /// still a window somebody has to read, and the panel has a header, an ask box and a send
    /// button that stop making sense side by side below roughly this.
    /// </summary>
    public const double MinimumWidth = 640;

    public const double MinimumHeight = 480;

    /// <summary>
    /// The size to open at when nothing is remembered, as a proportion of the work area rather
    /// than a number written down once.
    /// <para>
    /// A fixed default is a default chosen against one screen. 820x640 was picked at 100% on a
    /// 1080p display: on a 4K panel it opens as a postage stamp in the corner, and at 150%
    /// scaling it is 1230x960 real pixels, which is most of the usable height of the screen it
    /// was chosen on. Neither is a size anybody would choose for the screen it lands on, and
    /// both come from the same mistake - a length in a file where a proportion belongs.
    /// </para>
    /// <para>
    /// Still clamped by <see cref="Clamp"/> afterwards, which stays the guard for a remembered
    /// size arriving from a bigger screen than the one in front of the Commander now.
    /// </para>
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

    /// <summary>
    /// The position to restore a remembered window at, or null to let the platform centre it.
    /// <para>
    /// A remembered position is only honoured if enough of the window would land on a screen
    /// that exists now. Monitors get unplugged, and a window restored onto a display that is
    /// no longer there is a window the Commander cannot reach with the mouse — the failure is
    /// silent and looks exactly like the app not starting.
    /// </para>
    /// </summary>
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
