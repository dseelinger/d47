using D47.Core.Vr;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// Through the OpenXR layer, each surface's curvature is written into its slot, and the runtime supports curvature
/// exactly while the layer's half says it draws cylinders, read again at every serve.
/// </summary>
public class TheLayersCurvatureFollowsTheBlockTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 20, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Tick(int n) => Start + TimeSpan.FromMilliseconds(100 * n);

    [Fact]
    public void SupportsCurvatureFollowsTheLayersFieldAcrossServes()
    {
        using var block = new InMemoryXrBlock();
        block.Cylinders(true);
        var runtime = TheLayerDrawsIntoTheSpareTextureTests.Started(block, new FakeTextureDevice(), new FakeSurface());

        try
        {
            Assert.True(runtime.SupportsCurvature);

            block.Cylinders(false);
            block.Frame();
            Assert.True(runtime.Serve(Tick(0)));
            Assert.False(runtime.SupportsCurvature);

            block.Cylinders(true);
            block.Frame();
            Assert.True(runtime.Serve(Tick(1)));
            Assert.True(runtime.SupportsCurvature);
        }
        finally
        {
            runtime.Stop();
        }

        Assert.False(runtime.SupportsCurvature);
    }

    [Fact]
    public void ALayerThatDrawsNoCylindersDoesNotSupportCurvature()
    {
        using var block = new InMemoryXrBlock();
        var runtime = TheLayerDrawsIntoTheSpareTextureTests.Started(block, new FakeTextureDevice(), new FakeSurface());

        try
        {
            Assert.True(runtime.Serve(Tick(0)));
            Assert.False(runtime.SupportsCurvature);
        }
        finally
        {
            runtime.Stop();
        }
    }

    [Fact]
    public void EachSurfacesCurvatureIsWrittenIntoItsSlot()
    {
        using var block = new InMemoryXrBlock();
        var panel = new FakeSurface { Placement = new SurfacePlacement { Curvature = 0.4f } };
        var avatar = new FakeSurface { Surface = VrSurface.Avatar, Placement = new SurfacePlacement { Curvature = 0.1f } };
        var runtime = TheLayerDrawsIntoTheSpareTextureTests.Started(block, new FakeTextureDevice(), panel, avatar);

        try
        {
            Assert.True(runtime.Serve(Tick(0)));

            var half = block.D47Half;
            Assert.Equal(0.4f, half.Slots[OpenXrLayerRuntime.PanelSlot].Curvature);
            Assert.Equal(0.1f, half.Slots[OpenXrLayerRuntime.AvatarSlot].Curvature);
        }
        finally
        {
            runtime.Stop();
        }
    }
}
