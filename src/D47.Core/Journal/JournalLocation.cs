namespace D47.Core.Journal;

/// <summary>What the Commander is doing with themselves, as far as the journal reveals it.</summary>
public enum FlightMode
{
    Unknown,

    /// <summary>Normal space, under thrusters.</summary>
    Normal,

    Supercruise,

    /// <summary>Between systems. Set by StartJump and cleared by the FSDJump that lands.</summary>
    Hyperspace,

    Docked,

    /// <summary>Ship on a surface.</summary>
    Landed,

    /// <summary>Out of the ship — on a planet, in a station, or in an SRV.</summary>
    OnFoot,
}

/// <summary>
/// Where a Commander is and what they are doing there (list.md Phase 7, "Know your location").
/// <para>
/// Every field here is folded from an event that states it. Nothing is inferred from absence:
/// an event this type does not recognise leaves the location alone rather than clearing it,
/// because an unrelated event is not evidence that the Commander's position is now unknown.
/// </para>
/// </summary>
public sealed record JournalLocation(string? StarSystem, string? Body, bool Docked, string? StationName)
{
    public static readonly JournalLocation Unknown = new(null, null, false, null);

    /// <summary>Planet, star, station — as Elite words it, so d47 never invents a classification.</summary>
    public string? BodyType { get; init; }

    /// <summary>Coriolis, Outpost, FleetCarrier and so on. Null when not docked.</summary>
    public string? StationType { get; init; }

    public FlightMode Mode { get; init; } = FlightMode.Unknown;

    /// <summary>
    /// The next system on the route, from FSDTarget. Phase 8's route warnings read this, so the
    /// fuel warning and the "where am I" answer cannot disagree about where the Commander is
    /// heading.
    /// </summary>
    public string? NextJumpSystem { get; init; }

    /// <summary>The next system's star class, which is what decides whether it can be scooped.</summary>
    public string? NextJumpStarClass { get; init; }

    /// <summary>
    /// The class of the star the Commander is at now (list.md Phase 18, "Read a system name" —
    /// a variant's colour follows the star, and the variant is what sets an organic's price).
    /// <para>
    /// <b>It comes from the <c>FSDTarget</c> that preceded the jump, not from the arrival scan.</b>
    /// Measured over 7,412 jumps in the corpus: <c>FSDTarget</c> named the system being entered on
    /// <b>99.7%</b> of them, while a main-star <c>Scan</c> followed on only <b>28.6%</b>. The
    /// obvious source is the one that is usually not there.
    /// </para>
    /// <para>
    /// Null where d47 did not see the Commander arrive — logging in, or a carrier jump — because
    /// "I did not watch you get here" is a different answer from "this star has no class".
    /// </para>
    /// </summary>
    public string? StarClass { get; init; }

    /// <summary>Jumps left in the plotted route, from FSDTarget. Null when nothing is plotted.</summary>
    public int? JumpsRemaining { get; init; }

    /// <summary>
    /// Fuel in the main tank as of the last event that reported it — FSDJump and FuelScoop both
    /// do. Status.json carries it continuously, and Phase 8's low-fuel warning prefers that;
    /// this is what is known between those writes.
    /// </summary>
    public double? FuelMain { get; init; }

    /// <summary>
    /// The Power controlling this system, or null where nobody does (list.md Phase 15).
    /// <para>
    /// <b>Absent means unoccupied.</b> Elite omits the field entirely rather than writing null —
    /// 1,932 of 4,891 measured jumps have no <c>ControllingPower</c> at all — so null here is a
    /// fact about the system rather than a gap in what was read.
    /// </para>
    /// <para>
    /// It arrives only on the three events that state where the Commander now is:
    /// <c>Location</c>, <c>FSDJump</c> and <c>CarrierJump</c>. There is no <c>SupercruiseExit</c>
    /// copy, which is why anything reading this reads it from the location rather than waiting for
    /// the event that drops them into normal space.
    /// </para>
    /// </summary>
    public string? ControllingPower { get; init; }

    /// <summary>Docked at a fleet carrier, which is a station type rather than a separate place.</summary>
    public bool AtCarrier => StationType is "FleetCarrier";

    /// <summary>
    /// Folds one event into the current location. The switch covers the events that state
    /// position; everything else falls through unchanged.
    /// </summary>
    public JournalLocation Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        // CarrierJump shares Location's shape and means the same thing — this is where the
        // Commander now is — so it folds through the same arm. Without it, a Commander carried
        // into another system keeps the previous one's name and Power until they next jump under
        // their own drive, which is exactly the stale reading Phase 15's territory warning would
        // announce as fact.
        "Location" or "CarrierJump" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            Body = journalEvent.String("Body") ?? Body,
            BodyType = journalEvent.String("BodyType") ?? BodyType,
            Docked = journalEvent.Bool("Docked"),
            StationName = journalEvent.String("StationName") ?? StationName,
            StationType = journalEvent.String("StationType") ?? StationType,

            // Assigned rather than coalesced, unlike everything above it. Absent means nobody
            // controls this system, so keeping the last system's Power would be the one mistake
            // that matters here.
            ControllingPower = journalEvent.String("ControllingPower"),

            // Neither event carries a star class, and both can move the Commander somewhere new —
            // so a class carried over would describe the system they left. Kept only where the
            // system has not changed, which is the login-in-place case.
            StarClass = journalEvent.String("StarSystem") == StarSystem ? StarClass : null,

            Mode = journalEvent.Bool("OnFoot") ? FlightMode.OnFoot
                : journalEvent.Bool("Docked") ? FlightMode.Docked
                : FlightMode.Normal,
        },

        // The arrival, not the departure. StartJump sets Hyperspace; this is what clears it.
        "FSDJump" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            Body = journalEvent.String("Body") ?? Body,
            BodyType = journalEvent.String("BodyType") ?? BodyType,
            Docked = false,
            StationName = null,
            StationType = null,
            Mode = FlightMode.Supercruise,
            FuelMain = journalEvent.Double("FuelLevel") ?? FuelMain,
            ControllingPower = journalEvent.String("ControllingPower"),

            // Not discarded on arrival — moved. The class the route named for the system being
            // entered is the class of the star now outside the canopy, and it is the only reliable
            // report of it. Guarded on the names matching so a jump that ended somewhere other than
            // the plotted target carries no class rather than the wrong one.
            StarClass = journalEvent.String("StarSystem") == NextJumpSystem ? NextJumpStarClass : null,

            // Consumed by arriving. Leaving it set would have Phase 8 warning about a star the
            // Commander is now sitting next to.
            NextJumpSystem = null,
            NextJumpStarClass = null,
        },

        "Docked" => this with
        {
            Docked = true,
            StationName = journalEvent.String("StationName") ?? StationName,
            StationType = journalEvent.String("StationType") ?? StationType,
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            Mode = FlightMode.Docked,
        },

        "Undocked" => this with
        {
            Docked = false,
            StationName = null,
            StationType = null,
            Mode = FlightMode.Normal,
        },

        "SupercruiseEntry" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            Mode = FlightMode.Supercruise,

            // Supercruise is system-scale travel; whichever body was being approached is behind us.
            Body = null,
            BodyType = null,
        },

        "SupercruiseExit" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            Body = journalEvent.String("Body") ?? Body,
            BodyType = journalEvent.String("BodyType") ?? BodyType,
            Mode = FlightMode.Normal,
        },

        "ApproachBody" => this with
        {
            StarSystem = journalEvent.String("StarSystem") ?? StarSystem,
            Body = journalEvent.String("Body") ?? Body,
        },

        // Leaving orbit says which body was left, not which one is next. Clearing the body is
        // the honest answer; naming the one behind us would be worse than saying nothing.
        "LeaveBody" => this with { Body = null, BodyType = null },

        "Touchdown" => this with
        {
            Body = journalEvent.String("Body") ?? Body,
            Mode = FlightMode.Landed,
        },

        "Liftoff" => this with { Mode = FlightMode.Normal },

        // Disembark leaves the ship; Embark returns to one. Boarding an SRV is still out of the
        // ship as far as anything reading this is concerned.
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

        // Charging for a jump. Only a hyperspace one changes the mode — JumpType is
        // "Supercruise" for the far more common case, and treating that as hyperspace would
        // have Phase 8 remarking on the length of every supercruise entry.
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
