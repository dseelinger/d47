using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Persona;

/// <summary>When each core last left the ship, and what the ship's ledger looked like then.</summary>
public sealed class CoreAbsences
{
    private readonly ViewStateStore _store;
    private readonly Func<DateTimeOffset> _now;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (DateTimeOffset At, SessionSummary? Session)> _left = new(StringComparer.Ordinal);
    private bool _gameRunning = true;

    public CoreAbsences(ViewStateStore store, Func<DateTimeOffset> now, ILogger logger)
    {
        _store = store;
        _now = now;
        _logger = logger;

        foreach (var (core, at) in store.Load().CoresLastAboard)
        {
            _left[core] = (at, null);
        }
    }

    /// <summary>Records the outgoing core departing now, unless the game has already closed under it.</summary>
    public void Leaving(string core, SessionSummary session)
    {
        lock (_gate)
        {
            var at = !_gameRunning && _left.TryGetValue(core, out var shut) ? shut.At : _now();
            _left[core] = (at, session);
        }
    }

    /// <summary>How long the core has been gone and what changed aboard since, or nulls if it never left.</summary>
    public (TimeSpan? Away, string? Delta) Returning(string core, CommanderGameState? active)
    {
        lock (_gate)
        {
            if (!_left.TryGetValue(core, out var seen))
            {
                return (null, null);
            }

            var delta = seen.Session is { } session
                ? TelemetryDelta.Between(session, active?.Session, active)
                : null;

            return (_now() - seen.At, delta);
        }
    }

    /// <summary>Writes the outgoing core's departure where the next run can read it.</summary>
    public void Switched(string outgoing)
    {
        lock (_gate)
        {
            Persist(outgoing);
        }
    }

    /// <summary>A Shutdown is the core aboard leaving, recorded outside priming; a later LoadGame puts it back to work.</summary>
    public void Observe(IEnumerable<JournalEvent> events, bool priming, string coreAboard)
    {
        lock (_gate)
        {
            foreach (var journalEvent in events)
            {
                if (journalEvent.Kind == "Shutdown")
                {
                    _gameRunning = false;

                    if (!priming)
                    {
                        Record(coreAboard, journalEvent.Timestamp);
                    }
                }
                else if (journalEvent.Kind == "LoadGame")
                {
                    _gameRunning = true;
                }
            }
        }
    }

    /// <summary>Records the core aboard leaving now, unless the game closed under it first.</summary>
    public void Exiting(string coreAboard)
    {
        lock (_gate)
        {
            if (_gameRunning)
            {
                Record(coreAboard, _now());
            }
        }
    }

    private void Record(string core, DateTimeOffset at)
    {
        _left[core] = (at, _left.TryGetValue(core, out var last) ? last.Session : null);
        Persist(core);
    }

    private void Persist(string core)
    {
        if (!_left.TryGetValue(core, out var left))
        {
            return;
        }

        try
        {
            var state = _store.Load();

            _store.Save(state with
            {
                CoresLastAboard = new Dictionary<string, DateTimeOffset>(state.CoresLastAboard, StringComparer.Ordinal)
                {
                    [core] = left.At,
                },
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Could not record when {Core} was last aboard", core);
        }
    }
}
