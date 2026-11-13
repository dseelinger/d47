using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AskingForExobiologyByRegionIsAnsweredWithoutAModelTests
{
    [Fact]
    public void ThePhraseReachesTheToolWithNoModel()
    {
        var registry = CapabilityRegistry.Build([AstrometricsCapability.Create(() => null)]);
        var match = new KeywordRouter(registry).MatchToolCommand("show exobiology by region");

        Assert.NotNull(match);
        Assert.Equal(AstrometricsCapability.Tool, match.ToolName);
        Assert.True(match.Arguments.TryGetString("chart", out var chart));
        Assert.Equal(AstrometricsCapability.ExobiologyChart, chart);
    }

    [Fact]
    public async Task TheToolAddsTheLayerAndNamesTheFewestAndTheMost()
    {
        var counts = ExobiologyCatalogue.SpeciesPerRegion();
        var registry = CapabilityRegistry.Build([AstrometricsCapability.Create(() => null)]);

        var result = await registry.InvokeAsync(
            AstrometricsCapability.Tool,
            new ToolArguments(new Dictionary<string, string> { ["chart"] = "exobiology" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.StartsWith($"The catalogue lists species in {counts.Count} regions, from {counts.Values.Min()} in ", result.Content);
        Assert.EndsWith($"to {counts.Values.Max()} in {GalacticRegions.NameOf(counts.MaxBy(pair => pair.Value).Key)}.", result.Content);
        Assert.Equal(PageRef.StarChart(StarChartKind.Exobiology), result.Page);
    }

    [Fact]
    public void WithAPositionTheAnswerAddsTheCountForTheCommandersRegion()
    {
        var id = GalacticRegions.IdOf("Inner Orion Spur")!.Value;
        var here = GalacticRegions.LabelAt(id)!.Value;

        Assert.EndsWith(
            $" Here in Inner Orion Spur it lists {ExobiologyCatalogue.SpeciesPerRegion()[id]}.",
            AstrometricsCapability.DescribeExobiology(here));
    }
}
