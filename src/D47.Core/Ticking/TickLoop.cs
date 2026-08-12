using Microsoft.Extensions.Logging;

namespace D47.Core.Ticking;

/// <summary>
/// What a subscriber is told on each tick. <see cref="Now"/> is supplied by the caller rather
/// than read from the clock, which is the property that lets the replay harness run this at
/// 100x with times drawn from a journal fixture instead of from the machine (architecture.md §8).
/// </summary>
/// <param name="Now">The time this tick represents — not necessarily the time it is running.</param>
/// <param name="Since">Elapsed since the previous tick, or <see cref="TimeSpan.Zero"/> on the first.</param>
/// <param name="Number">1 for the first tick. Lets a subscriber recognise startup without a flag of its own.</param>
public readonly record struct TickContext(DateTimeOffset Now, TimeSpan Since, long Number)
{
    public bool IsFirst => Number == 1;
}

/// <summary>
/// The ~4–10 Hz loop from architecture.md §4, minus the thing that drives it. Subscribers are
/// called in registration order, and the caller supplies the time.
/// <para>
/// It owns no thread and reads no clock, for the same reason <see cref="Journal.JournalReader"/>
/// does not: that is the single decision that makes journal-driven behaviour testable without
/// the game. The production driver lives in the app and owns a timer; a test calls
/// <see cref="Tick"/> in a tight loop and gets identical results.
/// </para>
/// <para>
/// <b>A tick is synchronous and must not block.</b> Every subscriber runs on the driver's one
/// thread, so a subscriber that waits on the network stalls push-to-talk edge detection and
/// every callout behind it. Work that has to await — synthesising a callout, running a turn —
/// belongs on the thread pool, handed off through a queue the subscriber writes to.
/// </para>
/// </summary>
public sealed class TickLoop(ILogger<TickLoop> logger)
{
    /// <summary>
    /// The top of the §4 band. Push-to-talk sets this floor: the gate is polled here, so the
    /// period is the worst-case delay before a key-down is seen. Everything else in the loop
    /// would be happy at 4 Hz.
    /// </summary>
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromMilliseconds(100);

    private readonly Lock _gate = new();
    private readonly List<Subscriber> _subscribers = [];

    private Subscriber[] _snapshot = [];
    private DateTimeOffset? _previous;
    private long _number;

    public long Count => _number;

    /// <summary>
    /// Registers a subscriber. Order is registration order and it is load-bearing: the journal
    /// is polled before anything that reads game state, so a callout sees the events from this
    /// tick rather than from the last one.
    /// </summary>
    public TickLoop Add(string name, Action<TickContext> onTick)
    {
        lock (_gate)
        {
            _subscribers.Add(new Subscriber(name, onTick));
            _snapshot = [.. _subscribers];
        }

        return this;
    }

    /// <summary>
    /// Runs one tick. A subscriber that throws is logged and skipped, and the subscribers after
    /// it still run — one broken callout must not silence the rest of the loop, and it must
    /// certainly not take push-to-talk down with it.
    /// </summary>
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
    /// One registered participant, plus the state needed to keep a subscriber that throws every
    /// tick from filling the log. At 10 Hz an unthrottled failure writes 36,000 lines an hour
    /// and buries whatever else went wrong.
    /// </summary>
    private sealed class Subscriber(string name, Action<TickContext> onTick)
    {
        private const int ReportEvery = 100;

        private long _failures;

        public Action<TickContext> OnTick { get; } = onTick;

        public void Report(Exception ex, ILogger logger)
        {
            var count = ++_failures;

            // The first one in full, then one in every hundred. A subscriber that fails once is
            // the interesting case; one that fails always is one line either way.
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
