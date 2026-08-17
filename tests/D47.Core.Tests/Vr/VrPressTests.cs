using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// One trigger doing two jobs (remediation.md, "Controls should be clickable in the VR panels").
/// <para>
/// The panel had one gesture and it started the moment the trigger went down, so every attempt
/// to press a control moved the whole quad instead. A second action was not the answer: the
/// manifest lives under an application key SteamVR caches, and changing it throws away whatever
/// binding the Commander had made. The gesture carries the distinction, as it does on every
/// touch surface ever shipped.
/// </para>
/// </summary>
public class VrPressTests
{
    private static readonly TimeSpan Instant = TimeSpan.FromMilliseconds(16);

    [Fact]
    public void AStillHandStraightAfterThePressIsStillAPress() =>
        Assert.False(VrPress.BecomesACarry(Instant, 0.5f, 0.5f, 0.5f, 0.5f));

    /// <summary>
    /// Nobody holds a controller still. A hand that is trying not to move still travels, and
    /// that must not be read as the start of a carry.
    /// </summary>
    [Fact]
    public void AHandThatMerelyWobblesIsStillAPress() =>
        Assert.False(VrPress.BecomesACarry(Instant, 0.5f, 0.5f, 0.51f, 0.505f));

    [Fact]
    public void AHandThatTravelsIsACarry() =>
        Assert.True(VrPress.BecomesACarry(Instant, 0.5f, 0.5f, 0.7f, 0.5f));

    /// <summary>Diagonally too, which is what a squared distance is for.</summary>
    [Fact]
    public void TravelIsMeasuredInBothDirectionsAtOnce()
    {
        Assert.False(VrPress.BecomesACarry(Instant, 0.5f, 0.5f, 0.53f, 0.5f));
        Assert.False(VrPress.BecomesACarry(Instant, 0.5f, 0.5f, 0.5f, 0.53f));

        // Neither on its own is past the slop; together they are.
        Assert.True(VrPress.BecomesACarry(Instant, 0.5f, 0.5f, 0.54f, 0.54f));
    }

    /// <summary>
    /// Held long enough and it is a carry however still the hand is, which is what makes picking
    /// the panel up off a control possible at all.
    /// </summary>
    [Fact]
    public void AHeldTriggerBecomesACarryWithoutMoving() =>
        Assert.True(VrPress.BecomesACarry(VrPress.Dwell, 0.5f, 0.5f, 0.5f, 0.5f));

    /// <summary>
    /// And the dwell is long enough to press something deliberately. A press is a couple of
    /// hundred milliseconds; anything under that would make a careful press into a carry.
    /// </summary>
    [Fact]
    public void TheDwellLeavesRoomForADeliberatePress() =>
        Assert.True(VrPress.Dwell >= TimeSpan.FromMilliseconds(300));
}
