using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// A provider that cannot be told what language to speak never speaks for a slot carrying other
/// people's words (list.md Phase 58).
/// <para>
/// <b>The rule exists because of what those slots carry.</b> A message from another Commander can
/// be in any language at all, and a provider that cannot be told one follows the text — so a
/// French line arrives read as French, in the voice the Commander cast for English. It is the
/// same failure that moved the ElevenLabs pin off Multilingual 2, and it reaches d47 by a road
/// nothing can see: measured 2026-08-26, OpenAI accepts a <c>language</c> field with <c>200</c>
/// and ignores it (docs/spikes/openai-tts-language-and-speed.md §2).
/// </para>
/// <para>
/// So the guard is not a warning. The picker does not offer it, and a settings file naming it
/// anyway is not obeyed — because a rule enforced only in the picker is a rule a hand-edited file
/// walks straight past.
/// </para>
/// </summary>
public class AProviderThatCannotHoldALanguageStaysOutOfCommsTests
{
    private const string Edge = TtsProviderCatalog.EdgeId;
    private const string OpenAi = TtsProviderCatalog.OpenAiId;

    [Fact]
    public void TheTwoThatWereHereFirstBothPinALanguage()
    {
        // Edge sends xml:lang in its SSML and ElevenLabs sends a language_code, which is why the
        // rule below costs them nothing and why false is the exception rather than the default.
        Assert.True(TtsProviderCatalog.Edge.LanguageCanBePinned);
        Assert.True(TtsProviderCatalog.ElevenLabs.LanguageCanBePinned);
        Assert.False(TtsProviderCatalog.OpenAi.LanguageCanBePinned);
    }

    /// <summary>
    /// And the third one does too, which is half of why it was added (list.md Phase 60).
    /// <para>
    /// Cartesia sends a <c>language</c> with every line and holds it, so it is the second provider
    /// after ElevenLabs that may legally carry another player's words at all. <b>A capability
    /// rather than a preference</b>: the rule above has teeth, and this is what passing it buys.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("known")]
    [InlineData("direct")]
    [InlineData("range")]
    public void AndTheOneAddedForItsVoiceLibraryIsEligibleWhereOpenAiIsBarred(string slotId)
    {
        var slot = VoiceGroups.ById(slotId)!;

        Assert.True(TtsProviderCatalog.Cartesia.LanguageCanBePinned);
        Assert.Contains(TtsProviderCatalog.Cartesia, TtsProviderCatalog.For(slot));
    }

    /// <summary>
    /// And a settings file naming it for one of those slots is honoured rather than treated as
    /// unusable — the other side of the resolver's rule, and the assertion that would catch it
    /// being refused by accident along with OpenAI.
    /// </summary>
    [Fact]
    public void AHandEditedFileNamingItForLocalChatIsObeyed()
    {
        var speech = new SpeechSettings
        {
            Provider = Edge,
            GroupProviders = new Dictionary<string, string>
            {
                ["range"] = TtsProviderCatalog.CartesiaId,
            },
        };

        Assert.Equal(
            TtsProviderCatalog.CartesiaId,
            VoiceGroups.ProviderFor(speech, VoiceGroup.AnyoneInRange));
    }

    [Fact]
    public void TheCockpitAndTheCarrierMayNameIt()
    {
        // d47's own English prose, both of them — the ship's core and two fictions of its own.
        Assert.Contains(TtsProviderCatalog.OpenAi, TtsProviderCatalog.For(VoiceGroups.Aboard));
        Assert.Contains(TtsProviderCatalog.OpenAi, TtsProviderCatalog.For(VoiceGroups.Carrier));

        // And so may the NPCs: Frontier writes that text, it is English, and it is bounded.
        Assert.Contains(TtsProviderCatalog.OpenAi, TtsProviderCatalog.For(VoiceGroups.Npcs));
    }

    [Theory]
    [InlineData("known")]
    [InlineData("direct")]
    [InlineData("range")]
    public void TheSlotsCarryingOtherPeoplesWordsDoNot(string slotId)
    {
        var slot = VoiceGroups.ById(slotId)!;

        Assert.True(slot.OtherPeoplesWords);
        Assert.DoesNotContain(TtsProviderCatalog.OpenAi, TtsProviderCatalog.For(slot));

        // The ones that can hold a language are all still there, so this narrows rather than
        // empties: a slot with nothing to choose from would be a worse answer than the fault.
        Assert.Contains(TtsProviderCatalog.Edge, TtsProviderCatalog.For(slot));
        Assert.Contains(TtsProviderCatalog.ElevenLabs, TtsProviderCatalog.For(slot));
    }

    /// <summary>
    /// The half the picker cannot enforce. `settings.json` is a file a Commander reads and edits,
    /// and a rule that lives only in a dropdown is one a text editor walks past.
    /// </summary>
    [Fact]
    public void AHandEditedFileNamingItForLocalChatIsNotObeyed()
    {
        var speech = new SpeechSettings
        {
            Provider = Edge,
            GroupProviders = new Dictionary<string, string> { ["range"] = OpenAi },
        };

        Assert.Equal(Edge, VoiceGroups.ProviderFor(speech, VoiceGroup.AnyoneInRange));
    }

    /// <summary>
    /// And the fallback cannot loop. Falling back to <see cref="SpeechSettings.Provider"/> would
    /// resolve to the very provider being refused whenever the ship is on it too.
    /// </summary>
    [Fact]
    public void EvenWhenTheShipIsOnThatProviderItself()
    {
        var speech = new SpeechSettings
        {
            Provider = OpenAi,
            GroupProviders = new Dictionary<string, string> { ["range"] = OpenAi },
        };

        Assert.Equal(OpenAi, VoiceGroups.ProviderFor(speech, VoiceGroup.Aboard));
        Assert.Equal(Edge, VoiceGroups.ProviderFor(speech, VoiceGroup.AnyoneInRange));
    }

    /// <summary>
    /// A file from before Phase 57, where every slot follows the ship's provider. The rule has to
    /// reach that reading too — it is the one a Commander gets by upgrading rather than choosing.
    /// </summary>
    [Fact]
    public void AndWhenEverySlotIsFollowingTheShipsProvider()
    {
        var speech = new SpeechSettings { Provider = OpenAi };

        Assert.Equal(OpenAi, VoiceGroups.ProviderFor(speech, VoiceGroup.Aboard));
        Assert.Equal(OpenAi, VoiceGroups.ProviderFor(speech, VoiceGroup.Npcs));

        Assert.All(
            VoiceGroups.All.Where(slot => slot.OtherPeoplesWords),
            slot => Assert.Equal(Edge, VoiceGroups.ProviderFor(speech, slot.Group)));
    }

    /// <summary>
    /// The clients built for such a file, which is what the arithmetic downstream depends on:
    /// two, not one, and the untrusted slots are on the free one.
    /// </summary>
    [Fact]
    public void SoAShipOnOpenAiStillNeedsEdgeForTheStrangers()
    {
        var speech = new SpeechSettings { Provider = OpenAi };

        Assert.Equal([Edge, OpenAi], VoiceGroups.ProvidersInUse(speech));
    }
}
