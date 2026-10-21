using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public class GalaxyViewportTests
{
    private static StarPosition At(double x, double z, double y = 0) => new(x, y, z);

    [Fact]
    public void AProjectedPositionReturnsToTheSameGalacticXAndZ()
    {
        var view = new GalaxyViewport(100, -200, 400);
        var screen = view.ToScreen(At(150, -120), 800, 600);
        var back = view.ToGalaxy(screen.X, screen.Y, 800, 600);

        Assert.Equal(150, back.X, 6);
        Assert.Equal(-120, back.Z, 6);
    }

    [Fact]
    public void HeightDoesNotMoveAPositionOnTheScreen()
    {
        var view = new GalaxyViewport(0, 0, 400);

        Assert.Equal(view.ToScreen(At(10, 20, 0), 500, 500), view.ToScreen(At(10, 20, 900), 500, 500));
    }

    [Fact]
    public void TheCentreIsTheMiddleOfTheAreaAndHigherZIsHigherUp()
    {
        var view = new GalaxyViewport(0, 0, 400);

        Assert.Equal((250.0, 250.0), view.ToScreen(At(0, 0), 500, 500));
        Assert.True(view.ToScreen(At(0, 50), 500, 500).Y < 250);
    }

    [Fact]
    public void TheShorterSideSetsTheScaleInAWideArea()
    {
        var view = new GalaxyViewport(0, 0, 400);

        Assert.Equal(300 + 100, view.ToScreen(At(200, 0), 600, 200).X, 6);
    }

    [Fact]
    public void TheShorterSideSetsTheScaleInATallArea()
    {
        var view = new GalaxyViewport(0, 0, 400);

        Assert.Equal(300 - 100, view.ToScreen(At(0, 200), 200, 600).Y, 6);
    }

    [Fact]
    public void ZoomingInStopsAtOneHundredLightYears()
    {
        var view = GalaxyViewport.Around(At(0, 0)).ZoomIn().ZoomIn().ZoomIn();

        Assert.Equal(100, view.LightYearsAcross);
    }

    [Fact]
    public void ZoomingOutStopsAtTheWholeGalaxy()
    {
        var view = GalaxyViewport.WholeGalaxy.ZoomOut();

        Assert.Equal(102_400, view.LightYearsAcross);
    }

    [Fact]
    public void ZoomingMovesOneStepAndKeepsTheCentre()
    {
        var view = new GalaxyViewport(10, 20, 400);

        Assert.Equal(new GalaxyViewport(10, 20, 1_600), view.ZoomOut());
        Assert.Equal(new GalaxyViewport(10, 20, 100), view.ZoomIn());
    }

    [Fact]
    public void AStepIsFourTimesTheLast()
    {
        var view = GalaxyViewport.WholeGalaxy;
        var sizes = new List<double> { view.LightYearsAcross };

        for (var i = 0; i < 5; i++)
        {
            view = view.ZoomIn();
            sizes.Add(view.LightYearsAcross);
        }

        Assert.Equal([102_400.0, 25_600, 6_400, 1_600, 400, 100], sizes);
    }

    [Fact]
    public void WholeGalaxyIsCentredOnTheRegionGrid()
    {
        var extent = GalacticRegions.Extent;
        var view = GalaxyViewport.WholeGalaxy;

        Assert.Equal((extent.X0 + extent.X1) / 2, view.CentreX, 6);
        Assert.Equal((extent.Z0 + extent.Z1) / 2, view.CentreZ, 6);
    }

    [Fact]
    public void APanMovesTheCentreAQuarterOfTheWidth()
    {
        var view = new GalaxyViewport(0, 0, 400);

        Assert.Equal(new GalaxyViewport(100, 100, 400), view.Pan(1, 1));
        Assert.Equal(new GalaxyViewport(-100, 0, 400), view.Pan(-1, 0));
        Assert.Equal(new GalaxyViewport(0, -100, 400), view.Pan(0, -1));
    }

    [Fact]
    public void APanStopsAtTheEdgeOfTheRegionGrid()
    {
        var extent = GalacticRegions.Extent;
        var view = new GalaxyViewport(extent.X1 - 10, extent.Z0 + 10, 400).Pan(1, -1);

        Assert.Equal(extent.X1, view.CentreX);
        Assert.Equal(extent.Z0, view.CentreZ);
    }

    [Fact]
    public void AroundCentresOnThePositionAtFourHundredLightYears()
    {
        Assert.Equal(new GalaxyViewport(25, -40, 400), GalaxyViewport.Around(At(25, -40, 5)));
    }

    [Fact]
    public void FittingOnePointCentresOnItAtTheSmallestStep()
    {
        Assert.Equal(new GalaxyViewport(25, -40, 100), GalaxyViewport.Fitting([At(25, -40)]));
    }

    [Fact]
    public void FittingASpreadPicksTheSmallestStepWithAMarginAndCentresOnTheExtent()
    {
        var view = GalaxyViewport.Fitting([At(0, 0), At(300, 100)])!;

        Assert.Equal(new GalaxyViewport(150, 50, 400), view);
    }

    [Fact]
    public void FittingLeavesATenthOfTheWidthEachSide()
    {
        Assert.Equal(100, GalaxyViewport.Fitting([At(0, 0), At(80, 0)])!.LightYearsAcross);
        Assert.Equal(400, GalaxyViewport.Fitting([At(0, 0), At(81, 0)])!.LightYearsAcross);
    }

    [Fact]
    public void FittingPointsWiderThanTheGalaxyStepUsesTheGalaxyStep()
    {
        var view = GalaxyViewport.Fitting([At(-40_000, 0), At(40_000, 0), At(0, 20_000)])!;

        Assert.Equal(102_400, view.LightYearsAcross);
        Assert.Equal(0, view.CentreX, 6);
    }

    [Fact]
    public void FittingNoPointsGivesNothing()
    {
        Assert.Null(GalaxyViewport.Fitting([]));
    }
}

public class GalaxyMapHitTests
{
    private static readonly IReadOnlyList<(double X, double Y)> Points = [(10, 10), (100, 100)];

    [Fact]
    public void ATapInsideTheRadiusSelectsThePoint()
    {
        Assert.Equal(1, GalaxyMapHit.Nearest(Points, (103, 104), 6));
    }

    [Fact]
    public void ATapOutsideTheRadiusSelectsNothing()
    {
        Assert.Null(GalaxyMapHit.Nearest(Points, (50, 50), 6));
    }

    [Fact]
    public void ATapBetweenTwoPointsSelectsTheNearer()
    {
        Assert.Equal(0, GalaxyMapHit.Nearest([(0, 0), (10, 0)], (4, 0), 20));
    }

    [Fact]
    public void AnEqualDistanceGoesToTheLaterPoint()
    {
        Assert.Equal(1, GalaxyMapHit.Nearest([(0, 0), (10, 0)], (5, 0), 20));
    }
}
