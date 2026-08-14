using D47.Core.Audio;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// A sensible voice per core, chosen once (list.md Phase 11, #33).
/// <para>
/// Chosen by a model or not at all. Matching a core's description against a provider's voice
/// list is a judgement, and the keyword fallback that used to stand in for one handed out
/// voices on the strength of a substring — so a Commander with no model configured had eleven
/// cores confidently miscast rather than eleven cores sounding like the ship.
/// </para>
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

    /// <summary>An ElevenLabs account's list: named voices, an accent rather than a locale.</summary>
    private static IReadOnlyList<VoiceInfo> ElevenLabsVoices() =>
    [
        new("JBFqnCBsd6RMkjVDRZzb", "George", "british", "male"),
        new("XrExE9yKIg1WjnnlVkGX", "Matilda", "american", "female"),
        new("N2lVS1w4EtoT3dr4eOWO", "Callum", "transatlantic", "male"),
    ];

    private static Dictionary<string, string> Nothing() => new(StringComparer.Ordinal);

    [Fact]
    public async Task WithNoModelNothingIsPaired()
    {
        // Bypassed rather than guessed at. The cores keep the voice already in force, and the
        // first model configured pairs them properly.
        var paired = await VoicePairing.ChooseAsync(
            Voices(), Nothing(), provider: null, model: null, spend: null, prices: null, logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(paired);
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
            Voices(),
            existing,
            FakeLlmProvider.Answering("warden = en-US-GuyNeural\ncora = en-US-AriaNeural"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("en-GB-SoniaNeural", paired["warden"]);
        Assert.Equal("en-US-AriaNeural", paired["cora"]);
    }

    [Fact]
    public async Task NoVoicesMeansNoPairingsRatherThanAnError()
    {
        // The provider may be unreachable. Pairing is a convenience and must degrade to nothing.
        var paired = await VoicePairing.ChooseAsync(
            [],
            Nothing(),
            FakeLlmProvider.Answering("warden = en-US-GuyNeural"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(paired);
    }

    [Fact]
    public async Task EveryPairingNamesAVoiceThatActuallyExists()
    {
        // The anti-invention rule, enforced rather than asked for: a model that names a voice
        // the provider does not offer would write a pairing that fails at the first line spoken.
        var paired = await VoicePairing.ChooseAsync(
            Voices(),
            Nothing(),
            FakeLlmProvider.Answering("warden = en-GB-RyanNeural\ncora = a-voice-nobody-offers\nkex = kex"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("en-GB-RyanNeural", paired["warden"]);
        Assert.DoesNotContain("cora", paired.Keys);
        Assert.All(paired.Keys, id => Assert.True(PersonaCatalog.Knows(id), id));
    }

    /// <summary>
    /// Warden on ElevenLabs is not a judgement call, so it does not wait on a model — or need
    /// one. Matched by name, because the id is an account's copy of the voice and the name is
    /// what is the same on every account.
    /// </summary>
    [Fact]
    public async Task WardenTakesGeorgeOnElevenLabsWithNoModelAtAll()
    {
        var paired = await VoicePairing.ChooseAsync(
            ElevenLabsVoices(),
            Nothing(),
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            ttsProvider: "elevenlabs",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("JBFqnCBsd6RMkjVDRZzb", Assert.Single(paired).Value);
        Assert.Equal("warden", paired.Keys.Single());
    }

    [Fact]
    public async Task ANamedDefaultBelongsToItsOwnProviderAndToNoOther()
    {
        // "George" means an ElevenLabs voice. A voice of the same name on another provider is
        // another voice, and pairing it would be a coincidence acted on.
        var paired = await VoicePairing.ChooseAsync(
            [new("en-GB-GeorgeNeural", "George", "en-GB", "Male")],
            Nothing(),
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            ttsProvider: "edge",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(paired);
    }

    [Fact]
    public async Task ANamedDefaultDoesNotDisplaceAChoiceAlreadyMade()
    {
        var existing = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["warden"] = "N2lVS1w4EtoT3dr4eOWO",
        };

        var paired = await VoicePairing.ChooseAsync(
            ElevenLabsVoices(),
            existing,
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            ttsProvider: "elevenlabs",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("N2lVS1w4EtoT3dr4eOWO", paired["warden"]);
    }
}

/// <summary>
/// The lazy half: a voice for the one core the Commander has just selected, asked for at the
/// moment it is needed rather than at a startup that may have run before there was a model, a
/// key, or a voice list.
/// </summary>
public class LazyVoicePairingTests
{
    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new("JBFqnCBsd6RMkjVDRZzb", "George", "british", "male"),
        new("XrExE9yKIg1WjnnlVkGX", "Matilda", "american", "female"),
    ];

    [Fact]
    public async Task WithAModelTheCoreGetsTheVoiceItNamed()
    {
        var voice = await VoicePairing.ChooseOneAsync(
            PersonaCatalog.Cora,
            Voices(),
            taken: [],
            FakeLlmProvider.Answering("cora = XrExE9yKIg1WjnnlVkGX"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("XrExE9yKIg1WjnnlVkGX", voice);
    }

    [Fact]
    public async Task WithNoModelNothingIsChosen()
    {
        // The caller reads null as "leave the voice alone", which is the whole of the no-model
        // behaviour: a core speaks in the voice already in force rather than in a guess.
        var voice = await VoicePairing.ChooseOneAsync(
            PersonaCatalog.Cora,
            Voices(),
            taken: [],
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(voice);
    }

    [Fact]
    public async Task WardenStillTakesGeorgeOnElevenLabsWithNoModel()
    {
        var voice = await VoicePairing.ChooseOneAsync(
            PersonaCatalog.Warden,
            Voices(),
            taken: [],
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            ttsProvider: "elevenlabs",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("JBFqnCBsd6RMkjVDRZzb", voice);
    }

    [Fact]
    public async Task AVoiceAnotherCoreHoldsIsNotOffered()
    {
        // Two cores sharing a voice is the thing the pairing exists to avoid, and the lazy path
        // is the one that runs when the others already have theirs.
        var voice = await VoicePairing.ChooseOneAsync(
            PersonaCatalog.Cora,
            Voices(),
            taken: ["XrExE9yKIg1WjnnlVkGX"],
            FakeLlmProvider.Answering("cora = XrExE9yKIg1WjnnlVkGX"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(voice);
    }
}
