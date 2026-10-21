using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.CaptainsLog;

/// <summary>The Privacy and egress entry for the captain's log follows the providers selected now.</summary>
public class TheLogDisclosureNamesTheSelectedProvidersTests
{
    private static D47Settings With(string listening, string speech) =>
        D47Settings.Defaults with
        {
            Listening = D47Settings.Defaults.Listening with { Provider = listening },
            Speech = D47Settings.Defaults.Speech with { Provider = speech },
        };

    [Fact]
    public void TheEntryIsListedAndSaysItStaysOnThisPc()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.CaptainsLog, With(SttProviderCatalog.LocalId, TtsProviderCatalog.KokoroId), llmKeyPresent: false);

        Assert.Contains(EgressDisclosure.CaptainsLog, EgressDisclosure.Ids);
        Assert.False(entry.Active);
        Assert.Contains("data\\captains-log.json", entry.What, StringComparison.Ordinal);
        Assert.Contains("never sent anywhere by D47", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void HostedProvidersAreNamedAsNotRunningOnThisPc()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.CaptainsLog, With(SttProviderCatalog.GroqId, TtsProviderCatalog.ElevenLabsId), llmKeyPresent: false);

        Assert.Contains("Groq, which does not run on this PC", entry.What, StringComparison.Ordinal);
        Assert.Contains("ElevenLabs, which does not run on this PC", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalProvidersAreNamedAsRunningOnThisPc()
    {
        var entry = EgressDisclosure.Entry(EgressDisclosure.CaptainsLog, With(SttProviderCatalog.LocalId, TtsProviderCatalog.KokoroId), llmKeyPresent: false);

        Assert.Contains("This computer, which runs on this PC", entry.What, StringComparison.Ordinal);
        Assert.Contains("Kokoro, which runs on this PC", entry.What, StringComparison.Ordinal);
    }
}
