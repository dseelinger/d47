using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Media;
using D47.App.Panel;
using D47.App.Windowing;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Viewing;
using D47.Core.Vr;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>
/// The Movies player's full screen: in the window it covers the monitor with the band over the picture, on the big
/// panel the picture takes the panel's width over a row of controls, and each way in and out works (#2723).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheMoviePlayerGoesFullScreenTests
{
    private static readonly string Clip = Path.Combine(AppContext.BaseDirectory, "Fixtures", "video", "clip.mp4");

    private static readonly string Missing = Path.Combine(AppContext.BaseDirectory, "Fixtures", "video", "not-here.mp4");

    private static readonly Lazy<bool> Decodes = new(() =>
    {
        using var frames = VideoFrames.Open(Clip);
        return frames is not null;
    });

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

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static VideoTitle FileTitle(string path) => new(
        WatchHost.FileSource,
        path,
        Path.GetFileName(path),
        VideoKind.Other,
        string.Empty,
        null,
        new Uri(path, UriKind.Absolute),
        null);

    private static WatchHost Watch(SettingsService settings, TimeProvider? time = null)
    {
        var files = new MemoryFileSystem();
        var store = new WatchStore("data/watch.json", files, NullLogger<WatchStore>.Instance);
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var video = new VideoPlayback(arbiter, background: work => work());

        return new WatchHost(video, store, settings, files, time, readSubtitles: (_, _) => Task.FromResult<string?>(null), background: work => work());
    }

    private static void Until(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting until {what}.");
            Thread.Sleep(10);
        }
    }

    private static T Named<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    private static WriteableBitmap Picture(int width, int height) =>
        new(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    private static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private sealed record Desktop(Window Window, PanelView Panel, WatchHost Host)
    {
        public Grid? Layer => Panel.GetVisualDescendants().OfType<Grid>().FirstOrDefault(grid => grid.Name == VideoPlayerPage.FullScreenLayerName);

        public bool InFullScreen => Layer is not null;

        public Image Picture => Named<Image>(Panel, VideoPlayerPage.PictureName);

        public Border Frame => Named<Border>(Panel, "VideoFrame");

        public void Tick()
        {
            Dispatcher.UIThread.RunJobs();
            Panel.TickMedia();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        public void Finish()
        {
            Host.Video.Stop();
            Host.Dispose();
            Window.Close();
        }
    }

    private static Desktop OnTheDesktop(TimeProvider? time = null, VideoTitle? title = null, bool start = false)
    {
        var (settings, _, _) = TestSurface.Create();
        var host = Watch(settings, time);
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableMedia(watch: host);
        var window = new Window { Content = panel, Width = 1280, Height = 810 };
        WindowFullScreen.Follow(window, panel);
        window.Show();

        panel.Tab = PanelTab.Media;
        Assert.True(WatchPage.OpenPlayer(panel.Nav, host, title ?? Steel, start));
        var desktop = new Desktop(window, panel, host);
        desktop.Tick();

        return desktop;
    }

    [AvaloniaFact]
    public void TheWindowsTileFollowsFromTheStartAndPressingItCoversTheScreen()
    {
        if (!Decodes.Value)
        {
            Assert.Skip("Media Foundation cannot decode the fixture here.");
        }

        var desktop = OnTheDesktop(title: FileTitle(Clip), start: true);

        try
        {
            Until(() => desktop.Host.Video.State is not (VideoState.Opening or null), "the clip opened");
            desktop.Host.Pause();
            desktop.Tick();

            var tile = Named<Button>(desktop.Panel, VideoPlayerPage.FullScreenName);
            var fromStart = Named<Button>(desktop.Panel, VideoPlayerPage.FromStartName);
            Assert.Same(fromStart.Parent, tile.Parent);
            Assert.Equal(Grid.GetColumn(fromStart) + 1, Grid.GetColumn(tile));
            Assert.True(tile.IsEnabled);

            var position = desktop.Window.Position;
            var size = (desktop.Window.Width, desktop.Window.Height);
            Press(tile);
            desktop.Tick();

            Assert.True(desktop.Host.FullScreen);
            Assert.True(desktop.InFullScreen);
            Assert.Equal(WindowState.FullScreen, desktop.Window.WindowState);
            var layer = Assert.IsType<Grid>(desktop.Layer);
            Assert.Contains(desktop.Picture, layer.GetVisualDescendants());
            Assert.Null(desktop.Frame.Child);
            Assert.Equal(layer.Bounds.Size, desktop.Panel.Bounds.Size);

            Press(Named<Button>(layer, VideoPlayerPage.ExitFullScreenName));
            desktop.Tick();

            Assert.False(desktop.Host.FullScreen);
            Assert.False(desktop.InFullScreen);
            Assert.Null(desktop.Layer);
            Assert.Equal(WindowState.Normal, desktop.Window.WindowState);
            Assert.Equal(position, desktop.Window.Position);
            Assert.Equal(size, (desktop.Window.Width, desktop.Window.Height));
            Assert.NotNull(desktop.Frame.Child);
        }
        finally
        {
            desktop.Finish();
        }
    }

    [AvaloniaFact]
    public void TheWindowsFullScreenIsThePictureOnBlackWithTheBandOverItsBottom()
    {
        var desktop = OnTheDesktop();

        try
        {
            desktop.Picture.Source = Picture(1280, 720);
            desktop.Host.SetFullScreen(true);
            desktop.Tick();

            var layer = Assert.IsType<Grid>(desktop.Layer);
            var band = Named<Grid>(layer, VideoPlayerPage.FullScreenBandName);
            Assert.Equal(VideoPlayerPage.BandHeight, band.Bounds.Height, 0.5);
            Assert.Equal(layer.Bounds.Width, band.Bounds.Width, 0.5);
            Assert.Equal(layer.Bounds.Bottom, band.Bounds.Bottom, 0.5);
            Assert.Equal(layer.Bounds.Width, desktop.Picture.Bounds.Width, 0.5);

            var exit = Named<Button>(band, VideoPlayerPage.ExitFullScreenName);
            Assert.Equal(176, exit.Bounds.Width, 0.5);
            Assert.Equal(44, exit.Bounds.Height, 0.5);
            Assert.All(
                band.GetVisualDescendants().OfType<Button>().Where(button => button != exit),
                button => Assert.Equal(96, button.Bounds.Width, 0.5));
            Assert.Contains(band.GetVisualDescendants(), visual => visual is SeekBar);
            Assert.DoesNotContain(band.GetVisualDescendants().OfType<Button>(), button => button.Name == VideoPlayerPage.FromStartName);

            var layerHost = (Avalonia.Controls.Panel)layer.GetVisualParent()!;
            var scanlines = layerHost.GetVisualParent()!.GetVisualChildren().OfType<Border>().First(border => border.Name == "Scanlines");
            Assert.True(
                layerHost.GetVisualParent()!.GetVisualChildren().ToList().IndexOf(layerHost) > layerHost.GetVisualParent()!.GetVisualChildren().ToList().IndexOf(scanlines),
                "The full-screen layer is drawn over the scanlines.");

            desktop.Window.SaveFrame("media-movies-full-screen-window.png");
        }
        finally
        {
            desktop.Finish();
        }
    }

    [AvaloniaFact]
    public void TheBandGoesAfterFiveStillSecondsUnlessThePointerIsOverIt()
    {
        var clock = new Clock();
        var desktop = OnTheDesktop(clock);

        try
        {
            desktop.Host.SetFullScreen(true);
            desktop.Tick();

            var layer = Assert.IsType<Grid>(desktop.Layer);
            var band = Named<Grid>(layer, VideoPlayerPage.FullScreenBandName);
            var caption = (Border)Named<TextBlock>(layer, VideoPlayerPage.CaptionName).Parent!;

            // The fade is checked on its own below; here the opacity is read at its end.
            band.Transitions = null;

            void Shows(bool shown)
            {
                Assert.Equal(shown ? 1 : 0, band.Opacity);
                Assert.Equal(shown, band.IsHitTestVisible);
                Assert.Equal(shown, layer.Cursor is null);
                Assert.Equal(VideoPlayerPage.FullScreenEdge + (shown ? VideoPlayerPage.BandHeight : 0), caption.Margin.Bottom);
                Assert.Equal(VideoPlayerPage.FullScreenEdge, caption.Margin.Left);
            }

            Shows(true);
            Assert.Equal(layer.Bounds.Height / 27, Named<TextBlock>(layer, VideoPlayerPage.CaptionName).FontSize, 0.01);

            clock.Now += TimeSpan.FromSeconds(4.9);
            desktop.Tick();
            Shows(true);

            clock.Now += TimeSpan.FromSeconds(0.2);
            desktop.Tick();
            Shows(false);

            desktop.Window.MouseMove(new Point(400, 300));
            desktop.Window.MouseMove(new Point(401, 300));
            desktop.Tick();
            Shows(true);

            var over = new Point(300, layer.Bounds.Height - 30);
            desktop.Window.MouseMove(over);
            desktop.Window.MouseMove(over + new Point(1, 0));
            desktop.Tick();
            Assert.True(band.IsPointerOver);

            clock.Now += TimeSpan.FromSeconds(30);
            desktop.Tick();
            Shows(true);

            desktop.Window.MouseMove(new Point(400, 300));
            clock.Now += TimeSpan.FromSeconds(5);
            desktop.Tick();
            Shows(false);
        }
        finally
        {
            desktop.Finish();
        }
    }

    [AvaloniaFact]
    public void TheBandFadesOverTwoHundredMillisecondsOnlyWithAnimationEffectsOn()
    {
        var fade = Assert.IsType<DoubleTransition>(Assert.Single(VideoPlayerPage.BandTransitions(animate: true)!));

        Assert.Equal(Visual.OpacityProperty, fade.Property);
        Assert.Equal(TimeSpan.FromMilliseconds(200), fade.Duration);
        Assert.Null(VideoPlayerPage.BandTransitions(animate: false));
    }

    [AvaloniaFact]
    public void EscapeLeavesFullScreenFirstAndPutsBackAMaximisedWindow()
    {
        var desktop = OnTheDesktop();

        try
        {
            desktop.Window.WindowState = WindowState.Maximized;
            desktop.Host.SetFullScreen(true);
            desktop.Tick();
            Assert.Equal(WindowState.FullScreen, desktop.Window.WindowState);

            var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
            desktop.Layer!.RaiseEvent(key);
            desktop.Tick();

            Assert.True(key.Handled);
            Assert.False(desktop.InFullScreen);
            Assert.False(desktop.Host.FullScreen);
            Assert.Equal(WindowState.Maximized, desktop.Window.WindowState);
            Assert.Equal(VideoPlayerPage.CrumbOf(Steel).Key, desktop.Panel.Nav.Trail[^1].Key);
        }
        finally
        {
            desktop.Finish();
        }
    }

    [AvaloniaFact]
    public void LeavingThePlayerPageEndsFullScreen()
    {
        var desktop = OnTheDesktop();

        try
        {
            desktop.Host.SetFullScreen(true);
            desktop.Tick();
            Assert.True(desktop.InFullScreen);

            desktop.Panel.Tab = PanelTab.Transcript;
            desktop.Tick();

            Assert.False(desktop.InFullScreen);
            Assert.False(desktop.Host.FullScreen);
            Assert.Equal(WindowState.Normal, desktop.Window.WindowState);
        }
        finally
        {
            desktop.Finish();
        }
    }

    [AvaloniaFact]
    public void PlaysOnMovingThePictureEndsFullScreen()
    {
        var desktop = OnTheDesktop();

        try
        {
            desktop.Host.SetFullScreen(true);
            desktop.Tick();
            Assert.True(desktop.InFullScreen);

            desktop.Host.MoveTo(VideoScreen.MiniPanel);
            desktop.Tick();

            Assert.False(desktop.Host.FullScreen);
            Assert.False(desktop.InFullScreen);
            Assert.Equal(WindowState.Normal, desktop.Window.WindowState);
            Assert.Equal(FullScreenAnswer.MiniPanel, desktop.Host.SetFullScreen(true));
            Assert.False(desktop.Host.FullScreen);
        }
        finally
        {
            desktop.Finish();
        }
    }

    [AvaloniaFact]
    public void AVideoThatStopsOnAnErrorLeavesFullScreenForItsNotice()
    {
        var (settings, _, _) = TestSurface.Create();
        var host = Watch(settings);
        var title = FileTitle(Missing);

        try
        {
            host.Open(title);
            Assert.Equal(FullScreenAnswer.Done, host.SetFullScreen(true));
            Assert.True(host.Play(title));
            Until(() => host.Video.Failure is not null, "the missing file failed");

            Assert.False(host.FullScreen);
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
        }
    }

    [AvaloniaFact]
    public void AStoppedVideoCannotBePutBackOverItsNotice()
    {
        var desktop = OnTheDesktop(title: FileTitle(Missing), start: true);

        try
        {
            Until(() => desktop.Host.Video.Failure is not null, "the missing file failed");
            desktop.Tick();

            Assert.False(Named<Button>(desktop.Panel, VideoPlayerPage.FullScreenName).IsEnabled);

            desktop.Host.SetFullScreen(true);
            desktop.Tick();

            Assert.False(desktop.InFullScreen);
            Assert.Contains(
                desktop.Panel.GetVisualDescendants().OfType<Notice>(),
                notice => notice.IsEffectivelyVisible && notice.Label is VideoPlayerPage.NoDecoderLabel or VideoPlayerPage.StoppedLabel);
        }
        finally
        {
            desktop.Finish();
        }
    }

    [AvaloniaFact]
    public void FullScreenByVoiceOpensThePlayerInTheWindowFromAnotherPage()
    {
        var desktop = OnTheDesktop();

        try
        {
            desktop.Panel.Tab = PanelTab.Transcript;
            desktop.Tick();

            Assert.Equal(FullScreenAnswer.Done, desktop.Host.SetFullScreen(true));
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
            desktop.Tick();

            Assert.Equal(PanelTab.Media, desktop.Panel.Tab);
            Assert.Equal(VideoPlayerPage.CrumbOf(Steel).Key, desktop.Panel.Nav.Trail[^1].Key);
            Assert.True(desktop.InFullScreen);
            Assert.Equal(WindowState.FullScreen, desktop.Window.WindowState);
            Assert.Equal(FullScreenAnswer.Already, desktop.Host.SetFullScreen(true));

            Assert.Equal(FullScreenAnswer.Done, desktop.Host.SetFullScreen(false));
            desktop.Tick();
            Assert.False(desktop.InFullScreen);
            Assert.Equal(WindowState.Normal, desktop.Window.WindowState);
        }
        finally
        {
            desktop.Finish();
        }
    }

    [AvaloniaFact]
    public void ClosingInFullScreenRemembersThePlacementFromBefore()
    {
        var (_, viewState, _) = TestSurface.Create();
        var window = new Window { Width = 1180, Height = 880 };
        WindowPlacementMemory.Attach(window, viewState);
        window.Show();

        window.Width = 900;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        window.WindowState = WindowState.Maximized;
        Dispatcher.UIThread.RunJobs();
        window.WindowState = WindowState.FullScreen;
        Dispatcher.UIThread.RunJobs();
        window.Close();

        var remembered = viewState.Load().MainWindow;
        Assert.NotNull(remembered);
        Assert.True(remembered.Maximized);
        Assert.Equal((900.0, 700.0), (remembered.Width, remembered.Height));
    }

    private sealed record Headset(VrPanelSurface Panel, WatchHost Host)
    {
        public Visual View => Panel.Board.View;

        public Image Picture => Named<Image>(View, VideoPlayerPage.PictureName);

        public void Draw()
        {
            Dispatcher.UIThread.RunJobs();
            Panel.TickMedia();
            var (width, height) = Panel.Size;
            var buffer = new byte[width * height * 4];

            unsafe
            {
                fixed (byte* pixels = buffer)
                {
                    Panel.Draw((IntPtr)pixels, width * 4);
                }
            }
        }

        public void Finish()
        {
            Host.Video.Stop();
            Host.Dispose();
            Panel.Dispose();
        }
    }

    private static Headset InTheHeadset(VideoTitle? title = null, bool start = false)
    {
        var (settings, _, _) = TestSurface.Create();
        settings.Replace(
            "test",
            current => current with
            {
                Vr = current.Vr with
                {
                    Mode = "full",
                    Panel = current.Vr.Panel with { Pixels = "1024x640", Zoom = 100 },
                },
            });

        var host = Watch(settings);
        var panel = new VrPanelSurface(new PanelViewModel(), settings, _ => null, watch: host) { Enabled = true };
        panel.Configure();
        panel.ApplyMode();
        Dispatcher.UIThread.RunJobs();

        Assert.True(panel.Nav.Select(PanelTab.Media));
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.MoviesRoot));
        host.MoveTo(VideoScreen.BigPanel);
        Assert.True(WatchPage.OpenPlayer(panel.Nav, host, title ?? Steel, start, headset: true));
        var headset = new Headset(panel, host);
        headset.Draw();

        return headset;
    }

    private static void AreaIs(VrPixelRect? area, float x, float y, float width, float height)
    {
        var drawn = Assert.IsType<VrPixelRect>(area);
        Assert.True(Math.Abs(x - drawn.X) <= 1 && Math.Abs(y - drawn.Y) <= 1, $"The picture is at {drawn}.");
        Assert.Equal(x, drawn.X, 1f);
        Assert.Equal(y, drawn.Y, 1f);
        Assert.Equal(width, drawn.Width, 1f);
        Assert.Equal(height, drawn.Height, 1f);
    }

    [AvaloniaFact]
    public void TheBigPanelHasFullScreenUnderItsThreeTilesAndNoYourPlace()
    {
        var headset = InTheHeadset();

        try
        {
            var view = headset.View;
            var tile = Named<Button>(view, VideoPlayerPage.FullScreenName);
            var back = Named<Button>(view, VideoPlayerPage.BackName);
            var forward = Named<Button>(view, VideoPlayerPage.ForwardName);

            Rect In(Control control) => new Rect(control.Bounds.Size).TransformToAABB(control.TransformToVisual(view)!.Value);

            Assert.True(In(tile).Top >= In(back).Bottom);
            Assert.Equal(In(back).Left, In(tile).Left, 0.5);
            Assert.Equal(In(forward).Right, In(tile).Right, 0.5);
            Assert.DoesNotContain(
                view.GetVisualDescendants().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && block.Text == "YOUR PLACE");
        }
        finally
        {
            headset.Finish();
        }
    }

    [AvaloniaFact]
    public void TheBigPanelsFullScreenGivesThePictureThePanelsWidthOverTheControlRow()
    {
        var headset = InTheHeadset();

        try
        {
            headset.Host.SetFullScreen(true);
            headset.Draw();
            Assert.True(headset.Panel.IsDirty || headset.Panel.VideoArea is null);

            headset.Picture.Source = Picture(1280, 720);
            headset.Draw();

            Assert.Equal((1024, 640), headset.Panel.DrawnSize);
            AreaIs(headset.Panel.VideoArea, 0, 0, 1024, 576);

            var row = Named<Border>(headset.View, VideoPlayerPage.FullScreenBandName);
            var box = new Rect(row.Bounds.Size).TransformToAABB(row.TransformToVisual(headset.View)!.Value);
            Assert.Equal(576, box.Top, 1);
            Assert.Equal(640, box.Bottom, 1);
            Assert.Equal(1024, box.Width, 1);
            Assert.Contains(row.GetVisualDescendants().OfType<Button>(), button => button.Name == VideoPlayerPage.ExitFullScreenName);

            headset.Panel.Board.Render().SaveCapture("media-movies-full-screen-big-panel.png");

            headset.Picture.Source = Picture(640, 480);
            headset.Draw();
            AreaIs(headset.Panel.VideoArea, 128, 0, 768, 576);
        }
        finally
        {
            headset.Finish();
        }
    }

    [AvaloniaFact]
    public void TheControllersBackLeavesFullScreenBeforeItGoesBackAPage()
    {
        var headset = InTheHeadset();

        try
        {
            headset.Picture.Source = Picture(1280, 720);
            headset.Host.SetFullScreen(true);
            headset.Draw();
            AreaIs(headset.Panel.VideoArea, 0, 0, 1024, 576);

            Assert.True(headset.Panel.Back());
            headset.Draw();

            Assert.False(headset.Host.FullScreen);
            Assert.Equal(VideoPlayerPage.CrumbOf(Steel).Key, headset.Panel.Nav.Trail[^1].Key);
            Assert.Equal((float)VideoPlayerPage.BigPictureWidth, Assert.IsType<VrPixelRect>(headset.Panel.VideoArea).Width, 1f);

            headset.Panel.Back();
            Assert.NotEqual(VideoPlayerPage.CrumbOf(Steel).Key, headset.Panel.Nav.Trail[^1].Key);
        }
        finally
        {
            headset.Finish();
        }
    }

    [AvaloniaFact]
    public void ExitFullScreenOnTheBigPanelReturnsToTheNormalPlayer()
    {
        var headset = InTheHeadset();

        try
        {
            headset.Host.SetFullScreen(true);
            headset.Draw();

            Press(Named<Button>(headset.View, VideoPlayerPage.ExitFullScreenName));
            headset.Draw();

            Assert.False(headset.Host.FullScreen);
            Assert.DoesNotContain(
                headset.View.GetVisualDescendants().OfType<Grid>(),
                grid => grid.Name == VideoPlayerPage.FullScreenLayerName);
            Assert.NotNull(Named<Border>(headset.View, "VideoFrame").Child);
        }
        finally
        {
            headset.Finish();
        }
    }

    [AvaloniaFact]
    public void LeavingTheBigPanelsPlayerOrMovingThePictureEndsFullScreen()
    {
        var headset = InTheHeadset();

        try
        {
            headset.Host.SetFullScreen(true);
            headset.Draw();
            Assert.Contains(headset.View.GetVisualDescendants().OfType<Grid>(), grid => grid.Name == VideoPlayerPage.FullScreenLayerName);

            Assert.True(headset.Panel.Nav.Select(PanelTab.Transcript));
            headset.Draw();

            Assert.False(headset.Host.FullScreen);
            Assert.DoesNotContain(headset.View.GetVisualDescendants().OfType<Grid>(), grid => grid.Name == VideoPlayerPage.FullScreenLayerName);

            Assert.True(headset.Panel.Nav.Select(PanelTab.Media));
            headset.Host.SetFullScreen(true);
            headset.Draw();
            Assert.Contains(headset.View.GetVisualDescendants().OfType<Grid>(), grid => grid.Name == VideoPlayerPage.FullScreenLayerName);

            headset.Host.MoveTo(VideoScreen.MiniPanel);
            headset.Host.MoveTo(VideoScreen.BigPanel);
            headset.Draw();

            Assert.False(headset.Host.FullScreen);
            Assert.DoesNotContain(headset.View.GetVisualDescendants().OfType<Grid>(), grid => grid.Name == VideoPlayerPage.FullScreenLayerName);
        }
        finally
        {
            headset.Finish();
        }
    }

    [AvaloniaFact]
    public void NormalSizeByVoiceReturnsTheBigPanelsPicture()
    {
        var headset = InTheHeadset();

        try
        {
            headset.Picture.Source = Picture(1280, 720);
            headset.Host.SetFullScreen(true);
            headset.Draw();
            AreaIs(headset.Panel.VideoArea, 0, 0, 1024, 576);

            Assert.Equal(FullScreenAnswer.Done, headset.Host.SetFullScreen(false));
            headset.Draw();

            Assert.Equal((float)VideoPlayerPage.BigPictureWidth, Assert.IsType<VrPixelRect>(headset.Panel.VideoArea).Width, 1f);
        }
        finally
        {
            headset.Finish();
        }
    }

    [AvaloniaFact]
    public void TheBigPanelsTileGoesFullScreenWhileAVideoIsLoaded()
    {
        if (!Decodes.Value)
        {
            Assert.Skip("Media Foundation cannot decode the fixture here.");
        }

        var headset = InTheHeadset(FileTitle(Clip), start: true);

        try
        {
            Until(() => headset.Host.Video.State is not (VideoState.Opening or null), "the clip opened");
            headset.Host.Pause();
            headset.Draw();

            var tile = Named<Button>(headset.View, VideoPlayerPage.FullScreenName);
            Assert.True(tile.IsEnabled);

            Press(tile);
            headset.Draw();

            Assert.True(headset.Host.FullScreen);
            Assert.Contains(
                headset.View.GetVisualDescendants().OfType<Grid>(),
                grid => grid.Name == VideoPlayerPage.FullScreenLayerName);
        }
        finally
        {
            headset.Finish();
        }
    }

    [AvaloniaFact]
    public void FullScreenByVoiceOpensTheBigPanelsPlayerFromAnotherPage()
    {
        var headset = InTheHeadset();

        try
        {
            Assert.True(headset.Panel.Nav.Select(PanelTab.Transcript));
            headset.Draw();

            Assert.Equal(FullScreenAnswer.Done, headset.Host.SetFullScreen(true));
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
            headset.Picture.Source = Picture(1280, 720);
            headset.Draw();

            Assert.Equal(VideoPlayerPage.CrumbOf(Steel).Key, headset.Panel.Nav.Trail[^1].Key);
            AreaIs(headset.Panel.VideoArea, 0, 0, 1024, 576);
        }
        finally
        {
            headset.Finish();
        }
    }
}
