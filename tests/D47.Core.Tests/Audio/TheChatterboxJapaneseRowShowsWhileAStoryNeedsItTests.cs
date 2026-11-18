using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class TheChatterboxJapaneseRowShowsWhileAStoryNeedsItTests
{
    private static SettingRow Row(bool installed, bool storyNeeds) =>
        SpeechCapability.Create(new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            ChatterboxJapaneseInstalled = () => installed,
            StoryCastSpeaksJapaneseOnChatterbox = () => storyNeeds,
            ChatterboxJapaneseState = () => "Not downloaded.",
            DownloadChatterboxJapanese = () => (_, _) => Task.FromResult<string?>(null),
        }).Settings.Single(r => r.Key == SpeechCapability.ChatterboxJapaneseKey);

    [Fact]
    public void AStoryThatNeedsItShowsTheRowUntilItIsDownloaded()
    {
        Assert.True(Row(installed: false, storyNeeds: true).AppliesWhen!(D47Settings.Defaults));
        Assert.False(Row(installed: true, storyNeeds: true).AppliesWhen!(D47Settings.Defaults));
    }

    [Fact]
    public void NoStoryThatNeedsItHidesTheRow() =>
        Assert.False(Row(installed: false, storyNeeds: false).AppliesWhen!(D47Settings.Defaults));

    [Fact]
    public void ItIsTheSameKindOfRowAsChatterboxVoice()
    {
        var row = Row(installed: false, storyNeeds: true);

        Assert.Equal("Chatterbox Japanese", row.Label);
        Assert.Equal(SettingKind.Info, row.Kind);
        Assert.Equal("Download it", row.PressLabel);
        Assert.True(row.PressVisible!());
        Assert.False(string.IsNullOrEmpty(row.Exercise));
        Assert.False(string.IsNullOrEmpty(row.DocsAnchor));
        Assert.Equal("Not downloaded.", row.Binding!.Read!(D47Settings.Defaults));
        Assert.True(SettingsLayout.IsLocalVoiceDownloadFamily(row.Key));
    }
}
