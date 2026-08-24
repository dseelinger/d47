using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>
/// <b>The spoken phrases must not collide with the headset's</b> (list.md Phase 51).
/// <para>
/// <c>VrCapability.ModeKey</c> already owns "mini panel" and "full panel". A Commander in a headset
/// who says those must not shrink a window they cannot see, and one at a desk must not resize a
/// quad they are not wearing. So the window's are "mini window" and "full window", and each phrase
/// reaches exactly one surface.
/// </para>
/// <para>
/// <c>SettingsByVoiceTests.EveryDeclaredCommandPhraseActuallyRoutes</c> already asserts that every
/// declared phrase routes to <em>its own</em> row, so a collision would fail there too. This names
/// the four that matter, because the claim is specific enough to be worth reading.
/// </para>
/// </summary>
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

    /// <summary>
    /// And the row itself round-trips, so the phrase that matched writes what it said it would.
    /// </summary>
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
