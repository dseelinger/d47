namespace D47.App;

/// <summary>Decides when the memory store is checked for expiry, and whether a pass is the first since launch.</summary>
internal sealed class MemoryExpiryPacing(TimeSpan every)
{
    private DateTimeOffset _lastPass = DateTimeOffset.MinValue;
    private bool _passed;

    /// <summary>True when a pass is due now; <paramref name="first"/> is true for the first pass since launch.</summary>
    public bool TryBegin(DateTimeOffset now, out bool first)
    {
        first = !_passed;

        if (now - _lastPass < every)
        {
            return false;
        }

        _lastPass = now;
        _passed = true;
        return true;
    }
}
