using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public class AFittedViewportKeepsTheRoutesShapeTests
{
    private const double Side = 100;
    private const double Inset = 10;

    private static StarPosition At(double x, double z, double y = 0) => new(x, y, z);

    private static RouteMapStop Stop(double x, double z) => new("S", At(x, z), RouteStopMarks.None);

    private static RouteMapStop NoPosition() => new("N", null, RouteStopMarks.None);

    private static (double X, double Y) Screen(GalaxyViewport view, StarPosition at) => view.ToScreen(at, Side, Side);

    private static void AssertNear((double X, double Y) expected, (double X, double Y) actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
    }

    [Fact]
    public void TheLongerSideFillsTheSquareAndTheOtherIsCentred()
    {
        var points = new[] { At(0, 0), At(100, 0), At(100, 50) };
        var view = GalaxyViewport.Fitted(points, Side, Inset)!;

        AssertNear((10, 50 + 20), Screen(view, points[0]));
        AssertNear((90, 50 + 20), Screen(view, points[1]));
        AssertNear((90, 50 - 20), Screen(view, points[2]));
    }

    [Fact]
    public void HeightIsIgnored()
    {
        var flat = GalaxyViewport.Fitted([At(0, 0), At(40, 20)], Side, Inset);
        var tall = GalaxyViewport.Fitted([At(0, 0, 500), At(40, 20, -900)], Side, Inset);

        Assert.Equal(flat, tall);
    }

    [Fact]
    public void ARouteUnderALightYearDrawsAsAClusterInTheMiddle()
    {
        var points = new[] { At(5, 0), At(5, 0.5) };
        var view = GalaxyViewport.Fitted(points, Side, Inset)!;

        Assert.All(points, point =>
        {
            var (x, y) = Screen(view, point);
            Assert.True(double.IsFinite(x) && double.IsFinite(y));
        });
        AssertNear((50, 50 + 20), Screen(view, points[0]));
        AssertNear((50, 50 - 20), Screen(view, points[1]));
    }

    [Fact]
    public void GalacticPlusZIsUp()
    {
        var points = new[] { At(0, 0), At(0, 100) };
        var view = GalaxyViewport.Fitted(points, Side, Inset)!;

        Assert.True(Screen(view, points[1]).Y < Screen(view, points[0]).Y);
    }

    [Fact]
    public void FewerThanTwoPointsGiveNoViewport()
    {
        Assert.Null(GalaxyViewport.Fitted([], Side, Inset));
        Assert.Null(GalaxyViewport.Fitted([At(1, 1)], Side, Inset));
    }

    [Fact]
    public void NoRoomInsideTheInsetGivesNoViewport()
    {
        var points = new[] { At(0, 0), At(10, 10) };

        Assert.Null(GalaxyViewport.Fitted(points, 20, 10));
        Assert.Null(GalaxyViewport.Fitted(points, 20, 11));
        Assert.NotNull(GalaxyViewport.Fitted(points, 21, 10));
    }

    [Fact]
    public void TheViewIsNeitherClampedToTheGalaxyNorSnappedToAStep()
    {
        var far = GalacticRegions.Extent.X1 + 5_000;
        var view = GalaxyViewport.Fitted([At(far, 0), At(far + 30, 0)], Side, Inset)!;

        Assert.Equal(far + 15, view.CentreX, 6);
        Assert.Equal(37.5, view.LightYearsAcross, 6);
    }

    [Fact]
    public void TheCommanderOffTheRouteIsMarkedOnlyInsideItsExtent()
    {
        var stops = new[] { Stop(0, 0), Stop(100, 100) };

        Assert.Equal(At(50, 50), RouteMapStop.CommanderMark(stops, -1, At(50, 50)));
        Assert.Null(RouteMapStop.CommanderMark(stops, -1, At(150, 50)));
        Assert.Null(RouteMapStop.CommanderMark(stops, -1, null));
    }

    [Fact]
    public void TheCommanderOnTheRouteIsMarkedAtTheStopReached()
    {
        var stops = new[] { Stop(0, 0), Stop(100, 100) };

        Assert.Equal(At(100, 100), RouteMapStop.CommanderMark(stops, 1, At(0, 0)));
        Assert.Equal(At(0, 0), RouteMapStop.CommanderMark(stops, 0, At(900, 900)));
        Assert.Equal(At(100, 100), RouteMapStop.CommanderMark(stops, 1, null));
    }

    [Fact]
    public void AStopReachedWithoutAPositionOrOutOfRangeGivesNoMark()
    {
        var stops = new[] { Stop(0, 0), NoPosition(), Stop(100, 100) };

        Assert.Null(RouteMapStop.CommanderMark(stops, 1, At(50, 50)));
        Assert.Null(RouteMapStop.CommanderMark(stops, 3, At(50, 50)));
        Assert.Null(RouteMapStop.CommanderMark(stops, -2, At(50, 50)));
    }

    [Fact]
    public void FewerThanTwoPositionedStopsGiveNoMark()
    {
        Assert.Null(RouteMapStop.CommanderMark([], -1, At(0, 0)));
        Assert.Null(RouteMapStop.CommanderMark([Stop(0, 0), NoPosition()], 0, At(0, 0)));
    }
}
