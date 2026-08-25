using Xunit;

namespace D47.Llm.Tests;

/// <summary>
/// What the provider says a model can do (architecture.md §2, "capabilities as state").
/// <para>
/// These are read by the prompt assembler to decide where live game state goes and by the
/// caching arithmetic to decide whether a prefix is worth marking. Each is a table lookup, and
/// each has a default — so the failure mode is not an exception but a model being quietly
/// treated as less capable, or more, than it is.
/// </para>
/// <para>
/// In the demotion collection because one of these answers is no longer a table lookup alone:
/// effort also reads what the endpoint has refused this session, which is process-wide state
/// another test class can clear underneath it.
/// </para>
/// </summary>
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

    /// <summary>
    /// The models that accept a <c>{"role":"system"}</c> message carrying operator authority.
    /// Sonnet 5 is deliberately absent — it does not support them.
    /// </summary>
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

    /// <summary>
    /// The minimum cacheable prefix, which differs per model. Below it a prefix silently does not
    /// cache — no error, just no entry — so it is worth knowing rather than discovering.
    /// </summary>
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

    /// <summary>
    /// A model d47 has not heard of gets the conservative default rather than the smallest
    /// threshold. Guessing low here means marking prefixes that never cache and believing they do.
    /// </summary>
    [Fact]
    public void AnUnknownModelGetsAConservativeDefault()
    {
        var capabilities = Own().CapabilitiesFor("claude-something-7");

        Assert.Equal(1024, capabilities.MinimumCacheablePrefixTokens);
        Assert.False(capabilities.SupportsOperatorSystemMessages);
    }

    /// <summary>
    /// Server-side web search is the one capability that turns on the endpoint rather than the
    /// model. A gateway may forward somewhere that has no web search at all, and an unsupported
    /// declaration is a request that fails outright rather than one that quietly does less.
    /// </summary>
    [Fact]
    public void WebSearchIsOfferedOnlyOnAnthropicsOwnEndpoint()
    {
        Assert.True(Own().CapabilitiesFor("claude-opus-5").SupportsWebSearch);
        Assert.False(Gateway().CapabilitiesFor("claude-opus-5").SupportsWebSearch);
    }

    /// <summary>
    /// Caching and tool calls are true for everything this provider speaks to, gateway or not —
    /// they are protocol features rather than deployment ones.
    /// <para>
    /// <b>Effort used to be asserted here and is not any more</b> (list.md Phase 54). It is a
    /// per-model fact, not a protocol one: Haiku 4.5 predates the 4.6 generation and rejects the
    /// two fields that carry it. Left in this list, the claim would have been true of the model
    /// it was written against and wrong about the picker.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Which models take <c>thinking</c> and <c>output_config.effort</c>. Haiku 4.5 is the only
    /// one in the picker that does not, and getting this wrong is not a worse answer — it is a
    /// 400 on every turn, and a silent one on anything going through <c>FlavourTurn</c>.
    /// </summary>
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

    /// <summary>
    /// A model d47 has not heard of is assumed to take an effort, which is the opposite default
    /// to the cacheable prefix above and deliberately so. Taking an effort is the family rule —
    /// 4.6 and later — so guessing "current" is right for everything Anthropic ships next, and
    /// the one case it is wrong about heals itself on the first refusal rather than being wrong
    /// for ever. Guessing the other way would silently cost every future model its effort dial,
    /// and nothing would ever correct it.
    /// </summary>
    [Fact]
    public void AModelD47HasNotHeardOfIsAssumedToTakeAnEffort()
    {
        Assert.True(Own().CapabilitiesFor("claude-something-7").SupportsThinkingEffort);
    }

    /// <summary>
    /// An empty base URL is Anthropic's own endpoint, not a gateway. The setting is a text row a
    /// Commander can clear, and a cleared row that read as "some other endpoint" would silently
    /// turn web search off.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyEndpointIsTheDefaultOne(string? baseUrl)
    {
        Assert.True(new AnthropicLlmProvider("test-key", baseUrl).CapabilitiesFor("claude-opus-5").SupportsWebSearch);
    }
}
