namespace D47.Core.Journal;

/// <summary>
/// One Commander's derived state — everything Phase 7 knows about them, folded from the
/// journal. A plain holder: each part knows how to fold itself, and this is the list of parts.
/// <para>
/// Adding a part means adding a field and one line to <see cref="Apply"/>. Nothing here
/// interprets events itself, which is what keeps each part testable against its own events
/// rather than against the whole state.
/// </para>
/// </summary>
public sealed class CommanderGameState(CommanderIdentity identity)
{
    public CommanderIdentity Identity { get; private set; } = identity;

    public JournalLocation Location { get; private set; } = JournalLocation.Unknown;

    /// <summary>What the Commander is flying, and its metrics.</summary>
    public ShipLoadout Ship { get; private set; } = ShipLoadout.Unknown;

    /// <summary>Their fleet carrier, if they have one.</summary>
    public CarrierState Carrier { get; private set; } = CarrierState.None;

    /// <summary>Every other ship they own, and where.</summary>
    public FleetRegistry Fleet { get; private set; } = FleetRegistry.Empty;

    /// <summary>Every module they have in storage, and where.</summary>
    public ModuleStore Modules { get; private set; } = ModuleStore.Empty;

    public MaterialsInventory Materials { get; private set; } = MaterialsInventory.Empty;

    /// <summary>How far along they are with each engineer.</summary>
    public EngineerProgressState Engineers { get; private set; } = EngineerProgressState.Empty;

    /// <summary>Every community goal their journal has reported, and where they stand on it.</summary>
    public CommunityGoalBoard CommunityGoals { get; private set; } = CommunityGoalBoard.Empty;

    /// <summary>Which Power they fly for, if any (list.md Phase 15).</summary>
    public PowerplayPledge Pledge { get; private set; } = PowerplayPledge.None;

    /// <summary>Construction sites they have visited (list.md Phase 17).</summary>
    public ColonisationSites Colonisation { get; private set; } = ColonisationSites.Empty;

    /// <summary>Since they entered the game.</summary>
    public SessionSummary Session { get; private set; } = SessionSummary.Empty;

    /// <summary>The hired NPC pilots on the books (list.md Phase 11, "Ship Crew").</summary>
    public ShipCrew Crew { get; private set; } = ShipCrew.Empty;

    /// <summary>
    /// On-foot inventory. Set from the two files Elite writes rather than folded from events,
    /// so it is assigned by the spine rather than by <see cref="Apply"/>.
    /// </summary>
    public SuitInventory Suit { get; internal set; } = SuitInventory.Empty;

    /// <summary>
    /// What is in the cargo hold (list.md Phase 18). Same story as <see cref="Suit"/> and for the
    /// same measured reason — the manifest is in a file Elite rewrites, and the matching journal
    /// event carries it on 8% of its occurrences.
    /// </summary>
    public CargoHold Hold { get; internal set; } = CargoHold.Empty;

    public void Apply(JournalEvent journalEvent)
    {
        if (CommanderIdentity.From(journalEvent) is { } identity && identity.FrontierId == Identity.FrontierId)
        {
            Identity = identity; // The name can change (rename); the FID is the stable key.
        }

        Location = Location.Apply(journalEvent);
        Ship = Ship.Apply(journalEvent);
        Carrier = Carrier.Apply(journalEvent);
        Fleet = Fleet.Apply(journalEvent);
        Modules = Modules.Apply(journalEvent);
        Materials = Materials.Apply(journalEvent);
        Engineers = Engineers.Apply(journalEvent);
        CommunityGoals = CommunityGoals.Apply(journalEvent);
        Pledge = Pledge.Apply(journalEvent);
        Session = Session.Apply(journalEvent);

        // After Location, because the depot event names no system and no station: where the
        // Commander is standing is the only thing that can say where the site is, and the event
        // arrives while docked at it (measured, 6,307 of 6,330).
        Colonisation = Colonisation.Apply(journalEvent, Location.StarSystem, Location.StationName);

        // After Ship, so an assignment is tied to the hull the Commander is actually in rather
        // than the one they were in a moment ago. Elite's CrewAssign names no ship at all, so
        // this is the only thing that can tie the two together.
        Crew = Crew.Apply(journalEvent, Ship.Name ?? Ship.TypeName ?? Ship.Type);
    }
}
