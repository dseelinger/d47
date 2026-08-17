using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Which voices a re-voiced sender can be drawn from
/// (remediation.md, "Named NPCs should each use a different voice").
/// <para>
/// This was measured against a real account, not imagined: the installed build logged
/// "ElevenLabs offers 473 voices" and, on the next line, "1 voices are available for re-voiced
/// senders". Every NPC in a system shared that one, so the per-name assignment beside it had
/// nothing left to assign.
/// </para>
/// </summary>
public class VoicePoolTests
{
    /// <summary>
    /// Edge's tags are real locales, and filtering them is the reason this filter exists: a
    /// wingmate drawn from every language Edge supports is a wingmate most Commanders cannot
    /// understand.
    /// </summary>
    [Theory]
    [InlineData("en-GB", true)]
    [InlineData("en-US", true)]
    [InlineData("en", true)]
    [InlineData("de-DE", false)]
    [InlineData("fr-FR", false)]
    [InlineData("zh-CN", false)]
    public void ALocaleIsFilteredByLanguage(string locale, bool eligible) =>
        Assert.Equal(eligible, VoicePool.Eligible(locale));

    /// <summary>
    /// ElevenLabs puts an <em>accent</em> in the same field, because its model is multilingual
    /// and a voice does not belong to a locale. None of these is a language tag, and none of
    /// them is a reason to throw a voice away.
    /// </summary>
    [Theory]
    [InlineData("american")]
    [InlineData("british")]
    [InlineData("australian")]
    [InlineData("irish")]
    [InlineData("multilingual")]
    [InlineData("transatlantic")]
    public void AnAccentIsNotALocaleAndIsNotFilteredOn(string accent) =>
        Assert.True(VoicePool.Eligible(accent));

    [Fact]
    public void AnUntaggedVoiceIsEligible()
    {
        Assert.True(VoicePool.Eligible(string.Empty));
        Assert.True(VoicePool.Eligible(null));
    }

    /// <summary>
    /// The whole account, end to end, in the shape the provider reports it: an accent label per
    /// voice. All of it is the pool, which is what the class comment beside it always claimed
    /// and what the code did not do.
    /// </summary>
    [Fact]
    public void AnAccountTaggedByAccentIsEntirelyThePool()
    {
        VoiceInfo Voice(string id, string accent) => new(id, id, accent);

        var account = new[]
        {
            Voice("v1", "american"),
            Voice("v2", "british"),
            Voice("v3", "multilingual"),
            Voice("v4", "australian"),
        };

        Assert.Equal(["v1", "v2", "v3", "v4"], VoicePool.From(account));
    }

    /// <summary>And a locale-tagged catalogue is still cut down to what can be understood.</summary>
    [Fact]
    public void AnAccountTaggedByLocaleIsCutToEnglish()
    {
        VoiceInfo Voice(string id, string locale) => new(id, id, locale);

        var account = new[]
        {
            Voice("ryan", "en-GB"),
            Voice("katja", "de-DE"),
            Voice("aria", "en-US"),
            Voice("denise", "fr-FR"),
        };

        Assert.Equal(["ryan", "aria"], VoicePool.From(account));
    }
}
