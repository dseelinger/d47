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

    /// <summary>
    /// The subscribers repeated failure has paused, in registration order — each of them a feature that
    /// is no longer running.
    /// </summary>
    public IReadOnlyList<string> Paused =>
        [.. _snapshot.Where(subscriber => subscriber.IsPaused).Select(subscriber => subscriber.Name)];

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
            if (!subscriber.Due(now))
            {
                continue;
            }

            try
            {
                subscriber.OnTick(context);
                subscriber.Succeeded(logger);
            }
            catch (Exception ex)
            {
                subscriber.Failed(ex, now, logger);
            }
        }
    }

    /// <summary>
    /// One registered participant, plus the state that stops a subscriber which throws on every tick
    /// from being called, and logged, at the tick rate for the life of the process.
    /// </summary>
    private sealed class Subscriber(string name, Action<TickContext> onTick)
    {
        private const int ReportEvery = 100;

        /// <summary>Consecutive failures that pause the subscriber. Ten is a second of failing.</summary>
        private const int PauseAfter = 10;

        private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(1);

        private long _failures;
        private int _consecutive;
        private DateTimeOffset _retryAt;

        // Written on the tick thread and read on whichever thread asks the loop what is paused — the panel's
        // and the tool caller's.
        private volatile bool _paused;

        public string Name { get; } = name;

        public Action<TickContext> OnTick { get; } = onTick;

        public bool IsPaused => _paused;

        /// <summary>Whether this tick runs it: always, unless paused and not yet due to be retried.</summary>
        public bool Due(DateTimeOffset now) => !IsPaused || now >= _retryAt;

        public void Succeeded(ILogger logger)
        {
            if (IsPaused)
            {
                _paused = false;
                logger.LogInformation("Tick subscriber {Name} succeeded on a retry and is running again", Name);
            }

            _consecutive = 0;
        }

        public void Failed(Exception ex, DateTimeOffset now, ILogger logger)
        {
            var count = ++_failures;
            _consecutive++;

            if (IsPaused)
            {
                // A retry that failed: wait another minute.
                _retryAt = now + RetryAfter;
            }

            // The first one in full, then one in every hundred. The retries are counted too, which keeps a
            // subscriber that fails every retry to about one line an hour rather than to none at all.
            if (count == 1)
            {
                logger.LogError(ex, "Tick subscriber {Name} threw", Name);
            }
            else if (count % ReportEvery == 0)
            {
                logger.LogError(ex, "Tick subscriber {Name} has now thrown {Count} times", Name, count);
            }

            if (!IsPaused && _consecutive >= PauseAfter)
            {
                _paused = true;
                _retryAt = now + RetryAfter;

                logger.LogWarning(
                    "Tick subscriber {Name} threw {Count} times running and is paused; it will be retried "
                    + "every {Seconds} seconds until it succeeds",
                    Name,
                    _consecutive,
                    RetryAfter.TotalSeconds);
            }
        }
    }
}
