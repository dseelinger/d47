using System.Globalization;
using D47.Core.Persona;

namespace D47.Core.Adventures;

/// <summary>How an adventure arrived, which decides how much a Commander should trust its prose.</summary>
public enum AdventureSource
{
    /// <summary>The Commander's own words, written on the panel or in the file.</summary>
    Commander,

    /// <summary>Written by the ship's AI once, and accepted by a person before it could begin.</summary>
    Generated,
}

/// <summary>
/// The things a beat can wait for (Phase 47, "The trigger vocabulary is closed and the prose is free").
/// The last five are counted: they fire once enough of their event has happened since the beat became current.
/// </summary>
public enum TriggerKind
{
    /// <summary><c>FSDJump</c>, <c>Location</c> or <c>CarrierJump</c> into a <c>SystemAddress</c>.</summary>
    Arrive,

    /// <summary><c>Docked</c> at a <c>MarketID</c>.</summary>
    Dock,

    /// <summary><c>Touchdown</c> on a <c>SystemAddress</c> and <c>BodyID</c>.</summary>
    Land,

    /// <summary><c>Scan</c> of a <c>SystemAddress</c> and <c>BodyID</c>.</summary>
    Scan,

    /// <summary><c>Promotion</c> in a career to at least a rank.</summary>
    Rank,

    /// <summary><c>ShipyardNew</c> or <c>ShipyardSwap</c> into a <c>ShipType</c>.</summary>
    Board,

    /// <summary><c>DataScanned</c> while in a Guardian beacon system. Only a stock story's chapter one ends on one.</summary>
    Beacon,

    /// <summary><c>Bounty</c> events.</summary>
    Bounty,

    /// <summary><c>FactionKillBond</c> events, optionally for one <c>AwardingFaction</c>.</summary>
    Bond,

    /// <summary><c>MissionCompleted</c> events, optionally for one <c>Faction</c> and one mission family.</summary>
    Mission,

    /// <summary>Tons sold in <c>MarketSell</c>, optionally of one commodity or at one <c>MarketID</c>.</summary>
    Sell,

    /// <summary>Tons refined in <c>MiningRefined</c>, one per event, optionally of one commodity.</summary>
    Mine,
}

/// <summary>Where a beat lands on the galaxy.</summary>
public sealed record AdventureTrigger
{
    public required TriggerKind Kind { get; init; }

    public long? SystemAddress { get; init; }

    public long? MarketId { get; init; }

    public int? BodyId { get; init; }

    /// <summary>One of <see cref="Journal.RankState.Careers"/>, in the journal's own spelling.</summary>
    public string? Career { get; init; }

    /// <summary>The rank to reach, 1 to 8.</summary>
    public int? Rank { get; init; }

    /// <summary>The hull to board, as the journal spells it: lower case, such as <c>cobramkiii</c>.</summary>
    public string? ShipType { get; init; }

    public string? System { get; init; }

    public string? Station { get; init; }

    public string? Body { get; init; }

    /// <summary>How many events, or tons for <see cref="TriggerKind.Sell"/> and <see cref="TriggerKind.Mine"/>, a counted beat waits for.</summary>
    public int? Count { get; init; }

    /// <summary>The faction a bond is awarded by or a mission is completed for, as the journal spells it.</summary>
    public string? Faction { get; init; }

    /// <summary>A prefix of the mission's <c>Name</c>, such as <c>Mission_Courier</c>.</summary>
    public string? MissionFamily { get; init; }

    /// <summary>The commodity sold or refined, compared as a folded symbol.</summary>
    public string? Commodity { get; init; }

    /// <summary>Whether this kind fires on a running total rather than on one event.</summary>
    public bool IsCounted => IsCountedKind(Kind);

    public static bool IsCountedKind(TriggerKind kind) =>
        kind is TriggerKind.Bounty or TriggerKind.Bond or TriggerKind.Mission or TriggerKind.Sell or TriggerKind.Mine;

    /// <summary>Whether the ids this kind matches on are all present.</summary>
    public bool IsResolved => IsCounted ? Count >= 1 : Kind switch
    {
        TriggerKind.Arrive => SystemAddress is not null,
        TriggerKind.Dock => MarketId is not null,
        TriggerKind.Land or TriggerKind.Scan => SystemAddress is not null && BodyId is not null,
        TriggerKind.Rank => Career is not null && Rank is not null,
        TriggerKind.Board => ShipType is not null,
        TriggerKind.Beacon => SystemAddress is { } address && GuardianCores.Beacons.ContainsKey(address),
        _ => false,
    };

    /// <summary>The trigger in words — "arrive at Ossen's Lantern", "reach Exploration rank 6".</summary>
    public string Describe() => Kind switch
    {
        TriggerKind.Arrive => $"arrive at {System ?? Address(SystemAddress)}",
        TriggerKind.Dock => $"dock at {Station ?? Market(MarketId)}{In()}",
        TriggerKind.Land => $"land on {Body ?? Address(SystemAddress, BodyId)}{In()}",
        TriggerKind.Scan => $"scan {Body ?? Address(SystemAddress, BodyId)}{In()}",
        TriggerKind.Rank => $"reach {Careers.Word(Career)} rank {Rank}",
        TriggerKind.Board => $"board {Article(Knowledge.EliteSpecifications.HullName(ShipType) ?? ShipType ?? "an unknown ship")}",
        TriggerKind.Beacon => $"scan the Guardian beacon in {System ?? BeaconName(SystemAddress) ?? Address(SystemAddress)}",
        TriggerKind.Bounty => $"collect {Counted("bounty", "bounties")}",
        TriggerKind.Bond => $"earn {Counted("kill bond", "kill bonds")}{For()}",
        TriggerKind.Mission => $"complete {Counted(Missions(one: true), Missions(one: false))}{For()}",
        TriggerKind.Sell => $"sell {Tons()} of {CommodityWord()}{At()}",
        TriggerKind.Mine => $"refine {Tons()} of {CommodityWord()}",
        _ => Kind.ToString(),
    };

    /// <summary>A counted beat's running total in words — "Kill bonds for LTT 7786 Labour: 3 of 8" — or null for any other kind.</summary>
    public string? Progress(int done)
    {
        if (!IsCounted)
        {
            return null;
        }

        var of = $"{done.ToString(CultureInfo.InvariantCulture)} of {(Count ?? 0).ToString(CultureInfo.InvariantCulture)}";

        return Kind switch
        {
            TriggerKind.Bounty => $"Bounties: {of}",
            TriggerKind.Bond => $"Kill bonds{For()}: {of}",
            TriggerKind.Mission => $"{Capital(Missions(one: false))}{For()}: {of}",
            TriggerKind.Sell => $"{Capital(CommodityWord())} sold{At()}: {of} t",
            _ => $"{Capital(CommodityWord())} refined: {of} t",
        };
    }

    /// <summary>
    /// The trigger as a hand-off — "Next: dock at Maren Anchorage in Dyson's Hollow." — said with the
    /// beat before it.
    /// </summary>
    public string HandOff() => Kind switch
    {
        TriggerKind.Scan =>
            $"Next: {Describe()} — the ship's own scanner from supercruise does it, or a close pass; no surface scanner is needed, and simply going there counts if you have scanned it before.",
        TriggerKind.Beacon => $"Next: {Describe()} with the ship's data-link scanner.",
        _ when IsCounted => $"Next: {Describe()}, counted from now. {Progress(0)}.",
        _ => $"Next: {Describe()}.",
    };

    private string Counted(string one, string many) =>
        Count == 1 ? $"one {one}" : $"{(Count ?? 0).ToString(CultureInfo.InvariantCulture)} {many}";

    private string Tons() => $"{(Count ?? 0).ToString(CultureInfo.InvariantCulture)} t";

    private string For() => string.IsNullOrWhiteSpace(Faction) ? string.Empty : $" for {Faction.Trim()}";

    private string At() => Station is { Length: > 0 } station
        ? $" at {station}"
        : MarketId is { } market ? $" at market {market.ToString(CultureInfo.InvariantCulture)}" : string.Empty;

    private string CommodityWord() =>
        string.IsNullOrWhiteSpace(Commodity)
            ? "any commodity"
            : Commodity.TrimStart().StartsWith('$') ? Journal.JournalJson.Symbol(Commodity) ?? Commodity.Trim() : Commodity.Trim();

    /// <summary>"courier missions" for <c>Mission_Courier</c>, "missions" for any.</summary>
    private string Missions(bool one) =>
        (MissionFamilies.Word(MissionFamily) is { Length: > 0 } word ? word + " " : string.Empty) + (one ? "mission" : "missions");

    private static string Capital(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static string? BeaconName(long? address) =>
        address is { } known && GuardianCores.Beacons.TryGetValue(known, out var name) ? name : null;

    private static string Article(string name) =>
        name == "an unknown ship" ? name : (char.ToLowerInvariant(name[0]) is 'a' or 'e' or 'i' or 'o' ? "an " : "a ") + name;

    private string In() => System is { Length: > 0 } system && !string.Equals(system, Body, StringComparison.Ordinal)
        ? $" in {system}"
        : string.Empty;

    private static string Address(long? address, int? body = null) =>
        address is null
            ? "an unresolved place"
            : body is null ? $"system {address}" : $"body {body} of system {address}";

    private static string Market(long? market) => market is null ? "an unresolved station" : $"market {market}";
}

/// <summary>One dramatic function, anchored to a place (Phase 47, "Story, not a checklist").</summary>
public sealed record AdventureBeat
{
    /// <summary>The chapter's name, which is what the card shows.</summary>
    public required string Title { get; init; }

    /// <summary>Its place in the structure — setup, catalyst, midpoint, all is lost, finale.</summary>
    public string? Function { get; init; }

    public required AdventureTrigger Trigger { get; init; }

    /// <summary>What the ship's AI says when this beat is reached.</summary>
    public required string Line { get; init; }
}

/// <summary>
/// The story before the scenes — the blueprint a generated adventure writes in its own turn, and the
/// questions an authored one is offered in the craft's order.
/// </summary>
public sealed record AdventureSpine
{
    public string? Premise { get; init; }

    /// <summary>The outer goal — what the Commander is after in this story.</summary>
    public string? Want { get; init; }

    /// <summary>The inner one — the belief the story tests, and what it would cost to be wrong.</summary>
    public string? Stake { get; init; }

    /// <summary>Where it stops being what it looked like.</summary>
    public string? Turn { get; init; }

    /// <summary>What the last beat means.</summary>
    public string? Ending { get; init; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Premise) && string.IsNullOrWhiteSpace(Want) && string.IsNullOrWhiteSpace(Stake)
        && string.IsNullOrWhiteSpace(Turn) && string.IsNullOrWhiteSpace(Ending);
}

/// <summary>A story the Commander progresses through, tracked from their own journal (Phase 47).</summary>
public sealed record Adventure
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public AdventureSource Source { get; init; } = AdventureSource.Commander;

    public DateTimeOffset? Written { get; init; }

    /// <summary>The persona id that wrote a generated one, or null for the Commander or no persona.</summary>
    public string? WrittenBy { get; init; }

    public AdventureSpine? Spine { get; init; }

    /// <summary>The line spoken when it begins — the beat before the first beat.</summary>
    public string? Opening { get; init; }

    public IReadOnlyList<AdventureBeat> Beats { get; init; } = [];

    /// <summary>When the Commander pressed Begin.</summary>
    public DateTimeOffset? AcceptedAt { get; init; }

    /// <summary>Null unless abandoned.</summary>
    public DateTimeOffset? AbandonedAt { get; init; }

    /// <summary>
    /// The draft before the last revision, kept on a generated adventure that has not begun so Put it
    /// back costs a press and not a model call.
    /// </summary>
    public Adventure? Previous { get; init; }

    /// <summary>The key of the finished adventure this one is the next chapter of, or null.</summary>
    public string? Follows { get; init; }

    /// <summary>The stock story this adventure is a chapter of, or null.</summary>
    public string? StoryId { get; init; }

    /// <summary>
    /// What was actually said about this story, oldest first (asked for 2026-08-22) — the beats as the
    /// Commander heard them and the asides between them.
    /// </summary>
    public IReadOnlyList<AdventureTold> Told { get; init; } = [];

    public bool IsBegun => AcceptedAt is not null;

    public bool IsAbandoned => AbandonedAt is not null;

    /// <summary>A generated adventure nobody has agreed to yet.</summary>
    public bool IsDraft => Source == AdventureSource.Generated && AcceptedAt is null;

    /// <summary>Running: begun and not abandoned.</summary>
    public bool IsActive => IsBegun && !IsAbandoned;
}

/// <summary>Bounds, so a hand-edited file cannot be a novel and a model cannot be asked for one.</summary>
public static class AdventureLimits
{
    public const int MaxAdventures = 40;

    public const int MaxBeats = 12;

    public const int MaxNameLength = 80;

    public const int MaxTitleLength = 80;

    /// <summary>A beat's line or the opening.</summary>
    public const int MaxLineLength = 900;

    public const int MaxSpineLength = 700;

    /// <summary>How much of what was said is kept per story.</summary>
    public const int MaxTold = 60;
}
