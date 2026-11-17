using D47.Core.Vr;
using D47.XrLayer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// Through the OpenXR layer, a changed surface is drawn into the texture the layer was not last given, flushed,
/// and only then published under a new generation; an unchanged one is left alone.
/// </summary>
public class TheLayerDrawsIntoTheSpareTextureTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 17, 20, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Tick(int n) => Start + TimeSpan.FromMilliseconds(100 * n);

    [Fact]
    public void ADirtySurfaceIsDrawnIntoTheSpareAndPublishedAfterTheFlush()
    {
        using var block = new InMemoryXrBlock();
        var device = new FakeTextureDevice();
        var surface = new FakeSurface();
        var runtime = Started(block, device, surface);

        try
        {
            var published = new List<ulong>();
            device.Flushed = () => published.Add(block.D47Half.Slots[OpenXrLayerRuntime.PanelSlot].TextureGeneration);

            Assert.True(runtime.Serve(Tick(0)));
            var first = block.D47Half.Slots[OpenXrLayerRuntime.PanelSlot];

            block.Frame();
            Assert.True(runtime.Serve(Tick(1)));
            var second = block.D47Half.Slots[OpenXrLayerRuntime.PanelSlot];

            // Each flush saw the generation from before its draw.
            Assert.Equal([0ul, first.TextureGeneration], published);
            Assert.NotEqual(0ul, first.TextureGeneration);
            Assert.NotEqual(first.TextureGeneration, second.TextureGeneration);

            // The second draw went into the texture the layer was not reading, and that is the one named now.
            Assert.Equal(2, device.Writes.Count);
            Assert.NotEqual(device.Writes[0], device.Writes[1]);
            Assert.Equal(device.Shared[device.Writes[0]].Name, InMemoryXrBlock.NameIn(first));
            Assert.Equal(device.Shared[device.Writes[1]].Name, InMemoryXrBlock.NameIn(second));
            Assert.Equal(1u, second.Visible);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void ACleanSurfaceIsNotRedrawnAndKeepsItsGeneration()
    {
        using var block = new InMemoryXrBlock();
        var device = new FakeTextureDevice();
        var surface = new FakeSurface();
        var runtime = Started(block, device, surface);

        try
        {
            Assert.True(runtime.Serve(Tick(0)));
            var generation = block.D47Half.Slots[OpenXrLayerRuntime.PanelSlot].TextureGeneration;
            var heartbeat = block.D47Half.Heartbeat;

            surface.IsDirty = false;
            block.Frame();
            Assert.True(runtime.Serve(Tick(1)));

            Assert.Equal(1, surface.Draws);
            Assert.Single(device.Writes);
            Assert.Equal(generation, block.D47Half.Slots[OpenXrLayerRuntime.PanelSlot].TextureGeneration);
            Assert.Equal(heartbeat + 1, block.D47Half.Heartbeat);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void AResizeWithinTheLargestSizeMakesNoTextureAndMovesTheRectangle()
    {
        using var block = new InMemoryXrBlock();
        var device = new FakeTextureDevice();
        var surface = new FakeSurface { Size = (8, 8), LargestSize = (32, 16) };
        var runtime = Started(block, device, surface);

        try
        {
            Assert.True(runtime.Serve(Tick(0)));
            surface.Size = (24, 12);
            block.Frame();
            Assert.True(runtime.Serve(Tick(1)));

            var slot = block.D47Half.Slots[OpenXrLayerRuntime.PanelSlot];
            Assert.Equal(2, device.Created.Count);
            Assert.All(device.Shared.Values, made => Assert.Equal((32, 16), (made.Width, made.Height)));
            Assert.Equal((32u, 16u), (slot.TextureWidth, slot.TextureHeight));
            Assert.Equal((24, 12), (slot.RectWidth, slot.RectHeight));
            Assert.Equal((24, 12), (device.RegionWrites[^1].Width, device.RegionWrites[^1].Height));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void OpacityIsMultipliedIntoEveryChannel()
    {
        using var block = new InMemoryXrBlock();
        var device = new FakeTextureDevice();
        var surface = new FakeSurface { Paint = 200, Placement = new SurfacePlacement { Opacity = 0.5f } };
        var runtime = Started(block, device, surface);

        try
        {
            Assert.True(runtime.Serve(Tick(0)));
            Assert.Equal(100, device.RegionWrites[^1].First);

            // A new opacity redraws a surface that has not otherwise changed.
            surface.IsDirty = false;
            surface.Placement = new SurfacePlacement { Opacity = 1f };
            block.Frame();
            Assert.True(runtime.Serve(Tick(1)));

            Assert.Equal(2, surface.Draws);
            Assert.Equal(200, device.RegionWrites[^1].First);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void AHeadLockedSurfaceGoesInTheHeadSpaceAndAPlacedOneInTheCockpit()
    {
        using var block = new InMemoryXrBlock();
        var device = new FakeTextureDevice();
        var placed = new VrPose(new System.Numerics.Vector3(0.2f, 1.1f, -0.8f), System.Numerics.Quaternion.Identity);
        var surface = new FakeSurface();
        var runtime = Started(block, device, surface);

        try
        {
            Assert.True(runtime.Serve(Tick(0)));
            Assert.Equal(XrQuadSpace.Head, block.D47Half.Slots[OpenXrLayerRuntime.PanelSlot].Space);

            surface.Placement = new SurfacePlacement { Lock = SurfaceLock.WorldLocked, Placed = placed, PlacedAgainst = VrPose.Origin };
            block.Frame();
            Assert.True(runtime.Serve(Tick(1)));

            var slot = block.D47Half.Slots[OpenXrLayerRuntime.PanelSlot];
            Assert.Equal(XrQuadSpace.Cockpit, slot.Space);
            Assert.Equal((0.2f, 1.1f, -0.8f), (slot.Pose.PositionX, slot.Pose.PositionY, slot.Pose.PositionZ));
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void StopHidesEveryQuadAndGivesBackTheDeviceAndTheBlock()
    {
        using var block = new InMemoryXrBlock();
        var device = new FakeTextureDevice();
        var runtime = Started(block, device, new FakeSurface());

        Assert.True(runtime.Serve(Tick(0)));
        runtime.Stop();

        Assert.Equal(0u, block.D47Half.Slots[OpenXrLayerRuntime.PanelSlot].Visible);
        Assert.Empty(device.Live);
        Assert.False(device.IsOpen);
        Assert.False(block.IsOpen);
    }

    /// <summary>A layer runtime started against a layer on another runtime that has drawn two frames.</summary>
    internal static OpenXrLayerRuntime Started(InMemoryXrBlock block, FakeTextureDevice device, params IVrSurfaceSource[] sources)
    {
        block.Attach("VirtualDesktopXR");
        var runtime = new OpenXrLayerRuntime(sources, NullLogger<OpenXrLayerRuntime>.Instance, block, device);

        Assert.Equal(OpenXrLayerRuntime.NoFrames, runtime.Start().Detail);
        block.Frame();
        Assert.Equal(VrStartOutcome.Started, runtime.Start().Outcome);
        Assert.Equal([(0x1234u, 0)], device.LuidOpens);
        return runtime;
    }
}
