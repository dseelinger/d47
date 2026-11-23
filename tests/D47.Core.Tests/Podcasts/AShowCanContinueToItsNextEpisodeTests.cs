using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Podcasts;
using D47.Core.Storage;
using D47.Core.Tests.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Podcasts;

/// <summary>A show on continuous play starts its next episode when one ends, in the show's order (#2691).</summary>
public sealed class AShowCanContinueToItsNextEpisodeTests
{
    private const string ListeningPath = @"C:\d47\data\podcasts\listening.json";

    private static readonly Uri Feed = new("https://example.test/feed.xml");

    private static readonly Uri Other = new("https://other.example.test/feed.xml");

    private static readonly Lazy<CueLibrary> NoMusic = new(() => CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly)));

    private static readonly PodcastEpisode First = Episode("e1", 1);

    private static readonly PodcastEpisode Second = Episode("e2", 2);

    private static readonly PodcastEpisode Third = Episode("e3", 3);

    private static readonly PodcastEpisode Fourth = Episode("e4", 4);

    /// <summary>Newest first, as the feed parser gives them.</summary>
    private static readonly PodcastShow Show = new("Deep Space Hour", [Fourth, Third, Second, First]);

    private readonly RecordingAudioSink _sink = new();
    private readonly AudioArbiter _audio;
    private readonly AmbientMusic _music;
    private readonly MemoryFileSystem _files = new();
    private readonly List<Action> _queued = [];
    private PodcastSettings _settings = new() { Feeds = [Feed.AbsoluteUri] };
    private Func<IPcmStream> _stream = () => new MemoryPcm(canSeek: true);

    public AShowCanContinueToItsNextEpisodeTests()
    {
        _audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
        _music = new AmbientMusic(_audio, () => NoMusic.Value);
    }

    private static PodcastEpisode Episode(string id, int month) =>
        new(id, $"Episode {id}", new DateTimeOffset(2026, month, 1, 0, 0, 0, TimeSpan.Zero), new Uri($"https://cdn.example.test/{id}.mp3"), "audio/mpeg", null, TimeSpan.FromHours(1));

    private PodcastPlayer Player() =>
        new(
            _audio,
            _music,
            episode => new MusicTrack(episode.Id, () => _stream()),
            _files,
            ListeningPath,
            NullLogger<PodcastPlayer>.Instance,
            _queued.Add,
            new ManualTime(),
            feed => _settings.PlayFor(feed));

    private void Continuous(PodcastOrder order) => _settings = _settings.WithPlay(Feed, true, order);

    private void Finished(params PodcastEpisode[] episodes) =>
        _files.WriteText(
            ListeningPath,
            episodes.Aggregate(new PodcastListening(), (listening, episode) => listening.With(Feed, episode.Id, TimeSpan.Zero, true)).ToJson());

    private void RunQueued()
    {
        var work = _queued.ToList();
        _queued.Clear();
        work.ForEach(action => action());
    }

    private PlaybackRequest Live => _sink.Started.Last(request => request.Track is not null);

    private void ReadToTheEnd()
    {
        using var stream = Live.Track!.Open();
        var buffer = new byte[4096];

        while (stream.Read(buffer) > 0)
        {
        }
    }

    private void Ends(PodcastPlayer player, PodcastEpisode episode)
    {
        _ = player.Play(Feed, Show, episode);
        var id = Live.Id;
        ReadToTheEnd();
        _sink.CompletePlayback(id);
        RunQueued();
    }

    [Fact]
    public void OldestFirstStartsTheEarliestLaterEpisode()
    {
        Continuous(PodcastOrder.OldestFirst);
        var player = Player();

        Ends(player, First);

        Assert.Equal(Second.Id, Live.Track!.Name);
        Assert.Equal(Second.Id, player.State?.Episode.Id);
    }

    [Fact]
    public void OldestFirstSkipsFinishedEpisodes()
    {
        Continuous(PodcastOrder.OldestFirst);
        Finished(Second, Third);
        var player = Player();

        Ends(player, First);

        Assert.Equal(Fourth.Id, Live.Track!.Name);
    }

    [Fact]
    public void NewestFirstStartsTheLatestEarlierEpisode()
    {
        Continuous(PodcastOrder.NewestFirst);
        var player = Player();

        Ends(player, Fourth);

        Assert.Equal(Third.Id, Live.Track!.Name);
    }

    [Fact]
    public void NewestFirstSkipsFinishedEpisodes()
    {
        Continuous(PodcastOrder.NewestFirst);
        Finished(Third, Second);
        var player = Player();

        Ends(player, Fourth);

        Assert.Equal(First.Id, Live.Track!.Name);
    }

    [Fact]
    public void ANextEpisodeStartsFromItsSavedPosition()
    {
        Continuous(PodcastOrder.OldestFirst);
        _files.WriteText(ListeningPath, new PodcastListening().With(Feed, Second.Id, TimeSpan.FromMinutes(7), false).ToJson());
        var player = Player();

        Ends(player, First);

        Assert.Equal((Live.Id, TimeSpan.FromMinutes(7)), _sink.Seeks.Last());
    }

    [Fact]
    public void AShowThatIsOffStopsAtTheEnd()
    {
        var player = Player();

        Ends(player, First);

        Assert.Null(player.State);
        Assert.Single(_sink.Started);
    }

    [Fact]
    public void AnEpisodeThatFailsStartsNothing()
    {
        Continuous(PodcastOrder.OldestFirst);
        _stream = () => throw new IOException("The episode stopped arriving.");
        var player = Player();

        _ = player.Play(Feed, Show, First);
        var id = Live.Id;
        Assert.Throws<IOException>(() => Live.Track!.Open());
        _sink.CompletePlayback(id);
        RunQueued();

        Assert.Null(player.State);
        Assert.Single(_sink.Started);
    }

    [Fact]
    public void AnEpisodeTheCommanderStopsStartsNothing()
    {
        Continuous(PodcastOrder.OldestFirst);
        var player = Player();

        _ = player.Play(Feed, Show, First);
        var id = Live.Id;
        player.Stop();
        _sink.CompletePlayback(id);
        RunQueued();

        Assert.Null(player.State);
        Assert.Single(_sink.Started);
    }

    [Fact]
    public void AnEpisodeReplacedByAnotherStartsNothingAfterItself()
    {
        Continuous(PodcastOrder.OldestFirst);
        var player = Player();

        _ = player.Play(Feed, Show, First);
        var replaced = Live.Id;
        _ = player.Play(Feed, Show, Fourth);
        _sink.CompletePlayback(replaced);
        RunQueued();

        Assert.Equal(Fourth.Id, player.State?.Episode.Id);
        Assert.Equal(2, _sink.Started.Count);
    }

    [Fact]
    public void WithNoEpisodeLeftInThatDirectoryPlaybackStopsAndTheMusicReturns()
    {
        Continuous(PodcastOrder.OldestFirst);
        var player = Player();

        Ends(player, Fourth);

        Assert.Null(player.State);
        Assert.Single(_sink.Started);
        Assert.DoesNotContain("waits", _music.Control(MusicAction.Resume), StringComparison.Ordinal);
    }

    [Fact]
    public void TheMusicStaysAwayBetweenTheEndAndTheNextEpisode()
    {
        Continuous(PodcastOrder.OldestFirst);
        var player = Player();

        _ = player.Play(Feed, Show, First);
        var id = Live.Id;
        ReadToTheEnd();
        _queued.Clear();
        _sink.CompletePlayback(id);

        var work = _queued.ToList();
        _queued.Clear();
        work[..^1].ForEach(action => action());

        Assert.Null(player.State);
        Assert.Contains("waits while a podcast plays", _music.Control(MusicAction.Resume), StringComparison.Ordinal);

        work[^1]();

        Assert.Equal(Second.Id, player.State?.Episode.Id);
    }

    [Fact]
    public void NextFollowsTheShowsOrderWhileItIsOnNewestFirst()
    {
        Continuous(PodcastOrder.NewestFirst);
        var player = Player();

        _ = player.Play(Feed, Show, Third);
        _ = player.Next();

        Assert.Equal(Second.Id, Live.Track!.Name);
    }

    [Fact]
    public void NextIsOldestFirstWhileTheShowIsOff()
    {
        _settings = _settings.WithPlay(Feed, false, PodcastOrder.NewestFirst);
        var player = Player();

        _ = player.Play(Feed, Show, Second);
        _ = player.Next();

        Assert.Equal(Third.Id, Live.Track!.Name);
    }

    [Fact]
    public void TheOrderIsKeptAcrossOffAndOn()
    {
        var set = _settings.WithPlay(Feed, true, PodcastOrder.NewestFirst).WithPlay(Feed, false);

        Assert.False(set.PlayFor(Feed).Continuous);

        var again = set.WithPlay(Feed, true);

        Assert.True(again.PlayFor(Feed).Continuous);
        Assert.Equal(PodcastOrder.NewestFirst, again.PlayFor(Feed).Order);
    }

    [Fact]
    public void AFeedWithNoEntryIsOffAndOldestFirst()
    {
        var play = _settings.PlayFor(Feed);

        Assert.False(play.Continuous);
        Assert.Equal(PodcastOrder.OldestFirst, play.Order);
    }

    [Fact]
    public void AnEntryForAFeedNotAddedIsIgnored()
    {
        var settings = new PodcastSettings
        {
            Feeds = [Feed.AbsoluteUri],
            Shows = [new PodcastShowPlay { Feed = Other.AbsoluteUri, Continuous = true, Order = PodcastOrder.NewestFirst }],
        };

        Assert.False(settings.PlayFor(Other).Continuous);
        Assert.Same(settings, settings.WithPlay(Other, true));
    }

    [Fact]
    public void AShowRemovedAndAddedAgainStartsOff()
    {
        var set = _settings.WithPlay(Feed, true, PodcastOrder.NewestFirst);

        var viaMethod = set.WithoutFeed(Feed).WithFeed(Feed);
        Assert.False(viaMethod.PlayFor(Feed).Continuous);
        Assert.Equal(PodcastOrder.OldestFirst, viaMethod.PlayFor(Feed).Order);

        var row = PodcastCapability.Create(() => null).Settings.Single();
        var removed = row.Binding!.Write!(new D47Settings { Podcasts = set }, string.Empty);
        var added = row.Binding!.Write!(removed, Feed.AbsoluteUri);

        Assert.False(added.Podcasts.PlayFor(Feed).Continuous);
        Assert.Empty(removed.Podcasts.Shows);
    }

    private PodcastDesk Desk(PodcastPlayer player)
    {
        return new PodcastDesk(
            player,
            () => [new CachedShow(Feed, Show)],
            () => Task.FromResult<IReadOnlyList<PodcastRefresh>>([]),
            _ => Task.FromResult(new PodcastSearchOutcome([], null)),
            feed => Task.FromResult(new PodcastRefresh(feed, Show, null)),
            feed => _settings.PlayFor(feed),
            (feed, on, order) => _settings = _settings.WithPlay(feed, on, order));
    }

    [Theory]
    [InlineData("continuous play on", true, PodcastOrder.OldestFirst, "Continuous play is on for Deep Space Hour, oldest first.")]
    [InlineData("continuous play newest first", true, PodcastOrder.NewestFirst, "Continuous play is on for Deep Space Hour, newest first.")]
    [InlineData("continuous play oldest first", true, PodcastOrder.OldestFirst, "Continuous play is on for Deep Space Hour, oldest first.")]
    public async Task ThePhrasesSetTheShowInTheSlot(string said, bool on, PodcastOrder order, string reply)
    {
        var player = Player();
        var desk = Desk(player);
        var registry = CapabilityRegistry.Build([PodcastCapability.Create(() => desk)]);
        _ = player.Play(Feed, Show, Second);

        var match = new KeywordRouter(registry).MatchToolCommand(said);
        var result = await registry.InvokeAsync(match!.ToolName, match.Arguments, TestContext.Current.CancellationToken, ToolCaller.Commander);

        Assert.False(result.IsError);
        Assert.Equal(reply, result.Content);
        Assert.Equal(on, _settings.PlayFor(Feed).Continuous);
        Assert.Equal(order, _settings.PlayFor(Feed).Order);
    }

    [Fact]
    public async Task ContinuousPlayOffKeepsTheOrderAndNamesTheShow()
    {
        var player = Player();
        var desk = Desk(player);
        var registry = CapabilityRegistry.Build([PodcastCapability.Create(() => desk)]);
        var token = TestContext.Current.CancellationToken;
        _ = player.Play(Feed, Show, Second);

        await Say(registry, "continuous play newest first", token);
        var off = await Say(registry, "continuous play off", token);

        Assert.Equal("Continuous play is off for Deep Space Hour.", off.Content);
        Assert.Equal(PodcastOrder.NewestFirst, _settings.PlayFor(Feed).Order);
    }

    [Fact]
    public async Task TheModelCanNameAShowAndWithNeitherNothingIsPlaying()
    {
        var player = Player();
        var desk = Desk(player);
        var registry = CapabilityRegistry.Build([PodcastCapability.Create(() => desk)]);
        var token = TestContext.Current.CancellationToken;

        var none = await registry.InvokeAsync(
            PodcastCapability.ManagePodcastTool,
            new ToolArguments(new Dictionary<string, string> { ["action"] = "continuous_on" }),
            token);
        Assert.Equal("No podcast is playing.", none.Content);
        Assert.False(_settings.PlayFor(Feed).Continuous);

        var named = await registry.InvokeAsync(
            PodcastCapability.ManagePodcastTool,
            new ToolArguments(new Dictionary<string, string> { ["action"] = "continuous_newest", ["show"] = "deep space" }),
            token);
        Assert.Equal("Continuous play is on for Deep Space Hour, newest first.", named.Content);
    }

    private static Task<ToolResult> Say(CapabilityRegistry registry, string said, CancellationToken token)
    {
        var match = new KeywordRouter(registry).MatchToolCommand(said);

        return registry.InvokeAsync(match!.ToolName, match.Arguments, token, ToolCaller.Commander);
    }
}
