using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary><c>list_rares</c> lists rare goods from the catalogue and the rare index (#2094).</summary>
public class AListOfRaresComesFromTheRareIndexTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeIndex(RareIndex? index) : IRareIndexSource
    {
        public Task<RareIndex?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(index);
    }

    private sealed class Galaxy(StarPosition? position) : StubGalaxyService, IGalaxyService
    {
        public List<string> Asked { get; } = [];

        public Task<StarPosition?> PositionAsync(string system, CancellationToken cancellationToken)
        {
            Asked.Add(system);

            return Task.FromResult(position);
        }
    }

    private static RareEntry Home => RareCatalogue.All.First();

    private static RareIndex Index(params string[] rows) =>
        RareIndex.Parse(RareIndex.Header + "\n" + string.Join("\n", rows) + "\n")!;

    private static string Row(string symbol, int? far, int? buy, int? stock) =>
        $"{symbol}\t{far}\t3\t{buy}\t{stock}\t2026-11-03T08:00:00Z";

    private static async Task<ToolResult> ListAsync(
        IRareIndexSource? source,
        Dictionary<string, string> arguments,
        IGalaxyService? galaxy = null)
    {
        var settings = TestSurface.For(new MemoryInstall()).Settings;

        var registry = CapabilityRegistry.Build(
        [
            GalaxyCapability.Create(
                galaxy,
                () => "Nowhere",
                settings,
                now: () => Now,
                currentPosition: () => StarPosition.Origin,
                rareIndex: source),
        ]);

        return await registry.InvokeAsync(
            "list_rares",
            new ToolArguments(arguments),
            TestContext.Current.CancellationToken);
    }

    private static string[] Names(IEnumerable<RareListingRow> rows) => [.. rows.Select(row => row.Rare.Name)];

    [Fact]
    public async Task RaresAreListedNearestFirstAndPagedOverTheWholeRange()
    {
        var expected = RareCatalogue.All
            .Select(rare => (rare.Name, Distance: Home.Position.DistanceTo(rare.Position)))
            .Where(rare => rare.Distance <= 100_000)
            .OrderBy(rare => rare.Distance)
            .Select(rare => rare.Name)
            .ToArray();

        var pages = new List<string>();

        for (var page = 1; page <= 3; page++)
        {
            var result = await ListAsync(
                new FakeIndex(Index()),
                new() { ["near"] = Home.System, ["max_distance"] = "100000", ["limit"] = "1", ["page"] = page.ToString() });

            Assert.False(result.IsError, result.Content);
            Assert.Contains($"Rares {page} to {page} of {expected.Length}", result.Content, StringComparison.Ordinal);
            pages.Add(result.Content);
        }

        for (var page = 0; page < 3; page++)
        {
            Assert.Contains(expected[page], pages[page], StringComparison.Ordinal);
        }

        Assert.Equal(Home.Name, expected[0]);
    }

    [Fact]
    public void PriceProfitAndTotalOrderTheWholeRangeWithUnindexedRaresLast()
    {
        var withFar = RareCatalogue.All.Where(rare => rare.FarSellPrice is not null).Take(4).ToArray();
        var unindexed = withFar[3];

        var index = Index(
            Row(withFar[0].Symbol, 1_000, 400, 10),
            Row(withFar[1].Symbol, 1_000, 100, 50),
            Row(withFar[2].Symbol, 1_000, 900, 90));

        var everywhere = RareListing.Within(Home.Position, 100_000, index, "price", descending: false);
        var byPrice = RareListing.Within(Home.Position, 100_000, index, "price", descending: false);
        var byProfit = RareListing.Within(Home.Position, 100_000, index, "profit", descending: true);
        var byTotal = RareListing.Within(Home.Position, 100_000, index, "total", descending: true);

        Assert.Equal(RareCatalogue.All.Count, everywhere.Count);
        Assert.Equal([withFar[1].Name, withFar[0].Name, withFar[2].Name], Names(byPrice.Take(3)));
        Assert.Equal([withFar[1].Name, withFar[0].Name, withFar[2].Name], Names(byProfit.Take(3)));
        Assert.Equal([withFar[2].Name, withFar[1].Name, withFar[0].Name], Names(byTotal.Take(3)));
        Assert.All(byPrice.Skip(3), row => Assert.Null(row.Price));
        Assert.All(byProfit.Skip(3), row => Assert.Null(row.Profit));
        Assert.Contains(unindexed.Name, Names(byTotal.Skip(3)));
    }

    [Fact]
    public void AnExplicitDirectionReversesTheOrderButStillEndsWithTheUnknown()
    {
        var rares = RareCatalogue.All.Take(2).ToArray();
        var index = Index(Row(rares[0].Symbol, 2_000, 100, 5), Row(rares[1].Symbol, 2_000, 700, 5));

        var cheapest = RareListing.Within(Home.Position, 100_000, index, "price", descending: true);

        Assert.Equal([rares[1].Name, rares[0].Name], Names(cheapest.Take(2)));
        Assert.All(cheapest.Skip(2), row => Assert.Null(row.Price));
    }

    [Fact]
    public async Task ARareWithNoSupplyStaysAndOneWithNoIndexRowShowsNoFigures()
    {
        var rares = RareCatalogue.All.Where(rare => rare.System != Home.System).Take(1).ToArray();
        var index = Index(Row(Home.Symbol, 5_000, 1_000, 0));

        var result = await ListAsync(
            new FakeIndex(index),
            new() { ["near"] = Home.System, ["max_distance"] = "100000", ["limit"] = "100" });

        Assert.Contains(Home.Name, result.Content, StringComparison.Ordinal);
        Assert.Contains("0 on offer", result.Content, StringComparison.Ordinal);

        var rows = RareListing.Within(Home.Position, 100_000, index, "distance", descending: false);
        var bare = rows.First(row => row.Rare.Symbol == rares[0].Symbol);

        Assert.Null(bare.Supply);
        Assert.Null(bare.Price);
        Assert.Null(bare.Profit);
        Assert.Null(bare.Total);
    }

    [Fact]
    public void TheCataloguesFarPriceIsUsedWhenTheIndexHasNone()
    {
        var rare = RareCatalogue.All.First(entry => entry.FarSellPrice is not null);
        var index = Index(Row(rare.Symbol, null, 1_000, 10));

        var row = RareListing.Within(rare.Position, 1, index, "distance", descending: false)
            .First(listed => listed.Rare.Symbol == rare.Symbol);

        Assert.Equal(rare.FarSellPrice, row.FarPrice);
        Assert.Equal(rare.FarSellPrice - 1_000, row.Profit);
        Assert.Equal(10L * rare.FarSellPrice, row.Total);
    }

    [Fact]
    public async Task AnUnavailableIndexStillListsRaresAndSaysItsFiguresAreNotKnown()
    {
        var result = await ListAsync(
            new FakeIndex(null),
            new() { ["near"] = Home.System, ["order_by"] = "profit" });

        Assert.False(result.IsError, result.Content);
        Assert.Contains(Home.System, result.Content, StringComparison.Ordinal);
        Assert.Contains("rare index could not be fetched, so supply and prices are not known", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyThreeRaresAreNamedAndTheRestAreCounted()
    {
        var result = await ListAsync(
            new FakeIndex(Index()),
            new() { ["near"] = Home.System, ["max_distance"] = "100000", ["limit"] = "10" });

        var nearest = RareCatalogue.All
            .OrderBy(rare => Home.Position.DistanceTo(rare.Position))
            .Select(rare => rare.Name)
            .ToArray();

        Assert.Contains(nearest[2], result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(nearest[3], result.Content, StringComparison.Ordinal);
        Assert.Contains("7 more on this page", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task YourOwnSystemAndARaresSystemAskNoService()
    {
        var galaxy = new Galaxy(null);

        var own = await ListAsync(new FakeIndex(Index()), new() { ["near"] = "Nowhere" }, galaxy);
        var rares = await ListAsync(new FakeIndex(Index()), new() { ["near"] = Home.System }, galaxy);

        Assert.False(own.IsError, own.Content);
        Assert.False(rares.IsError, rares.Content);
        Assert.Empty(galaxy.Asked);
    }

    [Fact]
    public async Task AnyOtherSystemIsPlacedByTheGalaxyService()
    {
        var galaxy = new Galaxy(Home.Position);

        var result = await ListAsync(
            new FakeIndex(Index()),
            new() { ["near"] = "Some Other System", ["max_distance"] = "1" },
            galaxy);

        Assert.False(result.IsError, result.Content);
        Assert.Equal(["Some Other System"], galaxy.Asked);
        Assert.Contains(Home.Name, result.Content, StringComparison.Ordinal);
    }
}
