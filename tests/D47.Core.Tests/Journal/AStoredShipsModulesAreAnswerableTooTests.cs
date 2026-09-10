using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// <c>loadouts.json</c> holds every ship's modules, not only the one being flown — so
/// <c>get_ship</c> answers a named ship from it instead of refusing (#108).
/// </summary>
public class AStoredShipsModulesAreAnswerableTooTests
{
    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static void Apply(GameStateStore gameState, string json) => gameState.Apply(Parse(json));

    private static Task<ToolResult> Ask(GameStateStore gameState, string? ship = null) =>
        CapabilityRegistry.Build([JournalCapability.Create(gameState)]).InvokeAsync(
            "get_ship",
            ship is null
                ? ToolArguments.Empty
                : new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal) { ["ship"] = ship }),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task ANamedShipReportsItsModulesFromWhereTheCommanderIsFlyingSomethingElse()
    {
        var gameState = new GameStateStore();

        Apply(gameState, """{"timestamp":"2026-09-05T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply(
            gameState,
            """
            {"timestamp":"2026-08-01T09:00:00Z","event":"Loadout","Ship":"panthermkii","ShipID":41,"ShipName":"Campaigner",
             "CargoCapacity":1200,"MaxJumpRange":48.14,"UnladenMass":1575.4,"FuelCapacity":{"Main":128,"Reserve":1.5},
             "Modules":[{"Slot":"Slot01_Size8","Item":"int_cargorack_size8_class1","On":true,"Health":1.0}]}
            """);

        // Boarded since, so this is the ship being flown right now.
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-05T10:01:00Z","event":"Loadout","Ship":"Anaconda","ShipID":7,"ShipName":"Bold Endeavour",
             "CargoCapacity":8,"Modules":[]}
            """);

        var result = await Ask(gameState, "Panther Clipper");

        Assert.False(result.IsError);
        Assert.Contains("Campaigner", result.Content, StringComparison.Ordinal);
        Assert.Contains("1 modules fitted", result.Content, StringComparison.Ordinal);
        Assert.Contains("as of 2026-08-01 09:00 UTC", result.Content, StringComparison.Ordinal);

        // The figures are the Panther's, not the ship the Commander is actually sitting in.
        Assert.Contains("cargo capacity 1200 t", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Bold Endeavour", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AShipOwnedButNeverBoardedSaysSoRatherThanInventingAFit()
    {
        var gameState = new GameStateStore();

        Apply(gameState, """{"timestamp":"2026-09-05T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-05T10:01:00Z","event":"Loadout","Ship":"Anaconda","ShipID":7,"ShipName":"Bold Endeavour",
             "CargoCapacity":8,"Modules":[]}
            """);
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-05T10:02:00Z","event":"StoredShips","StarSystem":"Sol","StationName":"Abraham Lincoln",
             "ShipsHere":[],"ShipsRemote":[{"ShipID":41,"ShipType":"panthermkii","Name":"Campaigner","StarSystem":"Sol"}]}
            """);

        var result = await Ask(gameState, "Panther Clipper");

        Assert.False(result.IsError);
        Assert.Contains("no loadout has been read for it yet", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("modules fitted", result.Content, StringComparison.Ordinal);
    }

    /// <summary>The second defect #108 names: a restart before a fresh <c>Loadout</c> answers about
    /// the Commander's own ship from what is remembered rather than refusing (#337, one level down).</summary>
    [Fact]
    public async Task WithNoLoadoutReadThisSessionTheFlownShipAnswersFromMemory()
    {
        var remembered = ShipLoadouts.Empty.Remember(
            ShipLoadout.Unknown.Apply(Parse(
                """
                {"timestamp":"2026-08-01T09:00:00Z","event":"Loadout","Ship":"Anaconda","ShipID":7,
                 "ShipName":"Bold Endeavour","CargoCapacity":64,"MaxJumpRange":52.31,"Modules":[]}
                """)),
            DateTimeOffset.Parse("2026-08-01T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        var gameState = new GameStateStore { RestoreLoadouts = _ => remembered };

        Apply(gameState, """{"timestamp":"2026-09-05T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-05T10:00:01Z","event":"LoadGame","FID":"F1","Commander":"Jameson",
             "Ship":"Anaconda","ShipID":7,"Credits":100}
            """);

        var result = await Ask(gameState);

        Assert.False(result.IsError);
        Assert.DoesNotContain("No Loadout event has been seen yet", result.Content, StringComparison.Ordinal);
        Assert.Contains("Bold Endeavour", result.Content, StringComparison.Ordinal);
        Assert.Contains("as of 2026-08-01 09:00 UTC", result.Content, StringComparison.Ordinal);
    }
}
