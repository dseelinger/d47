using D47.Core.Catalog;

using Xunit;

namespace D47.Llm.Tests;

[Collection(nameof(EndpointDemotionCollection))]
public class AnthropicReadsHowToCallAModelFromItsCatalogTests
{
    private static readonly ModelCatalogSource Source = new(ModelCatalog.Parse("""
        {
          "schema": 1,
          "published": "2026-10-06",
          "providers": {
            "anthropic": {
              "default": "claude-catalogued-9",
              "backgroundDefault": null,
              "models": [
                {
                  "id": "claude-catalogued-9",
                  "offered": true,
                  "price": { "input": 3, "output": 15 },
                  "traits": { "operatorSystemMessages": true, "toolSearch": true, "minimumCacheablePrefix": 256, "basicWebSearchOnly": false, "legacyThinking": true }
                }
              ]
            }
          },
          "speech": {
            "elevenlabs": { "default": "eleven-a", "models": [ { "id": "eleven-a", "offered": true } ] },
            "openai": { "default": "tts-a", "models": [ { "id": "tts-a", "offered": true } ] },
            "cartesia": { "default": "sonic-a", "models": [ { "id": "sonic-a", "offered": true } ] }
          }
        }
        """));

    [Fact]
    public void TheDefaultAndTraitsComeFromTheSourceItWasGiven()
    {
        var provider = new AnthropicLlmProvider("test-key", catalog: Source);
        var capabilities = provider.CapabilitiesFor("claude-catalogued-9");

        Assert.Equal("claude-catalogued-9", provider.DefaultModel);
        Assert.True(capabilities.SupportsOperatorSystemMessages);
        Assert.True(capabilities.SupportsToolSearch);
        Assert.Equal(256, capabilities.MinimumCacheablePrefixTokens);
        Assert.False(capabilities.SupportsThinkingEffort);
    }

    [Fact]
    public void AModelTheCatalogDoesNotDescribeKeepsTheConservativeDefaults()
    {
        var capabilities = new AnthropicLlmProvider("test-key", catalog: Source).CapabilitiesFor("claude-sonnet-5-5");

        Assert.False(capabilities.SupportsOperatorSystemMessages);
        Assert.False(capabilities.SupportsToolSearch);
        Assert.Equal(1024, capabilities.MinimumCacheablePrefixTokens);
        Assert.True(capabilities.SupportsThinkingEffort);
    }
}
