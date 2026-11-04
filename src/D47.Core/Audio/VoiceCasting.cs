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

    /// <summary>Held over the casts and the scope state: the tick changes scopes while speech asks for a cast.</summary>
    private readonly Lock _gate = new();

    private DateTimeOffset? _arrival;

    private DateTimeOffset? _drop;

    private string? _system;

    /// <summary>The system NPC voices are drawn for, or null before one is known.</summary>
    public string? System
    {
        get
        {
            lock (_gate)
            {
                return _system;
            }
        }
    }

    /// <summary>The cast for one provider, made on first use in the scopes already open.</summary>
    public VoiceCast Of(string providerId)
    {
        lock (_gate)
        {
            if (!_casts.TryGetValue(providerId, out var cast))
            {
                cast = new VoiceCast();
                cast.EnteredSystem(_system, _arrival);

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
    }

    /// <summary>Records the system without clearing anyone's voice, for the first one seen.</summary>
    public void InSystem(string? system)
    {
        lock (_gate)
        {
            _system = system;

            foreach (var cast in _casts.Values)
            {
                cast.System = system;
            }
        }
    }

    /// <summary>A new system, for all of them, arrived in at journal time <paramref name="at"/> where it is known.</summary>
    public void EnteredSystem(string? system = null, DateTimeOffset? at = null)
    {
        lock (_gate)
        {
            Arrived(system, at);
        }
    }

    /// <summary>Opens and closes NPC voice scopes from the Commander's journal events, taken in order.</summary>
    public void Observe(IEnumerable<JournalEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        lock (_gate)
        {
            foreach (var journalEvent in events)
            {
                Observe(journalEvent);
            }
        }
    }

    private void Observe(JournalEvent journalEvent)
    {
        var system = journalEvent.String("StarSystem") ?? _system;

        switch (journalEvent.Kind)
        {
            case "FSDJump":
                Arrived(system, journalEvent.Timestamp);
                break;

            case "CarrierJump":
                Arrived(system, journalEvent.Timestamp);
                Dropped(journalEvent.Timestamp);
                break;

            // An arrival at startup or in another system; a respawn in the same one opens a drop only.
            case "Location":
                if (_arrival is null || !string.Equals(system, _system, StringComparison.Ordinal))
                {
                    Arrived(system, journalEvent.Timestamp);
                }

                Dropped(journalEvent.Timestamp);
                break;

            case "SupercruiseExit":
                Dropped(journalEvent.Timestamp);
                break;

            case "SupercruiseEntry":
                _drop = null;

                foreach (var cast in _casts.Values)
                {
                    cast.LeftNormalSpace();
                }

                break;

            case "Interdicted" or "EscapeInterdiction"
                when !journalEvent.Bool("IsPlayer")
                     && (journalEvent.String("Interdictor_Localised") ?? journalEvent.String("Interdictor")) is { Length: > 0 } named
                     && IncomingMessages.Undecorate(named) is { Length: > 0 } interdictor:
                _interdictors.Add(interdictor);

                foreach (var cast in _casts.Values)
                {
                    cast.Interdicted(interdictor);
                }

                break;
        }
    }

    private void Arrived(string? system, DateTimeOffset? at)
    {
        _system = system;
        _arrival = at;
        _drop = null;

        foreach (var cast in _casts.Values)
        {
            cast.EnteredSystem(system, at);
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

    /// <summary>
    /// Drops one provider's cast entirely — its pool, its role voices and every assignment made from
    /// it.
    /// </summary>
    public void Forget(string providerId)
    {
        lock (_gate)
        {
            _casts.Remove(providerId);
        }
    }
}
