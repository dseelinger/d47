using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AskingForTheGuardianSitesIsAnsweredWithoutAModelTests
{
    private static CapabilityRegistry Registry() => CapabilityRegistry.Build([AstrometricsCapability.Create(() => null)]);

    [Theory]
    [InlineData("show the Guardian sites")]
    [InlineData("Guardian sites on the map")]
    [InlineData("where are the Guardian ruins")]
    public void EachPhraseReachesTheToolWithNoModel(string said)
    {
        var match = new KeywordRouter(Registry()).MatchToolCommand(said);

        Assert.NotNull(match);
        Assert.Equal(AstrometricsCapability.Tool, match.ToolName);
        Assert.True(match.Arguments.TryGetString("chart", out var chart));
        Assert.Equal(AstrometricsCapability.GuardianSitesChart, chart);
    }

    [Theory]
    [InlineData("where are the Guardian beacons")]
    [InlineData("show the Guardian beacons")]
    public void TheBeaconPhrasesStillReachTheBeacons(string said)
    {
        var match = new KeywordRouter(Registry()).MatchToolCommand(said);

        Assert.NotNull(match);
        Assert.True(match.Arguments.TryGetString("chart", out var chart));
        Assert.Equal(AstrometricsCapability.GuardianBeacons, chart);
    }

    [Fact]
    public async Task TheToolAddsTheLayerAndSaysHowManySystemsItKnows()
    {
        var result = await Registry().InvokeAsync(
            AstrometricsCapability.Tool,
            new ToolArguments(new Dictionary<string, string> { ["chart"] = "guardian_sites" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(
            $"I know {StarCharts.Systems(StarChartKind.GuardianSites).Count} systems with a Guardian ruin or structure.",
            result.Content);
        Assert.Equal(PageRef.StarChart(StarChartKind.GuardianSites), result.Page);
    }

    [Fact]
    public void AKnownPositionNamesTheNearestSystemAndItsDistance()
    {
        var here = new StarPosition(800, -150, -100);
        var said = AstrometricsCapability.DescribeGuardianSites(here);
        var nearest = StarCharts.Systems(StarChartKind.GuardianSites).MinBy(system => system.At.DistanceTo(here))!;

        Assert.EndsWith(
            $"The nearest is {nearest.Name}, {nearest.At.DistanceTo(here).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} light years away.",
            said,
            StringComparison.Ordinal);
    }
}
