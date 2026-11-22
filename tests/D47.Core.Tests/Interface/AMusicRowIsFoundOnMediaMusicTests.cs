using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>A spoken Find naming one of the music rows opens it on Media › Music, not on the Settings tab (#2683).</summary>
public class AMusicRowIsFoundOnMediaMusicTests
{
    private static PanelFind Find()
    {
        var settings = TestSurface.SettingsFor(new MemoryInstall());

        settings.Bind(CapabilityRegistry.Build([AudioCapability.Create(music: _ => string.Empty)]));

        var nav = new PanelNavigator();
        nav.Register(PanelTab.Media, new NavCrumb("media-music", "Music"));

        return new PanelFind(nav, settings, new Dictionary<PanelTab, string> { [PanelTab.Media] = "Media" });
    }

    [Theory]
    [InlineData("where is the music publish token setting", AudioCapability.MusicPublishTokenKey, "Music publish token")]
    [InlineData("where is the download the d47 music library setting", AudioCapability.MusicLibraryKey, "Download the D47 music library")]
    public void TheRowOpensOnMediaMusic(string spoken, string key, string label)
    {
        var answer = PanelFindPhrases.Answer(spoken, Find());

        Assert.NotNull(answer?.Open);
        Assert.Equal(PanelFindKind.SettingRow, answer.Open.Kind);
        Assert.Equal(key, answer.Open.Target);
        Assert.Equal($"Media › Music › {label}.", answer.Said);
    }
}
