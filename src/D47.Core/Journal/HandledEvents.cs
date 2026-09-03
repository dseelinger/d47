using System.Collections.Frozen;

namespace D47.Core.Journal;

/// <summary>
/// The journal events d47 acts on — <b>one list, bound to the code by a gate</b>
/// (<a href="https://github.com/dseelinger/d47/issues/270">#270</a>).
/// <para>
/// Dispatch on an event's name is spread across dozens of files in three syntactic shapes —
/// <c>case "Docked":</c>, <c>Kind == "Docked"</c>, <c>Kind is "Docked" or "Undocked"</c> — and a
/// grep over two of the three missed <c>DockingGranted</c>, which <see cref="CarrierState"/>
/// plainly handles. So "does d47 handle this event?" had no trustworthy answer, and a new Elite
/// event that nothing matched fell through every consumer with no error and no log line — the
/// right runtime behaviour, and the reason the omission was invisible.
/// </para>
/// <para>
/// <b>This list is not what the code reads.</b> Every consumer still dispatches the way it did,
/// and nothing consults this at runtime except <see cref="Unhandled"/>. What makes it trustworthy
/// is <c>HandledEventsGateTests</c>, which compiles <c>src/</c> with the C# compiler, finds every
/// read of <see cref="JournalEvent.Kind"/> — including through a copy stored in a record — follows
/// each to the names it is compared against, and fails unless that set and this one are identical
/// in both directions. A name the code starts comparing against without being added here fails
/// the build; so does a name left here after its last consumer went; and so does a comparison of
/// the kind in a shape the gate cannot resolve — rather than being quietly left out, because a
/// check that under-reports is worse than none.
/// </para>
/// <para>
/// <b>Two blocks, because "handled" hides the question a Commander actually asks.</b> The
/// Journal File reading has a sentence for nearly every event Elite writes, and hides nine more
/// as noise; counting those as handled would answer "yes" for <c>Screenshot</c> and make the diff
/// against a corpus near-empty. So <see cref="ActedOn"/> is what something reacts to and
/// <see cref="NarratedOnly"/> is what only the reading knows — and "why did it say nothing when X
/// happened" is answered by X being in the second block far more often than by X being absent.
/// </para>
/// <para>
/// <b>What is deliberately not counted:</b> the donation scrubber's table in
/// <c>Diagnostics/Donation/JournalScrub.cs</c> also keys by event name, but it reads the name out
/// of the raw JSON rather than from a <see cref="JournalEvent"/>, and taking a person's name out
/// of a <c>Friends</c> event is not reacting to one.
/// </para>
/// <para>
/// <b>Three events are absent from both blocks and are not gaps.</b> <c>Backpack</c>,
/// <c>BackpackChange</c> and <c>ShipLocker</c> are answered by <see cref="SuitInventory"/> from
/// <c>Backpack.json</c> and <c>ShipLocker.json</c>, on purpose: the events carry the full contents
/// only sometimes and Elite rewrites the two files on every change. The corpus diff lists them
/// as unhandled, which is true of the journal and not of d47. Found the first time the diff was
/// read, and recorded here so it is not chased twice.
/// </para>
/// </summary>
public static class HandledEvents
{
    /// <summary>
    /// Events something reacts to: folded into game state, spoken about, planned from, mined for a
    /// goal, written into the logbook, or used to bound a log range. Alphabetical and one per
    /// line, so adding one is a one-line diff and the gate's "add these" message pastes in.
    /// </summary>
    public static readonly FrozenSet<string> ActedOn = FrozenSet.ToFrozenSet<string>(
    [
        "ApproachBody",
        "ApproachSettlement",
        "Bounty",
        "BuySuit",
        "BuyWeapon",
        "CarrierBuy",
        "CarrierDepositFuel",
        "CarrierJump",
        "CarrierJumpCancelled",
        "CarrierJumpRequest",
        "CarrierLocation",
        "CarrierStats",
        "CodexEntry",
        "ColonisationConstructionDepot",
        "ColonisationContribution",
        "Commander",
        "CommunityGoal",
        "CommunityGoalDiscard",
        "CommunityGoalJoin",
        "CommunityGoalReward",
        "CreateSuitLoadout",
        "CrewAssign",
        "CrewFire",
        "CrewHire",
        "Died",
        "Disembark",
        "Docked",
        "DockingGranted",
        "DockingRequested",
        "Embark",
        "EngineerCraft",
        "EngineerProgress",
        "FSDJump",
        "FSDTarget",
        "FSSSignalDiscovered",
        "FactionKillBond",
        "FuelScoop",
        "HeatDamage",
        "HullDamage",
        "Interdicted",
        "Interdiction",
        "LaunchSRV",
        "LeaveBody",
        "Liftoff",
        "LoadGame",
        "Loadout",
        "LoadoutEquipModule",
        "LoadoutRemoveModule",
        "Location",
        "MarketBuy",
        "MarketSell",
        "MaterialCollected",
        "MaterialDiscarded",
        "MaterialTrade",
        "Materials",
        "MissionAbandoned",
        "MissionAccepted",
        "MissionCompleted",
        "MissionFailed",
        "ModuleBuy",
        "ModuleSell",
        "ModuleSellRemote",
        "MultiSellExplorationData",
        "NpcCrewRank",
        "Powerplay",
        "PowerplayDefect",
        "PowerplayJoin",
        "PowerplayLeave",
        "Progress",
        "Promotion",
        "ProspectedAsteroid",
        "Rank",
        "ReceiveText",
        "RedeemVoucher",
        "Resurrect",
        "SAAScanComplete",
        "SAASignalsFound",
        "SRVDestroyed",
        "Scan",
        "ScanOrganic",
        "ScientificResearch",
        "SellExplorationData",
        "SellOrganicData",
        "SellSuit",
        "SellWeapon",
        "SetUserShipName",
        "ShieldState",
        "ShipyardBuy",
        "ShipyardNew",
        "ShipyardSell",
        "ShipyardSwap",
        "Shutdown",
        "StartJump",
        "StoredModules",
        "StoredShips",
        "SuitLoadout",
        "SupercruiseDestinationDrop",
        "SupercruiseEntry",
        "SupercruiseExit",
        "SwitchSuitLoadout",
        "Synthesis",
        "TechnologyBroker",
        "Touchdown",
        "UnderAttack",
        "Undocked",
        "UpgradeSuit",
        "UpgradeWeapon",
    ], StringComparer.Ordinal);

    /// <summary>
    /// Events only the Journal File reading knows: it has a sentence for each, or hides it as
    /// noise, and nothing else in d47 reacts to it. An event here is one d47 will show you and
    /// never speak about.
    /// </summary>
    public static readonly FrozenSet<string> NarratedOnly = FrozenSet.ToFrozenSet<string>(
    [
        "AfmuRepairs",
        "AppliedToSquadron",
        "AsteroidCracked",
        "BookDropship",
        "BookTaxi",
        "BuyAmmo",
        "BuyDrones",
        "BuyExplorationData",
        "BuyMicroResources",
        "CancelTaxi",
        "Cargo",
        "CargoDepot",
        "CargoTransfer",
        "CarrierBankTransfer",
        "CarrierCrewServices",
        "CarrierDecommission",
        "CarrierDockingPermission",
        "CarrierFinance",
        "CarrierModulePack",
        "CarrierNameChange",
        "CarrierTradeOrder",
        "ChangeCrewRole",
        "CollectCargo",
        "CollectItems",
        "ColonisationBeaconDeployed",
        "ColonisationSystemClaim",
        "CommitCrime",
        "CompleteConstruction",
        "CrimeVictim",
        "DataScanned",
        "DatalinkScan",
        "DatalinkVoucher",
        "DeleteSuitLoadout",
        "DeliverPowerMicroResources",
        "DiscoveryScan",
        "DockSRV",
        "DockingCancelled",
        "DockingDenied",
        "DockingTimeout",
        "DropItems",
        "DropshipDeploy",
        "EjectCargo",
        "EngineerContribution",
        "EscapeInterdiction",
        "FSSAllBodiesFound",
        "FSSDiscoveryScan",
        "FetchRemoteModule",
        "Fileheader",
        "Friends",
        "HeatWarning",
        "JetConeBoost",
        "JoinACrew",
        "JoinedSquadron",
        "LaunchDrone",
        "LeftSquadron",
        "Market",
        "MaterialDiscovered",
        "MiningRefined",
        "MissionRedirected",
        "Missions",
        "ModuleBuyAndStore",
        "ModuleInfo",
        "ModuleRetrieve",
        "ModuleStore",
        "Music",
        "NavBeaconScan",
        "NavRoute",
        "NavRouteClear",
        "NewCommander",
        "NpcCrewPaidWage",
        "Outfitting",
        "PayBounties",
        "PayFines",
        "PowerplayCollect",
        "PowerplayDeliver",
        "PowerplayMerits",
        "PowerplayRank",
        "QuitACrew",
        "RebootRepair",
        "RefuelAll",
        "RenameSuitLoadout",
        "Repair",
        "RepairAll",
        "RepairDrone",
        "Reputation",
        "ReservoirReplenished",
        "RestockVehicle",
        "Resupply",
        "ScanBaryCentre",
        "Scanned",
        "Screenshot",
        "SearchAndRescue",
        "SellDrones",
        "SellMicroResources",
        "SendText",
        "SharedBookmarkToSquadron",
        "ShipLocker",
        "ShipRedeemed",
        "ShipTargeted",
        "Shipyard",
        "ShipyardBankDeposit",
        "ShipyardRedeem",
        "ShipyardTransfer",
        "SquadronCreated",
        "SquadronPromotion",
        "SquadronStartup",
        "Statistics",
        "TradeMicroResources",
        "USSDrop",
        "UseConsumable",
        "WingAdd",
        "WingInvite",
        "WingJoin",
        "WingLeave",
    ], StringComparer.Ordinal);

    /// <summary>Both blocks: whether d47 knows the event at all.</summary>
    public static readonly FrozenSet<string> All = ActedOn.Concat(NarratedOnly).ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Of the event names seen — in a corpus, a session, a Frontier update's notes — the ones
    /// nothing in d47 matches. The question that finds the defects nobody reported, and the one
    /// <c>spike/CorpusReplay</c> answers on every run.
    /// </summary>
    public static IReadOnlyList<string> Unhandled(IEnumerable<string> seen) =>
        [.. seen.Where(kind => !All.Contains(kind)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
