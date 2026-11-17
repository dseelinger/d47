namespace D47.App.Voice;

/// <summary>Places in a queue: each place is ready once every place taken before it has been disposed.</summary>
internal sealed class CalloutOrder : IDisposable
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _closing = new();
    private readonly CancellationToken _closed;
    private Task _tail = Task.CompletedTask;
    private int _disposed;

    public CalloutOrder() => _closed = _closing.Token;

    /// <summary>The next place in the order. Thread-safe, and does not block.</summary>
    public Place Take()
    {
        lock (_gate)
        {
            var place = new Place(_tail, _closed);
            _tail = place.Finished;
            return place;
        }
    }

    /// <summary>Cancels the <see cref="Place.Ready"/> of every place not yet ready, and of every place taken afterwards.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _closing.Cancel();
            _closing.Dispose();
        }
    }

    /// <summary>One holder's place in the order. Disposed once its holder has spoken, or will not speak.</summary>
    internal sealed class Place : IDisposable
    {
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Place(Task earlier, CancellationToken closed)
        {
            Ready = earlier.WaitAsync(closed);
            Finished = AfterBothAsync(earlier, _released.Task);
        }

        /// <summary>Completes once every earlier place is disposed; cancelled when the order is disposed.</summary>
        public Task Ready { get; }

        /// <summary>Completes once this place and every earlier one are disposed.</summary>
        internal Task Finished { get; }

        /// <summary>Releases the next place.</summary>
        public void Dispose() => _released.TrySetResult();

        private static async Task AfterBothAsync(Task earlier, Task released)
        {
            await earlier.ConfigureAwait(false);
            await released.ConfigureAwait(false);
        }
    }
}
