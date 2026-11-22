using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Windowing;
using D47.Core.Audio;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>Media › Music draws the music's state and its tiles control it; the mini panel reads it out while the surface is on it (#2683).</summary>
[Trait("Category", "Integration")]
public sealed class TheMusicPageShowsTheMusicTests
{
    private static void Jobs() => Dispatcher.UIThread.RunJobs();

    private static MusicState Playing => new("long-drift-04.mp3", true, false, false, "supercruise");

    internal sealed class Shown
    {
        public required PanelView Panel { get; init; }

        public required Window Window { get; init; }

        public required PanelViewModel Model { get; init; }

        public List<MusicAction> Actions { get; } = [];

        public List<TimeSpan> Seeks { get; } = [];

        public int Downs { get; set; }

        public MusicPage Page => Panel.GetVisualDescendants().OfType<MusicPage>().Single();
    }

    /// <summary>A panel furnished with Media, on Media › Music.</summary>
    internal static Shown Open(MusicState state, Func<TrackProgress?>? progress = null, bool headset = false, PanelMode mode = PanelMode.Full)
    {
        new D47.App.Theming.ThemeManager(Application.Current!, Microsoft.Extensions.Logging.Abstractions.NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var model = new PanelViewModel { AudioPlayer = AudioPlayerReadout.From(state, false), AudioProgress = progress };
        var panel = new PanelView { DataContext = model, Mode = mode };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        Shown? shown = null;
        var host = new MusicHost(
            TestSurface.Settings(),
            action =>
            {
                shown!.Actions.Add(action);
                return string.Empty;
            },
            () => shown!.Downs++,
            () => progress?.Invoke(),
            to =>
            {
                shown!.Seeks.Add(to);
                return true;
            });

        panel.EnableMedia(music: host);

        var window = new Window { Content = panel, Width = headset ? 1024 : 1280, Height = mode == PanelMode.Mini ? 280 : 900 };
        window.Show();
        shown = new Shown { Panel = panel, Window = window, Model = model };

        Assert.True(panel.Nav.Show(MediaFurnishing.MusicRoot));
        Jobs();

        return shown;
    }

    internal static T Named<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Press(Button button) =>
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void APlayingTrackShowsItsNameFolderAndStateAndTheTilesControlIt()
    {
        var shown = Open(Playing);
        var page = shown.Page;

        Assert.Equal("long-drift-04.mp3", Named<TextBlock>(page, MusicPage.TrackName).Text);
        Assert.Equal("supercruise", Named<TextBlock>(page, MusicPage.FolderName).Text);
        Assert.Equal("PLAYING", Named<TextBlock>(page, MusicPage.StateName).Text);
        Assert.Equal("PAUSE", Named<Button>(page, MusicPage.PlayName).Content);

        Press(Named<Button>(page, MusicPage.PlayName));
        Press(Named<Button>(page, MusicPage.NextName));
        Press(Named<Button>(page, MusicPage.PreviousName));
        Press(Named<Button>(page, MusicPage.ThumbsDownName));

        Assert.Equal([MusicAction.Pause, MusicAction.Next, MusicAction.Previous], shown.Actions);
        Assert.Equal(1, shown.Downs);

        shown.Window.SaveFrame("music-page-playing.png");
    }

    [AvaloniaFact]
    public void ThePageFollowsTheMusicWhenItPausesAndWhenItStops()
    {
        var shown = Open(Playing);
        var page = shown.Page;

        shown.Model.AudioPlayer = AudioPlayerReadout.From(new MusicState("long-drift-04.mp3", false, true, false, "supercruise"), false);
        Jobs();

        Assert.Equal("PAUSED", Named<TextBlock>(page, MusicPage.StateName).Text);
        Assert.Equal("PLAY", Named<Button>(page, MusicPage.PlayName).Content);

        Press(Named<Button>(page, MusicPage.PlayName));
        Assert.Equal([MusicAction.Resume], shown.Actions);

        shown.Model.AudioPlayer = AudioPlayerReadout.From(new MusicState(null, false, false), false);
        Jobs();

        Assert.Equal("NOTHING PLAYING", Named<TextBlock>(page, MusicPage.StateName).Text);
        Assert.Equal(AudioPlayerReadout.Nothing, Named<TextBlock>(page, MusicPage.TrackName).Text);

        foreach (var name in new[] { MusicPage.PreviousName, MusicPage.PlayName, MusicPage.NextName, MusicPage.ThumbsDownName })
        {
            Assert.False(Named<Button>(page, name).IsEnabled, name);
        }
    }

    [AvaloniaFact]
    public void WithEliteClosedPlayAndNextStartTheLastFolder()
    {
        var shown = Open(new MusicState(null, false, false, Folder: "supercruise", Startable: true));
        var page = shown.Page;

        Assert.Equal("supercruise", Named<TextBlock>(page, MusicPage.FolderName).Text);
        Assert.Equal("PLAY", Named<Button>(page, MusicPage.PlayName).Content);
        Assert.True(Named<Button>(page, MusicPage.PlayName).IsEnabled);
        Assert.True(Named<Button>(page, MusicPage.NextName).IsEnabled);
        Assert.False(Named<Button>(page, MusicPage.PreviousName).IsEnabled);
        Assert.False(Named<Button>(page, MusicPage.ThumbsDownName).IsEnabled);

        Press(Named<Button>(page, MusicPage.PlayName));
        Assert.Equal([MusicAction.Resume], shown.Actions);
    }

    [AvaloniaFact]
    public void MutedLeavesTheTilesOffAndTheLevelPressable()
    {
        var shown = Open(Playing);
        var page = shown.Page;

        shown.Model.AudioPlayer = AudioPlayerReadout.From(Playing, muted: true);
        Jobs();

        Assert.Equal("MUTED", Named<TextBlock>(page, MusicPage.StateName).Text);
        Assert.False(Named<Button>(page, MusicPage.PlayName).IsEnabled);
        Assert.True(Named<Level>(page, MusicPage.LevelName).IsEnabled);
    }

    [AvaloniaFact]
    public void TheHintNamesTheBoundKeyOnTheWindowAndIsNotDrawnOnTheBigPanel()
    {
        var shown = Open(Playing);
        var hint = Named<StackPanel>(shown.Page, MusicPage.HintName);

        Assert.True(hint.IsVisible);
        Assert.Contains("CTRL+SHIFT+P", hint.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));

        var big = Open(Playing, headset: true);

        Assert.DoesNotContain(big.Page.GetVisualDescendants().OfType<StackPanel>(), panel => panel.Name == MusicPage.HintName);
        big.Window.SaveFrame("music-page-big-panel.png");
    }

    [AvaloniaFact]
    public void TheTitleLineCarriesTheSummaryAndTheMusicHelp()
    {
        var page = Open(Playing).Page;

        Assert.Equal(MusicPage.PageSummary, ((IPageSummary)page).Summary);
        Assert.Equal(D47.Core.Capabilities.Builtin.AudioCapability.Id, page.HelpTopic);
    }

    [AvaloniaFact]
    public void TheMusicRootIsFirstOnTheMediaTab()
    {
        var shown = Open(Playing);

        Assert.Equal(
            [MediaFurnishing.MusicRoot, MediaFurnishing.PodcastsRoot, MediaFurnishing.BooksRoot, MediaFurnishing.MoviesRoot],
            shown.Panel.Nav.Roots(PanelTab.Media).Select(root => root.Key));
    }

    [AvaloniaFact]
    public void AMiniPanelReadsTheTrackOutOnlyWhileItIsOnMediaMusic()
    {
        var shown = Open(Playing, mode: PanelMode.Mini);
        var readout = Named<AudioReadout>(shown.Panel, "AudioReadoutBar");

        Assert.True(readout.IsVisible);
        Assert.False(Named<Control>(shown.Page, MusicPage.BodyName).IsVisible);
        Assert.Equal("PLAYING", Named<TextBlock>(readout, AudioReadout.StateName).Text);
        Assert.Equal("long-drift-04.mp3", Named<TextBlock>(readout, AudioReadout.TrackName).Text);
        Assert.Contains("“pause”", Named<TextBlock>(readout, AudioReadout.SayName).Text);
        Assert.StartsWith("supercruise", Named<TextBlock>(readout, AudioReadout.SayName).Text);

        shown.Window.SaveFrame("music-page-mini.png");

        shown.Model.AudioPlayer = AudioPlayerReadout.From(new MusicState("long-drift-04.mp3", false, true, false, "supercruise"), false);
        Jobs();

        Assert.Equal("PAUSED", Named<TextBlock>(readout, AudioReadout.StateName).Text);
        Assert.Contains("“play”", Named<TextBlock>(readout, AudioReadout.SayName).Text);

        Assert.True(shown.Panel.Nav.Previous());
        Jobs();

        Assert.False(readout.IsVisible);
    }

    [AvaloniaFact]
    public void TheBigPanelShowsThePageWhenItLeavesMini()
    {
        var shown = Open(Playing, mode: PanelMode.Mini);
        var readout = Named<AudioReadout>(shown.Panel, "AudioReadoutBar");

        shown.Panel.Mode = PanelMode.Full;
        Jobs();

        Assert.False(readout.IsVisible);
        Assert.True(Named<Control>(shown.Page, MusicPage.BodyName).IsVisible);
    }
}
