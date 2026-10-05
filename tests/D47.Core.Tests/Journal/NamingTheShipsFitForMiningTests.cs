using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class NamingTheShipsFitForMiningTests
{
    private const string Laser = """{"Slot":"HugeHardpoint1","Item":"hpt_miningtoolv2_fixed_huge","On":true,"Priority":0}""";
    private const string Collector = """{"Slot":"Slot01_Size3","Item":"int_dronecontrol_collection_size3_class3","On":true,"Priority":0}""";
    private const string Refinery = """{"Slot":"Slot02_Size3","Item":"int_refinery_size3_class2","On":true,"Priority":0}""";

    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

    private static string Loadout(int id, string ship, string name, params string[] modules) =>
        $$"""{"timestamp":"2026-09-05T10:0{{id}}:00Z","event":"Loadout","Ship":"{{ship}}","ShipID":{{id}},"ShipName":"{{name}}","CargoCapacity":100,"Modules":[{{string.Join(",", modules)}}]}""";

    private static ShipLoadout Parsed(params string[] modules)
    {
        Assert.True(JournalEvent.TryParse(Loadout(1, "Type9", "Hammer", modules), NullLogger.Instance, out var parsed));

        return new ShipLoadout().Apply(parsed!);
    }

    [Fact]
    public void LasersCollectorsAndARefineryAreFitWithTheEvidence()
    {
        var fit = MiningFit.For(Parsed(Laser, Collector, Collector, Refinery));

        Assert.NotNull(fit);
        Assert.True(fit.IsFit);
        Assert.Contains("two collector controllers", fit.Evidence, StringComparison.Ordinal);
        Assert.Contains("a refinery", fit.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void NoRefineryIsNotFitAndSaysSo()
    {
        var fit = MiningFit.For(Parsed(Laser, Collector));

        Assert.NotNull(fit);
        Assert.False(fit.IsFit);
        Assert.Equal("no refinery", fit.Evidence);
    }

    [Fact]
    public void ALoadoutThatListsNoModulesCannotAnswer()
    {
        Assert.Null(MiningFit.For(Parsed()));
        Assert.Null(MiningFit.For(ShipLoadout.Unknown));
    }

    [Fact]
    public async Task TheFleetListsOnlyFitShipsAndReportsTheUnreadOnesAsNotSeenFitted()
    {
        var gameState = new GameStateStore();

        Apply(gameState, """{"timestamp":"2026-09-05T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply(gameState, Loadout(1, "Type9", "Hammer", Laser, Collector, Refinery));
        Apply(gameState, Loadout(2, "Python", "Mule", Laser, Collector));
        Apply(gameState, Loadout(3, "Asp", "Wanderer"));

        var result = await CapabilityRegistry.Build([JournalCapability.Create(gameState)]).InvokeAsync(
            "get_fleet_loadouts",
            new ToolArguments(new Dictionary<string, string> { ["mining"] = "true" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        var listed = result.Content.Split('\n').Where(line => line.StartsWith("  ", StringComparison.Ordinal)).ToArray();

        Assert.Single(listed);
        Assert.Contains("Hammer", listed[0], StringComparison.Ordinal);
        Assert.Contains("a refinery", listed[0], StringComparison.Ordinal);
        Assert.Contains("Mule", result.Content, StringComparison.Ordinal);
        Assert.Contains("no refinery", result.Content, StringComparison.Ordinal);
        Assert.Contains("Not seen fitted, so not covered: Wanderer", result.Content, StringComparison.Ordinal);
    }
}
