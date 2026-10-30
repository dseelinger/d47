namespace D47.Core.Audio;

/// <summary>
/// A <see cref="VoiceCast"/> per provider, because a voice id means nothing to a provider that did not
/// issue it (Phase 57).
/// </summary>
public sealed class VoiceCasting
{
    private readonly Dictionary<string, VoiceCast> _casts = new(StringComparer.OrdinalIgnoreCase);

    private string? _system;

    /// <summary>The cast for one provider, made on first use.</summary>
    public VoiceCast Of(string providerId)
    {
        if (!_casts.TryGetValue(providerId, out var cast))
        {
            cast = new VoiceCast { System = _system };
            _casts[providerId] = cast;
        }

        return cast;
    }

    /// <summary>Records the system without clearing anyone's voice, for the first one seen.</summary>
    public void InSystem(string? system)
    {
        _system = system;

        foreach (var cast in _casts.Values)
        {
            cast.System = system;
        }
    }

    /// <summary>A new system, for all of them.</summary>
    public void EnteredSystem(string? system = null)
    {
        _system = system;

        foreach (var cast in _casts.Values)
        {
            cast.EnteredSystem(system);
        }
    }

    /// <summary>
    /// Drops one provider's cast entirely — its pool, its role voices and every assignment made from
    /// it.
    /// </summary>
    public void Forget(string providerId) => _casts.Remove(providerId);
}
