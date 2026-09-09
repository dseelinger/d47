namespace D47.Core.Journal;

/// <summary>What the Commander is doing with themselves, as far as the journal reveals it.</summary>
public enum FlightMode
{
    Unknown,

    /// <summary>Normal space, under thrusters.</summary>
    Normal,

    Supercruise,

    /// <summary>Between systems.</summary>
    Hyperspace,

    Docked,

    /// <summary>Ship on a surface.</summary>
    Landed,

    /// <summary>Out of the ship — on a planet, in a station, or in an SRV.</summary>
    OnFoot,
}

/// <summary>Where a Commander is and what they are doing there (Phase 7, "Know your location").</summary>
public sealed record JournalLocation(string? StarSystem, string? Body, bool Docked, string? StationName)
{
    public static readonly JournalLocation Unknown = new(null, null, false, null);

    /// <summary>Frontier's own id for the system, from the event that stated where the Commander is.</summary>
    public long? SystemAddress { get; init; }

    /// <summary>
    /// The <c>MarketID</c> of the station the Commander is docked at, and null when they are not (Phase
    /// 47).
    /// </summary>
    public long? MarketId { get; init; }

    /// <summary>
    /// The <c>BodyID</c> of the body being approached, orbited, landed on or left, null between bodies.
    /// </summary>
    public int? BodyId { get; init; }

    /// <summary>
    /// Where the system is, in light years on Frontier's axes (Phase 28, "Where every engineer is").
    /// </summary>
    public StarPosition? StarPos { get; init; }

    /// <summary>Planet, star, station — as Elite words it, so d47 never invents a classification.</summary>
    public string? BodyType { get; init; }

    /// <summary>Coriolis, Outpost, FleetCarrier and so on.</summary>
    public string? StationType { get; init; }

    public FlightMode Mode { get; init; } = FlightMode.Unknown;

    /// <summary>The next system on the route, from FSDTarget.</summary>
    public string? NextJumpSystem { get; init; }

    /// <summary>The next system's star class, which is what decides whether it can be scooped.</summary>
    public string? NextJumpStarClass { get; init; }

    /// <summary>
    /// The class of the star the Commander is at now (Phase 18, "Read a system name" — a variant's
    /// colour follows the star, and the variant is what sets an organic's price).
    /// </summary>
    public string? StarClass { get; init; }

    /// <summary>Jumps left in the plotted route, from FSDTarget.</summary>
    public int? JumpsRemaining { get; init; }

    /// <summary>
    /// Fuel in the main tank as of the last event that reported it — FSDJump and FuelScoop both do.
    /// </summary>
    public double? FuelMain { get; init; }

    /// <summary>The Power controlling this system, or null where nobody does (Phase 15).</summary>
    public string? ControllingPower { get; init; }

    /// <summary>Docked at a fleet carrier, which is a station type rather than a separate place.</summary>
    public bool AtCarrier => StationType is "FleetCarrier";

    /// <summary>The address for an event that states which system the Commander is in.</summary>
    private static long? Addressed(JournalEvent journalEvent, long? current) =>
        journalEvent.String("StarSystem") is null ? current : journalEvent.Long("SystemAddress");

    /// <summary>
    /// The position for such an event, on the same rule and for a sharper version of the same reason.
    /// </summary>
    private static StarPosition? Placed(JournalEvent journalEvent, StarPosition? current) =>
        journalEvent.String("StarSystem") is null ? current : StarPosition.Read(journalEvent.Raw);

    /// <summary>Folds one event into the current location.</summary>
    public JournalLocation Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        // CarrierJump shares Location's shape and means the same thing — this is where the Commander now is —
        // so it folds through the same arm.
        "Location" or "CarrierJump" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            SystemAddress = Addressed(journalEvent, SystemAddress),
            StarPos = Placed(journalEvent, StarPos),
            Body = journalEvent.String("Body") ?? Body,
            BodyType = journalEvent.String("BodyType") ?? BodyType,
            Docked = journalEvent.Bool("Docked"),
            StationName = journalEvent.String("StationName") ?? StationName,
            StationType = journalEvent.String("StationType") ?? StationType,
            MarketId = journalEvent.Bool("Docked") ? journalEvent.Long("MarketID") : null,
            BodyId = journalEvent.Int("BodyID"),

            // Assigned rather than coalesced, unlike everything above it.
            ControllingPower = journalEvent.String("ControllingPower"),

            // Neither event carries a star class, and both can move the Commander somewhere new — so a class
            // carried over would describe the system they left.
            StarClass = journalEvent.String("StarSystem") == StarSystem ? StarClass : null,

            Mode = journalEvent.Bool("OnFoot") ? FlightMode.OnFoot
                : journalEvent.Bool("Docked") ? FlightMode.Docked
                : FlightMode.Normal,
        },

        // The arrival, not the departure.
        "FSDJump" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            SystemAddress = Addressed(journalEvent, SystemAddress),
            StarPos = Placed(journalEvent, StarPos),
            Body = journalEvent.String("Body") ?? Body,
            BodyType = journalEvent.String("BodyType") ?? BodyType,
            Docked = false,
            StationName = null,
            StationType = null,
            MarketId = null,
            BodyId = journalEvent.Int("BodyID"),
            Mode = FlightMode.Supercruise,
            FuelMain = journalEvent.Double("FuelLevel") ?? FuelMain,
            ControllingPower = journalEvent.String("ControllingPower"),

            // Not discarded on arrival — moved.
            StarClass = journalEvent.String("StarSystem") == NextJumpSystem ? NextJumpStarClass : null,

            // Consumed by arriving.
            NextJumpSystem = null,
            NextJumpStarClass = null,
        },

        "Docked" => this with
        {
            Docked = true,
            StationName = journalEvent.String("StationName") ?? StationName,
            StationType = journalEvent.String("StationType") ?? StationType,
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            SystemAddress = Addressed(journalEvent, SystemAddress),
            MarketId = journalEvent.Long("MarketID") ?? MarketId,
            Mode = FlightMode.Docked,
        },

        "Undocked" => this with
        {
            Docked = false,
            StationName = null,
            StationType = null,
            MarketId = null,
            Mode = FlightMode.Normal,
        },

        "SupercruiseEntry" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            Mode = FlightMode.Supercruise,

            // Supercruise is system-scale travel; whichever body was being approached is behind us.
            Body = null,
            BodyType = null,
            BodyId = null,
        },

        "SupercruiseExit" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            Body = journalEvent.String("Body") ?? Body,
            BodyType = journalEvent.String("BodyType") ?? BodyType,
            BodyId = journalEvent.Int("BodyID") ?? BodyId,
            Mode = FlightMode.Normal,
        },

        "ApproachBody" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            Body = journalEvent.String("Body") ?? Body,
            BodyId = journalEvent.Int("BodyID") ?? BodyId,
        },

        // Leaving orbit says which body was left, not which one is next.
        "LeaveBody" => this with { Body = null, BodyType = null, BodyId = null },

        "Touchdown" => this with
        {
            Body = journalEvent.String("Body") ?? Body,
            BodyId = journalEvent.Int("BodyID") ?? BodyId,
            SystemAddress = Addressed(journalEvent, SystemAddress),
            Mode = FlightMode.Landed,
        },

        "Liftoff" => this with { Mode = FlightMode.Normal },

        // Disembark leaves the ship; Embark returns to one.
        "Disembark" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            Body = journalEvent.String("Body") ?? Body,
            Mode = FlightMode.OnFoot,
        },

        "Embark" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            Body = journalEvent.String("Body") ?? Body,
            Mode = journalEvent.Bool("SRV") ? FlightMode.OnFoot
                : journalEvent.Bool("OnStation") ? FlightMode.Docked
                : FlightMode.Normal,
        },

        // Charging for a jump.
        "StartJump" => journalEvent.String("JumpType") == "Hyperspace"
            ? this with
            {
                Mode = FlightMode.Hyperspace,
                NextJumpSystem = journalEvent.String("StarSystem") ?? NextJumpSystem,
                NextJumpStarClass = journalEvent.String("StarClass") ?? NextJumpStarClass,
            }
            : this,

        "FSDTarget" => this with
        {
            NextJumpSystem = journalEvent.String("Name") ?? NextJumpSystem,
            NextJumpStarClass = journalEvent.String("StarClass") ?? NextJumpStarClass,
            JumpsRemaining = journalEvent.Int("RemainingJumpsInRoute") ?? JumpsRemaining,
        },

        "FuelScoop" => this with { FuelMain = journalEvent.Double("Total") ?? FuelMain },

        _ => this,
    };
}
