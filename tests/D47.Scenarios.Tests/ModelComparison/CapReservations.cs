namespace D47.Scenarios.Tests.ModelComparison;

/// <summary>
/// Holds each running chapter's worst case against the cap. A chapter that would push spend plus reservations past
/// the cap waits for a reservation to be released; one that cannot fit even with nothing reserved is refused.
/// </summary>
public sealed class CapReservations(Func<decimal> spent, decimal cap)
{
    private readonly Lock _gate = new();

    private readonly List<TaskCompletionSource> _waiting = [];

    private decimal _reserved;

    /// <summary>The reservation, or null when the worst case passes the cap with nothing else reserved.</summary>
    public async Task<IDisposable?> ReserveAsync(decimal worstCase, CancellationToken cancellationToken)
    {
        while (true)
        {
            TaskCompletionSource turn;

            lock (_gate)
            {
                if (spent() + _reserved + worstCase <= cap)
                {
                    _reserved += worstCase;
                    return new Reservation(this, worstCase);
                }

                if (_reserved == 0m)
                {
                    return null;
                }

                turn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiting.Add(turn);
            }

            await turn.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void Release(decimal amount)
    {
        List<TaskCompletionSource> woken;

        lock (_gate)
        {
            _reserved -= amount;
            woken = [.. _waiting];
            _waiting.Clear();
        }

        foreach (var turn in woken)
        {
            turn.TrySetResult();
        }
    }

    private sealed class Reservation(CapReservations owner, decimal amount) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                owner.Release(amount);
            }
        }
    }
}
