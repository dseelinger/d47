using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A story may pin a cast member only to a provider that is not billed. While it runs the provider's disclosure names
/// the character and the story (#1072, #1084).
/// </summary>
public sealed class AStoryPinsOnlyToAFreeVoiceProviderTests
{
    private static StorySecret Pinned(string provider, string voice) =>
        Secret with { Cast = [.. Secret.Cast.Select(member => member with { Provider = provider, Voice = voice, Pinned = true })] };

    private static IReadOnlyList<string> FaultsOf(StorySecret secret) =>
        new StoryCatalog([Card], () => [secret]).Faults();

    [Theory]
    [InlineData(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb")]
    [InlineData(TtsProviderCatalog.OpenAiId, "onyx")]
    [InlineData(TtsProviderCatalog.CartesiaId, "a0e99841-438c-4a64-b679-ae501e7d6091")]
    public void ABilledProviderIsRefusedAndNamed(string provider, string voice)
    {
        Assert.Contains(FaultsOf(Pinned(provider, voice)), fault => fault.Contains(provider, StringComparison.Ordinal) && fault.Contains("only a free one", StringComparison.Ordinal));
    }

    [Fact]
    public void AnEdgePinWithAVoiceIsAccepted()
    {
        Assert.DoesNotContain(FaultsOf(Pinned(TtsProviderCatalog.EdgeId, "en-GB-RyanNeural")), fault => fault.Contains("provider", StringComparison.Ordinal) || fault.Contains("voice that", StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, StoryVoiceChoice> CommandersChoice(string provider, string voice) =>
        new Dictionary<string, StoryVoiceChoice>(StringComparer.Ordinal) { [$"{Id}.dock-hand"] = new StoryVoiceChoice(provider, voice) };

    [Fact]
    public void ACommandersChoiceOfAProviderWithoutAStoredKeyCannotStart()
    {
        var here = new CastVoicesHere(Kokoro: true, Chatterbox: true, OwnRecording: true) { HasKey = _ => false };

        var missing = StoryVoices.Missing(Secret, null, here, CommandersChoice(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb"));

        Assert.Equal(["Add your ElevenLabs API key under Settings, Its voice."], missing);
    }

    [Fact]
    public void ACommandersChoiceOfAProviderWithAStoredKeyIsReady()
    {
        var here = new CastVoicesHere(Kokoro: true, Chatterbox: true, OwnRecording: true) { HasKey = _ => true };

        Assert.Empty(StoryVoices.Missing(Secret, null, here, CommandersChoice(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb")));
    }

    private static EgressEntry Speech(params RunningStoryVoice[] running) =>
        EgressDisclosure.Entry(EgressDisclosure.TextToSpeech, D47Settings.Defaults, llmKeyPresent: false, runningStory: running);

    [Fact]
    public void ARunningStoryPinnedToEdgeIsNamedWithItsCharacterAndStory()
    {
        var entry = Speech(new RunningStoryVoice("ride-along.juno", "Ride Along", "Juno", TtsProviderCatalog.EdgeId));

        Assert.Contains("Juno in Ride Along → Edge Neural", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void ARunningStoryPinnedToALocalProviderAddsNothing()
    {
        var entry = Speech(new RunningStoryVoice("ride-along.juno", "Ride Along", "Juno", TtsProviderCatalog.KokoroId));

        Assert.Equal(Speech(), entry);
    }

    [Fact]
    public void ACommandersChoiceReplacesThePinInTheDisclosure()
    {
        var settings = D47Settings.Defaults with
        {
            StoryVoices = new Dictionary<string, StoryVoiceChoice>(StringComparer.Ordinal)
            {
                ["ride-along.juno"] = new StoryVoiceChoice(TtsProviderCatalog.KokoroId, "af_river") { Story = "Ride Along", Character = "Juno" },
            },
        };

        var entry = EgressDisclosure.Entry(
            EgressDisclosure.TextToSpeech,
            settings,
            llmKeyPresent: false,
            runningStory: [new RunningStoryVoice("ride-along.juno", "Ride Along", "Juno", TtsProviderCatalog.ElevenLabsId)]);

        Assert.DoesNotContain("Juno", entry.What, StringComparison.Ordinal);
    }
}
