using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Listening;
using Xunit;
using D47.Core.Audio;

namespace D47.Core.Tests.Configuration;

public class AProviderNoCatalogKnowsIsReadAsItsFallbackTests
{
    [Fact]
    public void AProviderNoCatalogKnowsIsReadAsItsFallback()
    {
        var choices = SetupChoices.From(new D47Settings
        {
            Llm = new LlmSettings { Provider = "retired" },
            Speech = new SpeechSettings { Provider = "retired" },
            Listening = new ListeningSettings { Provider = "retired" },
        });

        Assert.Equal(LlmProviderCatalog.NoneId, choices.Conversation);
        Assert.Equal(TtsProviderCatalog.EdgeId, choices.Voice);
        Assert.Equal(SttProviderCatalog.LocalId, choices.Listening);
    }
}
