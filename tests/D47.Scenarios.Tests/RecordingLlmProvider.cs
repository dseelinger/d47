using System.Runtime.CompilerServices;
using D47.Core.Conversation;

namespace D47.Scenarios.Tests;

/// <summary>
/// Any provider, with the tool calls it asked for written down on the way past.
/// <para>
/// This exists because <see cref="TurnEvent.ToolStarted"/> carries the tool's name and not its
/// arguments, and the trace needs the arguments. That is a fact about the <em>event</em> rather
/// than a shortcoming of it — the arguments are the model's, they can be large, and a UI has no
/// use for them — so the harness reads them off the stream instead of asking the turn loop to
/// carry them. A production type does not grow a field to make a test easier, and a turn loop
/// that behaves differently under test is a turn loop this suite could not make a claim about
/// (docs/plans/phase-30-prove-the-model-behaves.md).
/// </para>
/// <para>
/// It records what the model <b>asked for</b>, which is deliberately not the same as what ran: a
/// protected tool never reaches its handler when the model is the caller, and a corpus run that
/// could only see handlers would report that attack as though the model had never tried.
/// </para>
/// </summary>
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
