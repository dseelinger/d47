using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Audio;

public class AMetBeforeLineSaysItCarriesACommandersNameTests
{
    private const string Sentence = "A line saying you met a Commander before carries that Commander's name to";

    private static string Disclosed(bool metBefore) =>
        EgressDisclosure.Entry(
            EgressDisclosure.TextToSpeech,
            D47Settings.Defaults with
            {
                Speech = D47Settings.Defaults.Speech with { Provider = TtsProviderCatalog.EdgeId },
                Callouts = D47Settings.Defaults.Callouts with { MetBefore = metBefore },
            },
            llmKeyPresent: false).What;

    [Fact]
    public void WithTheCalloutOnTheDisclosureSaysSo() => Assert.Contains(Sentence, Disclosed(metBefore: true));

    [Fact]
    public void WithTheCalloutOffItDoesNot() => Assert.DoesNotContain(Sentence, Disclosed(metBefore: false));
}
