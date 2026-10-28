using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AJumpRangeQuestionIsAnsweredWithTheJumpRangeTests
{
    private const string Anaconda =
        """{"timestamp":"2026-09-05T10:05:00Z","event":"Loadout","Ship":"Anaconda","ShipID":7,"ShipName":"Bold Endeavour","CargoCapacity":64,"MaxJumpRange":52.31,"UnladenMass":1122.6,"FuelCapacity":{"Main":32,"Reserve":1.07},"Rebuy":9694497,"Modules":[]}""";

    private const string NoRange =
        """{"timestamp":"2026-09-05T10:05:00Z","event":"Loadout","Ship":"Anaconda","ShipID":7,"ShipName":"Bold Endeavour","CargoCapacity":64,"UnladenMass":1122.6,"Rebuy":9694497,"Modules":[]}""";

    private static GameStateStore Flying(string loadout)
    {
        var gameState = new GameStateStore();

        foreach (var json in new[]
                 {
                     """{"timestamp":"2026-09-05T10:00:00Z","event":"Commander","FID":"F1","Name":"Jameson"}""",
                     loadout,
                 })
        {
            Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
            gameState.Apply(parsed!);
        }

        return gameState;
    }

    private static CapabilityRegistry Registry(GameStateStore gameState) =>
        CapabilityRegistry.Build([JournalCapability.Create(gameState)]);

    [Theory]
    [InlineData("my jump range")]
    [InlineData("what is my jump range")]
    [InlineData("what's my jump range")]
    [InlineData("jump range")]
    public async Task TheRouterAnswersWithOneLine(string asked)
    {
        var registry = Registry(Flying(Anaconda));
        var router = new KeywordRouter(registry);

        var match = router.MatchToolCommand(asked) ?? throw new Xunit.Sdk.XunitException("no command");
        var result = await registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.Equal("get_ship", match.ToolName);
        Assert.Equal("Unladen jump range is 52.31 ly.", result.Content);
    }

    [Theory]
    [InlineData("my ship")]
    [InlineData("my loadout")]
    [InlineData("what am i flying")]
    public async Task TheShipQuestionsStillGiveTheFullReport(string asked)
    {
        var registry = Registry(Flying(Anaconda));
        var match = new KeywordRouter(registry).MatchToolCommand(asked)!;
        var result = await registry.InvokeAsync(match.ToolName, match.Arguments, TestContext.Current.CancellationToken);

        Assert.Contains("Flying Bold Endeavour", result.Content, StringComparison.Ordinal);
        Assert.Contains("Rebuy", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AShipWithNoRecordedRangeSaysSo()
    {
        var result = await Registry(Flying(NoRange)).InvokeAsync(
            "get_ship",
            new ToolArguments(new Dictionary<string, string> { ["jump_range"] = "true" }),
            TestContext.Current.CancellationToken);

        Assert.Equal("The jump range is not known yet.", result.Content);
    }
}
