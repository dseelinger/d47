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

    public MaterialsInventory Materials { get; private set; } = MaterialsInventory.Empty;

    /// <summary>Since they entered the game.</summary>
    public SessionSummary Session { get; private set; } = SessionSummary.Empty;

    /// <summary>
    /// On-foot inventory. Set from the two files Elite writes rather than folded from events,
    /// so it is assigned by the spine rather than by <see cref="Apply"/>.
    /// </summary>
    public SuitInventory Suit { get; internal set; } = SuitInventory.Empty;

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
        Materials = Materials.Apply(journalEvent);
        Session = Session.Apply(journalEvent);
    }
}
