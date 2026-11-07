namespace D47.Core.Modules;

/// <summary>Decides whether a module that exited is started again. Not thread-safe.</summary>
public sealed class ModuleRestarts(IReadOnlyList<TimeSpan> waits)
{
    /// <summary>Exits further apart than this do not count together.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public static readonly IReadOnlyList<TimeSpan> Waits =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    private readonly List<DateTimeOffset> _exits = [];

    /// <summary>Records an exit at <paramref name="at"/>: the wait before starting the module again, or null when it stays stopped.</summary>
    public TimeSpan? Exited(DateTimeOffset at)
    {
        _exits.RemoveAll(exit => at - exit >= Window);
        _exits.Add(at);

        return _exits.Count <= waits.Count ? waits[_exits.Count - 1] : null;
    }

    /// <summary>Forgets every exit, as RESTART does.</summary>
    public void Clear() => _exits.Clear();
}
