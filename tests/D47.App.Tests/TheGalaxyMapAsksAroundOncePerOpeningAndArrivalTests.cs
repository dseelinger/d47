using D47.App.Panel;
using Xunit;

namespace D47.App.Tests;

/// <summary>The galaxy map asks for the systems around the Commander once per opening and arrival, and only while it can draw them.</summary>
public class TheGalaxyMapAsksAroundOncePerOpeningAndArrivalTests
{
    private static NearbyAsking Opened()
    {
        var asking = new NearbyAsking();
        asking.Opened();

        return asking;
    }

    [Fact]
    public void AClosedPageAsksNothing()
    {
        var asking = new NearbyAsking();

        Assert.False(asking.Ask("Sol", galaxy: true, wanted: true));

        asking.Opened();
        asking.Closed();

        Assert.False(asking.Ask("Sol", galaxy: true, wanted: true));
    }

    [Fact]
    public void AnOpenedPageAsksOnceInTheSameSystem()
    {
        var asking = Opened();

        Assert.True(asking.Ask("Sol", galaxy: true, wanted: true));
        Assert.False(asking.Ask("sol", galaxy: true, wanted: true));
    }

    [Fact]
    public void AnArrivalAsksAgainAndReturningAsksAgain()
    {
        var asking = Opened();

        Assert.True(asking.Ask("Sol", galaxy: true, wanted: true));
        Assert.True(asking.Ask("Achenar", galaxy: true, wanted: true));
        Assert.False(asking.Ask("Achenar", galaxy: true, wanted: true));
        Assert.True(asking.Ask("Sol", galaxy: true, wanted: true));
    }

    [Fact]
    public void ReopeningAsksAgainInTheSameSystem()
    {
        var asking = Opened();

        Assert.True(asking.Ask("Sol", galaxy: true, wanted: true));

        asking.Closed();
        asking.Opened();

        Assert.True(asking.Ask("Sol", galaxy: true, wanted: true));
    }

    [Fact]
    public void NothingIsAskedWithUnvisitedOffWithNoGalaxySearchOrWithNoSystem()
    {
        Assert.False(Opened().Ask("Sol", galaxy: true, wanted: false));
        Assert.False(Opened().Ask("Sol", galaxy: false, wanted: true));
        Assert.False(Opened().Ask(null, galaxy: true, wanted: true));
    }

    [Fact]
    public void AnArrivalWithUnvisitedOffIsNotAskedForUntilUnvisitedIsTurnedBackOn()
    {
        var asking = Opened();

        Assert.True(asking.Ask("Sol", galaxy: true, wanted: true));
        Assert.False(asking.Ask("Achenar", galaxy: true, wanted: false));
        Assert.False(asking.Ask("Achenar", galaxy: true, wanted: true));

        asking.Forget();

        Assert.True(asking.Ask("Achenar", galaxy: true, wanted: true));
    }
}
