using System.Runtime.CompilerServices;
using D47.Core.Conversation;

namespace D47.Scenarios.Tests;

/// <summary>Any provider, with the tool calls it asked for written down on the way past.</summary>
public sealed class RecordingLlmProvider(ILlmProvider inner) : ILlmProvider
{
    private readonly List<(string Tool, string ArgumentsJson)> _asked = [];

    private readonly List<LlmRequest> _requests = [];

    /// <summary>Every tool call the model produced this session, in order.</summary>
    public IReadOnlyList<(string Tool, string ArgumentsJson)> Asked => _asked;

    /// <summary>Every request that went out, which is what the on-the-wire assertions read.</summary>
    public IReadOnlyList<LlmRequest> Requests => _requests;

    public string Id => inner.Id;

    public bool RunsOnThisMachine => inner.RunsOnThisMachine;

    public string DisplayName => inner.DisplayName;

    public string DefaultModel => inner.DefaultModel;

    public LlmProviderCapabilities CapabilitiesFor(string model) => inner.CapabilitiesFor(model);

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _requests.Add(request);

        await foreach (var streamEvent in inner.StreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (streamEvent is LlmStreamEvent.ToolUse use)
            {
                _asked.Add((use.Name, use.InputJson));
            }

            yield return streamEvent;
        }
    }
}
