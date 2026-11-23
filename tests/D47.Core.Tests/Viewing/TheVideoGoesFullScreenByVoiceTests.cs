using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Viewing;
using Xunit;

namespace D47.Core.Tests.Viewing;

/// <summary>"Full screen", "exit full screen" and "normal size" reach the video's full-screen tool with no model, and each answer is one sentence (#2723).</summary>
public sealed class TheVideoGoesFullScreenByVoiceTests
{
    private static readonly VideoTitle Bunny =
        new("internet-archive", "a", "Big Buck Bunny", VideoKind.ShortFilm, "CC", null, new Uri("https://example.test/a"), null);

    private readonly List<bool> _asked = [];
    private VideoNow? _now;
    private FullScreenAnswer _answer = FullScreenAnswer.Done;

    private WatchDesk Desk() => new(
        () => [Bunny],
        () => [],
        (_, _) => Task.FromResult(VideoPage.Empty),
        _ => true,
        () => _now,
        () => { },
        () => true,
        _ => true,
        _ => { },
        fullScreen: on =>
        {
            _asked.Add(on);
            return _answer;
        });

    private CapabilityRegistry Registry() => CapabilityRegistry.Build([WatchCapability.Create(Desk)]);

    private async Task<string> Say(params (string Name, string Value)[] arguments) =>
        (await Registry().InvokeAsync(
            WatchCapability.FullScreenVideoTool,
            new ToolArguments(arguments.ToDictionary(pair => pair.Name, pair => pair.Value)),
            TestContext.Current.CancellationToken,
            ToolCaller.Commander)).Content;

    [Theory]
    [InlineData("full screen", null)]
    [InlineData("exit full screen", "true")]
    [InlineData("normal size", "true")]
    public void EachPhraseReachesTheToolWithoutTheModel(string said, string? exit)
    {
        var match = new KeywordRouter(Registry()).MatchToolCommand(said);

        Assert.Equal(WatchCapability.FullScreenVideoTool, match?.ToolName);
        Assert.Equal(exit, match?.Arguments.Text("exit"));
    }

    [Fact]
    public async Task WithNoVideoTheReplySaysSoAndNothingIsAsked()
    {
        Assert.Equal(WatchDesk.NoVideo, await Say());
        Assert.Equal(WatchDesk.NoVideo, await Say(("exit", "true")));
        Assert.Empty(_asked);
    }

    [Fact]
    public async Task APlayingOrPausedVideoGoesFullScreenAndBack()
    {
        _now = new VideoNow(Bunny, VideoStanding.Playing);
        Assert.Equal("Full screen.", await Say());

        _now = new VideoNow(Bunny, VideoStanding.Paused);
        Assert.Equal("Normal size.", await Say(("exit", "true")));

        Assert.Equal([true, false], _asked);
    }

    [Fact]
    public async Task AskingTwiceSaysItIsAlreadySo()
    {
        _now = new VideoNow(Bunny, VideoStanding.Playing);
        _answer = FullScreenAnswer.Already;

        Assert.Equal("The video is already full screen.", await Say());
        Assert.Equal("The video is not full screen.", await Say(("exit", "true")));
    }

    [Fact]
    public async Task TheMiniPanelHasNoFullScreen()
    {
        _now = new VideoNow(Bunny, VideoStanding.Playing);
        _answer = FullScreenAnswer.MiniPanel;

        Assert.Equal("The mini panel has no full screen.", await Say());
    }

    [Fact]
    public async Task TheModelMayCallIt()
    {
        _now = new VideoNow(Bunny, VideoStanding.Playing);

        var result = await Registry().InvokeAsync(
            WatchCapability.FullScreenVideoTool,
            new ToolArguments(new Dictionary<string, string>()),
            TestContext.Current.CancellationToken,
            ToolCaller.Model);

        Assert.False(result.IsError);
        Assert.Equal([true], _asked);
    }
}
