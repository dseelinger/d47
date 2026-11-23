using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Storage;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>The window's movie player spans the page, and its frame is as tall as leaves the timeline and the tiles on screen (#2724).</summary>
[Trait("Category", "Integration")]
public sealed class TheMoviePlayerFillsThePageTests
{
    private static readonly VideoTitle Steel = new(
        VideoSources.Blender,
        "tears",
        "Tears of Steel",
        VideoKind.ShortFilm,
        "CC BY 3.0",
        TimeSpan.FromSeconds(734),
        new Uri("https://media.example.test/tears_of_steel_720p.mp4"),
        null,
        "© Blender Foundation · mango.blender.org");

    private sealed record Shown(Window Window, WatchHost Host, VideoPlayerPage Page)
    {
        public Border Frame => Page.GetVisualDescendants().OfType<Border>().First(border => border.Name == "VideoFrame");

        public Image Picture => Page.GetVisualDescendants().OfType<Image>().First(image => image.Name == VideoPlayerPage.PictureName);

        public Grid Top => (Grid)Frame.GetVisualParent()!.GetVisualParent()!;

        public Control Slabs => Top.Children[1];

        public ScrollViewer Scroll => Page.GetVisualDescendants().OfType<ScrollViewer>().First();

        public Control Content => (Control)Scroll.Content!;

        public Rect In(Control control) => new(control.TranslatePoint(default, Content) ?? default, control.Bounds.Size);

        public void Resize(double width, double height)
        {
            Window.Width = width;
            Window.Height = height;
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static Shown Open(double width, double height, bool picture = false)
    {
        var (settings, _, _) = TestSurface.Create();
        var files = new MemoryFileSystem();
        var store = new WatchStore("data/watch.json", files, NullLogger<WatchStore>.Instance);
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var video = new D47.App.Media.VideoPlayback(arbiter, background: work => work());
        var host = new WatchHost(video, store, settings, files, readSubtitles: (_, _) => Task.FromResult<string?>(null), background: work => work());
        var page = new VideoPlayerPage(host, Steel);
        var window = new Window { Content = page, Width = width, Height = height };

        window.Show();
        var shown = new Shown(window, host, page);

        if (picture)
        {
            shown.Picture.Source = new Avalonia.Media.Imaging.WriteableBitmap(
                new PixelSize(160, 90),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Opaque);
        }

        shown.Resize(width, height);

        return shown;
    }

    private static void Finish(Shown shown)
    {
        shown.Host.Video.Stop();
        shown.Host.Dispose();
        shown.Window.Close();
    }

    [AvaloniaTheory]
    [InlineData(1280, 1000)]
    [InlineData(1920, 1000)]
    [InlineData(2560, 1400)]
    public void TheSlabsEndAtThePaddingAndThePictureTakesTheRest(double width, double height)
    {
        var shown = Open(width, height);
        var right = shown.Content.Bounds.Width - 14;

        Assert.Equal(right, shown.In(shown.Slabs).Right, 0.5);
        Assert.Equal(300, shown.Slabs.Bounds.Width, 0.5);
        Assert.Equal(right - 300 - 28 - 14, shown.Frame.Bounds.Width, 0.5);
        Assert.Equal(shown.Frame.Bounds.Width, shown.Frame.GetVisualParent()!.Bounds.Width, 0.5);

        Finish(shown);
    }

    [AvaloniaFact]
    public void TheTimelineAndTheTilesSpanTheFrame()
    {
        var shown = Open(1920, 1000);
        var column = shown.Frame.GetVisualParent()!;

        foreach (var below in column.GetVisualChildren().OfType<Control>())
        {
            Assert.Equal(shown.Frame.Bounds.Width, below.Bounds.Width, 0.5);
        }

        var tiles = column.GetVisualChildren().OfType<Grid>().Single();
        var widths = tiles.Children.Select(tile => tile.Bounds.Width).ToArray();

        Assert.Equal(widths[1], widths[0] * 2, 2.0);
        Assert.Equal(widths[0], widths[2], 1.5);
        Assert.Equal(widths[0], widths[3], 1.5);

        Finish(shown);
    }

    [AvaloniaFact]
    public void ATallPageShowsAFrameOfExactlySixteenByNine()
    {
        var shown = Open(1280, 1400, picture: true);
        var inner = shown.Picture.GetVisualParent()!;

        Assert.Equal(inner.Bounds.Width * 9 / 16, inner.Bounds.Height, 1.5);
        Assert.Equal(inner.Bounds.Width, shown.Picture.Bounds.Width, 1.5);
        Assert.Equal(inner.Bounds.Height, shown.Picture.Bounds.Height, 1.5);

        Finish(shown);
    }

    [AvaloniaFact]
    public void AShortPageKeepsTheTimelineAndTheTilesOnScreenWithTheFrameCentred()
    {
        var shown = Open(1917, 863, picture: true);
        var tiles = shown.Frame.GetVisualParent()!.GetVisualChildren().OfType<Grid>().Single();
        var inner = shown.Picture.GetVisualParent()!;

        Assert.Equal(0, shown.Scroll.Offset.Y);
        Assert.True(shown.In(tiles).Bottom <= shown.Scroll.Bounds.Height, $"tiles end at {shown.In(tiles).Bottom} of {shown.Scroll.Bounds.Height}");
        Assert.True(shown.Frame.Bounds.Height < (shown.Frame.Bounds.Width - 2) * 9 / 16);
        Assert.True(shown.Frame.Bounds.Height >= 120);
        Assert.Equal(16.0 / 9, shown.Picture.Bounds.Width / shown.Picture.Bounds.Height, 0.03);
        Assert.Equal(inner.Bounds.Height, shown.Picture.Bounds.Height, 1.5);

        var left = shown.Picture.Bounds.Left;
        var rightBlack = inner.Bounds.Width - shown.Picture.Bounds.Right;

        Assert.True(left > 20);
        Assert.Equal(left, rightBlack, 1.5);

        Finish(shown);
    }

    [AvaloniaFact]
    public void ResizingTheWindowFitsTheFrameAgain()
    {
        var shown = Open(1917, 863, picture: true);
        var short1 = shown.Frame.Bounds.Height;
        Assert.True(short1 < (shown.Frame.Bounds.Width - 2) * 9 / 16);
        var narrow = shown.Frame.Bounds.Width;

        shown.Resize(1280, 1400);

        Assert.True(shown.Frame.Bounds.Height < short1 || shown.Frame.Bounds.Height < narrow);
        Assert.True(shown.Frame.Bounds.Width < narrow);
        Assert.Equal((shown.Frame.Bounds.Width - 2) * 9 / 16 + 2, shown.Frame.Bounds.Height, 1.5);

        Finish(shown);
    }

    [AvaloniaFact]
    public void PlaysOnCaptionsAndSoundAreNoWiderThanNineHundred()
    {
        var shown = Open(2560, 1400);

        foreach (var name in new[] { VideoPlayerPage.PlaysOnName, VideoPlayerPage.CaptionsChoiceName, VideoPlayerPage.PushToTalkName })
        {
            var control = shown.Page.GetVisualDescendants().OfType<Control>().First(candidate => candidate.Name == name);

            Assert.True(control.Bounds.Width <= 901.5, $"{name} is {control.Bounds.Width} wide");
            Assert.Equal(14, shown.In(control).Left, 0.5);
        }

        Finish(shown);
    }
}
