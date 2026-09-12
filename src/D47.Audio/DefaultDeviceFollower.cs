using NAudio.CoreAudioApi;

namespace D47.Audio;

/// <summary>
/// Watches one flow's Default Device and marks a move as due once it has settled — Windows fires several
/// notifications in a burst (a USB headset waking, say), and this collapses a burst into one signal (#67).
/// </summary>
internal sealed class DefaultDeviceFollower : IDisposable
{
    /// <summary>How long to wait after the last notification before treating a burst as settled.</summary>
    private static readonly TimeSpan QuietWindow = TimeSpan.FromMilliseconds(500);

    private readonly IAudioEndpointEnumerator _enumerator;
    private readonly DataFlow _flow;
    private readonly Role _role;

    private volatile bool _notified;
    private DateTimeOffset? _dueAt;

    public DefaultDeviceFollower(IAudioEndpointEnumerator enumerator, DataFlow flow, Role role)
    {
        _enumerator = enumerator;
        _flow = flow;
        _role = role;
        _enumerator.DefaultDeviceChanged += OnDefaultDeviceChanged;
    }

    private void OnDefaultDeviceChanged(DataFlow flow, Role role)
    {
        if (flow == _flow && role == _role)
        {
            _notified = true;
        }
    }

    /// <summary>
    /// Whether a move has settled and is due to be acted on. Call once per tick with the tick's own time;
    /// stays true, across ticks, until <see cref="Acknowledge"/> — a caller waiting for the right moment to
    /// act can poll this every tick without losing the request.
    /// </summary>
    public bool Poll(DateTimeOffset now)
    {
        if (_notified)
        {
            _notified = false;
            _dueAt = now + QuietWindow;
        }

        return _dueAt is { } due && now >= due;
    }

    /// <summary>Marks a due move as handled.</summary>
    public void Acknowledge() => _dueAt = null;

    public void Dispose() => _enumerator.DefaultDeviceChanged -= OnDefaultDeviceChanged;
}
