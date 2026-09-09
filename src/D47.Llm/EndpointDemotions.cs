using System.Collections.Concurrent;

namespace D47.Llm;

/// <summary>Which optional parts of a request an endpoint has refused, this session (Phase 29).</summary>
internal static class EndpointDemotions
{
    /// <summary>One endpoint, one model.</summary>
    private readonly record struct Key(string Endpoint, string Model);

    private sealed class KeyComparer : IEqualityComparer<Key>
    {
        public static readonly KeyComparer Instance = new();

        public bool Equals(Key x, Key y) =>
            StringComparer.OrdinalIgnoreCase.Equals(x.Endpoint, y.Endpoint)
            && StringComparer.Ordinal.Equals(x.Model, y.Model);

        public int GetHashCode(Key key) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(key.Endpoint),
            StringComparer.Ordinal.GetHashCode(key.Model));
    }

    private static readonly ConcurrentDictionary<Key, ConcurrentDictionary<Demotable, bool>> Refused =
        new(KeyComparer.Instance);

    /// <summary>
    /// Whether <paramref name="what"/> may still be sent to <paramref name="endpoint"/> for <paramref
    /// name="model"/>.
    /// </summary>
    public static bool Allows(string endpoint, Demotable what, string model = "") =>
        !Refused.TryGetValue(new Key(endpoint, model), out var refused) || !refused.ContainsKey(what);

    /// <summary>Records a refusal.</summary>
    public static bool Demote(string endpoint, Demotable what, string model = "") =>
        Refused.GetOrAdd(new Key(endpoint, model), _ => new ConcurrentDictionary<Demotable, bool>()).TryAdd(what, true);

    /// <summary>Everything this endpoint has refused, for the settings surface to explain.</summary>
    public static IReadOnlyCollection<Demotable> RefusedBy(string endpoint, string model = "") =>
        Refused.TryGetValue(new Key(endpoint, model), out var refused) ? [.. refused.Keys] : [];

    /// <summary>Forgets everything.</summary>
    internal static void Clear() => Refused.Clear();
}

/// <summary>The parts of a request that can be dropped and still leave a turn worth having.</summary>
internal enum Demotable
{
    /// <summary>Tool definitions.</summary>
    Tools,

    /// <summary><c>reasoning_effort</c>.</summary>
    ReasoningEffort,

    /// <summary><c>stream_options.include_usage</c>.</summary>
    StreamUsage,

    /// <summary>
    /// <c>max_completion_tokens</c>, which replaced <c>max_tokens</c> and which older compatible
    /// servers have never heard of.
    /// </summary>
    ModernTokenLimit,

    /// <summary><c>temperature</c> (#98).</summary>
    Sampling,

    /// <summary>
    /// Anthropic's <c>thinking</c> and <c>output_config.effort</c>, which are one entry because they
    /// are one capability: both arrived with the 4.6 generation and a model that rejects either rejects
    /// both.
    /// </summary>
    AdaptiveThinking,
}
