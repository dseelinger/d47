using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>What happens to a frame the compositor turns down.</summary>
public class ARefusedFrameGoesAgainTests
{
    private static readonly FrameHeld Nothing = default;

    private static readonly FrameHeld Refused = new(Pending: true, Reported: true);

    /// <summary>The ordinary frame: something changed, so it is drawn and sent.</summary>
    [Fact]
    public void AChangedSurfaceIsDrawnAndSent()
    {
        var plan = FrameDelivery.Plan(Nothing, isDirty: true, isVisible: true);

        Assert.True(plan.Draw);
        Assert.True(plan.Submit);
    }

    /// <summary>The overwhelmingly common frame.</summary>
    [Fact]
    public void AnUnchangedSurfaceDoesNothing()
    {
        var plan = FrameDelivery.Plan(Nothing, isDirty: false, isVisible: true);

        Assert.False(plan.Draw);
        Assert.False(plan.Submit);
    }

    /// <summary>The defect, in one test.</summary>
    [Fact]
    public void ARefusedFrameIsSentAgainWithoutTheSurfaceAskingForIt()
    {
        var outcome = FrameDelivery.Took(Nothing, accepted: false);

        Assert.True(outcome.Held.Pending);

        var next = FrameDelivery.Plan(outcome.Held, isDirty: false, isVisible: true);

        Assert.True(next.Submit);
    }

    /// <summary>And it is not drawn again.</summary>
    [Fact]
    public void ARefusedFrameIsNotRasterisedAgain()
    {
        var next = FrameDelivery.Plan(Refused, isDirty: false, isVisible: true);

        Assert.False(next.Draw);
    }

    /// <summary>
    /// The ring does not move for a frame the runtime never took, or the retry would send a different
    /// buffer from the one that was drawn.
    /// </summary>
    [Fact]
    public void TheRingOnlyMovesForAFrameThatWasTaken()
    {
        Assert.False(FrameDelivery.Took(Nothing, accepted: false).Rotate);
        Assert.True(FrameDelivery.Took(Refused, accepted: true).Rotate);
    }

    /// <summary>
    /// A quad SteamVR is not drawing — the dashboard is up, the headset is in standby or off the
    /// Commander's head — refuses every frame, correctly.
    /// </summary>
    [Fact]
    public void AHeldFrameWaitsWhileTheQuadIsNotBeingDrawn()
    {
        var plan = FrameDelivery.Plan(Refused, isDirty: false, isVisible: false);

        Assert.False(plan.Submit);
        Assert.False(plan.Draw);
    }

    /// <summary>And it is still there when the dashboard closes.</summary>
    [Fact]
    public void AHeldFrameGoesTheMomentTheQuadIsDrawnAgain()
    {
        var waiting = FrameDelivery.Plan(Refused, isDirty: false, isVisible: false);
        Assert.False(waiting.Submit);

        var shown = FrameDelivery.Plan(Refused, isDirty: false, isVisible: true);
        Assert.True(shown.Submit);
    }

    /// <summary>Visibility throttles the retry, never the draw.</summary>
    [Fact]
    public void SomethingNewIsDrawnEvenWhileTheQuadIsHidden()
    {
        var plan = FrameDelivery.Plan(Refused, isDirty: true, isVisible: false);

        Assert.True(plan.Draw);
        Assert.True(plan.Submit);
    }

    /// <summary>Said once per run.</summary>
    [Fact]
    public void ARunOfRefusalsIsReportedOnce()
    {
        var first = FrameDelivery.Took(Nothing, accepted: false);
        Assert.True(first.Report);

        var second = FrameDelivery.Took(first.Held, accepted: false);
        Assert.False(second.Report);
    }

    /// <summary>And the recovery is said too.</summary>
    [Fact]
    public void GoingThroughAgainIsWorthALine()
    {
        var refused = FrameDelivery.Took(Nothing, accepted: false);

        var recovered = FrameDelivery.Took(refused.Held, accepted: true);

        Assert.True(recovered.Recovered);
        Assert.False(recovered.Held.Pending);
        Assert.False(recovered.Held.Reported);
    }

    /// <summary>An ordinary frame does not announce that it worked.</summary>
    [Fact]
    public void AnOrdinaryFrameSaysNothing()
    {
        var outcome = FrameDelivery.Took(Nothing, accepted: true);

        Assert.False(outcome.Recovered);
        Assert.False(outcome.Report);
    }

    /// <summary>A second run, after a recovery, is its own event.</summary>
    [Fact]
    public void ASecondRunIsReportedAgain()
    {
        var first = FrameDelivery.Took(Nothing, accepted: false);
        var recovered = FrameDelivery.Took(first.Held, accepted: true);

        var again = FrameDelivery.Took(recovered.Held, accepted: false);

        Assert.True(again.Report);
    }
}
