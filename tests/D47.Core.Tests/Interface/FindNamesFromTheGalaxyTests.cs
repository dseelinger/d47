using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>Find ranks the system, station and faction names Spansh returns beside the local ones (#1973).</summary>
public class FindNamesFromTheGalaxyTests
{
    private static PanelFind Find()
    {
        var (_, settings, nav) = FindRanksPagesSettingsAndHelpTests.Build();
        settings.Replace("test", current => current with { Knowledge = current.Knowledge with { GalaxySearch = true } });

        return new PanelFind(nav, settings, new Dictionary<PanelTab, string>());
    }

    private static readonly StarPosition Sol = new(0, 0, 0);

    [Fact]
    public void AGalaxySystemReplacesTheLocalOneWithTheSameNameAndCarriesItsAddress()
    {
        var names = GalaxyFindNames.Of([new SystemNameMatch("Darkan", 42, Sol)], [], [], null);

        var darkan = Find().Search("darkan", names).Where(r => r.Title == "Darkan").ToList();

        var only = Assert.Single(darkan);
        Assert.Equal(42, only.SystemAddress);
    }

    [Fact]
    public void ALocalSystemTheGalaxyDidNotReturnIsStillListed()
    {
        var names = GalaxyFindNames.Of([new SystemNameMatch("Darkinu", 7, Sol)], [], [], null);

        var results = Find().Search("dark", names);

        Assert.Contains(results, r => r.Title == "Darkan" && r.SystemAddress is null);
        Assert.Contains(results, r => r.Title == "Darkinu" && r.SystemAddress == 7);
    }

    [Theory]
    [InlineData("BNH-T2F", true)]
    [InlineData("k2p-9z0", true)]
    [InlineData("BNH-T2", false)]
    [InlineData("BNH T2F", false)]
    [InlineData("Dark Wheel", false)]
    [InlineData("ABCD-123", false)]
    public void ACarrierCallsignIsThreeThenAHyphenThenThree(string name, bool carrier)
    {
        Assert.Equal(carrier, GalaxyFindNames.IsCarrierCallsign(name));
    }

    [Fact]
    public void ACallsignStationIsMarkedCarrierAndANamedOneStation()
    {
        var names = GalaxyFindNames.Of(
            [],
            [new StationNameMatch("BNH-T2F", 1, 10, Sol), new StationNameMatch("Dark Wheel", 2, 11, Sol)],
            [],
            null);

        Assert.Equal(PanelFindKind.Carrier, names.Single(n => n.Title == "BNH-T2F").Kind);
        Assert.Equal(PanelFindKind.Station, names.Single(n => n.Title == "Dark Wheel").Kind);
        Assert.All(names, n => Assert.Equal("Station", n.Path));
    }

    [Fact]
    public void AStationHasADistanceOnlyWhenTheCommandersPositionIsKnown()
    {
        var station = new StationNameMatch("Dark Wheel", 2, 11, new StarPosition(3, 4, 0));

        var near = GalaxyFindNames.Of([], [station], [], Sol).Single();
        var unknown = GalaxyFindNames.Of([], [station], [], null).Single();

        Assert.Equal(5, near.LightYears);
        Assert.Null(unknown.LightYears);
        Assert.Equal("Station", unknown.Path);
    }

    [Fact]
    public void AFactionIsAFactionWhoseTargetIsItsName()
    {
        var name = GalaxyFindNames.Of([], [], ["Dark Echo"], null).Single();

        Assert.Equal(PanelFindKind.Faction, name.Kind);
        Assert.Equal("Minor faction", name.Path);
        Assert.Equal("Dark Echo", name.Target);
    }

    [Fact]
    public void TheAddressAndDistanceSurviveRanking()
    {
        var names = GalaxyFindNames.Of([], [new StationNameMatch("Dark Wheel", 2, 11, new StarPosition(3, 4, 0))], [], Sol);

        var result = Find().Search("wheel", names).Single(r => r.Kind == PanelFindKind.Station);

        Assert.Equal(11, result.SystemAddress);
        Assert.Equal(5, result.LightYears);
    }
}
