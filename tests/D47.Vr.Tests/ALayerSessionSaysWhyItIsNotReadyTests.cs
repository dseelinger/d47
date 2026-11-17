using D47.Core.Vr;
using D47.XrLayer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// The layer route starts only on a layer in an Elite session, with this build's layout, refusing nothing, and
/// drawing frames; otherwise its reason says which. A session ends when Elite's frames stop for a second.
/// </summary>
public class ALayerSessionSaysWhyItIsNotReadyTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 17, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WithNoBlockThereIsNoLayer()
    {
        using var block = new InMemoryXrBlock();
        var runtime = Runtime(block, new FakeTextureDevice());

        var start = runtime.Start();

        Assert.Equal(VrStartOutcome.NotReady, start.Outcome);
        Assert.Equal(OpenXrLayerRuntime.NoLayer, start.Detail);
        Assert.False(block.IsOpen);
    }

    [Fact]
    public void AnOlderLayoutAsksForElitesRestart()
    {
        using var block = new InMemoryXrBlock();
        var device = new FakeTextureDevice();
        var runtime = Runtime(block, device);
        block.Attach("VirtualDesktopXR", layoutVersion: XrBlock.CurrentLayoutVersion - 1);

        runtime.Start();
        block.Frame();
        var start = runtime.Start();

        Assert.Equal(VrStartOutcome.NotReady, start.Outcome);
        Assert.Equal("Elite loaded an older D47 layer. Restart Elite.", start.Detail);
        Assert.Empty(device.LuidOpens);
    }

    [Fact]
    public void ARefusalIsTheReason()
    {
        using var block = new InMemoryXrBlock();
        var runtime = Runtime(block, new FakeTextureDevice());
        block.Attach("VirtualDesktopXR");
        block.Refuse(XrRefusal.NotDirect3D11);

        runtime.Start();
        block.Frame();
        var start = runtime.Start();

        Assert.Equal(VrStartOutcome.NotReady, start.Outcome);
        Assert.Equal(OpenXrLayerRuntime.Describe(XrRefusal.NotDirect3D11), start.Detail);
    }

    [Fact]
    public void ADetachedLayerIsNoLayer()
    {
        using var block = new InMemoryXrBlock();
        var runtime = Runtime(block, new FakeTextureDevice());
        block.Attach("VirtualDesktopXR");
        block.Detach();

        Assert.Equal(OpenXrLayerRuntime.NoLayer, runtime.Start().Detail);
    }

    [Fact]
    public void AFrameCounterThatHasNotMovedSinceTheLastLookIsNoFrames()
    {
        using var block = new InMemoryXrBlock();
        var runtime = Runtime(block, new FakeTextureDevice());
        block.Attach("VirtualDesktopXR");

        Assert.Equal(OpenXrLayerRuntime.NoFrames, runtime.Start().Detail);
        Assert.Equal(OpenXrLayerRuntime.NoFrames, runtime.Start().Detail);

        block.Frame();
        Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
        runtime.Stop();
    }

    [Fact]
    public void AStalledFrameCounterEndsTheSession()
    {
        using var block = new InMemoryXrBlock();
        var runtime = TheLayerDrawsIntoTheSpareTextureTests.Started(block, new FakeTextureDevice(), new FakeSurface());

        try
        {
            Assert.True(runtime.Serve(Start));
            Assert.True(runtime.Serve(Start + TimeSpan.FromMilliseconds(900)));

            block.Frame();
            Assert.True(runtime.Serve(Start + TimeSpan.FromMilliseconds(1000)));
            Assert.True(runtime.Serve(Start + TimeSpan.FromMilliseconds(1900)));
            Assert.False(runtime.Serve(Start + TimeSpan.FromMilliseconds(2000)));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void ARefusalMidSessionEndsIt()
    {
        using var block = new InMemoryXrBlock();
        var runtime = TheLayerDrawsIntoTheSpareTextureTests.Started(block, new FakeTextureDevice(), new FakeSurface());

        try
        {
            Assert.True(runtime.Serve(Start));
            block.Refuse(XrRefusal.FrameRejected);
            block.Frame();
            Assert.False(runtime.Serve(Start + TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void AnAdapterThatCannotBeReachedFailsTheStartAndLetsTheBlockGo()
    {
        using var block = new InMemoryXrBlock();
        var device = new FakeTextureDevice { OpenRefused = "no adapter has that LUID" };
        var runtime = Runtime(block, device);
        block.Attach("VirtualDesktopXR");

        runtime.Start();
        block.Frame();
        var start = runtime.Start();

        Assert.Equal(VrStartOutcome.Failed, start.Outcome);
        Assert.Contains("no adapter has that LUID", start.Detail, StringComparison.Ordinal);
        Assert.False(block.IsOpen);
    }

    private static OpenXrLayerRuntime Runtime(InMemoryXrBlock block, FakeTextureDevice device) =>
        new([new FakeSurface()], NullLogger<OpenXrLayerRuntime>.Instance, block, device);
}
