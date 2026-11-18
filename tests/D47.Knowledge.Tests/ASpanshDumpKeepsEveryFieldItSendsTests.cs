using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public sealed class ASpanshDumpKeepsEveryFieldItSendsTests
{
    private static StarSystemProfile Sol()
    {
        using var document = Fixture.Json("spansh-dump-sol-full.json");
        return SpanshStarSystemService.ReadProfile(document.RootElement.GetProperty("system"), 0);
    }

    [Fact]
    public void ASystemKeepsItsBodyCount()
    {
        Assert.Equal(40, Sol().BodyCount);
    }

    [Fact]
    public void AStarKeepsItsBelt()
    {
        var belt = Assert.Single(Assert.Single(Sol().Bodies, body => body.Name == "Sol").Belts);

        Assert.Equal("Asteroid Belt", belt.Name);
        Assert.Equal("Rocky", belt.Type);
        Assert.Equal(308170000000.0, belt.InnerRadius);
        Assert.Equal(489190000000.0, belt.OuterRadius);
        Assert.Equal(4060500000000.0, belt.Mass);
    }

    [Fact]
    public void ARingKeepsItsRadiiAndMass()
    {
        var ring = Assert.Single(Assert.Single(Sol().Bodies, body => body.Name == "Jupiter").Rings);

        Assert.Equal(92000000.0, ring.InnerRadius);
        Assert.Equal(182000000.0, ring.OuterRadius);
        Assert.Equal(10000.0, ring.Mass);
    }

    [Fact]
    public void ASurfaceStationKeepsItsPosition()
    {
        var station = Assert.Single(Sol().Stations, found => found.Name == "Haberlandt Survey");

        Assert.Equal(4.171216, station.Latitude);
        Assert.Equal(45.113419, station.Longitude);
    }

    [Fact]
    public void ASystemKeepsItsControllingFactionDetail()
    {
        var sol = Sol();

        Assert.Equal("Federation", sol.ControllingFactionAllegiance);
        Assert.Equal("Democracy", sol.ControllingFactionGovernment);
        Assert.Equal(0.0, sol.ControllingFactionPendingStates["Expansion"]);
        Assert.NotNull(sol.PowersAt);
    }
}
