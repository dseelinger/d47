using D47.Core.Actions;
using D47.Core.Activities;
using D47.Core.Callouts;
using D47.Core.Checklists;
using D47.Core.Contribution;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Lore;
using D47.Core.Storage;
using D47.Core.Ticking;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.Core.Tests.Journal;

/// <summary>A <see cref="JournalTick"/> over a journal folder held in memory, with every store it folds into.</summary>
internal sealed class JournalTickFixture
{
    public const string Root = @"C:\d47-journal-tick";

    public const string Data = @"C:\d47-journal-tick-data";

    public static readonly DateTimeOffset Start = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private long _number;

    public JournalTickFixture(GameStateStore? gameState = null)
    {
        GameState = gameState ?? new GameStateStore();

        var loggers = NullLoggerFactory.Instance;
        var status = new GameStatusReader(Root, Files, NullLogger.Instance);

        History = new HistoryBackfill { Directory = Root, FileSystem = Files, Loggers = loggers };

        Tick = new JournalTick(new JournalTick.Sources
        {
            Journal = new JournalSpine(Root, Files, GameState, loggers, () => status.Current),
            GameState = GameState,
            History = History,
            Status = status,
            Route = new NavRouteReader(Root, Files, NullLogger<NavRouteReader>.Instance),
            ModulePower = new ModulePowerReader(Root, Files, NullLogger.Instance),
            Markets = new MarketReader(Root, new MarketBook(Path.Combine(Data, "markets.json"), Files, NullLogger.Instance), Files, NullLogger.Instance),
            Eddn = new EddnContribution(new EddnComposer(() => null, "1.0.0", () => null), new NoSink(), () => false),
            Checklists = new ChecklistService(
                new ChecklistStore(Path.Combine(Data, "checklist.json"), Files, NullLogger<ChecklistStore>.Instance),
                new ChecklistProposalStore(Path.Combine(Data, "checklist-proposals.json"), Files, NullLogger<ChecklistProposalStore>.Instance),
                () => GameState.Active),
            CommodityLedger = CommodityLedger,
            Exobiology = Exobiology,
            Cartography = new CartographyLedger(null, Files, NullLogger.Instance),
            Crimes = new OutstandingCrimes(Files, NullLogger.Instance),
            Activities = new ActivityLedger(Files, NullLogger.Instance),
            SignalSightings = new SignalSightings(Files, NullLogger.Instance),
            CommanderErases = new CommanderErases(Files, NullLogger<CommanderErases>.Instance),
            Friends = new FriendRoster(),
            CommanderLog = new CommanderLog(Files, NullLogger.Instance),
            TravelLog = new TravelLog(Files, NullLogger.Instance),
            FullyFound = new FullyFoundSystems(Files, NullLogger.Instance),
            Mail = new MailLedger(Path.Combine(Data, MailLedger.FileName), Files, NullLogger.Instance),
            PlanBook = new RoutePlanBook(Path.Combine(Data, "route-plans.json"), Files, NullLogger<RoutePlanBook>.Instance),
            Fight = new NearbyFight(),
            Scenes = new SceneTracker(),
            Callouts = Callouts,
            Autonomous = new AutonomousActionRunner(NullLogger<AutonomousActionRunner>.Instance),
            Sampling = new SamplingStore(Path.Combine(Data, "sampling.json"), Files, NullLogger<SamplingStore>.Instance),
            Loadouts = new LoadoutStore(Path.Combine(Data, "loadouts.json"), Files, NullLogger<LoadoutStore>.Instance),
            Kit = new KitStore(Path.Combine(Data, "kit.json"), Files, NullLogger<KitStore>.Instance),
            Lore = new LoreBook(new LoreStore(Path.Combine(Data, "lore.json"), Files, NullLogger<LoreStore>.Instance)),
            LoreVisits = new LoreVisits(Path.Combine(Data, "lore-visits.json"), Files, NullLogger<LoreVisits>.Instance),
        });
    }

    public MemoryFileSystem Files { get; } = new();

    public GameStateStore GameState { get; }

    public HistoryBackfill History { get; }

    public CommodityLedger CommodityLedger { get; } = new();

    public ExobiologyLedger Exobiology { get; } = new(null, new MemoryFileSystem(), NullLogger.Instance);

    public CalloutEngine Callouts { get; } = new(NullLogger<CalloutEngine>.Instance);

    public JournalTick Tick { get; }

    private string JournalFile => Path.Combine(Root, "Journal.2026-10-31T120000.01.log");

    /// <summary>Appends lines to the journal Elite is writing.</summary>
    public void Write(params string[] lines) => Files.AppendText(JournalFile, string.Concat(lines.Select(line => line + "\n")));

    /// <summary>The next tick's context, the first being the priming tick.</summary>
    public TickContext Next()
    {
        _number++;
        return new TickContext(Start.AddSeconds(_number), TimeSpan.FromMilliseconds(100), _number);
    }

    private sealed class NoSink : IEddnSink
    {
        public void Post(EddnEnvelope envelope)
        {
        }
    }
}
