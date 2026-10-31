using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Llm.OpenAi;
using Xunit;

namespace D47.Llm.Tests;

public sealed class TheLlmKeyCheckTests
{
    private static readonly LlmProviderInfo Provider = LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId);

    [Fact]
    public void ARefusalIsRejectedWithItsDetail()
    {
        var verdict = LlmKeyCheck.Verdict(Provider, EndpointModels.Refused("bad key"));

        Assert.Equal(SecretCheck.Rejected("bad key"), verdict);
    }

    [Fact]
    public void ARefusalWithNoDetailNamesTheProvider()
    {
        var verdict = LlmKeyCheck.Verdict(Provider, EndpointModels.Refused(null));

        Assert.Equal(SecretCheck.Rejected($"{Provider.Name} refused the request."), verdict);
    }

    [Fact]
    public void AnAnswerWithModelsWorksAndCountsThem()
    {
        var verdict = LlmKeyCheck.Verdict(Provider, new EndpointModels(EndpointReach.Answered, ["a", "b"], null));

        Assert.Equal(SecretCheck.Works($"{Provider.Name} answered — 2 models."), verdict);
    }

    [Fact]
    public void AnEmptyAnswerStillWorks()
    {
        var verdict = LlmKeyCheck.Verdict(Provider, new EndpointModels(EndpointReach.Answered, [], null));

        Assert.Equal(
            SecretCheck.Works($"{Provider.Name} answered, but lists no models. Type the model name yourself."),
            verdict);
    }

    [Fact]
    public void SilenceIsUnreachable()
    {
        Assert.Equal(
            SecretCheck.Unreachable($"{Provider.Name} could not be reached."),
            LlmKeyCheck.Verdict(Provider, EndpointModels.Unreachable(null)));
        Assert.Equal(
            SecretCheck.Unreachable("timed out"),
            LlmKeyCheck.Verdict(Provider, EndpointModels.Unreachable("timed out")));
    }

    [Fact]
    public void EachOfTheThreeProvidersHasALister()
    {
        foreach (var id in new[] { LlmProviderCatalog.AnthropicId, LlmProviderCatalog.OpenAiId, LlmProviderCatalog.OpenAiCompatibleId })
        {
            var client = LlmProviderFactory.Create(LlmProviderCatalog.Selected(id), "key", "http://localhost:1/v1");

            Assert.NotNull(LlmProviderFactory.ModelLister(client));
        }
    }

    [Fact]
    public void AnotherProviderOrNoneHasNoLister()
    {
        Assert.Null(LlmProviderFactory.ModelLister(null));
        Assert.Null(LlmProviderFactory.ModelLister(new OtherProvider()));
    }

    private sealed class OtherProvider : ILlmProvider
    {
        public string Id => "other";

        public string DisplayName => "Other";

        public string DefaultModel => "none";

        public LlmProviderCapabilities CapabilitiesFor(string model) => throw new NotSupportedException();

        public IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
