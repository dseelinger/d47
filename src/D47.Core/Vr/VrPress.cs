namespace D47.Core.Vr;

/// <summary>
/// Whether a held trigger is still a press or has become a carry
/// (remediation.md, "Controls should be clickable in the VR panels").
/// <para>
/// <b>One button does both, and that is deliberate.</b> The trigger is the button a Commander
/// already has a finger on, and it is the only action in d47's manifest — adding a second would
/// change the manifest under an application key SteamVR caches, throwing away any binding the
/// Commander had made. So the gesture carries the distinction instead, which is the same one a
/// touch screen makes: tap to press, hold or drag to move.
/// </para>
/// <para>
/// Pure arithmetic with the clock passed in, like everything else in Core, so a whole vocabulary
/// of gestures can be asserted without a headset.
/// </para>
/// </summary>
public static class VrPress
{
    /// <summary>
    /// How long the trigger may be held before it is a carry however still the hand is.
    /// <para>
    /// Generous rather than tight. The cost of being too short is that a deliberate press moves
    /// the panel, which is the failure this exists to end; the cost of being too long is a
    /// carry that starts a beat late, which the Commander sees happening and simply keeps
    /// holding through.
    /// </para>
    /// </summary>
    public static readonly TimeSpan Dwell = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// How far the aim may wander across the panel first, as a fraction of its width and height.
    /// <para>
    /// A twentieth. Nobody holds a controller still, and at arm's length a hand that is trying
    /// not to move still travels a few millimetres — but the intent to carry shows up as travel
    /// long before it reaches a twentieth of the panel.
    /// </para>
    /// </summary>
    public const float Slop = 0.05f;

    /// <summary>
    /// Whether a press that started at <paramref name="fromU"/>, <paramref name="fromV"/> and is
    /// now at <paramref name="toU"/>, <paramref name="toV"/> after <paramref name="held"/> has
    /// stopped being a press.
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
