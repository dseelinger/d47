namespace D47.Tts;

/// <summary>
/// Counts the runs using a native resource and calls <c>free</c> once, after disposal and the last run's end.
/// Not thread-safe: callers hold one lock around every member.
/// </summary>
internal sealed class RunsInFlight(Action free)
{
    private int _running;
    private bool _disposed;
    private bool _freed;

    /// <summary>False once disposed; otherwise the run is counted and must be ended.</summary>
    public bool TryBegin()
    {
        if (_disposed)
        {
            return false;
        }

        _running++;
        return true;
    }

    public void End()
    {
        _running--;
        FreeIfIdle();
    }

    public void Dispose()
    {
        _disposed = true;
        FreeIfIdle();
    }

    private void FreeIfIdle()
    {
        if (_disposed && _running == 0 && !_freed)
        {
            _freed = true;
            free();
        }
    }
}
