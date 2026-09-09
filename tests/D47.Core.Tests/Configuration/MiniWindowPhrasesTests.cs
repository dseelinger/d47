using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>The spoken phrases must not collide with the headset's.</summary>
public class MiniWindowPhrasesTests
{
    [Theory]
    [InlineData("mini window", InterfaceCapability.WindowModeKey, "mini")]
    [InlineData("full window", InterfaceCapability.WindowModeKey, "full")]
    [InlineData("mini panel", VrCapability.ModeKey, "mini")]
    [InlineData("full panel", VrCapability.ModeKey, "full")]
    public void EachPhraseReachesOneSurfaceAndOnlyOne(string spoken, string key, string value)
    {
        using var install = new TempInstall();
        var router = new KeywordRouter(TestSurface.For(install).Registry);

        var match = router.MatchSetting(spoken);

        Assert.NotNull(match);
        Assert.Equal(key, match!.Row.Key);
        Assert.Equal(value, match.Value);
    }

    /// <summary>And the row itself round-trips, so the phrase that matched writes what it said it would.</summary>
    [Theory]
    [InlineData("mini", "mini")]
    [InlineData("full", "full")]
    [InlineData("something a hand-edited file said", "full")]
    public void TheRowWritesOneOfTwoWordsAndNothingElse(string written, string expected)
    {
        var row = InterfaceCapability.Create().Settings
            .Single(row => row.Key == InterfaceCapability.WindowModeKey);

        var binding = row.Binding;

        Assert.NotNull(binding);
        Assert.NotNull(binding.Write);

        var after = binding.Write(new D47.Core.Configuration.D47Settings(), written);

        Assert.Equal(expected, after.Ui.Mode);
        Assert.Equal(expected, binding.Read(after));
    }
}
