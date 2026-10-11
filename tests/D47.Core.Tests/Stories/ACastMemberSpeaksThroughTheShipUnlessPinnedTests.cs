using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A cast member the story did not pin speaks through the ship's provider, in the voice paired with it there or the crew
/// voice; a pinned member, and one on the Commander's own voice, speaks in the story's voice.
/// </summary>
public sealed class ACastMemberSpeaksThroughTheShipUnlessPinnedTests
{
    private const string DockHand = $"{Id}.dock-hand";

    private static readonly ShipSpeech OpenAi = new(TtsProviderCatalog.OpenAiId);

    private static StorySecret WithMember(Func<StorySpeaker, StorySpeaker> change) =>
        Secret with { Cast = [.. Secret.Cast.Select(change)] };

    [Fact]
    public void AMemberTheStoryDidNotPinSpeaksTheShipProvidersCrewVoice()
    {
        var voice = StoryVoices.Of("dock-hand", Secret, null, null, null, OpenAi);

        Assert.Equal(new PinnedVoice(TtsProviderCatalog.OpenAiId, string.Empty), voice.Pinned);
        Assert.True(voice.Pinned!.IsCrewVoice);
    }

    [Fact]
    public void AMemberKeepsTheLinkAndEffectsTheStoryGaveIt()
    {
        var secret = WithMember(member => member with { Link = 0.5 });

        Assert.Equal(0.5, StoryVoices.Of("dock-hand", secret, null, null, null, OpenAi).Pinned!.Link);
    }

    [Fact]
    public void AMemberSpeaksTheVoicePairedWithItOnTheShipsProvider()
    {
        var pairings = new Dictionary<string, StoryVoiceChoice> { [DockHand] = new(TtsProviderCatalog.OpenAiId, "onyx") };

        var voice = StoryVoices.Of("dock-hand", Secret, null, null, null, OpenAi with { Pairings = pairings });

        Assert.Equal(new PinnedVoice(TtsProviderCatalog.OpenAiId, "onyx"), voice.Pinned);
    }

    [Fact]
    public void APairingForAnotherProviderIsIgnored()
    {
        var pairings = new Dictionary<string, StoryVoiceChoice> { [DockHand] = new(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb") };

        var voice = StoryVoices.Of("dock-hand", Secret, null, null, null, OpenAi with { Pairings = pairings });

        Assert.True(voice.Pinned!.IsCrewVoice);
    }

    [Fact]
    public void APinnedMemberSpeaksTheStoryVoiceWhateverTheShipUses()
    {
        var secret = WithMember(member => member with { Pinned = true });

        Assert.Equal(new PinnedVoice(StorySpeaker.Kokoro, "bm_george"), StoryVoices.Of("dock-hand", secret, null, null, null, OpenAi).Pinned);
    }

    [Fact]
    public void AMemberOnTheCommandersOwnVoiceIsPinnedWithoutSayingSo()
    {
        var secret = WithMember(member => member with { Provider = StorySpeaker.Chatterbox, Voice = StorySpeaker.Own });

        Assert.Equal(new PinnedVoice(StorySpeaker.Chatterbox, StorySpeaker.Own), StoryVoices.Of("dock-hand", secret, null, null, null, OpenAi).Pinned);
    }

    [Fact]
    public void PinnedCoversBothVersionsOfAMember()
    {
        var secret = Versioned with { Cast = [Versioned.Cast[0] with { Pinned = true }] };

        Assert.True(secret.Cast[0].Shown(Id, CommanderGender.Man).Pinned);
        Assert.True(secret.Cast[0].Shown(Id, CommanderGender.Woman).Pinned);
        Assert.False(Versioned.Cast[0].Shown(Id, CommanderGender.Man).Pinned);
    }

    [Fact]
    public void AChoiceThatFailsFallsBackToTheShipProviderNotTheKokoroVoice()
    {
        var choices = new Dictionary<string, StoryVoiceChoice> { [DockHand] = new(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb") };

        var voice = StoryVoices.Of("dock-hand", Secret, null, null, choices, OpenAi).Pinned!;

        Assert.Equal(new PinnedVoice(TtsProviderCatalog.OpenAiId, string.Empty), voice.Fallback!);
    }

    [Fact]
    public void AStoryWithNoPinnedMemberNeedsNoKokoro()
    {
        var here = new CastVoicesHere(Kokoro: false, Chatterbox: false, OwnRecording: false);

        Assert.Empty(StoryVoices.Missing(Secret, null, here, null, OpenAi));
        Assert.NotEmpty(StoryVoices.Missing(WithMember(member => member with { Pinned = true }), null, here, null, OpenAi));
    }

    [Fact]
    public void AnUnpinnedMemberCountsAsTheShipsProvider()
    {
        Assert.True(StoryVoices.Uses(Secret, null, TtsProviderCatalog.OpenAiId, null, OpenAi));
        Assert.False(StoryVoices.Uses(Secret, null, StorySpeaker.Kokoro, null, OpenAi));
    }

    [Fact]
    public void ACommandersChoiceStillNeedsItsOwnProvider()
    {
        var here = new CastVoicesHere(Kokoro: false, Chatterbox: false, OwnRecording: false) { HasKey = _ => false };
        var choices = new Dictionary<string, StoryVoiceChoice> { [DockHand] = new(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb") };

        Assert.NotEmpty(StoryVoices.Missing(Secret, null, here, choices, OpenAi));
    }
}
