namespace D47.App.Voice;

/// <summary>One unprompted speaker at a time. Code holding the turn must not take it again.</summary>
internal sealed class SpeakingTurn : IDisposable
{
    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly CancellationTokenSource _closing = new();
    private readonly CancellationToken _closed;

    public SpeakingTurn() => _closed = _closing.Token;

    /// <summary>Runs <paramref name="speak"/> once no other speaker holds the turn; runs nothing once disposed.</summary>
    public async Task TakeAsync(Func<Task> speak)
    {
        try
        {
            await _turn.WaitAsync(_closed).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (!_closed.IsCancellationRequested)
            {
                await speak().ConfigureAwait(false);
            }
        }
        finally
        {
            _turn.Release();
        }
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
    }
}
