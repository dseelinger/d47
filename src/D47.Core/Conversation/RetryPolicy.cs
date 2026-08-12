namespace D47.Core.Conversation;

/// <summary>How the wait grows between attempts (list.md Phase 5).</summary>
public enum BackoffShape
{
    /// <summary>X, 2X, 3X — the wait grows by the base each time.</summary>
    Sequential,

    /// <summary>
    /// X, ~1.6X, ~2X — grows, but decelerating. Worth having as the alternative because the
    /// failure this is retrying through is usually a busy provider rather than a rate limit,
    /// and a decelerating curve gets a Commander an answer sooner than doubling does.
    /// </summary>
    Logarithmic,
}

/// <summary>
/// N tries, X wait, one shape or the other. Pure arithmetic with no clock in it, so the
/// schedule is assertable without anything actually waiting (list.md Phase 5, architecture.md §8).
/// </summary>
public sealed record RetryPolicy
{
    public static readonly RetryPolicy Default = new();

    /// <summary>Total attempts, not retries. 1 means "try once and do not retry".</summary>
    public int Attempts { get; init; } = 3;

    /// <summary>The base wait. The first retry waits exactly this long under either shape.</summary>
    public TimeSpan Wait { get; init; } = TimeSpan.FromSeconds(2);

    public BackoffShape Backoff { get; init; } = BackoffShape.Sequential;

    /// <summary>
    /// How long one attempt may run before it counts as failed. Per attempt rather than for
    /// the turn as a whole, so a slow first try still leaves the retries their full budget.
    /// </summary>
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>The wait before attempt number <paramref name="attempt"/>, counting from 1.</summary>
    public TimeSpan WaitBefore(int attempt)
    {
        if (attempt <= 1)
        {
            return TimeSpan.Zero;
        }

        var retry = attempt - 1;

        var multiplier = Backoff switch
        {
            BackoffShape.Sequential => retry,
            BackoffShape.Logarithmic => Math.Log2(retry + 1),
            _ => retry,
        };

        return Wait * multiplier;
    }

    /// <summary>
    /// The whole schedule, for logging and for saying how long d47 will keep trying before it
    /// gives up out loud.
    /// </summary>
    public TimeSpan TotalWait()
    {
        var total = TimeSpan.Zero;

        for (var attempt = 2; attempt <= Attempts; attempt++)
        {
            total += WaitBefore(attempt);
        }

        return total;
    }
}

/// <summary>
/// The two things the turn path needs from a clock. Injected rather than called directly so a
/// test can assert the retry schedule without spending it (architecture.md §8) — the reason
/// no Core component reads the clock in the first place.
/// </summary>
public interface ITurnClock
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);

    /// <summary>A token that trips after <paramref name="duration"/>, or when the caller's does.</summary>
    CancellationTokenSource CreateTimeout(TimeSpan duration, CancellationToken linkedTo);
}

/// <summary>The real one. The single place in Core where wall time is consulted.</summary>
public sealed class SystemTurnClock : ITurnClock
{
    public static readonly SystemTurnClock Instance = new();

    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration, cancellationToken);

    public CancellationTokenSource CreateTimeout(TimeSpan duration, CancellationToken linkedTo)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(linkedTo);
        source.CancelAfter(duration);
        return source;
    }
}
