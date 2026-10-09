using D47.Core.Audio;
using D47.Core.Storage;
using D47.Core.Configuration;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A voice the Commander chose for a cast member, keyed by story and character, is the one its lines are spoken in, with
/// the story's pinned voice behind it; with no key the pinned voice speaks. Readiness reads the chosen voice (#737).
/// </summary>
public sealed class TheCommandersCastVoiceIsTheOneSpokenTests
{
    private const string DockHand = $"{Id}.dock-hand";

    private static Dictionary<string, StoryVoiceChoice> Chose(string key, string provider, string voice) =>
        new(StringComparer.Ordinal) { [key] = new StoryVoiceChoice(provider, voice) { Story = Card.Title, Character = "Ren" } };

    [Fact]
    public void AChosenElevenLabsVoiceSpeaksWithThePinnedKokoroVoiceBehindIt()
    {
        var voice = StoryVoices.Of("dock-hand", Secret, null, null, Chose(DockHand, TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb"));

        Assert.Equal(new PinnedVoice(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb") { Key = DockHand, Fallback = new PinnedVoice(StorySpeaker.Kokoro, "bm_george") }, voice.Pinned);
    }

    [Fact]
    public void WithNoKeyThePinnedVoiceSpeaks()
    {
        var voice = StoryVoices.Of("dock-hand", Secret, null, null, new Dictionary<string, StoryVoiceChoice>());

        Assert.Equal(new PinnedVoice(StorySpeaker.Kokoro, "bm_george"), voice.Pinned);
    }

    [Fact]
    public void AChoiceForOneVersionAppliesToThatVersionOnly()
    {
        var choices = Chose($"{Id}.cray.for-man", TtsProviderCatalog.OpenAiId, "onyx");

        Assert.Equal("onyx", StoryVoices.Of("cray", Versioned, CommanderGender.Man, null, choices).Pinned!.VoiceId);
        Assert.Equal("ellis-sample", StoryVoices.Of("cray", Versioned, CommanderGender.Woman, null, choices).Pinned!.VoiceId);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheChoiceSurvivesARestart()
    {
        var folder = Path.Combine(Path.GetTempPath(), "d47-story-voices-" + Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(folder);
        paths.EnsureCreated();

        try
        {
            new SettingsStore(paths, new DiskFileSystem(), NullLogger<SettingsStore>.Instance).Save(D47Settings.Defaults with
            {
                StoryVoices = Chose(DockHand, TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb"),
            });

            var loaded = new SettingsStore(paths, new DiskFileSystem(), NullLogger<SettingsStore>.Instance).Load();

            Assert.Equal(new StoryVoiceChoice(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb") { Story = Card.Title, Character = "Ren" }, loaded.StoryVoices[DockHand]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AHostedVoiceWithNoKeyIsNamedInThePrerequisites()
    {
        var choices = Chose(DockHand, TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb");
        var keyless = new CastVoicesHere(true, true, true) { HasKey = _ => false };
        var keyed = new CastVoicesHere(true, true, true) { HasKey = _ => true };

        Assert.Equal(["Add your ElevenLabs API key under Settings, Its voice."], StoryVoices.Missing(Secret, null, keyless, choices));
        Assert.Empty(StoryVoices.Missing(Secret, null, keyed, choices));
    }

    [Fact]
    public void AMemberMovedOffKokoroNoLongerNeedsKokoro()
    {
        var choices = Chose(DockHand, TtsProviderCatalog.EdgeId, "en-GB-RyanNeural");
        var noKokoro = new CastVoicesHere(Kokoro: false, Chatterbox: true, OwnRecording: true);

        Assert.Empty(StoryVoices.Missing(Secret, null, noKokoro, choices));
        Assert.False(StoryVoices.Uses(Secret, null, TtsProviderCatalog.KokoroId, choices));
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheDirectorSpeaksTheChoiceAndUseTheDefaultBringsThePinnedVoiceBack()
    {
        using var fixtures = new StoryFixtures(new Conversation.RoundScriptedLlmProvider(
            Conversation.RoundScriptedLlmProvider.Saying(Spine),
            Conversation.RoundScriptedLlmProvider.Saying(BeatsToTheBeacon)));

        IReadOnlyDictionary<string, StoryVoiceChoice> choices = Chose(DockHand, TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb");
        fixtures.Director.Choices = () => choices;

        Assert.Equal(TtsProviderCatalog.ElevenLabsId, fixtures.Director.CastMember(DockHand)!.Speaks.ProviderId);

        choices = new Dictionary<string, StoryVoiceChoice>();

        Assert.Equal(new PinnedVoice(StorySpeaker.Kokoro, "bm_george"), fixtures.Director.CastMember(DockHand)!.Speaks);
    }
}
