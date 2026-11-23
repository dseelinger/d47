using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Viewing;
using Xunit;

namespace D47.Core.Tests.Viewing;

/// <summary>The video tools, their phrases, and the bare words they share with books, podcasts and music (#2676).</summary>
public sealed class TheVideoVoiceCommandsReachTheirToolsTests
{
    private static readonly VideoTitle Bunny = Title("a", "Big Buck Bunny");

    private static readonly VideoTitle Sintel = Title("b", "Sintel");

    private static readonly VideoTitle Landing = Title("c", "Apollo Landing");

    private readonly List<string> _calls = [];
    private VideoNow? _now;
    private IReadOnlyList<VideoTitle> _list = [Bunny];
    private IReadOnlyList<VideoTitle> _carryOn = [Sintel];
    private bool _story;

    private static VideoTitle Title(string id, string name) =>
        new("internet-archive", id, name, VideoKind.ShortFilm, "CC", null, new Uri("https://example.test/" + id), null);

    private WatchDesk Desk() => new(
        () => _list,
        () => _carryOn,
        (query, _) => Task.FromResult(query.Contains("apollo", StringComparison.OrdinalIgnoreCase)
            ? new VideoPage([Landing], 1)
            : VideoPage.Empty with { Problem = "No video source is ticked, so nothing was searched." }),
        title =>
        {
            _calls.Add("play " + title.Title);
            _now = new VideoNow(title, VideoStanding.Playing);
            return true;
        },
        () => _now,
        () =>
        {
            _calls.Add("pause");
            _now = _now! with { Standing = VideoStanding.Paused };
        },
        () =>
        {
            _calls.Add("resume");
            _now = _now! with { Standing = VideoStanding.Playing };
            return true;
        },
        by =>
        {
            _calls.Add("skip " + by.TotalSeconds);
            return true;
        },
        title => _calls.Add("add " + title.Title),
        () => _story);

    private static CapabilityRegistry Registry(
        WatchDesk desk,
        Func<bool>? story = null,
        Func<bool>? videoTakes = null,
        Func<bool>? podcastTakes = null)
    {
        var video = videoTakes ?? (() => false);

        return CapabilityRegistry.Build(
            [
                WatchCapability.Create(() => desk, story, video, video),
                PodcastCapability.Create(() => null, story, podcastTakes, video),
                AudioCapability.Create(
                    storyPlaybackOpen: story,
                    podcastActive: () => video() || podcastTakes?.Invoke() == true),
                AdventureCapability.Create(desk: new AdventureCapability.AdventureDesk { PlaybackOpen = story ?? (() => false) }),
            ]);
    }

    private static ToolArguments Args(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(pair => pair.Name, pair => pair.Value));

    [Theory]
    [InlineData("pause the video", WatchCapability.PauseVideoTool)]
    [InlineData("pause video", WatchCapability.PauseVideoTool)]
    [InlineData("carry on", WatchCapability.ResumeVideoTool)]
    [InlineData("resume the video", WatchCapability.ResumeVideoTool)]
    [InlineData("resume video", WatchCapability.ResumeVideoTool)]
    [InlineData("continue watching", WatchCapability.ResumeVideoTool)]
    [InlineData("carry on watching", WatchCapability.ResumeVideoTool)]
    [InlineData("add this to my watch list", WatchCapability.AddToWatchListTool)]
    [InlineData("add to my watch list", WatchCapability.AddToWatchListTool)]
    [InlineData("add to watch list", WatchCapability.AddToWatchListTool)]
    public void EachPhraseReachesItsToolWithoutTheModel(string said, string tool)
    {
        var router = new KeywordRouter(Registry(Desk()));

        Assert.Equal(tool, router.MatchToolCommand(said)?.ToolName);
    }

    [Fact]
    public void ContinueWatchingAsksForTheNewestAndCarryOnDoesNot()
    {
        var router = new KeywordRouter(Registry(Desk()));

        Assert.Equal("true", router.MatchToolCommand("continue watching")?.Arguments.Text("carry_on"));
        Assert.Equal("true", router.MatchToolCommand("carry on watching")?.Arguments.Text("carry_on"));
        Assert.Null(router.MatchToolCommand("carry on")?.Arguments.Text("carry_on"));
    }

    [Theory]
    [InlineData("back thirty seconds", "-30")]
    [InlineData("skip back thirty seconds", "-30")]
    [InlineData("forward thirty seconds", "30")]
    [InlineData("skip forward thirty seconds", "30")]
    public void TheSkipPhrasesGoToTheVideoWhileItIsTheOne(string said, string seconds)
    {
        var router = new KeywordRouter(Registry(Desk(), videoTakes: () => true));
        var match = router.MatchToolCommand(said);

        Assert.Equal(WatchCapability.SkipVideoTool, match?.ToolName);
        Assert.Equal(seconds, match?.Arguments.Text("seconds"));
    }

    [Fact]
    public void TheSkipPhrasesGoToThePodcastWhenTheVideoIsNotTheOne()
    {
        var router = new KeywordRouter(Registry(Desk(), videoTakes: () => false));

        Assert.Equal(PodcastCapability.ManagePodcastTool, router.MatchToolCommand("back thirty seconds")?.ToolName);
    }

    [Fact]
    public void WatchAndATitleIsOnePhrasePerTitleInYourListOrCarryOnWatching()
    {
        var phrases = WatchCapability.WatchPhrases(Desk()).ToList();

        Assert.Equal(["watch Big Buck Bunny", "watch Sintel"], phrases.Select(phrase => phrase.Phrase));
        Assert.All(phrases, phrase => Assert.Equal(WatchCapability.WatchVideoTool, phrase.ToolName));
        Assert.Equal("Sintel", phrases[1].Arguments["title"]);
    }

    [Fact]
    public async Task TheModelMayCallEveryVideoTool()
    {
        var registry = Registry(Desk());
        var token = TestContext.Current.CancellationToken;

        Assert.All(
            registry.All.Single(capability => capability.Descriptor.Id == WatchCapability.Id).Descriptor.Tools,
            tool => Assert.False(tool.Protected, tool.Name));

        Assert.False((await registry.InvokeAsync(WatchCapability.WatchVideoTool, Args(("title", "sintel")), token, ToolCaller.Model)).IsError);
        Assert.Equal(["play Sintel"], _calls);
    }

    [Fact]
    public async Task WatchLooksInYourListBeforeTheSources()
    {
        var registry = Registry(Desk());
        var token = TestContext.Current.CancellationToken;

        async Task<string> Say(string title) =>
            (await registry.InvokeAsync(WatchCapability.WatchVideoTool, Args(("title", title)), token, ToolCaller.Commander)).Content;

        Assert.Equal("Watching Big Buck Bunny.", await Say("big buck"));
        Assert.Equal("Watching Apollo Landing.", await Say("apollo"));
        Assert.Equal("No video source is ticked, so nothing was searched.", await Say("nothing like it"));
        Assert.Equal(["play Big Buck Bunny", "play Apollo Landing"], _calls);
    }

    [Fact]
    public async Task EachToolAnswersInOneShortSentence()
    {
        var registry = Registry(Desk());
        var token = TestContext.Current.CancellationToken;

        async Task<string> Say(string tool, params (string, string)[] arguments) =>
            (await registry.InvokeAsync(tool, Args(arguments), token, ToolCaller.Commander)).Content;

        Assert.Equal("No video is playing.", await Say(WatchCapability.PauseVideoTool));
        Assert.Equal("No video is open. Say continue watching.", await Say(WatchCapability.ResumeVideoTool));
        Assert.Equal("Carrying on Sintel.", await Say(WatchCapability.ResumeVideoTool, ("carry_on", "true")));
        Assert.Equal("Paused.", await Say(WatchCapability.PauseVideoTool));
        Assert.Equal("The video is already paused.", await Say(WatchCapability.PauseVideoTool));
        Assert.Equal("Resumed.", await Say(WatchCapability.ResumeVideoTool));
        Assert.Equal("Back 30 seconds.", await Say(WatchCapability.SkipVideoTool, ("seconds", "-30")));
        Assert.Equal("Forward 30 seconds.", await Say(WatchCapability.SkipVideoTool, ("seconds", "30")));
        Assert.Equal("Added Sintel to Your list.", await Say(WatchCapability.AddToWatchListTool, ("title", "sintel")));
        Assert.Equal("Big Buck Bunny is already in Your list.", await Say(WatchCapability.AddToWatchListTool, ("title", "big buck")));
    }

    [Fact]
    public async Task CarryOnWatchingTriesAStoppedVideoAgainBeforeTheNewestTitle()
    {
        _now = new VideoNow(Landing, VideoStanding.Failed);
        var registry = Registry(Desk());

        var reply = await registry.InvokeAsync(
            WatchCapability.ResumeVideoTool,
            Args(("carry_on", "true")),
            TestContext.Current.CancellationToken,
            ToolCaller.Commander);

        Assert.Equal("Trying Apollo Landing again.", reply.Content);
        Assert.Equal(["resume"], _calls);
    }

    [Fact]
    public async Task AddingWithNoTitleAddsTheVideoThePlayerIsOn()
    {
        _now = new VideoNow(Landing, VideoStanding.Paused);
        var registry = Registry(Desk());

        var reply = await registry.InvokeAsync(
            WatchCapability.AddToWatchListTool,
            Args(),
            TestContext.Current.CancellationToken,
            ToolCaller.Commander);

        Assert.Equal("Added Apollo Landing to Your list.", reply.Content);
    }

    [Fact]
    public async Task AVideoDoesNotStartOrResumeWhileAStoryPlays()
    {
        var desk = Desk();
        _story = true;

        Assert.Equal(WatchDesk.StoryPlaying, await desk.Watch("sintel", TestContext.Current.CancellationToken));

        _story = false;
        await desk.Watch("sintel", TestContext.Current.CancellationToken);
        desk.Pause();
        _story = true;

        Assert.Equal(WatchDesk.StoryPlaying, desk.Resume());
        Assert.Equal(VideoStanding.Paused, _now?.Standing);
    }

    [Fact]
    public void BareWordsGoToTheStoryThenTheVideoThenTheMusic()
    {
        var story = false;
        var video = false;
        var router = new KeywordRouter(Registry(Desk(), () => story, () => video));

        Assert.Equal(AudioCapability.ManageMusicTool, router.MatchToolCommand("pause")?.ToolName);

        video = true;
        Assert.Equal(WatchCapability.PauseVideoTool, router.MatchToolCommand("pause")?.ToolName);
        Assert.Equal(WatchCapability.ResumeVideoTool, router.MatchToolCommand("play")?.ToolName);

        story = true;
        Assert.Equal(AdventureCapability.StoryPlaybackTool, router.MatchToolCommand("pause")?.ToolName);
    }

    [Fact]
    public void BareWordsGoToThePodcastWhenTheVideoIsNotTheOne()
    {
        var video = false;
        var router = new KeywordRouter(Registry(Desk(), null, () => video, () => !video));

        Assert.Equal(PodcastCapability.ManagePodcastTool, router.MatchToolCommand("pause")?.ToolName);

        video = true;
        Assert.Equal(WatchCapability.PauseVideoTool, router.MatchToolCommand("pause")?.ToolName);
    }
}
