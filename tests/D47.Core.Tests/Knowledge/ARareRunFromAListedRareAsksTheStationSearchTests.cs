using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>The Rare goods page's row action and its answer on the search board (#2096).</summary>
public class ARareRunFromAListedRareAsksTheStationSearchTests
{
    private sealed class FakeIndex(RareIndex? index) : IRareIndexSource
    {
        public Task<RareIndex?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(index);
    }

    private static RareEntry Rare => RareCatalogue.All.First();

    [Fact]
    public void ARareRunSearchesStationsWithAMarketBeyondTwoHundredLightYearsInUnoccupiedSystemsOfThePower()
    {
        var arguments = RareRunSearch.StationArguments(Rare, "Li Yong-Rui");

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["near"] = Rare.System,
                ["min_distance"] = "200",
                ["power_state"] = "Unoccupied",
                ["services"] = "Market",
                ["acquiring_power"] = "Li Yong-Rui",
            },
            arguments);
    }

    [Fact]
    public void ARareRunWithNoPledgeLeavesThePowerOpen()
    {
        Assert.DoesNotContain("acquiring_power", RareRunSearch.StationArguments(Rare, null).Keys);
    }

    [Fact]
    public async Task AListOfRaresPostsEveryRareAndOpensTheRareGoodsPage()
    {
        var board = new GalaxySearchBoard();
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                null,
                () => "Nowhere",
                settings,
                searches: board,
                currentPosition: () => StarPosition.Origin,
                rareIndex: new FakeIndex(null)),
        ]);

        var result = await registry.InvokeAsync(
            "list_rares",
            new ToolArguments(new Dictionary<string, string> { ["max_distance"] = "99999", ["limit"] = "2", ["order_by"] = "name" }),
            CancellationToken.None,
            ToolCaller.Commander);

        Assert.False(result.IsError);
        Assert.Equal(PageRef.Search(GalaxySearchKind.Rares), result.Page);

        var posting = board.Last(GalaxySearchKind.Rares)!;
        var rows = ((RareSearchResult)posting.Result).Rows;

        Assert.Equal(RareCatalogue.All.Count, rows.Count);
        Assert.Equal("name", posting.Arguments["order_by"]);
        Assert.Equal(rows.OrderBy(row => row.Rare.Name, StringComparer.OrdinalIgnoreCase).Select(row => row.Rare.Name), rows.Select(row => row.Rare.Name));
    }
}
