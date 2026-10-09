using D47.Core.Audio;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Audio;

public class ACustomVoiceIsNeverDrawnForAStrangerTests
{
    private static readonly VoiceInfo[] Voices =
    [
        new("marlow", "Marlow", "en", "female"),
        new("orson", "Orson", "en", "male"),
        new("own", "Your voice", "en", "female") { Custom = true },
        new("my-0badf00d", "Ally", "en", "female") { Custom = true },
    ];

    [Fact]
    public void ThePoolLeavesThemOut() =>
        Assert.Equal(["marlow", "orson"], VoicePool.From(Voices));

    [Fact]
    public void TheWomensVoicesLeaveThemOut() =>
        Assert.Equal<string[]>(["marlow"], [.. VoicePool.Feminine(Voices)]);

    [Fact]
    public async Task ACoreIsNeverPairedWithOne()
    {
        var paired = await VoicePairing.ChooseForAsync(
            [.. Voices.Where(voice => voice.Custom)],
            VoicePairing.Cores,
            [],
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(paired);
    }
}
