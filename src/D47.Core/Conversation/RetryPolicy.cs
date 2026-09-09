namespace D47.Core.Conversation;

/// <summary>How the wait grows between attempts (Phase 5).</summary>
public enum BackoffShape
{
    /// <summary>X, 2X, 3X — the wait grows by the base each time.</summary>
    Sequential,

    /// <summary>X, ~1.6X, ~2X — grows, but decelerating.</summary>
    Logarithmic,
}

/// <summary>N tries, X wait, one shape or the other.</summary>
public sealed record RetryPolicy
{
    public static readonly RetryPolicy Default = new();

    /// <summary>Total attempts, not retries. 1 means "try once and do not retry".</summary>
    public int Attempts { get; init; } = 3;

    /// <summary>The base wait.</summary>
    public TimeSpan Wait { get; init; } = TimeSpan.FromSeconds(2);

    public BackoffShape Backoff { get; init; } = BackoffShape.Sequential;

    /// <summary>How long one attempt may run before it counts as failed.</summary>
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
    /// The whole schedule, for logging and for saying how long d47 will keep trying before it gives up
    /// out loud.
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

/// <summary>The two things the turn path needs from a clock.</summary>
public interface ITurnClock
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);

    /// <summary>A token that trips after <paramref name="duration"/>, or when the caller's does.</summary>
    CancellationTokenSource CreateTimeout(TimeSpan duration, CancellationToken linkedTo);
}

/// <summary>The real one.</summary>
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
