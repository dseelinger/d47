using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Where the ElevenLabs key row sits in the Speech card (change-requests.md 17).
/// <para>
/// Selecting a provider is what makes its key relevant, so the row that answers that choice
/// belongs beneath the row that made it. Appended to the end of the card it sat below sixteen
/// rows about rates, cues, retries and egress: the dropdown asked for a key and the box to put
/// one in was off the bottom of the screen.
/// </para>
/// <para>
/// This is about order alone. Which key rows exist and when they apply was already settled — one
/// per provider that needs one, each declaring the provider it belongs to.
/// </para>
/// </summary>
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
        Assert.Equal(SpeechCapability.ProviderKey, keys[0]);

        Assert.Equal(
            [.. needKeys.Select(SpeechCapability.KeyRowFor)],
            keys.Skip(1).Take(needKeys.Length));
    }

    /// <summary>
    /// And the rest of the card is unchanged behind them — this moved one block of rows, it did
    /// not reorder the section.
    /// </summary>
    [Fact]
    public void TheVoiceRowStillFollowsTheKeys()
    {
        var keys = Keys();
        var needKeys = TtsProviderCatalog.All.Count(provider => provider.NeedsKey);

        Assert.Equal(SpeechCapability.VoiceKey, keys[needKeys + 1]);
        Assert.Equal(SpeechCapability.RateKey, keys[needKeys + 2]);
    }
}
