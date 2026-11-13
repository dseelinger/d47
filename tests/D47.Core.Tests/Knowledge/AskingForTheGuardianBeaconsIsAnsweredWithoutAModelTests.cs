using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Persona;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AskingForTheGuardianBeaconsIsAnsweredWithoutAModelTests
{
    private static CapabilityRegistry Registry() => CapabilityRegistry.Build([AstrometricsCapability.Create(() => null)]);

    [Theory]
    [InlineData("show me all known Guardian beacons")]
    [InlineData("show the Guardian beacons")]
    [InlineData("where are the Guardian beacons")]
    [InlineData("Guardian beacons on the map")]
    public void EachPhraseReachesTheToolWithNoModel(string said)
    {
        var match = new KeywordRouter(Registry()).MatchToolCommand(said);

        Assert.NotNull(match);
        Assert.Equal(AstrometricsCapability.Tool, match.ToolName);
        Assert.True(match.Arguments.TryGetString("chart", out var chart));
        Assert.Equal(AstrometricsCapability.GuardianBeacons, chart);
    }

    [Fact]
    public void TheToolIsNotProtected()
    {
        var tool = Assert.Single(Registry().Find(AstrometricsCapability.Id)!.Descriptor.Tools);

        Assert.False(tool.Protected);
    }

    [Fact]
    public async Task AnUnknownPositionGivesTheCountAndNoNearestSystem()
    {
        var result = await Registry().InvokeAsync(
            AstrometricsCapability.Tool,
            new ToolArguments(new Dictionary<string, string> { ["chart"] = "guardian_beacons" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal($"I know {StarCharts.Systems(StarChartKind.GuardianBeacons).Count} Guardian beacon systems.", result.Content);
        Assert.Equal(PageRef.StarChart(StarChartKind.GuardianBeacons), result.Page);
    }

    [Fact]
    public void AKnownPositionNamesTheNearestBeaconAndItsDistance()
    {
        var (address, name) = GuardianCores.NearestBeacon(StarPosition.Origin);
        var distance = GuardianCores.BeaconPositions[address].DistanceTo(StarPosition.Origin);

        var said = AstrometricsCapability.DescribeBeacons(StarPosition.Origin);

        Assert.Contains($"The nearest is {name}, ", said, StringComparison.Ordinal);
        Assert.Contains($"{distance:0.#} light years", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABadChartIsRefused()
    {
        var result = await Registry().InvokeAsync(
            AstrometricsCapability.Tool,
            new ToolArguments(new Dictionary<string, string> { ["chart"] = "nebulae" }),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Null(result.Page);
    }
}
