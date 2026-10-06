using D47.Core.Catalog;
using D47.Core.Conversation;

using Xunit;

namespace D47.Core.Tests.Catalog;

/// <summary>Replaces the shared catalog, so nothing may run beside it.</summary>
[CollectionDefinition(nameof(SharedModelCatalogCollection), DisableParallelization = true)]
public class SharedModelCatalogCollection;

[Collection(nameof(SharedModelCatalogCollection))]
public class AReplacedCatalogIsReadAtOnceTests
{
    [Fact]
    public void TheProviderListTakesTheNewDefault()
    {
        var original = ModelCatalogSource.Shared.Current;

        try
        {
            ModelCatalogSource.Shared.Replace(ModelCatalog.Parse("""
                {
                  "schema": 1,
                  "published": "2026-10-06",
                  "providers": {
                    "anthropic": {
                      "default": "claude-later-6",
                      "backgroundDefault": null,
                      "models": [
                        { "id": "claude-later-6", "offered": true, "price": { "input": 3, "output": 15 } }
                      ]
                    }
                  }
                }
                """));

            var anthropic = LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId);

            Assert.Equal("claude-later-6", anthropic.DefaultModel);
            Assert.Equal(["claude-later-6"], anthropic.Models);
            Assert.Equal(3m, PriceTable.Default.For(LlmProviderCatalog.AnthropicId, "claude-later-6")!.InputPerMillion);
        }
        finally
        {
            ModelCatalogSource.Shared.Replace(original);
        }

        Assert.Equal("claude-sonnet-5-5", LlmProviderCatalog.Selected(LlmProviderCatalog.AnthropicId).DefaultModel);
    }
}
