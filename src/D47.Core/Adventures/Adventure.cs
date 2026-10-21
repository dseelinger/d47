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

    /// <summary>A <c>CarrierBuy</c> event for a fleet carrier.</summary>
    CarrierBuy,

    /// <summary>A <c>CarrierLocation</c> event that puts the Commander's fleet carrier in a different system from the last one seen.</summary>
    CarrierJump,

    /// <summary><c>WingJoin</c> and <c>WingAdd</c> events.</summary>
    Wing,

    /// <summary><c>JoinACrew</c> events.</summary>
    Multicrew,

    /// <summary>A <c>JoinedSquadron</c> event.</summary>
    Squadron,

    /// <summary>A <c>SquadronCreated</c> event.</summary>
    SquadronFound,

    /// <summary>
    /// A kill bond for one side of an active or pending war or civil war in the system the Commander is in, or a mission for one side
    /// of an active election with an influence mark in the election's system. <see cref="AdventureTrigger.Filter"/> limits it to one war type.
    /// </summary>
    Conflict,

    /// <summary><c>+</c> influence marks in the <c>FactionEffects</c> of <c>MissionCompleted</c> for one <see cref="AdventureTrigger.Faction"/>, optionally in one <see cref="AdventureTrigger.SystemAddress"/>.</summary>
    Faction,

    /// <summary>A <c>Loadout</c> whose ship has the jump range and a fuel scoop for the trip to a Guardian beacon, arriving in the beacon's system, or a <c>CarrierBuy</c>. Not counted.</summary>
    Reach,

    /// <summary><c>JetConeBoost</c> events. <see cref="AdventureTrigger.Filter"/> limits it to <c>neutron</c> (a <c>BoostValue</c> of 4 or more) or <c>whitedwarf</c>.</summary>
    Boost,

    /// <summary><c>Scan</c> events of bodies no one has discovered, each body once. <see cref="AdventureTrigger.Filter"/> limits it to a planet class, or <c>N</c> for a neutron star.</summary>
    Discover,

    /// <summary><c>Disembark</c> events onto a body whose scan said no one had walked there. Each body once.</summary>
    Footfall,

    /// <summary>Tons in <c>ColonisationContribution</c> events, optionally of one <see cref="AdventureTrigger.Commodity"/>.</summary>
    Contribute,

    /// <summary>A <c>ColonisationSystemClaim</c> event.</summary>
    Claim,

    /// <summary>A <c>TechnologyBroker</c> event that unlocks one tech broker row, named by <see cref="AdventureTrigger.Filter"/>. Not counted.</summary>
    Unlock,

    /// <summary>A <c>PowerplayJoin</c> event, for the Power named by <see cref="AdventureTrigger.Filter"/> or any Power when that is null.</summary>
    Pledge,

    /// <summary>A <c>PowerplayRank</c> event whose rank is at least <see cref="AdventureTrigger.Rank"/>.</summary>
    PowerRank,

    /// <summary>A Guardian site or tourist beacon, named by <see cref="AdventureTrigger.Filter"/>. Not counted, not authorable.</summary>
    Site,

    /// <summary>A <c>MissionAccepted</c> of one family, optionally for one faction and at one station, that binds <see cref="AdventureTrigger.Handle"/>. Not counted, not authorable.</summary>
    MissionTaken,

    /// <summary>Arriving at the destination of the mission bound to <see cref="AdventureTrigger.Handle"/>. Not counted, not authorable.</summary>
    MissionDestination,

    /// <summary>A <c>MissionCompleted</c> for the mission bound to <see cref="AdventureTrigger.Handle"/>. Not counted, not authorable.</summary>
    MissionComplete,

    /// <summary>A <c>MissionAbandoned</c> or <c>MissionFailed</c> for the mission bound to <see cref="AdventureTrigger.Handle"/>. Not counted, not authorable.</summary>
    MissionAbandon,

    /// <summary>The Commander says which of the step's <see cref="BeatStep.Options"/> they choose: the d47 event <see cref="AdventureChoice.EventName"/>, never a journal event. Not counted, not authorable.</summary>
    Choice,
}

/// <summary>Where a beat lands on the galaxy.</summary>
public sealed record AdventureTrigger
{
    /// <summary><see cref="Filter"/> of a <see cref="TriggerKind.Site"/> at a Guardian ruin or structure.</summary>
    public const string GuardianSite = "guardian";

    /// <summary><see cref="Filter"/> of a <see cref="TriggerKind.Site"/> at a tourist beacon.</summary>
    public const string TouristSite = "tourist";

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

    /// <summary>How many events, or tons for <see cref="TriggerKind.Sell"/>, <see cref="TriggerKind.Mine"/> and <see cref="TriggerKind.Contribute"/>, a counted beat waits for.</summary>
    public int? Count { get; init; }

    /// <summary>The faction a bond is awarded by or a mission is completed for, the side a conflict is fought for, or the faction a faction beat works for, as the journal spells it.</summary>
    public string? Faction { get; init; }

    /// <summary>A prefix of the mission's <c>Name</c>, such as <c>Mission_Courier</c>.</summary>
    public string? MissionFamily { get; init; }

    /// <summary>For the mission kinds: the story's name for one mission; a <see cref="TriggerKind.MissionTaken"/> step binds it and the later steps refer to it.</summary>
    public string? Handle { get; init; }

    /// <summary>The commodity sold or refined, compared as a folded symbol.</summary>
    public string? Commodity { get; init; }

    /// <summary>The cargo, item, genus, category, signal, wreck or source type, the tech broker row an unlock beat waits for, the Power a pledge beat waits for, the suit mod, the name of what is rescued, or the war type (<c>war</c>, <c>civilwar</c> or <c>election</c>), that a counted beat is limited to.</summary>
    public string? Filter { get; init; }

    /// <summary>For a tourist <see cref="TriggerKind.Site"/>: the beacon's name, as the signal index shows it.</summary>
    public string? SiteName { get; init; }

    /// <summary>For <see cref="TriggerKind.DataSale"/>: true for organic data only, false for cartographic data only, null for both.</summary>
    public bool? Organic { get; init; }

    /// <summary>For <see cref="TriggerKind.Codex"/>: true counts only the first time the Commander logs an entry in a region; false or null counts any.</summary>
    public bool? New { get; init; }

    /// <summary>For <see cref="TriggerKind.Reach"/>: the jump range, in light years, the ship needs for the trip.</summary>
    public double? JumpRange { get; init; }

    /// <summary>The engineer an <see cref="TriggerKind.Engineer"/> beat waits on.</summary>
    public string? Engineer { get; init; }

    /// <summary><c>Invited</c> or <c>Unlocked</c>; an engineer beat is also met by the stage after it.</summary>
    public string? Stage { get; init; }

    /// <summary>Whether this kind fires on a running total rather than on one event.</summary>
    public bool IsCounted => IsCountedKind(Kind);

    public static bool IsCountedKind(TriggerKind kind) =>
        kind >= TriggerKind.Bounty && kind is not (TriggerKind.Engineer or TriggerKind.Reach or TriggerKind.Unlock or TriggerKind.Pledge or TriggerKind.PowerRank or TriggerKind.Site or TriggerKind.Choice) && !IsOnceKind(kind) && !IsMissionHandleKind(kind);

    /// <summary>Whether this kind takes part in one named mission.</summary>
    public static bool IsMissionHandleKind(TriggerKind kind) =>
        kind is TriggerKind.MissionTaken or TriggerKind.MissionDestination or TriggerKind.MissionComplete or TriggerKind.MissionAbandon;

    /// <summary>Whether this kind is met by one event and carries nothing else.</summary>
    public static bool IsOnceKind(TriggerKind kind) =>
        kind is TriggerKind.CarrierBuy or TriggerKind.Squadron or TriggerKind.SquadronFound or TriggerKind.Claim;

    /// <summary>Whether a Commander can write this kind on the authored form.</summary>
    public static bool IsAuthorable(TriggerKind kind) => kind <= TriggerKind.Beacon;

    /// <summary>Whether the ids this kind matches on are all present.</summary>
    public bool IsResolved => IsCounted ? Count >= 1 && (Kind != TriggerKind.Faction || !string.IsNullOrWhiteSpace(Faction)) : Kind switch
    {
        TriggerKind.Engineer => !string.IsNullOrWhiteSpace(Engineer) && EngineerStages.Rank(Stage) > 0,
        TriggerKind.Arrive => SystemAddress is not null,
        TriggerKind.Dock => MarketId is not null,
        TriggerKind.Land or TriggerKind.Scan => SystemAddress is not null && BodyId is not null,
        TriggerKind.Rank => Career is not null && Rank is not null,
        TriggerKind.Board => ShipType is not null,
        TriggerKind.Reach => JumpRange > 0,
        TriggerKind.Unlock => Knowledge.TechBrokerCatalogue.ForName(Filter) is not null,
        TriggerKind.Pledge => Filter is null || Knowledge.GalaxyFilters.Powers.Contains(Filter),
        TriggerKind.PowerRank => Rank is > 0,
        TriggerKind.MissionTaken => !string.IsNullOrWhiteSpace(Handle) && (string.IsNullOrWhiteSpace(Station) || MarketId is not null),
        TriggerKind.MissionDestination or TriggerKind.MissionComplete or TriggerKind.MissionAbandon => !string.IsNullOrWhiteSpace(Handle),
        TriggerKind.Beacon => SystemAddress is { } address && GuardianCores.Beacons.ContainsKey(address),
        TriggerKind.Site => Filter switch
        {
            GuardianSite => SystemAddress is not null && BodyId is not null,
            TouristSite => SystemAddress is not null,
            _ => false,
        },
        TriggerKind.CarrierBuy or TriggerKind.Squadron or TriggerKind.SquadronFound or TriggerKind.Claim or TriggerKind.Choice => true,
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
        TriggerKind.Reach => $"fit out a ship that can reach the beacon in {System?.Trim() ?? "the Guardian beacon's system"}",
        TriggerKind.Beacon => $"scan the Guardian beacon in {System ?? BeaconName(SystemAddress) ?? Address(SystemAddress)}",
        TriggerKind.Site => Filter == GuardianSite
            ? $"fly to the Guardian site on {Body ?? Address(SystemAddress, BodyId)}{In()}"
            : $"scan the tourist beacon {SiteName?.Trim()}{In()}",
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
        TriggerKind.Codex => $"log {Counted(CodexWord(one: true), CodexWord(one: false))}",
        TriggerKind.DataSale => $"sell {Credits()} of {DataWord()} data",
        TriggerKind.Salvage => $"pick up {Counted(Cargo(one: true), Cargo(one: false))}",
        TriggerKind.Uss => $"drop into {Counted(Sources(one: true), Sources(one: false))}",
        TriggerKind.Rescue => $"hand in {Counted(Survivors(one: true), Survivors(one: false))}",
        TriggerKind.Engineer => $"reach {Stage?.Trim()} with {Engineer?.Trim()}",
        TriggerKind.Srv => $"launch the SRV {Times()}",
        TriggerKind.Crew => $"hire {Counted("crew member", "crew members")}",
        TriggerKind.SuitMod => $"apply {Counted(SuitMods(one: true), SuitMods(one: false))}",
        TriggerKind.Livery => $"change your ship's livery {Times()}",
        TriggerKind.CarrierBuy => "buy a fleet carrier",
        TriggerKind.CarrierJump => $"jump your fleet carrier to {Counted("new system", "new systems")}",
        TriggerKind.Wing => $"join a wing, or have someone join yours, {Times()}",
        TriggerKind.Multicrew => $"join another Commander's crew {Times()}",
        TriggerKind.Squadron => "join a squadron",
        TriggerKind.SquadronFound => "found a squadron",
        TriggerKind.Conflict => $"take part {Times()} in {WarWord()}{For()}",
        TriggerKind.Faction => $"earn {Counted("influence mark", "influence marks")} for {Faction?.Trim()}",
        TriggerKind.Boost => $"supercharge your frame shift drive in {BoostStar()} jet cone {Times()}",
        TriggerKind.Footfall => $"be the first to walk on {Counted("world", "worlds")}",
        TriggerKind.Discover => $"be the first to scan {Counted(DiscoverWord(one: true), DiscoverWord(one: false))}",
        TriggerKind.Contribute => $"deliver {Tons()} of {CommodityWord()} to colonisation construction sites",
        TriggerKind.Claim => "claim a system for colonisation",
        TriggerKind.Unlock => UnlockWord(),
        TriggerKind.Pledge => string.IsNullOrWhiteSpace(Filter) ? "pledge to a Power" : $"pledge to {Filter.Trim()}",
        TriggerKind.PowerRank => $"reach PowerPlay rank {Rank}",
        TriggerKind.MissionTaken => $"take one {Missions(one: true)}{For()}{At()}",
        TriggerKind.MissionDestination => $"go to the destination of the {Handle?.Trim()} mission",
        TriggerKind.MissionComplete => $"complete the {Handle?.Trim()} mission",
        TriggerKind.MissionAbandon => $"abandon the {Handle?.Trim()} mission",
        TriggerKind.Choice => "say which option you choose",
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
            TriggerKind.Codex => $"{Capital(CodexWord(one: false))}: {of}",
            TriggerKind.DataSale => $"{Capital(DataWord())} data sold: {of} cr",
            TriggerKind.Salvage => $"{Capital(Cargo(one: false))}: {of}",
            TriggerKind.Uss => $"{Capital(Sources(one: false))}: {of}",
            TriggerKind.Rescue => $"{Capital(Survivors(one: false))}: {of}",
            TriggerKind.Srv => $"SRV launches: {of}",
            TriggerKind.Crew => $"Crew hired: {of}",
            TriggerKind.SuitMod => $"{Capital(SuitMods(one: false))}: {of}",
            TriggerKind.Livery => $"Livery changes: {of}",
            TriggerKind.CarrierJump => $"Carrier jumps: {of}",
            TriggerKind.Wing => $"Wing joins: {of}",
            TriggerKind.Multicrew => $"Crews joined: {of}",
            TriggerKind.Conflict => $"Contributions to {WarWord()}{For()}: {of}",
            TriggerKind.Faction => $"Influence marks for {Faction?.Trim()}: {of}",
            TriggerKind.Boost => $"{Capital(BoostWord())} boosts: {of}",
            TriggerKind.Footfall => $"First footfalls: {of}",
            TriggerKind.Discover => $"{Capital(DiscoverWord(one: false))} scanned: {of}",
            TriggerKind.Contribute => string.IsNullOrWhiteSpace(Commodity)
                ? $"Tons delivered to construction sites: {of}"
                : $"Tons of {CommodityWord()} delivered to construction sites: {of}",
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
        TriggerKind.Site when Filter == TouristSite => $"Next: {Describe()} with the ship's data-link scanner.",
        TriggerKind.Reach =>
            $"Next: {Describe()}: a jump range of at least {JumpRange.GetValueOrDefault().ToString("0.0", CultureInfo.InvariantCulture)} light years and a fuel scoop. Arriving there in any ship also counts.",
        _ when IsCounted => $"Next: {Describe()}, counted from now. {Progress(0)}.",
        _ => $"Next: {Describe()}.",
    };

    private string Counted(string one, string many) =>
        Count == 1 ? $"one {one}" : $"{(Count ?? 0).ToString(CultureInfo.InvariantCulture)} {many}";

    private string UnlockWord() => Knowledge.TechBrokerCatalogue.ForName(Filter) is { } row
        ? $"unlock the {row.Name} at a {(row.BrokerType == Knowledge.TechBrokerCatalogue.Guardian ? "Guardian" : "human")} tech broker"
        : $"unlock {Filter?.Trim() ?? "an item"} at a tech broker";

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

    /// <summary>"a civil war" for <c>civilwar</c>, "a war, civil war or election" for none.</summary>
    private string WarWord() => Journal.JournalJson.Symbol(Filter) switch
    {
        "war" => "a war",
        "civilwar" => "a civil war",
        "election" => "an election",
        _ => "a war, civil war or election",
    };

    /// <summary>"neutron", "white dwarf", or "jet cone" for either.</summary>
    private string BoostWord() => Journal.JournalJson.Symbol(Filter) switch
    {
        "neutron" => "neutron",
        "whitedwarf" => "white dwarf",
        _ => "jet cone",
    };

    private string DiscoverWord(bool one) => AdventureWatch.DiscoverKey(Filter) switch
    {
        "earthlikebody" => one ? "undiscovered Earth-like world" : "undiscovered Earth-like worlds",
        "waterworld" => one ? "undiscovered water world" : "undiscovered water worlds",
        "ammoniaworld" => one ? "undiscovered ammonia world" : "undiscovered ammonia worlds",
        "gasgiantwithwaterbasedlife" => one ? "undiscovered water-life gas giant" : "undiscovered water-life gas giants",
        "gasgiantwithammoniabasedlife" => one ? "undiscovered ammonia-life gas giant" : "undiscovered ammonia-life gas giants",
        "n" => one ? "undiscovered neutron star" : "undiscovered neutron stars",
        _ => one ? "undiscovered body" : "undiscovered bodies",
    };

    private string BoostStar() => Journal.JournalJson.Symbol(Filter) switch
    {
        "neutron" => "a neutron star's",
        "whitedwarf" => "a white dwarf's",
        _ => "a neutron star's or white dwarf's",
    };

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

    /// <summary>"new codex entries", "Guardian codex entries", "new Guardian codex entry".</summary>
    private string CodexWord(bool one) =>
        (New == true ? "new " : string.Empty)
        + (string.IsNullOrWhiteSpace(Filter) ? string.Empty : Plain(Filter) + " ")
        + (one ? "codex entry" : "codex entries");

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

    /// <summary>What the Commander does to reach this beat, in order; the beat fires when its last step does.</summary>
    public IReadOnlyList<BeatStep> Steps { get; init; } = [];

    /// <summary>The first step's trigger; setting it replaces that step's trigger and keeps the rest.</summary>
    public AdventureTrigger Trigger
    {
        get => FirstStep.Trigger;
        init => Steps = [FirstStep with { Trigger = value }, .. Steps.Skip(1)];
    }

    /// <summary>The first step's events that fail the chapter; setting it replaces them and keeps the rest.</summary>
    public IReadOnlyList<AdventureTrigger> FailsOn
    {
        get => FirstStep.FailsOn;
        init => Steps = [FirstStep with { FailsOn = value }, .. Steps.Skip(1)];
    }

    /// <summary>What is at the first step's place that matters to the story; said in the hand-off, before arrival.</summary>
    public string? Reason
    {
        get => FirstStep.Reason;
        init => Steps = [FirstStep with { Reason = value }, .. Steps.Skip(1)];
    }

    private BeatStep FirstStep => Steps.Count > 0 ? Steps[0] : new BeatStep();

    /// <summary>What is said when this beat is reached, in order: one line, or up to <see cref="AdventureLimits.MaxLinesPerBeat"/>.</summary>
    public IReadOnlyList<AdventureLine> Lines { get; init; } = [];

    /// <summary>The first line's text; setting it replaces that text and keeps the rest.</summary>
    public string Line
    {
        get => Lines.Count > 0 ? Lines[0].Text : string.Empty;
        init => Lines = [(Lines.Count > 0 ? Lines[0] : new AdventureLine { Text = value }) with { Text = value }, .. Lines.Skip(1)];
    }

    /// <summary>The first line's speaker; setting it replaces that speaker and keeps the rest.</summary>
    public string? Speaker
    {
        get => Lines.Count > 0 ? Lines[0].Speaker : null;
        init => Lines = [(Lines.Count > 0 ? Lines[0] : new AdventureLine { Text = string.Empty }) with { Speaker = value }, .. Lines.Skip(1)];
    }

    public bool Equals(AdventureBeat? other) =>
        other is not null
        && Title == other.Title
        && Function == other.Function
        && Steps.SequenceEqual(other.Steps)
        && Lines.SequenceEqual(other.Lines);

    public override int GetHashCode() => HashCode.Combine(Title, Function, Steps.Count, Steps.Count > 0 ? Steps[0] : null, Lines.Count > 0 ? Lines[0] : null);

    /// <summary>The first step's hand-off.</summary>
    public string HandOff() => FirstStep.HandOff();

    /// <summary>Whether this beat begins with a choice, which the line before it offers.</summary>
    public bool OpensWithChoice => Steps.Count > 0 && Steps[0].IsChoice;

    /// <summary>
    /// The steps the Commander does with <paramref name="option"/> chosen: the choice, then the option's steps, then the rest of the
    /// beat unless the option ends the chapter. With no option chosen, or no choice, <see cref="Steps"/>.
    /// </summary>
    public IReadOnlyList<BeatStep> StepsWith(int? option)
    {
        if (!OpensWithChoice || option is not { } chosen || Steps[0].Options.ElementAtOrDefault(chosen) is not { } picked)
        {
            return Steps;
        }

        return [Steps[0], .. picked.Steps, .. picked.Ending is null ? Steps.Skip(1) : []];
    }

    /// <summary>The most steps this beat can have, whichever option is chosen.</summary>
    public int MostSteps => OpensWithChoice ? Steps[0].Options.Select((_, index) => StepsWith(index).Count).DefaultIfEmpty(Steps.Count).Max() : Steps.Count;
}

/// <summary>One thing to do within a beat: a trigger, the events that fail the chapter while it is the one waited on, and a reason.</summary>
public sealed record BeatStep
{
    public AdventureTrigger Trigger { get; init; } = new() { Kind = TriggerKind.Arrive };

    /// <summary>Events that fail the chapter while this step is the one it waits on; none counts to a total.</summary>
    public IReadOnlyList<AdventureTrigger> FailsOn { get; init; } = [];

    /// <summary>What is at a travel step's place that matters to the story; said in the hand-off, before arrival.</summary>
    public string? Reason { get; init; }

    /// <summary>For a <see cref="TriggerKind.Choice"/> step: its options, two or three, in the order they are numbered.</summary>
    public IReadOnlyList<BeatOption> Options { get; init; } = [];

    public bool IsChoice => Trigger.Kind == TriggerKind.Choice;

    public bool Equals(BeatStep? other) =>
        other is not null && Trigger == other.Trigger && Reason == other.Reason && FailsOn.SequenceEqual(other.FailsOn) && Options.SequenceEqual(other.Options);

    public override int GetHashCode() => HashCode.Combine(Trigger, Reason, FailsOn.Count, Options.Count);

    /// <summary>The trigger's hand-off, followed by the reason when there is one; for a choice, the phrase that chooses each option.</summary>
    public string HandOff() => IsChoice
        ? BeatOption.Saying(Options)
        : string.IsNullOrWhiteSpace(Reason) ? Trigger.HandOff() : $"{Trigger.HandOff()} {Reason.Trim()}";
}

/// <summary>One option of a choice: a short label, the steps it asks for, and the named ending it ends the chapter in, or none to rejoin the chapter.</summary>
public sealed record BeatOption
{
    /// <summary>The spoken numbers of the options, from the first.</summary>
    public static readonly IReadOnlyList<string> Numbers = ["one", "two", "three"];

    public const int MaxLabelLength = 40;

    public required string Label { get; init; }

    public IReadOnlyList<BeatStep> Steps { get; init; } = [];

    /// <summary>The name of the ending the chapter ends in once this option's steps are done, or null when the chapter carries on.</summary>
    public string? Ending { get; init; }

    /// <summary>What is said when this option's ending is reached.</summary>
    public string? EndingLine { get; init; }

    public bool Equals(BeatOption? other) =>
        other is not null && Label == other.Label && Ending == other.Ending && EndingLine == other.EndingLine && Steps.SequenceEqual(other.Steps);

    public override int GetHashCode() => HashCode.Combine(Label, Ending, Steps.Count);

    /// <summary>The phrase that chooses the option at <paramref name="index"/>, from zero: "choose option two".</summary>
    public static string Phrase(int index) => $"choose option {Numbers[index]}";

    /// <summary>"Say choose option one to fight, or choose option two to flee."</summary>
    public static string Saying(IReadOnlyList<BeatOption> options)
    {
        var each = options.Take(Numbers.Count).Select((option, index) => $"{Phrase(index)} to {option.Label.Trim()}").ToList();

        return each.Count switch
        {
            0 => "Say which option you choose.",
            1 => $"Say {each[0]}.",
            _ => $"Say {string.Join(", ", each.SkipLast(1))}, or {each[^1]}.",
        };
    }
}

/// <summary>One line of a beat and who says it.</summary>
public sealed record AdventureLine
{
    public required string Text { get; init; }

    /// <summary>A speaker id of the story, or null for the ship.</summary>
    public string? Speaker { get; init; }

    /// <summary>The cast id this narrator line introduces; such a line is not counted against <see cref="AdventureLimits.MaxLinesPerBeat"/>.</summary>
    public string? Introduces { get; init; }
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

    /// <summary>What is said when it begins, in order — the beat before the first beat: none, one line, or up to <see cref="AdventureLimits.MaxLinesPerBeat"/>.</summary>
    public IReadOnlyList<AdventureLine> OpeningLines { get; init; } = [];

    /// <summary>The opening's first line, or null when it has none; setting it replaces that text and keeps the rest.</summary>
    public string? Opening
    {
        get => OpeningLines.Count > 0 ? OpeningLines[0].Text : null;
        init => OpeningLines = value is null
            ? []
            : [(OpeningLines.Count > 0 ? OpeningLines[0] : new AdventureLine { Text = value }) with { Text = value }, .. OpeningLines.Skip(1)];
    }

    /// <summary>Who says the opening's first line in a story chapter: a speaker id of the story, or null for the ship.</summary>
    public string? OpeningSpeaker => OpeningLines.Count > 0 ? OpeningLines[0].Speaker : null;

    public IReadOnlyList<AdventureBeat> Beats { get; init; } = [];

    /// <summary>The speaker id of one line of a beat, or of the opening at beat -1; null for the ship.</summary>
    public string? SpeakerOf(int beat, int line) =>
        (beat < 0 ? OpeningLines : Beats.ElementAtOrDefault(beat)?.Lines)?.ElementAtOrDefault(line)?.Speaker;

    /// <summary>When the Commander pressed Begin.</summary>
    public DateTimeOffset? AcceptedAt { get; init; }

    /// <summary>Null unless abandoned.</summary>
    public DateTimeOffset? AbandonedAt { get; init; }

    /// <summary>The named ending the chapter reached, or null while it is under way or ran to its last beat.</summary>
    public AdventureEnding? Ending { get; init; }

    /// <summary>When the beats from <see cref="RewrittenFrom"/> on were last replaced; events before it do not count toward them.</summary>
    public DateTimeOffset? RewrittenAt { get; init; }

    /// <summary>The index of the first beat the last replacement wrote.</summary>
    public int? RewrittenFrom { get; init; }

    /// <summary>The step of <see cref="RewrittenFrom"/> the last replacement began at; steps before it were kept.</summary>
    public int RewrittenStep { get; init; }

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

    /// <summary>The steps fired without their journal event, by the story bench or the Commander, oldest first; the fold counts each as a fire at its time.</summary>
    public IReadOnlyList<AdventureStep> Stepped { get; init; } = [];

    /// <summary>The choices the Commander made, oldest first; the fold applies each at its time, in order with the journal.</summary>
    public IReadOnlyList<AdventureChoice> Choices { get; init; } = [];

    /// <summary>When the offer of a choice was last said; the choice can be made only after it.</summary>
    public AdventureOffer? Offered { get; init; }

    public bool IsBegun => AcceptedAt is not null;

    public bool IsAbandoned => AbandonedAt is not null;

    public bool IsEnded => Ending is not null;

    public bool IsFailed => Ending is { Name: AdventureEnding.Failed };

    /// <summary>A generated adventure nobody has agreed to yet.</summary>
    public bool IsDraft => Source == AdventureSource.Generated && AcceptedAt is null;

    /// <summary>Running: begun, not abandoned and without an ending.</summary>
    public bool IsActive => IsBegun && !IsAbandoned && !IsEnded;
}

/// <summary>A named ending a chapter reached, when, and why.</summary>
public sealed record AdventureEnding(string Name, DateTimeOffset At, string Reason)
{
    /// <summary>The name of the ending a <c>failsOn</c> trigger gives.</summary>
    public const string Failed = "Failed";
}

/// <summary>A step of a beat fired without its journal event at <paramref name="At"/>; <paramref name="By"/> is the <see cref="AdventureStanding.FiredBy"/> it records, the bench when null.</summary>
public sealed record AdventureStep(int Beat, DateTimeOffset At, string? By = null, int Step = 0);

/// <summary>The d47 event <see cref="EventName"/>: the Commander chose <paramref name="Option"/>, from zero, of the choice that begins beat <paramref name="Beat"/>.</summary>
public sealed record AdventureChoice(int Beat, int Option, DateTimeOffset At)
{
    /// <summary>The event's fixed name, and the <see cref="AdventureStanding.FiredBy"/> of the step it fires.</summary>
    public const string EventName = "ChoiceMade";
}

/// <summary>The offer of the choice that begins beat <paramref name="Beat"/> was said at <paramref name="At"/>.</summary>
public sealed record AdventureOffer(int Beat, DateTimeOffset At);

/// <summary>What choosing did: a refusal, or what to say back.</summary>
public sealed record AdventureChoiceOutcome(string? Refusal, string Reply)
{
    public const string NoChoice = "No story is waiting on a choice.";

    public static AdventureChoiceOutcome Refused(string refusal) => new(refusal, refusal);
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

    public const int MaxLinesPerBeat = 3;

    public const int MaxSpineLength = 700;

    /// <summary>How much of what was said is kept per story.</summary>
    public const int MaxTold = 60;
}
