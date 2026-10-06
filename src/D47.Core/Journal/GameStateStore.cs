namespace D47.Core.Journal;

/// <summary>What moved the shown Commander to another.</summary>
public enum CommanderSwitchCause
{
    /// <summary>A <c>Commander</c> or <c>LoadGame</c> in the journal, before any pick.</summary>
    Journal,

    /// <summary><see cref="GameStateStore.Pick"/>.</summary>
    Picked,
}

/// <summary>The Commander d47 shows changed (Phase 44).</summary>
/// <param name="Previous">
/// Who it was, or null when nobody had been identified yet — which makes this an adoption.
/// </param>
/// <param name="Current">Who it is now.</param>
/// <param name="Priming">
/// Whether this came out of the startup replay rather than a login that just happened.
/// </param>
public sealed record CommanderSwitch(
    CommanderIdentity? Previous,
    CommanderIdentity Current,
    bool Priming,
    CommanderSwitchCause Cause = CommanderSwitchCause.Journal)
{
    /// <summary>Nobody to somebody.</summary>
    public bool IsAdoption => Previous is null;
}

/// <summary>
/// State keyed per Commander so a second Commander's journal can never blend into the first one's
/// (Phase 2).
/// </summary>
public sealed class GameStateStore
{
    private readonly Dictionary<string, CommanderGameState> _byFrontierId = new(StringComparer.Ordinal);

    private string? _activeFrontierId;

    private string? _inGameFrontierId;

    private bool _picked;

    /// <summary>
    /// The Commander d47 shows, or null before any identity has been seen. Follows the journal until
    /// the first <see cref="Pick"/>, and only a later pick changes it after that.
    /// </summary>
    public CommanderGameState? Active => _activeFrontierId is { } fid ? _byFrontierId[fid] : null;

    /// <summary>
    /// The Commander the journal is writing for, or null before any identity has been seen. Every
    /// event that is not an identity folds into this one.
    /// </summary>
    public CommanderGameState? InGame => _inGameFrontierId is { } fid ? _byFrontierId[fid] : null;

    /// <summary>
    /// The shown Commander is not the one in the game, including a pick made before the journal has
    /// named anybody.
    /// </summary>
    public bool IsOffDuty =>
        _activeFrontierId is not null && !string.Equals(_activeFrontierId, _inGameFrontierId, StringComparison.Ordinal);

    public IReadOnlyCollection<CommanderGameState> All => _byFrontierId.Values;

    /// <summary>A tick's events as reactions to the shown Commander take them: none while off duty.</summary>
    public IReadOnlyList<JournalEvent> Shown(IReadOnlyList<JournalEvent> events) => IsOffDuty ? [] : events;

    /// <summary>
    /// Feeds one event to the active Commander's bucket — creating that bucket first if this is the
    /// event establishing identity for a Commander not seen before.
    /// </summary>
    public Func<string, OrganicSampling?>? Restore { get; init; }

    /// <summary>
    /// This Commander's fleet as their older journals last recorded it, looked up on the same terms as
    /// <see cref="Restore"/>.
    /// </summary>
    public Func<string, FleetRegistry?>? RestoreFleet { get; init; }

    /// <summary>
    /// What this Commander's ships were last seen holding, on the same terms as <see
    /// cref="RestoreFleet"/>.
    /// </summary>
    public Func<string, ShipLoadouts?>? RestoreLoadouts { get; init; }

    /// <summary>
    /// Every suit and hand weapon this Commander owns, as it was last saved, on the same terms as
    /// <see cref="RestoreLoadouts"/> (#293).
    /// </summary>
    public Func<string, OwnedKit?>? RestoreKit { get; init; }

    /// <summary>
    /// Where this Commander's fleet carrier was when their journals last said, on the same terms as
    /// <see cref="RestoreFleet"/> (#406).
    /// </summary>
    public Func<string, CarrierState?>? RestoreCarrier { get; init; }

    /// <summary>
    /// This Commander's live missions with the detail their older journals hold, on the same terms as
    /// <see cref="RestoreFleet"/>.
    /// </summary>
    public Func<string, MissionBoard?>? RestoreMissions { get; init; }

    /// <summary>
    /// The construction sites this Commander's older journals reported, on the same terms as <see
    /// cref="RestoreFleet"/>. Merged per site, the later one kept (#798).
    /// </summary>
    public Func<string, ColonisationSites?>? RestoreColonisation { get; init; }

    /// <summary>
    /// Every place this Commander has met, on the same terms as <see cref="RestoreLoadouts"/> (#134).
    /// </summary>
    public Func<string, Listening.SpokenNames?>? RestoreNames { get; init; }

    /// <summary>
    /// This Commander's faction readings and engineer contributions from older journals, on the same terms as
    /// <see cref="RestoreNames"/>. Merged per reading, the later one kept.
    /// </summary>
    public Func<string, UnlockEvidence?>? RestoreEvidence { get; init; }

    /// <summary>
    /// The merits this Commander earned for their Power over the last week, on the same terms as <see
    /// cref="RestoreFleet"/>.
    /// </summary>
    public Func<string, PowerplayCycleMerits?>? RestoreCycleMerits { get; init; }

    /// <summary>
    /// Raised when <see cref="Active"/> changes (Phase 44, "One switch signal").
    /// </summary>
    public event Action<CommanderSwitch>? CommanderChanged;

    /// <summary>
    /// Raised when the system the active Commander is in changes, including the switch to a Commander
    /// standing somewhere else (#93).
    /// </summary>
    public event Action? SystemChanged;

    /// <summary>
    /// Replaces what every known Commander's ships were last seen holding, from a re-derivation of the
    /// journals (#128).
    /// </summary>
    public void ReplaceLoadouts(IReadOnlyDictionary<string, ShipLoadouts> loadouts)
    {
        ArgumentNullException.ThrowIfNull(loadouts);

        foreach (var (fid, state) in _byFrontierId)
        {
            state.Loadouts = loadouts.TryGetValue(fid, out var ships) ? ships with { IsWhole = true } : ShipLoadouts.NoShips;
        }
    }

    /// <summary>
    /// Offers the restore hooks again to every Commander already met, for a walk over older journals that
    /// finished after they were (#148). What the live journal has folded in the meantime wins.
    /// </summary>
    public void RestoreLate()
    {
        foreach (var (fid, state) in _byFrontierId)
        {
            if (!state.Fleet.IsKnown && RestoreFleet?.Invoke(fid) is { IsKnown: true } fleet)
            {
                state.Fleet = fleet;
            }

            // A CarrierLocation after LoadGame gives the live state a system but never a callsign. An owned
            // live state is kept, but for its hold.
            if (RestoreCarrier?.Invoke(fid) is { IsKnown: true } carrier)
            {
                state.Carrier = carrier.With(state.Carrier);
            }

            if (RestoreMissions?.Invoke(fid) is { IsKnown: true } missions)
            {
                state.Missions = missions.With(state.Missions);
            }

            if (RestoreColonisation?.Invoke(fid) is { IsKnown: true } sites)
            {
                state.Colonisation = sites.With(state.Colonisation);
            }

            // Merged rather than taken or refused: a ship boarded this session is in the live set and every
            // other ship the Commander owns is only in the recovered one.
            if (RestoreLoadouts?.Invoke(fid) is { IsKnown: true } loadouts)
            {
                state.Loadouts = loadouts.With(state.Loadouts);
            }

            if (RestoreKit?.Invoke(fid) is { IsKnown: true } kit)
            {
                state.Kit = kit.With(state.Kit);
            }

            if (RestoreNames?.Invoke(fid) is { IsKnown: true } names)
            {
                state.Names = names.With(state.Names.Names);
            }

            if (RestoreEvidence?.Invoke(fid) is { IsKnown: true } evidence)
            {
                state.Reputation = evidence.Reputation.With(state.Reputation);
                state.Contributions = evidence.Contributions.With(state.Contributions);
                state.Tallies = evidence.Tallies.With(state.Tallies);
            }

            if (RestoreCycleMerits?.Invoke(fid) is { IsKnown: true } merits)
            {
                state.CycleMerits = merits.With(state.CycleMerits);
            }
        }
    }

    /// <summary>
    /// Shows this Commander from now on, whoever the journal logs in as, until the next pick. Admits
    /// them first if they are not yet known.
    /// </summary>
    public void Pick(CommanderIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var previous = Active;

        Admit(identity);
        _picked = true;

        if (string.Equals(previous?.Identity.FrontierId, identity.FrontierId, StringComparison.Ordinal))
        {
            return;
        }

        _activeFrontierId = identity.FrontierId;

        CommanderChanged?.Invoke(new CommanderSwitch(previous?.Identity, identity, Priming: false, CommanderSwitchCause.Picked));

        if (!string.Equals(previous?.Location.StarSystem, Active!.Location.StarSystem, StringComparison.OrdinalIgnoreCase))
        {
            SystemChanged?.Invoke();
        }
    }

    public FoldReceipt Apply(JournalEvent journalEvent) => Apply(journalEvent, null);

    public FoldReceipt Apply(JournalEvent journalEvent, SurfaceFix? at) => Apply(journalEvent, at, priming: false);

    /// <summary>Folds one event in, says so when it moved the Commander to another system, and returns what it changed.</summary>
    /// <param name="at">
    /// Where the Commander was standing when this event landed, from <c>Status.json</c>.
    /// </param>
    /// <param name="priming">
    /// Whether this event is part of the startup replay rather than something that just happened.
    /// </param>
    public FoldReceipt Apply(JournalEvent journalEvent, SurfaceFix? at, bool priming)
    {
        var was = Active?.Location.StarSystem;

        var receipt = Fold(journalEvent, at, priming);

        // Read off the value rather than off a list of event kinds: Location, FSDJump, CarrierJump and a
        // Docked that names a system all fold into StarSystem, and an event that leaves it where it was —
        // SupercruiseExit in the system it was already in — raises nothing.
        if (!string.Equals(was, Active?.Location.StarSystem, StringComparison.OrdinalIgnoreCase))
        {
            SystemChanged?.Invoke();
        }

        return receipt;
    }

    private FoldReceipt Fold(JournalEvent journalEvent, SurfaceFix? at, bool priming)
    {
        // Goes to the Commander the event names, which is not necessarily the active one.
        if (journalEvent.Kind == "NewCommander" && journalEvent.String("FID") is { } created)
        {
            return Admit(new CommanderIdentity(created, journalEvent.String("Name") ?? created)).Apply(journalEvent);
        }

        if (CommanderIdentity.From(journalEvent) is { } identity)
        {
            var state = Admit(identity);

            _inGameFrontierId = identity.FrontierId;

            if (!_picked)
            {
                var previous = Active?.Identity;

                _activeFrontierId = identity.FrontierId;

                if (!string.Equals(previous?.FrontierId, identity.FrontierId, StringComparison.Ordinal))
                {
                    CommanderChanged?.Invoke(new CommanderSwitch(previous, identity, priming));
                }
            }

            return state.Apply(journalEvent, at);
        }

        // Every other event belongs to whoever is in the game, never to a picked Commander.
        return InGame?.Apply(journalEvent, at) ?? FoldReceipt.Nothing;
    }

    /// <summary>The state held for this Commander, created and restored first if they are not yet known.</summary>
    private CommanderGameState Admit(CommanderIdentity identity)
    {
        if (_byFrontierId.TryGetValue(identity.FrontierId, out var state))
        {
            return state;
        }

        state = new CommanderGameState(identity);

        // Anything this Commander had before d47 last stopped.
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

        if (RestoreKit?.Invoke(identity.FrontierId) is { IsKnown: true } kit)
        {
            state.Kit = kit;
        }

        // Applied only where the recovered state actually knows something, so a Commander with no
        // carrier in any journal stays at None rather than being handed an empty carrier that reads
        // the same as one d47 has merely lost track of (#406).
        if (RestoreCarrier?.Invoke(identity.FrontierId) is { IsKnown: true } carrier)
        {
            state.Carrier = carrier;
        }

        if (RestoreMissions?.Invoke(identity.FrontierId) is { IsKnown: true } missions)
        {
            state.Missions = missions;
        }

        if (RestoreColonisation?.Invoke(identity.FrontierId) is { IsKnown: true } sites)
        {
            state.Colonisation = sites;
        }

        if (RestoreNames?.Invoke(identity.FrontierId) is { IsKnown: true } names)
        {
            state.Names = names;
        }

        if (RestoreCycleMerits?.Invoke(identity.FrontierId) is { IsKnown: true } merits)
        {
            state.CycleMerits = merits;
        }

        if (RestoreEvidence?.Invoke(identity.FrontierId) is { IsKnown: true } evidence)
        {
            state.Reputation = evidence.Reputation;
            state.Contributions = evidence.Contributions;
            state.Tallies = evidence.Tallies;
        }

        _byFrontierId[identity.FrontierId] = state;
        return state;
    }
}
