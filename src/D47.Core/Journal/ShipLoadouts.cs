namespace D47.Core.Journal;

/// <summary>One ship's modules as they were last seen, and when that was.</summary>
/// <param name="Loadout">The whole picture, exactly as it stood.</param>
/// <param name="SeenAt">The timestamp of the journal event that carried it.</param>
public sealed record RememberedShip(ShipLoadout Loadout, DateTimeOffset SeenAt);

/// <summary>
/// Every ship the Commander has been seen sitting in, keyed on <c>ShipID</c> (asked for 2026-08-20:
/// "Don't get amnesia — you should remember modules of ships").
/// </summary>
public sealed record ShipLoadouts
{
    public static readonly ShipLoadouts Empty = new();

    /// <summary>Known to hold no ships, so a save removes whatever the file held.</summary>
    public static readonly ShipLoadouts NoShips = new() { IsWhole = true };

    public IReadOnlyDictionary<int, RememberedShip> Ships { get; init; } =
        new Dictionary<int, RememberedShip>();

    /// <summary>
    /// Whether this is every ship the Commander has, rather than only what this session has seen. A
    /// save replaces the file's ships with a whole set and writes over them with any other (#475).
    /// </summary>
    public bool IsWhole { get; init; }

    /// <summary>
    /// Ship ids forgotten by a sale or purchase. An entry in <see cref="Ships"/> under the same id is the
    /// hull that took the id afterwards.
    /// </summary>
    public IReadOnlySet<int> Forgotten { get; init; } = new HashSet<int>();

    public bool IsKnown => Ships.Count > 0;

    /// <summary>What this ship looked like when it was last seen, or null for one never seen.</summary>
    public RememberedShip? For(int? shipId) =>
        shipId is { } id && Ships.TryGetValue(id, out var remembered) ? remembered : null;

    /// <summary>The ship seen most recently of all, or null where none has been (#337).</summary>
    public RememberedShip? LastSeen => Ships.Count == 0
        ? null
        : Ships
            .OrderByDescending(entry => entry.Value.SeenAt)
            .ThenByDescending(entry => entry.Key)
            .First()
            .Value;

    /// <summary>Files the ship the Commander is in now, where it has moved on.</summary>
    public ShipLoadouts Remember(ShipLoadout ship, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(ship);

        if (ship.ShipId is not { } id || !ship.IsKnown)
        {
            return this;
        }

        if (Ships.TryGetValue(id, out var held) && ReferenceEquals(held.Loadout, ship))
        {
            return this;
        }

        return this with
        {
            Ships = new Dictionary<int, RememberedShip>(Ships) { [id] = new RememberedShip(ship, at) },
        };
    }

    /// <summary>
    /// These ships without the ones <paramref name="newer"/> forgot, and with its ships written over them,
    /// matched on ShipID. A whole <paramref name="newer"/> is taken as it is.
    /// </summary>
    public ShipLoadouts With(ShipLoadouts newer)
    {
        ArgumentNullException.ThrowIfNull(newer);

        if (newer.IsWhole)
        {
            return newer;
        }

        if (newer.Ships.Count == 0 && newer.Forgotten.Count == 0)
        {
            return this;
        }

        var merged = new Dictionary<int, RememberedShip>(Ships);

        foreach (var id in newer.Forgotten)
        {
            _ = merged.Remove(id);
        }

        foreach (var (id, ship) in newer.Ships)
        {
            merged[id] = ship;
        }

        return this with { Ships = merged, Forgotten = new HashSet<int>(Forgotten.Union(newer.Forgotten)) };
    }

    /// <summary>
    /// The event kinds that can change what is remembered, so a caller deciding whether to write the
    /// file asks this rather than keeping a second copy of the list (#128).
    /// </summary>
    public static bool MayChange(JournalEvent journalEvent) =>
        journalEvent is not null && journalEvent.Kind
            is "Loadout" or "SetUserShipName" or "EngineerCraft"
            or "ShipyardSell" or "ShipyardBuy" or "ShipyardNew" or "NewCommander";

    /// <summary>Forgetting a ship, which a durable file keeps forever unless this removes it (#128).</summary>
    public ShipLoadouts Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        var gone = journalEvent.Kind switch
        {
            "ShipyardSell" => journalEvent.Int("SellShipID"),

            // The part exchange.
            "ShipyardBuy" => journalEvent.Int("SellShipID"),

            // Not a sale at all, and it forgets for a stronger reason than one: whatever this id used to
            // name, it names this new hull now.
            "ShipyardNew" => journalEvent.Int("NewShipID"),

            _ => null,
        };

        if (gone is not { } sold)
        {
            return this;
        }

        // Recorded even for a ship this set never held, so a save still takes it out of the file.
        var forgotten = Forgotten.Contains(sold) ? Forgotten : new HashSet<int>(Forgotten) { sold };

        if (!Ships.ContainsKey(sold))
        {
            return ReferenceEquals(forgotten, Forgotten) ? this : this with { Forgotten = forgotten };
        }

        var remaining = new Dictionary<int, RememberedShip>(Ships);
        _ = remaining.Remove(sold);

        return this with { Ships = remaining, Forgotten = forgotten };
    }
}
