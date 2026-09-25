using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Configuration;

/// <summary>A ship voice the selected provider does not offer is removed and paired again from its list.</summary>
public class AVoiceTheProviderLacksIsReplacedTests
{
    private static readonly string Aboard = VoicePairing.Cores[0].Id;
    private static readonly string Other = VoicePairing.Cores[1].Id;
    private static readonly string Chosen = VoicePairing.Cores[2].Id;

    private static readonly VoiceCatalogue Edge = VoiceCatalogue.Of(
    [
        new VoiceInfo("en-US-AriaNeural", "Aria", "en-US", "Female"),
        new VoiceInfo("en-GB-RyanNeural", "Ryan", "en-GB", "Male"),
        new VoiceInfo("en-AU-NatashaNeural", "Natasha", "en-AU", "Female"),
    ]);

    private static D47Settings Holding() => new()
    {
        Speech = new SpeechSettings
        {
            Provider = SpeechCapability.EdgeId,
            Voice = "alloy",
            CarrierCaptainVoice = "cedar",
        },
        Persona = new PersonaSettings
        {
            VoicesPaired = true,
            Voices = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [Aboard] = "cedar",
                [Other] = "marin",
                [Chosen] = "EN-GB-RYANNEURAL",
            },
        },
    };

    [Fact]
    public void EveryCoreVoiceNotInTheListIsRemoved()
    {
        var after = SpeechCapability.WithoutVoicesNotIn(Holding(), Edge);

        Assert.False(after.Persona.Voices.ContainsKey(Aboard));
        Assert.False(after.Persona.Voices.ContainsKey(Other));
    }

    [Fact]
    public void TheGeneralVoiceIsClearedWhenTheListLacksIt()
    {
        Assert.Null(SpeechCapability.WithoutVoicesNotIn(Holding(), Edge).Speech.Voice);
    }

    [Fact]
    public void AVoiceInTheListIsKeptWhateverItsCase()
    {
        var after = SpeechCapability.WithoutVoicesNotIn(Holding(), Edge);

        Assert.Equal("EN-GB-RYANNEURAL", after.Persona.Voices[Chosen]);
    }

    [Fact]
    public void TheCarrierVoicesAreNotTouched()
    {
        Assert.Equal("cedar", SpeechCapability.WithoutVoicesNotIn(Holding(), Edge).Speech.CarrierCaptainVoice);
    }

    [Fact]
    public void NothingIsRemovedWhenEveryVoiceIsListed()
    {
        var settings = Holding() with
        {
            Speech = new SpeechSettings { Provider = SpeechCapability.EdgeId, Voice = "en-US-AriaNeural" },
            Persona = new PersonaSettings
            {
                Voices = new Dictionary<string, string>(StringComparer.Ordinal) { [Aboard] = "en-AU-NatashaNeural" },
            },
        };

        Assert.Same(settings, SpeechCapability.WithoutVoicesNotIn(settings, Edge));
    }

    public static TheoryData<string> ListsThatDidNotArrive => ["no key", "key rejected", "unreachable", "empty"];

    [Theory]
    [MemberData(nameof(ListsThatDidNotArrive))]
    public void NothingIsRemovedWhenTheListDidNotArrive(string why)
    {
        var list = why switch
        {
            "no key" => VoiceCatalogue.NoKey("no key"),
            "key rejected" => VoiceCatalogue.KeyRejected("401"),
            "unreachable" => VoiceCatalogue.Unreachable("timeout"),
            _ => VoiceCatalogue.Of([]),
        };

        var settings = Holding();

        Assert.Same(settings, SpeechCapability.WithoutVoicesNotIn(settings, list));
    }

    [Fact]
    public async Task ThePairingThenGivesTheCoreAVoiceFromTheList()
    {
        var removed = SpeechCapability.WithoutVoicesNotIn(Holding(), Edge);

        var paired = await VoicePairing.ChooseAsync(
            Edge.Voices, removed.Persona.Voices, null, null, null, null, null, cancellationToken: TestContext.Current.CancellationToken);

        var offered = Edge.Voices.Select(voice => voice.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(paired[Aboard], offered);
        Assert.Contains(paired[Other], offered);
    }
}
