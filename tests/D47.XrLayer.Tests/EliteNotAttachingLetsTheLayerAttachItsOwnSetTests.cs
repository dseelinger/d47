using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public sealed class EliteNotAttachingLetsTheLayerAttachItsOwnSetTests : IDisposable
{
    private readonly LayerRig rig = new();

    [Fact]
    public void NothingIsAttachedBeforeTheSessionIsFocused()
    {
        rig.Point(true);
        rig.Run();

        Assert.Empty(rig.Next.ActionCalls);
        Assert.Equal(0u, rig.Published.Hands[0].Valid);
    }

    [Fact]
    public void TheFirstFocusedFrameAttachesTheLayersSetThenEachFrameSyncsItOnce()
    {
        rig.Point(true);
        rig.EndFrame(0);
        rig.Focus();

        rig.EndFrame(LayerRig.Millisecond);

        Assert.Equal(
            [
                .. ControllerCalls.Prepare, nameof(FakeNext.AttachSessionActionSets), .. ControllerCalls.Spaces,
                nameof(FakeNext.SyncActions), .. ControllerCalls.Reads,
            ],
            rig.Next.ActionCalls);
        var attach = Assert.Single(rig.Next.Attaches);
        Assert.False(attach.IsElitesOwn);
        Assert.Equal([FakeNext.LayerSet], attach.Sets);

        rig.Next.Calls.Clear();
        rig.EndFrame(2 * LayerRig.Millisecond);

        Assert.Equal([nameof(FakeNext.SyncActions), .. ControllerCalls.Reads], rig.Next.ActionCalls);
        Assert.All(rig.Next.Syncs, sync => Assert.Equal([FakeNext.LayerSet], sync.Sets));
        Assert.Equal(1u, rig.Published.Hands[0].Valid);
        Assert.Equal(1u, rig.Published.Hands[1].Valid);
    }

    [Fact]
    public void ElitesOwnSyncsPassThroughUnchanged()
    {
        rig.Point(true);
        rig.Focus();
        rig.EndFrame(0);
        rig.Next.Syncs.Clear();

        rig.EliteSyncs(ControllerCalls.ElitesSet);

        var sync = Assert.Single(rig.Next.Syncs);
        Assert.True(sync.IsElitesOwn);
        Assert.Equal([ControllerCalls.ElitesSet], sync.Sets);
    }

    public void Dispose() => rig.Dispose();
}
