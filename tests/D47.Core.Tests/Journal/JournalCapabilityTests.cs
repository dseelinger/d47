using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class JournalCapabilityTests
{
    private static void Apply(GameStateStore gameState, string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        gameState.Apply(parsed!);
    }

 /// <summary><c>get_ship</c> says how full the hold is, not just how big it is.</summary>
    [Fact]
    public async Task ReportsHowFullTheHoldIsRatherThanOnlyItsCapacity()
    {
        var install = new TempInstall();

        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-09-05T000000.01.log"),
            [
                """{"timestamp":"3311-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                """{"timestamp":"3311-01-01T00:01:00Z","event":"Loadout","Ship":"Anaconda","ShipID":7,"ShipName":"Bold Endeavour","CargoCapacity":64,"Modules":[]}""",
            ]);

        File.WriteAllText(
            Path.Combine(install.Root, CargoManifestReader.ManifestFile),
            """{ "timestamp":"3311-01-01T00:01:00Z", "event":"Cargo", "Vessel":"Ship", "Count":64, "Inventory":[ { "Name":"gold", "Name_Localised":"Gold", "Count":64, "Stolen":0 } ] }""");

        var gameState = new GameStateStore();
        new JournalSpine(install.Root, gameState, NullLoggerFactory.Instance).Poll();

        var registry = CapabilityRegistry.Build([JournalCapability.Create(gameState)]);
        var result = await registry.InvokeAsync("get_ship", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("cargo 64/64 t", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("cargo capacity", result.Content, StringComparison.Ordinal);
    }

    /// <summary>
    /// And with no <c>Cargo.json</c> read for the ship, it states the capacity alone rather than
    /// claiming a fill figure it does not have.
    /// </summary>
    [Fact]
    public async Task AnUnreadHoldFallsBackToStatingTheCapacityAlone()
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(
            gameState,
            """{"timestamp":"2026-01-01T00:00:01Z","event":"Loadout","Ship":"Anaconda","ShipID":7,"CargoCapacity":64,"Modules":[]}""");

        var registry = CapabilityRegistry.Build([JournalCapability.Create(gameState)]);
        var result = await registry.InvokeAsync("get_ship", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Contains("cargo capacity 64 t", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoJournalDetectedTheAnswerSaysSoRatherThanGuessing()
    {
        var registry = CapabilityRegistry.Build([JournalCapability.Create(new GameStateStore())]);

        var result = await registry.InvokeAsync("get_location", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Contains("No Elite Dangerous journal", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsTheActiveCommandersRealLocation()
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(
            gameState,
            """{"timestamp":"2026-01-01T00:00:01Z","event":"FSDJump","StarSystem":"Fixture Nebula Point","Body":"Fixture Nebula Point A"}""");

        var registry = CapabilityRegistry.Build([JournalCapability.Create(gameState)]);
        var result = await registry.InvokeAsync("get_location", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        // Arriving from a hyperspace jump drops you into supercruise, and Phase 7 now says so.
        Assert.Equal(
            "Fixture is in Fixture Nebula Point, near Fixture Nebula Point A. Currently in supercruise.",
            result.Content);
    }

    [Fact]
    public async Task ReportsDockedState()
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(
            gameState,
            """{"timestamp":"2026-01-01T00:00:01Z","event":"Docked","StationName":"Fixture Outpost","StarSystem":"Fixture Nebula Point"}""");

        var registry = CapabilityRegistry.Build([JournalCapability.Create(gameState)]);
        var result = await registry.InvokeAsync("get_location", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.Equal("Fixture is in Fixture Nebula Point, docked at Fixture Outpost.", result.Content);
    }

    private const string StoredModules =
        """
        {"timestamp":"2026-01-01T00:00:02Z","event":"StoredModules","StationName":"Fixture Outpost",
         "StarSystem":"Fixture Nebula Point",
         "Items":[
           {"Name":"$hpt_beamlaser_fixed_medium_name;","Name_Localised":"Beam Laser",
            "StarSystem":"Fixture Nebula Point","TransferCost":0},
           {"Name":"$int_shieldgenerator_size5_class5_name;","Name_Localised":"Shield Generator",
            "StarSystem":"Fixture Depot","TransferCost":58000,"TransferTime":3600}]}
        """;

    private static CapabilityRegistry WithStoredModules()
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(gameState, StoredModules);

        return CapabilityRegistry.Build([JournalCapability.Create(gameState)]);
    }

    [Fact]
    public async Task StoredModulesAreGroupedByWhereTheyAreWithWhatFetchingThemCosts()
    {
        var result = await WithStoredModules()
            .InvokeAsync("get_stored_modules", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        // Grouped, because the question underneath is "can I fit it here or must I fetch it" — and the
        // transfer cost is the number that answers it.
        Assert.Contains("Fixture Nebula Point (where you are):", result.Content, StringComparison.Ordinal);
        Assert.Contains("58,000 cr to transfer, 60 minutes", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AskingForOneModuleNarrowsTheListAndSaysHowMuchWasLeftOut()
    {
        var result = await WithStoredModules().InvokeAsync(
            "get_stored_modules",
            ToolArguments.FromJson("""{"module":"interdictor"}"""),
            TestContext.Current.CancellationToken);

        // "Nothing matches" on its own reads as an empty store.
        Assert.Contains("Nothing in storage matches 'interdictor'", result.Content, StringComparison.Ordinal);
        Assert.Contains("2 modules are stored in total", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoStoredModulesEventTheAnswerNamesTheEventItIsWaitingFor()
    {
        var gameState = new GameStateStore();
        Apply(gameState, """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");

        var result = await CapabilityRegistry.Build([JournalCapability.Create(gameState)])
            .InvokeAsync("get_stored_modules", ToolArguments.Empty, TestContext.Current.CancellationToken);

        // "No modules in storage" would be a claim about the Commander's property that nothing has
        // established.
        Assert.Contains("dock at a station with outfitting", result.Content, StringComparison.Ordinal);
    }
 /// <summary>The stored fleet is a snapshot and says when it was taken.</summary>
    [Fact]
    public async Task TheStoredFleetIsDatedAndInTransitIsPastTense()
    {
        var gameState = new GameStateStore();

        Apply(gameState, """{"timestamp":"2026-01-01T00:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""");
        Apply(gameState, """{"timestamp":"2026-01-01T00:00:03Z","event":"StoredShips","StationName":"Jameson Memorial","StarSystem":"Shinrarta Dezhra","ShipsHere":[],"ShipsRemote":[{"ShipID":13,"ShipType":"krait_mkii","Name":"Sacred Fire","StarSystem":"Laksak","InTransit":true}]}""");

        var registry = CapabilityRegistry.Build([JournalCapability.Create(gameState)]);

        var result = await registry.InvokeAsync(
            "get_fleet", AskedForShips, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        // When the list was read, so nothing downstream can present it as the state of now.
        Assert.Contains("as of 2026-01-01 00:00 UTC", result.Content, StringComparison.Ordinal);

        // And the tense the snapshot was taken in, with the reason attached: a transfer that has since landed
        // looks exactly the same from here.
        Assert.Contains("In transit when that was read", result.Content, StringComparison.Ordinal);
        Assert.Contains("do not say one is still moving", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("In transit: ", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCarrierIsRememberedFromAnEarlierSessionAndSaysWhenItWasLastSeen()
    {
        using var install = new TempInstall();
        WriteCarrierHistory(install);

        foreach (var _ in new[] { "first run", "after a restart" })
        {
            var result = await CarrierReportAsync(install);

            // The system, and the age of the answer, on the same terms as the fleet line beside it.
            Assert.Contains("Sacred Fire (BNH-T2F) is in Meene", result, StringComparison.Ordinal);
            Assert.Contains("as of 2026-09-05 16:36 UTC", result, StringComparison.Ordinal);

            // The Commander's session boundary is not a fact about his carrier.
            Assert.DoesNotContain("this session", result, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// And the system it plots to is the one it just reported — the two answers cannot drift apart,
 /// whichever of them the Commander acts on.
    /// </summary>
    [Fact]
    public async Task ACourseToTheCarrierGoesToTheSystemJustReported()
    {
        using var install = new TempInstall();
        WriteCarrierHistory(install);

        var gameState = StoreOver(install);
        var result = await CarrierReportAsync(gameState);

        var plotted = CarrierCourse.Phrases(() => gameState.Active?.Carrier)
            .First(command => command.ToolName == "plot_course")
            .Arguments["system"];

        Assert.Equal("Meene", plotted);
        Assert.Contains($"is in {plotted}", result, StringComparison.Ordinal);
    }

    /// <summary>Unknown stays unknown.</summary>
    [Fact]
    public async Task WithNoCarrierInAnyJournalItSaysItDoesNotKnow()
    {
        using var install = new TempInstall();

        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-09-07T100000.01.log"),
            [
                """{"timestamp":"2026-09-07T10:00:00Z","event":"Commander","FID":"F1","Name":"Fixture"}""",
                """{"timestamp":"2026-09-07T10:10:00Z","event":"StoredShips","StarSystem":"Meene","StationName":"BNH-T2F","ShipsHere":[{"ShipID":1,"ShipType":"Sidewinder","Value":1}],"ShipsRemote":[]}""",
            ]);

        var result = await CarrierReportAsync(install);

        Assert.Contains("I do not know where one is", result, StringComparison.Ordinal);
        Assert.DoesNotContain("is in Meene", result, StringComparison.Ordinal);
        Assert.DoesNotContain("this session", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Two Commanders share one journal folder and neither is handed the other's carrier — the
    /// isolation rule the whole store is built on, asserted through the answer rather than the backfill
    /// alone.
    /// </summary>
    [Fact]
    public async Task TheRememberedCarrierIsPerCommander()
    {
        using var install = new TempInstall();
        WriteCarrierHistory(install);

        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-09-08T100000.01.log"),
            [
                """{"timestamp":"2026-09-08T10:00:00Z","event":"LoadGame","FID":"F7654321","Commander":"Other"}""",
            ]);

        var result = await CarrierReportAsync(install);

        // The newest journal belongs to the other Commander, who owns no carrier.
        Assert.Contains("I do not know where one is", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Meene", result, StringComparison.Ordinal);
    }

 /// <summary>The ship list is only read out when it was asked for.</summary>
    [Fact]
    public async Task TheCarrierAnswerNamesNoShipsAndTheShipQuestionStillDoes()
    {
        using var install = new TempInstall();
        WriteCarrierHistory(install);

        File.AppendAllLines(
            Path.Combine(install.Root, "Journal.2026-09-07T100000.01.log"),
            [
                """{"timestamp":"2026-09-07T10:05:00Z","event":"StoredShips","StarSystem":"Meene","StationName":"BNH-T2F","ShipsHere":[{"ShipID":13,"ShipType":"krait_mkii","Name":"Reaper","Value":1}],"ShipsRemote":[]}""",
            ]);

        var gameState = StoreOver(install);
        var registry = CapabilityRegistry.Build([JournalCapability.Create(gameState)]);

        var carrier = await registry.InvokeAsync(
            "get_fleet", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(carrier.IsError);

        // Where it is, when that was reported, and nothing else.
        Assert.Contains("is in Meene", carrier.Content, StringComparison.Ordinal);
        Assert.Contains("as of 2026-09-05 16:36 UTC", carrier.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Reaper", carrier.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Currently flying", carrier.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("stored", carrier.Content, StringComparison.OrdinalIgnoreCase);

        var fleet = await registry.InvokeAsync(
            "get_fleet", AskedForShips, TestContext.Current.CancellationToken);

        Assert.False(fleet.IsError);

        // And asked for the ships, it still answers exactly as it did — carrier first, then them.
        Assert.Contains("is in Meene", fleet.Content, StringComparison.Ordinal);
        Assert.Contains("Reaper", fleet.Content, StringComparison.Ordinal);
        Assert.Contains("Meene:", fleet.Content, StringComparison.Ordinal);
    }

 /// <summary>A ship to a line, under the system that holds them.</summary>
    [Fact]
    public async Task StoredShipsAreListedOneToALineWithNothingForAVoiceToReadOut()
    {
        using var install = new TempInstall();
        WriteCarrierHistory(install);

        File.AppendAllLines(
            Path.Combine(install.Root, "Journal.2026-09-07T100000.01.log"),
            [
                """{"timestamp":"2026-09-07T10:05:00Z","event":"StoredShips","StarSystem":"Meene","StationName":"BNH-T2F","ShipsHere":[{"ShipID":13,"ShipType":"krait_mkii","Name":"Reaper","Value":1},{"ShipID":14,"ShipType":"anaconda","Name":"Flamebrand","Value":1}],"ShipsRemote":[]}""",
            ]);

        var registry = CapabilityRegistry.Build([JournalCapability.Create(StoreOver(install))]);

        var fleet = await registry.InvokeAsync(
            "get_fleet", AskedForShips, TestContext.Current.CancellationToken);

        Assert.False(fleet.IsError);

        var lines = fleet.Content.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

        // The system is its own heading, and each ship is alone on the line under it.
        Assert.Contains("  Meene:", lines);
        Assert.Contains(lines, line => line.StartsWith("    ", StringComparison.Ordinal)
            && line.Contains("Reaper", StringComparison.Ordinal)
            && !line.Contains("Flamebrand", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("    ", StringComparison.Ordinal)
            && line.Contains("Flamebrand", StringComparison.Ordinal)
            && !line.Contains("Reaper", StringComparison.Ordinal));

        // The count and the date are where they were.
        Assert.Contains("2 other ships stored", fleet.Content, StringComparison.Ordinal);
        Assert.Contains("as of ", fleet.Content, StringComparison.Ordinal);

        // And nothing marks a line that a voice would have to say.
        Assert.DoesNotContain(lines, line => line.TrimStart().StartsWith('-')
            || line.TrimStart().StartsWith('*')
            || line.TrimStart().StartsWith('•'));
    }

    /// <summary>
    /// A folder holding one session that reports the carrier and a later one that does not — the
    /// reported shape exactly.
    /// </summary>
    private static void WriteCarrierHistory(TempInstall install)
    {
        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-09-05T100000.01.log"),
            [
                """{"timestamp":"2026-09-05T10:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""",
                """{"timestamp":"2026-09-05T16:00:00Z","event":"CarrierStats","CarrierID":3715429376,"Callsign":"BNH-T2F","Name":"Sacred Fire","DockingAccess":"all","FuelLevel":900}""",
                """{"timestamp":"2026-09-05T16:36:00Z","event":"CarrierLocation","CarrierID":3715429376,"CarrierType":"FleetCarrier","StarSystem":"Meene"}""",
            ]);

        File.WriteAllLines(
            Path.Combine(install.Root, "Journal.2026-09-07T100000.01.log"),
            [
                """{"timestamp":"2026-09-07T10:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""",
            ]);
    }

    /// <summary>
    /// A store wired to the folder the way <c>AppHost</c> wires the real one, and driven over it once.
    /// </summary>
    private static GameStateStore StoreOver(TempInstall install)
    {
        var carriers = CarrierBackfill.FromHistory(install.Root, NullLogger.Instance);

        var gameState = new GameStateStore
        {
            RestoreCarrier = fid => carriers.TryGetValue(fid, out var carrier) ? carrier : null,
        };

        new JournalSpine(install.Root, gameState, NullLoggerFactory.Instance).Poll();

        return gameState;
    }

    private static Task<string> CarrierReportAsync(TempInstall install) =>
        CarrierReportAsync(StoreOver(install));

    private static async Task<string> CarrierReportAsync(GameStateStore gameState)
    {
        var registry = CapabilityRegistry.Build([JournalCapability.Create(gameState)]);
        var result = await registry.InvokeAsync(
            "get_fleet", ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        return result.Content;
    }

    /// <summary>What a phrase asking for the ships carries, and nothing else does.</summary>
    private static readonly ToolArguments AskedForShips =
        new(new Dictionary<string, string> { ["ships"] = "true" });
}
