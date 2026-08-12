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

/// <summary>A provider that throws rather than reporting. Still just a failed turn.</summary>
public sealed class ThrowingLlmProvider : ILlmProvider
{
    public int CallCount { get; private set; }

    public string Id => "anthropic";

    public string DisplayName => "Throwing";

    public string DefaultModel => "claude-opus-5";

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
        CallCount++;
        await Task.CompletedTask;
        throw new IOException("the socket went away");

#pragma warning disable CS0162 // Required to make this an iterator rather than a plain throw.
        yield break;
#pragma warning restore CS0162
    }
}

/// <summary>Fails a fixed number of times, then answers. The recovery case.</summary>
public sealed class FlakyLlmProvider(int failuresBeforeSuccess) : ILlmProvider
{
    private int _calls;

    public string Id => "anthropic";

    public string DisplayName => "Flaky";

    public string DefaultModel => "claude-opus-5";

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
        await Task.CompletedTask;

        if (_calls++ < failuresBeforeSuccess)
        {
            yield return new LlmStreamEvent.Failed("Overloaded.", Transient: true);
            yield break;
        }

        yield return new LlmStreamEvent.TextDelta("Jump range is 12.5 light years.");
        yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
    }
}
