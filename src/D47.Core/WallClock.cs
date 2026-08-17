namespace D47.Core;

/// <summary>
/// What time it is, asked for rather than taken.
/// <para>
/// <b>The first thing in Core that answers "now", and it is an interface for that reason.</b>
/// No Core component reads the clock — that is what makes the replay harness possible
/// (architecture.md §8), and it is why the journal reader exposes <c>Poll()</c> instead of
/// ticking and why a gap reaction is handed its elapsed time. Those cases all pushed the clock
/// out to a caller that already had one. A ledger cannot: the instant a row happened is part of
/// the row, and there is no caller further out that knows it better.
/// </para>
/// <para>
/// So the rule is kept the way <see cref="Conversation.ITurnClock"/> keeps it — the dependency
/// is declared and injected, so a test can put the clock wherever it needs it and a replay can
/// run a year of turns in seconds without a single row claiming to have happened today.
/// </para>
/// <para>
/// <b>UTC, and only UTC.</b> Local time is a presentation decision made at the point of asking,
/// against a zone that is also passed in — see <c>SpendWindows</c>. An instant recorded in local
/// wall-clock is wrong twice a year and cannot be repaired afterwards, because the offset that
/// would fix it was never written down.
/// </para>
/// </summary>
public interface IWallClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>The real one. The single place in Core where the current instant is read.</summary>
public sealed class SystemWallClock : IWallClock
{
    public static readonly SystemWallClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
