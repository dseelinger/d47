namespace D47.Core.Journal;

/// <summary>
/// The Commander's fleet carrier, if they own one (list.md Phase 7, "Know your location and
/// your carrier (if owned)").
/// <para>
/// Ownership is established by an event that only an owner receives — CarrierBuy, CarrierStats
/// and the rest are written to the owner's journal alone. So <see cref="Owned"/> being false
/// means "no carrier event has ever been seen", which for a Commander who owns one and has not
/// interacted with it this session is indistinguishable from not owning one. That is stated
/// rather than papered over: the answer is "I have not seen your carrier this session", not
/// "you do not have one".
/// </para>
/// </summary>
public sealed record CarrierState
{
    public static readonly CarrierState None = new();

    /// <summary>The callsign, which is the carrier's stable identity and never changes.</summary>
    public string? CallSign { get; init; }

    /// <summary>The name the Commander gave it.</summary>
    public string? Name { get; init; }

    public long? CarrierId { get; init; }

    /// <summary>Where it is now.</summary>
    public string? StarSystem { get; init; }

    /// <summary>Where it is going, once a jump is scheduled. Cleared on arrival or cancellation.</summary>
    public string? DestinationSystem { get; init; }

    /// <summary>When the scheduled jump fires, as Elite reports it.</summary>
    public DateTimeOffset? DepartureTime { get; init; }

    /// <summary>Tritium in the carrier's own tank, from CarrierStats.</summary>
    public int? FuelLevel { get; init; }

    /// <summary>"all", "squadron", "squadronfriends", "friends", "none" — as Elite words it.</summary>
    public string? DockingAccess { get; init; }

    public bool Owned => CallSign is not null;

    public bool JumpScheduled => DestinationSystem is not null;

    public CarrierState Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "CarrierBuy" => this with
        {
            CallSign = journalEvent.String("Callsign") ?? CallSign,
            CarrierId = journalEvent.Long("CarrierID") ?? CarrierId,
            StarSystem = journalEvent.String("Location") ?? StarSystem,
        },

        "CarrierStats" => this with
        {
            CallSign = journalEvent.String("Callsign") ?? CallSign,
            Name = journalEvent.String("Name") ?? Name,
            CarrierId = journalEvent.Long("CarrierID") ?? CarrierId,
            FuelLevel = journalEvent.Int("FuelLevel") ?? FuelLevel,
            DockingAccess = journalEvent.String("DockingAccess") ?? DockingAccess,
        },

        "CarrierLocation" => this with
        {
            CallSign = journalEvent.String("Callsign") ?? CallSign,
            CarrierId = journalEvent.Long("CarrierID") ?? CarrierId,
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
        },

        "CarrierJumpRequest" => this with
        {
            CarrierId = journalEvent.Long("CarrierID") ?? CarrierId,
            DestinationSystem = journalEvent.String("SystemName") ?? DestinationSystem,
            DepartureTime = ParseDeparture(journalEvent.String("DepartureTime")) ?? DepartureTime,
        },

        "CarrierJumpCancelled" => this with { DestinationSystem = null, DepartureTime = null },

        // The carrier has arrived. Written to the owner's journal whether or not they were
        // aboard for it, which is what makes "where is my carrier" answerable after leaving it
        // parked and flying somewhere else.
        "CarrierJump" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? DestinationSystem ?? StarSystem,
            DestinationSystem = null,
            DepartureTime = null,
        },

        // Tritium in, tritium out. Neither is the whole picture — CarrierStats is — but between
        // stats requests these are the only reports of the level changing.
        "CarrierDepositFuel" => this with { FuelLevel = journalEvent.Int("Total") ?? FuelLevel },

        _ => this,
    };

    /// <summary>
    /// Elite writes the departure time as an ISO 8601 string. An unparseable one is dropped
    /// rather than defaulted: a wrong departure time is worse than no departure time, since the
    /// whole use of it is deciding whether there is time to get back.
    /// </summary>
    private static DateTimeOffset? ParseDeparture(string? value) =>
        DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
}
