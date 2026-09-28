using D47.Core.Engineers;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Engineers;

/// <summary>
/// A planned experimental is applied when the journal's symbol names it, whatever the localised name
/// says: Elite writes "Super Capacitors" for the experimental the catalogue calls "Super Capacitor"
/// (#658). The modules are the Commander's Tulimiekka (ShipID 49) Loadout of 2026-09-23.
/// </summary>
public class AnAppliedExperimentalIsMatchedBySymbolTests
{
    private const int ShipId = 49;

    private static readonly SlotPlan Booster =
        new("TinyHardpoint1", "Heavy Duty", 5, "Super Capacitor", "Shield Booster");

    private static readonly SlotPlan Generator =
        new("Slot01_Size5", "Reinforced", 5, "Hi-cap", "Shield Generator");

    [Fact]
    public void ABoosterWithSuperCapacitorsIsNotOutstanding()
    {
        var fitted = Fitted().Single(module => module.Slot == Booster.Slot);

        Assert.Equal("Super Capacitors", fitted.Experimental);
        Assert.False(ShipPlanService.Outstanding(Booster, fitted));
    }

    [Fact]
    public void ABoosterWithSuperCapacitorsAddsNothingToAnyEngineer()
    {
        var game = Game();
        var build = new ShipBuild("F1", "b1", "smallcombat01_nx", ShipId, Slots: [Booster]);

        Assert.Empty(EngineerWorkload.Outstanding([build], [], game.Active));
    }

    [Fact]
    public void AGeneratorMissingItsExperimentalIsStillOutstanding()
    {
        var fitted = Fitted().Single(module => module.Slot == Generator.Slot);

        Assert.True(ShipPlanService.Outstanding(Generator, fitted));

        var game = Game();
        var build = new ShipBuild("F1", "b1", "smallcombat01_nx", ShipId, Slots: [Generator]);

        Assert.NotEmpty(EngineerWorkload.Outstanding([build], [], game.Active));
    }

    [Fact]
    public void AModuleWithNoSymbolFallsBackToTheName()
    {
        var fitted = Fitted().Single(module => module.Slot == Booster.Slot) with
        {
            Experimental = "Super Capacitor",
            ExperimentalSymbol = null,
        };

        Assert.False(ShipPlanService.Outstanding(Booster, fitted));
    }

    private static IReadOnlyList<ShipModule> Fitted() => Game().Active!.Ship.Modules;

    private static GameStateStore Game()
    {
        var game = new GameStateStore();

        game.Apply(Event("""{"timestamp":"2026-09-23T20:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}"""));
        game.Apply(Event(Loadout));

        return game;
    }

    // The generator's Hi-Cap is taken off, so it stands for a roll whose experimental is not yet applied.
    private const string Loadout =
        """
        {"timestamp":"2026-09-23T20:22:30Z","event":"Loadout","Ship":"smallcombat01_nx","ShipID":49,
         "Modules":[
          {"Slot":"TinyHardpoint1","Item":"hpt_shieldbooster_size0_class5","On":true,"Priority":0,"Health":1.0,
           "Value":238850,"Engineering":{"Engineer":"Didi Vatermann","EngineerID":300000,"BlueprintID":128673784,
           "BlueprintName":"ShieldBooster_HeavyDuty","Level":5,"Quality":1.0,
           "ExperimentalEffect":"special_shieldbooster_chunky","ExperimentalEffect_Localised":"Super Capacitors",
           "Modifiers":[]}},
          {"Slot":"Slot01_Size5","Item":"int_shieldgenerator_size5_class3_fast","On":true,"Priority":0,"Health":1.0,
           "Value":723061,"Engineering":{"Engineer":"Lei Cheung","EngineerID":300120,"BlueprintID":128673839,
           "BlueprintName":"ShieldGenerator_Reinforced","Level":5,"Quality":1.0,"Modifiers":[]}}]}
        """;

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }
}
