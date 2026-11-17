using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public sealed class ASessionThatIsNotDirect3D11IsRefusedTests : IDisposable
{
    private readonly LayerRig rig = new(XrType.GraphicsBindingVulkan);

    [Fact]
    public void TheRefusalIsPublishedAndNothingIsAdded()
    {
        rig.Show(0);
        rig.Run();

        var frame = rig.EndFrame(2 * LayerRig.Millisecond).Calls.Single();

        Assert.Equal(XrRefusal.NotDirect3D11, rig.Published.Refusal);
        Assert.Equal(1u, rig.Published.Attached);
        Assert.True(frame.IsElitesOwn);
        Assert.Equal([rig.EliteLayer], frame.Layers);
        Assert.Equal([nameof(FakeNext.GetInstanceProperties)], rig.Next.Calls);
        Assert.Empty(rig.Device.Opened);
    }

    [Fact]
    public void ARuntimeWithNoBgraSwapchainFormatIsRefused()
    {
        using var other = new LayerRig(swapchainFormats: [29, 28]);

        other.Show(0);
        other.Run();
        var frame = other.EndFrame(2 * LayerRig.Millisecond).Calls.Single();

        Assert.Equal(XrRefusal.NoSwapchainFormat, other.Published.Refusal);
        Assert.True(frame.IsElitesOwn);
    }

    public void Dispose() => rig.Dispose();
}
