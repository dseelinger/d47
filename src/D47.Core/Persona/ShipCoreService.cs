using D47.Core.Journal;

namespace D47.Core.Persona;

/// <summary>
/// A ship change that has settled, and the core the Commander bound to the ship they are now in (Phase
/// 35, "Switching ships switches the core").
/// </summary>
/// <param name="Announce">Whether the arriving core is allowed to say anything.</param>
public sealed record ShipCoreSwitch(int ShipId, string Core, bool Announce);

/// <summary>Which core flies which ship, and when boarding one puts it aboard (Phase 35).</summary>
public sealed class ShipCoreService(ShipCoreStore store, Func<CommanderGameState?> game, TimeSpan? settle = null)
{
    /// <summary>
    /// How long a ship has to stay the ship before its binding acts, and the answer to the phase's
    /// question about a Commander boarding five ships in five minutes.
    /// </summary>
    public static readonly TimeSpan DefaultSettle = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _settle = settle ?? DefaultSettle;

    /// <summary>
    /// The four fields below are read on the tick thread and written on whichever thread pressed the
    /// button or the key.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>The ship whose binding has been acted on.</summary>
    private int? _aboard;

    /// <summary>The ship they have moved to and how long they have stayed in it.</summary>
    private int? _waiting;

    private TimeSpan _waited;

    /// <summary>Whether the first ship has been seen.</summary>
    private bool _started;

    public ShipCoreStore Store => store;

    /// <summary>Whose bindings are being read and written.</summary>
    private string Fid => game()?.Identity.FrontierId ?? string.Empty;

    private string? CommanderName => game()?.Identity.Name;

    /// <summary>The ship the Commander is in, where the journal has said.</summary>
    public int? CurrentShipId => game()?.Ship.ShipId;

    /// <summary>This Commander's binding for one ship, or null when nothing is bound.</summary>
    public ShipCoreBinding? For(int shipId) => store.For(Fid, shipId);

    /// <summary>The core bound to the ship they are in, or null.</summary>
    public ShipCoreBinding? Current => CurrentShipId is { } id ? For(id) : null;

    /// <summary>Every ship the Commander could bind a core to, as an id and the words they call it by.</summary>
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
            return store.Forget(Fid, shipId)
                ? $"{said} no longer asks for a core. Whoever is aboard stays aboard."
                : $"{said} was not bound to a core.";
        }

        var persona = PersonaCatalog.Resolve(core);
        var hull = game()?.Ship;

        store.Bind(new ShipCoreBinding(
            Fid,
            shipId,
            persona.Id,
            hull?.ShipId == shipId ? hull.Type : known.Said,
            hull?.ShipId == shipId ? hull.Name : null)
        {
            CommanderName = CommanderName,
        });

        // Marked as handled where it is the ship they are in, so it does not then read as a change and switch
        // to the core that is already aboard.
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
    /// changed.
    /// </summary>
    public ShipCoreSwitch? Observe(TimeSpan since)
    {
        // Bindings from before the file carried a Commander are claimed by the first one seen — a
        // pre-existing file was written by the installation's one Commander, and leaving them unowned would
        // silently unbind every core on the release that added the key.
        if (game()?.Identity is { FrontierId.Length: > 0 } identity)
        {
            store.Adopt(identity.FrontierId, identity.Name);
        }

        var shipId = CurrentShipId;

        lock (_gate)
        {
            return Observed(shipId, since);
        }
    }

    /// <summary>
    /// Forgets which ship has been acted on, so the next <see cref="Observe"/> adopts whatever the
    /// Commander is in as the ship d47 found them in — silently, as at startup (Phase 44, "What a new
    /// Commander logging in actually changes").
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _started = false;
            _aboard = null;
            _waiting = null;
            _waited = TimeSpan.Zero;
        }
    }

    private ShipCoreSwitch? Observed(int? shipId, TimeSpan since)
    {
        if (shipId is not { } ship)
        {
            // No Loadout yet, so there is no ship to have a core.
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
            // Back in the ship whose core is already aboard.
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

    /// <summary>Binds the ship the Commander is in to a core, and says what happened.</summary>
    public string Bind(string core)
    {
        var state = game();

        if (state?.Ship.ShipId is not { } shipId)
        {
            return "I do not know which ship you are in yet — nothing has said so this session.";
        }

        var persona = PersonaCatalog.Resolve(core);
        var ship = state.Ship;

        store.Bind(new ShipCoreBinding(Fid, shipId, persona.Id, ship.Type, ship.Name)
        {
            CommanderName = CommanderName,
        });

        // Marked as handled, so the ship they are already in does not then read as a change and switch to the
        // core that is already aboard.
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

        var was = For(shipId);

        if (!store.Forget(Fid, shipId))
        {
            return $"{Describe(state.Ship)} was not bound to a core.";
        }

        return $"{Describe(state.Ship)} no longer asks for {PersonaCatalog.Resolve(was!.Core).Name}. "
               + "Whoever is aboard stays aboard.";
    }

    /// <summary>What the ship they are in is bound to.</summary>
    public string DescribeCurrent()
    {
        var state = game();

        if (state?.Ship.ShipId is not { } shipId)
        {
            return "No ship yet — nothing has said which one you are in this session.";
        }

        var said = Describe(state.Ship);

        return For(shipId) is { } binding
            ? $"{said} flies with {PersonaCatalog.Resolve(binding.Core).Name}."
            : $"{said} is not bound to a core, so whoever is aboard stays aboard.";
    }

    /// <summary>
    /// Every binding, for the panel row that offers to forget one — an offer to clear something has to
    /// be able to say what there is to clear.
    /// </summary>
    public string DescribeAll()
    {
        // This Commander's, because the panel row is theirs — another Commander's bindings on the same
        // installation are not this one's to read out.
        var fid = Fid;

        var bindings = store.Bindings
            .Where(binding => string.Equals(binding.CommanderFid, fid, StringComparison.Ordinal))
            .ToList();

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
            (null, { } hull) => $"{Knowledge.EliteSpecifications.HullSaid(hull)} {binding.ShipId}",
            _ => $"ship {binding.ShipId}",
        };

    private static string Describe(ShipLoadout ship) => ship.Describe() ?? "This ship";

    private ShipCoreSwitch? Switch(int shipId, bool announce) =>
        For(shipId) is { } binding ? new ShipCoreSwitch(shipId, binding.Core, announce) : null;
}
