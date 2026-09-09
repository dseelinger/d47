namespace D47.Core.Journal;

/// <summary>The Commander being tailed changed (Phase 44).</summary>
/// <param name="Previous">
/// Who it was, or null when nobody had been identified yet — which makes this an adoption.
/// </param>
/// <param name="Current">Who it is now.</param>
/// <param name="Priming">
/// Whether this came out of the startup replay rather than a login that just happened.
/// </param>
public sealed record CommanderSwitch(CommanderIdentity? Previous, CommanderIdentity Current, bool Priming)
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

    /// <summary>
    /// The Commander whose journal is currently being tailed, or null before any identity has been
    /// seen.
    /// </summary>
    public CommanderGameState? Active => _activeFrontierId is { } fid ? _byFrontierId[fid] : null;

    public IReadOnlyCollection<CommanderGameState> All => _byFrontierId.Values;

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
    /// Where this Commander's fleet carrier was when their journals last said, on the same terms as
    /// <see cref="RestoreFleet"/> (#406).
    /// </summary>
    public Func<string, CarrierState?>? RestoreCarrier { get; init; }

    /// <summary>
    /// Every place this Commander has met, on the same terms as <see cref="RestoreLoadouts"/> (#134).
    /// </summary>
    public Func<string, Listening.SpokenNames?>? RestoreNames { get; init; }

    /// <summary>
    /// Raised when the Commander whose journal is being tailed changes (Phase 44, "One switch signal").
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
            state.Loadouts = loadouts.TryGetValue(fid, out var ships) ? ships : ShipLoadouts.Empty;
        }
    }

    public void Apply(JournalEvent journalEvent) => Apply(journalEvent, null);

    public void Apply(JournalEvent journalEvent, SurfaceFix? at) => Apply(journalEvent, at, priming: false);

    /// <summary>Folds one event in, and says so when it moved the Commander to another system.</summary>
    /// <param name="at">
    /// Where the Commander was standing when this event landed, from <c>Status.json</c>.
    /// </param>
    /// <param name="priming">
    /// Whether this event is part of the startup replay rather than something that just happened.
    /// </param>
    public void Apply(JournalEvent journalEvent, SurfaceFix? at, bool priming)
    {
        var was = Active?.Location.StarSystem;

        Fold(journalEvent, at, priming);

        // Read off the value rather than off a list of event kinds: Location, FSDJump, CarrierJump and a
        // Docked that names a system all fold into StarSystem, and an event that leaves it where it was —
        // SupercruiseExit in the system it was already in — raises nothing.
        if (!string.Equals(was, Active?.Location.StarSystem, StringComparison.OrdinalIgnoreCase))
        {
            SystemChanged?.Invoke();
        }
    }

    private void Fold(JournalEvent journalEvent, SurfaceFix? at, bool priming)
    {
        if (CommanderIdentity.From(journalEvent) is { } identity)
        {
            var previous = Active?.Identity;

            if (!_byFrontierId.TryGetValue(identity.FrontierId, out var state))
            {
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

                // Applied only where the recovered state actually knows something, so a Commander with no
                // carrier in any journal stays at None rather than being handed an empty carrier that reads
                // the same as one d47 has merely lost track of (#406).
                if (RestoreCarrier?.Invoke(identity.FrontierId) is { IsKnown: true } carrier)
                {
                    state.Carrier = carrier;
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

        // Every other event belongs to whoever is currently active.
        Active?.Apply(journalEvent, at);
    }
}
