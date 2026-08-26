using System.Globalization;
using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>
/// One journal event as a line a person can read
/// (<a href="https://github.com/dseelinger/d47/issues/51">#51</a>).
/// <para>
/// <b>In Core because it is a pure function from a <see cref="JournalEvent"/> to a string</b>, it
/// is the thing here most likely to be wrong about Elite, and it is only testable here. The
/// drawing belongs in the App.
/// </para>
/// <para>
/// <b>Where it is not confident, the bare event kind is the correct output.</b> The corpus carries
/// 221 distinct kinds and the detail pane beside this one shows every field of every one of them,
/// untouched — so a summary that is missing is a summary, and a summary that is <em>wrong</em> is a
/// data-accuracy defect. An event with no sentence lists as its own name and loses nothing, because
/// the pane next to it is exactly as complete as any other's.
/// </para>
/// <para>
/// <b>Every field named here was read off a real journal line rather than remembered.</b> That is
/// not a style note: the difference between <c>StationName</c> and <c>Name</c>, or between
/// <c>Body</c> and <c>BodyName</c>, is the difference between a sentence and a blank — and a
/// plausible guess produces a line that reads correctly and says nothing.
/// </para>
/// <para>
/// <b>Localised names win wherever Elite supplies one.</b> Frontier writes
/// <c>Name</c>/<c>Name_Localised</c> pairs where the first is a symbol — <c>fedcorecomposites</c>
/// against <c>Core Dynamics Composites</c> — and a Commander reads the second.
/// </para>
/// </summary>
public static class JournalSentence
{
    /// <summary>
    /// The kinds that are noise rather than events, hidden by default
    /// (<a href="https://github.com/dseelinger/d47/issues/51">#51</a>).
    /// <para>
    /// <b>Measured, not guessed.</b> Across 931 journals <c>FSSSignalDiscovered</c> and
    /// <c>ShipLocker</c> are 48% of the corpus by volume — 141 MB of ship-locker inventory alone —
    /// and a Commander wants to read neither. <c>Music</c> and <c>ReservoirReplenished</c> are the
    /// same shape: emitted constantly, describing nothing the Commander did.
    /// </para>
    /// <para>
    /// <b>A display filter and never a read filter.</b> The spine folds game state from these and
    /// must keep receiving every one; hiding is the page's business alone.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> Noise = new HashSet<string>(StringComparer.Ordinal)
    {
        "FSSSignalDiscovered",
        "ShipLocker",
        "Music",
        "ReservoirReplenished",
        "NpcCrewPaidWage",
        "Friends",
        "SquadronStartup",
        "ModuleInfo",
        "Scanned",
    };

    /// <summary>
    /// What this event says, in one line, or the bare kind where nothing honest is available.
    /// </summary>
    public static string For(JournalEvent entry)
    {
        var raw = entry.Raw;

        return entry.Kind switch
        {
            // ---- Flying -------------------------------------------------------------------
            "FSDJump" => Named(raw, "StarSystem") is { } jumped
                ? $"Jumped to {jumped}{Distance(raw)}"
                : "Jumped",
            "StartJump" => raw.String("JumpType") switch
            {
                "Hyperspace" => Named(raw, "StarSystem") is { } target
                    ? $"Charging to jump to {target}"
                    : "Charging the frame shift drive",
                "Supercruise" => "Entering supercruise",
                _ => "Frame shift drive charging",
            },
            "FSDTarget" => Named(raw, "Name") is { } plotted
                ? $"Course set for {plotted}{Remaining(raw)}"
                : "Course set",
            "SupercruiseEntry" => "Entered supercruise",
            "SupercruiseExit" => Named(raw, "Body") is { } dropped
                ? $"Dropped out of supercruise at {dropped}"
                : "Dropped out of supercruise",
            "SupercruiseDestinationDrop" => Named(raw, "Type") is { } destination
                ? $"Arrived at {destination}"
                : "Arrived at the destination",
            "Location" => Named(raw, "StarSystem") is { } located
                ? $"In {located}"
                : "Position reported",
            "ApproachBody" => Named(raw, "Body") is { } approached
                ? $"Approaching {approached}"
                : "Approaching a body",
            "LeaveBody" => Named(raw, "Body") is { } left
                ? $"Left {left}"
                : "Left orbit",
            "Touchdown" => Named(raw, "Body") is { } landed
                ? $"Touched down on {landed}"
                : "Touched down",
            "Liftoff" => Named(raw, "Body") is { } lifted
                ? $"Lifted off from {lifted}"
                : "Lifted off",
            "FuelScoop" => raw.Double("Scooped") is { } scooped
                ? $"Scooped {scooped:0.0} tonnes of fuel"
                : "Scooping fuel",
            "JetConeBoost" => "Boosted off a jet cone",
            "NavRoute" => "Route plotted",
            "NavRouteClear" => "Route cleared",
            "Interdicted" => raw.String("Interdictor") is { } by
                ? $"Interdicted by {by}"
                : "Interdicted",
            "Interdiction" => "Attempted an interdiction",
            "EscapeInterdiction" => "Escaped an interdiction",

            // ---- Stations and carriers ----------------------------------------------------
            "Docked" => Named(raw, "StationName") is { } station
                ? $"Docked at {station}{In(raw)}"
                : "Docked",
            "Undocked" => Named(raw, "StationName") is { } from
                ? $"Undocked from {from}"
                : "Undocked",
            "DockingRequested" => Named(raw, "StationName") is { } asked
                ? $"Requested docking at {asked}"
                : "Requested docking",
            "DockingGranted" => "Docking granted",
            "DockingDenied" => raw.String("Reason") is { } denied
                ? $"Docking denied — {Spaced(denied)}"
                : "Docking denied",
            "DockingCancelled" => "Docking request cancelled",
            "DockingTimeout" => "Docking request timed out",
            "ApproachSettlement" => Named(raw, "Name") is { } settlement
                ? $"Approaching {settlement}"
                : "Approaching a settlement",
            "CarrierJumpRequest" => Named(raw, "SystemName") is { } carrierTo
                ? $"Carrier jump requested to {carrierTo}"
                : "Carrier jump requested",
            "CarrierJump" => Named(raw, "StarSystem") is { } carrierAt
                ? $"Carrier jumped to {carrierAt}"
                : "Carrier jumped",
            "CarrierJumpCancelled" => "Carrier jump cancelled",
            "CarrierLocation" => Named(raw, "StarSystem") is { } carrierIn
                ? $"Carrier is in {carrierIn}"
                : "Carrier position reported",
            "CarrierNameChange" => Named(raw, "Name") is { } renamed
                ? $"Carrier renamed to {renamed}"
                : "Carrier renamed",
            "CarrierDepositFuel" => raw.Int("Amount") is { } tritium
                ? $"Deposited {tritium} tonnes of tritium into the carrier"
                : "Deposited tritium into the carrier",

            // ---- Exploration --------------------------------------------------------------
            "Scan" => Named(raw, "BodyName") is { } body
                ? $"Scanned {body}"
                : "Scanned a body",
            "ScanBaryCentre" => "Scanned a barycentre",
            "DiscoveryScan" => raw.Int("Bodies") is { } bodies
                ? $"Discovery scan found {bodies} bodies"
                : "Discovery scan",
            "FSSDiscoveryScan" => raw.Int("BodyCount") is { } counted
                ? $"System scan: {counted} bodies here"
                : "Scanned the system",
            "FSSAllBodiesFound" => "Every body in the system found",
            "SAAScanComplete" => Named(raw, "BodyName") is { } mapped
                ? $"Mapped {mapped}"
                : "Mapped a body",
            "SAASignalsFound" => Named(raw, "BodyName") is { } signals
                ? $"Signals found on {signals}{Signals(raw)}"
                : "Signals found",
            "NavBeaconScan" => "Scanned a nav beacon",
            "CodexEntry" => Named(raw, "Name") is { } codex
                ? $"Codex entry: {codex}"
                : "Codex entry recorded",
            "MultiSellExplorationData" => raw.Long("TotalEarnings") is { } earned
                ? $"Sold exploration data for {Credits(earned)}"
                : "Sold exploration data",
            "BuyExplorationData" => "Bought exploration data",
            "SellOrganicData" => "Sold organic data",

            // ---- Mining -------------------------------------------------------------------
            "ProspectedAsteroid" => Named(raw, "Content") is { } content
                ? $"Prospected an asteroid — {content}"
                : "Prospected an asteroid",
            "AsteroidCracked" => "Cracked an asteroid",
            "LaunchDrone" => raw.String("Type") is { } drone
                ? $"Launched a {Spaced(drone).ToLowerInvariant()} limpet"
                : "Launched a limpet",
            "BuyDrones" => raw.Int("Count") is { } bought
                ? $"Bought {bought} limpets"
                : "Bought limpets",
            "SellDrones" => raw.Int("Count") is { } sold
                ? $"Sold {sold} limpets"
                : "Sold limpets",

            // ---- Materials and engineering ------------------------------------------------
            "MaterialCollected" => Named(raw, "Name") is { } collected
                ? $"Collected {Count(raw)}{collected}"
                : "Collected a material",
            "MaterialDiscovered" => Named(raw, "Name") is { } discovered
                ? $"Discovered a new material: {discovered}"
                : "Discovered a new material",
            "MaterialTrade" => "Traded materials",
            "EngineerCraft" => raw.String("Engineer") is { } engineer
                ? $"{engineer} applied {Blueprint(raw)}{Grade(raw)}"
                : "Applied a modification",
            "EngineerProgress" => "Engineer progress",
            "EngineerContribution" => "Contributed to an engineer",
            "Synthesis" => raw.String("Name") is { } synthesised
                ? $"Synthesised {synthesised}"
                : "Synthesised something",
            "TechnologyBroker" => "Unlocked from a technology broker",

            // ---- Trade --------------------------------------------------------------------
            "MarketBuy" => Named(raw, "Type") is { } boughtGoods
                ? $"Bought {Count(raw)}{boughtGoods}{Cost(raw, "TotalCost")}"
                : "Bought cargo",
            "MarketSell" => Named(raw, "Type") is { } soldGoods
                ? $"Sold {Count(raw)}{soldGoods}{Cost(raw, "TotalSale")}"
                : "Sold cargo",
            "EjectCargo" => Named(raw, "Type") is { } ejected
                ? $"Ejected {Count(raw)}{ejected}"
                : "Ejected cargo",
            "CargoDepot" => "Cargo depot updated",
            "CollectCargo" => Named(raw, "Type") is { } scooped2
                ? $"Scooped {scooped2}"
                : "Scooped cargo",
            "RedeemVoucher" => raw.Long("Amount") is { } redeemed
                ? $"Redeemed {Spaced(raw.String("Type") ?? "voucher").ToLowerInvariant()} for {Credits(redeemed)}"
                : "Redeemed a voucher",
            "PayFines" => raw.Long("Amount") is { } fines
                ? $"Paid {Credits(fines)} in fines"
                : "Paid fines",
            "PayBounties" => raw.Long("Amount") is { } bounties
                ? $"Paid {Credits(bounties)} in bounties"
                : "Paid bounties",

            // ---- Ships and modules --------------------------------------------------------
            // Loadout carries Ship as a BARE SYMBOL with no localised twin, so this asked for the
            // Commander's own ship name and said nothing otherwise. That was solving a problem d47
            // had already solved and not asking: EliteSpecifications.HullSaid is the ladder - the
            // measured row, then the name read off the hull's own armour, then a spoken match -
            // and it exists because Frontier ships hulls before the community id list catches up.
            // "smallcombat01_nx" is a Kestrel Mk II and d47 has always been able to say so.
            //
            // Reported by the Commander, 2026-08-26, reading this formatter's own output.
            "Loadout" => Blank(raw.String("ShipName")) is { } named
                ? $"Loadout reported for {named} ({Hull(raw, "Ship")})"
                : $"Loadout reported for the {Hull(raw, "Ship")}",
            "ModuleBuy" => Named(raw, "BuyItem") is { } module
                ? $"Bought a {module}{Cost(raw, "BuyPrice")}"
                : "Bought a module",
            "ModuleSell" => Named(raw, "SellItem") is { } soldModule
                ? $"Sold a {soldModule}{Cost(raw, "SellPrice")}"
                : "Sold a module",
            "ModuleSellRemote" => Named(raw, "SellItem") is { } remote
                ? $"Sold a stored {remote}"
                : "Sold a stored module",
            "ModuleStore" => Named(raw, "StoredItem") is { } stored
                ? $"Stored a {stored}"
                : "Stored a module",
            "ModuleRetrieve" => Named(raw, "RetrievedItem") is { } retrieved
                ? $"Retrieved a {retrieved}"
                : "Retrieved a module",
            "ModuleBuyAndStore" => Named(raw, "BuyItem") is { } boughtStored
                ? $"Bought and stored a {boughtStored}"
                : "Bought and stored a module",
            "FetchRemoteModule" => "Sent for a stored module",
            "ShipyardSwap" => Named(raw, "ShipType") is not null
                ? $"Switched to the {Hull(raw, "ShipType")}"
                : "Switched ship",
            "ShipyardBuy" => Named(raw, "ShipType") is not null
                ? $"Bought a {Hull(raw, "ShipType")}"
                : "Bought a ship",
            "ShipyardSell" => Named(raw, "ShipType") is { } soldShip
                ? $"Sold a {soldShip}"
                : "Sold a ship",
            "ShipyardNew" => Named(raw, "ShipType") is { } newShip
                ? $"Took delivery of a {newShip}"
                : "Took delivery of a ship",
            "ShipyardTransfer" => Named(raw, "ShipType") is { } transferred
                ? $"Sent for the {transferred}"
                : "Sent for a stored ship",
            "SetUserShipName" => raw.String("UserShipName") is { } shipName
                ? $"Named the ship {shipName}"
                : "Renamed the ship",
            "RefuelAll" => raw.Double("Cost") is { } refuel
                ? $"Refuelled for {Credits((long)refuel)}"
                : "Refuelled",
            "RepairAll" => raw.Long("Cost") is { } repair
                ? $"Repaired for {Credits(repair)}"
                : "Repaired",
            "Repair" => Named(raw, "Item") is { } repaired
                ? $"Repaired the {repaired}"
                : "Repaired a module",
            "BuyAmmo" => "Restocked ammunition",
            "AfmuRepairs" => Named(raw, "Module") is { } afmu
                ? $"Field-repaired the {afmu}"
                : "Field repairs",
            "RebootRepair" => "Rebooted and repaired",
            "LaunchSRV" => Named(raw, "SRVType") is { } srv
                ? $"Deployed the {srv}"
                : "Deployed the SRV",
            "DockSRV" => "Docked the SRV",
            "SRVDestroyed" => "The SRV was destroyed",
            "RestockVehicle" => "Restocked a vehicle",

            // ---- Danger -------------------------------------------------------------------
            "UnderAttack" => raw.String("Target") is { } attacked
                ? $"Under attack — {attacked.ToLowerInvariant()}"
                : "Under attack",
            "HullDamage" => raw.Double("Health") is { } health
                ? $"Hull damage — {health * 100:0}% remaining"
                : "Hull damage",
            "HeatWarning" => "Heat warning",
            "HeatDamage" => "Taking heat damage",
            "ShieldState" => raw.Bool("ShieldsUp") ? "Shields back up" : "Shields down",
            "Died" => raw.String("KillerName") is { } killer
                ? $"Destroyed by {killer}"
                : "Destroyed",
            "Resurrect" => raw.Long("Cost") is { } rebuy
                ? $"Rebought the ship for {Credits(rebuy)}"
                : "Rebought the ship",
            // The localised crime name carries a symbol tail — "...no fire zone_hulldamage" — so
            // it is cut at the underscore. Found by reading the real output rather than by
            // reasoning about the schema.
            "CommitCrime" => Named(raw, "CrimeType") is { } crime
                ? $"Committed a crime: {Untailed(Spaced(crime)).ToLowerInvariant()}"
                : "Committed a crime",
            "CrimeVictim" => "Was the victim of a crime",

            // ---- Missions -----------------------------------------------------------------
            "MissionAccepted" => raw.String("LocalisedName") is { } mission
                ? $"Accepted: {mission}"
                : "Accepted a mission",
            "MissionCompleted" => raw.String("LocalisedName") is { } done
                ? $"Completed: {done}"
                : "Completed a mission",
            "MissionFailed" => raw.String("LocalisedName") is { } failed
                ? $"Failed: {failed}"
                : "Failed a mission",
            "MissionAbandoned" => raw.String("LocalisedName") is { } abandoned
                ? $"Abandoned: {abandoned}"
                : "Abandoned a mission",
            "MissionRedirected" => "A mission was redirected",
            "Missions" => "Mission list reported",

            // ---- Combat -------------------------------------------------------------------
            "Bounty" => raw.Long("TotalReward") is { } reward
                ? $"Bounty claimed{Against(raw)} — {Credits(reward)}"
                : "Bounty claimed",
            "ShipTargeted" => Named(raw, "Ship") is { } targeted
                ? $"Targeted a {targeted}"
                : "Target scanned",

            // ---- Rank and powers ----------------------------------------------------------
            "Promotion" => "Promoted",
            "Rank" => "Ranks reported",
            "Progress" => "Rank progress reported",
            "Reputation" => "Reputation reported",
            "PowerplayJoin" => raw.String("Power") is { } joined
                ? $"Pledged to {joined}"
                : "Pledged to a Power",
            "PowerplayLeave" => "Left a Power",
            "PowerplayMerits" => raw.Int("MeritsGained") is { } merits
                ? $"Earned {merits} merits"
                : "Earned merits",
            "PowerplayRank" => "Power rank changed",
            "Powerplay" => "Powerplay standing reported",

            // ---- On foot ------------------------------------------------------------------
            "SwitchSuitLoadout" => Named(raw, "SuitName") is { } suit
                ? $"Changed into the {suit}"
                : "Changed suit",
            "BuySuit" => Named(raw, "Name") is { } boughtSuit
                ? $"Bought the {boughtSuit}"
                : "Bought a suit",
            "BuyWeapon" => Named(raw, "Name") is { } weapon
                ? $"Bought the {weapon}"
                : "Bought a weapon",
            "UpgradeSuit" => "Upgraded a suit",
            "UpgradeWeapon" => "Upgraded a weapon",
            "BuyMicroResources" => "Bought on-foot materials",
            "SellMicroResources" => "Sold on-foot materials",
            "TradeMicroResources" => "Traded on-foot materials",
            "CollectItems" => "Picked up items",
            "DropItems" => "Dropped items",
            "UseConsumable" => Named(raw, "Name") is { } consumable
                ? $"Used a {consumable}"
                : "Used a consumable",
            "BookTaxi" => "Booked an Apex shuttle",
            "BookDropship" => "Booked a dropship",
            "CancelTaxi" => "Cancelled the shuttle",
            "DropshipDeploy" => "Deployed by dropship",

            // ---- Comms and crew -----------------------------------------------------------
            // ReceiveText and SendText deliberately have no sentence: the message itself is the
            // content and the page renders it in a muted colour, unformatted, because a player
            // who types ** must see ** rather than bold (#51).
            "CrewHire" => "Hired crew",
            "CrewFire" => "Dismissed crew",
            "CrewAssign" => "Assigned crew",
            "JoinACrew" => "Joined a crew",
            "QuitACrew" => "Left a crew",
            "WingInvite" => "Wing invitation",
            "WingJoin" => "Joined a wing",
            "WingLeave" => "Left a wing",
            "WingAdd" => "Someone joined the wing",

            // ---- Session ------------------------------------------------------------------
            "Fileheader" => "Journal opened",
            "Commander" => raw.String("Name") is { } commander
                ? $"Commander {commander}"
                : "Commander identified",
            "LoadGame" => Named(raw, "Ship") is not null
                ? $"Loaded, flying the {Hull(raw, "Ship")}"
                : "Game loaded",
            "Shutdown" => "Game closed",
            "NewCommander" => "New Commander created",
            "Screenshot" => "Screenshot taken",
            "Statistics" => "Statistics reported",
            "Cargo" => "Cargo reported",
            "Materials" => "Material inventory reported",
            "Outfitting" => "Outfitting list reported",
            "Shipyard" => "Shipyard list reported",
            "StoredShips" => "Stored ships reported",
            "StoredModules" => "Stored modules reported",
            "Market" => "Market list reported",

            // ---- Seen in the corpus and worth a line -------------------------------------
            "Embark" => raw.Bool("SRV") ? "Boarded the SRV" : "Boarded the ship",
            "Disembark" => raw.Bool("OnPlanet") ? "Stepped out onto the surface" : "Stepped out",
            "MiningRefined" => Named(raw, "Type") is { } refined
                ? $"Refined {refined}"
                : "Refined a fragment",
            "FactionKillBond" => raw.Long("Reward") is { } bond
                ? $"Combat bond — {Credits(bond)}"
                : "Combat bond claimed",
            "CargoTransfer" => "Transferred cargo",
            "SearchAndRescue" => "Handed in salvage",
            "USSDrop" => Named(raw, "USSType") is { } uss
                ? $"Dropped into a signal source — {uss}"
                : "Dropped into a signal source",
            "DatalinkScan" => "Scanned a datalink",
            "DatalinkVoucher" => "Datalink voucher",
            "DataScanned" => Named(raw, "Type") is { } scannedType
                ? $"Scanned {Spaced(scannedType).ToLowerInvariant()}"
                : "Scanned a data point",
            "CommunityGoal" => "Community goal standings",
            "CommunityGoalJoin" => Blank(raw.String("Name")) is { } goal
                ? $"Signed up for {goal}"
                : "Signed up for a community goal",
            "CommunityGoalReward" => raw.Long("Reward") is { } goalReward
                ? $"Community goal paid {Credits(goalReward)}"
                : "Community goal reward",
            "CommunityGoalDiscard" => "Left a community goal",
            "SuitLoadout" => Named(raw, "SuitName") is { } wearing
                ? $"Wearing the {wearing}"
                : "Suit loadout reported",
            "CreateSuitLoadout" => "Created a suit loadout",
            "DeleteSuitLoadout" => "Deleted a suit loadout",
            "RenameSuitLoadout" => "Renamed a suit loadout",
            "LoadoutEquipModule" => "Changed suit equipment",
            "CarrierStats" => "Carrier status reported",
            "CarrierBuy" => "Bought a fleet carrier",
            "CarrierDecommission" => "Carrier decommissioning",
            "CarrierBankTransfer" => "Carrier bank transfer",
            "CarrierFinance" => "Carrier finances reported",
            "CarrierCrewServices" => "Carrier crew services changed",
            "CarrierTradeOrder" => "Carrier trade order",
            "CarrierModulePack" => "Carrier module pack",
            "CarrierDockingPermission" => "Carrier docking permission changed",
            "ShipRedeemed" or "ShipyardRedeem" => "Redeemed a ship",
            "ShipyardBankDeposit" => "Deposited into the shipyard account",
            "PowerplayCollect" => "Collected Power cargo",
            "PowerplayDeliver" => "Delivered Power cargo",
            "DeliverPowerMicroResources" => "Delivered Power materials",
            "JoinedSquadron" => "Joined a squadron",
            "LeftSquadron" => "Left a squadron",
            "AppliedToSquadron" => "Applied to a squadron",
            "SquadronCreated" => "Created a squadron",
            "SquadronPromotion" => "Promoted in the squadron",
            "SharedBookmarkToSquadron" => "Shared a bookmark with the squadron",
            "ChangeCrewRole" => "Changed crew role",
            "RepairDrone" => "A repair limpet went to work",
            "Resupply" => "Resupplied",

            // ---- Colonisation -------------------------------------------------------------
            "ColonisationSystemClaim" => Named(raw, "StarSystem") is { } claimed
                ? $"Claimed {claimed} for colonisation"
                : "Claimed a system for colonisation",
            "ColonisationContribution" => "Contributed to a construction",
            "ColonisationBeaconDeployed" => "Deployed a colonisation beacon",
            "CompleteConstruction" => "Construction complete",

            // Everything else. 221 kinds exist and this covers what is worth covering; the rest
            // list as their own name beside a detail pane that is exactly as complete as any
            // other's, which is a summary that is missing rather than one that is wrong.
            _ => Spaced(entry.Kind),
        };
    }

    /// <summary>
    /// The localised name where Elite supplies one, then the raw. Frontier writes
    /// <c>X</c>/<c>X_Localised</c> pairs where the first is a symbol, and a Commander reads the
    /// second — <c>fedcorecomposites</c> against <c>Core Dynamics Composites</c>.
    /// </summary>
    private static string? Named(JsonElement raw, string property) =>
        Blank(raw.String(property + "_Localised")) ?? Blank(Symbol(raw.String(property)));

    /// <summary>
    /// A bare symbol made readable where no localised name came with it — Frontier wraps some in
    /// <c>$name;</c>, and a dollar sign in a summary line is a leaked implementation detail.
    /// </summary>
    private static string? Symbol(string? value)
    {
        if (value is not { Length: > 0 })
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith('$'))
        {
            trimmed = trimmed.TrimEnd(';')[1..];

            if (trimmed.EndsWith("_name", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[..^5];
            }
        }

        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// A hull, through the one ladder every caller is supposed to use. Frontier ships hulls before
    /// the community id list catches up, so <c>Knowledge.EliteSpecifications.HullSaid</c> reads
    /// the name off the hull's own armour rows where there is no measured one — which is how
    /// <c>smallcombat01_nx</c> says "Kestrel Mk II".
    /// </summary>
    private static string Hull(JsonElement raw, string property) =>
        Named(raw, property) is { } localised
            ? Knowledge.EliteSpecifications.HullSaid(localised)
            : "ship";

    /// <summary>
    /// Everything before the first underscore. Some of Elite's localised strings carry a symbol
    /// tail that a Commander should never see.
    /// </summary>
    private static string Untailed(string value) =>
        value.IndexOf('_') is var at && at > 0 ? value[..at].TrimEnd() : value;

    private static string Count(JsonElement raw) =>
        raw.Int("Count") is { } count and > 1
            ? $"{count.ToString("N0", CultureInfo.CurrentCulture)} × "
            : string.Empty;

    private static string In(JsonElement raw) =>
        Named(raw, "StarSystem") is { } system ? $", {system}" : string.Empty;

    private static string Distance(JsonElement raw) =>
        raw.Double("JumpDist") is { } distance ? $" — {distance:0.00} ly" : string.Empty;

    private static string Remaining(JsonElement raw) =>
        raw.Int("RemainingJumpsInRoute") is { } jumps and > 0
            ? $" — {jumps} jump{(jumps == 1 ? string.Empty : "s")} to go"
            : string.Empty;

    private static string Cost(JsonElement raw, string property) =>
        raw.Long(property) is { } amount and > 0 ? $" for {Credits(amount)}" : string.Empty;

    private static string Grade(JsonElement raw) =>
        raw.Int("Level") is { } level ? $", grade {level}" : string.Empty;

    /// <summary>
    /// <c>Armour_HeavyDuty</c> into <c>Armour Heavy Duty</c>.
    /// <para>
    /// The underscore is replaced <em>after</em> the camel split rather than before it: doing it
    /// first produced "Armour  Heavy Duty" with two spaces, which is the kind of thing only
    /// reading the real output finds.
    /// </para>
    /// </summary>
    private static string Blueprint(JsonElement raw) =>
        Blank(raw.String("BlueprintName")) is { } blueprint
            ? Spaced(blueprint.Replace('_', ' ')).Trim()
            : "a modification";

    private static string Against(JsonElement raw) =>
        Named(raw, "Target") is { } target ? $" on a {target}" : string.Empty;

    /// <summary>The signal types found, which is the half of a mapping worth reading.</summary>
    private static string Signals(JsonElement raw)
    {
        var types = raw.Items("Signals")
            .Select(signal => Blank(signal.String("Type_Localised")) ?? Blank(signal.String("Type")))
            .Where(type => type is not null)
            .Take(4)
            .ToList();

        return types.Count == 0 ? string.Empty : $" — {string.Join(", ", types)}";
    }

    private static string Credits(long amount) =>
        amount.ToString("N0", CultureInfo.CurrentCulture) + " Cr";

    /// <summary>
    /// <c>FSSDiscoveryScan</c> into <c>FSS Discovery Scan</c>. What an unhandled event falls back
    /// to, so a kind nobody wrote a sentence for still reads as words rather than as a token.
    /// </summary>
    private static string Spaced(string kind)
    {
        var said = new System.Text.StringBuilder(kind.Length + 8);

        for (var i = 0; i < kind.Length; i++)
        {
            // Never a second space where there already is one. Blueprint names arrive as
            // "Armour_HeavyDuty", and splitting the camel hump after an underscore that has
            // already become a space produced "Armour  Heavy Duty" - found by reading the real
            // output, which is the only way that class of fault surfaces.
            if (i > 0
                && char.IsUpper(kind[i])
                && !char.IsWhiteSpace(kind[i - 1])
                && kind[i - 1] != '_'
                && (!char.IsUpper(kind[i - 1]) || (i + 1 < kind.Length && char.IsLower(kind[i + 1]))))
            {
                said.Append(' ');
            }

            said.Append(kind[i]);
        }

        return said.ToString();
    }
}
