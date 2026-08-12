using D47.Core.Conversation;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// A scripted provider. Its existence is the point of the seam: the whole turn path — routing,
/// effort choice, prompt assembly, streaming, usage accounting, cost — is exercisable with no
/// network, no key and no vendor SDK.
/// </summary>
public sealed class FakeLlmProvider : ILlmProvider
{
    private readonly IReadOnlyList<LlmStreamEvent> _script;

    public FakeLlmProvider(params LlmStreamEvent[] script) => _script = script;

    /// <summary>The request the last call was made with, for asserting on prompt assembly.</summary>
    public LlmRequest? LastRequest { get; private set; }

    public int CallCount { get; private set; }

    public string Id { get; init; } = "anthropic";

    public string DisplayName => "Fake";

    public string DefaultModel { get; init; } = "claude-opus-5";

    public LlmProviderCapabilities CapabilitiesFor(string model) => new()
    {
        SupportsPromptCaching = true,
        SupportsThinkingEffort = true,
        SupportsOperatorSystemMessages = true,
        MinimumCacheablePrefixTokens = 512,
    };

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LastRequest = request;
        CallCount++;

        foreach (var streamEvent in _script)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return streamEvent;
        }

        await Task.CompletedTask;
    }

    /// <summary>The common case: some text, then a clean completion with the given usage.</summary>
    public static FakeLlmProvider Answering(string reply, LlmUsage? usage = null) =>
        new(
            new LlmStreamEvent.TextDelta(reply),
            new LlmStreamEvent.Completed(usage ?? LlmUsage.None, LlmStopReason.Completed));
}
