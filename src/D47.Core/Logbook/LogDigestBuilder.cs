using System.Globalization;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Logbook;

/// <summary>
/// Folds a window of journals into a <see cref="LogDigest"/> (Phase 33, item 2).
/// <para>
/// <b>This is the phase's quality bar in one class.</b> Everything a log can truthfully say has to
/// be computed here, because the generator downstream is only ever handed what this produces. A
/// thing this does not count is a thing the log cannot mention, which is the trade the item asks
/// for: a smaller log that is true beats a fuller one that is not.
/// </para>
/// <para>
/// <b>Two event kinds are excluded outright rather than filtered.</b> <c>ReceiveText</c> and
/// <c>SendText</c> carry text a stranger typed — the untrusted channel architecture.md §7 names —
/// and this is both the largest prompt d47 assembles and the one output the Commander posts
/// somewhere. There is no handler for them and <see cref="ExcludedEvents"/> is asserted by test, so
/// adding one is a deliberate act rather than an oversight.
/// </para>
/// <para>
/// It owns no thread and reads no clock: the window arrives resolved, the files arrive as a list,
/// and the App decides not to run it on the UI thread.
/// </para>
/// </summary>
public sealed class LogDigestBuilder(ILogger logger)
{
    /// <summary>
    /// The most facts a digest carries. A week of heavy play computes more, and the remainder is
    /// rolled into the totals with the digest saying in words that it happened.
    /// </summary>
    public const int MaxFacts = 150;

    /// <summary>
    /// The most individual facts one category contributes. Without it, four hundred completed
    /// courier missions crowd out the one death, which is the opposite of what a log is for.
    /// </summary>
    public const int MaxPerKind = 25;

    /// <summary>
    /// Events that never reach a digest, whatever else changes. Named here so the test that
    /// asserts it has one place to point at.
    /// </summary>
    public static IReadOnlyList<string> ExcludedEvents { get; } = ["ReceiveText", "SendText"];

    /// <summary>
    /// Which categories survive the global cap, most notable first. A death matters more than a
    /// jump count however many jumps there were.
    /// </summary>
    private static readonly LogFactKind[] Priority =
    [
        LogFactKind.Opening,
        LogFactKind.Closing,
        LogFactKind.Mishap,
        LogFactKind.Progress,
        LogFactKind.Fleet,
        LogFactKind.Engineering,
        LogFactKind.Exploration,
        LogFactKind.Combat,
        LogFactKind.Mission,
        LogFactKind.Trade,
        LogFactKind.OnFoot,
        LogFactKind.Travel,
    ];

    public LogDigest Build(IReadOnlyList<string> files, LogRange range)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(range);

        var fold = new Fold();
        var journals = 0;
        var events = 0L;

        foreach (var file in LogRanges.FilesFor(files, range))
        {
            var used = false;

            IEnumerable<string> lines;

            try
            {
                lines = File.ReadLines(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not read {File} for the Commander's log", file);
                continue;
            }

            foreach (var line in lines)
            {
                if (!JournalEvent.TryParse(line, logger, out var parsed) || parsed is null)
                {
                    continue;
                }

                // Identity is established from wherever it is stated, including from before the
                // window opened: a log of the last two hours of a five-hour session is still that
                // Commander's, and the event that named them is at the top of the file.
                fold.Identify(parsed);

                if (!range.Holds(parsed.Timestamp))
                {
                    continue;
                }

                events++;
                used = true;
                fold.Apply(parsed);
            }

            if (used)
            {
                journals++;
            }
        }

        return Seal(fold, range, journals, events);
    }

    private static LogDigest Seal(Fold fold, LogRange range, int journals, long events)
    {
        var moments = new List<LogFact>();
        var totals = new List<LogFact>();
        var dropped = 0;

        foreach (var kind in Priority)
        {
            var of = fold.Moments.Where(fact => fact.Kind == kind).OrderBy(fact => fact.At).ToList();

            if (of.Count <= MaxPerKind)
            {
                moments.AddRange(of);
                continue;
            }

            moments.AddRange(of.Take(MaxPerKind));
            dropped += of.Count - MaxPerKind;

            totals.Add(new LogFact
            {
                Kind = kind,
                Aggregate = true,
                Statement =
                    $"{(of.Count - MaxPerKind).ToString("N0", CultureInfo.InvariantCulture)} further "
                    + $"{kind.ToString().ToLowerInvariant()} events happened that are not listed above.",
                At = of[MaxPerKind].At,
                Sources = [.. of.Skip(MaxPerKind).Take(LogFact.SourcesKept).SelectMany(fact => fact.Sources)],
                SourceCount = of.Count - MaxPerKind,
            });
        }

        // Partitioned rather than appended wholesale. Everything the fold produces at the end is
        // an aggregate except the closing line, which is a moment — it happened at an instant and
        // renders in the chronology. Filing it with the totals numbered it after facts it appears
        // before, which is the one thing the numbering exists to prevent.
        var closing = fold.Totals.ToList();

        totals.AddRange(closing.Where(fact => fact.Aggregate));
        moments.AddRange(closing.Where(fact => !fact.Aggregate));

        moments = [.. moments.OrderBy(fact => fact.At)];

        // The global cap trims moments rather than totals, and from the least notable categories
        // first. A total is a whole window in one line and is never the expensive thing here.
        if (moments.Count + totals.Count > MaxFacts)
        {
            var room = Math.Max(0, MaxFacts - totals.Count);
            var rank = Priority.Select((kind, index) => (kind, index)).ToDictionary(pair => pair.kind, pair => pair.index);

            var kept = moments
                .OrderBy(fact => rank.GetValueOrDefault(fact.Kind, int.MaxValue))
                .ThenBy(fact => fact.At)
                .Take(room)
                .ToHashSet();

            dropped += moments.Count - kept.Count;
            moments = [.. moments.Where(kept.Contains)];
        }

        var numbered = new List<LogFact>(moments.Count + totals.Count);
        var id = 1;

        // Numbered in the order the digest renders them, so a Commander reading the Sources
        // section counts down the page rather than hunting.
        foreach (var fact in moments.Concat(totals))
        {
            numbered.Add(fact with { Id = id++ });
        }

        return new LogDigest
        {
            Range = range,
            Commander = fold.Commander,
            FrontierId = fold.FrontierId,
            Facts = numbered,
            JournalsRead = journals,
            EventsRead = events,
            FactsDropped = dropped,
        };
    }

    /// <summary>The supporting events for one aggregate, capped as they arrive.</summary>
    private sealed class Trail
    {
        private readonly List<LogSource> _sources = [];

        public int Count { get; private set; }

        public DateTimeOffset First { get; private set; } = DateTimeOffset.MaxValue;

        public IReadOnlyList<LogSource> Sources => _sources;

        public void Add(JournalEvent journalEvent)
        {
            Count++;

            if (journalEvent.Timestamp < First)
            {
                First = journalEvent.Timestamp;
            }

            if (_sources.Count < LogFact.SourcesKept)
            {
                _sources.Add(new LogSource(journalEvent.Kind, journalEvent.Timestamp));
            }
        }

        public LogFact? Fact(LogFactKind kind, string statement) =>
            Count == 0
                ? null
                : new LogFact
                {
                    Kind = kind,
                    Aggregate = true,
                    Statement = statement,
                    At = First,
                    Sources = _sources,
                    SourceCount = Count,
                };
    }

    /// <summary>
    /// Every roll of one blueprint on one module at one engineer, as one fact.
    /// <para>
    /// The grade kept is the highest reached rather than the last rolled, because that is what the
    /// module ended up at — Elite reports the level of each roll, and a run that finishes at 5 and
    /// then experiments back down at 1 has still produced a grade 5 module.
    /// </para>
    /// </summary>
    private sealed class Engineering(string module, string blueprint, string engineer)
    {
        private readonly Trail _trail = new();

        public DateTimeOffset First => _trail.First;

        private int Grade { get; set; }

        public void Add(JournalEvent journalEvent)
        {
            _trail.Add(journalEvent);
            Grade = Math.Max(Grade, journalEvent.Int("Level") ?? 0);
        }

        public LogFact Fact() =>
            _trail.Fact(
                LogFactKind.Engineering,
                $"Rolled {blueprint} on the {module} {Rolls()} with {engineer}"
                + (Grade > 0 ? $", finishing at grade {Grade.ToString(CultureInfo.InvariantCulture)}." : "."))!;

        private string Rolls() => _trail.Count == 1
            ? "once"
            : $"{_trail.Count.ToString("N0", CultureInfo.InvariantCulture)} times";
    }

    /// <summary>
    /// The accumulators. One object per digest, because every counter here belongs to one window
    /// and one Commander.
    /// </summary>
    private sealed class Fold
    {
        private readonly List<LogFact> _moments = [];

        private readonly Trail _jumps = new();
        private readonly Trail _docks = new();
        private readonly Trail _landings = new();
        private readonly Trail _scans = new();
        private readonly Trail _mapped = new();
        private readonly Trail _bounties = new();
        private readonly Trail _bonds = new();
        private readonly Trail _sales = new();
        private readonly Trail _purchases = new();
        private readonly Trail _vouchers = new();
        private readonly Trail _modules = new();
        private readonly Trail _materials = new();
        private readonly Trail _hull = new();
        private readonly Trail _ownInterdictions = new();
        private readonly Trail _srv = new();
        private readonly Trail _srvLost = new();
        private readonly Trail _onFoot = new();
        private readonly Trail _scooped = new();
        private readonly Trail _missionsAccepted = new();
        private readonly Trail _missionsLost = new();

        private readonly SortedSet<string> _systems = new(StringComparer.Ordinal);
        private readonly SortedSet<string> _stations = new(StringComparer.Ordinal);
        private readonly SortedSet<string> _settlements = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _commodities = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Engineering> _engineering = new(StringComparer.Ordinal);

        private double _distance;
        private long _bountyCredits;
        private long _bondCredits;
        private long _saleCredits;
        private long _purchaseCredits;
        private long _voucherCredits;
        private long _moduleCredits;
        private long _tonnesSold;
        private int _materialCount;
        private int _firstDiscoveries;
        private double _worstHull = 1.0;
        private long _scoopedTonnes;
        private bool _opened;

        private string? _firstSystem;
        private string? _lastSystem;
        private string? _lastStation;
        private DateTimeOffset _lastAt;
        private string? _ship;
        private LogSource? _lastPlace;

        public string? Commander { get; private set; }

        public string? FrontierId { get; private set; }

        public IReadOnlyList<LogFact> Moments => _moments;

        /// <summary>Who is flying, from wherever it was stated. Never part of the prompt.</summary>
        public void Identify(JournalEvent journalEvent)
        {
            if (journalEvent.Kind is not ("Commander" or "LoadGame"))
            {
                return;
            }

            Commander ??= journalEvent.String("Name");
            FrontierId ??= journalEvent.String("FID");
        }

        public void Apply(JournalEvent journalEvent)
        {
            _lastAt = journalEvent.Timestamp;

            switch (journalEvent.Kind)
            {
                case "LoadGame":
                    _ship = Ship(journalEvent);
                    Open(
                        journalEvent,
                        $"Entered the game flying {_ship}"
                        + (journalEvent.Long("Credits") is { } credits
                            ? $", {Credits(credits)} in the bank."
                            : "."));
                    break;

                case "Location":
                    Where(journalEvent);
                    Open(journalEvent, $"Started out {Place(journalEvent)}.");
                    break;

                case "FSDJump":
                    Where(journalEvent);
                    _firstSystem ??= journalEvent.String("StarSystem");
                    _distance += journalEvent.Double("JumpDist") ?? 0;
                    _jumps.Add(journalEvent);
                    Open(journalEvent, $"Started out in {journalEvent.String("StarSystem")}.");
                    break;

                case "CarrierJump":
                    Where(journalEvent);
                    Moment(
                        journalEvent,
                        LogFactKind.Travel,
                        $"The fleet carrier jumped to {journalEvent.String("StarSystem")}.");
                    break;

                case "Docked":
                    Where(journalEvent);
                    _docks.Add(journalEvent);

                    if (journalEvent.Named("StationName") is { Length: > 0 } station)
                    {
                        _stations.Add(station);
                    }

                    Open(journalEvent, $"Started out {Place(journalEvent)}.");
                    break;

                case "Touchdown":
                    _landings.Add(journalEvent);
                    break;

                case "FuelScoop":
                    _scooped.Add(journalEvent);
                    _scoopedTonnes += (long)(journalEvent.Double("Scooped") ?? 0);
                    break;

                case "Scan" when string.Equals(journalEvent.String("ScanType"), "Detailed", StringComparison.Ordinal):
                    _scans.Add(journalEvent);

                    if (!journalEvent.Bool("WasDiscovered"))
                    {
                        _firstDiscoveries++;
                    }

                    break;

                case "SAAScanComplete":
                    _mapped.Add(journalEvent);
                    break;

                case "CodexEntry":
                    Moment(
                        journalEvent,
                        LogFactKind.Exploration,
                        $"Logged a codex entry: {journalEvent.Named("Name")} in {journalEvent.String("System")}.");
                    break;

                case "SellExplorationData":
                case "MultiSellExplorationData":
                    Moment(
                        journalEvent,
                        LogFactKind.Exploration,
                        $"Sold cartographic data for {Credits(journalEvent.Long("TotalEarnings") ?? 0)}.");
                    break;

                case "SellOrganicData":
                    Moment(
                        journalEvent,
                        LogFactKind.Exploration,
                        "Sold biological data for "
                        + Credits(journalEvent.Items("BioData")
                            .Sum(data => (data.Long("Value") ?? 0) + (data.Long("Bonus") ?? 0)))
                        + ".");
                    break;

                case "Bounty":
                    _bounties.Add(journalEvent);
                    _bountyCredits += journalEvent.Long("TotalReward")
                        ?? journalEvent.Items("Rewards").Sum(reward => reward.Long("Reward") ?? 0);
                    break;

                case "FactionKillBond":
                    _bonds.Add(journalEvent);
                    _bondCredits += journalEvent.Long("Reward") ?? 0;
                    break;

                case "RedeemVoucher":
                    _vouchers.Add(journalEvent);
                    _voucherCredits += journalEvent.Long("Amount") ?? 0;
                    break;

                case "MarketSell":
                    _sales.Add(journalEvent);
                    _saleCredits += journalEvent.Long("TotalSale") ?? 0;
                    _tonnesSold += journalEvent.Long("Count") ?? 0;

                    if (journalEvent.Named("Type") is { Length: > 0 } commodity)
                    {
                        _commodities[commodity] =
                            _commodities.GetValueOrDefault(commodity) + (journalEvent.Long("TotalSale") ?? 0);
                    }

                    break;

                case "MarketBuy":
                    _purchases.Add(journalEvent);
                    _purchaseCredits += journalEvent.Long("TotalCost") ?? 0;
                    break;

                case "MissionAccepted":
                    _missionsAccepted.Add(journalEvent);
                    break;

                case "MissionCompleted":
                    Moment(
                        journalEvent,
                        LogFactKind.Mission,
                        $"Completed a mission for {journalEvent.String("Faction")}"
                        + (journalEvent.Long("Reward") is { } reward and > 0 ? $", {Credits(reward)}." : "."));
                    break;

                case "MissionFailed":
                case "MissionAbandoned":
                    _missionsLost.Add(journalEvent);
                    break;

                case "EngineerCraft":
                    Roll(journalEvent);
                    break;

                case "EngineerProgress"
                    when string.Equals(journalEvent.String("Progress"), "Unlocked", StringComparison.Ordinal):
                    Moment(
                        journalEvent,
                        LogFactKind.Progress,
                        $"Unlocked the engineer {journalEvent.String("Engineer")}.");
                    break;

                case "Promotion":
                    Moment(journalEvent, LogFactKind.Progress, Promotion(journalEvent));
                    break;

                case "ShipyardBuy":
                    Moment(
                        journalEvent,
                        LogFactKind.Fleet,
                        $"Bought a {JournalJson.Spoken(journalEvent.Named("ShipType"))} for "
                        + $"{Credits(journalEvent.Long("ShipPrice") ?? 0)}.");
                    break;

                case "ShipyardSell":
                    Moment(
                        journalEvent,
                        LogFactKind.Fleet,
                        $"Sold a {JournalJson.Spoken(journalEvent.Named("ShipType"))} for "
                        + $"{Credits(journalEvent.Long("ShipPrice") ?? 0)}.");
                    break;

                case "ShipyardSwap":
                    _ship = JournalJson.Spoken(journalEvent.Named("ShipType"));
                    Moment(journalEvent, LogFactKind.Fleet, $"Swapped into the {_ship}.");
                    break;

                case "ModuleBuy":
                    _modules.Add(journalEvent);
                    _moduleCredits += journalEvent.Long("BuyPrice") ?? 0;
                    break;

                case "ModuleSell":
                case "ModuleSellRemote":
                    _modules.Add(journalEvent);
                    _moduleCredits -= journalEvent.Long("SellPrice") ?? 0;
                    break;

                case "Died":
                    Moment(journalEvent, LogFactKind.Mishap, Death(journalEvent));
                    break;

                case "Resurrect":
                    Moment(
                        journalEvent,
                        LogFactKind.Mishap,
                        $"Took the rebuy, {Credits(journalEvent.Long("Cost") ?? 0)}.");
                    break;

                case "Interdicted":
                    Moment(
                        journalEvent,
                        LogFactKind.Mishap,
                        $"Was interdicted by {journalEvent.String("Interdictor") ?? "somebody unnamed"}, and "
                        + (journalEvent.Bool("Submitted") ? "submitted." : "ran for it."));
                    break;

                case "Interdiction":
                    _ownInterdictions.Add(journalEvent);
                    break;

                case "HullDamage":
                    _hull.Add(journalEvent);
                    _worstHull = Math.Min(_worstHull, journalEvent.Double("Health") ?? 1.0);
                    break;

                case "LaunchSRV":
                    _srv.Add(journalEvent);
                    break;

                // Counted apart from launches. "1 launch or loss" reads as a launch, so a
                // Commander who put an SRV down a crater was being told they had used one — a
                // small untruth, and the read-through that found it is the reason the item asks
                // for one.
                case "SRVDestroyed":
                    _srvLost.Add(journalEvent);
                    break;

                case "MaterialCollected":
                    _materials.Add(journalEvent);
                    _materialCount += journalEvent.Int("Count") ?? 1;
                    break;

                case "Disembark":
                    _onFoot.Add(journalEvent);
                    break;

                case "ApproachSettlement":
                    if (journalEvent.Named("Name") is { Length: > 0 } settlement)
                    {
                        _settlements.Add(settlement);
                    }

                    break;

                default:
                    break;
            }
        }

        /// <summary>The aggregates, built once at the end from the counters above.</summary>
        public IEnumerable<LogFact> Totals
        {
            get
            {
                var facts = new List<LogFact?>
                {
                    _jumps.Fact(
                        LogFactKind.Travel,
                        $"{Count(_jumps.Count)} hyperspace jump(s), {_distance:N0} ly in total"
                        + (_firstSystem is not null && _lastSystem is not null && _firstSystem != _lastSystem
                            ? $", {_firstSystem} out to {_lastSystem}."
                            : ".")),

                    _scooped.Fact(
                        LogFactKind.Travel,
                        $"Scooped {Count(_scoopedTonnes)} tonnes of hydrogen over {Count(_scooped.Count)} passes."),

                    _docks.Fact(
                        LogFactKind.Travel,
                        $"Docked {Count(_docks.Count)} time(s), at {Some(_stations)}."),

                    _landings.Fact(LogFactKind.Travel, $"Put down on a surface {Count(_landings.Count)} time(s)."),

                    _scans.Fact(
                        LogFactKind.Exploration,
                        $"Scanned {Count(_scans.Count)} bodies in detail"
                        + (_firstDiscoveries > 0
                            ? $", {Count(_firstDiscoveries)} of them not scanned by anybody before."
                            : ", none of them new to the galaxy.")),

                    _mapped.Fact(LogFactKind.Exploration, $"Surface-mapped {Count(_mapped.Count)} bodies."),

                    _bounties.Fact(
                        LogFactKind.Combat,
                        $"Collected {Count(_bounties.Count)} bounty voucher(s) worth {Credits(_bountyCredits)}."),

                    _bonds.Fact(
                        LogFactKind.Combat,
                        $"Earned {Count(_bonds.Count)} combat bond(s) worth {Credits(_bondCredits)}."),

                    _vouchers.Fact(LogFactKind.Combat, $"Cashed in vouchers for {Credits(_voucherCredits)}."),

                    _ownInterdictions.Fact(
                        LogFactKind.Combat,
                        $"Pulled {Count(_ownInterdictions.Count)} other ship(s) out of supercruise."),

                    _sales.Fact(
                        LogFactKind.Trade,
                        $"Sold {Count(_tonnesSold)} tonnes across {Count(_sales.Count)} sale(s) for "
                        + $"{Credits(_saleCredits)}{TopCommodity()}."),

                    _purchases.Fact(
                        LogFactKind.Trade,
                        $"Spent {Credits(_purchaseCredits)} on cargo across {Count(_purchases.Count)} purchase(s)."),

                    _modules.Fact(
                        LogFactKind.Trade,
                        _moduleCredits >= 0
                            ? $"Spent {Credits(_moduleCredits)} net on modules."
                            : $"Came out {Credits(-_moduleCredits)} ahead selling modules."),

                    _missionsAccepted.Fact(LogFactKind.Mission, $"Took on {Count(_missionsAccepted.Count)} mission(s)."),

                    _missionsLost.Fact(
                        LogFactKind.Mission,
                        $"Failed or abandoned {Count(_missionsLost.Count)} mission(s)."),

                    _materials.Fact(
                        LogFactKind.Exploration,
                        $"Picked up {Count(_materialCount)} units of material."),

                    _hull.Fact(
                        LogFactKind.Mishap,
                        $"Took hull damage {Count(_hull.Count)} time(s); the worst of it left the hull at "
                        + $"{_worstHull * 100:N0}%."),

                    _srv.Fact(LogFactKind.OnFoot, $"Launched an SRV {Count(_srv.Count)} time(s)."),

                    _srvLost.Fact(LogFactKind.Mishap, $"Lost an SRV {Count(_srvLost.Count)} time(s)."),

                    _onFoot.Fact(
                        LogFactKind.OnFoot,
                        $"Went out on foot {Count(_onFoot.Count)} time(s)"
                        + (_settlements.Count > 0 ? $", around {Some(_settlements)}." : ".")),
                };

                foreach (var fact in facts)
                {
                    if (fact is not null)
                    {
                        yield return fact;
                    }
                }

                foreach (var roll in _engineering.Values.OrderBy(entry => entry.First))
                {
                    yield return roll.Fact();
                }

                if (_lastSystem is not null && _lastPlace is { } place)
                {
                    yield return new LogFact
                    {
                        Kind = LogFactKind.Closing,
                        Statement = _lastStation is null
                            ? $"Finished in {_lastSystem}."
                            : $"Finished docked at {_lastStation}, {_lastSystem}.",
                        At = place.At,
                        Sources = [place],
                        SourceCount = 1,
                    };
                }
            }
        }

        private void Where(JournalEvent journalEvent)
        {
            if (journalEvent.String("StarSystem") is { Length: > 0 } system)
            {
                _lastSystem = system;
                _systems.Add(system);
                _firstSystem ??= system;
            }

            _lastStation = journalEvent.Kind is "Docked" ? journalEvent.Named("StationName") : null;

            // The event that put the Commander here, kept so the closing line can cite it. It used
            // to cite a Location event at the last timestamp seen, which is a fabricated
            // provenance — a citation naming an event that never happened is the exact failure the
            // audit exists to catch, arriving from the side the audit cannot see.
            _lastPlace = new LogSource(journalEvent.Kind, journalEvent.Timestamp);
        }

        /// <summary>
        /// One engineering roll, folded into the module and blueprint it was for.
        /// <para>
        /// <b>Aggregated rather than listed, and the real corpus is why.</b> One evening at Tod
        /// McQuinn's is 113 <c>EngineerCraft</c> events across six modules — 25 near-identical
        /// facts and 88 dropped, which crowded out an entire session and told the model almost
        /// nothing. Rolled up, it is six facts that each say what was worked on, how many rolls it
        /// took and where it finished, which is what somebody writing the evening up would say.
        /// </para>
        /// </summary>
        private void Roll(JournalEvent journalEvent)
        {
            var module = ModuleNames.Readable(journalEvent.Named("Module"));
            var blueprint = ModuleNames.Readable(journalEvent.Named("BlueprintName"));
            var engineer = journalEvent.String("Engineer") ?? "an engineer";
            var key = $"{module} {blueprint} {engineer}";

            if (!_engineering.TryGetValue(key, out var roll))
            {
                roll = new Engineering(module, blueprint, engineer);
                _engineering[key] = roll;
            }

            roll.Add(journalEvent);
        }

        /// <summary>
        /// The first thing that happened, stated once. Whichever event establishes a place first
        /// wins — a window that opens mid-session has no <c>LoadGame</c> in it, and a log that
        /// began with nothing but a jump count would be missing the only sentence that says where
        /// the Commander was.
        /// </summary>
        private void Open(JournalEvent journalEvent, string statement)
        {
            if (_opened)
            {
                return;
            }

            _opened = true;
            Moment(journalEvent, LogFactKind.Opening, statement);
        }

        private void Moment(JournalEvent journalEvent, LogFactKind kind, string statement) =>
            _moments.Add(new LogFact
            {
                Kind = kind,
                Statement = statement,
                At = journalEvent.Timestamp,
                Sources = [new LogSource(journalEvent.Kind, journalEvent.Timestamp)],
                SourceCount = 1,
            });

        private string TopCommodity()
        {
            if (_commodities.Count == 0)
            {
                return string.Empty;
            }

            var best = _commodities.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).First();

            return $", mostly {best.Key}";
        }

        private static string Place(JournalEvent journalEvent) =>
            journalEvent.Named("StationName") is { Length: > 0 } station
                ? $"docked at {station}, {journalEvent.String("StarSystem")}"
                : $"in {journalEvent.String("StarSystem")}";

        private static string Ship(JournalEvent journalEvent)
        {
            // Spoken, not Named: Elite omits _Localised on plenty of ship symbols and writes
            // them lower case, so one log would call it an "anaconda" in one line and an
            // "Anaconda" in the next.
            var type = JournalJson.Spoken(journalEvent.Named("Ship")) ?? "a ship";

            return journalEvent.String("ShipName") is { Length: > 0 } name
                ? $"the {type} \"{name}\""
                : $"a {type}";
        }

        private static string Promotion(JournalEvent journalEvent)
        {
            var ranks = new[] { "Combat", "Trade", "Explore", "Soldier", "Exobiologist", "CQC", "Federation", "Empire" };

            var earned = ranks
                .Where(rank => journalEvent.Int(rank) is not null)
                .Select(rank => $"{rank} rank {journalEvent.Int(rank)!.Value.ToString(CultureInfo.InvariantCulture)}")
                .ToList();

            return earned.Count == 0
                ? "Was promoted."
                : $"Was promoted: {string.Join(", ", earned)}.";
        }

        private static string Death(JournalEvent journalEvent)
        {
            if (journalEvent.Named("KillerName") is { Length: > 0 } killer)
            {
                return $"Was killed by {killer}.";
            }

            var killers = journalEvent.Items("Killers").Count();

            return killers > 0
                ? $"Was killed by a wing of {killers.ToString(CultureInfo.InvariantCulture)}."
                : "Died.";
        }

        /// <summary>
        /// A few of a set, and how many more. Naming every station a trader docked at would be a
        /// wall of proper nouns the prose cannot use.
        /// </summary>
        private static string Some(SortedSet<string> names)
        {
            const int Named = 4;

            if (names.Count <= Named)
            {
                return string.Join(", ", names);
            }

            return string.Join(", ", names.Take(Named))
                + $" and {(names.Count - Named).ToString("N0", CultureInfo.InvariantCulture)} more";
        }

        private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

        private static string Credits(long value) => $"{value.ToString("N0", CultureInfo.InvariantCulture)} Cr";
    }
}
