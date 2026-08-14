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

    /// <summary>
    /// The account this was found on names its voices "George - Warm, Captivating Storyteller",
    /// and an exact match found nothing — so the one pairing that is not a judgement call went
    /// to the model with all the others, and George was handed to whoever it liked.
    /// </summary>
    [Fact]
    public async Task WardenStillTakesGeorgeWhenTheAccountAppendsADescriptor()
    {
        var paired = await VoicePairing.ChooseAsync(
            [
                new("pqHfZKP75CvOlQylNhV4", "Bill - Wise, Mature, Balanced", "american", "male"),
                new("JBFqnCBsd6RMkjVDRZzb", "George - Warm, Captivating Storyteller", "british", "male"),
            ],
            Nothing(),
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            ttsProvider: "elevenlabs",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("JBFqnCBsd6RMkjVDRZzb", paired["warden"]);
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

    /// <summary>
    /// Ten of the eleven cores are written as men and one as a woman, and a voice of the wrong
    /// gender is not a near miss — it is a different character saying the lines. The model is
    /// told which is which and its answer is checked against it, because it has been observed
    /// weighing "a fussy, over-articulated man" against an accent and preferring the accent.
    /// </summary>
    [Fact]
    public async Task ACoreWrittenAsAManIsNotGivenAWomansVoice()
    {
        var paired = await VoicePairing.ChooseAsync(
            Voices(),
            Nothing(),
            FakeLlmProvider.Answering(
                "analyst-prime = en-US-AriaNeural\ncora = en-GB-SoniaNeural\nwarden = en-GB-RyanNeural"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.DoesNotContain("analyst-prime", paired.Keys);
        Assert.Equal("en-GB-SoniaNeural", paired["cora"]);
        Assert.Equal("en-GB-RyanNeural", paired["warden"]);
    }

    /// <summary>
    /// A provider that says nothing about gender must not end up offering nothing. The check
    /// refuses a contradiction; silence is not one.
    /// </summary>
    [Fact]
    public async Task AVoiceTheProviderDoesNotLabelIsStillOffered()
    {
        var paired = await VoicePairing.ChooseAsync(
            [new("some-voice", "Unlabelled", "en-GB")],
            Nothing(),
            FakeLlmProvider.Answering("analyst-prime = some-voice"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("some-voice", paired["analyst-prime"]);
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

/// <summary>
/// The pairings written before the pass was told which cores are men (list.md Phase 11, #33).
/// <para>
/// A pairing is otherwise never re-derived, because a hand-picked one is indistinguishable from
/// a derived one — but that rule protects a choice, and a core written as a man speaking in a
/// woman's voice is the pairing pass having answered a question it was not asked. It is dropped
/// once, so the ordinary path chooses again.
/// </para>
/// </summary>
public class MiscastVoicesAreDroppedTests
{
    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new("en-US-AriaNeural", "Aria", "en-US", "Female"),
        new("en-GB-RyanNeural", "Ryan", "en-GB", "Male"),
        new("unlabelled", "Nobody Says", "en-GB"),
    ];

    private static Dictionary<string, string> Paired(params (string Persona, string Voice)[] pairs) =>
        pairs.ToDictionary(pair => pair.Persona, pair => pair.Voice, StringComparer.Ordinal);

    [Fact]
    public void AManSpeakingInAWomansVoiceLosesIt()
    {
        var kept = VoicePairing.WithoutMiscastVoices(
            Paired(("analyst-prime", "en-US-AriaNeural")), Voices());

        Assert.Empty(kept);
    }

    [Fact]
    public void EveryOtherPairingSurvives()
    {
        // Including the core the miscast one sat beside: this drops a pairing, it does not
        // re-run the pairing.
        var kept = VoicePairing.WithoutMiscastVoices(
            Paired(
                ("analyst-prime", "en-US-AriaNeural"),
                ("cora", "en-US-AriaNeural"),
                ("warden", "en-GB-RyanNeural"),
                ("kex", "unlabelled")),
            Voices());

        Assert.Equal("en-US-AriaNeural", kept["cora"]);
        Assert.Equal("en-GB-RyanNeural", kept["warden"]);
        Assert.Equal("unlabelled", kept["kex"]);
        Assert.DoesNotContain("analyst-prime", kept.Keys);
    }

    [Fact]
    public void AVoiceThisProviderDoesNotOfferIsLeftAlone()
    {
        // It is another provider's, and which pairings belong to the provider in force is a
        // question that already has an owner. Answering it here would clear a Commander's
        // ElevenLabs cast the first time they looked at Edge.
        var kept = VoicePairing.WithoutMiscastVoices(
            Paired(("analyst-prime", "XrExE9yKIg1WjnnlVkGX")), Voices());

        Assert.Equal("XrExE9yKIg1WjnnlVkGX", kept["analyst-prime"]);
    }
}

/// <summary>
/// The named defaults, on a file where one of them did not take (list.md Phase 11, #33).
/// <para>
/// Warden on ElevenLabs is a lookup, not a judgement — so a file with Warden on something else
/// and George on another core is this pass having failed, which it did on every account that
/// appends a descriptor to a voice name. Repaired once, like the gender check, and a choice made
/// afterwards stands.
/// </para>
/// </summary>
public class NamedVoicesAreRestoredTests
{
    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new("pqHfZKP75CvOlQylNhV4", "Bill - Wise, Mature, Balanced", "american", "male"),
        new("JBFqnCBsd6RMkjVDRZzb", "George - Warm, Captivating Storyteller", "british", "male"),
        new("XrExE9yKIg1WjnnlVkGX", "Matilda - Warm, Professional", "american", "female"),
    ];

    private static Dictionary<string, string> Paired(params (string Persona, string Voice)[] pairs) =>
        pairs.ToDictionary(pair => pair.Persona, pair => pair.Voice, StringComparer.Ordinal);

    [Fact]
    public void WardenIsPutBackOnGeorge()
    {
        var put = VoicePairing.WithNamedDefaultsRestored(
            Paired(("warden", "pqHfZKP75CvOlQylNhV4")), Voices(), "elevenlabs");

        Assert.Equal("JBFqnCBsd6RMkjVDRZzb", put["warden"]);
    }

    [Fact]
    public void TheCoreThatHadGeorgeLosesItAndIsPairedAgain()
    {
        // Dropped rather than swapped: two cores cannot share a voice, and the one with no claim
        // on this one goes back through the ordinary pairing rather than being handed Warden's
        // cast-off by a rule that was never asked to choose for it.
        var put = VoicePairing.WithNamedDefaultsRestored(
            Paired(
                ("warden", "pqHfZKP75CvOlQylNhV4"),
                ("mender", "JBFqnCBsd6RMkjVDRZzb"),
                ("cora", "XrExE9yKIg1WjnnlVkGX")),
            Voices(),
            "elevenlabs");

        Assert.Equal("JBFqnCBsd6RMkjVDRZzb", put["warden"]);
        Assert.DoesNotContain("mender", put.Keys);
        Assert.Equal("XrExE9yKIg1WjnnlVkGX", put["cora"]);
    }

    /// <summary>
    /// Found on a real file: the Commander picked George for Warden by hand, so the repair saw
    /// the pairing it wanted and skipped the entry — leaving the core that had been given George
    /// by the model still holding it. Two cores, one voice, which is the thing the pairing exists
    /// to prevent.
    /// </summary>
    [Fact]
    public void TheOtherCoreLosesItEvenWhenWardenAlreadyHasIt()
    {
        var put = VoicePairing.WithNamedDefaultsRestored(
            Paired(("warden", "JBFqnCBsd6RMkjVDRZzb"), ("mender", "JBFqnCBsd6RMkjVDRZzb")),
            Voices(),
            "elevenlabs");

        Assert.Equal("JBFqnCBsd6RMkjVDRZzb", put["warden"]);
        Assert.DoesNotContain("mender", put.Keys);
    }

    [Fact]
    public void APairingAlreadyRightIsLeftExactlyAlone()
    {
        var already = Paired(("warden", "JBFqnCBsd6RMkjVDRZzb"), ("cora", "XrExE9yKIg1WjnnlVkGX"));

        Assert.Same(already, VoicePairing.WithNamedDefaultsRestored(already, Voices(), "elevenlabs"));
    }

    [Fact]
    public void AnotherProvidersFileIsNotTouched()
    {
        // "George" means an ElevenLabs voice. On Edge there is no named default at all, so there
        // is nothing here to restore and nothing to take off anyone.
        var edge = Paired(("warden", "en-GB-RyanNeural"));

        Assert.Same(edge, VoicePairing.WithNamedDefaultsRestored(edge, Voices(), "edge"));
    }
}
