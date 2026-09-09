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

    public IReadOnlyDictionary<int, RememberedShip> Ships { get; init; } =
        new Dictionary<int, RememberedShip>();

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
    /// The event kinds that can change what is remembered, so a caller deciding whether to write the
    /// file asks this rather than keeping a second copy of the list (#128).
    /// </summary>
    public static bool MayChange(JournalEvent journalEvent) =>
        journalEvent is not null && journalEvent.Kind
            is "Loadout" or "SetUserShipName" or "EngineerCraft"
            or "ShipyardSell" or "ShipyardBuy" or "ShipyardNew";

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

        if (gone is not { } sold || !Ships.ContainsKey(sold))
        {
            return this;
        }

        var remaining = new Dictionary<int, RememberedShip>(Ships);
        _ = remaining.Remove(sold);

        return this with { Ships = remaining };
    }
}
