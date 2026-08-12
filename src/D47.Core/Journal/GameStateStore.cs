namespace D47.Core.Journal;

/// <summary>
/// State keyed per Commander so a second Commander's journal can never blend into the
/// first one's (list.md Phase 2). Each journal file establishes its own identity near the
/// top, and every event after that is folded into that Commander's own bucket only — there is
/// no path from one bucket's events into another's.
/// </summary>
public sealed class GameStateStore
{
    private readonly Dictionary<string, CommanderGameState> _byFrontierId = new(StringComparer.Ordinal);

    private string? _activeFrontierId;

    /// <summary>The Commander whose journal is currently being tailed, or null before any identity has been seen.</summary>
    public CommanderGameState? Active => _activeFrontierId is { } fid ? _byFrontierId[fid] : null;

    public IReadOnlyCollection<CommanderGameState> All => _byFrontierId.Values;

    /// <summary>
    /// Feeds one event to the active Commander's bucket — creating that bucket first if this
    /// is the event establishing identity for a Commander not seen before.
    /// </summary>
    public void Apply(JournalEvent journalEvent)
    {
        if (CommanderIdentity.From(journalEvent) is { } identity)
        {
            if (!_byFrontierId.TryGetValue(identity.FrontierId, out var state))
            {
                state = new CommanderGameState(identity);
                _byFrontierId[identity.FrontierId] = state;
            }

            _activeFrontierId = identity.FrontierId;
            state.Apply(journalEvent);
            return;
        }

        // Every other event belongs to whoever is currently active. Before any identity has
        // been observed there is nowhere for it to go, and it is dropped rather than guessed at.
        Active?.Apply(journalEvent);
    }
}
