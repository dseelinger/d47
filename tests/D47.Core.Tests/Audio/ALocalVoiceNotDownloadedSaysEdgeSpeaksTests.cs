using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Speech;
using Xunit;

namespace D47.Core.Tests.Audio;

[CollectionDefinition(nameof(LocalVoiceStandInCollection), DisableParallelization = true)]
public sealed class LocalVoiceStandInCollection;

/// <summary>The spoken-replies disclosure for Kokoro and Chatterbox, before and after the model is on disk.</summary>
[Collection(nameof(LocalVoiceStandInCollection))]
public class ALocalVoiceNotDownloadedSaysEdgeSpeaksTests
{
    private static D47Settings With(string provider) =>
        D47Settings.Defaults with { Speech = D47Settings.Defaults.Speech with { Provider = provider } };

    private static EgressEntry Disclosure(string provider, bool installed)
    {
        var before = LocalVoiceStandIn.Installed;
        LocalVoiceStandIn.Installed = _ => installed;

        try
        {
            return EgressDisclosure.Entry(EgressDisclosure.TextToSpeech, With(provider), llmKeyPresent: false);
        }
        finally
        {
            LocalVoiceStandIn.Installed = before;
        }
    }

    [Theory]
    [InlineData(TtsProviderCatalog.KokoroId)]
    [InlineData(TtsProviderCatalog.ChatterboxId)]
    public void UntilTheModelIsDownloadedTheTextGoesToMicrosoft(string provider)
    {
        var entry = Disclosure(provider, installed: false);

        Assert.True(entry.Active);
        Assert.Contains("speech.platform.bing.com", entry.Destination, StringComparison.Ordinal);
        Assert.Contains("Until the", entry.What, StringComparison.Ordinal);
        Assert.Contains("goes to Microsoft", entry.What, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TtsProviderCatalog.KokoroId)]
    [InlineData(TtsProviderCatalog.ChatterboxId)]
    public void OnceTheModelIsDownloadedNothingIsSent(string provider)
    {
        var entry = Disclosure(provider, installed: true);

        Assert.Equal("nothing sent", entry.Destination);
        Assert.Contains("Nothing is sent anywhere", entry.What, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft", entry.What, StringComparison.Ordinal);
    }
}
