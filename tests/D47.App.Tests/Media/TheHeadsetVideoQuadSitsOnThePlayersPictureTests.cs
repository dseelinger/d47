using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Headset;
using D47.App.Media;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Viewing;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>
/// The headset's video quad covers the picture the big panel's player draws, fitted to the picture's shape, and goes
/// down when the panel is hidden, something covers it, or there is no picture.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheHeadsetVideoQuadSitsOnThePlayersPictureTests
{
    private static readonly VideoTitle Moon = new(
        VideoSources.InternetArchive,
        "moon",
        "A Trip to the Moon (1902)",
        VideoKind.ShortFilm,
        "public domain",
        TimeSpan.FromSeconds(788),
        new Uri("https://archive.example.test/moon/moon.mp4"),
        null);

    private static readonly VideoFrame Frame = new(new byte[16 * 9 * 4], 16, 9, TimeSpan.Zero);

    [AvaloniaFact]
    public void ThePlayersPictureIsTheVideosArea()
    {
        var (panel, host) = Headset();

        try
        {
            var video = new VrVideoSurface(panel, () => Frame);
            Draw(panel);

            // No picture decoded yet: nothing to lay a quad over.
            Assert.Null(panel.VideoArea);
            Assert.False(video.Visible);

            Picture(panel).Source = new WriteableBitmap(new PixelSize(640, 360), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            Draw(panel);

            var area = Assert.IsType<VrPixelRect>(panel.VideoArea);
            Assert.Equal(16f / 9f, area.Width / area.Height, 2);
            Assert.InRange(area.X, 0, panel.DrawnSize.Width - area.Width);
            Assert.InRange(area.Y, 0, panel.DrawnSize.Height - area.Height);
            Assert.True(video.Visible);

            var expected = VrPlacementMath.Over(panel.Placement.Sane(), panel.DrawnSize, area);
            Assert.Equal(expected.WidthMetres, video.Placement.WidthMetres, 4);
            Assert.NotNull(video.Placement.Offset);

            var picture = Assert.IsType<D47.Vr.VrPicture>(video.Picture);
            Assert.Same(Frame, picture.Key);
            Assert.Equal((16, 9), (picture.Width, picture.Height));
        }
        finally
        {
            Finish(panel, host);
        }
    }

    [AvaloniaFact]
    public void HidingThePanelOrHavingNoPictureTakesTheQuadDown()
    {
        var (panel, host) = Headset();
        VideoFrame? frame = Frame;

        try
        {
            var video = new VrVideoSurface(panel, () => frame);
            Draw(panel);
            Picture(panel).Source = new WriteableBitmap(new PixelSize(640, 360), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            Draw(panel);
            Assert.True(video.Visible);

            panel.Enabled = false;
            Assert.False(video.Visible);

            panel.Enabled = true;
            frame = null;
            Assert.False(video.Visible);
            Assert.Null(video.Picture);
        }
        finally
        {
            Finish(panel, host);
        }
    }

    [Theory]
    [InlineData(true, true, false, true, true)]
    [InlineData(false, true, false, true, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, true, true, true, false)]
    [InlineData(true, true, false, false, false)]
    public void TheQuadGoesUpOnlyWhenEveryConditionHolds(bool panelVisible, bool drawn, bool covered, bool picture, bool shows) =>
        Assert.Equal(shows, VrVideoSurface.Shows(panelVisible, drawn, covered, picture));

    private static (VrPanelSurface Panel, WatchHost Host) Headset()
    {
        var files = new MemoryFileSystem();
        var (settings, _, _) = TestSurface.Create();
        settings.Replace(
            "test",
            current => current with
            {
                Vr = current.Vr with
                {
                    Mode = "full",
                    Panel = current.Vr.Panel with { Pixels = "1250x690", Zoom = 80 },
                },
            });

        var store = new WatchStore("data/watch.json", files, NullLogger<WatchStore>.Instance);
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var playback = new VideoPlayback(arbiter, background: work => work());
        var host = new WatchHost(playback, store, settings, files, readSubtitles: (_, _) => Task.FromResult<string?>(null), background: work => work());

        var panel = new VrPanelSurface(new PanelViewModel(), settings, _ => null, watch: host) { Enabled = true };
        panel.Configure();
        panel.ApplyMode();
        Dispatcher.UIThread.RunJobs();

        Assert.True(panel.Nav.Select(PanelTab.Media));
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.MoviesRoot));
        Assert.True(WatchPage.OpenPlayer(panel.Nav, host, Moon, start: false));
        host.MoveTo(VideoScreen.BigPanel);
        Dispatcher.UIThread.RunJobs();

        return (panel, host);
    }

    private static Image Picture(VrPanelSurface panel) =>
        panel.Board.View.GetVisualDescendants().OfType<Image>().First(image => image.Name == VideoPlayerPage.PictureName);

    private static void Draw(VrPanelSurface panel)
    {
        var (width, height) = panel.Size;
        var buffer = new byte[width * height * 4];

        unsafe
        {
            fixed (byte* pixels = buffer)
            {
                panel.Draw((IntPtr)pixels, width * 4);
            }
        }
    }

    private static void Finish(VrPanelSurface panel, WatchHost host)
    {
        host.Video.Stop();
        host.Dispose();
        panel.Dispose();
    }
}
