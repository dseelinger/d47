using D47.Core.Vr;
using D47.XrLayer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Vr.Tests;

/// <summary>
/// Through the OpenXR layer, the video's pictures are drawn into slot 5's spare texture and published by the
/// presenter, on a device of its own; the serve only places the slot and shows or hides it.
/// </summary>
public class TheLayersVideoGoesUpFromItsOwnPresenterTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 20, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Tick(int n) => Start + TimeSpan.FromMilliseconds(100 * n);

    [Fact]
    public void TheVideoHasSlotFive() =>
        Assert.Equal(5, OpenXrLayerRuntime.SlotFor(VrSurface.Video));

    [Fact]
    public void TheServeUploadsNothingAndShowsNothingBeforeThePresenterHasPublished()
    {
        using var rig = new Rig();
        rig.Video.Show();

        Assert.True(rig.Runtime.Serve(Tick(0)));

        Assert.Empty(rig.VideoDevice.Writes);
        Assert.Equal(0u, rig.Block.D47Half.Slots[OpenXrLayerRuntime.VideoSlot].Visible);
    }

    [Fact]
    public void APictureIsDrawnIntoTheSpareFlushedAndThenPublished()
    {
        using var rig = new Rig();
        var published = new List<ulong>();
        rig.VideoDevice.Flushed = () => published.Add(rig.Block.D47Half.Slots[OpenXrLayerRuntime.VideoSlot].TextureGeneration);

        rig.Video.Show(4, 2);
        rig.Serve();
        rig.Clock.Step();
        var first = rig.Block.D47Half.Slots[OpenXrLayerRuntime.VideoSlot];

        rig.Video.Show(4, 2);
        rig.Clock.Step();
        var second = rig.Block.D47Half.Slots[OpenXrLayerRuntime.VideoSlot];

        Assert.Equal([(0x1234u, 0)], rig.VideoDevice.LuidOpens);
        Assert.Equal([0ul, first.TextureGeneration], published);
        Assert.NotEqual(0ul, first.TextureGeneration);
        Assert.NotEqual(first.TextureGeneration, second.TextureGeneration);
        Assert.Equal(2, rig.VideoDevice.Writes.Count);
        Assert.NotEqual(rig.VideoDevice.Writes[0], rig.VideoDevice.Writes[1]);
        Assert.Equal(rig.VideoDevice.Shared[rig.VideoDevice.Writes[1]].Name, InMemoryXrBlock.NameIn(second));
        Assert.Equal((4u, 2u, 4, 2), (second.TextureWidth, second.TextureHeight, second.RectWidth, second.RectHeight));

        // Nothing of the video's went through the surfaces' device.
        Assert.DoesNotContain(rig.Device.Shared.Values, texture => (texture.Width, texture.Height) == (4, 2));
    }

    [Fact]
    public void WhileTheServeDoesNotWantTheVideoThePresenterOpensNothingAndWritesNothing()
    {
        using var rig = new Rig();
        rig.Video.Visible = false;
        rig.Video.Show();
        rig.Serve();

        rig.Clock.Step();

        Assert.Empty(rig.VideoDevice.LuidOpens);
        Assert.Empty(rig.VideoDevice.Writes);
        Assert.Equal(0ul, rig.Block.D47Half.Slots[OpenXrLayerRuntime.VideoSlot].TextureGeneration);
    }

    [Fact]
    public void TheSamePictureIsNotWrittenTwice()
    {
        using var rig = new Rig();
        rig.Video.Show();
        rig.Serve();

        rig.Clock.Step();
        rig.Clock.Step();

        Assert.Single(rig.VideoDevice.Writes);
    }

    [Fact]
    public void OncePublishedTheServePlacesAndShowsTheSlotWithItsCurvature()
    {
        using var rig = new Rig();
        rig.Video.Placement = new SurfacePlacement { Curvature = 0.2f, WidthMetres = 0.6f };
        rig.Video.Show();
        rig.Serve();
        Assert.Equal(0u, rig.Block.D47Half.Slots[OpenXrLayerRuntime.VideoSlot].Visible);
        rig.Clock.Step();

        rig.Serve();

        var slot = rig.Block.D47Half.Slots[OpenXrLayerRuntime.VideoSlot];
        Assert.Equal(1u, slot.Visible);
        Assert.Equal(0.2f, slot.Curvature);
        Assert.Equal(0.6f, slot.WidthMetres);
    }

    [Fact]
    public void AHiddenVideoTakesTheSlotDown()
    {
        using var rig = new Rig();
        rig.Video.Show();
        rig.Serve();
        rig.Clock.Step();
        rig.Serve();

        rig.Video.Visible = false;
        rig.Serve();

        Assert.Equal(0u, rig.Block.D47Half.Slots[OpenXrLayerRuntime.VideoSlot].Visible);
    }

    [Fact]
    public void ANewOpacityRedrawsTheSamePictureFaded()
    {
        using var rig = new Rig();
        rig.Video.Show(paint: 0x80);
        rig.Serve();
        rig.Clock.Step();

        rig.Video.Placement = new SurfacePlacement { Opacity = 0.5f };
        rig.Serve();
        rig.Clock.Step();

        Assert.Equal([0x80, 0x40], rig.VideoDevice.RegionWrites.Select(write => (int)write.First));
    }

    [Fact]
    public void StopEndsThePresenterWhichGivesBackItsTexturesAndDevice()
    {
        using var rig = new Rig();
        rig.Video.Show();
        rig.Serve();
        rig.Clock.Step();

        rig.Runtime.Stop();

        Assert.Equal(1, rig.Clock.Finishes);
        Assert.False(rig.Clock.Running);
        Assert.Empty(rig.VideoDevice.Live);
        Assert.False(rig.VideoDevice.IsOpen);
        Assert.Equal(0u, rig.Block.D47Half.Slots[OpenXrLayerRuntime.VideoSlot].Visible);
    }

    private sealed class Rig : IDisposable
    {
        public Rig()
        {
            Block.Attach("VirtualDesktopXR");
            Runtime = new OpenXrLayerRuntime(
                [new FakeSurface()],
                NullLogger<OpenXrLayerRuntime>.Instance,
                Block,
                Device,
                Video,
                VideoDevice,
                Clock);

            Block.Frame();
            Assert.Equal(OpenXrLayerRuntime.NoFrames, Runtime.Start().Detail);
            Block.Frame();
            Assert.Equal(VrStartOutcome.Started, Runtime.Start().Outcome);
            Assert.Equal(1, Clock.Runs);
        }

        public InMemoryXrBlock Block { get; } = new();

        public FakeTextureDevice Device { get; } = new();

        public FakeTextureDevice VideoDevice { get; } = new();

        public FakeVideo Video { get; } = new();

        public ManualVideoClock Clock { get; } = new();

        public OpenXrLayerRuntime Runtime { get; }

        private int _ticks;

        /// <summary>One of Elite's frames and one serve.</summary>
        public void Serve()
        {
            Block.Frame();
            Assert.True(Runtime.Serve(Tick(_ticks++)));
        }

        public void Dispose()
        {
            Runtime.Stop();
            Block.Dispose();
        }
    }
}
