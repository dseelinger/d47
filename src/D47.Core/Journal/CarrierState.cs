namespace D47.Core.Journal;

/// <summary>
/// The Commander's fleet carrier, if they own one (Phase 7, "Know your location and
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

    /// <summary>
    /// The carrier as Elite writes it for display — name and callsign in one string, <c>"Sacred
    /// Fire BNH-T2F"</c> — or null until something carrying it has been vouched by id
    /// (<a href="https://github.com/dseelinger/d47/issues/109">#109</a>).
    /// <para>
    /// <b>Its own field rather than written into <see cref="Name"/>, because it is not the name.</b>
    /// <c>CarrierStats</c> says the name outright and this is a display string that happens to
    /// contain one; storing it as though Frontier had named the carrier would make every surface
    /// that prefers the name say <i>"welcome home to Sacred Fire BNH-T2F"</i>. It exists to be
    /// <em>matched against</em>, which is a different job from being said.
    /// </para>
    /// <para>
    /// <b>And it is the earliest identity there is.</b> The callsign is learned at the airlock and
    /// the name from <c>CarrierStats</c>; docking chatter is by definition the traffic that happens
    /// before the airlock, so both arrive too late for it. In the reported session this landed at
    /// 21:42:15 and the first message at 21:42:27 — twelve seconds, against the forty-seven the
    /// dock would have cost.
    /// </para>
    /// </summary>
    public string? DisplayName { get; init; }

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

    /// <summary>
    /// Tonnes of cargo aboard, from <c>CarrierStats.SpaceUsage.Cargo</c> (Phase 18).
    /// <para>
    /// <b>A total, and there is no manifest anywhere behind it.</b> Nothing Elite writes says what
    /// those tonnes are. The only per-commodity signal is <c>CargoTransfer</c>, and deriving stock
    /// from it was measured and fails: reconciled against this very figure across the corpus it was
    /// <b>wrong 679 times against right 347</b>, and it drove 11 commodities negative — transfers
    /// out of stock that arrived by a route the journal never itemises (the carrier's own market,
    /// another Commander's delivery, anything bought before the file d47 is reading). So d47 says
    /// how much and refuses to say what, which is the honest half rather than the useful-sounding
    /// one. Measured 2026-08-16; see <c>docs/spikes/colonisation-sources.md</c>.
    /// </para>
    /// </summary>
    public int? CargoTonnes { get; init; }

    /// <summary>
    /// When the stats above were reported. The figure only refreshes when the Commander opens the
    /// carrier management panel, so it needs its age said beside it for the same reason a
    /// construction site does.
    /// </summary>
    public DateTimeOffset? StatsSeenAt { get; init; }

    public bool Owned => CallSign is not null;

    public bool JumpScheduled => DestinationSystem is not null;

    /// <summary>
    /// Whether an event is about the Commander's own fleet carrier rather than a squadron's
    /// (reported 2026-08-21: <i>"That's not where my Fleet Carrier is"</i>).
    /// <para>
    /// <b>Elite writes both to the same journal, seconds apart, and this state kept whichever
    /// arrived last.</b> Measured over the 920-journal corpus: 628 <c>CarrierLocation</c> events
    /// say <c>FleetCarrier</c> and 267 say <c>SquadronCarrier</c>, 173 journals carry both, and in
    /// <b>152 of those 173 the squadron one is the last</b>. So a Commander in a squadron with a
    /// carrier was told their own carrier was wherever the squadron's happened to be — reliably,
    /// and with the right name on it, because the name comes from <c>CarrierStats</c>.
    /// </para>
    /// <para>
    /// <b>An absent <c>CarrierType</c> is accepted, and that is not a gap.</b> Frontier added the
    /// field partway through: all 223 <c>CarrierLocation</c> events without it are the same single
    /// carrier id, so there is nothing to tell apart in the journals that predate it.
    /// </para>
    /// <para>
    /// <b><c>CarrierStats</c> is not the discriminator</b>, which is worth writing down because it
    /// looks like one. One account in the corpus receives <c>CarrierStats</c> for two carrier ids
    /// in the same journal — its own and a squadron's — so pinning the id from stats would pin the
    /// wrong carrier as readily as this fixes it.
    /// </para>
    /// </summary>
    /// <summary>
    /// Whether this docking event is at the carrier this state is already about — its MarketID is
    /// the carrier id, and it says so is a fleet carrier. Both, because a MarketID is unique across
    /// every station in the galaxy and the type check costs nothing to state.
    /// </summary>
    private bool SaysMyCallsign(JournalEvent journalEvent) =>
        CarrierId is { } id
        && journalEvent.Long("MarketID") == id
        && string.Equals(
            journalEvent.String("StationType"), "FleetCarrier", StringComparison.OrdinalIgnoreCase)
        && journalEvent.String("StationName") is { Length: > 0 };

    /// <summary>
    /// The carrier's name out of a string that ends with its callsign, or null.
    /// <para>
    /// <b>Exactly the known callsign, and only when one is known.</b> Nothing is parsed
    /// speculatively: no pattern is matched, no shape is guessed, and a carrier d47 has not
    /// already identified yields nothing. The separating space is required too, so a callsign
    /// that happens to be a suffix of a longer word cannot strip one.
    /// </para>
    /// <para>
    /// Callers write <c>Name ?? …</c> rather than <c>… ?? Name</c>, which is what keeps
    /// <c>CarrierStats</c> the authority: Frontier said the name outright, so a name it supplies
    /// overrides a derived one and a derived one never replaces it.
    /// </para>
    /// </summary>
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

    private static bool Mine(JournalEvent journalEvent) =>
        journalEvent.String("CarrierType") is not { Length: > 0 } type
        || string.Equals(type, "FleetCarrier", StringComparison.OrdinalIgnoreCase);

    public CarrierState Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return Mine(journalEvent) ? Folded(journalEvent) : this;
    }

    private CarrierState Folded(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        // <b>The callsign, learned at the airlock</b> — reported 2026-08-23 as <i>"Carrier Captain
        // and Tower have not been talking to me, and I've been in and around the carrier all day"</i>.
        //
        // <see cref="Owned"/> is the callsign being known, and until now only <c>CarrierStats</c>
        // could supply one: over the 925-journal corpus, <b>1,035 CarrierStats carry a Callsign and
        // not one of 1,134 CarrierLocation events does</b> — the read below is a hope, not a source.
        // CarrierStats is written when the Commander opens the carrier management panel, so a day
        // spent flying in and out of their own carrier without opening it left the crew mute:
        // <b>69 of the 199 journals that dock at the Commander's own carrier have no CarrierStats
        // anywhere in them</b>, 148 dockings in all. The day this was reported was one of them —
        // nine dockings at BNH-T2F, not a word.
        //
        // <b>The dock says it, and by id rather than by shape.</b> Docking at a carrier writes the
        // callsign as the station name and the carrier's id as the MarketID, so an event whose
        // MarketID is the id this state already holds is the Commander's own carrier saying its own
        // name. No callsign pattern is matched and no name is guessed: a carrier d47 has not already
        // identified stays unidentified, which is why the 19 corpus journals that dock before any
        // carrier event still say nothing.
        // <b>And the same reading moved forward to the events that come first</b>
        // (<a href="https://github.com/dseelinger/d47/issues/109">#109</a>). The rule above was
        // right and its input was late: docking chatter is the traffic that happens *before* the
        // dock, so a carrier identified at the airlock is identified 47 seconds after the messages
        // it was meant to attribute. DockingRequested and DockingGranted carry everything
        // SaysMyCallsign already tests for and arrive at the start of the approach rather than the
        // end of it — they were simply not in this list.
        //
        // Counted over the Commander's 935 journals rather than assumed, the way the airlock fix
        // counted CarrierStats against CarrierLocation: <b>859 DockingRequested and 857
        // DockingGranted at a fleet carrier, and every single one carries MarketID, StationName and
        // StationType together</b>. No new field is trusted and no new shape is matched; two event
        // kinds join a test that already existed.
        "Docked" or "Undocked" or "Location" or "DockingRequested" or "DockingGranted"
            when SaysMyCallsign(journalEvent) => this with
            {
                CallSign = journalEvent.String("StationName") ?? CallSign,
            },

        // <b>The name, learned from a string that carries it decorated</b> (#130). Reported as
        // <i>"Docking granted, Commander. Welcome home to BNH-T2F"</i> — and the wording was not
        // the bug. Every one of the five surfaces that says the carrier already prefers the name
        // and falls back to the callsign; they all said the callsign because the name was null.
        //
        // <b><c>CarrierStats</c> is its only source and it is usually absent.</b> Elite writes it
        // only when the Commander opens the carrier management panel: <b>34 corpus journals dock
        // at BNH-T2F and only 13 contain a CarrierStats anywhere</b>, so in 21 of 34 sessions d47
        // docks at the Commander's own carrier with no way to know what it is called. This is the
        // airlock fix one field over.
        //
        // <b>Three events carry "Sacred Fire BNH-T2F" — the name and the callsign in one string.</b>
        // This is the safe one, because it carries a <c>MarketID</c>: matched against the id this
        // state already holds, it is the Commander's own carrier naming itself, which is the same
        // id-not-shape rule the callsign fix established. <c>FSSSignalDiscovered</c> carries no id
        // and is not read at all.
        // <b>And the whole string is kept as well as the name pulled out of it</b> (#109). The
        // name derivation needs the callsign to strip, so before the first dock of a session it
        // yields nothing — which is exactly the window the docking chatter arrives in. The
        // undivided display string needs no callsign to be useful, because it is what the
        // <c>From</c> field of those messages literally is.
        //
        // Vouched by id and by nothing else: this arm only runs when the MarketID is the carrier
        // id this state already holds, which is the same id-not-shape rule as above. The
        // Commander's squadron carrier is 3713474048 against their own 3715429376, so it never
        // reaches here — asserted, because that is the mistake #28 exists to prevent.
        "SupercruiseDestinationDrop" when journalEvent.Long("MarketID") == CarrierId => this with
        {
            Name = Name ?? NameWithoutCallsign(journalEvent.String("Type")),
            DisplayName = journalEvent.String("Type") ?? DisplayName,
        },

        // <b>The secondary, and it is safe only because the callsign was learned by id.</b>
        // <c>ReceiveText</c>'s <c>From</c> carries the same decorated string 244 times in the
        // corpus and carries no id at all, so it cannot be trusted on shape — the Commander has a
        // squadron carrier in these same journals, and #28 already ruled that one must never be
        // mistaken for their own. What makes it usable is that a string ending in exactly the
        // vouched callsign can only be that carrier. It matters because these arrive on approach,
        // before the dock.
        "ReceiveText" => this with
        {
            Name = Name ?? NameWithoutCallsign(journalEvent.String("From")),
        },

        // <b>And the same rule reaches the event that actually carries it most often.</b>
        // Measured over this Commander's corpus: of the <b>27 journals that name BNH-T2F with no
        // CarrierStats in them, 22 name it in an FSSSignalDiscovered</b>, against 14 apiece for
        // the other two. The issue proposed ReceiveText as the secondary and this one is the
        // same argument applied where the data is: it carries no id either, and what makes both
        // safe is not the event but the callsign they end with, which was learned by id.
        // Leaving it out would have left half these sessions still saying the callsign.
        "FSSSignalDiscovered" => this with
        {
            Name = Name ?? NameWithoutCallsign(journalEvent.String("SignalName")),
        },

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
            CargoTonnes = journalEvent.Object("SpaceUsage")?.Int("Cargo") ?? CargoTonnes,
            StatsSeenAt = journalEvent.Timestamp,
        },

        // The callsign read here has never once arrived — 0 of 1,134 across the corpus — and is
        // left in place because it costs nothing and would start working the day Frontier adds the
        // field. It is written down rather than trusted: this event is where the carrier's *id*
        // comes from, and the dock above is where its name does.
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
        //
        // **It carries neither a carrier id nor a CarrierType**, so the filter above cannot see
        // it, and it was worth measuring rather than assuming: across the corpus's 132 distinct
        // CarrierJump systems, **not one** belongs only to the squadron carrier. So this event
        // describes the Commander's own carrier in practice, and is folded unchanged.
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
