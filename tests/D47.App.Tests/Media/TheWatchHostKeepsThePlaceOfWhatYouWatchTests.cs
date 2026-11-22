using D47.App.Media;
using D47.App.Panel;
using D47.Core.Audio;
using D47.Core.Storage;
using D47.Core.Viewing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Media;

/// <summary>The place in a film is saved every ten seconds, on a pause and before another title starts, and PLAY resumes there (#2673).</summary>
[Trait("Category", "Integration")]
public sealed class TheWatchHostKeepsThePlaceOfWhatYouWatchTests : IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static readonly string Clip = Path.Combine(AppContext.BaseDirectory, "Fixtures", "video", "clip.mp4");

    private static readonly string Other = Path.Combine(AppContext.BaseDirectory, "Fixtures", "video", "no-sound.mp4");

    private static readonly Lazy<bool> Decodes = new(() =>
    {
        using var frames = VideoFrames.Open(Clip);
        return frames is not null;
    });

    private readonly PulledSink _sink = new();
    private readonly MemoryFileSystem _files = new();
    private readonly ManualClock _clock = new();
    private readonly List<WatchHost> _hosts = [];
    private readonly AudioArbiter _audio;

    public TheWatchHostKeepsThePlaceOfWhatYouWatchTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
    }

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.Video.Stop();
            host.Dispose();
        }

        _sink.Dispose();
    }

    private WatchStore Store()
    {
        var store = new WatchStore("data/watch.json", _files, NullLogger<WatchStore>.Instance);
        store.Load();
        return store;
    }

    private WatchHost Host()
    {
        if (!Decodes.Value)
        {
            Assert.Skip("Media Foundation cannot decode the fixture here.");
        }

        var (settings, _, _) = TestSurface.Create();
        var video = new VideoPlayback(_audio, background: work => work());
        var host = new WatchHost(video, Store(), settings, _files, _clock, readSubtitles: (_, _) => Task.FromResult<string?>(null), background: work => work());
        _hosts.Add(host);

        return host;
    }

    private static void Until(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting until {what}.");
            Thread.Sleep(10);
        }
    }

    private static void Opened(WatchHost host) =>
        Until(() => host.Video.State is not (null or VideoState.Opening), "it opened");

    [Fact]
    public void ThePlaceIsSavedEveryTenSecondsWhileItPlays()
    {
        var host = Host();
        var title = host.FileTitle(Clip);

        Assert.True(host.Play(title));
        Opened(host);
        host.Video.Seek(TimeSpan.FromSeconds(3));
        _clock.Advance(WatchHost.SaveEvery);

        var kept = Assert.Single(Store().CarryOnWatching());

        Assert.Equal(title.Id, kept.Id);
        Assert.Equal(_clock.GetUtcNow(), kept.LastWatched);
        Assert.InRange(kept.Place, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(8));
        Assert.Equal(title, kept.Video);
    }

    [Fact]
    public void APauseSavesThePlaceAtOnce()
    {
        var host = Host();
        var title = host.FileTitle(Clip);

        host.Play(title);
        Opened(host);
        host.Video.Seek(TimeSpan.FromSeconds(4));
        host.Pause();

        Until(() => Store().Find(title.Source, title.Id) is { Place.TotalSeconds: >= 3.5 }, "the pause was saved");
    }

    [Fact]
    public void StartingAnotherTitleSavesThePlaceOfTheFirst()
    {
        var host = Host();
        var first = host.FileTitle(Clip);

        host.Play(first);
        Opened(host);
        host.Video.Seek(TimeSpan.FromSeconds(5));
        host.Play(host.FileTitle(Other));

        Assert.InRange(Store().Find(first.Source, first.Id)!.Place, TimeSpan.FromSeconds(4.5), TimeSpan.FromSeconds(8));
    }

    [Fact]
    public void ReopeningListsTheTitleAtItsPlaceAndPlayResumesThere()
    {
        var first = Host();
        var title = first.FileTitle(Clip);

        first.Play(title);
        Opened(first);
        first.Video.Seek(TimeSpan.FromSeconds(4));
        first.Flush();
        first.Video.Stop();

        var second = Host();
        var (entry, kept) = Assert.Single(second.CarryOn());

        Assert.Equal(title, kept);
        Assert.InRange(entry.Place, TimeSpan.FromSeconds(3.5), TimeSpan.FromSeconds(8));

        second.Open(kept);
        Assert.True(second.Resume());
        Assert.InRange(second.Video.Position, entry.Place - TimeSpan.FromSeconds(0.5), entry.Place + TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AnUntickedSourceStaysHeldWhileATitleFromItIsKept()
    {
        var host = Host();
        var title = new VideoTitle(VideoSources.Nasa, "apollo", "Apollo 11", VideoKind.Footage, "US government work", null, new Uri("https://images-assets.nasa.gov/video/a/a.mp4"), null);

        Assert.Empty(host.HeldSources);

        host.Store.Add(title.Source, title.Id, Noon, title);

        Assert.Equal([VideoSources.Nasa], host.HeldSources);
    }

    /// <summary>A clock whose one timer fires when told to.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = Noon;
        private TimerCallback? _callback;

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _callback = callback;
            return new Quiet();
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
            _callback?.Invoke(null);
        }

        private sealed class Quiet : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
