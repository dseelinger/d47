using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>The timing shared by the callouts that speak unprompted: situation tracking, the first-tick hold and the spread wait.</summary>
internal sealed class UnpromptedPace(int offset)
{
    private AmbientSituation _situation = AmbientSituation.None;
    private DateTimeOffset _situationSince;
    private DateTimeOffset _lastSpokenAt;

    /// <summary>Lines spoken so far.</summary>
    public int Picks { get; private set; }

    /// <summary>Records a change of situation.</summary>
    public void Watch(AmbientSituation situation, DateTimeOffset now)
    {
        if (situation != _situation)
        {
            _situation = situation;
            _situationSince = now;
        }
    }

    /// <summary>False while nothing has been spoken, and records the time as if it had; then true once the situation has settled and the wait has passed.</summary>
    public bool Due(DateTimeOffset now, TimeSpan interval, TimeSpan longest, TimeSpan settle)
    {
        if (_lastSpokenAt == default)
        {
            _lastSpokenAt = now;
            return false;
        }

        return now - _situationSince >= settle && now - _lastSpokenAt >= Gap(interval, longest);
    }

    /// <summary>Counts a pick and records the time.</summary>
    public void Spoke(DateTimeOffset now)
    {
        Picks++;
        _lastSpokenAt = now;
    }

    private TimeSpan Gap(TimeSpan interval, TimeSpan longest)
    {
        if (longest <= interval)
        {
            return interval;
        }

        var fraction = unchecked((uint)(Picks + offset) * 2654435761u) / 4294967296.0;

        return interval + (longest - interval) * fraction;
    }
}
