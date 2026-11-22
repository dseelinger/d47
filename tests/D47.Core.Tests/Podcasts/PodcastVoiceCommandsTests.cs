using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Podcasts;
using D47.Core.Tests.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Podcasts;

/// <summary>The podcast tools, their phrases, and who may reach them (#2658).</summary>
public sealed class PodcastVoiceCommandsTests
{
    private const string ListeningPath = @"C:\d47\data\podcasts\listening.json";

    private static readonly Uri Feed = new("https://example.test/feed.xml");

    private static readonly Lazy<CueLibrary> NoMusic = new(() => CueLibrary.Load(null, new EmbeddedCueSource(typeof(CueLibrary).Assembly)));

    private static readonly PodcastEpisode Episode = new(
        "e1",
        "Episode one",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new Uri("https://cdn.example.test/e1.mp3"),
        "audio/mpeg",
        null,
        TimeSpan.FromHours(1));

    private static readonly PodcastShow Show = new("Deep Space Hour", [Episode]);

    private static readonly PodcastSearchResult[] Results =
    [
        new("First show", "A", new Uri("https://one.example.test/rss"), 10, null),
        new("Second show", "B", new Uri("https://two.example.test/rss"), null, null),
        new("Third show", "C", new Uri("https://three.example.test/rss"), 3, null),
        new("Fourth show", "D", new Uri("https://four.example.test/rss"), 4, null),
    ];

    private readonly RecordingAudioSink _sink = new();
    private readonly List<Uri> _added = [];

    private PodcastDesk Desk()
    {
        var audio = new AudioArbiter(_sink, NullLogger<AudioArbiter>.Instance).Start();
        var player = new PodcastPlayer(
            audio,
            new AmbientMusic(audio, () => NoMusic.Value),
            episode => new MusicTrack(episode.Id, () => new MemoryPcm(canSeek: true)),
            new D47.Core.Storage.MemoryFileSystem(),
            ListeningPath,
            NullLogger<PodcastPlayer>.Instance,
            work => work(),
            new ManualTime());

        return new PodcastDesk(
            player,
            () => [new CachedShow(Feed, Show)],
            () => Task.FromResult<IReadOnlyList<PodcastRefresh>>([new PodcastRefresh(Feed, Show, null)]),
            _ => Task.FromResult(new PodcastSearchOutcome(Results, null)),
            feed =>
            {
                _added.Add(feed);
                return Task.FromResult(new PodcastRefresh(feed, Show, null));
            });
    }

    private static CapabilityRegistry Registry(PodcastDesk desk, Func<bool>? story = null, Func<bool>? music = null) =>
        CapabilityRegistry.Build(
            [
                PodcastCapability.Create(() => desk, story),
                AudioCapability.Create(storyPlaybackOpen: story, podcastActive: () => desk.Active),
                AdventureCapability.Create(desk: new AdventureCapability.AdventureDesk { PlaybackOpen = story ?? (() => false) }),
            ]);

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(pair => pair.Name, pair => pair.Value));

    [Fact]
    public async Task TheModelCannotAddAFeedButTheKeywordRouterCan()
    {
        var desk = Desk();
        var registry = Registry(desk);
        var token = TestContext.Current.CancellationToken;

        var found = await registry.InvokeAsync(PodcastCapability.FindPodcastsTool, Args(("words", "space")), token, ToolCaller.Model);
        Assert.False(found.IsError);
        Assert.Contains("2. Second show by B", found.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Fourth", found.Content, StringComparison.Ordinal);

        var refused = await registry.InvokeAsync(PodcastCapability.AddPodcastTool, Args(("result", "1")), token, ToolCaller.Model);
        Assert.True(refused.IsError);
        Assert.Empty(_added);

        var match = new KeywordRouter(registry).MatchToolCommand("add podcast two");
        Assert.Equal(PodcastCapability.AddPodcastTool, match?.ToolName);

        var added = await registry.InvokeAsync(match!.ToolName, match.Arguments, token, ToolCaller.Commander);
        Assert.False(added.IsError);
        Assert.Equal([Results[1].Feed], _added);
        Assert.Equal("Added Deep Space Hour, with 1 episode.", added.Content);
    }

    [Theory]
    [InlineData("add podcast one", "1")]
    [InlineData("add podcast 2", "2")]
    [InlineData("add the podcast three", "3")]
    public void TheNumberIsSaidOrTyped(string said, string result)
    {
        var router = new KeywordRouter(Registry(Desk()));

        Assert.Null(router.MatchSetting(said));
        Assert.Equal(result, router.MatchToolCommand(said)?.Arguments.Values["result"]);
    }

    [Fact]
    public async Task AddingBeforeASearchOrPastTheResultsSaysSo()
    {
        var registry = Registry(Desk());
        var token = TestContext.Current.CancellationToken;

        Assert.True((await registry.InvokeAsync(PodcastCapability.AddPodcastTool, Args(("result", "1")), token)).IsError);

        await registry.InvokeAsync(PodcastCapability.FindPodcastsTool, Args(("words", "space")), token, ToolCaller.Model);

        Assert.True((await registry.InvokeAsync(PodcastCapability.AddPodcastTool, Args(("result", "4")), token)).IsError);
        Assert.True((await registry.InvokeAsync(PodcastCapability.AddPodcastTool, Args(("result", "0")), token)).IsError);
        Assert.Empty(_added);
    }

    [Fact]
    public void TheFeedsRowIsRefusedToSetSetting()
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        var refused = settings.Apply(PodcastCapability.FeedsKey, "https://example.test/feed.xml", SettingsCaller.Model);

        Assert.Equal(SettingApplyStatus.Refused, refused.Status);
        Assert.Empty(settings.Current.Podcasts.Feeds);
    }

    [Fact]
    public void ThePanelSavesFeedsAndRefusesAddressesThatAreNotWeb()
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        Assert.True(settings.Apply(PodcastCapability.FeedsKey, "https://a.example.test/rss  http://b.example.test/rss", SettingsCaller.Panel).Ok);
        Assert.Equal(["https://a.example.test/rss", "http://b.example.test/rss"], settings.Current.Podcasts.Feeds);

        var refused = settings.Apply(PodcastCapability.FeedsKey, "ftp://c.example.test/rss", SettingsCaller.Panel);
        Assert.Equal(SettingApplyStatus.Rejected, refused.Status);
        Assert.Equal(2, settings.Current.Podcasts.Feeds.Count);
    }

    [Fact]
    public async Task NextEpisodeWithNothingPlayingSaysSo()
    {
        var desk = Desk();
        var registry = Registry(desk);
        var match = new KeywordRouter(registry).MatchToolCommand("next episode");

        Assert.Equal(PodcastCapability.ManagePodcastTool, match?.ToolName);

        var said = await registry.InvokeAsync(match!.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.Equal("No podcast is playing.", said.Content);
    }

    [Fact]
    public void BareWordsGoToTheStoryThenThePodcastThenTheMusic()
    {
        var desk = Desk();
        var story = false;
        var router = new KeywordRouter(Registry(desk, () => story));

        Assert.Equal(AudioCapability.ManageMusicTool, router.MatchToolCommand("pause")?.ToolName);
        Assert.Equal(AudioCapability.ManageMusicTool, router.MatchToolCommand("play")?.ToolName);

        _ = desk.Play(null);
        Assert.True(desk.Active);

        var pause = router.MatchToolCommand("pause");
        Assert.Equal(PodcastCapability.ManagePodcastTool, pause?.ToolName);
        Assert.Equal("pause", pause!.Arguments.Values["action"]);

        var play = router.MatchToolCommand("play");
        Assert.Equal(PodcastCapability.ManagePodcastTool, play?.ToolName);
        Assert.Equal("resume", play!.Arguments.Values["action"]);

        story = true;
        Assert.Equal(AdventureCapability.StoryPlaybackTool, router.MatchToolCommand("pause")?.ToolName);
        Assert.Equal(AdventureCapability.StoryPlaybackTool, router.MatchToolCommand("play")?.ToolName);
        Assert.Equal(PodcastCapability.ManagePodcastTool, router.MatchToolCommand("pause the podcast")?.ToolName);

        desk.Stop();
    }

    [Fact]
    public async Task PlayWithAShowPlaysItsNewestUnfinishedEpisodeAndAnUnknownShowSaysSo()
    {
        var desk = Desk();
        var registry = Registry(desk);
        var token = TestContext.Current.CancellationToken;

        var none = await registry.InvokeAsync(PodcastCapability.ManagePodcastTool, Args(("action", "play"), ("show", "Nothing")), token, ToolCaller.Model);
        Assert.Equal("No added show is called Nothing.", none.Content);

        var playing = registry.InvokeAsync(PodcastCapability.ManagePodcastTool, Args(("action", "play"), ("show", "deep space")), token, ToolCaller.Model);
        Assert.True(desk.Active);
        Assert.Equal("Deep Space Hour", desk.Player.State?.Show);
        Assert.Equal("Episode one", desk.Player.State?.Episode.Title);

        desk.Stop();
        Assert.Equal("Episode one was stopped before it started.", (await playing).Content);
    }

    [Fact]
    public async Task AListGivesTheModelEveryShowAndTheCommanderTheNames()
    {
        var desk = Desk();
        var registry = Registry(desk);

        var listed = await registry.InvokeAsync(PodcastCapability.ListPodcastsTool, ToolArguments.Empty, TestContext.Current.CancellationToken, ToolCaller.Model);

        Assert.Equal("One show: Deep Space Hour.", listed.Spoken);
        Assert.Equal("Deep Space Hour: 1 unfinished, newest Episode one.", listed.Content);
    }
}
