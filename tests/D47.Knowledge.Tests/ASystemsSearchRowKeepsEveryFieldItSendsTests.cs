using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public sealed class ASystemsSearchRowKeepsEveryFieldItSendsTests
{
    private static SystemSummary Row(string name)
    {
        using var document = Fixture.Json("spansh-systems-search-sol-full.json");
        return Assert.Single(SpanshResponse.ReadSearch(document).Systems, system => system.Name == name);
    }

    [Fact]
    public void ARowKeepsItsEconomyPermitColonisationAndPowerFields()
    {
        var sol = Row("Sol");

        Assert.Equal("Service", sol.SecondaryEconomy);
        Assert.Equal("Inner Orion Spur", sol.Region);
        Assert.True(sol.KnownPermit);
        Assert.False(sol.IsColonised);
        Assert.Equal(40, sol.BodyCount);
        Assert.Equal(605861, sol.ScanValue);
        Assert.Equal("None", sol.ControllingFactionState);
        Assert.Equal(0.583561, sol.PowerControlProgress!.Value, 6);
        Assert.Equal(138137, sol.Reinforcement);
        Assert.Equal(166576, sol.Undermining);
        Assert.Equal(6, sol.Powers.Count);
        Assert.Equal("None", sol.ThargoidWarState);
        Assert.True(Row("Lalande 21185").IsBeingColonised);
    }

    [Fact]
    public void ARowKeepsItsFactionStates()
    {
        var faction = Row("Groombridge 34").Factions.Single(presence => presence.Name == "Equestrian Naval Fleet");

        Assert.Equal("Independent", faction.Allegiance);
        Assert.Equal(["Boom", "Civil Liberty"], faction.ActiveStates);
        Assert.Equal(["Expansion"], faction.PendingStates);
        Assert.Equal(["Expansion"], Row("Lalande 21185").Factions[0].RecoveringStates);
    }

    [Fact]
    public void ANestedStationKeepsItsMarketFlagAndItsSystem()
    {
        var sol = Row("Sol");
        var daedalus = Assert.Single(sol.Stations, station => station.Name == "Daedalus");

        Assert.True(daedalus.HasMarket);
        Assert.Equal("Orbis Starport", daedalus.Type);
        Assert.Equal(128016384, daedalus.MarketId);
        Assert.Equal(6, daedalus.LargePads);
        Assert.Equal("Mother Gaia", daedalus.ControllingFaction);
        Assert.Contains("Universal Cartographics", daedalus.Services);
        Assert.Equal("Sol", daedalus.SystemName);
        Assert.Equal(sol.SystemAddress, daedalus.SystemAddress);
        Assert.Equal("Jerome Archer", daedalus.ControllingPower);
        Assert.Equal("Stronghold", daedalus.PowerState);
    }

    [Fact]
    public void ANestedBodyKeepsItsSubtypeScanValueAndLandmarks()
    {
        var mercury = Assert.Single(Row("Sol").Bodies, body => body.Name == "Mercury");

        Assert.Equal("Metal-rich body", mercury.Subtype);
        Assert.Equal("Planet", mercury.Type);
        Assert.Equal(28693, mercury.ScanValue);
        Assert.Equal(124339, mercury.MappingValue);
        Assert.Equal("Not terraformable", mercury.TerraformingState);
        Assert.Equal(36028807496337771, mercury.BodyAddress);
        Assert.Equal("Sol", mercury.SystemName);
        Assert.Equal(2, mercury.Landmarks.Single(landmark => landmark.Subtype == "Crater Outpost").Count);
        Assert.True(Assert.Single(Row("Sol").Bodies, body => body.Name == "Sol").IsMainStar);
    }
}
