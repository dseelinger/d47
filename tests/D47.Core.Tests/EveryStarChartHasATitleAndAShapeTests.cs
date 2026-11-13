using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public class EveryStarChartHasATitleAndAShapeTests
{
    [Fact]
    public void EveryKindHasATitleAndAShape()
    {
        foreach (var kind in Enum.GetValues<StarChartKind>())
        {
            var info = StarCharts.Info(kind);

            Assert.False(string.IsNullOrWhiteSpace(info.Title), kind.ToString());
            Assert.True(Enum.IsDefined(info.Shape), kind.ToString());
        }
    }

    [Fact]
    public void TheKindsAreListedInSwitchOrder()
    {
        Assert.Equal(
            ["Travels", "Route", "Regions", "Engineers", "Bookmarks", "Carrier", "Ships", "Unvisited"],
            StarCharts.All.Select(kind => StarCharts.Info(kind).Title));
    }

    [Fact]
    public void RouteIsALineRegionsAnAreaAndTheRestPoints()
    {
        Assert.Equal(StarChartShape.Line, StarCharts.Info(StarChartKind.Route).Shape);
        Assert.Equal(StarChartShape.Area, StarCharts.Info(StarChartKind.Regions).Shape);
        Assert.All(
            StarCharts.All.Where(kind => kind is not (StarChartKind.Route or StarChartKind.Regions)),
            kind => Assert.Equal(StarChartShape.Points, StarCharts.Info(kind).Shape));
    }

    [Fact]
    public void OnlyTheKindsThatAreMarksHaveAWordForOne()
    {
        Assert.Equal("Engineer", StarCharts.Info(StarChartKind.Engineers).Mark);
        Assert.Equal("Bookmark", StarCharts.Info(StarChartKind.Bookmarks).Mark);
        Assert.Equal("Fleet carrier", StarCharts.Info(StarChartKind.Carrier).Mark);
        Assert.Equal("Ship", StarCharts.Info(StarChartKind.Ships).Mark);
        Assert.All(
            new[] { StarChartKind.Travels, StarChartKind.Route, StarChartKind.Regions, StarChartKind.Unvisited },
            kind => Assert.Null(StarCharts.Info(kind).Mark));
    }

    [Fact]
    public void YourChartsAndTheGalaxysChartsAreGroupedApart()
    {
        Assert.Equal(
            [StarChartKind.Travels, StarChartKind.Route, StarChartKind.Bookmarks, StarChartKind.Carrier, StarChartKind.Ships],
            StarCharts.All.Where(kind => StarCharts.Info(kind).Group == StarCharts.Yours));
        Assert.Equal(
            [StarChartKind.Regions, StarChartKind.Engineers, StarChartKind.Unvisited],
            StarCharts.All.Where(kind => StarCharts.Info(kind).Group == StarCharts.Galaxy));
    }
}
