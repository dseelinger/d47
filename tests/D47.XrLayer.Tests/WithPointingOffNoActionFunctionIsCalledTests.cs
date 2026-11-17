using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public sealed class WithPointingOffNoActionFunctionIsCalledTests : IDisposable
{
    private readonly LayerRig rig = new();

    [Fact]
    public void ElitesAttachAndSyncsPassThroughUnchanged()
    {
        rig.EliteSuggests((0xE1A, "/user/hand/right/input/a/click"));
        rig.EliteAttaches(ControllerCalls.ElitesSet);
        rig.Focus();
        for (var frame = 0; frame < 3; frame++)
        {
            rig.EliteSyncs(ControllerCalls.ElitesSet);
            rig.EndFrame(frame * LayerRig.Millisecond);
        }

        Assert.Equal(
            [FakeNext.EliteAttachCall, FakeNext.EliteSyncCall, FakeNext.EliteSyncCall, FakeNext.EliteSyncCall],
            rig.Next.ActionCalls);
        Assert.Empty(rig.Next.Suggestions);
        Assert.Equal(0u, rig.Published.Hands[0].Valid);
        Assert.Equal(XrControllerRefusal.None, rig.Published.ControllerRefusal);
    }

    [Fact]
    public void AFocusedSessionElitesNeverAttachesGetsNoAttach()
    {
        rig.Focus();
        rig.Run();

        Assert.Empty(rig.Next.ActionCalls);
    }

    [Fact]
    public void TurnedOnAfterEliteAttachedItIsRefusedAndTheControllersStayUnread()
    {
        rig.EliteAttaches(ControllerCalls.ElitesSet);
        rig.Focus();
        rig.Point(true);

        rig.EliteSyncs(ControllerCalls.ElitesSet);
        rig.EndFrame(0);

        Assert.Equal([FakeNext.EliteAttachCall, FakeNext.EliteSyncCall], rig.Next.ActionCalls);
        Assert.Equal(XrControllerRefusal.PointingTurnedOnAfterEliteAttached, rig.Published.ControllerRefusal);
        Assert.Equal(0u, rig.Published.Hands[0].Valid);
    }

    [Fact]
    public void TurnedOffAfterTheLayerAttachedStopsItsSync()
    {
        rig.Point(true);
        rig.Focus();
        rig.EndFrame(0);
        rig.Point(false);
        rig.Next.Calls.Clear();

        rig.EndFrame(LayerRig.Millisecond);

        Assert.Empty(rig.Next.ActionCalls);
        Assert.Equal(0u, rig.Published.Hands[0].Valid);
        Assert.Equal(0u, rig.Published.Hands[1].Valid);
    }

    public void Dispose() => rig.Dispose();
}
