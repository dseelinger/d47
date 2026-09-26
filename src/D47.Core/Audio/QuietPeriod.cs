namespace D47.Core.Audio;

/// <summary>
/// Decides when a folder that keeps changing has been quiet long enough to rebuild from, and keeps two
/// rebuilds from running at once. Reads no clock: every call takes the time.
/// </summary>
public sealed class QuietPeriod(TimeSpan quiet)
{
    private DateTimeOffset? _lastChange;
    private bool _running;

    public TimeSpan Quiet { get; } = quiet;

    /// <summary>Whether a change has arrived that no rebuild has yet picked up.</summary>
    public bool Pending => _lastChange is not null;

    public void Changed(DateTimeOffset now) => _lastChange = now;

    /// <summary>
    /// True when a rebuild should start now; the caller must call <see cref="Finished"/> when it ends.
    /// </summary>
    public bool Start(DateTimeOffset now)
    {
        if (_running || _lastChange is not { } last || now - last < Quiet)
        {
            return false;
        }

        _lastChange = null;
        _running = true;

        return true;
    }

    public void Finished() => _running = false;

    /// <summary>How long until <see cref="Start"/> can next return true, or null when nothing is waiting.</summary>
    public TimeSpan? Wait(DateTimeOffset now)
    {
        if (_running || _lastChange is not { } last)
        {
            return null;
        }

        var left = last + Quiet - now;

        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }
}
