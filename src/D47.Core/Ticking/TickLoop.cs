using Microsoft.Extensions.Logging;

namespace D47.Core.Ticking;

/// <summary>What a subscriber is told on each tick.</summary>
/// <param name="Now">The time this tick represents — not necessarily the time it is running.</param>
/// <param name="Since">
/// Elapsed since the previous tick, or <see cref="TimeSpan.Zero"/> on the first.
/// </param>
/// <param name="Number">1 for the first tick.</param>
public readonly record struct TickContext(DateTimeOffset Now, TimeSpan Since, long Number)
{
    public bool IsFirst => Number == 1;
}

/// <summary>The ~4–10 Hz loop, minus the thing that drives it.</summary>
public sealed class TickLoop(ILogger<TickLoop> logger)
{
    /// <summary>The top of the §4 band.</summary>
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromMilliseconds(100);

    private readonly Lock _gate = new();
    private readonly List<Subscriber> _subscribers = [];

    private Subscriber[] _snapshot = [];
    private DateTimeOffset? _previous;
    private long _number;

    public long Count => _number;

    /// <summary>Registers a subscriber.</summary>
    public TickLoop Add(string name, Action<TickContext> onTick)
    {
        lock (_gate)
        {
            _subscribers.Add(new Subscriber(name, onTick));
            _snapshot = [.. _subscribers];
        }

        return this;
    }

    /// <summary>Runs one tick.</summary>
    public void Tick(DateTimeOffset now)
    {
        var since = _previous is { } previous ? now - previous : TimeSpan.Zero;
        _previous = now;

        var context = new TickContext(now, since, ++_number);

        foreach (var subscriber in _snapshot)
        {
            try
            {
                subscriber.OnTick(context);
            }
            catch (Exception ex)
            {
                subscriber.Report(ex, logger);
            }
        }
    }

    /// <summary>
    /// One registered participant, plus the state needed to keep a subscriber that throws every tick
    /// from filling the log.
    /// </summary>
    private sealed class Subscriber(string name, Action<TickContext> onTick)
    {
        private const int ReportEvery = 100;

        private long _failures;

        public Action<TickContext> OnTick { get; } = onTick;

        public void Report(Exception ex, ILogger logger)
        {
            var count = ++_failures;

            // The first one in full, then one in every hundred.
            if (count == 1)
            {
                logger.LogError(ex, "Tick subscriber {Name} threw", name);
            }
            else if (count % ReportEvery == 0)
            {
                logger.LogError(ex, "Tick subscriber {Name} has now thrown {Count} times", name, count);
            }
        }
    }
}
