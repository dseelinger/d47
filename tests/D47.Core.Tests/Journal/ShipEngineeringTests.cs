using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>The <c>Engineering</c> block, all seven fields of it.</summary>
public class ShipEngineeringTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static ShipModule Module(string moduleJson)
    {
        var loadout = ShipLoadout.Unknown.Apply(Event(
            $$"""
            {"timestamp":"3311-01-01T00:01:00Z","event":"Loadout","Ship":"Krait_MkII","ShipID":4,
             "Modules":[{{moduleJson}}]}
            """));

        return Assert.Single(loadout.Modules);
    }

    /// <summary>The commonest shape — 513 of 772.</summary>
    private const string FullyEngineeredDrive =
        """
        {"Slot":"FrameShiftDrive","Item":"int_hyperdrive_size5_class5","On":true,"Health":1.0,"Value":1610350,
         "Engineering":{"BlueprintID":128673694,"BlueprintName":"FSD_LongRange","Level":5,"Quality":1.0,
           "Engineer":"Felicity Farseer","EngineerID":300100,
           "ExperimentalEffect":"special_fsd_heavy","ExperimentalEffect_Localised":"Mass Manager",
           "Modifiers":[
             {"Label":"Mass","Value":26.0,"OriginalValue":20.0,"LessIsGood":1},
             {"Label":"Integrity","Value":94.5,"OriginalValue":105.0,"LessIsGood":0},
             {"Label":"FSDOptimalMass","Value":1692.75,"OriginalValue":1050.0,"LessIsGood":0}
           ]}}
        """;

    [Fact]
    public void TheRollIsReadInRealUnitsRatherThanThrownAway()
    {
        var module = Module(FullyEngineeredDrive);

        // This is the whole point of the step. "How good is my roll" needs no table at all — the module
        // carries its own before and after.
        Assert.Equal("FSD_LongRange", module.Blueprint);
        Assert.Equal(5, module.BlueprintLevel);
        Assert.Equal(1.0, module.Quality);
        Assert.Equal("Felicity Farseer", module.Engineer);
        Assert.Equal(300100, module.EngineerId);
        Assert.Equal("Mass Manager", module.Experimental);
        Assert.Equal(3, module.Modifiers.Count);

        var optimalMass = module.Modifiers.Single(m => m.Label == "FSDOptimalMass");

        Assert.Equal(1692.75, optimalMass.Value);
        Assert.Equal(1050.0, optimalMass.OriginalValue);
        Assert.Equal(642.75, optimalMass.Change!.Value, 6);
        Assert.True(optimalMass.IsImprovement);
    }

    [Fact]
    public void LessIsGoodArrivesAsZeroOrOneAndNotAsABoolean()
    {
        // Read as a JSON boolean this answers false every time, and every improvement on a less-is-better
        // figure gets reported backwards.
        var module = Module(FullyEngineeredDrive);

        var mass = module.Modifiers.Single(m => m.Label == "Mass");
        var integrity = module.Modifiers.Single(m => m.Label == "Integrity");

        Assert.True(mass.LessIsGood);
        Assert.False(integrity.LessIsGood);
        Assert.False(mass.IsImprovement);
        Assert.False(integrity.IsImprovement);
    }

    [Fact]
    public void AModifierThatIsNotANumberSurvivesAsText()
    {
        // 16 of 3,384 modifiers measured carry ValueStr and no Value, OriginalValue or LessIsGood — a damage
        // type rather than a quantity.
        var module = Module(
            """
            {"Slot":"MediumHardpoint1","Item":"hpt_multicannon_gimbal_medium","On":true,
             "Engineering":{"BlueprintName":"Weapon_Overcharged","Level":5,"Quality":1.0,
               "Engineer":"Tod McQuinn","EngineerID":300260,
               "Modifiers":[{"Label":"DamageType","ValueStr":"$Thermic;","ValueStr_Localised":"Thermal"}]}}
            """);

        var damageType = Assert.Single(module.Modifiers);

        Assert.Equal("DamageType", damageType.Label);
        Assert.Equal("Thermal", damageType.Text);
        Assert.Null(damageType.Value);
        Assert.Null(damageType.Change);
        Assert.Null(damageType.IsImprovement);
    }

    [Fact]
    public void AValueArrivingAsAStringIsKeptRatherThanDroppedForBeingTheWrongType()
    {
        // Not seen in the corpus, and cheap to survive: the strict Double() helper answers null for a string,
        // so without a fallback the modifier reads as present and empty.
        var module = Module(
            """
            {"Slot":"TinyHardpoint1","Item":"hpt_heatsinklauncher_turret_tiny","On":true,
             "Engineering":{"BlueprintName":"Misc_HeatSinkCapacity","Level":1,
               "Modifiers":[{"Label":"AmmoClipSize","Value":"3"}]}}
            """);

        var modifier = Assert.Single(module.Modifiers);

        Assert.Equal("3", modifier.Text);
        Assert.Null(modifier.Value);
    }

    [Fact]
    public void AModuleWithNoExperimentalIsNotGivenOne()
    {
        // 232 of 772 — the second commonest shape, and the one where a missing key is the answer.
        var module = Module(
            """
            {"Slot":"PowerPlant","Item":"int_powerplant_size7_class5","On":true,
             "Engineering":{"BlueprintName":"PowerPlant_Armoured","Level":5,"Quality":0.85,
               "Engineer":"Selene Jean","EngineerID":300210,
               "Modifiers":[{"Label":"Integrity","Value":180.0,"OriginalValue":120.0,"LessIsGood":0}]}}
            """);

        Assert.Null(module.Experimental);
        Assert.Equal("Selene Jean", module.Engineer);

        // 0.85 is finished, and this is a real Quality value out of the corpus rather than a contrived one.
        Assert.Equal(0, EngineeringRules.RollsRemaining(module.BlueprintLevel!.Value, 5, module.Quality!.Value));
    }

    [Fact]
    public void AnExperimentalPresentButEmptyMeansThereIsNone()
    {
        // Zero occurrences in the corpus — carried on EDDiscovery's authority.
        var module = Module(
            """
            {"Slot":"PowerDistributor","Item":"int_powerdistributor_size6_class5","On":true,
             "Engineering":{"BlueprintName":"PowerDistributor_HighFrequency","Level":4,
               "ExperimentalEffect":"","Modifiers":[]}}
            """);

        Assert.Null(module.Experimental);
        Assert.True(module.IsEngineered);
    }

    [Fact]
    public void TheOtherSpellingOfTheExperimentalFieldIsAlsoRead()
    {
        // Loadout writes ExperimentalEffect; EngineerCraft writes ApplyExperimentalEffect for the same thing,
        // and appears 287 times in the corpus.
        var module = Module(
            """
            {"Slot":"Armour","Item":"krait_mkii_armour_grade3","On":true,
             "Engineering":{"BlueprintName":"Armour_HeavyDuty","Level":5,
               "ApplyExperimentalEffect":"special_armour_chunky",
               "ApplyExperimentalEffect_Localised":"Deep Plating","Modifiers":[]}}
            """);

        Assert.Equal("Deep Plating", module.Experimental);
    }

    [Fact]
    public void ThePreThreePointZeroBlueprintSpellingStillReads()
    {
        // Zero occurrences in the corpus, which begins in July 2025 — seven years after the spelling changed.
        var module = Module(
            """
            {"Slot":"FrameShiftDrive","Item":"int_hyperdrive_size5_class5","On":true,
             "Engineering":{"Blueprint":"FSD_LongRange","Level":3}}
            """);

        Assert.Equal("FSD_LongRange", module.Blueprint);
        Assert.True(module.IsEngineered);
    }

    [Fact]
    public void AModuleThatArrivedAlreadyEngineeredHasAnIdAndNoEngineer()
    {
        // 27 of 772, every one of them engineer id 399999 on a grade 5 cargo rack.
        var module = Module(
            """
            {"Slot":"Slot02_Size6","Item":"int_cargorack_size6_class1","On":true,
             "Engineering":{"BlueprintID":128731581,"BlueprintName":"CargoRack_IncreasedCapacity",
               "Level":5,"Quality":1.0,"EngineerID":399999,
               "Modifiers":[{"Label":"CargoCapacity","Value":128.0,"OriginalValue":64.0,"LessIsGood":0}]}}
            """);

        Assert.Null(module.Engineer);
        Assert.Equal(399999, module.EngineerId);
        Assert.True(module.IsEngineered);
        Assert.True(module.Modifiers.Single().IsImprovement);
    }

    [Fact]
    public void AnUnmodifiedModuleCarriesNothingRatherThanEmptyEngineering()
    {
        var module = Module(
            """{"Slot":"Slot05_Size4","Item":"int_cargorack_size4_class1","On":false,"Health":1.0,"Value":34330}""");

        Assert.False(module.IsEngineered);
        Assert.Null(module.Quality);
        Assert.Null(module.Engineer);
        Assert.Null(module.EngineerId);
        Assert.Empty(module.Modifiers);
    }

    [Fact]
    public void AModifierThatDidNotMoveIsNeitherBetterNorWorse()
    {
        // Null rather than false. "Unchanged" and "worse" are different things to say out loud.
        var module = Module(
            """
            {"Slot":"PowerPlant","Item":"int_powerplant_size7_class5","On":true,
             "Engineering":{"BlueprintName":"PowerPlant_Armoured","Level":1,
               "Modifiers":[{"Label":"PowerCapacity","Value":24.0,"OriginalValue":24.0,"LessIsGood":0}]}}
            """);

        var modifier = Assert.Single(module.Modifiers);

        Assert.Equal(0.0, modifier.Change);
        Assert.Null(modifier.IsImprovement);
    }
}
