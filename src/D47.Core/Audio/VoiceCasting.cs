namespace D47.Core.Audio;

/// <summary>
/// A <see cref="VoiceCast"/> per provider, because a voice id means nothing to a provider that did not
/// issue it (Phase 57).
/// </summary>
public sealed class VoiceCasting
{
    private readonly Dictionary<string, VoiceCast> _casts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The cast for one provider, made on first use.</summary>
    public VoiceCast Of(string providerId)
    {
        if (!_casts.TryGetValue(providerId, out var cast))
        {
            cast = new VoiceCast();
            _casts[providerId] = cast;
        }

        return cast;
    }

    /// <summary>Every provider a cast has been made for.</summary>
    public IReadOnlyCollection<string> Providers => _casts.Keys;

    /// <summary>A new system, for all of them.</summary>
    public void EnteredSystem()
    {
        foreach (var cast in _casts.Values)
        {
            cast.EnteredSystem();
        }
    }

    /// <summary>
    /// Drops one provider's cast entirely — its pool, its role voices and every assignment made from
    /// it.
    /// </summary>
    public void Forget(string providerId) => _casts.Remove(providerId);

    /// <summary>A new session.</summary>
    public void Reset() => _casts.Clear();
}
