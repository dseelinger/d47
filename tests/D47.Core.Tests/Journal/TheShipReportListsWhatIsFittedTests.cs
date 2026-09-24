using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// <c>get_ship</c> names each fitted module and its engineering, in the outfitting screen's order, and
/// leaves cosmetics out of the list and the count (#450).
/// </summary>
public class TheShipReportListsWhatIsFittedTests
{
    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

    private static async Task<string> Ask(string loadout)
    {
        var gameState = new GameStateStore();

        Apply(gameState, """{"timestamp":"2026-09-05T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply(gameState, loadout);

        var result = await CapabilityRegistry.Build([JournalCapability.Create(gameState)]).InvokeAsync(
            "get_ship",
            ToolArguments.Empty,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Content;
    }

    private const string Anaconda =
        """
        {"timestamp":"2026-09-05T10:01:00Z","event":"Loadout","Ship":"anaconda","ShipID":7,"ShipName":"Bold Endeavour",
         "Modules":[
          {"Slot":"PaintJob","Item":"paintjob_anaconda_default_01","On":true,"Priority":0},
          {"Slot":"FrameShiftDrive","Item":"int_hyperdrive_size6_class5","On":true,"Priority":0},
          {"Slot":"LargeHardpoint1","Item":"hpt_multicannon_fixed_large","On":false,"Priority":0,
           "Engineering":{"Engineer":"Tod 'The Blaster' McQuinn","EngineerID":300260,"BlueprintID":128673328,
            "BlueprintName":"Weapon_Overcharged","Level":5,"Quality":1.0,
            "ExperimentalEffect":"special_auto_loader","ExperimentalEffect_Localised":"Auto Loader","Modifiers":[]}}
         ]}
        """;

    [Fact]
    public async Task AWeaponIsNamedInItsSlotWithItsEngineering()
    {
        var report = await Ask(Anaconda);

        Assert.Contains("Hardpoints:", report, StringComparison.Ordinal);
        Assert.Contains(
            "Large Hardpoint 1: 3C Multi-Cannon, fixed — Overcharged Weapon, grade 5, Auto Loader",
            report,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnengineeredModuleAddsNothingAfterItsName()
    {
        var report = await Ask(Anaconda);

        Assert.Contains("Frame Shift Drive: 6A Frame Shift Drive" + Environment.NewLine, report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APaintJobIsInNeitherTheListNorTheCount()
    {
        var report = await Ask(Anaconda);

        Assert.DoesNotContain("paint", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 modules fitted, 1 engineered.", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HardpointsComeBeforeCoreInternalAsOutfittingOrdersThem()
    {
        var report = await Ask(Anaconda);

        Assert.True(
            report.IndexOf("Hardpoints:", StringComparison.Ordinal)
            < report.IndexOf("Core Internal:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnUnpoweredModuleIsNamedAsItIsSaid()
    {
        var report = await Ask(Anaconda);

        Assert.Contains("Unpowered: 3C Multi-Cannon, fixed.", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHullWithNoLayoutStillListsEveryModule()
    {
        var report = await Ask(
            """
            {"timestamp":"2026-09-05T10:01:00Z","event":"Loadout","Ship":"notahull","ShipID":8,
             "Modules":[{"Slot":"LargeHardpoint1","Item":"hpt_multicannon_fixed_large","On":true,"Priority":0}]}
            """);

        Assert.Contains("1 modules fitted", report, StringComparison.Ordinal);
        Assert.Contains("LargeHardpoint1: 3C Multi-Cannon, fixed", report, StringComparison.Ordinal);
    }
}
