namespace D47.Core.Journal;

/// <summary>One ship the Commander owns, and where it is.</summary>
public sealed record StoredShip(int ShipId, string Type, string? Name, string StarSystem)
{
    /// <summary>The station it is parked at, where the event says. Absent for remote ships.</summary>
    public string? StationName { get; init; }

    public long? Value { get; init; }

    /// <summary>Being transferred right now, so it is neither here nor there yet.</summary>
    public bool InTransit { get; init; }

    /// <summary>What transferring it here would cost. Only present for remote ships.</summary>
    public long? TransferPrice { get; init; }

    /// <summary>The ship the Commander is currently flying is not in the stored list.</summary>
    public bool Here { get; init; }

    public string Describe() => Name is not null ? $"{Name} ({Type})" : Type;
}

/// <summary>
/// Every ship the Commander owns (list.md Phase 7, "Know what ships you own and where they
/// are"), built from the StoredShips event.
/// <para>
/// StoredShips is a complete snapshot rather than a delta, and Elite writes it on docking at
/// any station with a shipyard. So the registry is replaced wholesale on each one: merging
/// would accumulate ships that have since been sold, and a fleet list containing a ship the
/// Commander no longer owns is worse than a list that is merely out of date.
/// </para>
/// <para>
/// The event says nothing about the ship currently being flown — that is
/// <see cref="ShipLoadout"/>'s job — so a Commander with four ships sees three here plus the
/// one they are sitting in.
/// </para>
/// </summary>
public sealed record FleetRegistry
{
    public static readonly FleetRegistry Empty = new();

    /// <summary>Where the snapshot was taken, which is what "here" means for the ships in it.</summary>
    public string? SnapshotSystem { get; init; }

    public string? SnapshotStation { get; init; }

    /// <summary>When the snapshot was taken. Null means no shipyard has been visited yet.</summary>
    public DateTimeOffset? TakenAt { get; init; }

    public IReadOnlyList<StoredShip> Ships { get; init; } = [];

    public bool IsKnown => TakenAt is not null;

    public IReadOnlyList<StoredShip> Here => [.. Ships.Where(ship => ship.Here)];

    public IReadOnlyList<StoredShip> Elsewhere => [.. Ships.Where(ship => !ship.Here)];

    /// <summary>Systems holding at least one stored ship, each named once.</summary>
    public IReadOnlyList<string> Systems =>
        [.. Ships.Select(ship => ship.StarSystem).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    public FleetRegistry Apply(JournalEvent journalEvent)
    {
        if (journalEvent.Kind != "StoredShips")
        {
            return this;
        }

        var system = journalEvent.String("StarSystem");
        var station = journalEvent.String("StationName");

        // ShipsHere carries no StarSystem of its own — it is implicitly wherever the event was
        // written. Filling it in here means every ship in the registry answers the same
        // question the same way, rather than the caller having to know which list it came from.
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
}
