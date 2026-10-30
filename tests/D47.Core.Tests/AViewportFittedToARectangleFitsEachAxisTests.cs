using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

public class AViewportFittedToARectangleFitsEachAxisTests
{
    private const double Inset = 20;

    private static StarPosition At(double x, double z) => new(x, 0, z);

    [Fact]
    public void ARouteWiderThanTallFillsTheWidth()
    {
        var view = GalaxyViewport.FittedIn([At(0, 0), At(100, 10)], 800, 200, Inset)!;
        var left = view.ToScreen(At(0, 0), 800, 200);
        var right = view.ToScreen(At(100, 10), 800, 200);

        Assert.Equal(Inset, left.X, 6);
        Assert.Equal(800 - Inset, right.X, 6);
    }

    [Fact]
    public void ARouteTallerThanWideFillsTheHeight()
    {
        var view = GalaxyViewport.FittedIn([At(0, 0), At(10, 100)], 800, 200, Inset)!;
        var bottom = view.ToScreen(At(0, 0), 800, 200);
        var top = view.ToScreen(At(10, 100), 800, 200);

        Assert.Equal(200 - Inset, bottom.Y, 6);
        Assert.Equal(Inset, top.Y, 6);
    }

    [Fact]
    public void ASinglePointIsCentred()
    {
        var view = GalaxyViewport.FittedIn([At(50, 70)], 800, 200, Inset)!;
        var at = view.ToScreen(At(50, 70), 800, 200);

        Assert.Equal(400, at.X, 6);
        Assert.Equal(100, at.Y, 6);
    }

    [Fact]
    public void NoPointsOrNoRoomGivesNothing()
    {
        Assert.Null(GalaxyViewport.FittedIn([], 800, 200, Inset));
        Assert.Null(GalaxyViewport.FittedIn([At(0, 0)], 800, 40, Inset));
    }
}
