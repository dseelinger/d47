using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AskingForWhereIDiedOnTheMapIsAnsweredWithoutAModelTests
{
    [Theory]
    [InlineData("show where I died")]
    [InlineData("my deaths on the map")]
    public void ThePhrasesReachTheToolWithNoModel(string phrase)
    {
        var registry = CapabilityRegistry.Build([AstrometricsCapability.Create(() => null)]);
        var match = new KeywordRouter(registry).MatchToolCommand(phrase);

        Assert.NotNull(match);
        Assert.Equal(AstrometricsCapability.Tool, match.ToolName);
        Assert.True(match.Arguments.TryGetString("chart", out var chart));
        Assert.Equal(AstrometricsCapability.DeathsChart, chart);
    }

    [Fact]
    public async Task TheToolAddsTheLayerAndCountsTheDeathsAndTheirSystems()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Test Commander"));
        IReadOnlyList<DeathPlace> places =
        [
            new(1, "Alpha", new StarPosition(0, 0, 0), 2),
            new(2, "Beta", new StarPosition(1, 0, 0), 1),
        ];
        var registry = CapabilityRegistry.Build([AstrometricsCapability.Create(() => state, fid => fid == "F1" ? places : [])]);

        var result = await registry.InvokeAsync(
            AstrometricsCapability.Tool,
            new ToolArguments(new Dictionary<string, string> { ["chart"] = "deaths" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("You have died 3 times in 2 systems.", result.Content);
        Assert.Equal(PageRef.StarChart(StarChartKind.Deaths), result.Page);
    }

    [Fact]
    public void NoPlacedDeathIsSaidPlainly()
    {
        Assert.Equal("I have no record of where you died.", AstrometricsCapability.DescribeDeaths([]));
    }
}
