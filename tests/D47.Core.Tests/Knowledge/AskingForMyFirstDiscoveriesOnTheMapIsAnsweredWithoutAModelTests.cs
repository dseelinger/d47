using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AskingForMyFirstDiscoveriesOnTheMapIsAnsweredWithoutAModelTests
{
    [Theory]
    [InlineData("show my first discoveries")]
    [InlineData("first discoveries on the map")]
    public void ThePhrasesReachTheToolWithNoModel(string phrase)
    {
        var registry = CapabilityRegistry.Build([AstrometricsCapability.Create(() => null)]);
        var match = new KeywordRouter(registry).MatchToolCommand(phrase);

        Assert.NotNull(match);
        Assert.Equal(AstrometricsCapability.Tool, match.ToolName);
        Assert.True(match.Arguments.TryGetString("chart", out var chart));
        Assert.Equal(AstrometricsCapability.FirstDiscoveredChart, chart);
    }

    [Fact]
    public async Task TheToolAddsTheLayerAndCountsTheBodiesAndTheirSystems()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Test Commander"));
        IReadOnlyList<FirstDiscoveryPlace> places =
        [
            new(1, "Alpha", new StarPosition(0, 0, 0), 2),
            new(2, "Beta", new StarPosition(1, 0, 0), 3),
        ];
        var registry = CapabilityRegistry.Build([AstrometricsCapability.Create(() => state, firstDiscoveries: fid => fid == "F1" ? places : [])]);

        var result = await registry.InvokeAsync(
            AstrometricsCapability.Tool,
            new ToolArguments(new Dictionary<string, string> { ["chart"] = "first_discovered" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("You were the first to discover 5 bodies in 2 systems.", result.Content);
        Assert.Equal(PageRef.StarChart(StarChartKind.FirstDiscovered), result.Page);
    }

    [Fact]
    public void NoFirstDiscoveryIsSaidPlainly()
    {
        Assert.Equal("I have no record of a body you were the first to discover.", AstrometricsCapability.DescribeFirstDiscoveries([]));
    }
}
