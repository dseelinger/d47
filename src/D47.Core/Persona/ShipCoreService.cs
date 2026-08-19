using D47.Core.Journal;

namespace D47.Core.Persona;

/// <summary>
/// A ship change that has settled, and the core the Commander bound to the ship they are now in
/// (list.md Phase 35, "Switching ships switches the core").
/// </summary>
/// <param name="Announce">
/// Whether the arriving core is allowed to say anything. False for the ship d47 finds the
/// Commander already in when it starts: launching the app is not a ship change they just made,
/// and a core announcing itself every launch is noise rather than continuity.
/// </param>
public sealed record ShipCoreSwitch(int ShipId, string Core, bool Announce);

/// <summary>
/// Which core flies which ship, and when boarding one puts it aboard (list.md Phase 35).
/// <para>
/// <b>Binding is an act, applying is a consequence.</b> The store is written only from here and
/// only by <see cref="Bind"/> and <see cref="Forget"/>, whose callers are a panel button, a
/// phrase and a hotkey — the three callers a protected act has. <see cref="Observe"/> writes
/// nothing: it reads the binding the Commander already made and says a switch is due. That split
/// is what makes "written at the Commander's command, never by watching" true of the file rather
/// than merely intended.
/// </para>
/// <para>
/// <b>Owns no thread and reads no clock.</b> The settle window is measured in elapsed time handed
/// in by the tick loop, for the same reason the journal reader exposes <c>Poll()</c> — so the
/// replay harness runs this at 100x with times drawn from a fixture.
/// </para>
/// </summary>
public sealed class ShipCoreService(ShipCoreStore store, Func<CommanderGameState?> game, TimeSpan? settle = null)
{
    /// <summary>
    /// How long a ship has to stay the ship before its binding acts, and the answer to the
    /// phase's question about a Commander boarding five ships in five minutes.
    /// <para>
    /// Each arrival is a model call and a discarded working transcript, so a shipyard shuffle
    /// must not be five of each. Every change restarts the window and only the ship they are
    /// still in when it elapses is acted on: five ships in five minutes costs one switch and one
    /// line, and that line is from the core of the ship they actually left in.
    /// </para>
    /// </summary>
    public static readonly TimeSpan DefaultSettle = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _settle = settle ?? DefaultSettle;

    /// <summary>
    /// The four fields below are read on the tick thread and written on whichever thread pressed
    /// the button or the key. Nothing here is contended for more than a few instructions, and the
    /// alternative — a binding made while a settle window is open leaving the watch half-updated —
    /// is a core arriving that nobody asked for.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>The ship whose binding has been acted on. Not necessarily the ship they are in.</summary>
    private int? _aboard;

    /// <summary>The ship they have moved to and how long they have stayed in it.</summary>
    private int? _waiting;

    private TimeSpan _waited;

    /// <summary>
    /// Whether the first ship has been seen. This rather than the tick loop's own first-tick flag,
    /// because this service is built after the loop has already been primed — "the ship d47 found
    /// the Commander in" is the first one <em>this</em> has seen, whenever that happens to be.
    /// </summary>
    private bool _started;

    public ShipCoreStore Store => store;

    /// <summary>The ship the Commander is in, where the journal has said.</summary>
    public int? CurrentShipId => game()?.Ship.ShipId;

    /// <summary>The core bound to the ship they are in, or null.</summary>
    public ShipCoreBinding? Current => CurrentShipId is { } id ? store.For(id) : null;

    /// <summary>
    /// Every ship the Commander could bind a core to, as an id and the words they call it by.
    /// <para>
    /// <b>The fleet, not the ship they are sitting in</b> (remediation.md 15, item 13). Binding
    /// used to be scoped to the ship being flown, so setting a core meant boarding that ship in
    /// game first. Nothing in Phase 35 required that — it asks for the binding to be deliberate and
    /// protected, and a dropdown is both.
    /// </para>
    /// <para>
    /// The flown ship is included even though <c>StoredShips</c> never lists it: Elite reports the
    /// fleet as what is in storage, so the one underneath the Commander has to be added back or the
    /// list is missing exactly the ship they are most likely to want.
    /// </para>
    /// </summary>
    public IReadOnlyList<(int ShipId, string Said)> Fleet()
    {
        var state = game();

        if (state is null)
        {
            return [];
        }

        var found = new Dictionary<int, string>();

        foreach (var ship in state.Fleet.Ships)
        {
            found[ship.ShipId] = ship.Describe();
        }

        if (state.Ship is { ShipId: { } aboard } flown && flown.Describe() is { Length: > 0 } said)
        {
            found[aboard] = said;
        }

        return [.. found.Select(pair => (pair.Key, pair.Value)).OrderBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Binds a named ship to a core, or unbinds it when the core is null, and says what happened.
    /// <para>
    /// The ship is named rather than assumed, which is the whole of item 13. Everything else is
    /// <see cref="Bind(string)"/>'s: the key is the ship's own id, so two Kraits are two ships.
    /// </para>
    /// </summary>
    public string BindTo(int shipId, string? core)
    {
        var known = Fleet().FirstOrDefault(entry => entry.ShipId == shipId);

        if (known.Said is not { Length: > 0 } said)
        {
            return "I do not know that ship. Dock somewhere with a shipyard and I will read your fleet.";
        }

        if (core is not { Length: > 0 })
        {
            return store.Forget(shipId)
                ? $"{said} no longer asks for a core. Whoever is aboard stays aboard."
                : $"{said} was not bound to a core.";
        }

        var persona = PersonaCatalog.Resolve(core);
        var hull = game()?.Ship;

        store.Bind(new ShipCoreBinding(
            shipId,
            persona.Id,
            hull?.ShipId == shipId ? hull.Type : known.Said,
            hull?.ShipId == shipId ? hull.Name : null));

        // Marked as handled where it is the ship they are in, so it does not then read as a change
        // and switch to the core that is already aboard.
        if (CurrentShipId == shipId)
        {
            lock (_gate)
            {
                _started = true;
                _aboard = shipId;
                _waiting = null;
                _waited = TimeSpan.Zero;
            }
        }

        return $"{persona.Name} now flies {said}.";
    }

    /// <summary>
    /// What the ship change on this tick means, or null for the ordinary case of nothing having
    /// changed. Called once per tick with the time since the last one.
    /// </summary>
    public ShipCoreSwitch? Observe(TimeSpan since)
    {
        var shipId = CurrentShipId;

        lock (_gate)
        {
            return Observed(shipId, since);
        }
    }

    private ShipCoreSwitch? Observed(int? shipId, TimeSpan since)
    {
        if (shipId is not { } ship)
        {
            // No Loadout yet, so there is no ship to have a core. Deliberately not treated as a
            // change: a Commander on foot has not left their ship.
            //
            // Nor has one in a fighter or an SRV, and that needs no handling here — measured
            // across the 915 journals on this machine, Loadout is written for twenty-two hulls
            // and not one of them is a fighter. The id stays the mother ship's throughout.
            return null;
        }

        if (!_started)
        {
            _started = true;
            _aboard = ship;

            return Switch(ship, announce: false);
        }

        if (ship == _aboard)
        {
            // Back in the ship whose core is already aboard. Whatever they were shuffling
            // through, they finished where they started, and nothing is due.
            _waiting = null;
            _waited = TimeSpan.Zero;

            return null;
        }

        if (ship != _waiting)
        {
            _waiting = ship;
            _waited = TimeSpan.Zero;

            return null;
        }

        _waited += since;

        if (_waited < _settle)
        {
            return null;
        }

        _aboard = ship;
        _waiting = null;
        _waited = TimeSpan.Zero;

        return Switch(ship, announce: true);
    }

    /// <summary>
    /// Binds the ship the Commander is in to a core, and says what happened.
    /// <para>
    /// The core is passed in rather than read from a host here, so the one caller that means
    /// something else — a hand edit, a test — is not forced to pretend. Every real caller passes
    /// the core aboard, because "remember this one for this ship" is the whole of the gesture.
    /// </para>
    /// </summary>
    public string Bind(string core)
    {
        var state = game();

        if (state?.Ship.ShipId is not { } shipId)
        {
            return "I do not know which ship you are in yet — nothing has said so this session.";
        }

        var persona = PersonaCatalog.Resolve(core);
        var ship = state.Ship;

        store.Bind(new ShipCoreBinding(shipId, persona.Id, ship.Type, ship.Name));

        // Marked as handled, so the ship they are already in does not then read as a change and
        // switch to the core that is already aboard.
        lock (_gate)
        {
            _started = true;
            _aboard = shipId;
            _waiting = null;
            _waited = TimeSpan.Zero;
        }

        return $"{persona.Name} now flies {Describe(ship)}.";
    }

    /// <summary>Unbinds the ship the Commander is in, and says what happened.</summary>
    public string Forget()
    {
        var state = game();

        if (state?.Ship.ShipId is not { } shipId)
        {
            return "I do not know which ship you are in yet — nothing has said so this session.";
        }

        var was = store.For(shipId);

        if (!store.Forget(shipId))
        {
            return $"{Describe(state.Ship)} was not bound to a core.";
        }

        return $"{Describe(state.Ship)} no longer asks for {PersonaCatalog.Resolve(was!.Core).Name}. "
               + "Whoever is aboard stays aboard.";
    }

    /// <summary>
    /// What the ship they are in is bound to. One line, for the panel row and for the answer the
    /// model is allowed to read.
    /// </summary>
    public string DescribeCurrent()
    {
        var state = game();

        if (state?.Ship.ShipId is not { } shipId)
        {
            return "No ship yet — nothing has said which one you are in this session.";
        }

        var said = Describe(state.Ship);

        return store.For(shipId) is { } binding
            ? $"{said} flies with {PersonaCatalog.Resolve(binding.Core).Name}."
            : $"{said} is not bound to a core, so whoever is aboard stays aboard.";
    }

    /// <summary>
    /// Every binding, for the panel row that offers to forget one — an offer to clear something
    /// has to be able to say what there is to clear.
    /// </summary>
    public string DescribeAll()
    {
        var bindings = store.Bindings;

        if (bindings.Count == 0)
        {
            return "No ship is bound to a core. Until one is, changing ship changes nothing about "
                   + "who is answering you.";
        }

        var said = bindings.Select(binding =>
            $"{Named(binding)}: {PersonaCatalog.Resolve(binding.Core).Name}");

        var problems = store.Problems;

        return problems.Count == 0
            ? string.Join("; ", said) + "."
            : string.Join("; ", said) + ". "
              + $"Refused: {string.Join("; ", problems.Select(problem => $"{problem.Where} — {problem.Reason}"))}";
    }

    /// <summary>A stored binding as a person reads it, falling back to the id it is keyed on.</summary>
    private static string Named(ShipCoreBinding binding) =>
        (binding.Name, binding.Hull) switch
        {
            ({ } name, _) => name,
            (null, { } hull) => $"{Knowledge.EliteSpecifications.Ship(hull)?.Name ?? hull} {binding.ShipId}",
            _ => $"ship {binding.ShipId}",
        };

    private static string Describe(ShipLoadout ship) => ship.Describe() ?? "This ship";

    private ShipCoreSwitch? Switch(int shipId, bool announce) =>
        store.For(shipId) is { } binding ? new ShipCoreSwitch(shipId, binding.Core, announce) : null;
}
