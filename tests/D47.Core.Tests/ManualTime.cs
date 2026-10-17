namespace D47.Core.Tests;

/// <summary>A <see cref="TimeProvider"/> that moves only when <see cref="Advance"/> is called, firing due timers on the caller's thread.</summary>
internal sealed class ManualTime : TimeProvider
{
    private readonly Lock _gate = new();

    private readonly List<Scheduled> _timers = [];

    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => GetUtcNow().UtcTicks;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Scheduled(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Moves time on, firing each timer that falls due in order.</summary>
    public void Advance(TimeSpan by)
    {
        var target = GetUtcNow() + by;

        while (true)
        {
            Scheduled? next;

            lock (_gate)
            {
                next = _timers.Where(timer => timer.Due <= target).OrderBy(timer => timer.Due).FirstOrDefault();

                if (next is null)
                {
                    _now = target;
                    return;
                }

                _now = next.Due!.Value;
                next.Due = next.Period > TimeSpan.Zero ? _now + next.Period : null;
            }

            next.Fire();
        }
    }

    private sealed class Scheduled(ManualTime owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? Due { get; set; }

        public TimeSpan Period { get; private set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
                Period = period == Timeout.InfiniteTimeSpan ? TimeSpan.Zero : period;

                if (!owner._timers.Contains(this))
                {
                    owner._timers.Add(this);
                }
            }

            return true;
        }

        public void Dispose()
        {
            lock (owner._gate)
            {
                owner._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
