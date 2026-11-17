using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public sealed class ASyncThatIsNotFocusedPublishesNoValidHandTests : IDisposable
{
    private readonly LayerRig rig = new();

    [Fact]
    public void TheLayersOwnSyncNotFocusedReadsNoHand()
    {
        rig.Point(true);
        rig.Focus();
        rig.EndFrame(0);
        rig.Next.SyncResult = XrResult.SessionNotFocused;
        rig.Next.Calls.Clear();

        rig.EndFrame(LayerRig.Millisecond);

        Assert.Equal([nameof(FakeNext.SyncActions)], rig.Next.ActionCalls);
        Assert.Equal(0u, rig.Published.Hands[0].Valid);
        Assert.Equal(0u, rig.Published.Hands[1].Valid);
    }

    [Fact]
    public void ElitesSyncNotFocusedReadsNoHandUntilAFocusedOne()
    {
        rig.Point(true);
        rig.EliteAttaches(ControllerCalls.ElitesSet);
        rig.Next.SyncResult = XrResult.SessionNotFocused;

        rig.EliteSyncs(ControllerCalls.ElitesSet);
        rig.EndFrame(0);

        Assert.Equal(0u, rig.Published.Hands[0].Valid);
        Assert.Equal(0u, rig.Published.Hands[1].Valid);

        rig.Next.SyncResult = XrResult.Success;
        rig.EliteSyncs(ControllerCalls.ElitesSet);
        rig.EndFrame(LayerRig.Millisecond);

        Assert.Equal(1u, rig.Published.Hands[0].Valid);
        Assert.Equal(1u, rig.Published.Hands[1].Valid);
    }

    public void Dispose() => rig.Dispose();
}
