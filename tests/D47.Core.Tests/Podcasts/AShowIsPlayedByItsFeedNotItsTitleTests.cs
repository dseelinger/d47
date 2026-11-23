using D47.Core.Audio;
using D47.Core.Podcasts;
using D47.Core.Storage;
using D47.Core.Tests.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Podcasts;

/// <summary>Playing a show from its page finds the show by feed and carries on the episode in the slot (#2711).</summary>
public sealed class AShowIsPlayedByItsFeedNotItsTitleTests
{
    private const string ListeningPath = @"C:\d47\data\podcasts\listening.json";

    private static readonly Lazy<CueLibrary> NoMusic = new(() => CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly)));

    private static readonly Uri ShortFeed = new("https://example.test/short.xml");

    private static readonly Uri LongFeed = new("https://example.test/long.xml");

    private static readonly PodcastShow Short = new("Deep Space", [Episode("s-new", 20), Episode("s-old", 10)]);

    private static readonly PodcastShow Long = new("Deep Space Hour", [Episode("l-new", 22), Episode("l-old", 12)]);

    private static PodcastEpisode Episode(string id, int day) =>
        new(id, $"Episode {id}", new DateTimeOffset(2026, 11, day, 0, 0, 0, TimeSpan.Zero), new Uri($"https://cdn.example.test/{id}.mp3"), "audio/mpeg", null, TimeSpan.FromHours(1));

    private static PodcastDesk Desk(Func<PodcastListening, PodcastListening>? listened = null)
    {
        var files = new MemoryFileSystem();

        if (listened is not null)
        {
            files.WriteText(ListeningPath, listened(new PodcastListening()).ToJson());
        }

        var audio = new AudioArbiter(new RecordingAudioSink(), NullLogger<AudioArbiter>.Instance).Start();
        var player = new PodcastPlayer(
            audio,
            new AmbientMusic(audio, () => NoMusic.Value),
            episode => new MusicTrack(episode.Id, () => new MemoryPcm(canSeek: true)),
            files,
            ListeningPath,
            NullLogger<PodcastPlayer>.Instance,
            work => work(),
            new ManualTime());

        return new PodcastDesk(
            player,
            () => [new CachedShow(LongFeed, Long), new CachedShow(ShortFeed, Short)],
            () => Task.FromResult<IReadOnlyList<PodcastRefresh>>([]),
            _ => Task.FromResult(new PodcastSearchOutcome([], null)),
            feed => Task.FromResult(new PodcastRefresh(feed, null, null)));
    }

    [Fact]
    public void TwoShowsWhoseTitlesOverlapEachPlayTheirOwnNewestEpisode()
    {
        var desk = Desk();

        _ = desk.PlayFeed(ShortFeed);
        Assert.Equal(("Deep Space", "s-new"), (desk.Player.State?.Show, desk.Player.State?.Episode.Id));

        _ = desk.PlayFeed(LongFeed);
        Assert.Equal(("Deep Space Hour", "l-new"), (desk.Player.State?.Show, desk.Player.State?.Episode.Id));

        desk.Stop();
    }

    [Fact]
    public void ThePausedEpisodeOfTheShowIsResumedNotTheNewest()
    {
        var desk = Desk();

        _ = desk.Player.Play(ShortFeed, Short, Short.Episodes[1]);
        desk.Pause();
        Assert.False(desk.Player.State?.Playing);

        _ = desk.PlayFeed(ShortFeed);

        Assert.Equal("s-old", desk.Player.State?.Episode.Id);
        Assert.True(desk.Player.State?.Playing);

        desk.Stop();
    }

    [Fact]
    public void AnEpisodeOfAnotherShowInTheSlotIsReplacedByTheNewestOfThisShow()
    {
        var desk = Desk();

        _ = desk.Player.Play(LongFeed, Long, Long.Episodes[1]);
        _ = desk.PlayFeed(ShortFeed);

        Assert.Equal(("Deep Space", "s-new"), (desk.Player.State?.Show, desk.Player.State?.Episode.Id));

        desk.Stop();
    }

    [Fact]
    public async Task TheNewestUnfinishedEpisodeIsPlayedAndAFinishedShowSaysSo()
    {
        var desk = Desk(listening => listening.With(ShortFeed, "s-new", TimeSpan.Zero, finished: true));

        _ = desk.PlayFeed(ShortFeed);
        Assert.Equal("s-old", desk.Player.State?.Episode.Id);
        desk.Stop();

        var done = Desk(listening => listening
            .With(ShortFeed, "s-new", TimeSpan.Zero, finished: true)
            .With(ShortFeed, "s-old", TimeSpan.Zero, finished: true));

        Assert.Equal("Every episode of Deep Space is finished.", await done.PlayFeed(ShortFeed));
        Assert.Null(done.Player.State);
    }

    [Fact]
    public async Task AFeedThatIsNotAddedSaysSo()
    {
        Assert.Equal("That show is not added.", await Desk().PlayFeed(new Uri("https://example.test/other.xml")));
    }
}
