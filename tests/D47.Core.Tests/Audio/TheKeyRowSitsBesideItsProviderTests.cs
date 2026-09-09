using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Where the ElevenLabs key row sits in the Speech card.</summary>
public class TheKeyRowSitsBesideItsProviderTests
{
    private static IReadOnlyList<string> Keys() =>
        [.. SpeechCapability.Create(new SpeechCapability.SpeechSurface
        {
            Silence = () => { },
            Beds = () => [],
        }).Settings.Select(row => row.Key)];

    [Fact]
    public void TheKeyRowsFollowTheProviderRowImmediately()
    {
        var keys = Keys();
        var needKeys = TtsProviderCatalog.All.Where(provider => provider.NeedsKey).ToArray();

        Assert.NotEmpty(needKeys);

        // Output device took the top of the card on 2026-09-01 — every other row here is about *how* D47
        // sounds and that one is about whether the Commander hears it at all.
        Assert.Equal(SpeechCapability.OutputDeviceKey, keys[0]);
        Assert.Equal(SpeechCapability.ProviderKey, keys[1]);

        Assert.Equal(
            [.. needKeys.Select(SpeechCapability.KeyRowFor)],
            keys.Skip(2).Take(needKeys.Length));
    }

    /// <summary>
    /// And the rest of the card is unchanged behind them — this moved one block of rows, it did not
    /// reorder the section.
    /// </summary>
    [Fact]
    public void TheVoiceRowStillFollowsTheKeys()
    {
        var keys = Keys();
        var needKeys = TtsProviderCatalog.All.Count(provider => provider.NeedsKey);

        Assert.Equal(SpeechCapability.VoiceKey, keys[needKeys + 2]);

 // The ElevenLabs model joined the pair on 2026-09-04, between them rather than beside its
        // provider's key: a key is setup and this is a choice about how d47 sounds, which is what the voice
        // and the rate are.
        Assert.Equal(SpeechCapability.ElevenLabsModelKey, keys[needKeys + 3]);
        Assert.Equal(SpeechCapability.RateKey, keys[needKeys + 4]);
    }
}
