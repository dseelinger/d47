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
/// All after the first seven are counted except engineer: they fire once enough of their event has happened since the beat became current.
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

    /// <summary><c>Disembark</c> onto a planet surface, optionally on one body.</summary>
    OnFoot,

    /// <summary>Items from <c>CollectItems</c>, optionally of one type or name.</summary>
    Collect,

    /// <summary><c>ScanOrganic</c> analyses, optionally of one genus.</summary>
    Organic,

    /// <summary><c>SAAScanComplete</c> surface maps, optionally of one body.</summary>
    Map,

    /// <summary><c>SAASignalsFound</c> bodies with a signal, optionally of one signal type.</summary>
    Signal,

    /// <summary><c>Touchdown</c> at a crashed ship, optionally of one wreck type.</summary>
    Wreck,

    /// <summary><c>CodexEntry</c> events, optionally of one category.</summary>
    Codex,

    /// <summary>Credits from exploration and organic data sales.</summary>
    DataSale,

    /// <summary><c>CollectCargo</c> events, optionally of one cargo type.</summary>
    Salvage,

    /// <summary><c>USSDrop</c> events, optionally of one signal source type.</summary>
    Uss,

    /// <summary>Items from <c>SearchAndRescue</c>, optionally of one name.</summary>
    Rescue,

    /// <summary><c>EngineerProgress</c> reaching a stage with one engineer. Not counted.</summary>
    Engineer,

    /// <summary><c>LaunchSRV</c> events.</summary>
    Srv,

    /// <summary><c>CrewHire</c> events.</summary>
    Crew,

    /// <summary>Suit or weapon mods that first appear in a <c>SuitLoadout</c>, optionally of one mod name.</summary>
    SuitMod,

    /// <summary><c>Loadout</c> events whose cosmetic slots differ from the ship's previous one.</summary>
    Livery,
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

    /// <summary>The cargo, item, genus, category, signal, wreck or source type, the suit mod, or the name of what is rescued, that a counted beat is limited to.</summary>
    public string? Filter { get; init; }

    /// <summary>For <see cref="TriggerKind.DataSale"/>: true for organic data only, false for cartographic data only, null for both.</summary>
    public bool? Organic { get; init; }

    /// <summary>The engineer an <see cref="TriggerKind.Engineer"/> beat waits on.</summary>
    public string? Engineer { get; init; }

    /// <summary><c>Invited</c> or <c>Unlocked</c>; an engineer beat is also met by the stage after it.</summary>
    public string? Stage { get; init; }

    /// <summary>Whether this kind fires on a running total rather than on one event.</summary>
    public bool IsCounted => IsCountedKind(Kind);

    public static bool IsCountedKind(TriggerKind kind) =>
        kind >= TriggerKind.Bounty && kind != TriggerKind.Engineer;

    /// <summary>Whether a Commander can write this kind on the authored form.</summary>
    public static bool IsAuthorable(TriggerKind kind) => kind <= TriggerKind.Beacon;

    /// <summary>Whether the ids this kind matches on are all present.</summary>
    public bool IsResolved => IsCounted ? Count >= 1 : Kind switch
    {
        TriggerKind.Engineer => !string.IsNullOrWhiteSpace(Engineer) && EngineerStages.Rank(Stage) > 0,
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
        TriggerKind.OnFoot => $"step out on foot onto a planet surface {Times()}",
        TriggerKind.Collect => $"collect {Counted(Items(one: true), Items(one: false))}",
        TriggerKind.Organic => $"analyse {Counted("organic sample", "organic samples")}{Of()}",
        TriggerKind.Map => $"map {Counted("body", "bodies")} with the surface mapper",
        TriggerKind.Signal => $"survey {Counted("body", "bodies")} with {Plain()} signals",
        TriggerKind.Wreck => $"touch down at {Counted(Wrecks(one: true), Wrecks(one: false))}",
        TriggerKind.Codex => $"log {Counted("codex entry", "codex entries")}{Of()}",
        TriggerKind.DataSale => $"sell {Credits()} of {DataWord()} data",
        TriggerKind.Salvage => $"pick up {Counted(Cargo(one: true), Cargo(one: false))}",
        TriggerKind.Uss => $"drop into {Counted(Sources(one: true), Sources(one: false))}",
        TriggerKind.Rescue => $"hand in {Counted(Survivors(one: true), Survivors(one: false))}",
        TriggerKind.Engineer => $"reach {Stage?.Trim()} with {Engineer?.Trim()}",
        TriggerKind.Srv => $"launch the SRV {Times()}",
        TriggerKind.Crew => $"hire {Counted("crew member", "crew members")}",
        TriggerKind.SuitMod => $"apply {Counted(SuitMods(one: true), SuitMods(one: false))}",
        TriggerKind.Livery => $"change your ship's livery {Times()}",
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
            TriggerKind.OnFoot => $"Surface disembarks: {of}",
            TriggerKind.Collect => $"{Capital(Items(one: false))}: {of}",
            TriggerKind.Organic => $"Organic analyses{Of()}: {of}",
            TriggerKind.Map => $"Bodies mapped: {of}",
            TriggerKind.Signal => $"Bodies with {Plain()} signals: {of}",
            TriggerKind.Wreck => $"{Capital(Wrecks(one: false))}: {of}",
            TriggerKind.Codex => $"Codex entries{Of()}: {of}",
            TriggerKind.DataSale => $"{Capital(DataWord())} data sold: {of} cr",
            TriggerKind.Salvage => $"{Capital(Cargo(one: false))}: {of}",
            TriggerKind.Uss => $"{Capital(Sources(one: false))}: {of}",
            TriggerKind.Rescue => $"{Capital(Survivors(one: false))}: {of}",
            TriggerKind.Srv => $"SRV launches: {of}",
            TriggerKind.Crew => $"Crew hired: {of}",
            TriggerKind.SuitMod => $"{Capital(SuitMods(one: false))}: {of}",
            TriggerKind.Livery => $"Livery changes: {of}",
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

    private string Times() => Count == 1 ? "once" : $"{(Count ?? 0).ToString(CultureInfo.InvariantCulture)} times";

    private string Credits() => $"{(Count ?? 0).ToString("N0", CultureInfo.InvariantCulture)} cr";

    private string Plain() => string.IsNullOrWhiteSpace(Filter) ? "any" : Plain(Filter);

    private string Of() => string.IsNullOrWhiteSpace(Filter) ? string.Empty : $" of {Plain(Filter)}";

    private static string Plain(string filter) => Journal.JournalJson.Symbol(filter)?.Split('_')[^1] ?? filter.Trim();

    private string Items(bool one) =>
        (string.IsNullOrWhiteSpace(Filter) ? string.Empty : Plain(Filter) + " ") + (one ? "item" : "items");

    private string SuitMods(bool one) =>
        (string.IsNullOrWhiteSpace(Filter) ? string.Empty : Plain(Filter) + " ") + (one ? "suit mod" : "suit mods");

    private string Wrecks(bool one) =>
        string.IsNullOrWhiteSpace(Filter) || string.Equals(Filter.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase)
            ? one ? "crashed Thargoid ship" : "crashed Thargoid ships"
            : $"{Filter.Trim()} {(one ? "wreck" : "wrecks")}";

    private string Cargo(bool one) =>
        (string.IsNullOrWhiteSpace(Filter) ? string.Empty : Plain(Filter) + " ") + (one ? "cargo canister" : "cargo canisters");

    private string Sources(bool one) =>
        (string.IsNullOrWhiteSpace(Filter) ? string.Empty : Plain(Filter) + " ") + (one ? "signal source" : "signal sources");

    private string Survivors(bool one) =>
        (string.IsNullOrWhiteSpace(Filter) ? string.Empty : Plain(Filter) + " ") + (one ? "rescue item" : "rescue items");

    private string DataWord() => Organic switch { true => "organic", false => "cartographic", _ => "exploration" };

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
