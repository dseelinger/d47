using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The headset's mini panel phrases, kept apart from the window's own settings.</summary>
public class PanelModePhrasesTests
{
    [Theory]
    [InlineData("mini panel", "mini")]
    [InlineData("full panel", "full")]
    public void EachPhraseReachesTheHeadsetsModeSetting(string spoken, string value)
    {
        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install).Registry);

        var match = router.MatchSetting(spoken);

        Assert.NotNull(match);
        Assert.Equal(VrCapability.ModeKey, match!.Row.Key);
        Assert.Equal(value, match.Value);
    }
}
