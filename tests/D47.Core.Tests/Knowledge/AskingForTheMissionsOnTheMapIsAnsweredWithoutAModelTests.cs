using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AskingForTheMissionsOnTheMapIsAnsweredWithoutAModelTests
{
    private static CommanderGameState Accepting(params (long Id, string System)[] missions)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Test Commander"));

        foreach (var (id, system) in missions)
        {
            Assert.True(JournalEvent.TryParse(
                "{\"timestamp\":\"2026-10-21T11:00:00Z\",\"event\":\"MissionAccepted\",\"MissionID\":" + id
                + ",\"Name\":\"Mission_Courier\",\"DestinationSystem\":\"" + system + "\"}",
                NullLogger.Instance,
                out var accepted));
            state.Apply(accepted!);
        }

        return state;
    }

    [Fact]
    public void ThePhraseReachesTheToolWithNoModel()
    {
        var registry = CapabilityRegistry.Build([AstrometricsCapability.Create(() => null)]);
        var match = new KeywordRouter(registry).MatchToolCommand("show my missions on the map");

        Assert.NotNull(match);
        Assert.Equal(AstrometricsCapability.Tool, match.ToolName);
        Assert.True(match.Arguments.TryGetString("chart", out var chart));
        Assert.Equal(AstrometricsCapability.MissionDestinationsChart, chart);
    }

    [Fact]
    public async Task TheToolAddsTheLayerAndCountsTheMissionsAndTheirSystems()
    {
        var state = Accepting((1, "Alpha"), (2, "Alpha"), (3, "Beta"));
        var registry = CapabilityRegistry.Build([AstrometricsCapability.Create(() => state)]);

        var result = await registry.InvokeAsync(
            AstrometricsCapability.Tool,
            new ToolArguments(new Dictionary<string, string> { ["chart"] = "mission_destinations" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("You have 3 active missions bound for 2 systems.", result.Content);
        Assert.Equal(PageRef.StarChart(StarChartKind.MissionDestinations), result.Page);
    }

    [Fact]
    public void NoMissionsIsSaidPlainly()
    {
        Assert.Equal("You have no active missions with a destination.", AstrometricsCapability.DescribeMissionDestinations(MissionBoard.Empty));
    }
}
