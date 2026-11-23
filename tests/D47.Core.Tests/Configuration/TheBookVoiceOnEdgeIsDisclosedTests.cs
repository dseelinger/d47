using D47.Core.Audio;
using D47.Core.Configuration;
using Xunit;

namespace D47.Core.Tests.Configuration;

public class TheBookVoiceOnEdgeIsDisclosedTests
{
    private const string Bing = "speech.platform.bing.com";

    private static D47Settings With(string slotProvider, string bookProvider)
    {
        var settings = D47Settings.Defaults;

        return settings with
        {
            Speech = settings.Speech with { Provider = slotProvider, GroupProviders = null },
            Books = new BookSettings { Provider = bookProvider },
        };
    }

    private static EgressEntry SpokenReplies(D47Settings settings) =>
        EgressDisclosure.Entry(EgressDisclosure.TextToSpeech, settings, llmKeyPresent: false);

    [Fact]
    public void ANewBookVoiceIsEdgeNeural() =>
        Assert.Equal("edge", new BookSettings().Provider);

    [Fact]
    public void EdgeForBooksAddsMicrosoftToSpokenRepliesWhenNoSlotUsesIt()
    {
        var entry = SpokenReplies(With(TtsProviderCatalog.KokoroId, TtsProviderCatalog.EdgeId));

        Assert.Contains(Bing, entry.Destination, StringComparison.Ordinal);
        Assert.Contains("Books → Edge Neural", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void AKokoroBookVoiceLeavesSpokenRepliesAsTheyWere()
    {
        var entry = SpokenReplies(With(TtsProviderCatalog.KokoroId, TtsProviderCatalog.KokoroId));

        Assert.DoesNotContain(Bing, entry.Destination, StringComparison.Ordinal);
        Assert.DoesNotContain("Books →", entry.What, StringComparison.Ordinal);
    }

    [Fact]
    public void ASlotAlreadyOnEdgeLeavesTheEntryUnchangedByTheBookVoice()
    {
        var withBooksOnEdge = SpokenReplies(With(TtsProviderCatalog.EdgeId, TtsProviderCatalog.EdgeId));
        var withBooksOnKokoro = SpokenReplies(With(TtsProviderCatalog.EdgeId, TtsProviderCatalog.KokoroId));

        Assert.Equal(withBooksOnKokoro, withBooksOnEdge);
        Assert.DoesNotContain("Books →", withBooksOnEdge.What, StringComparison.Ordinal);
    }

    [Fact]
    public void EdgeForBooksIsDisclosedEvenWhenNoSlotSpeaks()
    {
        var entry = SpokenReplies(With(TtsProviderCatalog.None.Id, TtsProviderCatalog.EdgeId));

        Assert.Contains(Bing, entry.Destination, StringComparison.Ordinal);
        Assert.True(entry.Active);
    }
}
