using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public sealed class EliteAttachingAfterTheLayerGetsTheRuntimesErrorTests : IDisposable
{
    private readonly LayerRig rig = new();

    [Fact]
    public void ElitesAttachReachesTheRuntimeUnchangedAndARefusalIsPublished()
    {
        rig.Point(true);
        rig.Focus();
        rig.EndFrame(0);
        rig.Next.Calls.Clear();

        var result = rig.EliteAttaches(ControllerCalls.ElitesSet);

        Assert.Equal(XrResult.ErrorActionSetsAlreadyAttached, result);
        Assert.Equal([FakeNext.EliteAttachCall], rig.Next.ActionCalls);
        Assert.Equal([ControllerCalls.ElitesSet], rig.Next.Attaches[^1].Sets);

        rig.EndFrame(LayerRig.Millisecond);

        Assert.Equal(XrControllerRefusal.EliteAttachedAfterLayer, rig.Published.ControllerRefusal);
        Assert.Equal(XrRefusal.None, rig.Published.Refusal);
    }

    public void Dispose() => rig.Dispose();
}
