using D47.Core.Knowledge;
using D47.Core.Persona;
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
            ["Travels", "Route", "Regions", "Engineers", "Bookmarks", "Carrier", "Ships", "Unvisited", "Guardian beacons", "Farming sites"],
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
        Assert.Equal("Guardian beacon", StarCharts.Info(StarChartKind.GuardianBeacons).Mark);
        Assert.Equal("Farming site", StarCharts.Info(StarChartKind.FarmingSites).Mark);
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
            [StarChartKind.Regions, StarChartKind.Engineers, StarChartKind.Unvisited, StarChartKind.FarmingSites],
            StarCharts.All.Where(kind => StarCharts.Info(kind).Group == StarCharts.Galaxy));
        Assert.Equal(
            [StarChartKind.GuardianBeacons],
            StarCharts.All.Where(kind => StarCharts.Info(kind).Group == StarCharts.Guardian));
    }

    [Fact]
    public void TheFixedChartsAreOffAMapWithNothingSaved()
    {
        Assert.Equal([StarChartKind.GuardianBeacons, StarChartKind.FarmingSites], StarCharts.All.Where(StarCharts.IsFixed));
        Assert.Equal(StarCharts.All.Where(kind => !StarCharts.IsFixed(kind)), StarCharts.Default);
    }

    [Fact]
    public void TheGuardianBeaconsChartIsEveryKnownBeaconSystemWithItsAddressAndPosition()
    {
        var systems = StarCharts.Systems(StarChartKind.GuardianBeacons);

        Assert.Equal(28, systems.Count);
        Assert.All(systems, system =>
        {
            Assert.Equal(GuardianCores.Beacons[system.Address!.Value], system.Name);
            Assert.Equal(GuardianCores.BeaconPositions[system.Address!.Value], system.At);
        });
    }

    [Fact]
    public void TheFarmingSitesChartIsOneSystemForEachSystemInTheFarmingTable()
    {
        var systems = StarCharts.Systems(StarChartKind.FarmingSites);
        var expected = FarmingSites.All.Select(site => site.System).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        Assert.NotEmpty(systems);
        Assert.Equal(expected.Count, systems.Count);
        Assert.Equal(expected.Order(StringComparer.Ordinal), systems.Select(system => system.Name));
        Assert.All(systems, system => Assert.Equal(FarmingSites.All.First(site => site.System == system.Name).Position, system.At));
        Assert.Contains(FarmingSites.All, site => site.IsAlternate && systems.Any(system => system.Name == site.System));
    }

    [Fact]
    public void AFarmingSiteIsLabelledWithItsMaterialMethodAndBody()
    {
        var site = FarmingSites.All[0];

        Assert.Equal("Adaptive Encryptors Capture by Jameson crash site on 1 b", site.Label);
    }

    [Fact]
    public void AChartThatIsNotFixedHasNoSystems()
    {
        Assert.All(StarCharts.Default, kind => Assert.Empty(StarCharts.Systems(kind)));
    }
}
