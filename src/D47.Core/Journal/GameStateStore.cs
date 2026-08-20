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
    /// <summary>
    /// What a Commander had on disk, looked up by Frontier id the first time they are seen. Null
    /// where nothing composed a store, which is every test that is not about persistence.
    /// </summary>
    public Func<string, OrganicSampling?>? Restore { get; init; }

    /// <summary>
    /// This Commander's fleet as their older journals last recorded it, looked up on the same
    /// terms as <see cref="Restore"/>. Null where nothing composed one.
    /// <para>
    /// The fleet is the one bucket the newest journal frequently cannot refold, because
    /// <c>StoredShips</c> is written only on docking at a shipyard and a session may contain no
    /// such docking. See <see cref="FleetBackfill"/>. Applied only when it actually knows
    /// something, so a folder with no snapshot in it leaves the fleet unknown rather than empty —
    /// those are different answers and the surface renders them differently.
    /// </para>
    /// </summary>
    public Func<string, FleetRegistry?>? RestoreFleet { get; init; }

    /// <summary>
    /// What this Commander's ships were last seen holding, on the same terms as
    /// <see cref="RestoreFleet"/>. Null where nothing composed one.
    /// <para>
    /// The newest journal describes the ship being flown and no other, so without this a fleet
    /// recovered by <see cref="RestoreFleet"/> arrives with every slot of every parked ship
    /// unknown — which is the state that was reported. See <see cref="LoadoutBackfill"/>.
    /// </para>
    /// </summary>
    public Func<string, ShipLoadouts?>? RestoreLoadouts { get; init; }

    public void Apply(JournalEvent journalEvent) => Apply(journalEvent, null);

    /// <param name="at">
    /// Where the Commander was standing when this event landed, from <c>Status.json</c>. Null
    /// everywhere except a surface, and null for a backlogged event — see <see cref="JournalSpine"/>
    /// for why a position read now must not be stamped onto an event from two hours ago.
    /// </param>
    public void Apply(JournalEvent journalEvent, SurfaceFix? at)
    {
        if (CommanderIdentity.From(journalEvent) is { } identity)
        {
            if (!_byFrontierId.TryGetValue(identity.FrontierId, out var state))
            {
                state = new CommanderGameState(identity);

                // Anything this Commander had before d47 last stopped. Two buckets need it, and
                // for different reasons: sampling because it is d47's own record and nothing in
                // the journal restates it (list.md Phase 18), and the fleet because the newest
                // journal often does not contain a StoredShips to refold from at all.
                if (Restore?.Invoke(identity.FrontierId) is { } restored)
                {
                    state.Sampling = restored;
                }

                if (RestoreFleet?.Invoke(identity.FrontierId) is { IsKnown: true } fleet)
                {
                    state.Fleet = fleet;
                }

                if (RestoreLoadouts?.Invoke(identity.FrontierId) is { IsKnown: true } loadouts)
                {
                    state.Loadouts = loadouts;
                }

                _byFrontierId[identity.FrontierId] = state;
            }

            _activeFrontierId = identity.FrontierId;
            state.Apply(journalEvent, at);
            return;
        }

        // Every other event belongs to whoever is currently active. Before any identity has
        // been observed there is nowhere for it to go, and it is dropped rather than guessed at.
        Active?.Apply(journalEvent, at);
    }
}
