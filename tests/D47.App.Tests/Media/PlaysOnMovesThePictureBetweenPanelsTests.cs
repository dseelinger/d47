using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Media;
using D47.App.Panel;
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
/// Plays on puts the picture on the window, the big panel or the mini panels without touching the video; the big
/// panel draws the 640×360 picture the headset's quad covers, the mini panel the picture beside the avatar, and the
/// caption band follows the video drawn in the headset (#2675).
/// </summary>
[Trait("Category", "Integration")]
public sealed class PlaysOnMovesThePictureBetweenPanelsTests
{
    private static readonly string Clip = Path.Combine(AppContext.BaseDirectory, "Fixtures", "video", "clip.mp4");

    private static readonly string Missing = Path.Combine(AppContext.BaseDirectory, "Fixtures", "video", "not-here.mp4");

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

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
        new Uri("https://media.example.test/tears_of_steel.srt"),
        "© Blender Foundation · mango.blender.org");

    private const string Subtitles = "1\r\n00:00:04,000 --> 00:00:09,000\r\nThe reactor is stable for now.\r\n";

    private static (SettingsService Settings, WatchHost Host) Watch(Action<SettingsService>? configure = null)
    {
        var files = new MemoryFileSystem();
        var (settings, _, _) = TestSurface.Create();
        configure?.Invoke(settings);

        var store = new WatchStore("data/watch.json", files, NullLogger<WatchStore>.Instance);
        var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
        var video = new VideoPlayback(arbiter, background: work => work());

        return (settings, new WatchHost(video, store, settings, files, readSubtitles: (_, _) => Task.FromResult<string?>(Subtitles), background: work => work()));
    }

    private static VideoTitle FileTitle(string path) => new(
        WatchHost.FileSource,
        path,
        Path.GetFileName(path),
        VideoKind.Other,
        string.Empty,
        null,
        new Uri(path, UriKind.Absolute),
        new Uri(Path.ChangeExtension(path, ".srt"), UriKind.Absolute));

    private static void Until(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting until {what}.");
            Thread.Sleep(10);
        }
    }

    private static WriteableBitmap Picture() =>
        new(new PixelSize(1280, 720), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    private static IReadOnlyList<string> Drawn(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void ChangingPlaysOnLeavesTheVideoWhereItWas()
    {
        var (_, host) = Watch();
        var title = FileTitle(Missing);
        var starts = 0;
        host.Video.Starting += () => starts++;

        try
        {
            Assert.True(host.Play(title, TimeSpan.FromMinutes(5)));
            var source = host.Video.Source;
            var place = host.Video.Position;
            starts = 0;

            host.MoveTo(VideoScreen.MiniPanel);
            host.MoveTo(VideoScreen.BigPanel);
            host.MoveTo(VideoScreen.Window);

            Assert.Equal(0, starts);
            Assert.Equal(source, host.Video.Source);
            Assert.Equal(place, host.Video.Position);
            Assert.Equal(VideoScreen.Window, host.PlaysOn);
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
        }
    }

    private static void SetMode(SettingsService settings, string mode) =>
        settings.Replace("test", current => current with { Vr = current.Vr with { Mode = mode } });

    [AvaloniaFact]
    public void AVideoStartedWhileTheHeadsetIsUpPlaysOnThePanelTheModeNames()
    {
        var (settings, host) = Watch(settings => SetMode(settings, "full"));
        var moved = new List<VideoScreen>();
        host.Moved += moved.Add;

        try
        {
            host.FollowHeadset(true);
            Assert.True(host.Play(FileTitle(Missing)));
            Assert.Equal(VideoScreen.BigPanel, host.PlaysOn);
            Assert.Equal([VideoScreen.BigPanel], moved);

            SetMode(settings, "mini");
            Assert.True(host.Play(FileTitle(Clip)));
            Assert.Equal(VideoScreen.MiniPanel, host.PlaysOn);
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
        }
    }

    [AvaloniaFact]
    public void AVideoStartedFromTheWindowsPlayerPageOpensTheHeadsetBigPanelsPlayer()
    {
        var (settings, host) = Watch(settings => SetMode(settings, "full"));
        var panel = new VrPanelSurface(new PanelViewModel(), settings, _ => null, watch: host) { Enabled = true };

        try
        {
            panel.Configure();
            panel.ApplyMode();
            Dispatcher.UIThread.RunJobs();
            Assert.True(panel.Nav.Select(PanelTab.Media));
            Draw(panel);
            host.FollowHeadset(true);

            Assert.True(host.Play(FileTitle(Missing)));
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

            Assert.Equal(VideoPlayerPage.CrumbOf(host.Current!).Key, panel.Nav.Trail[^1].Key);
            Assert.Equal(VideoScreen.BigPanel, host.PlaysOn);
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
            panel.Dispose();
        }
    }

    [AvaloniaFact]
    public void ChangingThePanelModeMovesThePictureWithoutLosingThePlace()
    {
        var (settings, host) = Watch(settings => SetMode(settings, "full"));
        var starts = 0;
        host.Video.Starting += () => starts++;

        try
        {
            host.FollowHeadset(true);
            Assert.True(host.Play(FileTitle(Missing), TimeSpan.FromMinutes(5)));
            var source = host.Video.Source;
            var place = host.Video.Position;
            starts = 0;

            SetMode(settings, "mini");
            Assert.Equal(VideoScreen.MiniPanel, host.PlaysOn);

            SetMode(settings, "full");
            Assert.Equal(VideoScreen.BigPanel, host.PlaysOn);

            Assert.Equal(0, starts);
            Assert.Equal(source, host.Video.Source);
            Assert.Equal(place, host.Video.Position);
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
        }
    }

    [AvaloniaFact]
    public void ASessionThatStartsOrEndsMovesThePictureAndKeepsTheVideoPlaying()
    {
        var (_, host) = Watch(settings => SetMode(settings, "mini"));

        try
        {
            Assert.True(host.Play(FileTitle(Missing), TimeSpan.FromMinutes(5)));
            var source = host.Video.Source;
            var place = host.Video.Position;
            Assert.Equal(VideoScreen.Window, host.PlaysOn);

            host.FollowHeadset(true);
            Assert.Equal(VideoScreen.MiniPanel, host.PlaysOn);

            host.FollowHeadset(false);
            Assert.Equal(VideoScreen.Window, host.PlaysOn);
            Assert.Equal(source, host.Video.Source);
            Assert.Equal(place, host.Video.Position);
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
        }
    }

    [AvaloniaFact]
    public void WithNoHeadsetSessionAVideoStaysOnTheWindowWhateverThePanelMode()
    {
        var (settings, host) = Watch(settings => SetMode(settings, "full"));

        try
        {
            Assert.True(host.Play(FileTitle(Missing)));
            SetMode(settings, "mini");
            SetMode(settings, "full");

            Assert.Equal(VideoScreen.Window, host.PlaysOn);
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
        }
    }

    [AvaloniaFact]
    public void ThisWindowHoldsUntilThePanelModeChangesTheSessionEndsOrAnotherVideoStarts()
    {
        var (settings, host) = Watch(settings => SetMode(settings, "full"));

        try
        {
            host.FollowHeadset(true);
            Assert.True(host.Play(FileTitle(Missing)));
            host.MoveTo(VideoScreen.Window);

            settings.Replace("test", current => current with { Watch = current.Watch with { PauseForPushToTalk = !current.Watch.PauseForPushToTalk } });
            Assert.Equal(VideoScreen.Window, host.PlaysOn);

            SetMode(settings, "mini");
            Assert.Equal(VideoScreen.MiniPanel, host.PlaysOn);

            host.MoveTo(VideoScreen.Window);
            Assert.True(host.Play(FileTitle(Clip)));
            Assert.Equal(VideoScreen.MiniPanel, host.PlaysOn);

            host.MoveTo(VideoScreen.Window);
            host.FollowHeadset(false);
            Assert.Equal(VideoScreen.Window, host.PlaysOn);
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
        }
    }

    [AvaloniaFact]
    public void APanelPutsTheHeadsetInItsModeOnlyWhileTheHeadsetIsOn()
    {
        var (off, hostOff) = Watch();
        var before = off.Current.Vr.Mode;
        hostOff.MoveTo(VideoScreen.MiniPanel);
        Assert.Equal(before, off.Current.Vr.Mode);
        hostOff.Dispose();

        var (on, hostOn) = Watch(settings => settings.Replace("test", current => current with { Vr = current.Vr with { Enabled = true, Mode = "full" } }));
        hostOn.MoveTo(VideoScreen.MiniPanel);
        Assert.Equal("mini", on.Current.Vr.Mode);
        hostOn.MoveTo(VideoScreen.BigPanel);
        Assert.Equal("full", on.Current.Vr.Mode);
        hostOn.MoveTo(VideoScreen.Window);
        Assert.Equal("full", on.Current.Vr.Mode);
        hostOn.Dispose();
    }

    [AvaloniaFact]
    public void TheWindowsPlayerDrawsThePictureOnlyWhileItPlaysThere()
    {
        var (_, host) = Watch();
        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableMedia(watch: host);
        var window = new Window { Content = panel, Width = 1280, Height = 900 };
        window.Show();

        try
        {
            panel.Tab = PanelTab.Media;
            Assert.True(WatchPage.OpenPlayer(panel.Nav, host, Steel, start: false));
            Dispatcher.UIThread.RunJobs();

            var choice = panel.GetVisualDescendants().OfType<Segment>().First(segment => segment.Name == VideoPlayerPage.PlaysOnName);
            var picture = panel.GetVisualDescendants().OfType<Image>().First(image => image.Name == VideoPlayerPage.PictureName);

            Assert.All(choice.GetVisualDescendants().OfType<RadioButton>(), option => Assert.True(option.IsEnabled));
            Assert.Equal(0, choice.SelectedIndex);
            Assert.True(picture.IsVisible);

            Choose(choice, 2);

            Assert.Equal(VideoScreen.MiniPanel, host.PlaysOn);
            Assert.False(picture.IsVisible);
            Assert.Contains("Playing on the mini panel.", Drawn(panel));
            window.SaveFrame("media-movies-player-plays-on-mini.png");

            Choose(choice, 0);

            Assert.Equal(VideoScreen.Window, host.PlaysOn);
            Assert.True(picture.IsVisible);
        }
        finally
        {
            host.Dispose();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheBigPanelDrawsThePictureTheQuadCoversAtItsOwnSize()
    {
        var (settings, host) = Watch(settings => settings.Replace("test", current => current with { Vr = current.Vr with { Mode = "full" } }));
        var panel = new VrPanelSurface(new PanelViewModel(), settings, _ => null, watch: host) { Enabled = true };

        try
        {
            panel.Configure();
            panel.ApplyMode();
            Dispatcher.UIThread.RunJobs();
            Assert.True(panel.Nav.Select(PanelTab.Media));
            Assert.True(WatchPage.OpenPlayer(panel.Nav, host, Steel, start: false));
            host.MoveTo(VideoScreen.BigPanel);
            Dispatcher.UIThread.RunJobs();

            Draw(panel);
            var view = panel.Board.View;
            var choice = view.GetVisualDescendants().OfType<Segment>().First(segment => segment.Name == VideoPlayerPage.PlaysOnName);
            Assert.Equal(["BIG PANEL", "MINI PANEL"], choice.ItemsSource!.Cast<string>());
            Assert.Equal(0, choice.SelectedIndex);

            view.GetVisualDescendants().OfType<Image>().First(image => image.Name == VideoPlayerPage.PictureName).Source = Picture();
            Draw(panel);

            var area = Assert.IsType<VrPixelRect>(panel.VideoArea);
            Assert.Equal(16f / 9f, area.Width / area.Height, 2);

            foreach (var tile in view.GetVisualDescendants().OfType<Button>().Where(button => button.Name is VideoPlayerPage.BackName or VideoPlayerPage.PlayName or VideoPlayerPage.ForwardName))
            {
                var box = new Rect(tile.Bounds.Size).TransformToAABB(tile.TransformToVisual(view)!.Value);
                Assert.True(box.Bottom <= panel.DrawnSize.Height, $"{tile.Name} is drawn below the panel's edge.");
            }

            panel.Board.Render().SaveCapture("media-movies-big-panel.png");

            host.MoveTo(VideoScreen.Window);
            Dispatcher.UIThread.RunJobs();
            panel.TickMedia();
            Assert.True(panel.IsDirty);
            Draw(panel);

            Assert.Null(panel.VideoArea);
            Assert.Equal(-1, choice.SelectedIndex);
        }
        finally
        {
            host.Dispose();
            panel.Dispose();
        }
    }

    [AvaloniaFact]
    public void TheHeadsetsMiniPanelDrawsThePictureBesideTheAvatar()
    {
        if (!Decodes.Value)
        {
            Assert.Skip("Media Foundation cannot decode the fixture here.");
        }

        var (settings, host) = Watch(settings => settings.Replace("test", current => current with { Vr = current.Vr with { Mode = "mini" } }));
        var panel = new VrPanelSurface(new PanelViewModel(), settings, _ => null, watch: host) { Enabled = true };

        try
        {
            panel.Configure();
            panel.ApplyMode();
            Dispatcher.UIThread.RunJobs();
            Assert.True(panel.Nav.Select(PanelTab.Media));
            host.MoveTo(VideoScreen.MiniPanel);
            Assert.True(WatchPage.OpenPlayer(panel.Nav, host, FileTitle(Clip), start: true, headset: true));
            Until(() => host.Video.State is not (VideoState.Opening or null), "the clip opened");
            Assert.Null(host.Video.Failure);
            Dispatcher.UIThread.RunJobs();
            Draw(panel);

            var mini = panel.Board.View.GetVisualDescendants().OfType<VideoMini>().Single();
            var picture = mini.GetVisualDescendants().OfType<Image>().Single(image => image.Name == VideoPlayerPage.PictureName);
            Assert.Equal(VideoScreen.MiniPanel, host.PlaysOn);

            picture.Source ??= Picture();
            panel.TickMedia();
            Draw(panel);

            var area = Assert.IsType<VrPixelRect>(panel.VideoArea);
            Assert.InRange(area.Height, panel.DrawnSize.Height - 2, panel.DrawnSize.Height);
            Assert.True(area.X > panel.DrawnSize.Height, "The picture sits beside the avatar, not over it.");
            Assert.DoesNotContain(mini.GetVisualDescendants().OfType<TextBlock>(), block => Shown(block, mini) && block.Text is { Length: > 0 });
            panel.Board.Render().SaveCapture("media-movies-mini-panel.png");
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
            panel.Dispose();
        }
    }

    [AvaloniaFact]
    public void TheDesktopMiniPanelDrawsTheCaptionOverThePicture()
    {
        if (!Decodes.Value)
        {
            Assert.Skip("Media Foundation cannot decode the fixture here.");
        }

        var (_, host) = Watch();
        var mini = new VideoMini(host, headset: false);
        var window = new Window { Content = mini, Width = 511, Height = 280 };
        window.Show();

        try
        {
            host.MoveTo(VideoScreen.MiniPanel);
            Assert.True(host.Play(FileTitle(Clip), TimeSpan.FromSeconds(5)));
            Until(() => host.Video.State is not (VideoState.Opening or null), "the clip opened");
            Until(() => !host.CuesLoading, "the subtitles were read");
            Dispatcher.UIThread.RunJobs();
            mini.Tick();
            Dispatcher.UIThread.RunJobs();

            Assert.Contains("The reactor is stable for now.", Drawn(mini));
            window.SaveFrame("media-movies-mini-overlay.png");
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AStoppedVideoOnTheMiniPanelGivesThePhraseToTryAgain()
    {
        var (_, host) = Watch();
        var mini = new VideoMini(host, headset: true);
        var window = new Window { Content = mini, Width = 511, Height = 280 };
        window.Show();

        try
        {
            Assert.Contains(VideoMini.NothingPlaying, Drawn(mini));

            host.MoveTo(VideoScreen.MiniPanel);
            Assert.True(host.Play(FileTitle(Missing)));
            Until(() => host.Video.Failure is not null, "the missing file failed");
            Dispatcher.UIThread.RunJobs();
            mini.Tick();

            var notice = mini.GetVisualDescendants().OfType<Notice>().Single(found => found.IsEffectivelyVisible);
            Assert.Equal(VideoPlayerPage.StoppedLabel, notice.Label);
            Assert.Contains("Say “continue watching” to try again.", notice.Text, StringComparison.Ordinal);
            Assert.Empty(notice.Actions);
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TheCaptionBandFollowsTheVideoOnlyWhileTheHeadsetDrawsIt()
    {
        var (_, host) = Watch();
        var layer = new CaptionLayer();
        var captions = new VideoCaptions(layer);

        try
        {
            Assert.True(host.Play(FileTitle(Missing), TimeSpan.FromSeconds(5)));
            Until(() => !host.CuesLoading, "the subtitles were read");

            captions.Serve(host, drawn: true);
            Assert.Equal(["The reactor is stable for now."], layer.Lines);

            captions.Serve(host, drawn: false);
            Assert.Empty(layer.Lines);

            captions.Serve(host, drawn: true);
            host.CaptionsOn = false;
            captions.Serve(host, drawn: true);
            Assert.Empty(layer.Lines);
        }
        finally
        {
            host.Video.Stop();
            host.Dispose();
        }
    }

    /// <summary>Whether every control from <paramref name="control"/> up to <paramref name="root"/> is visible; a headset surface is never shown, so IsEffectivelyVisible is false there.</summary>
    private static bool Shown(Visual control, Visual root)
    {
        for (var at = control; at is not null && !ReferenceEquals(at, root); at = at.GetVisualParent())
        {
            if (!at.IsVisible)
            {
                return false;
            }
        }

        return true;
    }

    private static void Choose(Segment segment, int index)
    {
        segment.GetVisualDescendants().OfType<RadioButton>().ElementAt(index).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

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
}
