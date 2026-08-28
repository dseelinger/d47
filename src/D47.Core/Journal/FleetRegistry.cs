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

    /// <summary>
    /// Whether <see cref="StarSystem"/> actually names somewhere, rather than standing in for a
    /// system nothing has reported. <c>"unknown"</c> is what every path here writes when it has no
    /// name — so it is asked about in one place rather than compared against in several, and a
    /// caller putting a system on the clipboard cannot hand the Commander the placeholder to
    /// paste into the Galaxy Map.
    /// </summary>
    public bool HasSystem =>
        StarSystem is { Length: > 0 } && !string.Equals(StarSystem, "unknown", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The ship as a person reads it, with the hull named rather than spelled
    /// (https://github.com/dseelinger/d47/issues/105).
    /// <para>
    /// <b>Through <c>HullSaid</c>, which is the ladder every caller turning a stored hull into
    /// words is meant to climb.</b> This was the one <c>Describe</c> that did not, and it is the
    /// third time a raw symbol has reached the Commander — <c>HullSaid</c>'s own comment predicted
    /// exactly this, that "one of them could say Kestrel Mk II while another said smallcombat01_nx
    /// about the same ship".
    /// </para>
    /// <para>
    /// <c>StoredShips</c> carries no <c>ShipType_Localised</c> at all, so <see cref="Type"/> is
    /// always the bare symbol here — unlike <c>ShipLoadout</c>, where Frontier sends a localised
    /// name and the fleet reads correctly. That is why the same fleet had two spellings, decided by
    /// which event a row happened to come from.
    /// </para>
    /// <para>
    /// Safe even when <see cref="Type"/> already holds a name: the ladder tries the measured row,
    /// then the armour prefix, then a spoken match — and hands back what it was given when all
    /// three miss, so a hull nothing knows is no worse than before.
    /// </para>
    /// </summary>
    public string Describe()
    {
        var hull = Knowledge.EliteSpecifications.HullSaid(Type);

        return Name is not null ? $"{Name} ({hull})" : hull;
    }
}

/// <summary>
/// Every ship the Commander owns (Phase 7, "Know what ships you own and where they
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

    public FleetRegistry Apply(JournalEvent journalEvent) => Apply(journalEvent, null, null);

    /// <param name="atSystem">Where the Commander is standing, for the events that store a ship
    /// without naming a place. See <see cref="Stored"/>.</param>
    /// <param name="atStation">The station they are docked at, on the same terms.</param>
    public FleetRegistry Apply(JournalEvent journalEvent, string? atSystem, string? atStation)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (journalEvent.Kind == "StoredShips")
        {
            return FromSnapshot(journalEvent);
        }

        // A delta with no snapshot under it is not information, it is a fleet of one invented
        // from a single event. Until a shipyard has been visited the honest answer is "unknown",
        // which is what IsKnown reports and what the surface renders.
        if (!IsKnown)
        {
            return this;
        }

        return journalEvent.Kind switch
        {
            // The ship is gone. This is the event the whole delta path exists for: a snapshot
            // read back out of an older journal is only safe if what happened since can take
            // ships out of it again.
            "ShipyardSell" => Without(journalEvent.Int("SellShipID")),

            // Swapping takes one ship out of storage and puts one in. The taken ship becomes the
            // flown one, and the flown ship is never in this registry.
            "ShipyardSwap" => Without(journalEvent.Int("ShipID")).Stored(journalEvent, atSystem, atStation),

            // Buying stores the ship being flown. The ship *bought* is not stored — it is flown
            // out of the shipyard — and its id arrives on the ShipyardNew written immediately
            // after, so there is deliberately nothing to do for that event here.
            "ShipyardBuy" => Stored(journalEvent, atSystem, atStation),

            // ShipyardTransfer is intentionally not handled. It moves a ship without changing
            // who owns it, and this path exists to keep ownership honest across a gap. Where a
            // ship is parked stays as of TakenAt, which is what that stamp is for.
            _ => this,
        };
    }

    private FleetRegistry FromSnapshot(JournalEvent journalEvent)
    {
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

    /// <summary>The same fleet without one ship, or unchanged where it held no such ship.</summary>
    private FleetRegistry Without(int? shipId) =>
        shipId is not { } id || !Ships.Any(ship => ship.ShipId == id)
            ? this
            : this with { Ships = [.. Ships.Where(ship => ship.ShipId != id)] };

    /// <summary>
    /// The ship named by <c>StoreOldShip</c>/<c>StoreShipID</c> joins the fleet — the one the
    /// Commander stepped out of, on a swap or a purchase.
    /// <para>
    /// Neither event names a system or a station: a ship is stored wherever the Commander is
    /// standing, and Elite leaves that implicit. So the place is passed in, exactly as the
    /// colonisation depot's is and for the same reason.
    /// </para>
    /// <para>
    /// It joins without a name. The events carry the hull and the id but never the Commander's
    /// name for it, and inventing one would be worse than showing the hull — the next real
    /// snapshot fills it in.
    /// </para>
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
