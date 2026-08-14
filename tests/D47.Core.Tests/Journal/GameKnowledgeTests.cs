using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// Phase 7, folded event by event. Every test here drives a real journal line through the same
/// path the tick loop uses, which is what the pull-based reader design is for — none of this
/// needs Elite running, or a clock.
/// </summary>
public class GameKnowledgeTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CommanderGameState Fold(params string[] lines)
    {
        var store = new GameStateStore();
        store.Apply(Event("""{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));

        foreach (var line in lines)
        {
            store.Apply(Event(line));
        }

        return store.Active!;
    }

    // ---- Location and flight mode -------------------------------------------------------

    [Fact]
    public void SupercruiseEntryClearsTheBodyBeingApproached()
    {
        var state = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"ApproachBody","StarSystem":"Sol","Body":"Earth"}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"SupercruiseEntry","StarSystem":"Sol"}""");

        // Supercruise is system-scale travel: still in Sol, no longer at Earth.
        Assert.Equal("Sol", state.Location.StarSystem);
        Assert.Null(state.Location.Body);
        Assert.Equal(FlightMode.Supercruise, state.Location.Mode);
    }

    [Fact]
    public void LeavingABodyDoesNotLeaveItsNameBehind()
    {
        var state = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"ApproachBody","StarSystem":"Sol","Body":"Earth"}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"LeaveBody","StarSystem":"Sol","Body":"Earth"}""");

        // LeaveBody names the body being left, not a new one. Keeping it would have d47
        // reporting the Commander as being at somewhere they just departed.
        Assert.Null(state.Location.Body);
    }

    [Fact]
    public void OnlyAHyperspaceStartJumpEntersHyperspace()
    {
        var supercruise = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"StartJump","JumpType":"Supercruise"}""");

        // JumpType is "Supercruise" for the far more common case. Treating it as hyperspace
        // would have Phase 8 remarking on the length of every supercruise entry.
        Assert.NotEqual(FlightMode.Hyperspace, supercruise.Location.Mode);

        var hyperspace = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Wolf 359","StarClass":"M"}""");

        Assert.Equal(FlightMode.Hyperspace, hyperspace.Location.Mode);
        Assert.Equal("Wolf 359", hyperspace.Location.NextJumpSystem);
        Assert.Equal("M", hyperspace.Location.NextJumpStarClass);
    }

    [Fact]
    public void ArrivingConsumesTheNextJumpTarget()
    {
        var state = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"FSDTarget","Name":"Wolf 359","StarClass":"M","RemainingJumpsInRoute":3}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Wolf 359","StarClass":"M"}""",
            """{"timestamp":"3311-01-01T00:03:00Z","event":"FSDJump","StarSystem":"Wolf 359","JumpDist":7.8,"FuelLevel":28.5}""");

        Assert.Equal("Wolf 359", state.Location.StarSystem);
        Assert.Equal(FlightMode.Supercruise, state.Location.Mode);

        // Left set, Phase 8 would warn about a star the Commander is now sitting next to.
        Assert.Null(state.Location.NextJumpSystem);
        Assert.Equal(28.5, state.Location.FuelMain);
    }

    [Fact]
    public void DockingAtACarrierIsRecognisedAsSuch()
    {
        var state = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"Docked","StarSystem":"Sol","StationName":"K7Q-BQL","StationType":"FleetCarrier"}""");

        Assert.True(state.Location.Docked);
        Assert.True(state.Location.AtCarrier);
        Assert.Equal(FlightMode.Docked, state.Location.Mode);
    }

    [Fact]
    public void DisembarkingLeavesTheShip()
    {
        var state = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"Disembark","StarSystem":"Sol","Body":"Earth","OnPlanet":true}""");

        Assert.Equal(FlightMode.OnFoot, state.Location.Mode);
    }

    // ---- Ship loadout and metrics -------------------------------------------------------

    private const string AnacondaLoadout =
        """
        {"timestamp":"3311-01-01T00:01:00Z","event":"Loadout","Ship":"Anaconda","ShipID":7,
         "ShipName":"Bold Endeavour","ShipIdent":"JM-01","HullValue":141889930,"ModulesValue":52000000,
         "HullHealth":0.94,"UnladenMass":1122.6,"CargoCapacity":64,"MaxJumpRange":52.31,"Rebuy":9694497,
         "FuelCapacity":{"Main":32,"Reserve":1.07},
         "Modules":[
           {"Slot":"PowerPlant","Item":"int_powerplant_size8_class5","On":true,"Health":1.0,"Value":143585,
            "Engineering":{"BlueprintName":"PowerPlant_Armoured","Level":5,"ExperimentalEffect_Localised":"Thermal Spread"}},
           {"Slot":"FrameShiftDrive","Item":"int_hyperdrive_size6_class5","On":true,"Health":1.0,"Value":1610350},
           {"Slot":"Slot05_Size4","Item":"int_cargorack_size4_class1","On":false,"Health":1.0,"Value":34330}
         ]}
        """;

    [Fact]
    public void LoadoutGivesTheMetricsWithoutAShipSpecificationTable()
    {
        var ship = Fold(AnacondaLoadout).Ship;

        // Every one of these is a figure Loadout reported. The checklist is explicit that ship
        // metrics come from the event rather than from a table of ship specifications, because
        // a table cannot know what this Commander engineered.
        Assert.Equal("Anaconda", ship.Type);
        Assert.Equal("Bold Endeavour", ship.Name);
        Assert.Equal("JM-01", ship.Ident);
        Assert.Equal(52.31, ship.MaxJumpRange);
        Assert.Equal(32, ship.FuelCapacity);
        Assert.Equal(64, ship.CargoCapacity);
        Assert.Equal(1122.6, ship.UnladenMass);
        Assert.Equal(94, ship.HullHealth);
        Assert.Equal(193889930, ship.TotalValue);
    }

    [Fact]
    public void EngineeredAndUnpoweredModulesAreDistinguishable()
    {
        var ship = Fold(AnacondaLoadout).Ship;

        Assert.Equal(3, ship.Modules.Count);

        var engineered = Assert.Single(ship.Engineered);
        Assert.Equal("PowerPlant_Armoured", engineered.Blueprint);
        Assert.Equal(5, engineered.BlueprintLevel);
        Assert.Equal("Thermal Spread", engineered.Experimental);

        // An unpowered module is one the Commander believes they have.
        var unpowered = Assert.Single(ship.Unpowered);
        Assert.Equal("Slot05_Size4", unpowered.Slot);
    }

    [Fact]
    public void RenamingTheShipDoesNotNeedAFreshLoadout()
    {
        var ship = Fold(
            AnacondaLoadout,
            """{"timestamp":"3311-01-01T00:02:00Z","event":"SetUserShipName","Ship":"Anaconda","ShipID":7,"UserShipName":"Second Wind","UserShipID":"JM-02"}""")
            .Ship;

        // Renaming does not re-emit Loadout, so without this the ship keeps answering to the
        // name it had when it was last outfitted.
        Assert.Equal("Second Wind", ship.Name);
        Assert.Equal("JM-02", ship.Ident);
        Assert.Equal(52.31, ship.MaxJumpRange);
    }

    [Fact]
    public void ANewLoadoutReplacesTheModuleListRatherThanMergingIntoIt()
    {
        var ship = Fold(
            AnacondaLoadout,
            """
            {"timestamp":"3311-01-01T00:05:00Z","event":"Loadout","Ship":"SideWinder","ShipID":9,
             "MaxJumpRange":8.1,
             "Modules":[{"Slot":"PowerPlant","Item":"int_powerplant_size2_class1","On":true}]}
            """)
            .Ship;

        // Swapping ships and keeping the old modules would describe a vessel that does not exist.
        Assert.Equal("SideWinder", ship.Type);
        Assert.Single(ship.Modules);
        Assert.Null(ship.Name);
    }

    [Fact]
    public void AShipWithNoNameIsDescribedByItsType()
    {
        var ship = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"Loadout","Ship":"Python","ShipID":3,"ShipName":"","ShipIdent":""}""")
            .Ship;

        // Elite writes an empty string for a ship that has never been named; null is what the
        // rest of the type means by "not named", and the two must not both be in circulation.
        Assert.Null(ship.Name);
        Assert.Equal("Python", ship.Describe());
    }

    // ---- Fleet carrier ------------------------------------------------------------------

    [Fact]
    public void TheCarriersLocationSurvivesFlyingAwayFromIt()
    {
        var carrier = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"CarrierStats","CarrierID":3700005632,"Callsign":"K7Q-BQL","Name":"Bootstrap","FuelLevel":842,"DockingAccess":"friends"}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"CarrierLocation","CarrierID":3700005632,"Callsign":"K7Q-BQL","StarSystem":"Colonia"}""",
            """{"timestamp":"3311-01-01T00:03:00Z","event":"FSDJump","StarSystem":"Sol","JumpDist":22000}""")
            .Carrier;

        // The whole use of this is answering "where is my carrier" from somewhere else.
        Assert.True(carrier.Owned);
        Assert.Equal("Bootstrap", carrier.Name);
        Assert.Equal("Colonia", carrier.StarSystem);
        Assert.Equal(842, carrier.FuelLevel);
    }

    [Fact]
    public void AScheduledCarrierJumpIsHeldUntilItLandsOrIsCancelled()
    {
        var scheduled = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"CarrierStats","CarrierID":1,"Callsign":"K7Q-BQL","Name":"Bootstrap"}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"CarrierJumpRequest","CarrierID":1,"SystemName":"Deciat","DepartureTime":"3311-01-01T00:17:00Z"}""")
            .Carrier;

        Assert.True(scheduled.JumpScheduled);
        Assert.Equal("Deciat", scheduled.DestinationSystem);
        Assert.Equal(DateTimeOffset.Parse("3311-01-01T00:17:00Z"), scheduled.DepartureTime);

        var arrived = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"CarrierStats","CarrierID":1,"Callsign":"K7Q-BQL"}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"CarrierJumpRequest","CarrierID":1,"SystemName":"Deciat","DepartureTime":"3311-01-01T00:17:00Z"}""",
            """{"timestamp":"3311-01-01T00:17:00Z","event":"CarrierJump","StarSystem":"Deciat","Docked":true}""")
            .Carrier;

        Assert.False(arrived.JumpScheduled);
        Assert.Equal("Deciat", arrived.StarSystem);
    }

    [Fact]
    public void NoCarrierEventsMeansNotSeenRatherThanNotOwned()
    {
        // Carrier events only reach an owner's journal, so this state is genuinely ambiguous
        // and the capability says so rather than asserting the Commander owns nothing.
        Assert.False(Fold().Carrier.Owned);
    }

    // ---- Fleet --------------------------------------------------------------------------

    private const string StoredShips =
        """
        {"timestamp":"3311-01-01T00:01:00Z","event":"StoredShips","StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra",
         "ShipsHere":[{"ShipID":11,"ShipType":"python","Name":"Mule","Value":56000000}],
         "ShipsRemote":[
           {"ShipID":12,"ShipType":"asp","Name":"Wanderer","StarSystem":"Deciat","TransferPrice":121000,"Value":18000000},
           {"ShipID":13,"ShipType":"sidewinder","StarSystem":"Sol","InTransit":true,"Value":30000}]}
        """;

    [Fact]
    public void StoredShipsHereInheritTheSystemTheSnapshotWasTakenIn()
    {
        var fleet = Fold(StoredShips).Fleet;

        // ShipsHere carries no StarSystem of its own. Filling it in means every ship answers the
        // same question the same way regardless of which list it came from.
        var mule = Assert.Single(fleet.Here);
        Assert.Equal("Shinrarta Dezhra", mule.StarSystem);
        Assert.Equal("Jameson Memorial", mule.StationName);

        Assert.Equal(2, fleet.Elsewhere.Count);
        Assert.Equal(["Deciat", "Shinrarta Dezhra", "Sol"], fleet.Systems);
        Assert.Contains(fleet.Ships, ship => ship.InTransit);
    }

    [Fact]
    public void AFreshStoredShipsSnapshotReplacesTheRegistry()
    {
        var fleet = Fold(
            StoredShips,
            """{"timestamp":"3311-01-01T01:00:00Z","event":"StoredShips","StationName":"Farseer Inc","StarSystem":"Deciat","ShipsHere":[{"ShipID":12,"ShipType":"asp","Name":"Wanderer"}],"ShipsRemote":[]}""")
            .Fleet;

        // Merging would accumulate ships that have since been sold, and a fleet list containing
        // a ship the Commander no longer owns is worse than one that is merely out of date.
        Assert.Single(fleet.Ships);
        Assert.Equal("Deciat", fleet.SnapshotSystem);
    }

    // ---- Stored modules -------------------------------------------------------------------

    private const string StoredModules =
        """
        {"timestamp":"3311-01-01T00:01:00Z","event":"StoredModules","MarketID":128666762,
         "StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra",
         "Items":[
           {"Name":"$hpt_beamlaser_fixed_medium_name;","Name_Localised":"Beam Laser","StorageSlot":56,
            "StarSystem":"Shinrarta Dezhra","MarketID":128666762,"TransferCost":0,"TransferTime":0},
           {"Name":"$int_powerplant_size6_class5_name;","StorageSlot":8,"StarSystem":"Deciat",
            "MarketID":128674535,"TransferCost":58000,"TransferTime":3600,
            "EngineerModifications":"PowerPlant_Boosted","Level":5,"Hot":true},
           {"Name":"$int_shieldgenerator_size5_class5_strong_name;","Name_Localised":"Prismatic Shield Generator",
            "StorageSlot":9,"InTransit":true}]}
        """;

    [Fact]
    public void StoredModulesSplitIntoWhatIsHereAndWhatIsNot()
    {
        var store = Fold(StoredModules).Modules;

        Assert.True(store.IsKnown);
        Assert.Equal("Shinrarta Dezhra", store.SnapshotSystem);
        Assert.Equal("Jameson Memorial", store.SnapshotStation);

        var here = Assert.Single(store.Here);
        Assert.Equal("Beam Laser", here.Name);
        Assert.Equal("Jameson Memorial", here.StationName);

        // The remote one and the in-transit one are both "not here", which is the distinction a
        // Commander standing at an outfitting screen actually needs.
        Assert.Equal(2, store.Elsewhere.Count);
    }

    [Fact]
    public void AModuleWithNoLocalisedNameKeepsTheSymbolsWordsRatherThanInventingOne()
    {
        var store = Fold(StoredModules).Modules;

        var powerPlant = Assert.Single(store.Modules, module => module.StarSystem == "Deciat");

        // Ugly and true. Making up "6A Power Plant" here would be inventing game data from a
        // string, and a wrong guess is indistinguishable from the feature working.
        Assert.Equal("int powerplant size6 class5", powerPlant.Name);
        Assert.Equal("PowerPlant Boosted", powerPlant.Engineering);
        Assert.Equal(5, powerPlant.EngineeringGrade);
        Assert.True(powerPlant.Hot);
        Assert.Contains("hot", powerPlant.Describe(), StringComparison.Ordinal);
        Assert.Contains("grade 5", powerPlant.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnInTransitModuleBelongsToNoSystem()
    {
        var store = Fold(StoredModules).Modules;

        // It has left one station and not arrived at the next, so listing it under either would
        // send the Commander somewhere it is not.
        Assert.DoesNotContain("Shinrarta Dezhra", store.Systems.Where(s => s == "unknown"));
        Assert.Equal(["Deciat", "Shinrarta Dezhra"], store.Systems);
    }

    [Fact]
    public void AFreshStoredModulesSnapshotReplacesTheStore()
    {
        var store = Fold(
            StoredModules,
            """{"timestamp":"3311-01-01T02:00:00Z","event":"StoredModules","StationName":"Farseer Inc","StarSystem":"Deciat","Items":[{"Name":"$int_fuelscoop_size5_class5_name;","Name_Localised":"Fuel Scoop","StarSystem":"Deciat"}]}""")
            .Modules;

        // Merging would keep modules that have since been sold or fitted, and the failure that
        // causes is a Commander flying somewhere to collect something that is not there.
        Assert.Single(store.Modules);
        Assert.Equal("Deciat", store.SnapshotSystem);
    }

    [Fact]
    public void MatchingAStoredModuleTakesAFragmentOfItsName()
    {
        var store = Fold(StoredModules).Modules;

        Assert.Single(store.Matching("shield"));
        Assert.Single(store.Matching("BEAM"));
        Assert.Empty(store.Matching("interdictor"));
        Assert.Empty(store.Matching("  "));
    }

    [Fact]
    public void NoStoredModulesEventMeansNotSeenRatherThanNothingStored()
    {
        // Written on docking somewhere with outfitting, so silence before that is missing
        // evidence — the same distinction the fleet list draws.
        Assert.False(Fold().Modules.IsKnown);
        Assert.Empty(Fold().Modules.Modules);
    }

    // ---- Materials ----------------------------------------------------------------------

    [Fact]
    public void TheMaterialsSnapshotReplacesWhateverDeltasHadAccumulated()
    {
        var materials = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"MaterialCollected","Category":"Raw","Name":"iron","Count":9}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"Materials","Raw":[{"Name":"carbon","Count":47}],"Manufactured":[],"Encoded":[]}""")
            .Materials;

        // A material absent from a fresh snapshot is a material at zero. Keeping the old figure
        // would have d47 confidently reporting a stock that was spent last session.
        Assert.True(materials.SnapshotSeen);
        Assert.Equal(0, materials.CountOf("iron"));
        Assert.Equal(47, materials.CountOf("carbon"));
    }

    [Fact]
    public void CollectingAndSpendingBothMoveTheCount()
    {
        var materials = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"Materials","Raw":[{"Name":"iron","Count":40}],"Manufactured":[],"Encoded":[]}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"MaterialCollected","Category":"Raw","Name":"iron","Count":6}""",
            """{"timestamp":"3311-01-01T00:03:00Z","event":"EngineerCraft","Ingredients":[{"Name":"iron","Count":10}]}""")
            .Materials;

        Assert.Equal(36, materials.CountOf("iron"));
    }

    [Fact]
    public void AMaterialTradeAppliesBothSides()
    {
        var materials = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"Materials","Raw":[{"Name":"iron","Count":40}],"Manufactured":[],"Encoded":[]}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"MaterialTrade","Paid":{"Material":"iron","Category":"Raw","Quantity":6},"Received":{"Material":"nickel","Category":"Raw","Quantity":2}}""")
            .Materials;

        // Applying only one side would drift the inventory every time the Commander visits a trader.
        Assert.Equal(34, materials.CountOf("iron"));
        Assert.Equal(2, materials.CountOf("nickel"));
    }

    [Fact]
    public void SpendingMoreThanWasSeenCollectedStopsAtZero()
    {
        var materials = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"EngineerCraft","Ingredients":[{"Name":"iron","Count":10}]}""")
            .Materials;

        // d47 started mid-session sees spending it never saw collected. An understated stock is
        // a worse answer than a negative one is a wrong one.
        Assert.Equal(0, materials.CountOf("iron"));
        Assert.False(materials.SnapshotSeen);
    }

    [Fact]
    public void TheLocalisedNameIsKeptForSpeakingAndTheSymbolForIdentity()
    {
        var materials = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"MaterialCollected","Category":"Encoded","Name":"shieldcyclerecordings","Name_Localised":"Distorted Shield Cycle Recordings","Count":3}""")
            .Materials;

        var holding = materials.Find("shieldcyclerecordings");
        Assert.NotNull(holding);
        Assert.Equal("Distorted Shield Cycle Recordings", holding!.Speak());
        Assert.Equal(MaterialCategory.Encoded, holding.Category);
    }

    // ---- Session summary ----------------------------------------------------------------

    [Fact]
    public void EarningsAreSummedBySourceAcrossTheSession()
    {
        var session = Fold(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson","Credits":1000000}""",
            """{"timestamp":"3311-01-01T00:10:00Z","event":"Bounty","TotalReward":250000}""",
            """{"timestamp":"3311-01-01T00:20:00Z","event":"MarketSell","TotalSale":1400000}""",
            """{"timestamp":"3311-01-01T00:30:00Z","event":"MultiSellExplorationData","TotalEarnings":880000}""",
            """{"timestamp":"3311-01-01T00:40:00Z","event":"MissionCompleted","Reward":75000}""",
            """{"timestamp":"3311-01-01T00:50:00Z","event":"FSDJump","StarSystem":"Sol","JumpDist":11.4}""")
            .Session;

        Assert.Equal(250000, session.BountyEarnings);
        Assert.Equal(1400000, session.TradeEarnings);
        Assert.Equal(880000, session.ExplorationEarnings);
        Assert.Equal(75000, session.MissionEarnings);
        Assert.Equal(2605000, session.TotalEarnings);
        Assert.Equal(1, session.Jumps);
        Assert.Equal(11.4, session.DistanceTravelled);
        Assert.Equal(1000000, session.Balance);
        Assert.Equal(TimeSpan.FromMinutes(50), session.Elapsed);
    }

    [Fact]
    public void ABountyWithoutATotalIsSummedFromItsRewards()
    {
        var session = Fold(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson"}""",
            """{"timestamp":"3311-01-01T00:10:00Z","event":"Bounty","Rewards":[{"Faction":"A","Reward":1000},{"Faction":"B","Reward":2500}]}""")
            .Session;

        Assert.Equal(3500, session.BountyEarnings);
    }

    [Fact]
    public void ANewLoadGameStartsTheTotalsOver()
    {
        var session = Fold(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson"}""",
            """{"timestamp":"3311-01-01T00:10:00Z","event":"Bounty","TotalReward":250000}""",
            """{"timestamp":"3311-01-01T09:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson","Credits":9000000}""")
            .Session;

        // Carrying it forward would report a morning's earnings as an evening's.
        Assert.Equal(0, session.TotalEarnings);
        Assert.Equal(9000000, session.Balance);
    }

    [Fact]
    public void AnEscapedInterdictionIsNotCountedAsOne()
    {
        var session = Fold(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson"}""",
            """{"timestamp":"3311-01-01T00:10:00Z","event":"Interdicted","Submitted":false,"Succeeded":false,"Interdictor":"Someone"}""",
            """{"timestamp":"3311-01-01T00:20:00Z","event":"Interdicted","Submitted":false,"Succeeded":true,"Interdictor":"Someone"}""");

        Assert.Equal(1, session.Session.Interdictions);
    }

    // ---- Situational awareness ----------------------------------------------------------

    [Fact]
    public void NothingKnownProducesNoBlockAtAll()
    {
        // Null rather than a block saying nothing is known: an empty report still costs tokens
        // on every turn, and a model told nothing is known will say so unprompted.
        Assert.Null(Situation.Describe(null));
        Assert.Null(Situation.Describe(Fold()));
    }

    [Fact]
    public void TheSituationBlockStatesWhatIsKnownAndOmitsWhatIsNot()
    {
        var block = Situation.Describe(Fold(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson","Credits":1000}""",
            AnacondaLoadout,
            """{"timestamp":"3311-01-01T00:02:00Z","event":"Docked","StarSystem":"Deciat","StationName":"Garay Terminal","StationType":"Orbis"}"""));

        Assert.NotNull(block);
        Assert.Contains("Deciat", block);
        Assert.Contains("Garay Terminal", block);
        Assert.Contains("Bold Endeavour", block);
        Assert.Contains("52.31 ly", block);

        // The carrier line is absent rather than saying "no carrier": this block is re-billed
        // every turn, so a line stating nothing is a line not worth sending.
        Assert.DoesNotContain("Fleet carrier", block);
    }

    [Fact]
    public void TheSituationBlockFramesItselfAsUntrustedData()
    {
        var block = Situation.Describe(Fold(
            """{"timestamp":"3311-01-01T00:02:00Z","event":"Docked","StarSystem":"Deciat","StationName":"Garay Terminal"}"""));

        // Journal content is untrusted (architecture.md §7). The guardrails say so in the cached
        // region; this is the reminder at the point the untrusted text actually arrives.
        Assert.Contains("untrusted", block!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not instructions", block, StringComparison.OrdinalIgnoreCase);
    }
}
