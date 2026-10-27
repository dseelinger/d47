namespace D47.Core.Journal;

/// <summary>
/// One Commander's derived state — everything Phase 7 knows about them, folded from the journal.
/// </summary>
public sealed class CommanderGameState(CommanderIdentity identity)
{
    public CommanderIdentity Identity { get; private set; } = identity;

    public JournalLocation Location { get; private set; } = JournalLocation.Unknown;

    /// <summary>The body the Commander is at and where their ship landed on it.</summary>
    public SurfacePoints Surface { get; private set; } = SurfacePoints.None;

    /// <summary>What the Commander is flying, and its metrics.</summary>
    public ShipLoadout Ship { get; private set; } = ShipLoadout.Unknown;

    /// <summary>Every ship they have been seen sitting in, as it stood when they left it.</summary>
    public ShipLoadouts Loadouts { get; internal set; } = ShipLoadouts.Empty;

    /// <summary>
    /// The loadout to answer "what is fitted on my ship" from: the live one where the journal has
    /// described it, and the remembered one otherwise (#337).
    /// </summary>
    public ShipLoadout FlownShip => Ship.IsKnown
        ? Ship
        : (Loadouts.For(Ship.ShipId) ?? Loadouts.LastSeen)?.Loadout ?? Ship;

    /// <summary>
    /// The ship last seen, when the live journal has not named one and it is not stored since; null otherwise.
    /// </summary>
    public ShipLoadout? LastFlownShip =>
        !Ship.IsKnown
        && Loadouts.LastSeen?.Loadout is { IsKnown: true, ShipId: { } id } remembered
        && !Fleet.Ships.Any(stored => stored.ShipId == id)
            ? remembered
            : null;

    /// <summary>Their fleet carrier, if they have one.</summary>
    public CarrierState Carrier { get; internal set; } = CarrierState.None;

    /// <summary>Their squadron's carrier, if they are in a squadron that has one (#230).</summary>
    public CarrierState SquadronCarrier { get; private set; } = CarrierState.NoSquadron;

    /// <summary>The squadron they are in, if any.</summary>
    public SquadronState Squadron { get; private set; } = SquadronState.None;

    /// <summary>Every other ship they own, and where.</summary>
    public FleetRegistry Fleet { get; internal set; } = FleetRegistry.Empty;

    /// <summary>Every module they have in storage, and where.</summary>
    public ModuleStore Modules { get; private set; } = ModuleStore.Empty;

    /// <summary>Every place they have met, as a catalogue to match a misheard name against (#134).</summary>
    public Listening.SpokenNames Names { get; internal set; } = Listening.SpokenNames.Empty;

    public MaterialsInventory Materials { get; private set; } = MaterialsInventory.Empty;

    /// <summary>How far along they are with each engineer.</summary>
    public EngineerProgressState Engineers { get; private set; } = EngineerProgressState.Empty;

    /// <summary>Where they stand in every career ladder (Phase 34).</summary>
    public RankState Ranks { get; private set; } = RankState.Empty;

    /// <summary>Their career statistics, from the last <c>Statistics</c> event.</summary>
    public CareerStatistics Statistics { get; private set; } = CareerStatistics.Empty;

    /// <summary>Their reputation with the superpowers and with every faction met.</summary>
    public ReputationState Reputation { get; internal set; } = ReputationState.Empty;

    /// <summary>The factions' influence and the conflicts in each system they have been in.</summary>
    public SystemStandings Standings { get; private set; } = SystemStandings.Empty;

    /// <summary>What they have contributed to each engineer.</summary>
    public EngineerContributions Contributions { get; internal set; } = EngineerContributions.Empty;

    /// <summary>The missions, sales and visits counted towards engineers that ask for a number of them.</summary>
    public EngineerTallies Tallies { get; internal set; } = EngineerTallies.Empty;

    /// <summary>Every community goal their journal has reported, and where they stand on it.</summary>
    public CommunityGoalBoard CommunityGoals { get; private set; } = CommunityGoalBoard.Empty;

    /// <summary>Their live missions.</summary>
    public MissionBoard Missions { get; internal set; } = MissionBoard.Empty;

    /// <summary>Which Power they fly for, if any (Phase 15).</summary>
    public PowerplayPledge Pledge { get; private set; } = PowerplayPledge.None;

    /// <summary>The merits earned for that Power over the last week.</summary>
    public PowerplayCycleMerits CycleMerits { get; internal set; } = PowerplayCycleMerits.None;

    /// <summary>The BGS work they have done over the last week, per faction.</summary>
    public BgsWork BgsWork { get; internal set; } = BgsWork.None;

    /// <summary>Every kill they have made, from every journal on disk.</summary>
    public CombatRecord Combat { get; internal set; } = CombatRecord.None;

    /// <summary>Every credit transaction they have made, from every journal on disk.</summary>
    public CreditLedger CreditLedger { get; internal set; } = CreditLedger.None;

    /// <summary>Every codex entry they have logged, from every journal on disk.</summary>
    public CodexLedger CodexLedger { get; internal set; } = CodexLedger.None;

    /// <summary>Salvage scooped in the current system while it was their Power's own.</summary>
    public ScoopedSalvage Salvage { get; private set; } = ScoopedSalvage.None;

    /// <summary>The mining run in progress and the last one finished.</summary>
    public MiningRuns Mining { get; private set; } = MiningRuns.None;

    /// <summary>The Operation run in progress and the last one to end.</summary>
    public OperationRuns Operations { get; private set; } = OperationRuns.None;

    /// <summary>Construction sites they have visited (Phase 17).</summary>
    public ColonisationSites Colonisation { get; internal set; } = ColonisationSites.Empty;

    /// <summary>What their surface scans found on each body (Phase 18).</summary>
    public BodySignals Bodies { get; private set; } = BodySignals.Empty;

    /// <summary>What each body's own scan said, and when it was first footfalled (#202).</summary>
    public BodyScans Scans { get; private set; } = BodyScans.Empty;

    /// <summary>The tech broker rows they are known to have unlocked.</summary>
    public TechBrokerUnlocks TechBroker { get; internal set; } = TechBrokerUnlocks.Empty;

    /// <summary>What they have sampled, per body and per genus (Phase 18).</summary>
    public OrganicSampling Sampling { get; internal set; } = OrganicSampling.Empty;

    /// <summary>Since they entered the game.</summary>
    public SessionSummary Session { get; private set; } = SessionSummary.Empty;

    /// <summary>Takes credits recovered from an older journal as the balance, unless a LoadGame has set one.</summary>
    internal void RestoreBalance(long credits)
    {
        if (Session.Balance is null)
        {
            Session = Session with { Balance = credits };
        }
    }

    /// <summary>The hired NPC pilots on the books (Phase 11, "Ship Crew").</summary>
    public ShipCrew Crew { get; private set; } = ShipCrew.Empty;

    /// <summary>The ship currently targeted, or null when none is.</summary>
    public TargetedShip? Target { get; private set; }

    /// <summary>When the Commander entered the Operation they are in, or null when they are in the main game.</summary>
    public DateTimeOffset? InOperationSince { get; private set; }

    /// <summary>On-foot inventory.</summary>
    public SuitInventory Suit { get; internal set; } = SuitInventory.Empty;

    /// <summary>What they are wearing and carrying on foot (Phase 20).</summary>
    public OnFootLoadout OnFoot { get; private set; } = OnFootLoadout.Unknown;

    /// <summary>Every suit and hand weapon they own, not only the one being worn (#292).</summary>
    public OwnedKit Kit { get; internal set; } = OwnedKit.Empty;

    /// <summary>What is in the cargo hold (Phase 18).</summary>
    public CargoHold Hold { get; internal set; } = CargoHold.Empty;

    public FoldReceipt Apply(JournalEvent journalEvent) => Apply(journalEvent, null);

    /// <summary>Folds one event in, and says which parts of the state it changed.</summary>
    /// <param name="at">Where the Commander was standing, where that is known.</param>
    public FoldReceipt Apply(JournalEvent journalEvent, SurfaceFix? at)
    {
        var before = Parts.Select(part => part.Read(this)).ToArray();

        Fold(journalEvent, at);

        var changes = new List<FoldChange>();

        for (var i = 0; i < Parts.Length; i++)
        {
            if (Changed(Parts[i], before[i], Parts[i].Read(this)) is { } change)
            {
                changes.Add(change);
            }
        }

        return changes.Count == 0 ? FoldReceipt.Nothing : new FoldReceipt(changes);
    }

    /// <summary>The phrase a receipt uses for this part, or null for a name that is not a folded part.</summary>
    public static string? PhraseFor(string part) =>
        Parts.FirstOrDefault(each => each.Name == part)?.Phrase;

    /// <summary>A part of the state an event can replace, in the order <see cref="Fold"/> applies them.</summary>
    private sealed record Part(string Name, string Phrase, Func<CommanderGameState, object?> Read);

    private static readonly Part[] Parts =
    [
        new(nameof(Identity), "your Commander name", state => state.Identity),
        new(nameof(Location), "your location", state => state.Location),
        new(nameof(Surface), "your parked ship", state => state.Surface),
        new(nameof(Ship), "your ship", state => state.Ship),
        new(nameof(Loadouts), "your remembered ships", state => state.Loadouts),
        new(nameof(OnFoot), "your on-foot loadout", state => state.OnFoot),
        new(nameof(Kit), "your suits and weapons", state => state.Kit),
        new(nameof(Carrier), "your carrier", state => state.Carrier),
        new(nameof(SquadronCarrier), "your squadron's carrier", state => state.SquadronCarrier),
        new(nameof(Squadron), "your squadron", state => state.Squadron),
        new(nameof(Fleet), "your stored ships", state => state.Fleet),
        new(nameof(Modules), "your stored modules", state => state.Modules),
        new(nameof(Names), "the place names d47 listens for", state => state.Names),
        new(nameof(Materials), "your materials", state => state.Materials),
        new(nameof(Engineers), "your engineer progress", state => state.Engineers),
        new(nameof(Ranks), "your ranks", state => state.Ranks),
        new(nameof(Statistics), "your career statistics", state => state.Statistics),
        new(nameof(Reputation), "your reputation", state => state.Reputation),
        new(nameof(Standings), "system factions and conflicts", state => state.Standings),
        new(nameof(Contributions), "your engineer contributions", state => state.Contributions),
        new(nameof(Tallies), "your engineer tallies", state => state.Tallies),
        new(nameof(CommunityGoals), "community goals", state => state.CommunityGoals),
        new(nameof(Missions), "your missions", state => state.Missions),
        new(nameof(Pledge), "your Powerplay pledge", state => state.Pledge),
        new(nameof(CycleMerits), "your merits this cycle", state => state.CycleMerits),
        new(nameof(BgsWork), "your BGS work", state => state.BgsWork),
        new(nameof(Combat), "your kills", state => state.Combat),
        new(nameof(CreditLedger), "your credit record", state => state.CreditLedger),
        new(nameof(CodexLedger), "your codex entries", state => state.CodexLedger),
        new(nameof(Salvage), "your scooped salvage", state => state.Salvage),
        new(nameof(Mining), "your mining runs", state => state.Mining),
        new(nameof(Operations), "your Operation runs", state => state.Operations),
        new(nameof(Bodies), "body signals", state => state.Bodies),
        new(nameof(Scans), "body scans", state => state.Scans),
        new(nameof(TechBroker), "your tech broker unlocks", state => state.TechBroker),
        new(nameof(Sampling), "your organic samples", state => state.Sampling),
        new(nameof(Session), "this session's totals", state => state.Session),
        new(nameof(Colonisation), "construction sites", state => state.Colonisation),
        new(nameof(Crew), "your crew", state => state.Crew),
        new(nameof(Target), "your target", state => state.Target),
        new(nameof(InOperationSince), "whether you are in an Operation", state => state.InOperationSince),
    ];

    /// <summary>How a receipt names this part's change, or null when it did not change.</summary>
    private static FoldChange? Changed(Part part, object? was, object? now) => (was, now) switch
    {
        _ when Equals(was, now) => null,

        // Docking and system names move the record without changing the work.
        (BgsWork a, BgsWork b) when ReferenceEquals(a.Entries, b.Entries) => null,

        // Names and positions move the record without changing the kills.
        (CombatRecord a, CombatRecord b) when ReferenceEquals(a.Kills, b.Kills) && a.IsWhole == b.IsWhole => null,

        // Every event moves the ledger's last time, so only its entries count.
        (CreditLedger a, CreditLedger b) when ReferenceEquals(a.Entries, b.Entries) && a.IsWhole == b.IsWhole => null,

        // Every event moves LastEventAt, so only the rest of the session counts.
        (SessionSummary a, SessionSummary b) when a with { LastEventAt = b.LastEventAt } == b => null,

        (CarrierState { CallSign: null }, CarrierState { CallSign: not null }) when part.Name == nameof(Carrier) =>
            new FoldChange(part.Name, "your carrier's callsign", "learned here"),

        (_, JournalLocation location) => new FoldChange(part.Name, part.Phrase, Where(location)),

        _ => new FoldChange(part.Name, part.Phrase),
    };

    /// <summary>The system, then the station or body, or null when none is known.</summary>
    private static string? Where(JournalLocation location)
    {
        string?[] named = [location.StarSystem, location.StationName ?? location.Body];
        var said = string.Join(" · ", named.Where(name => name is { Length: > 0 }));

        return said.Length > 0 ? said : null;
    }

    private void Fold(JournalEvent journalEvent, SurfaceFix? at)
    {
        if (journalEvent.Kind == "NewCommander")
        {
            Reputation = Reputation.WithoutFactions();
            Standings = SystemStandings.Empty;
            BgsWork = BgsWork.None;
            Combat = CombatRecord.None with { IsWhole = Combat.IsWhole };
            CreditLedger = CreditLedger.None with { IsWhole = CreditLedger.IsWhole };
            CodexLedger = CodexLedger.None with { IsWhole = CodexLedger.IsWhole, SessionStart = CodexLedger.SessionStart };
            Contributions = EngineerContributions.Empty;
            Tallies = EngineerTallies.Empty;
            Loadouts = ShipLoadouts.NoShips;
            Kit = OwnedKit.Empty;
            Missions = MissionBoard.Empty;
            return;
        }

        if (CommanderIdentity.From(journalEvent) is { } identity && identity.FrontierId == Identity.FrontierId)
        {
            Identity = identity; // The name can change (rename); the FID is the stable key.
        }

        Location = Location.Apply(journalEvent);
        Surface = Surface.Apply(journalEvent);
        Ship = Ship.Apply(journalEvent);

        // After Ship and in one place, so every route that changes the flown ship is remembered by the same
        // line — the Loadout, the rename, and the EngineerCraft that Elite writes no Loadout for.
        Loadouts = Loadouts.Remember(Ship, journalEvent.Timestamp).Apply(journalEvent);
        OnFoot = OnFoot.Apply(journalEvent);
        Kit = Kit.Apply(journalEvent);
        Carrier = Carrier.Apply(journalEvent);
        SquadronCarrier = SquadronCarrier.Apply(journalEvent);
        Squadron = Squadron.Apply(journalEvent);
        // After Location, for the reason Colonisation below is: storing a ship happens wherever the Commander
        // is standing, and neither ShipyardSwap nor ShipyardBuy names a place.
        Fleet = Fleet.Apply(journalEvent, Location.StarSystem, Location.StationName);
        Modules = Modules.Apply(journalEvent);

        // Names of places, and only places — see SpokenNames.Apply, which reads named fields rather than
        // scraping the event, because the line between a place Elite wrote and words another player typed is
        // the whole of the trust rule here.
        Names = Names.Apply(journalEvent);
        Materials = Materials.Apply(journalEvent);
        Engineers = Engineers.Apply(journalEvent);
        Ranks = Ranks.Apply(journalEvent);
        Statistics = Statistics.Apply(journalEvent);
        Reputation = Reputation.Apply(journalEvent);
        Standings = Standings.Apply(journalEvent);
        Contributions = Contributions.Apply(journalEvent);
        Tallies = Tallies.Apply(journalEvent);
        CommunityGoals = CommunityGoals.Apply(journalEvent);
        Missions = Missions.Apply(journalEvent);
        Pledge = Pledge.Apply(journalEvent);
        CycleMerits = CycleMerits.Apply(journalEvent, Pledge);
        BgsWork = BgsWork.Apply(journalEvent);
        Combat = Combat.Apply(journalEvent);
        CreditLedger = CreditLedger.Apply(journalEvent);
        CodexLedger = CodexLedger.Apply(journalEvent);
        Salvage = Salvage.Apply(journalEvent, Location, Pledge);
        Mining = Mining.Apply(journalEvent);
        Operations = Operations.Apply(journalEvent);
        Bodies = Bodies.Apply(journalEvent);
        Scans = Scans.Apply(journalEvent);
        TechBroker = TechBroker.Apply(journalEvent);
        Sampling = Sampling.Apply(journalEvent, at);
        Session = Session.Apply(journalEvent);

        // After Location, because the depot event names no system and no station: where the Commander is
        // standing is the only thing that can say where the site is, and the event arrives while docked at it
        // (measured, 6,307 of 6,330).
        Colonisation = Colonisation.Apply(journalEvent, Location.StarSystem, Location.StationName);

        // After Ship, so an assignment is tied to the hull the Commander is actually in rather than the one
        // they were in a moment ago.
        Crew = Crew.Apply(journalEvent, Ship.Name ?? Ship.TypeName ?? Ship.Type);

        InOperationSince = journalEvent.Kind switch
        {
            "GameModeChange" => journalEvent.String("GameMode") == "Operation" ? journalEvent.Timestamp : null,
            "LoadGame" or "Shutdown" => null,
            _ => InOperationSince,
        };

        Target = journalEvent.Kind switch
        {
            "ShipTargeted" => TargetedShip.From(journalEvent),
            "FSDJump" or "CarrierJump" or "Docked" or "LoadGame" => null,
            _ => Target,
        };
    }

    private bool _loadoutSilenceLogged;

    private DateTimeOffset? _loadoutSilenceLoggedFor;

    /// <summary>
    /// Whether the unknown-loadout warning is still owed for this game session — true the first time it
    /// is asked in a session and false after (#337).
    /// </summary>
    internal bool OweLoadoutSilenceWarning()
    {
        if (_loadoutSilenceLogged && _loadoutSilenceLoggedFor == Session.StartedAt)
        {
            return false;
        }

        _loadoutSilenceLogged = true;
        _loadoutSilenceLoggedFor = Session.StartedAt;
        return true;
    }
}
