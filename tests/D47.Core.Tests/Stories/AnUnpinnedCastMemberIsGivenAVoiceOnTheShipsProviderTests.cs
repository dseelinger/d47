using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;
using VoiceGender = D47.Core.Persona.VoiceGender;

namespace D47.Core.Tests.Stories;

/// <summary>
/// Each cast member a story did not pin is given a voice from the ship's provider's list, of the gender the story wrote
/// it for, kept per provider and never shared within the story or with the ship or the Narrator.
/// </summary>
public sealed class AnUnpinnedCastMemberIsGivenAVoiceOnTheShipsProviderTests
{
    private const string Eleven = TtsProviderCatalog.ElevenLabsId;

    private const string DockHand = $"{Id}.dock-hand";

    private static readonly IReadOnlyList<VoiceInfo> ElevenVoices =
    [
        new("v-ship", "Ship", "en-GB", "male"),
        new("v-narrator", "Narrator", "en-GB", "female"),
        new("v-adam", "Adam", "en-US", "male"),
        new("v-bella", "Bella", "en-US", "female"),
        new("v-carl", "Carl", "en-GB", "male"),
        new("v-dora", "Dora", "en-GB", "female"),
    ];

    private static readonly IReadOnlyList<VoiceInfo> Chatterbox = [new("ellis-sample", "Ellis", "en", "male")];

    private static readonly StorySecret ThreeMembers = Secret with
    {
        Cast =
        [
            .. Secret.Cast,
            new StorySpeaker { Id = "medic", Name = "Ama", Who = "A ship's medic, calm.", Provider = StorySpeaker.Kokoro, Voice = "af_heart" },
            new StorySpeaker { Id = "pilot", Name = "Jo", Who = "A wing pilot, quick.", Provider = StorySpeaker.Kokoro, Voice = "bf_emma" },
        ],
    };

    private static Task<IReadOnlyDictionary<string, StoryVoiceChoice>> Choose(
        StorySecret secret,
        D47Settings? settings = null,
        IEnumerable<string>? taken = null,
        IReadOnlyList<VoiceInfo>? voices = null,
        ILlmProvider? llm = null,
        SpendTracker? spend = null,
        string? gender = null) =>
        StoryCastPairing.ChooseAsync(
            secret,
            Card.Title,
            gender,
            Eleven,
            settings ?? new D47Settings(),
            taken ?? ["v-ship", "v-narrator"],
            voices ?? ElevenVoices,
            Chatterbox,
            llm,
            llm is null ? null : "claude-opus-5",
            spend,
            prices: null,
            logger: null,
            new Random(7),
            TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("af_heart", VoiceGender.Female)]
    [InlineData("bf_emma", VoiceGender.Female)]
    [InlineData("am_adam", VoiceGender.Male)]
    [InlineData("bm_george", VoiceGender.Male)]
    public void AKokoroVoicesGenderIsReadFromItsId(string voice, VoiceGender expected) =>
        Assert.Equal(expected, StoryCastPairing.GenderOf(StorySpeaker.Kokoro, voice, []));

    [Fact]
    public void AChatterboxVoicesGenderIsReadFromItsCatalogue() =>
        Assert.Equal(VoiceGender.Male, StoryCastPairing.GenderOf(StorySpeaker.Chatterbox, "ellis-sample", Chatterbox));

    [Fact]
    public async Task EachUnpinnedMemberIsPairedOnTheShipsProviderUnderItsPictureKey()
    {
        var paired = await Choose(Secret);

        var pairing = Assert.Single(paired);
        Assert.Equal(DockHand, pairing.Key);
        Assert.Equal(Eleven, pairing.Value.Provider);
        Assert.Equal("Ren", pairing.Value.Character);
        Assert.Equal(Card.Title, pairing.Value.Story);
    }

    [Fact]
    public async Task APinnedMemberIsNotPaired()
    {
        var paired = await Choose(Secret with { Cast = [Secret.Cast[0] with { Pinned = true }] });

        Assert.Empty(paired);
    }

    [Fact]
    public async Task AMemberInTwoVersionsIsPairedAsTheVersionTheCommanderMeets()
    {
        var paired = await Choose(Versioned, gender: CommanderGender.Woman);

        Assert.Contains($"{Id}.cray.{StorySpeakerVersions.ForWomanKey}", paired.Keys);
        Assert.DoesNotContain($"{Id}.cray.{StorySpeakerVersions.ForManKey}", paired.Keys);
    }

    [Fact]
    public async Task WithoutAModelEachMemberGetsAVoiceOfTheGenderTheStoryWroteItFor()
    {
        var paired = await Choose(ThreeMembers);

        Assert.Contains(paired[DockHand].Voice, new[] { "v-adam", "v-carl" });
        Assert.Contains(paired[$"{Id}.medic"].Voice, new[] { "v-bella", "v-dora" });
        Assert.Contains(paired[$"{Id}.pilot"].Voice, new[] { "v-bella", "v-dora" });
    }

    [Fact]
    public async Task NoTwoMembersShareAVoiceAndNoneTakesTheShipsOrTheNarrators()
    {
        var paired = await Choose(ThreeMembers);
        var voices = paired.Values.Select(choice => choice.Voice).ToList();

        Assert.Equal(3, voices.Count);
        Assert.Equal(voices.Count, voices.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain("v-ship", voices);
        Assert.DoesNotContain("v-narrator", voices);
    }

    [Fact]
    public async Task AVoiceAlreadyHeldByAMemberOfTheStoryIsNotHandedToAnother()
    {
        var secret = ThreeMembers with { Cast = [.. ThreeMembers.Cast.Select(member => member.Id == "medic" ? member with { Pinned = true } : member)] };
        var settings = new D47Settings
        {
            StoryVoices = new Dictionary<string, StoryVoiceChoice> { [$"{Id}.medic"] = new(Eleven, "v-bella") },
        };

        var paired = await Choose(secret, settings);

        Assert.Equal("v-dora", paired[$"{Id}.pilot"].Voice);
    }

    [Fact]
    public async Task AMemberAlreadyPairedOnTheProviderIsNotPairedAgain()
    {
        var settings = StoryCastPairing.WithPairings(
            new D47Settings(), Eleven, new Dictionary<string, StoryVoiceChoice> { [DockHand] = new(Eleven, "v-adam") });

        Assert.Empty(await Choose(Secret, settings));
    }

    [Fact]
    public async Task APairingOnAnotherProviderLeavesTheMemberToBePairedOnThisOne()
    {
        var settings = StoryCastPairing.WithPairings(
            new D47Settings(), TtsProviderCatalog.OpenAiId, new Dictionary<string, StoryVoiceChoice> { [DockHand] = new(TtsProviderCatalog.OpenAiId, "onyx") });

        Assert.Equal(Eleven, (await Choose(Secret, settings))[DockHand].Provider);
    }

    [Fact]
    public void SwitchingBackToAProviderFindsItsEarlierPairing()
    {
        var settings = StoryCastPairing.WithPairings(
            new D47Settings(), TtsProviderCatalog.OpenAiId, new Dictionary<string, StoryVoiceChoice> { [DockHand] = new(TtsProviderCatalog.OpenAiId, "onyx") });
        settings = StoryCastPairing.WithPairings(
            settings, Eleven, new Dictionary<string, StoryVoiceChoice> { [DockHand] = new(Eleven, "v-adam") });

        var ship = new ShipSpeech(TtsProviderCatalog.OpenAiId, StoryCastPairing.On(settings, TtsProviderCatalog.OpenAiId));

        Assert.Equal("onyx", ship.VoiceFor(DockHand));
    }

    [Fact]
    public void APairingIsWrittenToTheSettingsFileAndReadBack()
    {
        var settings = StoryCastPairing.WithPairings(
            new D47Settings(), Eleven, new Dictionary<string, StoryVoiceChoice> { [DockHand] = new(Eleven, "v-adam") { Character = "Ren" } });

        var read = System.Text.Json.JsonSerializer.Deserialize<D47Settings>(
            System.Text.Json.JsonSerializer.Serialize(settings, SettingsStore.Json), SettingsStore.Json)!;

        Assert.Equal("v-adam", StoryCastPairing.On(read, Eleven)![DockHand].Voice);
        Assert.Null(read.Extra);
    }

    [Fact]
    public void ACommandersChoiceWinsOverAPairing()
    {
        var pairings = new Dictionary<string, StoryVoiceChoice> { [DockHand] = new(Eleven, "v-adam") };
        var choices = new Dictionary<string, StoryVoiceChoice> { [DockHand] = new(Eleven, "v-carl") };

        var voice = StoryVoices.Of("dock-hand", Secret, null, null, choices, new ShipSpeech(Eleven, pairings));

        Assert.Equal("v-carl", voice.Pinned!.VoiceId);
    }

    [Fact]
    public async Task TheModelIsGivenEachMembersNameWhoAndGender()
    {
        var llm = FakeLlmProvider.Answering("cast1 = v-carl");

        var paired = await Choose(Secret, llm: llm);

        var asked = string.Join(
            ' ',
            llm.LastRequest!.Prompt.History
                .SelectMany(message => message.Content.OfType<ConversationContent.Text>())
                .Select(part => part.Value));

        Assert.Contains("male voice. Ren: A tired dock hand, short words.", asked, StringComparison.Ordinal);
        Assert.Equal("v-carl", paired[DockHand].Voice);
    }

    [Fact]
    public async Task TheModelsSpendIsRecorded()
    {
        var spend = new SpendTracker();
        var llm = FakeLlmProvider.Answering("cast1 = v-carl", new LlmUsage(100, 10, 0, 0));

        await Choose(Secret, llm: llm, spend: spend);

        Assert.Equal(1, spend.TurnCount);
    }
}
