using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>Play, pause and next reach the story playback while it is open, and the music otherwise.</summary>
public sealed class SpokenPlayPauseAndNextDrivePlaybackTests
{
    private static KeywordRouter Router(bool open)
    {
        var desk = new AdventureCapability.AdventureDesk { PlaybackOpen = () => open };
        var registry = CapabilityRegistry.Build(
            [AdventureCapability.Create(desk: desk), AudioCapability.Create(storyPlaybackOpen: () => open)]);

        return new KeywordRouter(registry);
    }

    [Theory]
    [InlineData("pause", "pause")]
    [InlineData("play", "play")]
    [InlineData("resume", "play")]
    [InlineData("next", "next")]
    [InlineData("next line", "next")]
    [InlineData("skip", "next")]
    public void WithAPlaybackOpenTheWordsReachThePlayback(string said, string action)
    {
        var match = Router(open: true).MatchToolCommand(said);

        Assert.Equal(AdventureCapability.StoryPlaybackTool, match?.ToolName);
        Assert.Equal(action, match!.Arguments.Values["action"]);
    }

    [Theory]
    [InlineData("pause", "pause")]
    [InlineData("play", "resume")]
    public void WithNoPlaybackOpenPauseAndPlayReachTheMusic(string said, string action)
    {
        var match = Router(open: false).MatchToolCommand(said);

        Assert.Equal(AudioCapability.ManageMusicTool, match?.ToolName);
        Assert.Equal(action, match!.Arguments.Values["action"]);
    }

    [Theory]
    [InlineData("next")]
    [InlineData("next line")]
    [InlineData("skip")]
    public void WithNoPlaybackOpenNextMatchesNothing(string said) =>
        Assert.Null(Router(open: false).MatchToolCommand(said));

    [Fact]
    public void TheLongerMusicPhrasesStayLiveDuringAPlayback()
    {
        var router = Router(open: true);

        Assert.Equal(AudioCapability.ManageMusicTool, router.MatchToolCommand("pause the music")?.ToolName);
        Assert.Equal(AudioCapability.ManageMusicTool, router.MatchToolCommand("next track")?.ToolName);
    }

    [Fact]
    public async Task TheModelIsRefusedAndTheCommanderIsAnsweredInAWord()
    {
        var acted = new List<string>();
        var desk = new AdventureCapability.AdventureDesk { PlaybackOpen = () => true, Playback = action => { acted.Add(action); return null; } };
        var registry = CapabilityRegistry.Build([AdventureCapability.Create(desk: desk)]);
        var pause = new ToolArguments(new Dictionary<string, string> { ["action"] = "pause" });

        var refused = await registry.InvokeAsync(AdventureCapability.StoryPlaybackTool, pause, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(refused.IsError);
        Assert.Empty(acted);

        var done = await registry.InvokeAsync(AdventureCapability.StoryPlaybackTool, pause, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(done.IsError);
        Assert.Equal("Paused.", done.Content);
        Assert.Equal(["pause"], acted);
    }
}
