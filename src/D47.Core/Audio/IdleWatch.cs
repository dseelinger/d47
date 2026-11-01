using System.Collections.Frozen;
using D47.Core.Journal;

namespace D47.Core.Audio;

/// <summary>
/// Whether nothing has happened for <see cref="Threshold"/>. Activity is any journal event but
/// <see cref="Background"/>, or a <see cref="Stir"/>. Time comes only from the caller.
/// </summary>
public sealed class IdleWatch
{
    public static readonly TimeSpan Threshold = TimeSpan.FromMinutes(10);

    /// <summary>Events Elite writes with nobody acting, such as station chatter and crew wages.</summary>
    public static readonly FrozenSet<string> Background = new HashSet<string>(StringComparer.Ordinal)
    {
        "Music",
        "ReceiveText",
        "NpcCrewPaidWage",
        "CommunityGoal",
        "FSSSignalDiscovered",
    }.ToFrozenSet(StringComparer.Ordinal);

    private DateTimeOffset? _since;

    /// <summary>Takes one tick's events. The first call starts the clock.</summary>
    public void Observe(IEnumerable<JournalEvent> events, DateTimeOffset now)
    {
        _since ??= now;

        if (events.Any(journalEvent => !Background.Contains(journalEvent.Kind)))
        {
            _since = now;
        }
    }

    /// <summary>Activity that is not a journal event: a change of folder, a voice turn, a transport key.</summary>
    public void Stir(DateTimeOffset now) => _since = now;

    public bool IsIdle(DateTimeOffset now) => _since is { } since && now - since >= Threshold;

    /// <summary>Forgets the clock, so nothing is idle until the next <see cref="Observe"/>.</summary>
    public void Reset() => _since = null;
}
