using D47.Core.Audio;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>A voice per core, chosen by the model where there is one.</summary>
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
    public async Task AVoiceTheCommanderChoseIsNeverOverwritten()
    {
        var existing = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = "en-GB-ThomasNeural" };

        var paired = await VoicePairing.ChooseAsync(
            Voices(),
            existing,
            FakeLlmProvider.Answering("warden = en-US-GuyNeural\ncora = en-US-AriaNeural"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("en-GB-ThomasNeural", paired["warden"]);
        Assert.Equal("en-US-AriaNeural", paired["cora"]);
    }

    [Fact]
    public async Task NoVoicesMeansNoPairingsRatherThanAnError()
    {
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
        var offered = Voices().Select(voice => voice.Id).ToHashSet();

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
        Assert.All(paired.Values, voice => Assert.Contains(voice, offered));
    }

    [Fact]
    public async Task TheModelIsNotAskedAboutAVoiceInAnotherLanguage()
    {
        var llm = FakeLlmProvider.Answering("warden = fr-FR-HenriNeural");

        var paired = await VoicePairing.ChooseForAsync(
            [.. Voices(), new VoiceInfo("fr-FR-HenriNeural", "Henri", "fr-FR", "Male")],
            [VoicePairing.SlotFor(PersonaCatalog.Warden)],
            taken: [],
            llm,
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        var asked = string.Join(
            ' ',
            llm.LastRequest!.Prompt.History
                .SelectMany(message => message.Content.OfType<D47.Core.Conversation.ConversationContent.Text>())
                .Select(part => part.Value));

        Assert.Contains("en-GB-RyanNeural", asked);
        Assert.DoesNotContain("fr-FR-HenriNeural", asked);
        Assert.NotEqual("fr-FR-HenriNeural", paired["warden"]);
    }

    /// <summary>A remark's ceiling is spent on reasoning before a thinking model writes the answer.</summary>
    [Fact]
    public async Task AThinkingModelIsGivenRoomToAnswer()
    {
        var llm = FakeLlmProvider.Answering("warden = en-GB-RyanNeural");

        await VoicePairing.ChooseForAsync(
            Voices(),
            [VoicePairing.SlotFor(PersonaCatalog.Warden)],
            taken: [],
            llm,
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(VoicePairing.CastingTokens, llm.LastRequest!.MaxOutputTokens);
    }

    [Fact]
    public async Task TheModelIsOfferedEveryVoiceNotOnlyTheFirstPage()
    {
        // The only voice that fits is past the first request's worth.
        IReadOnlyList<VoiceInfo> many =
        [
            .. Enumerable.Range(0, VoicePairing.VoicesPerRequest * 2)
                .Select(n => new VoiceInfo($"voice-{n}", $"Voice {n}", "en-GB")),
        ];

        var last = many[^1].Id;
        var llm = FakeLlmProvider.Answering($"warden = {last}");

        var paired = await VoicePairing.ChooseForAsync(
            many,
            [VoicePairing.SlotFor(PersonaCatalog.Warden)],
            taken: [],
            llm,
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(last, paired["warden"]);
    }
}

/// <summary>With no model, every slot is still paired from what the list says.</summary>
public class NoModelPairingFillsEverySlotTests
{
    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        .. Enumerable.Range(0, 10).Select(n => new VoiceInfo($"woman-{n}", $"Woman {n}", "en-GB", "Female")),
        .. Enumerable.Range(0, 10).Select(n => new VoiceInfo($"man-{n}", $"Man {n}", "en-US", "Male")),
    ];

    [Fact]
    public async Task EveryCoreTheCarrierCaptainAndTheTowerGetAVoiceOfTheirOwn()
    {
        IReadOnlyList<VoicePairing.Slot> slots = [.. VoicePairing.Cores, .. VoicePairing.CarrierRoles];

        var paired = await VoicePairing.ChooseForAsync(
            Voices(),
            slots,
            taken: [],
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            new Random(7),
            TestContext.Current.CancellationToken);

        Assert.Equal(slots.Select(slot => slot.Id).Order(), paired.Keys.Order());
        Assert.Equal(paired.Count, paired.Values.Distinct().Count());
    }

    [Fact]
    public async Task AListWithNoMetadataIsDealtAtRandom()
    {
        // OpenAI's list, which says nothing about gender.
        IReadOnlyList<VoiceInfo> bare =
        [
            .. new[] { "alloy", "ash", "ballad", "coral", "echo", "fable", "onyx", "nova", "sage", "shimmer",
                "verse", "marin", "cedar" }.Select(id => new VoiceInfo(id, id, "multilingual")),
        ];

        var paired = await VoicePairing.ChooseForAsync(
            bare,
            VoicePairing.Cores,
            taken: [],
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            new Random(3),
            TestContext.Current.CancellationToken);

        Assert.Equal(VoicePairing.Cores.Count, paired.Count);
        Assert.Equal(paired.Count, paired.Values.Distinct().Count());
    }

    [Fact]
    public async Task FewerVoicesThanSlotsStillPairsEverySlot()
    {
        IReadOnlyList<VoiceInfo> two =
        [
            new("en-GB-RyanNeural", "Ryan", "en-GB", "Male"),
            new("en-GB-SoniaNeural", "Sonia", "en-GB", "Female"),
        ];

        var paired = await VoicePairing.ChooseForAsync(
            two,
            VoicePairing.Cores,
            taken: [],
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            new Random(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(VoicePairing.Cores.Count, paired.Count);

        // The two bound by gender are served first, so each gets the one voice that fits it.
        Assert.Equal("en-GB-SoniaNeural", paired["cora"]);
        Assert.Equal("en-GB-RyanNeural", paired["analyst-prime"]);
    }

    [Fact]
    public async Task AVoiceAlreadyTakenIsNotDealtAgainWhileAFreeOneRemains()
    {
        var paired = await VoicePairing.ChooseForAsync(
            [new VoiceInfo("held", "Held", "en-GB"), new VoiceInfo("free", "Free", "en-GB")],
            [VoicePairing.Tower],
            taken: ["held"],
            provider: null,
            model: null,
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("free", paired[VoicePairing.Tower.Id]);
    }
}

/// <summary>Gender binds Cora and Analyst Prime, and no other core.</summary>
public class OnlyCoraAndAnalystPrimeAreBoundByGenderTests
{
    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new("aria", "Aria", "en-US", "Female"),
        new("sonia", "Sonia", "en-GB", "Female"),
        new("ryan", "Ryan", "en-GB", "Male"),
        new("guy", "Guy", "en-US", "Male"),
    ];

    [Fact]
    public async Task TheModelCannotGiveCoraAMansVoice()
    {
        var paired = await VoicePairing.ChooseForAsync(
            Voices(),
            [VoicePairing.SlotFor(PersonaCatalog.Cora)],
            taken: [],
            FakeLlmProvider.Answering("cora = ryan"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(paired["cora"], new[] { "aria", "sonia" });
    }

    [Fact]
    public async Task TheModelCannotGiveAnalystPrimeAWomansVoice()
    {
        var paired = await VoicePairing.ChooseForAsync(
            Voices(),
            [VoicePairing.SlotFor(PersonaCatalog.AnalystPrime)],
            taken: [],
            FakeLlmProvider.Answering("analyst-prime = aria"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(paired["analyst-prime"], new[] { "ryan", "guy" });
    }

    [Fact]
    public async Task AnyOtherCoreTakesTheVoiceTheModelNamedWhateverItsGender()
    {
        // Warden's hint is written as a man, and that does not constrain the choice.
        var paired = await VoicePairing.ChooseForAsync(
            Voices(),
            [VoicePairing.SlotFor(PersonaCatalog.Warden)],
            taken: [],
            FakeLlmProvider.Answering("warden = aria"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("aria", paired["warden"]);
    }

    [Fact]
    public void OnlyTheTwoSlotsCarryAGender()
    {
        var bound = VoicePairing.Cores
            .Concat(VoicePairing.CarrierRoles)
            .Where(slot => slot.Hint.Gender != D47.Core.Persona.VoiceGender.Unspecified)
            .Select(slot => slot.Id)
            .Order();

        Assert.Equal(["analyst-prime", "cora"], bound);
    }

    [Fact]
    public void CartesiasFeminineAndMasculineLabelsAreRead()
    {
        var cora = VoicePairing.SlotFor(PersonaCatalog.Cora).Hint;

        Assert.False(cora.Admits("masculine"));
        Assert.True(cora.Admits("feminine"));
    }
}

/// <summary>The lazy half: a voice for the one core the Commander has just selected.</summary>
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
    public async Task WithNoModelTheCoreIsStillGivenAVoiceThatFits()
    {
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

        Assert.Equal("XrExE9yKIg1WjnnlVkGX", voice);
    }
}

/// <summary>The pairings written before Cora and Analyst Prime were bound by gender.</summary>
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
    public void AnalystPrimeSpeakingInAWomansVoiceLosesIt()
    {
        var kept = VoicePairing.WithoutMiscastVoices(
            Paired(("analyst-prime", "en-US-AriaNeural")), Voices());

        Assert.Empty(kept);
    }

    [Fact]
    public void AnyOtherCoreKeepsItsVoiceWhateverItsGender()
    {
        var kept = VoicePairing.WithoutMiscastVoices(
            Paired(
                ("analyst-prime", "en-US-AriaNeural"),
                ("cora", "en-US-AriaNeural"),
                ("warden", "en-US-AriaNeural"),
                ("kex", "unlabelled")),
            Voices());

        Assert.Equal("en-US-AriaNeural", kept["cora"]);
        Assert.Equal("en-US-AriaNeural", kept["warden"]);
        Assert.Equal("unlabelled", kept["kex"]);
        Assert.DoesNotContain("analyst-prime", kept.Keys);
    }

    [Fact]
    public void AVoiceThisProviderDoesNotOfferIsLeftAlone()
    {
        var kept = VoicePairing.WithoutMiscastVoices(
            Paired(("analyst-prime", "XrExE9yKIg1WjnnlVkGX")), Voices());

        Assert.Equal("XrExE9yKIg1WjnnlVkGX", kept["analyst-prime"]);
    }
}
