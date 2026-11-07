using System.Text.Json;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Audio;

public sealed class TheAudioPlayerOpensByKeyAndVoiceTests
{
    private static KeywordRouter Router() =>
        new(CapabilityRegistry.Build([AudioCapability.Create(audioPlayer: _ => "ok", music: _ => "ok")]));

    [Fact]
    public void ASettingsFileWithNoAudioPlayerKeyGetsCtrlShiftP()
    {
        var read = JsonSerializer.Deserialize<D47Settings>("""{"hotkeys":{"find":"Ctrl+F"}}""", SettingsStore.Json)!;

        Assert.Equal("Ctrl+Shift+P", read.Hotkeys.AudioPlayer);
        Assert.Null(read.Hotkeys.AudioPlayerButton);
    }

    [Theory]
    [InlineData("audio player", "toggle")]
    [InlineData("open the audio player", "open")]
    [InlineData("show the audio player", "open")]
    [InlineData("close the audio player", "close")]
    public void ThePhrasesReachTheAudioPlayerTool(string said, string action)
    {
        var match = Router().MatchToolCommand(said);

        Assert.Equal(AudioCapability.AudioPlayerTool, match?.ToolName);
        Assert.True(match!.Arguments.TryGetString("action", out var got));
        Assert.Equal(action, got);
    }

    [Theory]
    [InlineData("play", "resume")]
    [InlineData("pause", "pause")]
    public void PlayAndPauseReachManageMusicAndNoSettingClaimsThem(string said, string action)
    {
        var router = Router();
        var match = router.MatchToolCommand(said);

        Assert.Equal(AudioCapability.ManageMusicTool, match?.ToolName);
        Assert.True(match!.Arguments.TryGetString("action", out var got));
        Assert.Equal(action, got);
        Assert.Null(router.MatchSetting(said));
    }

    [Fact]
    public void PauseGalnetIsNotReadAsPause()
    {
        Assert.Null(Router().MatchToolCommand("pause galnet"));
    }

    [Fact]
    public void ThePlayerToolIsOneOfTheModelsToCall()
    {
        var tool = AudioCapability.Create().Tools.Single(t => t.Name == AudioCapability.AudioPlayerTool);

        Assert.False(tool.Protected);
    }

    [Fact]
    public async Task AMissingPlayerAnswersThatItIsNotAvailable()
    {
        var tool = AudioCapability.Create().Tools.Single(t => t.Name == AudioCapability.AudioPlayerTool);
        var result = await tool.Handler(new ToolArguments(new Dictionary<string, string> { ["action"] = "open" }), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public void APlayingTrackReadsAsPlayingWithItsFolderAndAPauseButton()
    {
        var read = AudioPlayerReadout.From(new MusicState("long-drift-04.mp3", true, false, false, "supercruise"), false);

        Assert.Equal("Playing", read.StateWord);
        Assert.Equal("long-drift-04.mp3", read.Track);
        Assert.Equal("supercruise", read.Folder);
        Assert.Equal("Pause", read.PlayLabel);
        Assert.True(read.Enabled);
        Assert.True(read.Playing);
    }

    [Fact]
    public void APausedTrackOffersPlay()
    {
        var read = AudioPlayerReadout.From(new MusicState("a.mp3", false, true, false, "docked"), false);

        Assert.Equal("Paused", read.StateWord);
        Assert.Equal("Play", read.PlayLabel);
        Assert.True(read.Enabled);
        Assert.False(read.Playing);
    }

    [Fact]
    public void NothingPlayingReadsAsDashesWithTheButtonsOff()
    {
        var read = AudioPlayerReadout.From(new MusicState(null, false, false), false);

        Assert.Equal("Nothing playing", read.StateWord);
        Assert.Equal(AudioPlayerReadout.Nothing, read.Track);
        Assert.Equal(AudioPlayerReadout.Nothing, read.Folder);
        Assert.Equal("Play", read.PlayLabel);
        Assert.False(read.Enabled);
    }

    [Fact]
    public void AMutedChannelReadsAsMutedWithTheButtonsOff()
    {
        var read = AudioPlayerReadout.From(new MusicState("a.mp3", true, false, false, "docked"), true);

        Assert.Equal("Muted", read.StateWord);
        Assert.Equal(AudioPlayerReadout.Nothing, read.Track);
        Assert.False(read.Enabled);
        Assert.False(read.Playing);
    }
}
