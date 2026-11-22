using D47.Core.Audio;
using D47.Core.Podcasts;
using D47.Core.Storage;
using D47.Core.Tests.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Podcasts;

/// <summary>Positions and finished episodes are read back from <c>listening.json</c> by the next run (#2657).</summary>
[Trait("Category", "Integration")]
public sealed class PodcastPositionsSurviveARestartTests : IDisposable
{
    private static readonly Uri Feed = new("https://example.test/feed.xml");

    private static readonly PodcastEpisode Older = Episode("older", 1);

    private static readonly PodcastEpisode Newer = Episode("newer", 2);

    private static readonly PodcastShow Show = new("Deep Space Hour", [Newer, Older]);

    private static readonly Lazy<CueLibrary> NoMusic = new(() => CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly)));

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-podcast-listening-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static PodcastEpisode Episode(string id, int month) =>
        new(id, id, new DateTimeOffset(2026, month, 1, 0, 0, 0, TimeSpan.Zero), new Uri($"https://cdn.example.test/{id}.mp3"), "audio/mpeg", null, null);

    private (PodcastPlayer Player, RecordingAudioSink Sink, AudioArbiter Audio) Run()
    {
        var sink = new RecordingAudioSink();
        var audio = new AudioArbiter(sink, NullLogger<AudioArbiter>.Instance).Start();
        var player = new PodcastPlayer(
            audio,
            new AmbientMusic(audio, () => NoMusic.Value),
            episode => new MusicTrack(episode.Id, () => new MemoryPcm(canSeek: true)),
            new DiskFileSystem(),
            Path.Combine(_folder, "podcasts", "listening.json"),
            NullLogger<PodcastPlayer>.Instance,
            work => work(),
            new ManualTime());

        return (player, sink, audio);
    }

    [Fact]
    public void ThePositionAndTheFinishedEpisodeAreThereAfterARestart()
    {
        var (first, firstSink, _) = Run();
        _ = first.Play(Feed, Show, Newer);
        var newer = firstSink.Started.Last().Id;

        using (var stream = firstSink.Started.Last().Track!.Open())
        {
            while (stream.Read(new byte[4096]) > 0)
            {
            }
        }

        firstSink.CompletePlayback(newer);

        _ = first.Play(Feed, Show, Older);
        firstSink.Progresses[firstSink.Started.Last().Id] = new TrackProgress(TimeSpan.FromMinutes(21), TimeSpan.FromHours(1));
        first.SaveNow();

        var (second, secondSink, _) = Run();

        Assert.True(second.Listening.Find(Feed, Newer.Id)!.Finished);
        _ = second.Play(Feed, Show, Older);
        Assert.Equal((secondSink.Started.Last().Id, TimeSpan.FromMinutes(21)), secondSink.Seeks.Last());
    }
}
