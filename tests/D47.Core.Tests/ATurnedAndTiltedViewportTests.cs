using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public class ATurnedAndTiltedViewportTests
{
    private static StarPosition At(double x, double z, double y = 0) => new(x, y, z);

    [Fact]
    public void AtTiltNinetyTheHigherOfTwoPositionsDrawsHigherAtTheSameX()
    {
        var view = new GalaxyViewport(0, 0, 400) { Tilt = 90 };
        var low = view.ToScreen(At(30, 40), 500, 500);
        var high = view.ToScreen(At(30, 40, 50), 500, 500);

        Assert.Equal(low.X, high.X, 6);
        Assert.True(high.Y < low.Y);
    }

    [Fact]
    public void AfterSixClockwiseTurnsPositiveZDrawsToTheRight()
    {
        var view = new GalaxyViewport(0, 0, 400);

        for (var i = 0; i < 6; i++)
        {
            view = view.Turned(1);
        }

        Assert.Equal(270, view.Heading, 6);
        Assert.True(view.ToScreen(At(0, 50), 500, 500).X > 250);
    }

    [Fact]
    public void TiltStopsAtZeroAndNinety()
    {
        var view = new GalaxyViewport(0, 0, 400);

        Assert.Equal(0, view.Tilted(-1).Tilt);

        for (var i = 0; i < 8; i++)
        {
            view = view.Tilted(1);
        }

        Assert.Equal(90, view.Tilt);
    }

    [Fact]
    public void HeadingWrapsInBothDirections()
    {
        var view = new GalaxyViewport(0, 0, 400);

        Assert.Equal(345, view.Turned(1).Heading, 6);
        Assert.Equal(0, (view with { Heading = 345 }).Turned(-1).Heading, 6);
        Assert.Equal(10, view.Rotated(-370, 0).Heading, 6);
    }

    [Fact]
    public void AStepFromBetweenMultiplesSnapsToTheNextOneInThatDirection()
    {
        var view = new GalaxyViewport(0, 0, 400) { Heading = 7, Tilt = 7 };

        Assert.Equal(0, view.Turned(1).Heading, 6);
        Assert.Equal(15, view.Turned(-1).Heading, 6);
        Assert.Equal(15, view.Tilted(1).Tilt, 6);
        Assert.Equal(0, view.Tilted(-1).Tilt, 6);
    }

    [Fact]
    public void RotatingClampsTheTilt()
    {
        var view = new GalaxyViewport(0, 0, 400).Rotated(0, 200);

        Assert.Equal(90, view.Tilt);
        Assert.Equal(0, view.Rotated(0, -500).Tilt);
    }

    [Fact]
    public void AtTiltNinetyPanningUpMovesTheCentreHeightOnly()
    {
        var view = new GalaxyViewport(10, 20, 400) { Tilt = 90 }.Pan(0, 1);

        Assert.Equal(100, view.CentreY, 6);
        Assert.Equal(10, view.CentreX, 6);
        Assert.Equal(20, view.CentreZ, 6);
    }

    [Fact]
    public void AtRestADragMovesTheCentreAgainstThePointer()
    {
        var view = new GalaxyViewport(0, 0, 400).Dragged(50, 25, 500, 500);

        Assert.Equal(-40, view.CentreX, 6);
        Assert.Equal(20, view.CentreZ, 6);
        Assert.Equal(0, view.CentreY);
    }

    [Fact]
    public void AtHeadingNinetyADragToTheRightMovesTheCentreAlongMinusR()
    {
        var view = new GalaxyViewport(0, 0, 400) { Heading = 90 }.Dragged(50, 0, 500, 500);

        Assert.Equal(0, view.CentreX, 6);
        Assert.Equal(40, view.CentreZ, 6);
    }

    [Fact]
    public void AtTiltNinetyFittingPicksItsStepFromTheHeightSpread()
    {
        var points = new[] { At(0, 0, 0), At(0, 0, 300) };

        Assert.Equal(100, GalaxyViewport.Fitting(points)!.LightYearsAcross);
        Assert.Equal(400, GalaxyViewport.Fitting(points, 0, 90)!.LightYearsAcross);
    }

    [Fact]
    public void FittingCentresOnTheMiddleOfTheExtentAsSeen()
    {
        var view = GalaxyViewport.Fitting([At(0, 0, 0), At(0, 0, 300)], 0, 90)!;
        var middle = view.Face(At(0, 0, 150));
        var centre = view.Face(new StarPosition(view.CentreX, view.CentreY, view.CentreZ));

        Assert.Equal(middle.Up, centre.Up, 6);
        Assert.Equal(90, view.Tilt);
    }

    [Fact]
    public void TheHeightOfTheCentreIsKeptNearThePlane()
    {
        var view = new GalaxyViewport(0, 0, 400) { CentreY = 1_000_000 }.Pan(0, 0);
        var extent = GalacticRegions.Extent;

        Assert.Equal((extent.X1 - extent.X0) / 2, view.CentreY, 6);
    }
}
