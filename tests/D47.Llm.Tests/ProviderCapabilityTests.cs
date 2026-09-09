using Xunit;

namespace D47.Llm.Tests;

[Collection(nameof(EndpointDemotionCollection))]
public class ProviderCapabilityTests
{
    private static AnthropicLlmProvider Own() => new("test-key");

    private static AnthropicLlmProvider Gateway() => new("test-key", "http://127.0.0.1:9");

    [Fact]
    public void TheSeamIdentifiesItself()
    {
        var provider = Own();

        Assert.Equal("anthropic", provider.Id);
        Assert.Equal("Anthropic", provider.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(provider.DefaultModel));
    }

    /// <summary>The models that accept a <c>{"role":"system"}</c> message carrying operator authority.</summary>
    [Theory]
    [InlineData("claude-opus-5", true)]
    [InlineData("claude-opus-4-8", true)]
    [InlineData("claude-fable-5", true)]
    [InlineData("claude-sonnet-5", false)]
    [InlineData("claude-haiku-4-5", false)]
    public void OperatorSystemMessagesAreDeclaredPerModel(string model, bool expected)
    {
        Assert.Equal(expected, Own().CapabilitiesFor(model).SupportsOperatorSystemMessages);
    }

    [Theory]
    [InlineData("claude-opus-5", 512)]
    [InlineData("claude-fable-5", 512)]
    [InlineData("claude-opus-4-8", 1024)]
    [InlineData("claude-sonnet-5", 1024)]
    [InlineData("claude-opus-4-7", 2048)]
    [InlineData("claude-haiku-4-5", 4096)]
    public void TheMinimumCacheablePrefixIsPerModel(string model, int expected)
    {
        Assert.Equal(expected, Own().CapabilitiesFor(model).MinimumCacheablePrefixTokens);
    }

    [Fact]
    public void AnUnknownModelGetsAConservativeDefault()
    {
        var capabilities = Own().CapabilitiesFor("claude-something-7");

        Assert.Equal(1024, capabilities.MinimumCacheablePrefixTokens);
        Assert.False(capabilities.SupportsOperatorSystemMessages);
    }

    [Fact]
    public void WebSearchIsOfferedOnlyOnAnthropicsOwnEndpoint()
    {
        Assert.True(Own().CapabilitiesFor("claude-opus-5").SupportsWebSearch);
        Assert.False(Gateway().CapabilitiesFor("claude-opus-5").SupportsWebSearch);
    }

    /// <summary>Caching and tool calls are protocol features rather than deployment ones, so they hold everywhere.</summary>
    [Fact]
    public void CachingAndToolCallsHoldEverywhere()
    {
        foreach (var provider in new[] { Own(), Gateway() })
        {
            var capabilities = provider.CapabilitiesFor("claude-opus-5");

            Assert.True(capabilities.SupportsPromptCaching);
            Assert.True(capabilities.SupportsToolCalls);
        }
    }

    /// <summary>Which models take <c>thinking</c> and <c>output_config.effort</c>.</summary>
    [Theory]
    [InlineData("claude-opus-5", true)]
    [InlineData("claude-opus-4-8", true)]
    [InlineData("claude-sonnet-5", true)]
    [InlineData("claude-fable-5", true)]
    [InlineData("claude-haiku-4-5", false)]
    public void AnEffortIsDeclaredPerModel(string model, bool expected)
    {
        foreach (var provider in new[] { Own(), Gateway() })
        {
            Assert.Equal(expected, provider.CapabilitiesFor(model).SupportsThinkingEffort);
        }
    }

    /// <summary>An unknown model is assumed to take an effort, the opposite default to the cacheable prefix above.</summary>
    [Fact]
    public void AModelD47HasNotHeardOfIsAssumedToTakeAnEffort()
    {
        Assert.True(Own().CapabilitiesFor("claude-something-7").SupportsThinkingEffort);
    }

    /// <summary>An empty base URL is Anthropic's own endpoint, not a gateway.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyEndpointIsTheDefaultOne(string? baseUrl)
    {
        Assert.True(new AnthropicLlmProvider("test-key", baseUrl).CapabilitiesFor("claude-opus-5").SupportsWebSearch);
    }
}
