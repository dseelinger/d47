using D47.Core.Audio;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>An automatic pairing of Nicole is replaced; a hand-picked Nicole is kept.</summary>
public class ACastNicoleIsRecastOnceTests
{
    private const string Nicole = "af_nicole";

    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new(Nicole, "Nicole", "en-US", "female"),
        new("af_heart", "Heart", "en-US", "female"),
        new("af_bella", "Bella", "en-US", "female"),
    ];

    private static Dictionary<string, string> Map(string voice) =>
        new(StringComparer.Ordinal) { ["covas"] = voice };

    [Fact]
    public async Task APairedNicoleIsReplacedAndTheNewPairingRecorded()
    {
        var before = Map(Nicole);
        var recorded = Map(Nicole);

        var repair = await VoicePairing.WithReplacementsAsync(
            before,
            VoicePairing.WithoutNotCastPairings(before, recorded),
            Voices(),
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(Nicole, repair.Voices["covas"]);
        Assert.True(repair.Complete);
        Assert.Equal(
            repair.Voices["covas"],
            VoicePairing.WithPairingsRecorded(recorded, before, repair.Voices)["covas"]);
    }

    [Fact]
    public void ANicoleWithNoMatchingPairingIsKept()
    {
        var before = Map(Nicole);

        Assert.Same(before, VoicePairing.WithoutNotCastPairings(before, new Dictionary<string, string>()));
        Assert.Same(before, VoicePairing.WithoutNotCastPairings(before, Map("af_heart")));
    }
}
