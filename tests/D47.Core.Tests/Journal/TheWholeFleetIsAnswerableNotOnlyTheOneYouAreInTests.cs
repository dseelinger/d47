using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Ships;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// Every ship's cargo capacity and jump range are in the remembered loadouts, so comparing ships is
/// an answer rather than a refusal (#97).
/// </summary>
public class TheWholeFleetIsAnswerableNotOnlyTheOneYouAreInTests
{
    /// <summary>
    /// Three ships, ending aboard the Anaconda — which has the longest jump of the three and a hold
    /// too small to qualify, so a filtered ranking cannot be the flown ship by accident.
    /// </summary>
    private static GameStateStore Fleet()
    {
        var gameState = new GameStateStore();

        Apply(gameState, """{"timestamp":"2026-09-05T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-05T10:01:00Z","event":"Loadout","Ship":"Python","ShipID":8,"ShipName":"Mule","CargoCapacity":128,"MaxJumpRange":21.44,"UnladenMass":350.6,"FuelCapacity":{"Main":32,"Reserve":0.83},"HullValue":56000000,"ModulesValue":5204110,"Rebuy":3060205,"Modules":[]}
            """);
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-05T10:02:00Z","event":"Loadout","Ship":"Asp","ShipID":9,"ShipName":"Wanderer","CargoCapacity":32,"MaxJumpRange":41.5,"UnladenMass":280,"FuelCapacity":{"Main":32,"Reserve":0.63},"HullValue":6100000,"ModulesValue":18010400,"Rebuy":1205520,"Modules":[]}
            """);
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-05T10:05:00Z","event":"Loadout","Ship":"Anaconda","ShipID":7,"ShipName":"Bold Endeavour","CargoCapacity":8,"MaxJumpRange":52.31,"UnladenMass":1122.6,"FuelCapacity":{"Main":32,"Reserve":1.07},"HullValue":210000000,"ModulesValue":9694497,"Rebuy":9694497,"Modules":[]}
            """);
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-05T10:06:00Z","event":"StoredShips","StarSystem":"Sol","StationName":"Abraham Lincoln","ShipsHere":[],"ShipsRemote":[{"ShipID":8,"ShipType":"Python","Name":"Mule","StarSystem":"Shinrarta Dezhra","Value":61204110},{"ShipID":9,"ShipType":"Asp","Name":"Wanderer","StarSystem":"Deciat","Value":24110400}]}
            """);

        return gameState;
    }

    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

    private static Task<ToolResult> Ask(GameStateStore gameState, params (string Name, string Value)[] arguments) =>
        CapabilityRegistry.Build([JournalCapability.Create(gameState)]).InvokeAsync(
            "get_fleet_loadouts",
            new ToolArguments(arguments.ToDictionary(argument => argument.Name, argument => argument.Value)),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task TheShipWithTheBestJumpRangeAboveACargoFloorIsOneTheCommanderIsNotIn()
    {
        var result = await Ask(Fleet(), ("min_cargo", "24"), ("order_by", "jump_range"));

        Assert.False(result.IsError);

        var lines = result.Content.Split('\n').Select(line => line.Trim()).ToArray();
        var ranked = lines.Where(line => line.StartsWith("Wanderer", StringComparison.Ordinal)
                                         || line.StartsWith("Mule", StringComparison.Ordinal))
            .ToArray();

        // The Asp first, and with both figures the question asked about on its line.
        Assert.Equal(2, ranked.Length);
        Assert.StartsWith("Wanderer", ranked[0], StringComparison.Ordinal);
        Assert.Contains("cargo 32 t", ranked[0], StringComparison.Ordinal);
        Assert.Contains("jump 41.5 ly", ranked[0], StringComparison.Ordinal);

        // The Anaconda jumps further than either and is under the floor, so it must not be listed.
        Assert.DoesNotContain("Bold Endeavour", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NothingBigEnoughSaysSoAndNamesTheLargestHold()
    {
        var result = await Ask(Fleet(), ("min_cargo", "512"));

        Assert.False(result.IsError);
        Assert.Contains("No ship I remember carries 512 t", result.Content, StringComparison.Ordinal);
        Assert.Contains("Mule", result.Content, StringComparison.Ordinal);
        Assert.Contains("128 t", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheJumpRangeIsNamedAsFullTankAndEmptyHold()
    {
        var result = await Ask(Fleet());

        Assert.Contains(
            "full tank with an empty hold",
            result.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AShipIsDatedFromWhenItWasLastSeenRatherThanFromNow()
    {
        var result = await Ask(Fleet());

        var mule = result.Content
            .Split('\n')
            .Single(line => line.Contains("Mule", StringComparison.Ordinal));

        // Boarded at 10:01 and not since, while the Anaconda under the Commander was boarded at 10:05.
        Assert.Contains("as of 2026-09-05 10:01 UTC", mule, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AShipWithNoLoadoutReadIsNamedRatherThanLeftOut()
    {
        var gameState = new GameStateStore();

        Apply(gameState, """{"timestamp":"2026-09-05T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""");
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-05T10:05:00Z","event":"Loadout","Ship":"Anaconda","ShipID":7,"ShipName":"Bold Endeavour","CargoCapacity":8,"MaxJumpRange":52.31,"Modules":[]}
            """);
        Apply(
            gameState,
            """
            {"timestamp":"2026-09-05T10:06:00Z","event":"StoredShips","StarSystem":"Sol","StationName":"Abraham Lincoln","ShipsHere":[],"ShipsRemote":[{"ShipID":8,"ShipType":"Python","Name":"Mule","StarSystem":"Shinrarta Dezhra"}]}
            """);

        var result = await Ask(gameState);

        Assert.Contains("No loadout read, so not covered above: Mule (Python).", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// The count the model is told every turn stays a count, so asking how many ships there are still
    /// costs no tool call.
    /// </summary>
    [Fact]
    public void TheFleetCountIsStillToldWithoutATool()
    {
        using var install = new TempInstall();
        var state = Fleet().Active;

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(install.Root, "ships.json"), NullLogger<ShipBuildStore>.Instance),
            new ChecklistService(
                new ChecklistStore(
                    Path.Combine(install.Root, "checklist.json"),
                    NullLogger<ChecklistStore>.Instance),
                new ChecklistProposalStore(
                    Path.Combine(install.Root, "checklist-proposals.json"),
                    NullLogger<ChecklistProposalStore>.Instance),
                () => state),
            () => state);

        Assert.Equal("Fleet: 3 ships, none with a build planned.", ShipsCapability.Live(ships));
    }
}
