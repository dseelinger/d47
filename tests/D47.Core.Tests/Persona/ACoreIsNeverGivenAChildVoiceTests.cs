using D47.Core.Audio;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

public class ACoreIsNeverGivenAChildVoiceTests
{
    private const string Maisie = "en-GB-MaisieNeural";
    private const string Ana = "en-US-AnaNeural";
    private const string Sonia = "en-GB-SoniaNeural";

    private static VoiceInfo Voice(string id, string locale, string gender) => new(id, id, locale, gender);

    private static IReadOnlyList<VoiceInfo> EdgeVoices() =>
    [
        Voice("en-GB-LibbyNeural", "en-GB", "Female"),
        Voice(Maisie, "en-GB", "Female"),
        Voice(Sonia, "en-GB", "Female"),
        Voice(Ana, "en-US", "Female"),
        Voice("en-US-GuyNeural", "en-US", "Male"),
        Voice("en-US-JennyNeural", "en-US", "Female"),
        Voice("en-GB-RyanNeural", "en-GB", "Male"),
        Voice("en-AU-WilliamNeural", "en-AU", "Male"),
    ];

    private static Task<IReadOnlyDictionary<string, string>> CastAsync(IReadOnlyList<VoicePairing.Slot> slots, int seed) =>
        VoicePairing.ChooseForAsync(
            EdgeVoices(), slots, taken: [], provider: null, model: null, spend: null, prices: null, logger: null,
            random: new Random(seed), cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public async Task CovasOnEdgeIsAlwaysSonia()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var chosen = await CastAsync([VoicePairing.SlotFor(PersonaCatalog.Covas)], seed);

            Assert.Equal(Sonia, chosen["covas"]);
        }
    }

    [Fact]
    public async Task NoSlotIsPairedWithMaisieOrAna()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var chosen = await CastAsync([.. VoicePairing.Cores, .. VoicePairing.CarrierRoles], seed);

            Assert.DoesNotContain(chosen.Values, voice => voice is Maisie or Ana);
        }
    }

    [Fact]
    public void ACoreAutomaticallyPairedWithMaisieIsCastAgainAndAHandPickedOneIsKept()
    {
        var voices = new Dictionary<string, string> { ["covas"] = Maisie, ["cora"] = Ana };
        var recorded = new Dictionary<string, string> { ["covas"] = Maisie };

        var kept = VoicePairing.WithoutNotCastPairings(voices, recorded);

        Assert.False(kept.ContainsKey("covas"));
        Assert.Equal(Ana, kept["cora"]);
    }
}
