using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Mission events as scene beats: taken, done and lost, each merging the events of its kind that arrive
/// within <see cref="Window"/> of the first. A failed or abandoned mission is named from the board as it
/// stood before the event; one not on it makes no beat. Tick thread only.
/// </summary>
public sealed class MissionBeats
{
    /// <summary>How long a beat waits for more events of its kind, from the first.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    /// <summary>The most missions one beat names; the rest are counted.</summary>
    public const int MostNamed = 3;

    private readonly Dictionary<SceneBeatKind, Group> _open = [];
    private MissionBoard _board = MissionBoard.Empty;

    /// <summary>Starts from this board with nothing waiting, as after priming.</summary>
    public void Prime(MissionBoard board)
    {
        _board = board ?? MissionBoard.Empty;
        _open.Clear();
    }

    /// <summary>Drops every beat waiting.</summary>
    public void Clear() => _open.Clear();

    /// <summary>Folds this tick's events, then keeps <paramref name="board"/>, which has them applied.</summary>
    public void Fold(DateTimeOffset now, IReadOnlyList<JournalEvent> events, MissionBoard board)
    {
        ArgumentNullException.ThrowIfNull(events);

        // Taken this tick, so a loss in the same tick still finds it.
        var taken = new Dictionary<long, Mission>();

        foreach (var journalEvent in events)
        {
            switch (journalEvent.Kind)
            {
                case "MissionAccepted" when Mission.Of(journalEvent) is { } mission:
                    taken[mission.Id] = mission;
                    Add(SceneBeatKind.MissionTaken, mission, now);
                    break;

                case "MissionCompleted" when Mission.Of(journalEvent) is { } mission:
                    Add(SceneBeatKind.MissionDone, mission, now);
                    break;

                case "MissionFailed" or "MissionAbandoned"
                    when journalEvent.Long("MissionID") is { } id
                        && (taken.GetValueOrDefault(id) ?? _board.For(id)) is { } mission:
                    Add(SceneBeatKind.MissionLost, mission, now);
                    break;
            }
        }

        _board = board ?? MissionBoard.Empty;
    }

    /// <summary>The beats whose window has closed by <paramref name="now"/>, oldest first.</summary>
    public IReadOnlyList<SceneBeat> Due(DateTimeOffset now)
    {
        var due = _open
            .Where(entry => now - entry.Value.Opened >= Window)
            .OrderBy(entry => entry.Value.Opened)
            .ToList();

        foreach (var entry in due)
        {
            _open.Remove(entry.Key);
        }

        return [.. due.Select(entry => Beat(entry.Key, entry.Value.Missions))];
    }

    private void Add(SceneBeatKind kind, Mission mission, DateTimeOffset now)
    {
        if (!_open.TryGetValue(kind, out var group))
        {
            _open[kind] = group = new Group(now);
        }

        group.Missions.Add(mission);
    }

    private static SceneBeat Beat(SceneBeatKind kind, List<Mission> missions) =>
        new(
            kind, 0, null, null, null, null,
            Place: ScenePlace.Mission,
            Missions: [.. missions.Take(MostNamed)],
            MoreMissions: Math.Max(0, missions.Count - MostNamed));

    private sealed record Group(DateTimeOffset Opened)
    {
        public List<Mission> Missions { get; } = [];
    }
}
