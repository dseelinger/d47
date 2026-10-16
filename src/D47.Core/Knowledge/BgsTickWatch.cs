using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>
/// Holds the time of the last BGS tick, asking <see cref="IBgsTickSource"/> once when switched on and then
/// every <see cref="Interval"/>, only while the setting is on and Elite is running. Tick thread only.
/// </summary>
public sealed class BgsTickWatch
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    /// <summary>How far after now an answer may be.</summary>
    public static readonly TimeSpan MostAhead = TimeSpan.FromMinutes(5);

    /// <summary>How far before now an answer may be.</summary>
    public static readonly TimeSpan MostBehind = TimeSpan.FromDays(7);

    private readonly IBgsTickSource _source;
    private readonly Func<bool> _enabled;
    private readonly Func<bool> _eliteRunning;
    private readonly Action<string> _remember;
    private readonly ILogger<BgsTickWatch> _logger;

    /// <summary>One entry per finished fetch; null when the source had nothing usable.</summary>
    private readonly ConcurrentQueue<DateTimeOffset?> _answers = new();

    private bool _fetching;
    private bool _wasEnabled;
    private DateTimeOffset? _fetchedAt;

    /// <param name="stored">The time held from the last session, read once here.</param>
    /// <param name="remember">Stores <see cref="Last"/> when it changes.</param>
    public BgsTickWatch(
        IBgsTickSource source,
        Func<bool> enabled,
        Func<bool> eliteRunning,
        Func<string?> stored,
        Action<string> remember,
        ILogger<BgsTickWatch> logger)
    {
        _source = source;
        _enabled = enabled;
        _eliteRunning = eliteRunning;
        _remember = remember;
        _logger = logger;
        Last = Parse(stored());
    }

    /// <summary>Starts the fetch off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    /// <summary>The time of the last BGS tick, or null when none is known.</summary>
    public DateTimeOffset? Last { get; private set; }

    /// <summary>Takes any finished answer, then starts a fetch when one is due.</summary>
    public void Poll(DateTimeOffset now)
    {
        while (_answers.TryDequeue(out var answer))
        {
            _fetching = false;

            if (answer is { } time)
            {
                Accept(time, now);
            }
        }

        var enabled = _enabled();

        if (enabled && !_wasEnabled)
        {
            _fetchedAt = null;
        }

        _wasEnabled = enabled;

        if (!enabled || _fetching || !_eliteRunning())
        {
            return;
        }

        if (_fetchedAt is { } at && now - at < Interval)
        {
            return;
        }

        _fetching = true;
        _fetchedAt = now;
        Dispatch(Fetch);
    }

    /// <summary>The form <see cref="Last"/> is stored in.</summary>
    public static string Format(DateTimeOffset time) =>
        time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private async Task Fetch()
    {
        DateTimeOffset? answer = null;

        try
        {
            answer = await _source.LastTickAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogInformation("BGS tick fetch failed: {Reason}", ex.GetType().Name);
        }
        finally
        {
            _answers.Enqueue(answer);
        }
    }

    private void Accept(DateTimeOffset time, DateTimeOffset now)
    {
        if (time > now + MostAhead)
        {
            _logger.LogInformation("BGS tick {Time:O} ignored: later than now", time);
            return;
        }

        if (time < now - MostBehind)
        {
            _logger.LogInformation("BGS tick {Time:O} ignored: more than seven days ago", time);
            return;
        }

        if (Last is { } last && time < last)
        {
            _logger.LogInformation("BGS tick {Time:O} ignored: earlier than the one held, {Last:O}", time, last);
            return;
        }

        if (time == Last)
        {
            return;
        }

        Last = time;
        _remember(Format(time));
    }

    private static DateTimeOffset? Parse(string? stored) =>
        DateTimeOffset.TryParse(
            stored,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
