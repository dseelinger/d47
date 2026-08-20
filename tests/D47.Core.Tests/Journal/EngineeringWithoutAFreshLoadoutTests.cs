using D47.Core.Checklists;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// A roll reaches the ship without waiting for a <c>Loadout</c>.
/// <para>
/// <b>Elite does not write a <c>Loadout</c> when a module is engineered.</b> Measured over the
/// local 917-journal corpus: of 6,485 <c>EngineerCraft</c> events, <b>not one</b> is followed by a
/// <c>Loadout</c> within five seconds, only 79 within a minute, and 1,378 are never followed by
/// one at all in the same file. <see cref="ShipLoadout.Apply"/> said <c>Loadout</c> "fires on
/// every change that matters — outfitting, module swap, ship swap"; that list is right and
/// engineering is not on it.
/// </para>
/// <para>
/// So without this fold a plan item is diffed against the loadout from when the Commander last
/// boarded, reads its module as unengineered, and stays open straight through the roll that
/// finished it. Then they swap ships, <c>IsActive</c> answers null, and the stale verdict stands
/// until they board that hull again. Reported from Commander JOHN DEPARAGON's Type-10 on
/// 2026-08-20: grade 5 Light Weight Scanner rolled at Lei Cheung at 01:38:52Z, no <c>Loadout</c>
/// after it, ship swapped away at 01:47:14Z, item still open.
/// </para>
/// <para>
/// Every event below is verbatim from that journal, minus the ingredient lists.
/// </para>
/// </summary>
public class EngineeringWithoutAFreshLoadoutTests
{
    /// <summary>The Type-10 as its last Loadout described it — nothing on it engineered.</summary>
    private const string Boarded =
        """
        {"timestamp":"2026-08-20T00:51:06Z","event":"Loadout","Ship":"type9_military","ShipID":53,
         "Modules":[
           {"Slot":"Radar","Item":"int_sensors_size4_class2","On":true,"Health":1.0,"Value":50403},
           {"Slot":"Slot01_Size8","Item":"int_shieldgenerator_size8_class5","On":true,"Health":1.0,"Value":5473816},
           {"Slot":"TinyHardpoint2","Item":"hpt_shieldbooster_size0_class5","On":true,"Health":1.0,"Value":281000}]}
        """;

    /// <summary>The last roll of the sensors — grade 5, quality 1.0, and no experimental at all.</summary>
    private const string RolledTheSensors =
        """
        {"timestamp":"2026-08-20T01:38:52Z","event":"EngineerCraft","Slot":"Radar",
         "Module":"int_sensors_size4_class2","Engineer":"Lei Cheung","EngineerID":300120,
         "BlueprintID":128740673,"BlueprintName":"Sensor_LightWeight","Level":5,"Quality":1.0,
         "Modifiers":[
           {"Label":"Mass","Value":0.8,"OriginalValue":4.0,"LessIsGood":1},
           {"Label":"Integrity","Value":32.0,"OriginalValue":64.0,"LessIsGood":0},
           {"Label":"SensorTargetScanAngle","Value":22.5,"OriginalValue":30.0,"LessIsGood":0}]}
        """;

    /// <summary>
    /// Applying an experimental. It carries the blueprint underneath it as well, and the effect
    /// under <em>both</em> spellings — <c>ApplyExperimentalEffect</c> as the symbol and
    /// <c>ExperimentalEffect</c> localised beside it.
    /// </summary>
    private const string AppliedFastCharge =
        """
        {"timestamp":"2026-08-20T01:37:22Z","event":"EngineerCraft","Slot":"Slot01_Size8",
         "Module":"int_shieldgenerator_size8_class5","ApplyExperimentalEffect":"special_shield_regenerative",
         "Engineer":"Lei Cheung","EngineerID":300120,"BlueprintID":128673839,
         "BlueprintName":"ShieldGenerator_Reinforced","Level":5,"Quality":1.0,
         "ExperimentalEffect":"special_shield_regenerative","ExperimentalEffect_Localised":"Fast Charge",
         "Modifiers":[
           {"Label":"ShieldGenStrength","Value":165.600006,"OriginalValue":120.000008,"LessIsGood":0},
           {"Label":"RegenRate","Value":2.76,"OriginalValue":2.4,"LessIsGood":0}]}
        """;

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static ShipLoadout Ship(params string[] events)
    {
        var loadout = ShipLoadout.Unknown;

        foreach (var json in events)
        {
            loadout = loadout.Apply(Event(json));
        }

        return loadout;
    }

    private static ShipModule Slot(ShipLoadout loadout, string slot) =>
        Assert.Single(loadout.Modules, module => module.Slot == slot);

    [Fact]
    public void ARollReachesTheModuleWithoutWaitingForALoadout()
    {
        var radar = Slot(Ship(Boarded, RolledTheSensors), "Radar");

        Assert.Equal("Sensor_LightWeight", radar.Blueprint);
        Assert.Equal(5, radar.BlueprintLevel);
        Assert.Equal(1.0, radar.Quality);
        Assert.Equal("Lei Cheung", radar.Engineer);
        Assert.Equal(300120, radar.EngineerId);

        // The roll in real units, which is the half no table can supply.
        var mass = radar.Modifiers.Single(modifier => modifier.Label == "Mass");

        Assert.Equal(0.8, mass.Value);
        Assert.Equal(4.0, mass.OriginalValue);
        Assert.True(mass.IsImprovement);
    }

    [Fact]
    public void EverythingElseOnTheShipIsLeftAlone()
    {
        var ship = Ship(Boarded, RolledTheSensors);

        // The fold is one slot, not a rewrite. A Loadout replaces the whole picture because it
        // carries the whole picture; EngineerCraft carries one module and must not be read as
        // saying anything about the other two.
        Assert.Equal(53, ship.ShipId);
        Assert.Equal("type9_military", ship.Type);
        Assert.Equal(3, ship.Modules.Count);
        Assert.False(Slot(ship, "Slot01_Size8").IsEngineered);
        Assert.False(Slot(ship, "TinyHardpoint2").IsEngineered);
    }

    [Fact]
    public void ApplyingAnExperimentalKeepsTheBlueprintUnderneathIt()
    {
        var shields = Slot(Ship(Boarded, AppliedFastCharge), "Slot01_Size8");

        // The name a Commander would say back, not the symbol — a plan naming "Fast Charge" is
        // checked against this exactly, because Elite localises this one and never the blueprint.
        Assert.Equal("Fast Charge", shields.Experimental);
        Assert.Equal("ShieldGenerator_Reinforced", shields.Blueprint);
        Assert.Equal(5, shields.BlueprintLevel);
    }

    [Fact]
    public void APlainRollDoesNotTakeAwayAnExperimentalAlreadyOnTheModule()
    {
        // A grade roll writes no experimental field at all, which is not the same as writing an
        // empty one. Reading absence as "there is none" would strip Fast Charge back off the
        // moment the Commander improved the grade under it.
        var reRolled =
            """
            {"timestamp":"2026-08-20T01:40:00Z","event":"EngineerCraft","Slot":"Slot01_Size8",
             "Module":"int_shieldgenerator_size8_class5","Engineer":"Lei Cheung","EngineerID":300120,
             "BlueprintName":"ShieldGenerator_Reinforced","Level":5,"Quality":0.4,"Modifiers":[]}
            """;

        var shields = Slot(Ship(Boarded, AppliedFastCharge, reRolled), "Slot01_Size8");

        Assert.Equal("Fast Charge", shields.Experimental);
        Assert.Equal(0.4, shields.Quality);
    }

    [Fact]
    public void ASlotTheShipDoesNotHaveIsNotInvented()
    {
        // EngineerCraft carries no ShipID, so the only thing tying it to a ship is that a
        // Commander can engineer the one they are sitting in and no other. That holds — but an
        // event naming a slot this loadout has never listed is a disagreement, and answering it
        // by adding a module with no Item, health or value would put a phantom in the fleet
        // report. Nothing known is lost by ignoring it: the next Loadout carries the truth.
        var elsewhere =
            """
            {"timestamp":"2026-08-20T01:41:00Z","event":"EngineerCraft","Slot":"Slot09_Size3",
             "Module":"int_buggybay_size2_class1","Engineer":"Lei Cheung","EngineerID":300120,
             "BlueprintName":"Misc_LightWeight","Level":3,"Quality":1.0,"Modifiers":[]}
            """;

        var ship = Ship(Boarded, elsewhere);

        Assert.Equal(3, ship.Modules.Count);
        Assert.DoesNotContain(ship.Modules, module => module.Slot == "Slot09_Size3");
    }

    [Fact]
    public void EngineeringBeforeAnyLoadoutHasBeenSeenChangesNothing()
    {
        // d47 started mid-session and has no picture of the ship. There is no module to update
        // and nothing to build one out of, so the state stays unknown rather than becoming a
        // one-module ship with no id.
        var ship = Ship(RolledTheSensors);

        Assert.Empty(ship.Modules);
        Assert.Null(ship.ShipId);
    }

    [Fact]
    public void TheChecklistItemIsDoneAtTheConsoleRatherThanAtTheNextBoarding()
    {
        // The whole point, end to end. This is Commander JOHN DEPARAGON's own plan item, against
        // his own journal: `bp/radar`, grade 5 Light Weight Scanner on the Type-10.
        var store = new GameStateStore();

        foreach (var json in new[]
                 {
                     """{"timestamp":"2026-08-20T00:40:00Z","event":"Commander","FID":"F735466","Name":"JOHN DEPARAGON"}""",
                     Boarded,
                 })
        {
            store.Apply(Event(json));
        }

        var item = new ChecklistItem
        {
            Key = "bp/radar",
            Scope = ChecklistScope.Ship(53),
            Kind = ChecklistItemKind.Derived,
            Source = ChecklistSource.EngineeringPlan,
            Text = "Grade 5 Light Weight Scanner on Radar",
            Hull = "type9_military",
            Intent = new ChecklistIntent(ChecklistIntentKind.Blueprint, "Radar")
            {
                Detail = "Light Weight Scanner",
                Grade = 5,
            },
        };

        Assert.Equal(ChecklistState.Open, ChecklistEvaluator.Evaluate(item, store.Active)!.Value.State);

        store.Apply(Event(RolledTheSensors));

        // Done the moment the last roll lands, with no Loadout in between — which is what puts
        // "that is done" in the Commander's ear while they are still at the console.
        Assert.Equal(ChecklistState.Done, ChecklistEvaluator.Evaluate(item, store.Active)!.Value.State);
    }
}
