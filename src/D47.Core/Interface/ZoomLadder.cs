namespace D47.Core.Interface;

/// <summary>
/// The zoom levels the panel steps through, and the arithmetic for moving between them
/// (list.md Phase 9, "Zoom the desktop window").
/// <para>
/// The ladder is Chrome's, deliberately. The whole argument for this feature is that every
/// browser has already taught the Commander the four gestures, and a control that answers a
/// familiar gesture with unfamiliar steps is only half-borrowed — 110% between 100% and 125%
/// looks arbitrary written down and is exactly what a Commander's hand expects.
/// </para>
/// <para>
/// Pure and in Core because it is arithmetic over a list of integers: no window, no
/// dispatcher, and a test can walk the whole ladder in both directions.
/// </para>
/// </summary>
public static class ZoomLadder
{
    /// <summary>Unzoomed. What Ctrl+0 returns to.</summary>
    public const int Default = 100;

    public static readonly IReadOnlyList<int> Steps =
        [50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200, 250, 300];

    public static int Minimum => Steps[0];

    public static int Maximum => Steps[^1];

    /// <summary>
    /// The nearest rung to an arbitrary number. Settings are a hand-editable file, so 137 is a
    /// value this has to answer for; snapping beats rejecting, because a rejected zoom level
    /// presents as the setting silently not taking.
    /// </summary>
    public static int Snap(int percent)
    {
        var nearest = Steps[0];

        foreach (var step in Steps)
        {
            if (Math.Abs(step - percent) < Math.Abs(nearest - percent))
            {
                nearest = step;
            }
        }

        return nearest;
    }

    /// <summary>One rung larger, or the top if there is nothing above.</summary>
    public static int In(int percent) => Next(percent, forward: true);

    /// <summary>One rung smaller, or the bottom if there is nothing below.</summary>
    public static int Out(int percent) => Next(percent, forward: false);

    /// <summary>The multiplier a scale transform wants.</summary>
    public static double ScaleOf(int percent) => percent / 100.0;

    /// <summary>"125%", for a settings row and for the panel to say what it is at.</summary>
    public static string Describe(int percent) => $"{percent}%";

    private static int Next(int percent, bool forward)
    {
        var snapped = Snap(percent);
        var index = Steps.ToList().IndexOf(snapped);

        // A value between rungs snaps towards the direction of travel rather than jumping past
        // it: at 137, one step out is 125, not 110.
        if (snapped != percent)
        {
            var towards = forward ? snapped > percent : snapped < percent;
            if (towards)
            {
                return snapped;
            }
        }

        var next = index + (forward ? 1 : -1);
        return next < 0 || next >= Steps.Count ? snapped : Steps[next];
    }
}
