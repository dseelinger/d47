using System.Collections.Concurrent;

namespace D47.Llm.OpenAi;

/// <summary>
/// Which optional parts of a request an endpoint has refused, this session (list.md Phase 29).
/// <para>
/// <b>The rule is advertise, then demote.</b> A model list says what an endpoint serves and
/// nothing about what it accepts — whether tool calls work, whether reasoning effort is a field
/// it knows, whether it will report usage at all. That is learned from the first failure rather
/// than assumed, because assuming means either offering nothing or lying about everything.
/// </para>
/// <para>
/// <b>Once per capability per endpoint, never a loop.</b> A rejection names the field it
/// rejected, so the capability turns off for that endpoint and the turn is attempted once more
/// without it. A retry policy that searches for a working request shape is indistinguishable
/// from an outage from the Commander's seat.
/// </para>
/// <para>
/// <b>Session only, and never written down.</b> A demotion persisted to disk outlives the server
/// upgrade that fixed it, and the Commander would have no way of knowing why d47 had quietly
/// stopped offering tools to a server that now supports them. Keyed statically by address rather
/// than held on the provider, because the provider is rebuilt on every settings change and a
/// demotion that forgets itself that easily would be re-probed on every visit to the panel.
/// </para>
/// </summary>
internal static class EndpointDemotions
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<Demotable, bool>> Refused =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="what"/> may still be sent to <paramref name="endpoint"/>.</summary>
    public static bool Allows(string endpoint, Demotable what) =>
        !Refused.TryGetValue(endpoint, out var refused) || !refused.ContainsKey(what);

    /// <summary>
    /// Records a refusal. Returns false if this endpoint had already refused this — which is
    /// what makes the retry happen exactly once rather than forever.
    /// </summary>
    public static bool Demote(string endpoint, Demotable what) =>
        Refused.GetOrAdd(endpoint, _ => new ConcurrentDictionary<Demotable, bool>()).TryAdd(what, true);

    /// <summary>Everything this endpoint has refused, for the settings surface to explain.</summary>
    public static IReadOnlyCollection<Demotable> RefusedBy(string endpoint) =>
        Refused.TryGetValue(endpoint, out var refused) ? [.. refused.Keys] : [];

    /// <summary>Forgets everything. For tests, and for nothing else — there is no reset in the app.</summary>
    internal static void Clear() => Refused.Clear();
}

/// <summary>
/// The parts of a request that can be dropped and still leave a turn worth having. Each is
/// something d47 would rather send than not, and none of them is worth failing a turn over.
/// </summary>
internal enum Demotable
{
    /// <summary>Tool definitions. Dropping them leaves a model that can talk but not act.</summary>
    Tools,

    /// <summary>
    /// <c>reasoning_effort</c>. Unknown to most local servers, and dropping it costs the effort
    /// router its lever rather than costing the turn.
    /// </summary>
    ReasoningEffort,

    /// <summary>
    /// <c>stream_options.include_usage</c>. Dropping it means the turn is unpriced rather than
    /// mispriced, which is the honest side of that trade.
    /// </summary>
    StreamUsage,

    /// <summary>
    /// <c>max_completion_tokens</c>, which replaced <c>max_tokens</c> and which older compatible
    /// servers have never heard of. The fallback is the older field rather than no ceiling at
    /// all: a cockpit reply has to stop.
    /// </summary>
    ModernTokenLimit,
}
