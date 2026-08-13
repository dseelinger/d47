using D47.Core.Audio;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// A sensible voice per core, chosen once (list.md Phase 11, #33).
/// </summary>
public class VoicePairingTests
{
    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new("en-GB-SoniaNeural", "Sonia", "en-GB", "Female"),
        new("en-GB-RyanNeural", "Ryan", "en-GB", "Male"),
        new("en-US-AriaNeural", "Aria", "en-US", "Female"),
        new("en-US-GuyNeural", "Guy", "en-US", "Male"),
        new("en-US-DavisNeural", "Davis", "en-US", "Male"),
        new("en-GB-ThomasNeural", "Thomas", "en-GB", "Male"),
    ];

    private static Dictionary<string, string> Nothing() => new(StringComparer.Ordinal);

    [Fact]
    public async Task WithNoModelEveryCoreStillGetsAVoiceAndNoTwoShareOne()
    {
        // The fallback only has to beat "every core sounds identical", which is the thing the
        // item exists to prevent.
        var paired = await VoicePairing.ChooseAsync(
            Voices(), Nothing(), provider: null, model: null, spend: null, prices: null, logger: null,
            TestContext.Current.CancellationToken);

        // Six voices, eleven cores — so not all of them can be distinct, but the ones that fit
        // must not double up while there are voices left.
        Assert.NotEmpty(paired);
        Assert.Equal(paired.Count, paired.Values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task AVoiceTheCommanderChoseIsNeverOverwritten()
    {
        // Nothing distinguishes a hand-picked pairing from one an earlier run made, so the rule
        // has to be "never overwrite" rather than "overwrite the ones we made".
        var existing = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["warden"] = "en-GB-SoniaNeural",
        };

        var paired = await VoicePairing.ChooseAsync(
            Voices(), existing, provider: null, model: null, spend: null, prices: null, logger: null,
            TestContext.Current.CancellationToken);

        Assert.Equal("en-GB-SoniaNeural", paired["warden"]);
    }

    [Fact]
    public async Task NoVoicesMeansNoPairingsRatherThanAnError()
    {
        // The provider may be unreachable. Pairing is a convenience and must degrade to nothing.
        var paired = await VoicePairing.ChooseAsync(
            [], Nothing(), provider: null, model: null, spend: null, prices: null, logger: null,
            TestContext.Current.CancellationToken);

        Assert.Empty(paired);
    }

    [Fact]
    public async Task EveryPairingNamesAVoiceThatActuallyExists()
    {
        var voices = Voices();

        var paired = await VoicePairing.ChooseAsync(
            voices, Nothing(), provider: null, model: null, spend: null, prices: null, logger: null,
            TestContext.Current.CancellationToken);

        var known = voices.Select(v => v.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(paired.Values, voice => Assert.Contains(voice, known));
        Assert.All(paired.Keys, id => Assert.True(PersonaCatalog.Knows(id), id));
    }

    [Fact]
    public void TheKeywordFallbackPrefersAMatchAndThenAnythingUnused()
    {
        var voices = Voices();

        // Cora's hint leads with "aria", and it is in the list.
        Assert.Equal("en-US-AriaNeural", VoicePairing.Nearest(PersonaCatalog.Cora, voices, []));

        // Taken, so it moves down her keyword list rather than doubling up.
        var second = VoicePairing.Nearest(PersonaCatalog.Cora, voices, ["en-US-AriaNeural"]);
        Assert.NotEqual("en-US-AriaNeural", second);
        Assert.NotNull(second);
    }

    [Fact]
    public void ACoreWhoseKeywordsMatchNothingStillGetsAVoice()
    {
        // Falling back to the ship AI's voice would mean two cores sounding the same, which is
        // the thing being avoided.
        var voices = new List<VoiceInfo> { new("x-1", "Zzz", "en-GB") };

        Assert.Equal("x-1", VoicePairing.Nearest(PersonaCatalog.Kex, voices, []));
    }
}
