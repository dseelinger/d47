using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>Media › Movies opens on the Watch page, a title's player sits one level below it, and the push-to-talk row is one setting (#2673).</summary>
[Trait("Category", "Integration")]
public sealed class TheMoviesPagesPlayAndKeepVideoTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

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

    private static readonly VideoTitle Moon = new(
        VideoSources.InternetArchive,
        "moon",
        "A Trip to the Moon (1902)",
        VideoKind.ShortFilm,
        "public domain",
        TimeSpan.FromSeconds(788),
        new Uri("https://archive.example.test/moon/moon.mp4"),
        null);

    private sealed class Fixture
    {
        public MemoryFileSystem Files { get; } = new();

        public SettingsService Settings { get; }

        public WatchStore Store { get; }

        public WatchHost Host { get; }

        public Fixture(bool catalogue = false, ISoundtrackCaptioner? captioner = null)
        {
            var (settings, _, _) = TestSurface.Create();
            Settings = settings;
            Store = new WatchStore("data/watch.json", Files, NullLogger<WatchStore>.Instance);
            var arbiter = new AudioArbiter(new SilentSink(), NullLogger<AudioArbiter>.Instance).Start();
            var video = new D47.App.Media.VideoPlayback(arbiter, background: work => work());

            Host = new WatchHost(video, Store, settings, Files, catalogue: catalogue, readSubtitles: (_, _) => Task.FromResult<string?>(null), background: work => work(), captioner: captioner);
        }
    }

    private static (Window Window, PanelView Panel) Open(Fixture fixture)
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };

        panel.EnableMedia(watch: fixture.Host);

        var window = new Window { Content = panel, Width = 1280, Height = 900 };

        window.Show();
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.MoviesRoot));
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static void Finish(Window window, Fixture fixture)
    {
        fixture.Host.Video.Stop();
        fixture.Host.Dispose();
        window.Close();
    }

    private static IReadOnlyList<string> Drawn(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    private static Button Named(Control root, string name) =>
        root.GetVisualDescendants().OfType<Button>().First(button => button.Name == name);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Keep(Fixture fixture)
    {
        fixture.Store.SavePlace(Steel.Source, Steel.Id, TimeSpan.FromSeconds(401), false, Noon, Steel);
        fixture.Store.SavePlace(Moon.Source, Moon.Id, TimeSpan.FromSeconds(182), true, Noon, Moon);
        fixture.Store.Add(Moon.Source, Moon.Id, Noon, Moon);
        fixture.Store.Add(WatchHost.FileSource, "C:/clips/orbit.mp4", Noon, fixture.Host.FileTitle("C:/clips/orbit.mp4"));
    }

    [AvaloniaFact]
    public void TheMoviesSubTabOpensTheWatchPageWithItsLists()
    {
        var fixture = new Fixture();
        Keep(fixture);
        var (window, panel) = Open(fixture);
        var drawn = Drawn(panel);

        Assert.Contains("CARRY ON WATCHING", drawn);
        Assert.Contains("TEARS OF STEEL", drawn);
        Assert.Contains("00:06:41 OF 00:12:14", drawn);
        Assert.Contains("YOUR LIST", drawn);
        Assert.Contains("A TRIP TO THE MOON (1902)", drawn);
        Assert.Contains("00:13:08", drawn);
        Assert.Contains("ORBIT.MP4", drawn);
        Assert.Contains("YOUR FILE · NO CAPTIONS YET", drawn);
        Assert.Contains("BLENDER STUDIO · CC BY 3.0 · SUBTITLE FILE", drawn);
        Assert.Contains("FIND A VIDEO", panel.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string));
        Assert.Contains("ADD A FILE FROM THIS COMPUTER", panel.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string));
        Assert.Equal("Search the catalogue", panel.FindControl<TextBox>("SearchInput")!.PlaceholderText);
        Assert.Equal(["Movies"], panel.Nav.Trail.Select(crumb => crumb.Word));

        window.SaveFrame("media-movies-watch.png");
        Finish(window, fixture);
    }

    [AvaloniaFact]
    public void ATitleStartedFromTheCatalogueOpensDirectlyBelowMovies()
    {
        var fixture = new Fixture(catalogue: true);
        var (window, panel) = Open(fixture);

        Assert.True(panel.Nav.Drill(WatchPage.CatalogueCrumb));
        Assert.True(WatchPage.OpenPlayer(panel.Nav, fixture.Host, Moon, start: false));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Movies", "A Trip to the Moon (1902)"], panel.Nav.Trail.Select(crumb => crumb.Word));
        Assert.True(panel.Nav.Back());
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Movies"], panel.Nav.Trail.Select(crumb => crumb.Word));
        Assert.Contains("FIND A VIDEO", Drawn(panel).Concat(panel.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string ?? string.Empty)));

        Finish(window, fixture);
    }

    [AvaloniaFact]
    public void PressingARowOpensThePlayerWithEveryPartOnIt()
    {
        var fixture = new Fixture();
        Keep(fixture);
        var (window, panel) = Open(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == WatchPage.RowName && (button.Tag as string) == "blender.tears"));

        Assert.Equal(["Movies", "Tears of Steel"], panel.Nav.Trail.Select(crumb => crumb.Word));

        var drawn = Drawn(panel).Select(text => text.ToUpperInvariant()).ToList();

        foreach (var expected in new[]
        {
            "YOUR PLACE", "LENGTH", "CREDIT", "LICENCE", "PLAYS FROM", "VIDEO", "PLAYS ON", "CAPTIONS", "SOUND", "VIDEO VOLUME",
            "00:06:41", "00:12:14", "CC BY 3.0", "MEDIA.EXAMPLE.TEST", "© BLENDER FOUNDATION · MANGO.BLENDER.ORG", "MP4",
        })
        {
            Assert.Contains(expected, drawn);
        }

        foreach (var name in new[] { VideoPlayerPage.BackName, VideoPlayerPage.PlayName, VideoPlayerPage.ForwardName, VideoPlayerPage.FromStartName })
        {
            Assert.NotNull(Named(panel, name));
        }

        Assert.DoesNotContain(drawn, text => text.Contains("PAUSE WHILE D47 SPEAKS", StringComparison.Ordinal));
        Assert.Contains(drawn, text => text == "PAUSE WHILE YOU HOLD PUSH-TO-TALK");

        window.SaveFrame("media-movies-player.png");
        Finish(window, fixture);
    }

    [AvaloniaFact]
    public void ThePlayerKeepsItsControlsWhileItIsRefreshed()
    {
        var fixture = new Fixture();
        Keep(fixture);
        var (window, panel) = Open(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == WatchPage.RowName && (button.Tag as string) == "archive.moon"));

        var player = panel.GetVisualDescendants().OfType<VideoPlayerPage>().First();
        var options = player.GetVisualDescendants().OfType<RadioButton>().ToList();
        var notice = player.GetVisualDescendants().OfType<D47.App.Controls.Notice>().ToList();

        player.Refresh();
        player.Refresh();

        Assert.Equal(options, player.GetVisualDescendants().OfType<RadioButton>());
        Assert.Equal(notice, player.GetVisualDescendants().OfType<D47.App.Controls.Notice>());
        Finish(window, fixture);
    }

    [AvaloniaFact]
    public void ThePlayerCarriesThePageSettingsTileWhenAStripIsGiven()
    {
        var fixture = new Fixture();
        var strip = new SettingsStripStub();

        Assert.Same(strip.Tool, new VideoPlayerPage(fixture.Host, Steel, strip).BarTool);
        Assert.Null(new VideoPlayerPage(fixture.Host, Steel).BarTool);
        fixture.Host.Dispose();
    }

    private sealed class SettingsStripStub : ContentControl, IPageChrome
    {
        public Control Tool { get; } = new Button();

        public Control? BarTool => Tool;

        public string? HelpTopic => null;
    }

    [AvaloniaFact]
    public void ThePushToTalkRowOnThePlayerAndTheSettingFollowEachOther()
    {
        var fixture = new Fixture();
        Keep(fixture);
        var (window, panel) = Open(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == WatchPage.RowName && (button.Tag as string) == "blender.tears"));

        var box = panel.GetVisualDescendants().OfType<CheckBox>().First(check => check.Name == VideoPlayerPage.PushToTalkName);
        Assert.True(box.IsChecked);
        Assert.True(fixture.Settings.Current.Watch.PauseForPushToTalk);

        box.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(fixture.Settings.Current.Watch.PauseForPushToTalk);

        fixture.Settings.Apply(AudioCapability.VideoPushToTalkKey, "true", SettingsCaller.Panel);
        Dispatcher.UIThread.RunJobs();
        Assert.True(box.IsChecked);

        Finish(window, fixture);
    }

    [AvaloniaFact]
    public void ATitleWithNoSubtitleFileSaysSoAndOffersNoCaptions()
    {
        var fixture = new Fixture();
        Keep(fixture);
        var (window, panel) = Open(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == WatchPage.RowName && (button.Tag as string) == "archive.moon"));

        var drawn = Drawn(panel).Select(text => text.ToUpperInvariant()).ToList();

        Assert.Contains("NO SUBTITLE FILE", drawn);
        Assert.Contains("NONE FOR THIS TITLE", drawn);
        Assert.Contains("PUBLIC DOMAIN", drawn);

        Finish(window, fixture);
    }

    [AvaloniaFact]
    public void WriteCaptionsOnTheNoticeWritesThemFromTheSoundtrackAndSaysHowFarTheyReach()
    {
        var clip = Path.Combine(AppContext.BaseDirectory, "Fixtures", "video", "clip.mp4");
        var fixture = new Fixture(captioner: new WritesOneCue(null));
        var title = fixture.Host.FileTitle(clip);
        fixture.Store.Add(title.Source, title.Id, Noon, title);
        var (window, panel) = Open(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == WatchPage.RowName && (button.Tag as string) == "file." + clip));
        Click(Named(panel, VideoPlayerPage.WriteCaptionsName));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!fixture.Host.WrittenToEnd)
        {
            Assert.True(DateTime.UtcNow < deadline, "The captions were not written.");
            Thread.Sleep(10);
        }

        panel.GetVisualDescendants().OfType<VideoPlayerPage>().Single().Refresh();
        Dispatcher.UIThread.RunJobs();
        var drawn = Drawn(panel).Select(text => text.ToUpperInvariant()).ToList();

        Assert.True(fixture.Host.Writing);
        Assert.Equal("Words in the soundtrack.", Assert.Single(fixture.Host.Cues).Text);
        Assert.False(Named(panel, VideoPlayerPage.WriteCaptionsName).IsEffectivelyVisible);
        Assert.True(panel.GetVisualDescendants().OfType<StackPanel>().Single(stack => stack.Name == VideoPlayerPage.WrittenName).IsVisible);
        Assert.Contains("CAPTIONS WRITTEN TO", drawn);
        Assert.Contains(drawn, text => text.StartsWith("THE END · PLAYING ", StringComparison.Ordinal));
        Assert.Equal(1, panel.GetVisualDescendants().OfType<Segment>().Single(choice => choice.Name == VideoPlayerPage.CaptionsChoiceName).SelectedIndex);

        Finish(window, fixture);
    }

    [AvaloniaFact]
    public void TheBigPanelStacksTheWrittenReadoutInItsSideColumnWhileCaptionsAreWritten()
    {
        var clip = Path.Combine(AppContext.BaseDirectory, "Fixtures", "video", "clip.mp4");
        var fixture = new Fixture(captioner: new WritesOneCue(null));
        var title = fixture.Host.FileTitle(clip);
        fixture.Store.Add(title.Source, title.Id, Noon, title);
        var (window, panel) = Open(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == WatchPage.RowName && (button.Tag as string) == "file." + clip));
        Click(Named(panel, VideoPlayerPage.WriteCaptionsName));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!fixture.Host.WrittenToEnd)
        {
            Assert.True(DateTime.UtcNow < deadline, "The captions were not written.");
            Thread.Sleep(10);
        }

        var big = new VideoPlayerPage(fixture.Host, title, null, big: true);
        var bigWindow = new Window { Content = big, Width = 1000, Height = 640 };

        bigWindow.Show();
        big.Refresh();
        Dispatcher.UIThread.RunJobs();

        var written = big.GetVisualDescendants().OfType<StackPanel>().Single(stack => stack.Name == VideoPlayerPage.WrittenName);
        var heading = Assert.IsType<StackPanel>(written.Children[0]);
        var texts = heading.Children.OfType<TextBlock>().Select(block => block.Text).ToList();

        Assert.True(written.IsVisible);
        Assert.Equal("CAPTIONS WRITTEN TO", texts[0]);
        Assert.StartsWith("the end · playing ", texts[1], StringComparison.Ordinal);
        Assert.Equal(heading.Bounds.X, written.Children[1].Bounds.X);

        bigWindow.Close();
        Finish(window, fixture);
    }

    [AvaloniaFact]
    public void WithNoSpeechModelCaptionsCannotBeWritten()
    {
        var fixture = new Fixture(captioner: new WritesOneCue("Writing captions needs a speech model on this computer."));
        Keep(fixture);
        var (window, panel) = Open(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == WatchPage.RowName && (button.Tag as string) == "archive.moon"));

        Assert.False(Named(panel, VideoPlayerPage.WriteCaptionsName).IsEnabled);
        Assert.Contains("NEEDS A SPEECH MODEL", Drawn(panel).Select(text => text.ToUpperInvariant()));
        Assert.False(fixture.Host.Writing);

        Finish(window, fixture);
    }

    private sealed class WritesOneCue(string? unavailable) : ISoundtrackCaptioner
    {
        public string? Unavailable => unavailable;

        public Task<IReadOnlyList<D47.Core.Vr.SubtitleCue>> CaptionAsync(D47.Core.Listening.Utterance sound, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<D47.Core.Vr.SubtitleCue>>([new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "Words in the soundtrack.")]);

        public void Release()
        {
        }
    }

    [AvaloniaFact]
    public void TheWriteChoiceOpensOnceASpeechModelIsReady()
    {
        var captioner = new Switchable { Unavailable = "Writing captions needs a speech model on this computer." };
        var fixture = new Fixture(captioner: captioner);
        Keep(fixture);
        var (window, panel) = Open(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == WatchPage.RowName && (button.Tag as string) == "archive.moon"));

        var choice = panel.GetVisualDescendants().OfType<Segment>().Single(segment => segment.Name == VideoPlayerPage.CaptionsChoiceName);

        Assert.False(Named(panel, VideoPlayerPage.WriteCaptionsName).IsEnabled);
        Assert.False(choice.GetVisualDescendants().OfType<RadioButton>().ElementAt(1).IsEnabled);

        captioner.Unavailable = null;
        panel.GetVisualDescendants().OfType<VideoPlayerPage>().Single().Refresh();
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named(panel, VideoPlayerPage.WriteCaptionsName).IsEnabled);
        Assert.True(choice.GetVisualDescendants().OfType<RadioButton>().ElementAt(1).IsEnabled);

        Finish(window, fixture);
    }

    [AvaloniaFact]
    public void PickingWriteAgainAfterItFailedStartsWritingAgain()
    {
        var clip = Path.Combine(AppContext.BaseDirectory, "Fixtures", "video", "clip.mp4");
        var captioner = new Switchable { Fails = true };
        var fixture = new Fixture(captioner: captioner);
        var title = fixture.Host.FileTitle(clip);
        fixture.Store.Add(title.Source, title.Id, Noon, title);
        var (window, panel) = Open(fixture);

        Click(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == WatchPage.RowName && (button.Tag as string) == "file." + clip));
        Click(Named(panel, VideoPlayerPage.WriteCaptionsName));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (fixture.Host.WritingProblem is null)
        {
            Assert.True(DateTime.UtcNow < deadline, "Writing did not fail.");
            Thread.Sleep(10);
        }

        var calls = captioner.Calls;
        var page = panel.GetVisualDescendants().OfType<VideoPlayerPage>().Single();
        page.Refresh();
        Dispatcher.UIThread.RunJobs();

        var write = panel.GetVisualDescendants().OfType<Segment>().Single(segment => segment.Name == VideoPlayerPage.CaptionsChoiceName)
            .GetVisualDescendants().OfType<RadioButton>().ElementAt(1);

        write.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (captioner.Calls == calls)
        {
            Assert.True(DateTime.UtcNow < deadline, "Writing did not start again.");
            Thread.Sleep(10);
        }

        Finish(window, fixture);
    }

    private sealed class Switchable : ISoundtrackCaptioner
    {
        private int _calls;

        public string? Unavailable { get; set; }

        public bool Fails { get; set; }

        public int Calls => Volatile.Read(ref _calls);

        public Task<IReadOnlyList<D47.Core.Vr.SubtitleCue>> CaptionAsync(D47.Core.Listening.Utterance sound, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);

            return Fails
                ? Task.FromException<IReadOnlyList<D47.Core.Vr.SubtitleCue>>(new InvalidOperationException("The speech model failed."))
                : Task.FromResult<IReadOnlyList<D47.Core.Vr.SubtitleCue>>([]);
        }

        public void Release()
        {
        }
    }

    [AvaloniaFact]
    public void TheSearchOpensTheCatalogueOnlyWhereOneExists()
    {
        var without = new Fixture();
        var (windowWithout, panelWithout) = Open(without);
        var page = (WatchPage)panelWithout.GetVisualDescendants().OfType<WatchPage>().First();

        Assert.False(page.Submit("moon"));
        Assert.Equal(["Movies"], panelWithout.Nav.Trail.Select(crumb => crumb.Word));
        Finish(windowWithout, without);

        var with = new Fixture(catalogue: true);
        var (windowWith, panelWith) = Open(with);
        var found = panelWith.GetVisualDescendants().OfType<WatchPage>().First();

        Assert.True(found.Submit("  moon "));
        Assert.Equal("moon", with.Host.CatalogueQuery);
        Assert.Equal(["Movies", "Catalogue"], panelWith.Nav.Trail.Select(crumb => crumb.Word));
        Finish(windowWith, with);
    }

    [AvaloniaFact]
    public void AFileChosenOnThisComputerJoinsYourListAndOpensItsPlayer()
    {
        var fixture = new Fixture();
        var (window, panel) = Open(fixture);
        var path = Path.Combine(Path.GetTempPath(), "orbit-timelapse.mp4");
        var page = panel.GetVisualDescendants().OfType<WatchPage>().First();
        page.ChooseFile = () => Task.FromResult<string?>(path);

        Click(Named(panel, WatchPage.AddFileName));

        Assert.Contains(fixture.Store.YourList(), entry => entry.Source == WatchHost.FileSource && entry.Id == path);
        Assert.Equal(["Movies", "orbit-timelapse.mp4"], panel.Nav.Trail.Select(crumb => crumb.Word));

        Finish(window, fixture);
    }
}
