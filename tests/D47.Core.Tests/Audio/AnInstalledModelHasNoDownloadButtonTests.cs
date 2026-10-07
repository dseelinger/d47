using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AnInstalledModelHasNoDownloadButtonTests
{
    private static SettingRow Row(string key, bool installed)
    {
        var surface = new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            DownloadLocalVoice = () => (_, _) => Task.FromResult<string?>(null),
            DownloadChatterbox = () => (_, _) => Task.FromResult<string?>(null),
            LocalVoiceInstalled = () => installed,
            ChatterboxInstalled = () => installed,
        };

        return SpeechCapability.Create(surface).Settings.Single(row => row.Key == key);
    }

    [Theory]
    [InlineData(SpeechCapability.LocalVoiceKey)]
    [InlineData(SpeechCapability.ChatterboxVoiceKey)]
    public void TheButtonIsHiddenOnceTheModelIsInstalled(string key) =>
        Assert.False(Row(key, installed: true).PressVisible!());

    [Theory]
    [InlineData(SpeechCapability.LocalVoiceKey)]
    [InlineData(SpeechCapability.ChatterboxVoiceKey)]
    public void TheButtonShowsWhileTheModelIsMissing(string key)
    {
        var row = Row(key, installed: false);

        Assert.True(row.PressVisible!());
        Assert.Equal("Download it", row.PressLabel);
    }

    [Theory]
    [InlineData(SpeechCapability.LocalVoiceKey)]
    [InlineData(SpeechCapability.ChatterboxVoiceKey)]
    public void TheButtonGoesWhenTheDownloadFinishesWithoutRebuildingTheRow(string key)
    {
        var installed = false;
        var surface = new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            LocalVoiceInstalled = () => installed,
            ChatterboxInstalled = () => installed,
        };

        var row = SpeechCapability.Create(surface).Settings.Single(r => r.Key == key);

        Assert.True(row.PressVisible!());

        installed = true;

        Assert.False(row.PressVisible!());
    }
}
