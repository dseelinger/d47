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

        // Grouped, because the question underneath is "can I fit it here or must I fetch it" —
        // and the transfer cost is the number that answers it.
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

        // "Nothing matches" on its own reads as an empty store. The total is what tells the
        // Commander the difference between the two.
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

        // "No modules in storage" would be a claim about the Commander's property that nothing
        // has established. This says which event would establish it.
        Assert.Contains("dock at a station with outfitting", result.Content, StringComparison.Ordinal);
    }
}
