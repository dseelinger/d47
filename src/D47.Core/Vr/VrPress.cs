namespace D47.Core.Vr;

/// <summary>
/// Whether a held trigger is still a press or has become a carry (remediation.md, "Controls should be
/// clickable in the VR panels").
/// </summary>
public static class VrPress
{
    /// <summary>How long the trigger may be held before it is a carry however still the hand is.</summary>
    public static readonly TimeSpan Dwell = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// How far the aim may wander across the panel first, as a fraction of its width and height.
    /// </summary>
    public const float Slop = 0.05f;

    /// <summary>
    /// Whether a press that started at <paramref name="fromU"/>, <paramref name="fromV"/> and is now at
    /// <paramref name="toU"/>, <paramref name="toV"/> after <paramref name="held"/> has stopped being a
    /// press.
    /// </summary>
    public static bool BecomesACarry(TimeSpan held, float fromU, float fromV, float toU, float toV)
    {
        if (held >= Dwell)
        {
            return true;
        }

        var across = toU - fromU;
        var down = toV - fromV;

        return (across * across) + (down * down) > Slop * Slop;
    }
}
