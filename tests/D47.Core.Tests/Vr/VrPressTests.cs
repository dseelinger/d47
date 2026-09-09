using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// One trigger doing two jobs.
/// </summary>
public class VrPressTests
{
    private static readonly TimeSpan Instant = TimeSpan.FromMilliseconds(16);

    [Fact]
    public void AStillHandStraightAfterThePressIsStillAPress() =>
        Assert.False(VrPress.BecomesACarry(Instant, 0.5f, 0.5f, 0.5f, 0.5f));

    /// <summary>Nobody holds a controller still.</summary>
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
    /// Held long enough and it is a carry however still the hand is, which is what makes picking the
    /// panel up off a control possible at all.
    /// </summary>
    [Fact]
    public void AHeldTriggerBecomesACarryWithoutMoving() =>
        Assert.True(VrPress.BecomesACarry(VrPress.Dwell, 0.5f, 0.5f, 0.5f, 0.5f));

    /// <summary>And the dwell is long enough to press something deliberately.</summary>
    [Fact]
    public void TheDwellLeavesRoomForADeliberatePress() =>
        Assert.True(VrPress.Dwell >= TimeSpan.FromMilliseconds(300));
}
