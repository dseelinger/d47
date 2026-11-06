namespace D47.Core.Journal;

/// <summary>What moved the shown Commander to another.</summary>
public enum CommanderSwitchCause
{
    /// <summary>A <c>Commander</c> or <c>LoadGame</c> in the journal, or an older journal's login at startup.</summary>
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

    /// <summary>The journal's last word was a <c>Shutdown</c>.</summary>
    private bool _shutDown;

    /// <summary>The Elite process was running when last checked; assumed until <see cref="GameRunning"/> says otherwise.</summary>
    private bool _running = true;

    /// <summary>
    /// The Commander d47 shows, or null before any identity has been seen. A <see cref="Pick"/> holds
    /// until the next pick or a login by a different Commander.
    /// </summary>
    public CommanderGameState? Active => _activeFrontierId is { } fid ? _byFrontierId[fid] : null;

    /// <summary>
    /// The Commander the journal is writing for, or null before any identity has been seen. Every
    /// event that is not an identity folds into this one.
    /// </summary>
    public CommanderGameState? InGame => _inGameFrontierId is { } fid ? _byFrontierId[fid] : null;

    /// <summary>
    /// The shown Commander is not the one in the game, including one picked or seeded before the journal
    /// has named anybody.
    /// </summary>
    public bool IsOffDuty =>
        _activeFrontierId is not null && !string.Equals(_activeFrontierId, _inGameFrontierId, StringComparison.Ordinal);

    /// <summary>
    /// Off duty with a Commander logged in and Elite open. With Elite closed, or nobody logged in yet, nobody
    /// is flying, so the shown Commander is spoken to; the game's live files still belong to <see cref="InGame"/>.
    /// </summary>
    public bool IsAnotherFlying => IsOffDuty && _inGameFrontierId is not null && _running && !_shutDown;

    /// <summary>Records whether the Elite process is running, which a crash leaves no <c>Shutdown</c> to say.</summary>
    public void GameRunning(bool running) => _running = running;

    /// <summary>Whether the Elite process was running when last checked.</summary>
    public bool IsGameRunning => _running;

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

    /// <summary>The last location the journals show for a Commander, taken while the live journal has given none.</summary>
    public Func<string, JournalLocation?>? RestoreLocation { get; init; }

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

    /// <summary>The squadron faction sightings this Commander's journals hold, merged into what is known.</summary>
    public Func<string, SquadronFactionSightings?>? RestoreSquadronFactions { get; init; }

    /// <summary>The tech broker unlocks this Commander's older journals show, merged into what is known.</summary>
    public Func<string, TechBrokerUnlocks?>? RestoreTechBroker { get; init; }

    /// <summary>
    /// The merits this Commander earned for their Power over the last week, on the same terms as <see
    /// cref="RestoreFleet"/>.
    /// </summary>
    public Func<string, PowerplayCycleMerits?>? RestoreCycleMerits { get; init; }

    /// <summary>The BGS work this Commander did over the last week, on the same terms as <see cref="RestoreFleet"/>.</summary>
    public Func<string, BgsWork?>? RestoreBgsWork { get; init; }

    /// <summary>
    /// The kills this Commander has made, on the same terms as <see cref="RestoreFleet"/> but merged whenever a
    /// record comes back, so an empty one still marks the record whole.
    /// </summary>
    public Func<string, CombatRecord?>? RestoreCombat { get; init; }

    /// <summary>The surface mining sites of this Commander, on the same terms as <see cref="RestoreCombat"/>.</summary>
    public Func<string, SurfaceMiningSites?>? RestoreSurfaceMining { get; init; }

    /// <summary>The credit transactions of this Commander, on the same terms as <see cref="RestoreCombat"/>.</summary>
    public Func<string, CreditLedger?>? RestoreCreditLedger { get; init; }

    /// <summary>The codex entries of this Commander, on the same terms as <see cref="RestoreCombat"/>.</summary>
    public Func<string, CodexLedger?>? RestoreCodexLedger { get; init; }

    /// <summary>
    /// The credits this Commander's newest LoadGame reported, on the same terms as <see cref="RestoreFleet"/>.
    /// </summary>
    public Func<string, long?>? RestoreCredits { get; init; }

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
            foreach (var restorer in Restorers)
            {
                restorer.Late?.Invoke(this, fid, state);
            }
        }
    }

    /// <summary>
    /// Shows this Commander until the next pick or a login by a different Commander. Admits them first if
    /// they are not yet known.
    /// </summary>
    public void Pick(CommanderIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var previous = Active;

        Admit(identity);

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

    /// <summary>
    /// Shows this Commander, the last one an older journal logged in as, while nobody is shown yet. Leaves
    /// <see cref="InGame"/> alone: nobody has logged in.
    /// </summary>
    public void Seed(CommanderIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (_activeFrontierId is not null)
        {
            return;
        }

        Admit(identity);
        _activeFrontierId = identity.FrontierId;

        CommanderChanged?.Invoke(new CommanderSwitch(null, identity, Priming: true));

        if (Active!.Location.StarSystem is not null)
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
            _shutDown = false;

            // A login by anybody but the shown Commander shows them, replacing a pick.
            if (!string.Equals(_activeFrontierId, identity.FrontierId, StringComparison.Ordinal))
            {
                var previous = Active?.Identity;

                _activeFrontierId = identity.FrontierId;

                CommanderChanged?.Invoke(new CommanderSwitch(previous, identity, priming));
            }

            return state.Apply(journalEvent, at);
        }

        if (journalEvent.Kind == "Shutdown")
        {
            _shutDown = true;
        }

        // Every other event belongs to whoever is in the game, never to a picked or seeded one.
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
        foreach (var restorer in Restorers)
        {
            restorer.Admit?.Invoke(this, identity.FrontierId, state);
        }

        _byFrontierId[identity.FrontierId] = state;
        return state;
    }

    private sealed record Restorer(
        Action<GameStateStore, string, CommanderGameState>? Admit,
        Action<GameStateStore, string, CommanderGameState>? Late);

    private static Action<GameStateStore, string, CommanderGameState>? Step<T>(
        Func<GameStateStore, Func<string, T?>?> hook, Func<T, bool> usable, Action<CommanderGameState, T>? write)
        where T : class =>
        write is null
            ? null
            : (store, fid, state) =>
            {
                if (hook(store)?.Invoke(fid) is { } value && usable(value))
                {
                    write(state, value);
                }
            };

    private static Restorer Part<T>(
        Func<GameStateStore, Func<string, T?>?> hook,
        Func<T, bool> usable,
        Action<CommanderGameState, T>? admit,
        Action<CommanderGameState, T>? late)
        where T : class => new(Step(hook, usable, admit), Step(hook, usable, late));

    private static Restorer Merged<T>(Func<GameStateStore, Func<string, T?>?> hook, Action<CommanderGameState, T> merge)
        where T : class => Part(hook, _ => true, merge, merge);

    private static readonly Action<GameStateStore, string, CommanderGameState> Credits = (store, fid, state) =>
    {
        if (store.RestoreCredits?.Invoke(fid) is { } credits)
        {
            state.RestoreBalance(credits);
        }
    };

    // One order serves both methods: each hook writes only its own parts. A hook with no Admit step is
    // read only by RestoreLate, and one with no Late step only when a Commander is first met.
    private static readonly Restorer[] Restorers =
    [
        Part<OrganicSampling>(s => s.Restore, _ => true, (c, v) => c.Sampling = v, null),
        Part<FleetRegistry>(s => s.RestoreFleet, v => v.IsKnown, (c, v) => c.Fleet = v, (c, v) => c.Fleet = c.Fleet.IsKnown ? c.Fleet : v),
        Part<JournalLocation>(s => s.RestoreLocation, v => v.StarSystem is not null, null, (c, v) => c.Location = c.Location.StarSystem is null ? v : c.Location),
        // Merged rather than taken or refused: a ship boarded this session is in the live set and every
        // other ship the Commander owns is only in the recovered one.
        Part<ShipLoadouts>(s => s.RestoreLoadouts, v => v.IsKnown, (c, v) => c.Loadouts = v, (c, v) => c.Loadouts = v.With(c.Loadouts)),
        Part<OwnedKit>(s => s.RestoreKit, v => v.IsKnown, (c, v) => c.Kit = v, (c, v) => c.Kit = v.With(c.Kit)),
        // A CarrierLocation after LoadGame gives the live state a system but never a callsign. An owned
        // live state is kept, but for its hold. Only a known carrier is applied, so a Commander with none
        // stays at None.
        Part<CarrierState>(s => s.RestoreCarrier, v => v.IsKnown, (c, v) => c.Carrier = v, (c, v) => c.Carrier = v.With(c.Carrier)),
        Part<MissionBoard>(s => s.RestoreMissions, v => v.IsKnown, (c, v) => c.Missions = v, (c, v) => c.Missions = v.With(c.Missions)),
        Part<ColonisationSites>(s => s.RestoreColonisation, v => v.IsKnown, (c, v) => c.Colonisation = v, (c, v) => c.Colonisation = v.With(c.Colonisation)),
        Part<Listening.SpokenNames>(s => s.RestoreNames, v => v.IsKnown, (c, v) => c.Names = v, (c, v) => c.Names = v.With(c.Names.Names)),
        Part<PowerplayCycleMerits>(s => s.RestoreCycleMerits, v => v.IsKnown, (c, v) => c.CycleMerits = v, (c, v) => c.CycleMerits = v.With(c.CycleMerits)),
        Part<BgsWork>(s => s.RestoreBgsWork, v => v.IsKnown, (c, v) => c.BgsWork = v, (c, v) => c.BgsWork = v.With(c.BgsWork)),
        Merged<CombatRecord>(s => s.RestoreCombat, (c, v) => c.Combat = v.With(c.Combat)),
        Merged<SurfaceMiningSites>(s => s.RestoreSurfaceMining, (c, v) => c.SurfaceMining = v.With(c.SurfaceMining)),
        Merged<CreditLedger>(s => s.RestoreCreditLedger, (c, v) => c.CreditLedger = v.With(c.CreditLedger)),
        Merged<CodexLedger>(s => s.RestoreCodexLedger, (c, v) => c.CodexLedger = v.With(c.CodexLedger)),
        Part<UnlockEvidence>(s => s.RestoreEvidence, v => v.IsKnown,
            (c, v) => (c.Reputation, c.Contributions, c.Tallies) = (v.Reputation, v.Contributions, v.Tallies),
            (c, v) => (c.Reputation, c.Contributions, c.Tallies) =
                (v.Reputation.With(c.Reputation), v.Contributions.With(c.Contributions), v.Tallies.With(c.Tallies))),
        // The other way round from the rest: the live unlocks take the restored ones.
        Part<TechBrokerUnlocks>(s => s.RestoreTechBroker, v => v.IsKnown, (c, v) => c.TechBroker = v, (c, v) => c.TechBroker = c.TechBroker.With(v)),
        Part<SquadronFactionSightings>(s => s.RestoreSquadronFactions, v => v.IsKnown, (c, v) => c.SquadronFactions = v, (c, v) => c.SquadronFactions = c.SquadronFactions.With(v)),
        new Restorer(Credits, Credits),
    ];
}
