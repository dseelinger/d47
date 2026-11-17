using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public sealed class ARejectedFrameIsResubmittedWithoutQuadsTests : IDisposable
{
    private readonly LayerRig rig = new();

    [Fact]
    public void TheRejectedFrameGoesAgainAsElitesOwnAndLaterFramesCarryNone()
    {
        rig.Show(0);
        rig.Show(1);
        rig.Run();
        rig.Next.RejectQuads = true;

        var (result, calls) = rig.EndFrame(2 * LayerRig.Millisecond);

        Assert.Equal(XrResult.Success, result);
        Assert.Equal(2, calls.Count);
        Assert.Equal(2, calls[0].Quads.Count);
        Assert.True(calls[1].IsElitesOwn);
        Assert.Equal([rig.EliteLayer], calls[1].Layers);
        Assert.Equal(XrRefusal.FrameRejected, rig.Published.Refusal);

        rig.Next.RejectQuads = false;
        rig.Beat();
        var later = rig.EndFrame(3 * LayerRig.Millisecond).Calls.Single();

        Assert.True(later.IsElitesOwn);
        Assert.Empty(later.Quads);
        Assert.Equal(0, rig.Device.OpenCount);
    }

    [Fact]
    public void AManagedExceptionPassesTheFrameThroughAndLaterFramesCarryNone()
    {
        rig.Show(0);
        rig.Device.ThrowOnCopy = true;
        rig.Beat();
        rig.EndFrame(0);
        rig.Beat();

        var (result, calls) = rig.EndFrame(LayerRig.Millisecond);

        Assert.Equal(XrResult.Success, result);
        Assert.True(Assert.Single(calls).IsElitesOwn);
        Assert.Equal(XrRefusal.Faulted, rig.Published.Refusal);

        rig.Device.ThrowOnCopy = false;
        rig.Beat();
        var later = rig.EndFrame(2 * LayerRig.Millisecond).Calls.Single();

        Assert.True(later.IsElitesOwn);
        Assert.Empty(rig.Device.Copies);
    }

    public void Dispose() => rig.Dispose();
}
