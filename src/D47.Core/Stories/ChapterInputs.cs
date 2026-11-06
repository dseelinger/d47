using D47.Core.Adventures;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Stories;

/// <summary>The Commander's state and the story's terms that the chapter planner builds a drawn chapter from.</summary>
public sealed record ChapterInputs
{
    public string? System { get; init; }

    public StarPosition? Position { get; init; }

    /// <summary>The longest one hop may be, in light years.</summary>
    public double Reach { get; init; }

    public ShipLoadout Ship { get; init; } = ShipLoadout.Unknown;

    /// <summary>Hull symbols the Commander owns, the ship aboard included.</summary>
    public IReadOnlyList<string> OwnedHulls { get; init; } = [];

    public long? Credits { get; init; }

    public RankState Ranks { get; init; } = RankState.Empty;

    public CarrierState Carrier { get; init; } = CarrierState.None;

    public bool InSquadron { get; init; }

    public PowerplayPledge? Pledge { get; init; }

    public IReadOnlyList<Mission> Missions { get; init; } = [];

    /// <summary>A station whose docks are offline, which no step may dock at.</summary>
    public long? ClosedMarketId { get; init; }

    public StoryStage Stage { get; init; } = StoryStage.FunAndGames;

    public IReadOnlyList<DrawnActivity> Slots { get; init; } = [];

    public IReadOnlyList<GoalClimbStep> Goals { get; init; } = [];

    public AdventureBeacon? Beacon { get; init; }

    public AdventureDestination? Destination { get; init; }

    /// <summary>The distance from Sol the last stop must fall in; null where no band applies.</summary>
    public ChapterBand.Range? Band { get; init; }

    /// <summary>The home leg or the target the chapter travels toward; null where it travels none.</summary>
    public ChapterLeg? Leg { get; init; }

    /// <summary>The base the chapter ends near; null where the base rule does not hold the chapter.</summary>
    public ShipBase? Base { get; init; }

    /// <summary><c>RefusedActivities</c> keys the Commander refused for the story.</summary>
    public IReadOnlyList<string> Refused { get; init; } = [];

    public string? ChapterSize { get; init; }

    /// <summary><see cref="ChapterTags"/> every place the chapter draws is limited to, where some candidate has them.</summary>
    public IReadOnlySet<string> Tags { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The inputs for a chapter of <paramref name="ask"/> from <paramref name="state"/>; <paramref name="homeSystem"/> is the Home system setting.</summary>
    public static ChapterInputs Of(CommanderGameState? state, AdventureAsk ask, string? homeSystem = null)
    {
        ArgumentNullException.ThrowIfNull(ask);

        var ship = state?.Ship ?? ShipLoadout.Unknown;
        var carrier = state?.Carrier ?? CarrierState.None;
        var position = state?.Location.StarPos;
        var story = ask.Story;
        var ships = state?.Fleet.Ships ?? [];

        return new ChapterInputs
        {
            System = state?.Location.StarSystem,
            Position = position,
            Reach = AdventureGenerator.ReachOf(ask, position, ship.MaxJumpRange, carrier.Owned),
            Ship = ship,
            OwnedHulls = [.. new[] { ship.Type }.Concat(ships.Select(stored => stored.Type)).OfType<string>().Select(type => EliteSpecifications.HullSymbol(type) ?? type)],
            Credits = state?.Session.Balance,
            Ranks = state?.Ranks ?? RankState.Empty,
            Carrier = carrier,
            InSquadron = state?.Squadron.IsMember == true,
            Pledge = state?.Pledge,
            Missions = state?.Missions.Missions ?? [],
            ClosedMarketId = ask.Rewrite?.ClosedMarketId,
            Stage = StageOf(story?.Stage),
            Slots = story?.Slots ?? [],
            Goals = story?.Goals ?? [],
            Beacon = story?.Beacon,
            Destination = story?.Destination,
            Band = ChapterBand.Of(position, ask),
            Leg = ChapterPull.Of(ask, position),
            Base = ShipBases.Applies(ask) ? ShipBases.Of(ships, carrier.Owned, homeSystem) : null,
            Refused = story?.Refused ?? [],
            ChapterSize = story?.ChapterSize,
        };
    }

    /// <summary>The stage a story's stage name names; Fun and Games where it names none.</summary>
    public static StoryStage StageOf(string? name) =>
        Enum.GetValues<StoryStage>().FirstOrDefault(stage => string.Equals(StoryClues.StageName(stage), name?.Trim(), StringComparison.OrdinalIgnoreCase), StoryStage.FunAndGames);

    /// <summary>The pad a dock must have for <paramref name="hull"/>, or for the ship aboard when null; null where unknown.</summary>
    public PadSize? NeedsPad(string? hull = null) =>
        Enum.TryParse<PadSize>(EliteSpecifications.Ship(hull ?? Ship.Type)?.Pad, ignoreCase: true, out var size) ? size : null;

    /// <summary>Whether the Commander owns a hull of <paramref name="symbol"/>.</summary>
    public bool Owns(string symbol) => OwnedHulls.Contains(symbol, StringComparer.OrdinalIgnoreCase);
}
