namespace D47.Core.Journal;

/// <summary>
/// The Commander being tailed changed (Phase 44).
/// </summary>
/// <param name="Previous">Who it was, or null when nobody had been identified yet — which makes this an adoption.</param>
/// <param name="Current">Who it is now.</param>
/// <param name="Priming">
/// Whether this came out of the startup replay rather than a login that just happened. A
/// subscriber that discards anything must do nothing when this is set.
/// </param>
public sealed record CommanderSwitch(CommanderIdentity? Previous, CommanderIdentity Current, bool Priming)
{
    /// <summary>
    /// Nobody to somebody. Discards nothing: d47 runs before Elite has said who is flying, and
    /// what happened before that belongs to no Commander rather than retroactively to this one.
    /// </summary>
    public bool IsAdoption => Previous is null;
}

/// <summary>
/// State keyed per Commander so a second Commander's journal can never blend into the
/// first one's (Phase 2). Each journal file establishes its own identity near the
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

    /// <summary>
    /// Every place this Commander has met, on the same terms as <see cref="RestoreLoadouts"/>
    /// (<a href="https://github.com/dseelinger/d47/issues/134">#134</a>). Null where nothing
    /// composed one.
    /// <para>
    /// The live fold only sees this session, and the whole point of the catalogue is depth: a
    /// Commander is far likelier to be misheard about a system they visited last summer than one
    /// they are standing in.
    /// </para>
    /// </summary>
    public Func<string, Listening.SpokenNames?>? RestoreNames { get; init; }

    /// <summary>
    /// Raised when the Commander whose journal is being tailed changes (Phase 44, "One
    /// switch signal"). From nobody to somebody is an adoption; from one to another is a switch.
    /// <para>
    /// <b>The signal carries whether it happened during priming, and every subscriber honours
    /// it.</b> The backlog is folded through the same <see cref="Apply"/> as live events, so a
    /// replay that crosses into a journal belonging to somebody else raises this exactly as a
    /// login would — and a subscriber that discards a transcript on it would be discarding one
    /// for a login that happened last month. Reading the id per call, the way a pure reader does,
    /// is the other correct shape and does not generalise: a transcript cannot be discarded by a
    /// function that reads an id.
    /// </para>
    /// <para>
    /// Raised synchronously from inside the fold, before the identity event itself is applied to
    /// the new bucket, so a subscriber that resets a watch sees the new Commander's events arrive
    /// after the reset rather than before it.
    /// </para>
    /// </summary>
    public event Action<CommanderSwitch>? CommanderChanged;

    /// <summary>
    /// Replaces what every known Commander's ships were last seen holding, from a re-derivation of
    /// the journals (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>).
    /// <para>
    /// <b>Every known Commander, not only the ones the rescan mentions.</b> A Commander whose
    /// ships have all been sold comes back from a rescan with nothing, and skipping them would
    /// leave exactly the stale row the Commander pressed the button to be rid of. Whether the
    /// rescan is worth acting on at all is decided before this is called — it is a repair, and a
    /// repair handed an empty answer by a folder that has moved would be a wipe.
    /// </para>
    /// <para>
    /// A Commander the rescan found and this store has never seen is ignored rather than created:
    /// a bucket exists because their journal established an identity, and inventing one here would
    /// put a Commander in the panel who has not logged in.
    /// </para>
    /// </summary>
    public void ReplaceLoadouts(IReadOnlyDictionary<string, ShipLoadouts> loadouts)
    {
        ArgumentNullException.ThrowIfNull(loadouts);

        foreach (var (fid, state) in _byFrontierId)
        {
            state.Loadouts = loadouts.TryGetValue(fid, out var ships) ? ships : ShipLoadouts.Empty;
        }
    }

    public void Apply(JournalEvent journalEvent) => Apply(journalEvent, null);

    public void Apply(JournalEvent journalEvent, SurfaceFix? at) => Apply(journalEvent, at, priming: false);

    /// <param name="at">
    /// Where the Commander was standing when this event landed, from <c>Status.json</c>. Null
    /// everywhere except a surface, and null for a backlogged event — see <see cref="JournalSpine"/>
    /// for why a position read now must not be stamped onto an event from two hours ago.
    /// </param>
    /// <param name="priming">
    /// Whether this event is part of the startup replay rather than something that just
    /// happened. Carried on <see cref="CommanderChanged"/> and nowhere else: the buckets fold a
    /// backlog and a live event identically.
    /// </param>
    public void Apply(JournalEvent journalEvent, SurfaceFix? at, bool priming)
    {
        if (CommanderIdentity.From(journalEvent) is { } identity)
        {
            var previous = Active?.Identity;

            if (!_byFrontierId.TryGetValue(identity.FrontierId, out var state))
            {
                state = new CommanderGameState(identity);

                // Anything this Commander had before d47 last stopped. Two buckets need it, and
                // for different reasons: sampling because it is d47's own record and nothing in
                // the journal restates it (Phase 18), and the fleet because the newest
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

                if (RestoreNames?.Invoke(identity.FrontierId) is { IsKnown: true } names)
                {
                    state.Names = names;
                }

                _byFrontierId[identity.FrontierId] = state;
            }

            var switched = !string.Equals(previous?.FrontierId, identity.FrontierId, StringComparison.Ordinal);

            _activeFrontierId = identity.FrontierId;

            if (switched)
            {
                CommanderChanged?.Invoke(new CommanderSwitch(previous, identity, priming));
            }

            state.Apply(journalEvent, at);
            return;
        }

        // Every other event belongs to whoever is currently active. Before any identity has
        // been observed there is nowhere for it to go, and it is dropped rather than guessed at.
        Active?.Apply(journalEvent, at);
    }
}
