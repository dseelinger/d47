using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A story character the Commander put on a hosted provider is named in the text-to-speech disclosure with its story and
/// provider, along with that provider's own disclosure; a character on a local provider adds nothing (#737).
/// </summary>
public sealed class AStoryVoiceOnAHostedProviderIsDisclosedTests
{
    private static D47Settings With(string provider, string voice) => D47Settings.Defaults with
    {
        StoryVoices = new Dictionary<string, StoryVoiceChoice>(StringComparer.Ordinal)
        {
            ["ride-along.juno"] = new StoryVoiceChoice(provider, voice) { Story = "Ride Along", Character = "Juno" },
        },
    };

    private static EgressEntry Speech(D47Settings settings) =>
        EgressDisclosure.Entry(EgressDisclosure.TextToSpeech, settings, llmKeyPresent: false);

    [Fact]
    public void AnElevenLabsCharacterIsNamedWithItsStoryAndProvider()
    {
        var entry = Speech(With(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb"));

        Assert.Contains("Juno in Ride Along → ElevenLabs", entry.What, StringComparison.Ordinal);
        Assert.Contains(TtsProviderCatalog.ElevenLabs.Egress, entry.What, StringComparison.Ordinal);
        Assert.Contains("api.elevenlabs.io", entry.Destination, StringComparison.Ordinal);
    }

    [Fact]
    public void ACharacterOnKokoroSaysNothingAboutStories()
    {
        var entry = Speech(With(TtsProviderCatalog.KokoroId, "af_river"));

        Assert.DoesNotContain("Story characters", entry.What, StringComparison.Ordinal);
        Assert.DoesNotContain("Juno", entry.What, StringComparison.Ordinal);
        Assert.Equal(Speech(D47Settings.Defaults), entry);
    }

    [Fact]
    public void TheToolAnswersFromTheSameDisclosure()
    {
        var described = EgressDisclosure.Describe(With(TtsProviderCatalog.OpenAiId, "onyx"), llmKeyPresent: false);

        Assert.Contains("Juno in Ride Along → OpenAI", described, StringComparison.Ordinal);
    }
}
