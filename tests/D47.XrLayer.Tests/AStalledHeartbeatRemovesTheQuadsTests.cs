using Xunit;

namespace D47.XrLayer.Tests;

public sealed class AStalledHeartbeatRemovesTheQuadsTests : IDisposable
{
    private readonly LayerRig rig = new();

    [Fact]
    public void QuadsStayForUnderASecondOfDisplayTimeThenGoAndTheirTexturesAreReleased()
    {
        rig.Show(0);
        rig.Run();

        Assert.Single(rig.EndFrame(900 * LayerRig.Millisecond).Calls.Single().Quads);

        var stalled = rig.EndFrame(1001 * LayerRig.Millisecond).Calls.Single();

        Assert.True(stalled.IsElitesOwn);
        Assert.Empty(stalled.Quads);
        Assert.Equal(["d47.slot0.a"], rig.Device.Released);
        Assert.Equal(0, rig.Device.OpenCount);
    }

    [Fact]
    public void AHeartbeatThatMovesAgainBringsThemBack()
    {
        rig.Show(0);
        rig.Run();
        rig.EndFrame(1001 * LayerRig.Millisecond);

        rig.Beat();
        var frame = rig.EndFrame(1002 * LayerRig.Millisecond).Calls.Single();

        Assert.Single(frame.Quads);
        Assert.Equal(2, rig.Device.Copies.Count);
    }

    [Fact]
    public void AHeartbeatAlreadyInTheBlockIsNotTakenAsD47Running()
    {
        rig.Show(0);
        rig.Beat();
        rig.Beat();

        var frame = rig.EndFrame(0).Calls.Single();

        Assert.Empty(frame.Quads);
        Assert.Empty(rig.Device.Opened);
    }

    public void Dispose() => rig.Dispose();
}
