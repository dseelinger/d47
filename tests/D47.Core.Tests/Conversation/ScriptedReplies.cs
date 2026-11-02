using D47.Core.Conversation;

namespace D47.Core.Tests.Conversation;

/// <summary>Answers each request with the next reply, repeating the last, and records what it was asked.</summary>
public sealed class ScriptedReplies(params string[] replies) : ILlmProvider
{
    public List<string> Requests { get; } = [];

    public List<(string? Model, ThinkingEffort Effort)> Calls { get; } = [];

    public string Id => "scripted";

    public string DisplayName => "Scripted";

    public string DefaultModel => "scripted";

    public LlmProviderCapabilities CapabilitiesFor(string model) => new()
    {
        SupportsPromptCaching = false,
        SupportsThinkingEffort = false,
        SupportsOperatorSystemMessages = false,
        MinimumCacheablePrefixTokens = 0,
    };

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Calls.Add((request.Model, request.Effort));
        Requests.Add(string.Join("\n", request.Prompt.History.Select(message => message.ToString())));
        yield return new LlmStreamEvent.TextDelta(replies[Math.Min(Requests.Count, replies.Length) - 1]);
        yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
        await Task.CompletedTask;
    }
}
