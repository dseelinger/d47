using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AskingForTheFarmingSitesIsAnsweredWithoutAModelTests
{
    private static CapabilityRegistry Registry() => CapabilityRegistry.Build([AstrometricsCapability.Create(() => null)]);

    [Theory]
    [InlineData("show the farming sites")]
    [InlineData("material farming sites on the map")]
    public void EachPhraseReachesTheToolWithNoModel(string said)
    {
        var match = new KeywordRouter(Registry()).MatchToolCommand(said);

        Assert.NotNull(match);
        Assert.Equal(AstrometricsCapability.Tool, match.ToolName);
        Assert.True(match.Arguments.TryGetString("chart", out var chart));
        Assert.Equal(AstrometricsCapability.FarmingSitesChart, chart);
    }

    [Fact]
    public async Task TheToolAddsTheLayerAndSaysHowManySitesItKnows()
    {
        var result = await Registry().InvokeAsync(
            AstrometricsCapability.Tool,
            new ToolArguments(new Dictionary<string, string> { ["chart"] = "farming_sites" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(
            $"I know {FarmingSites.All.Count} material farming sites in {StarCharts.Systems(StarChartKind.FarmingSites).Count} systems.",
            result.Content);
        Assert.Equal(PageRef.StarChart(StarChartKind.FarmingSites), result.Page);
    }

    [Fact]
    public void AKnownPositionNamesTheNearestSystem()
    {
        var said = AstrometricsCapability.DescribeFarmingSites(StarPosition.Origin);
        var nearest = StarCharts.Systems(StarChartKind.FarmingSites).MinBy(system => system.At.DistanceTo(StarPosition.Origin))!;

        Assert.Contains($"The nearest is {nearest.Name}, ", said, StringComparison.Ordinal);
    }
}
