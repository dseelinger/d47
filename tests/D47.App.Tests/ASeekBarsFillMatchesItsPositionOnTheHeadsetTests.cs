using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Headset;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Podcasts;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A seek bar draws its fill and mark at its position of the track it was last laid out at, so the headset's panels,
/// which lay out twice a frame at two widths, draw the Podcasts bar where the episode is (#2744).
/// </summary>
public sealed class ASeekBarsFillMatchesItsPositionOnTheHeadsetTests
{
    private static readonly TimeSpan EpisodeLength = new(0, 34, 57);

    private static readonly TimeSpan Listened = TimeSpan.FromMinutes(15);

    private static double Expected => Listened / EpisodeLength;

    private sealed class FixedSink(TrackProgress now) : IAudioSink
    {
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

        public TrackProgress? Progress(long playbackId) => now;

        public bool Seek(long playbackId, TimeSpan to) => true;
    }

    private static PodcastsHost Host()
    {
        var sink = new FixedSink(new TrackProgress(Listened, EpisodeLength));
        var arbiter = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var music = new AmbientMusic(arbiter, () => CueLibrary.Load());
        var player = new PodcastPlayer(arbiter, music, episode => new MusicTrack(episode.Id, () => throw new IOException("no audio")), new MemoryFileSystem(), "listening.json", NullLogger<PodcastPlayer>.Instance, work => work());
        var feed = new Uri("https://example.test/one.xml");
        var episode = new PodcastEpisode("a", "The long game", new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero), new Uri("https://cdn.example.test/a.mp3"), "audio/mpeg", null, EpisodeLength);
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

        return host;
    }

    /// <summary>The headset's own draw, into a buffer shaped like the texture it is handed.</summary>
    private static void Draw(VrPanelSurface surface)
    {
        surface.Configure();
        surface.ApplyMode();
        var (width, height) = surface.Size;
        var buffer = new byte[width * 4 * height];

        unsafe
        {
            fixed (byte* into = buffer)
            {
                surface.Draw((IntPtr)into, width * 4);
            }
        }

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The track, fill and mark, as drawn.</summary>
    private static (Control Track, Control Fill, Control Mark) Parts(SeekBar bar)
    {
        var parts = bar.GetVisualDescendants().OfType<Avalonia.Controls.Panel>().First().Children;

        return (parts[0], parts[1], parts[2]);
    }

    private static void AssertTheFillIsThePosition(SeekBar bar, string surface)
    {
        var (track, fill, mark) = Parts(bar);
        var at = Expected * track.Bounds.Width;

        Assert.True(track.Bounds.Width > 100, $"the {surface} track is {track.Bounds.Width} px");
        Assert.True(Math.Abs(fill.Bounds.Width - at) < 1, $"the {surface} fill is {fill.Bounds.Width} px of a {track.Bounds.Width} px track; {at:0.#} px is 15:00 of 34:57");
        Assert.True(Math.Abs(mark.Bounds.X + (mark.Bounds.Width / 2) - at) < 1, $"the {surface} mark is centred at {mark.Bounds.X + (mark.Bounds.Width / 2)} px; {at:0.#} px is 15:00 of 34:57");
    }

    [AvaloniaFact]
    [Trait("Category", "Integration")]
    public void TheHeadsetDrawsThePodcastBarAtTheEpisodesPositionOnTheBigPanelAndAfterSwitchingToMini()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite);
        var (settings, _, _) = TestSurface.Create();
        settings.Replace(
            "test",
            current => current with
            {
                Vr = current.Vr with
                {
                    Mode = "full",
                    Panel = current.Vr.Panel with { Pixels = "1600x1443", Zoom = 100 },
                    Mini = current.Vr.Mini with { Pixels = string.Empty, Zoom = 80 },
                },
            });

        using var surface = new VrPanelSurface(new PanelViewModel(), settings, _ => null, podcasts: Host());
        surface.Nav.Select(PanelTab.Media);
        Assert.True(surface.Nav.SelectRoot(MediaFurnishing.PodcastsRoot));
        Draw(surface);
        Draw(surface);

        var bar = surface.Board.View.GetVisualDescendants().OfType<SeekBar>().Single(seek => seek.Name == PodcastsPage.SeekName);
        surface.Board.Render().SaveCapture("podcasts-headset-big.png");
        AssertTheFillIsThePosition(bar, "big panel");

        settings.Replace("test", current => current with { Vr = current.Vr with { Mode = "mini" } });
        Draw(surface);
        Draw(surface);

        surface.Board.Render().SaveCapture("podcasts-headset-mini.png");
        AssertTheFillIsThePosition(bar, "mini panel");
    }

    [AvaloniaFact]
    public void ABarLaidOutAtOneWidthAndThenAnotherDrawsItsFillAtTheSecond()
    {
        var bar = new SeekBar { Position = 0.25 };
        var window = new Window { Content = bar, Width = 1000, Height = 100 };
        window.Show();

        foreach (var width in new[] { 800.0, 400.0 })
        {
            bar.Measure(new Size(width, 44));
            bar.Arrange(new Rect(0, 0, width, 44));
        }

        var (track, fill, _) = Parts(bar);

        Assert.Equal(400 - 24, track.Bounds.Width, 3);
        Assert.Equal(0.25 * track.Bounds.Width, fill.Bounds.Width, 3);

        window.Close();
    }
}
