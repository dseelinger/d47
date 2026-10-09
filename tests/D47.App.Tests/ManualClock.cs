using D47.App.Timing;

namespace D47.App.Tests;

/// <summary>A clock and one-shot timers that move only when <see cref="Advance"/> is called.</summary>
internal sealed class ManualClock
{
    private readonly List<Scheduled> _timers = [];

    public DateTimeOffset Now { get; private set; } = DateTimeOffset.UnixEpoch;

    public int Pending => _timers.Count;

    public IDisposable Start(TimeSpan delay, Action fire)
    {
        var timer = new Scheduled(Now + delay, fire, this);
        _timers.Add(timer);
        return timer;
    }

    public StartOneShot Starter => Start;

    /// <summary>Moves time on, firing each timer that falls due in order.</summary>
    public void Advance(TimeSpan by)
    {
        var target = Now + by;

        while (_timers.OrderBy(timer => timer.Due).FirstOrDefault(timer => timer.Due <= target) is { } next)
        {
            _timers.Remove(next);
            Now = next.Due;
            next.Fire();
        }

        Now = target;
    }

    /// <summary>Replaces <see cref="OneShot.Dispatcher"/> until disposed.</summary>
    public IDisposable UseForDispatcher()
    {
        var previous = OneShot.Dispatcher;
        OneShot.Dispatcher = Start;
        return new Restore(previous);
    }

    private sealed class Scheduled(DateTimeOffset due, Action fire, ManualClock owner) : IDisposable
    {
        public DateTimeOffset Due { get; } = due;

        public Action Fire { get; } = fire;

        public void Dispose() => owner._timers.Remove(this);
    }

    private sealed class Restore(StartOneShot previous) : IDisposable
    {
        public void Dispose() => OneShot.Dispatcher = previous;
    }
}
