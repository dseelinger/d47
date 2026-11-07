using D47.Core.Conversation;

namespace D47.Core.Tests.Adventures;

/// <summary>An Anthropic-shaped provider fake: each fake supplies its name and its stream.</summary>
internal abstract class StoryTestProvider : ILlmProvider
{
    public string Id => "anthropic";

    public string DefaultModel => "claude-opus-5";

    public bool RunsOnThisMachine => true;

    public LlmProviderCapabilities CapabilitiesFor(string model) => new()
    {
        SupportsPromptCaching = false,
        SupportsThinkingEffort = true,
        SupportsOperatorSystemMessages = true,
        MinimumCacheablePrefixTokens = 0,
    };

    public abstract IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken cancellationToken);
}
