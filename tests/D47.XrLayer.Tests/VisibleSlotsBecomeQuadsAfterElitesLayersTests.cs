using D47.XrLayer;
using Xunit;

namespace D47.XrLayer.Tests;

public sealed unsafe class VisibleSlotsBecomeQuadsAfterElitesLayersTests : IDisposable
{
    private readonly LayerRig rig = new();

    [Fact]
    public void VisibleSlotsAreAppendedInSlotOrderAsPremultipliedQuads()
    {
        rig.Show(4, XrQuadSpace.Cockpit);
        rig.Show(1, XrQuadSpace.Head);
        rig.Show(3, XrQuadSpace.Cockpit);
        rig.Run();

        var (result, calls) = rig.EndFrame(2 * LayerRig.Millisecond);

        Assert.Equal(XrResult.Success, result);
        var frame = Assert.Single(calls);
        Assert.False(frame.IsElitesOwn);
        Assert.Equal(2 * LayerRig.Millisecond, frame.DisplayTime);
        Assert.Equal(4, frame.Layers.Count);
        Assert.Equal(rig.EliteLayer, frame.Layers[0]);
        Assert.Equal([1f, 3f, 4f], frame.Quads.Select(q => q.Pose.PositionX));
        Assert.Equal([LayerRig.ViewSpace, LayerRig.LocalSpace, LayerRig.LocalSpace], frame.Quads.Select(q => q.Space));
        Assert.All(frame.Quads, quad =>
        {
            Assert.Equal(XrType.CompositionLayerQuad, quad.Type);
            Assert.Equal(XrConstant.LayerBlendTextureSourceAlpha, quad.LayerFlags);
            Assert.Equal(0, quad.EyeVisibility);
            Assert.Equal(1f, quad.Width);
            Assert.Equal(0.375f, quad.Height);
            Assert.Equal(800, quad.SubImage.ImageRect.Width);
            Assert.Equal(300, quad.SubImage.ImageRect.Height);
        });
    }

    [Fact]
    public void EachQuadShowsItsSlotsTextureCopiedIntoItsOwnSwapchain()
    {
        rig.Show(0);
        rig.Show(2);
        rig.Run();

        var quads = rig.EndFrame(2 * LayerRig.Millisecond).Calls.Single().Quads;

        Assert.Equal(2, rig.Next.Swapchains.Count);
        Assert.Equal(rig.Next.Swapchains.Select(s => s.Handle), quads.Select(q => q.SubImage.Swapchain));
        Assert.Equal(
            [("d47.slot0.a", FakeNext.SwapchainImage(quads[0].SubImage.Swapchain, 0)),
             ("d47.slot2.a", FakeNext.SwapchainImage(quads[1].SubImage.Swapchain, 0))],
            rig.Device.Copies);
    }

    [Fact]
    public void ATextureIsCopiedOnlyWhenItsGenerationChanges()
    {
        rig.Show(0);
        rig.Run();
        rig.EndFrame(2 * LayerRig.Millisecond);
        rig.EndFrame(3 * LayerRig.Millisecond);
        Assert.Single(rig.Device.Copies);

        rig.Slot(0).TextureGeneration = 2;
        rig.Beat();
        rig.EndFrame(4 * LayerRig.Millisecond);

        Assert.Equal(2, rig.Device.Copies.Count);
        Assert.Single(rig.Device.Opened);
    }

    [Fact]
    public void ASwapchainIsCreatedOnlyWhenTheTextureSizeChanges()
    {
        rig.Show(0, width: 800, height: 600);
        rig.Run();
        rig.Slot(0).TextureGeneration = 2;
        rig.Slot(0).RectWidth = 400;
        rig.Beat();
        rig.EndFrame(2 * LayerRig.Millisecond);
        Assert.Single(rig.Next.Swapchains);

        rig.Show(0, width: 1024, height: 640);
        rig.Slot(0).TextureGeneration = 3;
        rig.Beat();
        var quad = rig.EndFrame(3 * LayerRig.Millisecond).Calls.Single().Quads.Single();

        Assert.Equal([(800u, 600u), (1024u, 640u)], rig.Next.Swapchains.Select(s => (s.Width, s.Height)));
        Assert.Equal(rig.Next.Swapchains[1].Handle, quad.SubImage.Swapchain);
    }

    [Fact]
    public void WithNoVisibleSlotElitesFrameGoesThroughUnchanged()
    {
        rig.Run();

        var frame = rig.EndFrame(2 * LayerRig.Millisecond).Calls.Single();

        Assert.True(frame.IsElitesOwn);
        Assert.Equal([rig.EliteLayer], frame.Layers);
    }

    [Fact]
    public void TheLayerPublishesItsFramesHeadAndAdapter()
    {
        rig.Show(0);
        rig.Run();

        var published = rig.Published;

        Assert.Equal(XrBlock.MagicValue, rig.Block->Magic);
        Assert.Equal(XrBlock.CurrentLayoutVersion, rig.Block->LayoutVersion);
        Assert.Equal(1u, published.Attached);
        Assert.Equal(XrRefusal.None, published.Refusal);
        Assert.Equal(2ul, published.FrameCounter);
        Assert.Equal(0u, published.Sequence & 1);
        Assert.Equal(3u, published.HeadValid);
        Assert.Equal(1.2f, published.Head.PositionY);
        Assert.Equal((0xA1u, 2), (published.AdapterLuidLow, published.AdapterLuidHigh));
        ReadOnlySpan<byte> name = published.RuntimeName;
        Assert.Equal("VirtualDesktopXR"u8, name[..name.IndexOf((byte)0)]);

        rig.Session.Destroy();

        Assert.Equal(0u, rig.Published.Attached);
    }

    public void Dispose() => rig.Dispose();
}
