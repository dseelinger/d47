using D47.Core.Journal;
using Xunit;

namespace D47.Core.Tests.Journal;

public class TheDepartureSystemIsNeverAheadOnTheRouteTests
{
    private static readonly NavRoute Plotted = new()
    {
        Hops = [new RouteHop("Sol", "G"), new RouteHop("Alpha Centauri", "G"), new RouteHop("Barnard's Star", "M")],
    };

    private static string[] Names(string? current) => [.. Plotted.Ahead(current).Select(hop => hop.StarSystem)];

    [Fact]
    public void OffTheRouteNothingIsAhead()
    {
        Assert.Empty(Plotted.Ahead("Shinrarta Dezhra"));
        Assert.Empty(Plotted.Ahead(null));
    }

    [Fact]
    public void OnTheDepartureSystemEveryOtherHopIsAhead() =>
        Assert.Equal(["Alpha Centauri", "Barnard's Star"], Names("Sol"));

    [Fact]
    public void OnAMiddleHopOnlyTheLaterHopsAreAhead() =>
        Assert.Equal(["Barnard's Star"], Names("alpha centauri"));

    [Fact]
    public void OnTheLastHopNothingIsAheadAndTheRouteIsAtItsEnd()
    {
        Assert.Empty(Plotted.Ahead("Barnard's Star"));
        Assert.True(Plotted.IsAtEnd("Barnard's Star"));
        Assert.False(Plotted.IsAtEnd("Sol"));
        Assert.False(Plotted.IsAtEnd("Shinrarta Dezhra"));
        Assert.False(NavRoute.None.IsAtEnd("Sol"));
    }
}
