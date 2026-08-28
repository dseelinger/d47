namespace D47.Core.Audio;

/// <summary>
/// A <see cref="VoiceCast"/> per provider, because a voice id means nothing to a provider that
/// did not issue it (Phase 57).
/// <para>
/// Phase 11 could hold one cast, because there was one provider and every voice in it came from
/// the same list. With six slots naming up to six providers, a stranger in local drawn from
/// Edge's pool and the ship's AI pinned to an ElevenLabs id are two different vocabularies —
/// handing either one to the other's synthesiser is the failure Phase 19 spent a release
/// chasing, arriving by a new road.
/// </para>
/// <para>
/// So the multiplexing lives here and <see cref="VoiceCast"/> is untouched: it still holds one
/// pool, one rate and one set of assignments, and still knows nothing about slots. Two slots
/// that name the same provider share a cast, which is what keeps a station and a wingmate from
/// being given the same voice — the stepping-past in <see cref="VoiceCast.ForSender"/> only
/// works across senders it can see.
/// </para>
/// <para>
/// Owns no thread and reads no clock, exactly as the cast it holds does.
/// </para>
/// </summary>
public sealed class VoiceCasting
{
    private readonly Dictionary<string, VoiceCast> _casts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The cast for one provider, made on first use. Made rather than found because a slot can
    /// be moved onto a provider between two ticks, and a null here would be a silent line.
    /// </summary>
    public VoiceCast Of(string providerId)
    {
        if (!_casts.TryGetValue(providerId, out var cast))
        {
            cast = new VoiceCast();
            _casts[providerId] = cast;
        }

        return cast;
    }

    /// <summary>Every provider a cast has been made for. For diagnostics and for tests.</summary>
    public IReadOnlyCollection<string> Providers => _casts.Keys;

    /// <summary>
    /// A new system, for all of them. The turnover is a property of the galaxy rather than of a
    /// provider, so it reaches every cast — a Commander whose NPCs are on Edge and whose
    /// squadron is on ElevenLabs still left both behind when they jumped.
    /// </summary>
    public void EnteredSystem()
    {
        foreach (var cast in _casts.Values)
        {
            cast.EnteredSystem();
        }
    }

    /// <summary>
    /// Drops one provider's cast entirely — its pool, its role voices and every assignment made
    /// from it. For a provider no slot speaks through any more, and for one whose voice list has
    /// just been refetched.
    /// </summary>
    public void Forget(string providerId) => _casts.Remove(providerId);

    /// <summary>A new session. Everything goes, for the reason <see cref="VoiceCast.Reset"/> gives.</summary>
    public void Reset() => _casts.Clear();
}
