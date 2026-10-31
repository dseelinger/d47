using D47.Core.Configuration;
using D47.Core.Conversation;

namespace D47.App.Panel;

/// <summary>
/// Keeps each Commander's comms selection in view state, by Frontier ID. Reads the saved selections once, when constructed.
/// A change is written <see cref="Settle"/> after the last one, through <c>post</c>, which must run the write on the UI thread.
/// </summary>
public sealed class CommsSelectionMemory : IDisposable
{
    /// <summary>How long after the last change the write waits.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(1);

    private static readonly CommsMemory Nothing = new();

    private readonly CommsChannels _channels;
    private readonly ViewStateStore _store;
    private readonly Action<Action> _post;
    private readonly ITimer _timer;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, CommsMemory> _saved;
    private readonly Dictionary<string, CommsMemory> _pending = new(StringComparer.Ordinal);
    private string? _commander;
    private int _version;
    private int _written;
    private bool _disposed;

    public CommsSelectionMemory(CommsChannels channels, ViewStateStore store, Action<Action> post, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(time);

        _channels = channels;
        _store = store;
        _post = post;
        _saved = new Dictionary<string, CommsMemory>(store.Load().CommsByCommander, StringComparer.Ordinal);
        _timer = time.CreateTimer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        channels.Changed += OnChanged;
    }

    /// <summary>
    /// Restores the selection saved for <paramref name="frontierId"/>, or the launch state when none was, if it is not the
    /// Commander last followed. Call before <see cref="CommsChannels.Update"/>.
    /// </summary>
    public void Follow(string? frontierId)
    {
        lock (_lock)
        {
            if (_disposed || string.Equals(frontierId, _commander, StringComparison.Ordinal))
            {
                return;
            }

            _commander = frontierId;
            _channels.Restore(frontierId is { Length: > 0 } ? Last(frontierId) : null);
        }
    }

    /// <summary>Writes a change still waiting, on the calling thread, and stops following the channels.</summary>
    public void Dispose()
    {
        int version;
        Dictionary<string, CommsMemory> all;

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _channels.Changed -= OnChanged;
            _timer.Dispose();

            (version, all) = TakePending();
        }

        Write(version, all);
    }

    private void OnChanged()
    {
        lock (_lock)
        {
            if (_disposed || _commander is not { Length: > 0 } commander)
            {
                return;
            }

            var memory = _channels.Memory;

            if (memory.SameAs(Last(commander) ?? Nothing))
            {
                return;
            }

            _pending[commander] = memory;
            _timer.Change(Settle, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        int version;
        Dictionary<string, CommsMemory> all;

        lock (_lock)
        {
            if (_disposed || _pending.Count == 0)
            {
                return;
            }

            (version, all) = TakePending();
        }

        _post(() => Write(version, all));
    }

    private (int Version, Dictionary<string, CommsMemory> All) TakePending()
    {
        if (_pending.Count > 0)
        {
            foreach (var (commander, memory) in _pending)
            {
                _saved[commander] = memory;
            }

            _pending.Clear();
            _version++;
        }

        return (_version, new Dictionary<string, CommsMemory>(_saved, StringComparer.Ordinal));
    }

    /// <summary>Saves <paramref name="all"/> unless a later version has been written already.</summary>
    private void Write(int version, Dictionary<string, CommsMemory> all)
    {
        lock (_lock)
        {
            if (version <= _written)
            {
                return;
            }

            _written = version;
        }

        _store.Save(_store.Load() with { CommsByCommander = all });
    }

    private CommsMemory? Last(string commander) =>
        _pending.GetValueOrDefault(commander) ?? _saved.GetValueOrDefault(commander);
}
