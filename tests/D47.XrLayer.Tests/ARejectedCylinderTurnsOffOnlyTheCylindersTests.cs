using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

/// <summary>
/// A frame the runtime rejects while it carries a cylinder is resubmitted as Elite's own, cylinders are off for the
/// rest of the session and the block says so, and quads carry on from the next frame.
/// </summary>
public sealed class ARejectedCylinderTurnsOffOnlyTheCylindersTests : IDisposable
{
    private readonly LayerRig rig = new(cylinders: true);

    [Fact]
    public void TheFrameGoesAgainAsElitesOwnAndLaterFramesCarryQuads()
    {
        rig.Show(0);
        rig.Slot(0).Curvature = 0.5f;
        rig.Show(2);
        rig.Publish();
        rig.Run();
        rig.Next.RejectCylinders = true;

        var (result, calls) = rig.EndFrame(2 * LayerRig.Millisecond);

        Assert.Equal(XrResult.Success, result);
        Assert.Equal(2, calls.Count);
        Assert.Single(calls[0].Cylinders);
        Assert.True(calls[1].IsElitesOwn);
        Assert.Equal([rig.EliteLayer], calls[1].Layers);
        Assert.Equal(0u, rig.Published.Cylinders);
        Assert.Equal(XrRefusal.None, rig.Published.Refusal);

        // d47 has not read the field yet and still writes a curvature.
        rig.Beat();
        var later = rig.EndFrame(3 * LayerRig.Millisecond).Calls.Single();

        Assert.False(later.IsElitesOwn);
        Assert.Empty(later.Cylinders);
        Assert.Equal([0f, 2f], later.Quads.Select(q => q.Pose.PositionX));
        Assert.Equal(0u, rig.Published.Cylinders);
    }

    [Fact]
    public void AQuadOnlyFrameRejectedAfterwardsStillTurnsDrawingOff()
    {
        rig.Show(0);
        rig.Slot(0).Curvature = 0.5f;
        rig.Publish();
        rig.Run();
        rig.Next.RejectCylinders = true;
        rig.EndFrame(2 * LayerRig.Millisecond);

        rig.Next.RejectQuads = true;
        rig.Beat();
        var calls = rig.EndFrame(3 * LayerRig.Millisecond).Calls;

        Assert.Equal(2, calls.Count);
        Assert.True(calls[1].IsElitesOwn);
        Assert.Equal(XrRefusal.FrameRejected, rig.Published.Refusal);
    }

    public void Dispose() => rig.Dispose();
}
