using D47.Core.Vr;
using Xunit;

namespace D47.Core.Tests.Vr;

/// <summary>
/// What happens to a frame the compositor turns down (remediation.md 16, item 1).
/// <para>
/// It used to be nothing. <c>Serve</c> submitted only when the surface said it was dirty, the
/// draw cleared that flag before the submit, and the submit's answer was thrown away — so a
/// refusal lost the frame for good and the headset held a stale panel with no line in the log
/// saying why.
/// </para>
/// <para>
/// This is the whole decision, and it lives in Core for the reason
/// <see cref="RuntimeReadback"/> does: inside <c>SteamVrRuntime</c> it is three booleans
/// threaded between an overlay call and a compositor call, in the one part of d47 no test can
/// reach without a headset (architecture.md §8).
/// </para>
/// </summary>
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

    /// <summary>
    /// The overwhelmingly common frame. A surface with nothing new costs one boolean and no
    /// call into the runtime at all.
    /// </summary>
    [Fact]
    public void AnUnchangedSurfaceDoesNothing()
    {
        var plan = FrameDelivery.Plan(Nothing, isDirty: false, isVisible: true);

        Assert.False(plan.Draw);
        Assert.False(plan.Submit);
    }

    /// <summary>
    /// The defect, in one test. A refusal holds the frame, and the next pass sends it again
    /// without the surface having to say anything — which it cannot, its dirty flag having been
    /// cleared by the draw that produced the frame.
    /// </summary>
    [Fact]
    public void ARefusedFrameIsSentAgainWithoutTheSurfaceAskingForIt()
    {
        var outcome = FrameDelivery.Took(Nothing, accepted: false);

        Assert.True(outcome.Held.Pending);

        var next = FrameDelivery.Plan(outcome.Held, isDirty: false, isVisible: true);

        Assert.True(next.Submit);
    }

    /// <summary>
    /// <b>And it is not drawn again.</b> What the compositor refused was the upload, not the
    /// picture: the pixels are still in the buffer and are still the ones wanted. Re-rasterising
    /// an Avalonia tree to retry a call that costs microseconds would pay the whole price of the
    /// frame for the cheapest part of it.
    /// </summary>
    [Fact]
    public void ARefusedFrameIsNotRasterisedAgain()
    {
        var next = FrameDelivery.Plan(Refused, isDirty: false, isVisible: true);

        Assert.False(next.Draw);
    }

    /// <summary>
    /// The ring does not move for a frame the runtime never took, or the retry would send a
    /// different buffer from the one that was drawn.
    /// </summary>
    [Fact]
    public void TheRingOnlyMovesForAFrameThatWasTaken()
    {
        Assert.False(FrameDelivery.Took(Nothing, accepted: false).Rotate);
        Assert.True(FrameDelivery.Took(Refused, accepted: true).Rotate);
    }

    /// <summary>
    /// A quad SteamVR is not drawing — the dashboard is up, the headset is in standby or off the
    /// Commander's head — refuses every frame, correctly. Retrying through that would re-send a
    /// panel-sized buffer at every tick for as long as the dashboard is open, which is nine
    /// megabytes a submit copied inside OpenVR for a picture nobody can see.
    /// </summary>
    [Fact]
    public void AHeldFrameWaitsWhileTheQuadIsNotBeingDrawn()
    {
        var plan = FrameDelivery.Plan(Refused, isDirty: false, isVisible: false);

        Assert.False(plan.Submit);
        Assert.False(plan.Draw);
    }

    /// <summary>
    /// And it is still there when the dashboard closes. Waiting is not forgetting.
    /// </summary>
    [Fact]
    public void AHeldFrameGoesTheMomentTheQuadIsDrawnAgain()
    {
        var waiting = FrameDelivery.Plan(Refused, isDirty: false, isVisible: false);
        Assert.False(waiting.Submit);

        var shown = FrameDelivery.Plan(Refused, isDirty: false, isVisible: true);
        Assert.True(shown.Submit);
    }

    /// <summary>
    /// <b>Visibility throttles the retry, never the draw.</b> A surface that changed while the
    /// dashboard was up has to be current the moment the dashboard closes, and the only way to be
    /// sure of that is to have drawn it.
    /// </summary>
    [Fact]
    public void SomethingNewIsDrawnEvenWhileTheQuadIsHidden()
    {
        var plan = FrameDelivery.Plan(Refused, isDirty: true, isVisible: false);

        Assert.True(plan.Draw);
        Assert.True(plan.Submit);
    }

    /// <summary>
    /// Said once per run. A stalled compositor refuses ten times a second and the log is the only
    /// place any of this can be seen — an unfiltered one would be the same line thousands of
    /// times, unreadable exactly when it is wanted.
    /// </summary>
    [Fact]
    public void ARunOfRefusalsIsReportedOnce()
    {
        var first = FrameDelivery.Took(Nothing, accepted: false);
        Assert.True(first.Report);

        var second = FrameDelivery.Took(first.Held, accepted: false);
        Assert.False(second.Report);
    }

    /// <summary>
    /// <b>And the recovery is said too.</b> Without it a log can report that frames were refused
    /// and never say whether that lasted a second or the rest of the session.
    /// </summary>
    [Fact]
    public void GoingThroughAgainIsWorthALine()
    {
        var refused = FrameDelivery.Took(Nothing, accepted: false);

        var recovered = FrameDelivery.Took(refused.Held, accepted: true);

        Assert.True(recovered.Recovered);
        Assert.False(recovered.Held.Pending);
        Assert.False(recovered.Held.Reported);
    }

    /// <summary>
    /// An ordinary frame does not announce that it worked. Only one that follows a refusal
    /// somebody was told about.
    /// </summary>
    [Fact]
    public void AnOrdinaryFrameSaysNothing()
    {
        var outcome = FrameDelivery.Took(Nothing, accepted: true);

        Assert.False(outcome.Recovered);
        Assert.False(outcome.Report);
    }

    /// <summary>
    /// A second run, after a recovery, is its own event. The first version of the complaint filter
    /// was add-only for the life of the process, so a stall an hour later left no trace at all.
    /// </summary>
    [Fact]
    public void ASecondRunIsReportedAgain()
    {
        var first = FrameDelivery.Took(Nothing, accepted: false);
        var recovered = FrameDelivery.Took(first.Held, accepted: true);

        var again = FrameDelivery.Took(recovered.Held, accepted: false);

        Assert.True(again.Report);
    }
}
