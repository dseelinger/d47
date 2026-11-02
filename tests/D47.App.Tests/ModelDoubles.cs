using D47.Core.Conversation;

namespace D47.App.Tests;

/// <summary>Plays one scripted round per call, keeping the text of every request; calls from <see cref="HoldFrom"/> on wait for <see cref="Hold"/>.</summary>
internal sealed class RecordingModel(params IReadOnlyList<LlmStreamEvent>[] rounds) : ILlmProvider
{
    private readonly List<string> _asked = [];

    public int HoldFrom { get; init; } = int.MaxValue;

    public Task Hold { get; init; } = Task.CompletedTask;

    public IReadOnlyList<string> Asked
    {
        get
        {
            lock (_asked)
            {
                return [.. _asked];
            }
        }
    }

    public string Id => "anthropic";

    public string DisplayName => "Recording";

    public string DefaultModel => "claude-opus-5";

    /// <summary>Replies in order, every call waiting for <paramref name="hold"/>.</summary>
    public static RecordingModel Replying(Task hold, params string[] replies) =>
        new([.. replies.Select(reply => (IReadOnlyList<LlmStreamEvent>)
            [new LlmStreamEvent.TextDelta(reply), new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed)])])
        {
            HoldFrom = 0,
            Hold = hold,
        };

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
        int round;

        lock (_asked)
        {
            round = _asked.Count;
            _asked.Add(string.Join("\n", request.Prompt.History.SelectMany(message => message.Content).OfType<ConversationContent.Text>().Select(part => part.Value)));
        }

        if (round >= HoldFrom)
        {
            await Hold.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (round >= rounds.Length)
        {
            throw new InvalidOperationException($"Round {round + 1} was asked for and {rounds.Length} were scripted.");
        }

        foreach (var streamEvent in rounds[round])
        {
            yield return streamEvent;
        }
    }
}

/// <summary>Says the scripted replies in order and keeps every request.</summary>
internal sealed class ScriptedReplies(params string[] replies) : ILlmProvider
{
    public List<LlmRequest> Requests { get; } = [];

    public string Id => "anthropic";

    public string DisplayName => "Scripted";

    public string DefaultModel => "claude-sonnet-5-5";

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
        Requests.Add(request);
        await Task.Yield();

        yield return new LlmStreamEvent.TextDelta(replies[Requests.Count - 1]);
        yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
    }
}
