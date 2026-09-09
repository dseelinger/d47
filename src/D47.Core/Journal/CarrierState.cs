namespace D47.Core.Journal;

/// <summary>
/// The Commander's fleet carrier, if they own one (Phase 7, "Know your location and your carrier (if
/// owned)").
/// </summary>
/// <param name="Role">
/// Elite's own word for the service — <c>Refuel</c>, <c>Rearm</c>, and so on.
/// </param>
/// <param name="Activated">Whether the service has been bought at all.</param>
/// <param name="Enabled">Whether a bought service is currently switched on.</param>
/// <param name="Name">The crew member's name, or empty for a service nobody staffs.</param>
public readonly record struct CarrierService(string Role, bool Activated, bool Enabled, string Name)
{
    /// <summary>Whether a Commander docking right now could use this.</summary>
    public bool IsOpen => Activated && Enabled;
}

public sealed record CarrierState
{
    public static readonly CarrierState None = new();

    /// <summary>The empty state for a squadron's carrier (#230).</summary>
    public static readonly CarrierState NoSquadron = new() { IsSquadron = true };

    /// <summary>The callsign, which is the carrier's stable identity and never changes.</summary>
    public string? CallSign { get; init; }

    /// <summary>The name the Commander gave it.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// The carrier as Elite writes it for display — name and callsign in one string, <c>"Sacred Fire
    /// BNH-T2F"</c> — or null until something carrying it has been vouched by id (#109).
    /// </summary>
    public string? DisplayName { get; init; }

    public long? CarrierId { get; init; }

    /// <summary>Where it is now.</summary>
    public string? StarSystem { get; init; }

    /// <summary>
    /// When <see cref="StarSystem"/> was last reported, from the timestamp of the event that reported
    /// it (#406).
    /// </summary>
    public DateTimeOffset? SeenAt { get; init; }

    /// <summary>Where it is going, once a jump is scheduled.</summary>
    public string? DestinationSystem { get; init; }

    /// <summary>When the scheduled jump fires, as Elite reports it.</summary>
    public DateTimeOffset? DepartureTime { get; init; }

    /// <summary>Tritium in the carrier's own tank, from CarrierStats.</summary>
    public int? FuelLevel { get; init; }

    /// <summary>"all", "squadron", "squadronfriends", "friends", "none" — as Elite words it.</summary>
    public string? DockingAccess { get; init; }

    /// <summary>Tonnes of cargo aboard, from <c>CarrierStats.SpaceUsage.Cargo</c> (Phase 18).</summary>
    public int? CargoTonnes { get; init; }

    /// <summary>When the stats above were reported.</summary>
    public DateTimeOffset? StatsSeenAt { get; init; }

    /// <summary>The rest of what <c>CarrierStats</c> says, for the page that draws it (#230).</summary>
    public long? Balance { get; init; }

    /// <summary>Total capacity in tonnes, from <c>SpaceUsage.TotalCapacity</c>.</summary>
    public int? Capacity { get; init; }

    /// <summary>What is left of it, from <c>SpaceUsage.FreeSpace</c>.</summary>
    public int? FreeSpace { get; init; }

    /// <summary>How far it can jump now, in light years.</summary>
    public double? JumpRange { get; init; }

    /// <summary>Whether it is booked to be scrapped.</summary>
    public bool PendingDecommission { get; init; }

    /// <summary>The services aboard and whether each is open, in the order Elite reported them.</summary>
    public IReadOnlyList<CarrierService> Services { get; init; } = [];

    /// <summary>Where it is going, and where it will park when it gets there.</summary>
    public string? DestinationBody { get; init; }

    /// <summary>How full it is, 0 to 1, or null when nothing has said how big it is.</summary>
    public double? HowFull =>
        Capacity is > 0 && FreeSpace is { } free ? (Capacity.Value - free) / (double)Capacity.Value : null;

    public bool Owned => CallSign is not null;

    /// <summary>
    /// Whether this state holds anything worth reporting — a callsign, a system, or both (#406).
    /// </summary>
    public bool IsKnown => Owned || StarSystem is { Length: > 0 };

    public bool JumpScheduled => DestinationSystem is not null;

    /// <summary>
    /// Whether an event is about the Commander's own fleet carrier rather than a squadron's (reported
    /// 2026-08-21: "That's not where my Fleet Carrier is").
    /// </summary>
    private static List<CarrierService> Crew(JournalEvent journalEvent) =>
        [.. journalEvent.Items("Crew")
            .Select(member => new CarrierService(
                member.String("CrewRole") ?? string.Empty,
                member.Bool("Activated"),
                member.Bool("Enabled"),
                member.String("CrewName") ?? string.Empty))
            .Where(service => service.Role.Length > 0)];

    private bool SaysMyCallsign(JournalEvent journalEvent) =>
        CarrierId is { } id
        && journalEvent.Long("MarketID") == id
        && string.Equals(
            journalEvent.String("StationType"), "FleetCarrier", StringComparison.OrdinalIgnoreCase)
        && journalEvent.String("StationName") is { Length: > 0 };

    /// <summary>The carrier's name out of a string that ends with its callsign, or null.</summary>
    private string? NameWithoutCallsign(string? decorated)
    {
        if (CallSign is not { Length: > 0 } callsign || decorated is not { Length: > 0 })
        {
            return null;
        }

        var tail = " " + callsign;

        if (!decorated.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = decorated[..^tail.Length].Trim();

        return name.Length > 0 ? name : null;
    }

    /// <summary>
    /// Whether this state is following a squadron's carrier rather than the Commander's own (#230).
    /// </summary>
    public bool IsSquadron { get; init; }

    /// <summary>Whether this event is about the carrier this state is following.</summary>
    private bool Mine(JournalEvent journalEvent)
    {
        var type = journalEvent.String("CarrierType");
        var said = type is { Length: > 0 };

        var fleet = said && string.Equals(type, "FleetCarrier", StringComparison.OrdinalIgnoreCase);
        var squadron = said && string.Equals(type, "SquadronCarrier", StringComparison.OrdinalIgnoreCase);

        if (!IsSquadron)
        {
            return !squadron;
        }

        if (fleet)
        {
            return false;
        }

        return squadron || Names(journalEvent);
    }

    /// <summary>Whether an event names the carrier id this state already holds.</summary>
    private bool Names(JournalEvent journalEvent) =>
        CarrierId is { } id
        && (journalEvent.Long("CarrierID") == id || journalEvent.Long("MarketID") == id);

    public CarrierState Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return Mine(journalEvent) ? Folded(journalEvent) : this;
    }

    private CarrierState Folded(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        // The callsign, learned at the airlock — reported 2026-08-23 as "Carrier Captain and Tower
        // have not been talking to me, and I've been in and around the carrier all day". <see
        // cref="Owned"/> is the callsign being known, and until now only <c>CarrierStats</c> could supply
        // one: over the 925-journal corpus, 1,035 CarrierStats carry a Callsign and not one of 1,134
        // CarrierLocation events does — the read below is a hope, not a source.
        "Docked" or "Undocked" or "Location" or "DockingRequested" or "DockingGranted"
            when SaysMyCallsign(journalEvent) => this with
            {
                CallSign = journalEvent.String("StationName") ?? CallSign,
            },

        // The name, learned from a string that carries it decorated (#130).
        "SupercruiseDestinationDrop" when journalEvent.Long("MarketID") == CarrierId => this with
        {
            Name = Name ?? NameWithoutCallsign(journalEvent.String("Type")),
            DisplayName = journalEvent.String("Type") ?? DisplayName,
        },

        // The secondary, and it is safe only because the callsign was learned by id.
        // <c>ReceiveText</c>'s <c>From</c> carries the same decorated string 244 times in the corpus and
        // carries no id at all, so it cannot be trusted on shape — the Commander has a squadron carrier in
        // these same journals, and #28 already ruled that one must never be mistaken for their own.
        "ReceiveText" => this with
        {
            Name = Name ?? NameWithoutCallsign(journalEvent.String("From")),
        },

        // And the same rule reaches the event that actually carries it most often. Measured over this
        // Commander's corpus: of the 27 journals that name BNH-T2F with no CarrierStats in them, 22 name
        // it in an FSSSignalDiscovered, against 14 apiece for the other two.
        "FSSSignalDiscovered" => this with
        {
            Name = Name ?? NameWithoutCallsign(journalEvent.String("SignalName")),
        },

        "CarrierBuy" => this with
        {
            CallSign = journalEvent.String("Callsign") ?? CallSign,
            CarrierId = journalEvent.Long("CarrierID") ?? CarrierId,
            StarSystem = journalEvent.String("Location") ?? StarSystem,
            SeenAt = Stamped(journalEvent, journalEvent.String("Location")),
        },

        "CarrierStats" => this with
        {
            CallSign = journalEvent.String("Callsign") ?? CallSign,
            Name = journalEvent.String("Name") ?? Name,
            CarrierId = journalEvent.Long("CarrierID") ?? CarrierId,
            FuelLevel = journalEvent.Int("FuelLevel") ?? FuelLevel,
            DockingAccess = journalEvent.String("DockingAccess") ?? DockingAccess,
            CargoTonnes = journalEvent.Object("SpaceUsage")?.Int("Cargo") ?? CargoTonnes,
            StatsSeenAt = journalEvent.Timestamp,

            // The figures the carrier page draws (#230).
            Balance = journalEvent.Object("Finance")?.Long("CarrierBalance") ?? Balance,
            Capacity = journalEvent.Object("SpaceUsage")?.Int("TotalCapacity") ?? Capacity,
            FreeSpace = journalEvent.Object("SpaceUsage")?.Int("FreeSpace") ?? FreeSpace,
            JumpRange = journalEvent.Double("JumpRangeCurr") ?? JumpRange,
            PendingDecommission = journalEvent.Bool("PendingDecommission"),
            Services = Crew(journalEvent) is { Count: > 0 } crew ? crew : Services,
        },

        // The callsign read here has never once arrived — 0 of 1,134 across the corpus — and is left in place
        // because it costs nothing and would start working the day Frontier adds the field.
        "CarrierLocation" => this with
        {
            CallSign = journalEvent.String("Callsign") ?? CallSign,
            CarrierId = journalEvent.Long("CarrierID") ?? CarrierId,
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            SeenAt = Stamped(journalEvent, journalEvent.String("StarSystem")),
        },

        // The body as well as the system (#230).
        "CarrierJumpRequest" => this with
        {
            CarrierId = journalEvent.Long("CarrierID") ?? CarrierId,
            DestinationSystem = journalEvent.String("SystemName") ?? DestinationSystem,
            DepartureTime = ParseDeparture(journalEvent.String("DepartureTime")) ?? DepartureTime,

            // The body as well as the system (#230).
            DestinationBody = journalEvent.String("Body") ?? DestinationBody,
        },

        "CarrierJumpCancelled" => this with
        {
            DestinationSystem = null,
            DepartureTime = null,
            DestinationBody = null,
        },

        // The carrier has arrived.
        "CarrierJump" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? DestinationSystem ?? StarSystem,

            // The jump's own instant, but only where the jump itself named where it arrived — falling back to
            // the system already held would date a stale answer as fresh.
            SeenAt = Stamped(journalEvent, journalEvent.String("StarSystem") ?? DestinationSystem),
            DestinationSystem = null,
            DepartureTime = null,
            DestinationBody = null,
        },

        // Tritium in, tritium out.
        "CarrierDepositFuel" => this with { FuelLevel = journalEvent.Int("Total") ?? FuelLevel },

        _ => this,
    };

    /// <summary>The event's own instant, but only where the event actually named a system (#406).</summary>
    private DateTimeOffset? Stamped(JournalEvent journalEvent, string? system) =>
        system is { Length: > 0 } ? journalEvent.Timestamp : SeenAt;

    /// <summary>Elite writes the departure time as an ISO 8601 string.</summary>
    private static DateTimeOffset? ParseDeparture(string? value) =>
        DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
}
