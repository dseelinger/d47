using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>The account section asks a provider only when a voice or the transcription uses it (#2541).</summary>
public sealed class TheAccountSectionAsksOnlyTheProvidersInUseTests
{
    private static readonly D47Settings Edge = D47Settings.Defaults with
    {
        Speech = D47Settings.Defaults.Speech with { Provider = TtsProviderCatalog.EdgeId, GroupProviders = null },
        Listening = D47Settings.Defaults.Listening with { Provider = SttProviderCatalog.LocalId },
        StoryVoices = new Dictionary<string, StoryVoiceChoice>(StringComparer.Ordinal),
    };

    private static D47Settings Hearing(D47Settings settings, string provider) =>
        settings with { Listening = settings.Listening with { Provider = provider } };

    private static IReadOnlyList<AccountProvider> InUse(D47Settings settings, params RunningStoryVoice[] running) =>
        AccountTargets.InUse(settings, running);

    [Fact]
    public void NoHostedVoiceOrHearingAsksNobody()
    {
        Assert.Empty(InUse(Edge));
    }

    [Fact]
    public void TheShipsVoiceOnElevenLabsAsksElevenLabs()
    {
        var settings = Edge with { Speech = Edge.Speech with { Provider = TtsProviderCatalog.ElevenLabsId } };

        Assert.Equal([AccountProvider.ElevenLabs], InUse(settings));
    }

    [Fact]
    public void AnotherSlotOnElevenLabsAsksElevenLabs()
    {
        var settings = Edge with
        {
            Speech = Edge.Speech with
            {
                GroupProviders = new Dictionary<string, string>(StringComparer.Ordinal) { ["carrier"] = TtsProviderCatalog.ElevenLabsId },
            },
        };

        Assert.Equal([AccountProvider.ElevenLabs], InUse(settings));
    }

    [Fact]
    public void AStoryVoiceChosenOnElevenLabsAsksElevenLabs()
    {
        var settings = Edge with
        {
            StoryVoices = new Dictionary<string, StoryVoiceChoice>(StringComparer.Ordinal)
            {
                ["ride-along.juno"] = new StoryVoiceChoice(TtsProviderCatalog.ElevenLabsId, "JBFqnCBsd6RMkjVDRZzb"),
            },
        };

        Assert.Equal([AccountProvider.ElevenLabs], InUse(settings));
    }

    [Fact]
    public void ARunningStorysPinOnElevenLabsAsksElevenLabs()
    {
        Assert.Equal(
            [AccountProvider.ElevenLabs],
            InUse(Edge, new RunningStoryVoice("ride-along.juno", "Ride Along", "Juno", TtsProviderCatalog.ElevenLabsId)));
    }

    [Fact]
    public void AChoiceThatReplacesAnElevenLabsPinAsksNobody()
    {
        var settings = Edge with
        {
            StoryVoices = new Dictionary<string, StoryVoiceChoice>(StringComparer.Ordinal)
            {
                ["ride-along.juno"] = new StoryVoiceChoice(TtsProviderCatalog.KokoroId, "af_river"),
            },
        };

        Assert.Empty(InUse(settings, new RunningStoryVoice("ride-along.juno", "Ride Along", "Juno", TtsProviderCatalog.ElevenLabsId)));
    }

    [Fact]
    public void TranscriptionOnElevenLabsAsksElevenLabs()
    {
        Assert.Equal([AccountProvider.ElevenLabs], InUse(Hearing(Edge, SttProviderCatalog.ElevenLabsId)));
    }

    [Fact]
    public void TranscriptionOnDeepgramAsksDeepgram()
    {
        Assert.Equal([AccountProvider.Deepgram], InUse(Hearing(Edge, SttProviderCatalog.DeepgramId)));
    }

    [Fact]
    public void AnElevenLabsVoiceAndDeepgramTranscriptionAskBoth()
    {
        var settings = Hearing(Edge with { Speech = Edge.Speech with { Provider = TtsProviderCatalog.ElevenLabsId } }, SttProviderCatalog.DeepgramId);

        Assert.Equal([AccountProvider.ElevenLabs, AccountProvider.Deepgram], InUse(settings));
    }

    [Fact]
    public void AnotherHostedTranscriptionAsksNobody()
    {
        Assert.Empty(InUse(Hearing(Edge, SttProviderCatalog.GroqId)));
    }
}
