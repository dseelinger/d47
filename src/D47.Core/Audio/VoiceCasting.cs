using D47.Core.Callouts;
using D47.Core.Journal;

namespace D47.Core.Audio;

/// <summary>
/// A <see cref="VoiceCast"/> per provider, because a voice id means nothing to a provider that did not
/// issue it (Phase 57).
/// </summary>
public sealed class VoiceCasting
{
    private readonly Dictionary<string, VoiceCast> _casts = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _interdictors = new(StringComparer.OrdinalIgnoreCase);

    private DateTimeOffset? _arrival;

    private DateTimeOffset? _drop;

    /// <summary>The system NPC voices are drawn for, or null before one is known.</summary>
    public string? System { get; private set; }

    /// <summary>The cast for one provider, made on first use.</summary>
    public VoiceCast Of(string providerId)
    {
        if (!_casts.TryGetValue(providerId, out var cast))
        {
            cast = new VoiceCast();
            cast.EnteredSystem(System, _arrival);

            if (_drop is { } drop)
            {
                cast.Dropped(drop);
            }

            foreach (var interdictor in _interdictors)
            {
                cast.Interdicted(interdictor);
            }

            _casts[providerId] = cast;
        }

        return cast;
    }

    /// <summary>Records the system without clearing anyone's voice, for the first one seen.</summary>
    public void InSystem(string? system)
    {
        System = system;

        foreach (var cast in _casts.Values)
        {
            cast.System = system;
        }
    }

    /// <summary>A new system, for all of them, arrived in at journal time <paramref name="at"/> where it is known.</summary>
    public void EnteredSystem(string? system = null, DateTimeOffset? at = null)
    {
        System = system;
        _arrival = at;
        _drop = null;

        foreach (var cast in _casts.Values)
        {
            cast.EnteredSystem(system, at);
        }
    }

    /// <summary>
    /// Opens and closes NPC voice scopes from the Commander's journal events, in order: an arrival from
    /// <c>FSDJump</c>, <c>CarrierJump</c> or <c>Location</c>; a drop into normal space from <c>SupercruiseExit</c>,
    /// <c>CarrierJump</c> or <c>Location</c>; leaving it on <c>SupercruiseEntry</c>; and an NPC interdictor kept from
    /// <c>Interdicted</c> or <c>EscapeInterdiction</c>.
    /// </summary>
    public void Observe(IEnumerable<JournalEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var journalEvent in events)
        {
            switch (journalEvent.Kind)
            {
                case "FSDJump":
                    EnteredSystem(journalEvent.String("StarSystem") ?? System, journalEvent.Timestamp);
                    break;

                case "CarrierJump" or "Location":
                    EnteredSystem(journalEvent.String("StarSystem") ?? System, journalEvent.Timestamp);
                    Dropped(journalEvent.Timestamp);
                    break;

                case "SupercruiseExit":
                    Dropped(journalEvent.Timestamp);
                    break;

                case "SupercruiseEntry":
                    LeftNormalSpace();
                    break;

                case "Interdicted" or "EscapeInterdiction"
                    when !journalEvent.Bool("IsPlayer")
                         && (journalEvent.String("Interdictor_Localised") ?? journalEvent.String("Interdictor")) is { Length: > 0 } interdictor:
                    Interdicted(IncomingMessages.Undecorate(interdictor));
                    break;
            }
        }
    }

    private void Dropped(DateTimeOffset at)
    {
        _drop = at;

        foreach (var cast in _casts.Values)
        {
            cast.Dropped(at);
        }
    }

    private void LeftNormalSpace()
    {
        _drop = null;

        foreach (var cast in _casts.Values)
        {
            cast.LeftNormalSpace();
        }
    }

    private void Interdicted(string interdictor)
    {
        if (interdictor.Length == 0)
        {
            return;
        }

        _interdictors.Add(interdictor);

        foreach (var cast in _casts.Values)
        {
            cast.Interdicted(interdictor);
        }
    }

    /// <summary>
    /// Drops one provider's cast entirely — its pool, its role voices and every assignment made from
    /// it.
    /// </summary>
    public void Forget(string providerId) => _casts.Remove(providerId);
}
