using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Podcasts;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.App.Tests.TheMusicPageShowsTheMusicTests;

namespace D47.App.Tests;

/// <summary>
/// On the headset the Music and Podcasts seek bars and the mini music read-out move once a second while they play:
/// the host's tick reports a change even when the control's own timer already read the new second (#2729).
/// </summary>
[Trait("Category", "Integration")]
public sealed class TheHeadsetMediaTimelinesMoveWhilePlayingTests
{
    private static MusicState Playing => new("long-drift-04.mp3", true, false, false, "supercruise");

    private sealed class ProgressSink : IAudioSink
    {
        public TrackProgress? Now { get; set; }

        public event Action<long>? Finished;

        public IRenderReferenceTap ReferenceTap { get; } = new SilentTap();

        public void Play(PlaybackRequest request) => _ = Finished;

        public void Stop(long playbackId)
        {
        }

        public void Pause(long playbackId)
        {
        }

        public void Resume(long playbackId)
        {
        }

        public void SetGain(long playbackId, float gain)
        {
        }

        public void Fade(long playbackId, float to, TimeSpan over, bool holdAtEnd = false)
        {
        }

        public TrackProgress? Progress(long playbackId) => Now;

        public bool Seek(long playbackId, TimeSpan to) => false;
    }

    private static TrackProgress At(int seconds) => new(TimeSpan.FromSeconds(seconds), TimeSpan.FromMinutes(5));

    [AvaloniaFact]
    public void TheMusicSeekBarOnTheBigPanelMovesOnTheTickAfterItsTimerRead()
    {
        var at = At(60);
        var shown = Open(Playing, () => at, headset: true);
        shown.Panel.Tab = PanelTab.Media;
        Dispatcher.UIThread.RunJobs();

        shown.Panel.TickMedia();
        Assert.False(shown.Panel.TickMedia());

        at = At(61);
        shown.Page.Poll();

        Assert.True(shown.Panel.TickMedia());
        Assert.False(shown.Panel.TickMedia());
    }

    [AvaloniaFact]
    public void TheMiniPanelsMusicReadOutMovesOnTheTickAfterItsTimerRead()
    {
        var at = At(60);
        var shown = Open(Playing, () => at, mode: PanelMode.Mini);
        shown.Panel.Tab = PanelTab.Media;
        Dispatcher.UIThread.RunJobs();
        var readout = Named<AudioReadout>(shown.Panel, "AudioReadoutBar");

        shown.Panel.TickMedia();
        Assert.False(shown.Panel.TickMedia());

        at = At(61);
        readout.Poll();

        Assert.True(shown.Panel.TickMedia());
        Assert.False(shown.Panel.TickMedia());
    }

    [AvaloniaFact]
    public void ThePodcastsSeekBarOnTheBigPanelMovesOnTheTickAfterItsTimerRead()
    {
        var sink = new ProgressSink { Now = At(60) };
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var music = new AmbientMusic(arbiter, () => CueLibrary.Load());
        var player = new PodcastPlayer(arbiter, music, episode => new MusicTrack(episode.Id, () => throw new IOException("no audio")), new MemoryFileSystem(), "listening.json", NullLogger<PodcastPlayer>.Instance, work => work());
        var feed = new Uri("https://example.test/one.xml");
        var episode = new PodcastEpisode("a", "Episode a", new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero), new Uri("https://cdn.example.test/a.mp3"), "audio/mpeg", null, TimeSpan.FromMinutes(40));
        var show = new PodcastShow("Deep Space Radio", [episode]);

        var host = new PodcastsHost(
            TestSurface.Settings(),
            player,
            () => [new CachedShow(feed, show)],
            () => [feed],
            () => Task.FromResult<IReadOnlyList<PodcastRefresh>>([]),
            _ => Task.FromResult(new PodcastSearchOutcome([], null)),
            _ => Task.FromResult(new PodcastRefresh(feed, null, "no")),
            _ => { },
            uri => new PodcastShowPlay { Feed = uri.AbsoluteUri },
            (_, _, _) => { },
            _ => Task.FromResult(string.Empty));

        _ = player.Play(feed, show, episode);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.Classes.Add("headset");
        panel.EnableMedia(host);
        var window = new Window { Content = panel, Width = 1024, Height = 900 };
        window.Show();
        panel.Tab = PanelTab.Media;
        Assert.True(panel.Nav.SelectRoot(MediaFurnishing.PodcastsRoot));
        Dispatcher.UIThread.RunJobs();
        var page = panel.GetVisualDescendants().OfType<PodcastsPage>().Single();

        panel.TickMedia();
        Assert.False(panel.TickMedia());

        sink.Now = At(61);
        page.Poll();

        Assert.True(panel.TickMedia());
        Assert.False(panel.TickMedia());

        window.Close();
    }
}
