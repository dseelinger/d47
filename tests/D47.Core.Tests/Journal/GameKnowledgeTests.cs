using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Phase 7, folded event by event.</summary>
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

        // LeaveBody names the body being left, not a new one.
        Assert.Null(state.Location.Body);
    }

    [Fact]
    public void OnlyAHyperspaceStartJumpEntersHyperspace()
    {
        var supercruise = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"StartJump","JumpType":"Supercruise"}""");

        // JumpType is "Supercruise" for the far more common case.
        Assert.NotEqual(FlightMode.Hyperspace, supercruise.Location.Mode);

        var hyperspace = Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Wolf 359","StarClass":"M"}""");

        Assert.Equal(FlightMode.Hyperspace, hyperspace.Location.Mode);
        Assert.Equal("Wolf 359", hyperspace.Location.JumpDestination);
        Assert.Equal("M", hyperspace.Location.JumpDestinationStarClass);
    }

    /// <summary>
    /// The ordering the game actually writes on a plotted route (#152): the FSDTarget for the hop after
    /// this one lands about seven seconds into the tunnel, before the arrival.
    /// </summary>
    [Fact]
    public void ArrivingConsumesTheDestinationOfTheJumpJustFlown()
    {
        var state = Fold(
            """{"timestamp":"3311-01-01T00:02:00Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Wolf 359","StarClass":"M"}""",
            """{"timestamp":"3311-01-01T00:02:07Z","event":"FSDTarget","Name":"Alpha Centauri","StarClass":"G","RemainingJumpsInRoute":3}""",
            """{"timestamp":"3311-01-01T00:03:00Z","event":"FSDJump","StarSystem":"Wolf 359","JumpDist":7.8,"FuelLevel":28.5}""");

        Assert.Equal("Wolf 359", state.Location.StarSystem);
        Assert.Equal(FlightMode.Supercruise, state.Location.Mode);

        // Left set, Phase 8 would warn about a star the Commander is now sitting next to.
        Assert.Null(state.Location.JumpDestination);
        Assert.Equal(28.5, state.Location.FuelMain);

        // The class of the system arrived at, not of the one targeted mid-tunnel.
        Assert.Equal("M", state.Location.StarClass);
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

        // Every one of these is a figure Loadout reported.
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

        // Renaming does not re-emit Loadout, so without this the ship keeps answering to the name it had when
        // it was last outfitted.
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

        // Elite writes an empty string for a ship that has never been named; null is what the rest of the
        // type means by "not named", and the two must not both be in circulation.
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
        // Carrier events only reach an owner's journal, so this state is genuinely ambiguous and the
        // capability says so rather than asserting the Commander owns nothing.
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

        // ShipsHere carries no StarSystem of its own.
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

        // Merging would accumulate ships that have since been sold, and a fleet list containing a ship the
        // Commander no longer owns is worse than one that is merely out of date.
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

        // The remote one and the in-transit one are both "not here", which is the distinction a Commander
        // standing at an outfitting screen actually needs.
        Assert.Equal(2, store.Elsewhere.Count);
    }

    [Fact]
    public void AModuleWithNoLocalisedNameKeepsTheSymbolsWordsRatherThanInventingOne()
    {
        var store = Fold(StoredModules).Modules;

        var powerPlant = Assert.Single(store.Modules, module => module.StarSystem == "Deciat");

        // Ugly and true.
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

        // It has left one station and not arrived at the next, so listing it under either would send the
        // Commander somewhere it is not.
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

        // Merging would keep modules that have since been sold or fitted, and the failure that causes is a
        // Commander flying somewhere to collect something that is not there.
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
        // Written on docking somewhere with outfitting, so silence before that is missing evidence — the same
        // distinction the fleet list draws.
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

        // A material absent from a fresh snapshot is a material at zero.
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

        // d47 started mid-session sees spending it never saw collected.
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
        // Null rather than a block saying nothing is known: an empty report still costs tokens on every turn,
        // and a model told nothing is known will say so unprompted.
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

        // No Cargo.json was ever read here, so the hold is unknown: the line falls back to stating capacity
        // alone rather than claiming a fill figure it does not have.
        Assert.Contains("cargo capacity 64 t", block);

        // The carrier line is absent rather than saying "no carrier": this block is re-billed every turn, so
        // a line stating nothing is a line not worth sending.
        Assert.DoesNotContain("Fleet carrier", block);
    }

    [Fact]
    public void TheSituationBlocksCarrierLineIsDated()
    {
        // #406: the location is remembered across sessions now, so an undated present-tense line in the block
        // the model reads would assert a system last seen weeks ago as current.
        var block = Situation.Describe(Fold(
            """{"timestamp":"3311-01-01T00:01:00Z","event":"CarrierStats","CarrierID":1,"Callsign":"K7Q-BQL","Name":"Bootstrap"}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"CarrierLocation","CarrierID":1,"Callsign":"K7Q-BQL","StarSystem":"Meene"}"""));

        Assert.NotNull(block);
        Assert.Contains("Fleet carrier: Bootstrap (K7Q-BQL) in Meene, as of 3311-01-01 00:02 UTC", block);
    }

    [Fact]
    public void TheSituationBlockSaysOutrightThatTheShipIsNotDocked()
    {
        // #364: undocked from Zeppelin Depot, and a minute later d47 said the ship was docked there.
        var block = Situation.Describe(Fold(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson","Credits":1000}""",
            """{"timestamp":"3311-01-01T00:02:00Z","event":"Docked","StarSystem":"Alrai Sector KC-U b3-4","StationName":"Zeppelin Depot","StationType":"Outpost"}""",
            """{"timestamp":"3311-01-01T00:08:00Z","event":"Undocked","StationName":"Zeppelin Depot"}"""));

        Assert.NotNull(block);
        Assert.Contains("Status: in normal space, not docked", block);
        Assert.DoesNotContain("Zeppelin Depot", block);

        // And the block says which text wins where the two disagree, since the negative alone does not settle
        // that.
        Assert.Contains("earlier in this conversation", block);
    }

    [Fact]
    public void NeitherClaimIsMadeBeforeTheGameHasSaidWhereTheCommanderIs()
    {
 // Silence still takes positive evidence.
        var block = Situation.Describe(Fold(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson","Credits":1000}""",
            AnacondaLoadout));

        Assert.NotNull(block);
        Assert.DoesNotContain("docked", block, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheDockedCaseStillReadsExactlyAsItDid()
    {
        var block = Situation.Describe(Fold(
            """{"timestamp":"3311-01-01T00:02:00Z","event":"Docked","StarSystem":"Deciat","StationName":"Garay Terminal","StationType":"Orbis"}"""));

        Assert.Contains("docked at Garay Terminal (Orbis)", block);
        Assert.Contains("Status: docked", block);
        Assert.DoesNotContain("not docked", block!);
    }

    /// <summary>A Commander in an Anaconda (64t hold) carrying the given items, built through <see cref="JournalSpine"/> with a real <c>Cargo.json</c> on disk, because the hold cannot arrive any other way.</summary>
    private static CommanderGameState CommanderWithCargo(int count, params (string Symbol, string Name, int Tonnes)[] items)
    {
        var install = new TempInstall();

        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-09-05T000000.01.log"),
            [
                """{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                """{"timestamp":"3311-01-01T00:01:00Z","event":"Loadout","Ship":"Anaconda","ShipID":7,"ShipName":"Bold Endeavour","CargoCapacity":64,"Modules":[]}""",
            ]);

        var inventory = string.Join(",", items.Select(item =>
            $$"""{ "Name":"{{item.Symbol}}", "Name_Localised":"{{item.Name}}", "Count":{{item.Tonnes}}, "Stolen":0 }"""));

        File.WriteAllText(
            Path.Combine(install.Root, CargoManifestReader.ManifestFile),
            $$"""{ "timestamp":"3311-01-01T00:01:00Z", "event":"Cargo", "Vessel":"Ship", "Count":{{count}}, "Inventory":[ {{inventory}} ] }""");

        var store = new GameStateStore();
        new JournalSpine(install.Root, store, NullLoggerFactory.Instance).Poll();

        return store.Active!;
    }

    [Fact]
    public void TheSituationBlockStatesActualCargoFillNotJustCapacity()
    {
        // #329: capacity alone lets the persona guess at fullness.
        var state = CommanderWithCargo(40, ("gold", "Gold", 25), ("silver", "Silver", 15));

        var block = Situation.Describe(state);

        Assert.Contains("cargo 40/64 t", block);
        Assert.Contains("Cargo hold: Gold 25t, Silver 15t", block);
    }

    [Fact]
    public void TheCargoItemsSummaryStaysShortWhenTheHoldHasManyLines()
    {
        // Item names are the Commander's own journal text (untrusted) and are re-billed every turn, so the
        // summary names only the heaviest few and folds the rest into a count.
        var state = CommanderWithCargo(
            60,
            ("gold", "Gold", 20),
            ("silver", "Silver", 15),
            ("palladium", "Palladium", 10),
            ("platinum", "Platinum", 8),
            ("painite", "Painite", 7));

        var block = Situation.Describe(state);

        Assert.Contains("Cargo hold: Gold 20t, Silver 15t, Palladium 10t, +2 more", block);
        Assert.DoesNotContain("Platinum", block);
    }

    [Fact]
    public void AnEmptyKnownHoldAddsNoCargoItemsLine()
    {
        var state = CommanderWithCargo(0);

        var block = Situation.Describe(state);

        Assert.Contains("cargo 0/64 t", block);
        Assert.DoesNotContain("Cargo hold:", block!);
    }

 // ---- The credit balance ------------------------------------------------------

    /// <summary>The clock the block is described against, so the reads below have an age.</summary>
    private static readonly DateTimeOffset Now = new(3311, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// In the main ship, with the given balance, read just now — the shape Status.json has in flight,
    /// where Elite rewrites it every few seconds.
    /// </summary>
    private static GameStatus Flying(long? balance) => new()
    {
        Flags = StatusFlags.InMainShip,
        Balance = balance,
        ReadAt = Now - TimeSpan.FromSeconds(4),
    };

    /// <summary>A real <c>Status.json</c>, read the way the app reads it.</summary>
    private static GameStatus ReadStatus(string json)
    {
        var install = new TempInstall();
        File.WriteAllText(Path.Combine(install.Root, GameStatusReader.FileName), json);

        var reader = new GameStatusReader(install.Root, NullLogger.Instance);
        Assert.True(reader.Poll());

        return reader.Current;
    }

    private static readonly string[] Grounded =
    [
        """{"timestamp":"3311-01-01T00:02:00Z","event":"Docked","StarSystem":"Deciat","StationName":"Garay Terminal"}""",
    ];

    [Fact]
    public void TheSituationBlockStatesTheSpendableBalance()
    {
        // #360: d47 has parsed Balance every tick since Phase 7 and never shown it to the model, so asked for
        // the balance it answered from session earnings and denied knowing.
        var block = Situation.Describe(Fold(Grounded), Flying(8_862_291_458), Now);

        Assert.Contains("Credit balance: 8,862,291,458 cr", block);

 // Both forms in the one line: banded for speech, exact for a "how much exactly".
        Assert.Contains("say \"8.9 billion\"", block);
        Assert.Contains("exact only if asked", block);
    }

    [Theory]
    [InlineData(48_295L, "48,295")]
    [InlineData(1_000_000L, "1 million")]
    [InlineData(2_129_966_400L, "2.1 billion")]
    public void TheSpokenBalanceIsBanded(long balance, string said)
    {
        var block = Situation.Describe(Fold(Grounded), Flying(balance), Now);

        Assert.Contains($"say \"{said}\"", block);
    }

    [Fact]
    public void AStatusFileWithNoBalanceKeyLeavesTheLineOutRatherThanSayingNought()
    {
        var status = ReadStatus(
            """{ "timestamp":"2026-09-06T21:11:02Z", "event":"Status", "Flags":16777224, "Flags2":0 }""");

        Assert.Null(status.Balance);

        Assert.DoesNotContain(
            "Credit balance", Situation.Describe(Fold(Grounded), status, DateTimeOffset.UtcNow)!);
    }

    [Fact]
    public void WithTheGameNotRunningTheBalanceIsNotClaimedAtAll()
    {
        // Never read at all, and no read offered.
        Assert.DoesNotContain(
            "Credit balance", Situation.Describe(Fold(Grounded), GameStatus.Unknown, Now)!);
        Assert.DoesNotContain("Credit balance", Situation.Describe(Fold(Grounded))!);

        // At the main menu the file is fresh and carries a balance, and the Commander is still not in the
        // game.
        var atMenu = new GameStatus { Balance = 8_862_291_458, ReadAt = Now };

        Assert.False(atMenu.CommanderIsInTheGame);
        Assert.DoesNotContain("Credit balance", Situation.Describe(Fold(Grounded), atMenu, Now)!);
    }

    [Fact]
    public void AStatusFileTheGameStoppedWritingIsNotQuotedAsTheBalanceNow()
    {
        // Elite force-quit from the cockpit leaves Status.json saying InMainShip with last night's balance in
        // it, and nothing ever clears the parsed copy — so CommanderIsInTheGame stays true forever and the
        // age of the read is what settles it.
        var lastNight = Flying(8_862_291_458) with { ReadAt = Now - TimeSpan.FromHours(9) };

        Assert.True(lastNight.CommanderIsInTheGame);
        Assert.DoesNotContain("Credit balance", Situation.Describe(Fold(Grounded), lastNight, Now)!);

        // And with no clock to age it against, the line is left out rather than stated unaged.
        Assert.DoesNotContain("Credit balance", Situation.Describe(Fold(Grounded), Flying(8_862_291_458))!);
    }

    /// <summary>A Commander in the ship with the given flags set as well.</summary>
    private static GameStatus Ship(StatusFlags extra) => new()
    {
        Flags = StatusFlags.InMainShip | extra,
        ReadAt = Now - TimeSpan.FromSeconds(4),
    };

    [Fact]
    public void TheSituationBlockSaysWhenTheDriveIsMassLocked()
    {
        // Asked to jump beside a station, d47 pressed the key, the press succeeded, the ship did not move,
        // and it reported the jump as done — then said it could not see mass lock at all.
        var block = Situation.Describe(Fold(Grounded), Ship(StatusFlags.FsdMassLocked), Now);

        Assert.Contains("Drive: mass locked", block);

        // The way out, named: boost already works in normal space and is already in control_flight's reach,
        // so the model is told to use it rather than left to work it out.
        Assert.Contains("boost", block);
        Assert.Contains("get clear and jump", block);

        // And the lie the whole line exists to stop.
        Assert.Contains("Do not report a jump as done", block);
    }

    [Fact]
    public void TheDriveLineIsSilentWhenThereIsNothingToSay()
    {
        // Re-billed every turn, so three quiet states cost nothing on the vast majority of them.
        Assert.DoesNotContain("Drive:", Situation.Describe(Fold(Grounded), Ship(StatusFlags.None), Now)!);
    }

    [Fact]
    public void TheDriveLineNamesCoolingDownAndCharging()
    {
        Assert.Contains(
            "Drive: cooling down",
            Situation.Describe(Fold(Grounded), Ship(StatusFlags.FsdCooldown), Now));

        Assert.Contains(
            "Drive: charging",
            Situation.Describe(Fold(Grounded), Ship(StatusFlags.FsdCharging), Now));
    }

    [Fact]
    public void MassLockOutranksTheOtherTwoDriveStates()
    {
        // Elite sets charging alongside the lock while the drive spools against it.
        var block = Situation.Describe(
            Fold(Grounded),
            Ship(StatusFlags.FsdMassLocked | StatusFlags.FsdCharging),
            Now);

        Assert.Contains("Drive: mass locked", block);
        Assert.DoesNotContain("Drive: charging", block!);
    }

    [Fact]
    public void ADriveStateIsNotClaimedFromAReadThatIsNotLive()
    {
        // The same gate as the balance: a file the game stopped writing must not report a lock that cleared
        // hours ago, and the main menu is not the game.
        var lastNight = Ship(StatusFlags.FsdMassLocked) with { ReadAt = Now - TimeSpan.FromHours(9) };

        Assert.DoesNotContain("Drive:", Situation.Describe(Fold(Grounded), lastNight, Now)!);
        Assert.DoesNotContain("Drive:", Situation.Describe(Fold(Grounded), GameStatus.Unknown, Now)!);
        Assert.DoesNotContain("Drive:", Situation.Describe(Fold(Grounded))!);

        // And with no clock to age the read against, nothing is stated.
        Assert.DoesNotContain(
            "Drive:", Situation.Describe(Fold(Grounded), Ship(StatusFlags.FsdMassLocked))!);
    }

    [Fact]
    public void TheBlockAsksForTheCorrectionToBeSilent()
    {
        // #364 gave the block the last word over the history, and the first thing it did with it was announce
        // the win: "Confirmed, not docked.
        var block = Situation.Describe(Fold(Grounded), Ship(StatusFlags.None), Now);

        Assert.Contains("correct yourself silently", block);
        Assert.Contains("never announcing the correction", block);
    }

    [Fact]
    public void TheCreditsSpentLineIsUnchangedByTheLiveBalance()
    {
        // TelemetryDelta reads SessionSummary.Balance — the session's opening figure from LoadGame — and the
        // live figure does not supersede it.
        var was = Fold(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson","Credits":10000000}""")
            .Session;

        var after = Fold(
            """{"timestamp":"3311-01-01T00:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson","Credits":10000000}""",
            """{"timestamp":"3311-01-01T02:00:00Z","event":"LoadGame","FID":"F1","Commander":"Jameson","Credits":7500000}""")
            .Session;

        var delta = TelemetryDelta.Between(was, after, Fold(Grounded));

        Assert.Contains("2,500,000 credits spent.", delta);

        // And the catch-up block is what it was: it describes a world d47 was not watching, where the status
        // read may be stale or the game shut.
        Assert.DoesNotContain("Credit balance", delta!);
    }

    [Fact]
    public void TheSituationBlockFramesItselfAsUntrustedData()
    {
        var block = Situation.Describe(Fold(
            """{"timestamp":"3311-01-01T00:02:00Z","event":"Docked","StarSystem":"Deciat","StationName":"Garay Terminal"}"""));

 // Journal content is untrusted.
        Assert.Contains("untrusted", block!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not instructions", block, StringComparison.OrdinalIgnoreCase);
    }
}
