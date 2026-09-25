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

    /// <summary>Tonnes of tritium in the hold, counted from cargo movements — null until one is seen.</summary>
    public int? TritiumInHold { get; init; }

    /// <summary>
    /// Whether <see cref="TritiumInHold"/> might be wrong: an open trade order lets other Commanders
    /// move tritium the journal never reports.
    /// </summary>
    public bool TritiumInHoldUncertain { get; init; }

    /// <summary>
    /// Whether the Commander is docked at their own carrier — CargoTransfer carries no carrier id of
    /// its own, so it is attributed to this carrier only while this is true.
    /// </summary>
    public bool DockedAtOwnCarrier { get; init; }

    /// <summary>"all", "squadron", "squadronfriends", "friends", "none" — as Elite words it.</summary>
    public string? DockingAccess { get; init; }

    /// <summary>Tonnes of cargo aboard, from <c>CarrierStats.SpaceUsage.Cargo</c> (Phase 18).</summary>
    public int? CargoTonnes { get; init; }

    /// <summary>When the stats above were reported.</summary>
    public DateTimeOffset? StatsSeenAt { get; init; }

    /// <summary>The balance last recorded, by <c>CarrierStats</c> or <c>CarrierBankTransfer</c>.</summary>
    public long? Balance { get; init; }

    /// <summary>When <see cref="Balance"/> was recorded.</summary>
    public DateTimeOffset? BalanceSeenAt { get; init; }

    /// <summary>
    /// The upkeep Elite takes at each weekly tick, from the latest interval between two recorded balances
    /// that held a tick, a fall, and no spending (#447).
    /// </summary>
    public long? WeeklyUpkeep { get; init; }

    /// <summary>Whether something besides upkeep has been spent since <see cref="Balance"/> was recorded.</summary>
    public bool SpentSinceBalance { get; init; }

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
    /// This recovered state with what <paramref name="newer"/> has set laid over it. A newer state that
    /// holds a callsign is taken whole.
    /// </summary>
    public CarrierState With(CarrierState newer)
    {
        ArgumentNullException.ThrowIfNull(newer);

        if (newer.Owned)
        {
            return newer;
        }

        return this with
        {
            Name = newer.Name ?? Name,
            DisplayName = newer.DisplayName ?? DisplayName,
            CarrierId = newer.CarrierId ?? CarrierId,
            StarSystem = newer.StarSystem ?? StarSystem,
            SeenAt = newer.SeenAt ?? SeenAt,
            DestinationSystem = newer.DestinationSystem ?? DestinationSystem,
            DepartureTime = newer.DepartureTime ?? DepartureTime,
            DestinationBody = newer.DestinationBody ?? DestinationBody,
            FuelLevel = newer.FuelLevel ?? FuelLevel,
            TritiumInHold = newer.TritiumInHold ?? TritiumInHold,
            TritiumInHoldUncertain = newer.TritiumInHoldUncertain || TritiumInHoldUncertain,
        };
    }

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
        "Docked" when SaysMyCallsign(journalEvent) => this with
        {
            CallSign = journalEvent.String("StationName") ?? CallSign,
            DockedAtOwnCarrier = true,
        },

        "Undocked" when SaysMyCallsign(journalEvent) => this with
        {
            CallSign = journalEvent.String("StationName") ?? CallSign,
            DockedAtOwnCarrier = false,
        },

        "Location" or "DockingRequested" or "DockingGranted" when SaysMyCallsign(journalEvent) => this with
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

        "CarrierStats" => Recorded(
            journalEvent.Object("Finance")?.Long("CarrierBalance"), journalEvent.Timestamp) with
        {
            CallSign = journalEvent.String("Callsign") ?? CallSign,
            Name = journalEvent.String("Name") ?? Name,
            CarrierId = journalEvent.Long("CarrierID") ?? CarrierId,
            FuelLevel = journalEvent.Int("FuelLevel") ?? FuelLevel,
            DockingAccess = journalEvent.String("DockingAccess") ?? DockingAccess,
            CargoTonnes = journalEvent.Object("SpaceUsage")?.Int("Cargo") ?? CargoTonnes,
            StatsSeenAt = journalEvent.Timestamp,

            // The figures the carrier page draws (#230).
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

        // Tritium in, tritium out — from the ship's own hold into the tank, not the carrier's hold.
        "CarrierDepositFuel" => this with { FuelLevel = journalEvent.Int("Total") ?? FuelLevel },

        // CargoTransfer carries no carrier id of its own, so it counts only while docked here.
        "CargoTransfer" when DockedAtOwnCarrier
            && journalEvent.Items("Transfers").Any(transfer => NamesTritium(transfer.String("Type")))
            => Moved(TritiumTransferDelta(journalEvent)),

        "MarketSell" when journalEvent.Long("MarketID") == CarrierId
            && NamesTritium(journalEvent.String("Type")) => Moved(journalEvent.Int("Count") ?? 0),

        "MarketBuy" when journalEvent.Long("MarketID") == CarrierId
            && NamesTritium(journalEvent.String("Type")) => Moved(-(journalEvent.Int("Count") ?? 0)),

        // Another Commander can fill the order and the journal never says by how much.
        "CarrierTradeOrder" when journalEvent.Long("CarrierID") == CarrierId
            && NamesTritium(journalEvent.String("Commodity"))
            && !journalEvent.Bool("CancelTrade") => this with
            {
                TritiumInHoldUncertain = true,
                SpentSinceBalance = true,
            },

        // The measured drop is the balance before the transfer, so a deposit is not read as upkeep.
        "CarrierBankTransfer" when CarrierId is null || journalEvent.Long("CarrierID") == CarrierId =>
            Recorded(
                journalEvent.Long("CarrierBalance"),
                journalEvent.Timestamp,
                journalEvent.Long("CarrierBalance") is { } after
                    ? after - (journalEvent.Long("Deposit") ?? 0) + (journalEvent.Long("Withdraw") ?? 0)
                    : null),

        "CarrierTradeOrder" or "CarrierCrewServices" or "CarrierModulePack" or "CarrierShipPack"
            when journalEvent.Long("CarrierID") == CarrierId => this with { SpentSinceBalance = true },

        _ => this,
    };

    /// <summary>
    /// A newly recorded balance, taking a new <see cref="WeeklyUpkeep"/> from the drop since the last one
    /// where the interval allows it. <paramref name="measured"/> is the balance to compare, where it
    /// differs from the one recorded.
    /// </summary>
    private CarrierState Recorded(long? balance, DateTimeOffset at, long? measured = null)
    {
        if (balance is not { } recorded)
        {
            return this;
        }

        var weekly = WeeklyUpkeep;
        var compared = measured ?? recorded;

        if (!IsSquadron
            && !SpentSinceBalance
            && Balance is { } before
            && BalanceSeenAt is { } since
            && CarrierUpkeep.TicksBetween(since, at) is var ticks and > 0
            && compared < before)
        {
            weekly = (before - compared) / ticks;
        }

        return this with
        {
            Balance = recorded,
            BalanceSeenAt = at,
            WeeklyUpkeep = weekly,
            SpentSinceBalance = false,
        };
    }

    /// <summary>Whether a commodity symbol, cased however Elite wrote it, names tritium.</summary>
    private static bool NamesTritium(string? symbol) =>
        string.Equals(symbol, "tritium", StringComparison.OrdinalIgnoreCase);

    /// <summary>The net tritium a CargoTransfer moved into (positive) or out of (negative) the hold.</summary>
    private static int TritiumTransferDelta(JournalEvent journalEvent) =>
        journalEvent.Items("Transfers")
            .Where(transfer => NamesTritium(transfer.String("Type")))
            .Sum(transfer => transfer.String("Direction") switch
            {
                "tocarrier" => transfer.Int("Count") ?? 0,
                "toship" => -(transfer.Int("Count") ?? 0),
                _ => 0,
            });

    /// <summary>
    /// <see cref="TritiumInHold"/> moved by <paramref name="delta"/>, held at zero and marked
    /// uncertain rather than going negative.
    /// </summary>
    private CarrierState Moved(int delta)
    {
        var next = (TritiumInHold ?? 0) + delta;

        return next < 0
            ? this with { TritiumInHold = 0, TritiumInHoldUncertain = true }
            : this with { TritiumInHold = next };
    }

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
