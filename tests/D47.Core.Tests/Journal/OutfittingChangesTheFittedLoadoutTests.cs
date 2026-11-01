using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Outfitting events change the fitted modules without waiting for a <c>Loadout</c>.</summary>
public class OutfittingChangesTheFittedLoadoutTests
{
    private const string Boarded =
        """
        {"timestamp":"2026-08-20T00:51:06Z","event":"Loadout","Ship":"type9_military","ShipID":53,
         "Modules":[
           {"Slot":"Radar","Item":"int_sensors_size4_class2","On":true,"Health":1.0,"Value":50403},
           {"Slot":"MainEngines","Item":"int_engine_size4_class5","On":true,"Health":1.0,"Value":100,
            "Engineering":{"BlueprintName":"Engine_Dirty","Level":3,"Quality":0.5}},
           {"Slot":"Slot01_Size8","Item":"int_shieldgenerator_size8_class5","On":true,"Health":1.0,"Value":5473816}]}
        """;

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static ShipLoadout Ship(params string[] events)
    {
        var loadout = ShipLoadout.Unknown;

        foreach (var json in events)
        {
            loadout = loadout.Apply(Parse(json));
        }

        return loadout;
    }

    private static ShipModule? In(ShipLoadout ship, string slot) =>
        ship.Modules.SingleOrDefault(module => module.Slot == slot);

    [Fact]
    public void ABoughtModuleIsFittedUnengineered()
    {
        var ship = Ship(Boarded,
            """{"event":"ModuleBuy","Slot":"Radar","BuyItem":"int_sensors_size5_class2","BuyItem_Localised":"Sensors","BuyPrice":9000,"ShipID":53}""");

        var radar = In(ship, "Radar")!;

        Assert.Equal("int_sensors_size5_class2", radar.Item);
        Assert.False(radar.IsEngineered);
        Assert.True(radar.Powered);
        Assert.Equal(9000, radar.Value);
        Assert.Equal(3, ship.Modules.Count);
    }

    [Fact]
    public void ABoughtModuleReadsTheSameAsOneFromALoadout()
    {
        var bought = Ship(Boarded,
            """{"event":"ModuleBuy","Slot":"MainEngines","BuyItem":"$int_engine_size4_class5_name;","BuyItem_Localised":"Thrusters","BuyPrice":1,"ShipID":53}""");

        Assert.Equal(In(Ship(Boarded), "MainEngines")!.Item, In(bought, "MainEngines")!.Item);
    }

    [Fact]
    public void ABoughtModuleInAnEmptySlotIsAdded()
    {
        var ship = Ship(Boarded,
            """{"event":"ModuleBuy","Slot":"Slot02_Size4","BuyItem":"int_cargorack_size4_class1","BuyPrice":5,"ShipID":53}""");

        Assert.Equal("int_cargorack_size4_class1", In(ship, "Slot02_Size4")!.Item);
        Assert.Equal(4, ship.Modules.Count);
    }

    [Fact]
    public void ASoldModuleLeavesItsSlotEmpty()
    {
        var ship = Ship(Boarded, """{"event":"ModuleSell","Slot":"Radar","SellItem":"int_sensors_size4_class2","ShipID":53}""");

        Assert.Null(In(ship, "Radar"));
        Assert.Equal(2, ship.Modules.Count);
    }

    [Fact]
    public void AStoredModuleIsReplacedWhereTheEventNamesAReplacement()
    {
        var replaced = Ship(Boarded,
            """{"event":"ModuleStore","Slot":"Radar","StoredItem":"int_sensors_size4_class2","ReplacementItem":"int_sensors_size1_class1","ShipID":53}""");
        var emptied = Ship(Boarded,
            """{"event":"ModuleStore","Slot":"Radar","StoredItem":"int_sensors_size4_class2","ShipID":53}""");

        Assert.Equal("int_sensors_size1_class1", In(replaced, "Radar")!.Item);
        Assert.Null(In(emptied, "Radar"));
    }

    [Fact]
    public void ARetrievedModuleKeepsItsEngineering()
    {
        var ship = Ship(Boarded,
            """{"event":"ModuleRetrieve","Slot":"Radar","RetrievedItem":"int_sensors_size4_class2","EngineerModifications":"Sensor_LightWeight","Level":5,"Quality":0.9,"ShipID":53}""");

        var radar = In(ship, "Radar")!;

        Assert.Equal("Sensor_LightWeight", radar.Blueprint);
        Assert.Equal(5, radar.BlueprintLevel);
        Assert.Equal(0.9, radar.Quality);
    }

    [Fact]
    public void ARetrievedPlainModuleIsNotEngineered()
    {
        var ship = Ship(Boarded,
            """{"event":"ModuleRetrieve","Slot":"Radar","RetrievedItem":"int_sensors_size4_class2","ShipID":53}""");

        Assert.False(In(ship, "Radar")!.IsEngineered);
    }

    [Fact]
    public void ASwapMovesTheModulesWithTheirEngineering()
    {
        var ship = Ship(Boarded,
            """{"event":"ModuleSwap","FromSlot":"Radar","ToSlot":"MainEngines","FromItem":"int_sensors_size4_class2","ToItem":"int_engine_size4_class5","ShipID":53}""");

        Assert.Equal("int_sensors_size4_class2", In(ship, "MainEngines")!.Item);
        Assert.Equal("int_engine_size4_class5", In(ship, "Radar")!.Item);
        Assert.Equal("Engine_Dirty", In(ship, "Radar")!.Blueprint);
    }

    [Fact]
    public void ASwapIntoAnEmptySlotLeavesTheSourceEmpty()
    {
        var ship = Ship(Boarded,
            """{"event":"ModuleSwap","FromSlot":"Radar","ToSlot":"Slot02_Size4","FromItem":"int_sensors_size4_class2","ToItem":"Null","ShipID":53}""");

        Assert.Null(In(ship, "Radar"));
        Assert.Equal("int_sensors_size4_class2", In(ship, "Slot02_Size4")!.Item);
    }

    [Fact]
    public void AMassStoreEmptiesEveryListedSlot()
    {
        var ship = Ship(Boarded,
            """{"event":"MassModuleStore","ShipID":53,"Items":[{"Slot":"Radar","Name":"int_sensors_size4_class2"},{"Slot":"Slot01_Size8","Name":"int_shieldgenerator_size8_class5"}]}""");

        Assert.Equal(["MainEngines"], ship.Modules.Select(module => module.Slot));
    }

    [Fact]
    public void ABoughtModuleMeetsAPlanForItsSymbol()
    {
        var ship = Ship(Boarded,
            """{"event":"ModuleBuy","Slot":"Slot02_Size4","BuyItem":"$int_engine_size4_class5_name;","BuyItem_Localised":"Thrusters","BuyPrice":1,"ShipID":53}""");
        var plan = new D47.Core.Ships.SlotPlan("Slot02_Size4") { Variant = "int_engine_size4_class5" };

        Assert.False(D47.Core.Ships.ShipPlanService.Outstanding(plan, In(ship, "Slot02_Size4")));
    }

    [Theory]
    [InlineData("""{"event":"ModuleSell","Slot":"Radar","SellItem":"x","ShipID":99}""")]
    [InlineData("""{"event":"ModuleBuy","Slot":"Radar","BuyItem":"x","ShipID":99}""")]
    [InlineData("""{"event":"ModuleSwap","FromSlot":"Radar","ToSlot":"MainEngines","ShipID":99}""")]
    [InlineData("""{"event":"MassModuleStore","ShipID":99,"Items":[{"Slot":"Radar"}]}""")]
    [InlineData("""{"event":"ModuleSell","Slot":"Radar","SellItem":"x"}""")]
    public void AnotherShipsOutfittingLeavesTheLoadoutAlone(string json)
    {
        var before = Ship(Boarded);

        Assert.Same(before, before.Apply(Parse(json)));
    }

    [Theory]
    [InlineData("ModuleBuy")]
    [InlineData("ModuleSell")]
    [InlineData("ModuleStore")]
    [InlineData("ModuleRetrieve")]
    [InlineData("ModuleSwap")]
    [InlineData("MassModuleStore")]
    public void TheLoadoutFileIsWrittenAfterEachOutfittingEvent(string kind) =>
        Assert.True(ShipLoadouts.MayChange(Parse($$"""{"event":"{{kind}}"}""")));
}
