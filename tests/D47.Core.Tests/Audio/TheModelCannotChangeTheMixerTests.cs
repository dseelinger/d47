using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>The model cannot mute or turn down D47's voice or the danger callouts; the panel can.</summary>
public class TheModelCannotChangeTheMixerTests
{
    [Fact]
    public void TheModelCannotSetALevelAMuteOrADuck()
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        Assert.False(settings.Apply(AudioCapability.LevelKey(AudioChannel.Speech), "0", SettingsCaller.Model).Ok);
        Assert.False(settings.Apply(AudioCapability.LevelKey(AudioChannel.Alert), "0", SettingsCaller.Model).Ok);
        Assert.False(settings.Apply(AudioCapability.MuteKey(AudioChannel.Speech), "true", SettingsCaller.Model).Ok);
        Assert.False(settings.Apply(AudioCapability.DuckKey(AudioChannel.Music), "0", SettingsCaller.Model).Ok);
    }

    [Fact]
    public void ThePanelCanSetALevel()
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        Assert.True(settings.Apply(AudioCapability.LevelKey(AudioChannel.Speech), "0.5", SettingsCaller.Panel).Ok);
    }
}
