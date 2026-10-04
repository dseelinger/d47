using D47.Core.Audio;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

public class TheStockCovasIsACalmBritishWomanTests
{
    private static VoiceInfo Voice(string id, string gender, string locale, string description) =>
        new(id, id, locale, gender) { Description = description };

    private static VoiceInfo Louise { get; } = new("louise", "Louise - Calm & Neutral Narration", "british", "female");

    private static async Task<IReadOnlyDictionary<string, string>> CastAsync(
        IReadOnlyList<VoiceInfo> voices, IReadOnlyList<VoicePairing.Slot>? slots = null, int seed = 0) =>
        await VoicePairing.ChooseForAsync(
            voices,
            slots ?? [VoicePairing.SlotFor(PersonaCatalog.Covas)],
            taken: [],
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            random: new Random(seed),
            cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public async Task ACalmBritishWomanBeatsEveryoneElse()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var chosen = await CastAsync(
                [
                    Voice("us-calm", "female", "american", "Calm"),
                    Voice("gb-man", "male", "british", "Calm"),
                    Voice("gb-warm", "female", "british", "Warm"),
                    Louise,
                ],
                seed: seed);

            Assert.Equal("louise", chosen["covas"]);
        }
    }

    [Fact]
    public async Task WithNoCalmOneABritishWomanIsCast()
    {
        var chosen = await CastAsync(
            [Voice("us", "female", "american", "Calm"), Voice("gb", "female", "british", "Warm"), Voice("gb-man", "male", "british", "Calm")]);

        Assert.Equal("gb", chosen["covas"]);
    }

    [Fact]
    public async Task WithNoBritishWomanAWomanIsCast()
    {
        var chosen = await CastAsync(
            [Voice("gb-man", "male", "british", "Calm"), Voice("us", "female", "american", "Warm")]);

        Assert.Equal("us", chosen["covas"]);
    }

    [Fact]
    public async Task WithNoWomanLabelledCovasIsCastAsBefore()
    {
        var chosen = await CastAsync([Voice("man", "male", "british", "Calm")]);

        Assert.Equal("man", chosen["covas"]);
    }

    [Fact]
    public async Task NoOtherCoreIsGivenTheCovasVoice()
    {
        var chosen = await CastAsync(
            [Louise, .. Enumerable.Range(0, 20).Select(n => Voice($"v{n}", n % 2 == 0 ? "female" : "male", "american", "Warm"))],
            VoicePairing.Cores);

        Assert.Equal("louise", chosen["covas"]);
        Assert.DoesNotContain(chosen.Where(pair => pair.Key != "covas"), pair => pair.Value == "louise");
    }
}
