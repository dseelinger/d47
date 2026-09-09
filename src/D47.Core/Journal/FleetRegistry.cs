namespace D47.Core.Journal;

/// <summary>One ship the Commander owns, and where it is.</summary>
public sealed record StoredShip(int ShipId, string Type, string? Name, string StarSystem)
{
    /// <summary>The station it is parked at, where the event says.</summary>
    public string? StationName { get; init; }

    public long? Value { get; init; }

    /// <summary>Being transferred right now, so it is neither here nor there yet.</summary>
    public bool InTransit { get; init; }

    /// <summary>What transferring it here would cost.</summary>
    public long? TransferPrice { get; init; }

    /// <summary>The ship the Commander is currently flying is not in the stored list.</summary>
    public bool Here { get; init; }

    /// <summary>
    /// Whether <see cref="StarSystem"/> actually names somewhere, rather than standing in for a system
    /// nothing has reported.
    /// </summary>
    public bool HasSystem =>
        StarSystem is { Length: > 0 } && !string.Equals(StarSystem, "unknown", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The ship as a person reads it, with the hull named rather than spelled
    /// (https://github.com/dseelinger/d47/issues/105).
    /// </summary>
    public string Describe()
    {
        var hull = Knowledge.EliteSpecifications.HullSaid(Type);

        return Name is not null ? $"{Name} ({hull})" : hull;
    }
}

/// <summary>
/// Every ship the Commander owns (Phase 7, "Know what ships you own and where they are"), built from
/// the StoredShips event.
/// </summary>
public sealed record FleetRegistry
{
    public static readonly FleetRegistry Empty = new();

    /// <summary>Where the snapshot was taken, which is what "here" means for the ships in it.</summary>
    public string? SnapshotSystem { get; init; }

    public string? SnapshotStation { get; init; }

    /// <summary>When the snapshot was taken.</summary>
    public DateTimeOffset? TakenAt { get; init; }

    public IReadOnlyList<StoredShip> Ships { get; init; } = [];

    public bool IsKnown => TakenAt is not null;

    public IReadOnlyList<StoredShip> Here => [.. Ships.Where(ship => ship.Here)];

    public IReadOnlyList<StoredShip> Elsewhere => [.. Ships.Where(ship => !ship.Here)];

    /// <summary>Systems holding at least one stored ship, each named once.</summary>
    public IReadOnlyList<string> Systems =>
        [.. Ships.Select(ship => ship.StarSystem).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    public FleetRegistry Apply(JournalEvent journalEvent) => Apply(journalEvent, null, null);

    /// <summary>
    /// <param name="atSystem">Where the Commander is standing, for the events that store a ship without
    /// naming a place.
    /// </summary>
    /// <param name="atSystem">
    /// Where the Commander is standing, for the events that store a ship without naming a place.
    /// </param>
    /// <param name="atStation">The station they are docked at, on the same terms.</param>
    public FleetRegistry Apply(JournalEvent journalEvent, string? atSystem, string? atStation)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (journalEvent.Kind == "StoredShips")
        {
            return FromSnapshot(journalEvent);
        }

        // A delta with no snapshot under it is not information, it is a fleet of one invented from a single
        // event.
        if (!IsKnown)
        {
            return this;
        }

        return journalEvent.Kind switch
        {
            // The ship is gone.
            "ShipyardSell" => Without(journalEvent.Int("SellShipID")),

            // Swapping takes one ship out of storage and puts one in.
            "ShipyardSwap" => Without(journalEvent.Int("ShipID")).Stored(journalEvent, atSystem, atStation),

            // Buying stores the ship being flown.
            "ShipyardBuy" => Stored(journalEvent, atSystem, atStation),

            // ShipyardTransfer is intentionally not handled.
            _ => this,
        };
    }

    private FleetRegistry FromSnapshot(JournalEvent journalEvent)
    {
        var system = journalEvent.String("StarSystem");
        var station = journalEvent.String("StationName");

        // ShipsHere carries no StarSystem of its own — it is implicitly wherever the event was written.
        var here = journalEvent.Items("ShipsHere").Select(element => new StoredShip(
            element.Int("ShipID") ?? 0,
            element.Named("ShipType") ?? "unknown",
            element.String("Name"),
            system ?? "unknown")
        {
            StationName = station,
            Value = element.Long("Value"),
            Here = true,
        });

        var remote = journalEvent.Items("ShipsRemote").Select(element => new StoredShip(
            element.Int("ShipID") ?? 0,
            element.Named("ShipType") ?? "unknown",
            element.String("Name"),
            element.String("StarSystem") ?? "unknown")
        {
            Value = element.Long("Value"),
            InTransit = element.Bool("InTransit"),
            TransferPrice = element.Long("TransferPrice"),
        });

        return new FleetRegistry
        {
            SnapshotSystem = system,
            SnapshotStation = station,
            TakenAt = journalEvent.Timestamp,
            Ships = [.. here, .. remote],
        };
    }

    /// <summary>The same fleet without one ship, or unchanged where it held no such ship.</summary>
    private FleetRegistry Without(int? shipId) =>
        shipId is not { } id || !Ships.Any(ship => ship.ShipId == id)
            ? this
            : this with { Ships = [.. Ships.Where(ship => ship.ShipId != id)] };

    /// <summary>
    /// The ship named by <c>StoreOldShip</c>/<c>StoreShipID</c> joins the fleet — the one the Commander
    /// stepped out of, on a swap or a purchase.
    /// </summary>
    private FleetRegistry Stored(JournalEvent journalEvent, string? atSystem, string? atStation)
    {
        if (journalEvent.Int("StoreShipID") is not { } id || Ships.Any(ship => ship.ShipId == id))
        {
            return this;
        }

        var ship = new StoredShip(
            id,
            journalEvent.Named("StoreOldShip") ?? "unknown",
            Name: null,
            atSystem ?? "unknown")
        {
            StationName = atStation,

            // "Here" is relative to the snapshot, which is the only place this registry knows.
            Here = atSystem is not null
                   && string.Equals(atSystem, SnapshotSystem, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(atStation, SnapshotStation, StringComparison.OrdinalIgnoreCase),
        };

        return this with { Ships = [.. Ships, ship] };
    }
}
