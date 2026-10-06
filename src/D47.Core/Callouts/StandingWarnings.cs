namespace D47.Core.Callouts;

/// <summary>Carrier warnings that have fired, and the Commander's answer to each, per Commander in memory (#837).</summary>
public sealed class StandingWarnings
{
    private enum Hold
    {
        Unanswered,
        Noted,
        NextTime,
        NextSession,
    }

    private sealed record Entry(string Label, DateTimeOffset FiredAt, Hold Hold);

    private readonly object _gate = new();

    private readonly Dictionary<(string Commander, string Key), Entry> _entries = [];

    /// <summary>Records that a warning has just been spoken and waits for an answer.</summary>
    public void Fired(string commander, string key, string label, DateTimeOffset at)
    {
        lock (_gate)
        {
            _entries[(commander, key)] = new Entry(label, at, Hold.Unanswered);
        }
    }

    /// <summary>True while the Commander has answered "noted" or "next session" and the warning has not come back.</summary>
    public bool Silenced(string commander, string key)
    {
        lock (_gate)
        {
            return _entries.TryGetValue((commander, key), out var entry) && entry.Hold is Hold.Noted or Hold.NextSession;
        }
    }

    /// <summary>True when the Commander asked to hear the warning at the next moment, even on the same reading.</summary>
    public bool Repeats(string commander, string key)
    {
        lock (_gate)
        {
            return _entries.TryGetValue((commander, key), out var entry) && entry.Hold == Hold.NextTime;
        }
    }

    /// <summary>The warning's condition no longer holds; forgets everything but a hold until the next session.</summary>
    public void Cleared(string commander, string key)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue((commander, key), out var entry) && entry.Hold != Hold.NextSession)
            {
                _entries.Remove((commander, key));
            }
        }
    }

    /// <summary>A new session has started: warnings held until then speak again.</summary>
    public void NewSession()
    {
        lock (_gate)
        {
            foreach (var pair in _entries.Where(pair => pair.Value.Hold == Hold.NextSession).ToList())
            {
                _entries.Remove(pair.Key);
            }
        }
    }

    /// <summary>The Commander's most recent warning that fired and has no answer, or null.</summary>
    public (string Label, DateTimeOffset At)? LastUnanswered(string commander)
    {
        lock (_gate)
        {
            return Latest(commander) is { } found ? (found.Value.Label, found.Value.FiredAt) : null;
        }
    }

    /// <summary>Answers the most recent unanswered warning with "noted"; false when none is waiting.</summary>
    public bool Acknowledge(string commander) => Answer(commander, Hold.Noted) is not null;

    /// <summary>Answers it with a snooze, to the next moment or the next session; its label, or null when none is waiting.</summary>
    public string? Snooze(string commander, bool nextSession) =>
        Answer(commander, nextSession ? Hold.NextSession : Hold.NextTime);

    private string? Answer(string commander, Hold hold)
    {
        lock (_gate)
        {
            if (Latest(commander) is not { } found)
            {
                return null;
            }

            _entries[found.Key] = found.Value with { Hold = hold };
            return found.Value.Label;
        }
    }

    private KeyValuePair<(string Commander, string Key), Entry>? Latest(string commander) =>
        _entries
            .Where(pair => pair.Key.Commander == commander && pair.Value.Hold == Hold.Unanswered)
            .OrderBy(pair => pair.Value.FiredAt)
            .Select(pair => (KeyValuePair<(string Commander, string Key), Entry>?)pair)
            .LastOrDefault();
}
