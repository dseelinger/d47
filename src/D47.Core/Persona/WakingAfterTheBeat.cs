namespace D47.Core.Persona;

/// <summary>
/// Cores woken by a beacon scan, each held until the line owed ahead of it has been said: the story beat the same scan
/// reached, told in the voice that was aboard before the scan, or a narrated scan. Not thread-safe: one caller, the tick.
/// </summary>
public sealed class WakingAfterTheBeat
{
    /// <summary>The longest a waking waits on a beat line that is never said.</summary>
    public static readonly TimeSpan Longest = TimeSpan.FromMinutes(3);

    private readonly Queue<(CoreWaking Waking, Persona? Core, string? Chapter, DateTimeOffset At)> _waiting = new();

    /// <summary>
    /// Queues a waking and the core it brings aboard, resolved at the scan, behind the chapter whose beat line is owed,
    /// or behind nothing when <paramref name="chapter"/> is null.
    /// </summary>
    public void Add(CoreWaking waking, Persona? core, string? chapter, DateTimeOffset now) => _waiting.Enqueue((waking, core, chapter, now));

    /// <summary>The wakings due now, oldest first: each whose chapter is no longer owed a line, or that has waited <see cref="Longest"/>.</summary>
    public IReadOnlyList<(CoreWaking Waking, Persona? Core)> Due(Func<string, bool> owed, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(owed);

        var due = new List<(CoreWaking, Persona?)>();

        while (_waiting.TryPeek(out var next)
               && (next.Chapter is null || !owed(next.Chapter) || now - next.At >= Longest))
        {
            _waiting.Dequeue();
            due.Add((next.Waking, next.Core));
        }

        return due;
    }
}
