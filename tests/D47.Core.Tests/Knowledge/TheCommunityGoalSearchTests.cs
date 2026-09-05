using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The four filters INARA has and the search lacked, the nearest-first ordering, and the saved
/// search that runs them by voice (#296).
/// </summary>
public class TheCommunityGoalSearchTests
{
    private static MarketSnapshot Market(
        string station,
        double x = 10,
        int supply = 20_000,
        double? arrival = 500,
        string? type = "Coriolis Starport",
        bool largePad = true,
        int buy = 50_000) => new()
        {
            Station = station,
            System = station + " system",
            X = x,
            Type = type,
            HasLargePad = largePad,
            DistanceToArrival = arrival,
            UpdatedAt = DateTimeOffset.UnixEpoch,
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                ["Palladium"] = new("Palladium") { BuyPrice = buy, Supply = supply },
            },
        };

    private static MarketSnapshot Origin() => new() { Station = "Here", System = "Ega" };

    private static CommodityQuery Saved() => new(
        "Palladium",
        TradeSide.Buying,
        MaxDistance: 250,
        LargePadOnly: true,
        Limit: 10,
        MaxStationDistance: 50_000,
        MinAvailable: 10_000,
        SurfaceStations: false,
        OrderBy: CommodityOrder.Distance);

    private static string[] Stations(IReadOnlyList<CommodityOffer> offers) =>
        [.. offers.Select(offer => offer.Market.Station)];

    [Fact]
    public void AStationSixtyThousandLightSecondsOutIsExcluded()
    {
        var offers = CommodityMarketSearch.Rank(
            Saved(),
            [Market("Far pad", arrival: 60_000), Market("Near pad", arrival: 400)],
            Origin());

        Assert.Equal(["Near pad"], Stations(offers));
    }

    [Fact]
    public void AStationWithNineThousandInStockIsExcluded()
    {
        var offers = CommodityMarketSearch.Rank(
            Saved(),
            [Market("Thin", supply: 9_000), Market("Stocked", supply: 10_000)],
            Origin());

        Assert.Equal(["Stocked"], Stations(offers));
    }

    [Theory]
    [InlineData("Planetary Outpost")]
    [InlineData("Planetary Port")]
    [InlineData("Settlement")]
    [InlineData("Surface Settlement")]
    public void ASurfacePortIsExcludedUnlessAskedFor(string type)
    {
        var markets = new[] { Market("Ground", type: type), Market("Orbit") };

        Assert.Equal(["Orbit"], Stations(CommodityMarketSearch.Rank(Saved(), markets, Origin())));

        Assert.Equal(
            ["Ground", "Orbit"],
            Stations(CommodityMarketSearch.Rank(Saved() with { SurfaceStations = true }, markets, Origin()))
                .Order()
                .ToArray());
    }

    [Fact]
    public void AStrongholdCarrierFallsUnderTheCarrierSwitch()
    {
        var markets = new[] { Market("Stronghold", type: "Stronghold Carrier"), Market("Station") };

        Assert.Equal(["Station"], Stations(CommodityMarketSearch.Rank(Saved(), markets, Origin())));

        Assert.Contains(
            "Stronghold",
            Stations(CommodityMarketSearch.Rank(Saved() with { IncludeCarriers = true }, markets, Origin())));
    }

    [Fact]
    public void AStationWithNoTypeOrNoArrivalDistanceIsKept()
    {
        var offers = CommodityMarketSearch.Rank(
            Saved(),
            [Market("Unknown", type: null, arrival: null)],
            Origin());

        Assert.Equal(["Unknown"], Stations(offers));
    }

    [Fact]
    public void NearestFirstPutsTheNearestFirstWhateverItCharges()
    {
        var offers = CommodityMarketSearch.Rank(
            Saved(),
            [Market("Cheap and far", x: 100, buy: 40_000), Market("Dear and near", x: 5, buy: 60_000)],
            Origin());

        Assert.Equal(["Dear and near", "Cheap and far"], Stations(offers));

        // And the price ordering is untouched.
        var byPrice = CommodityMarketSearch.Rank(
            Saved() with { OrderBy = CommodityOrder.Price },
            [Market("Cheap and far", x: 100, buy: 40_000), Market("Dear and near", x: 5, buy: 60_000)],
            Origin());

        Assert.Equal(["Cheap and far", "Dear and near"], Stations(byPrice));
    }

    [Fact]
    public void TheSavedSearchBakesTheInaraQueryIntoTheGalaxyTool()
    {
        var search = new CommunityGoalSearch();

        var command = Assert.Single(search.Phrases(), candidate => candidate.Phrase == "community goal search");

        Assert.Equal(GalaxyCapability.Id, command.CapabilityId);
        Assert.Equal("find_nearest_station", command.ToolName);
        Assert.Equal("Palladium", command.Arguments["commodity"]);
        Assert.Equal("250", command.Arguments["max_distance"]);
        Assert.Equal("8", command.Arguments["max_price_age_hours"]);
        Assert.Equal("true", command.Arguments["large_pad"]);
        Assert.Equal("50000", command.Arguments["max_station_distance"]);
        Assert.Equal("10000", command.Arguments["min_supply"]);
        Assert.Equal("distance", command.Arguments["order_by"]);
        Assert.False(command.Arguments.ContainsKey("surface_stations"));
        Assert.False(command.Arguments.ContainsKey("include_carriers"));
        Assert.False(command.Arguments.ContainsKey("near"), "It runs from wherever the ship is.");
    }

    [Fact]
    public void TheCommodityIsTheOneThingThatMoves()
    {
        var search = new CommunityGoalSearch { Commodity = "  Gold " };

        Assert.Equal("Gold", search.Commodity);
        Assert.All(search.Phrases(), command => Assert.Equal("Gold", command.Arguments["commodity"]));

        search.Commodity = "   ";

        Assert.Equal(CommunityGoalSearch.DefaultCommodity, search.Commodity);
    }

    [Fact]
    public void RefreshIsACommandOnlyWhileThePageIsShowing()
    {
        var showing = false;
        var search = new CommunityGoalSearch { Showing = () => showing };

        Assert.DoesNotContain(search.Phrases(), command => command.Phrase == "refresh");

        showing = true;

        var refresh = Assert.Single(search.Phrases(), command => command.Phrase == "refresh");

        Assert.Equal("find_nearest_station", refresh.ToolName);
        Assert.Equal("Palladium", refresh.Arguments["commodity"]);
    }

    [Theory]
    [InlineData("Palladium", "palladium", true)]
    [InlineData("Palladium", "Palladium", true)]
    [InlineData("Low Temperature Diamonds", "lowtemperaturediamonds", true)]
    [InlineData("Palladium", "Gold", false)]
    [InlineData("Palladium", null, false)]
    public void TheJournalsSpellingOfTheCommodityIsRecognised(string commodity, string? named, bool expected)
    {
        Assert.Equal(expected, new CommunityGoalSearch { Commodity = commodity }.IsCommodity(named));
    }

    [Fact]
    public void TheRouterTakesThePhraseWholeAndFirst()
    {
        using var install = new TempInstall();
        var surface = TestSurface.For(install);
        var search = new CommunityGoalSearch();
        var router = new KeywordRouter(surface.Registry, search.Phrases);

        var match = router.MatchToolCommand("Community goal search.");

        Assert.NotNull(match);
        Assert.Equal("find_nearest_station", match.ToolName);
        Assert.True(match.Arguments.TryGetString("min_supply", out var floor));
        Assert.Equal("10000", floor);

        // Whole utterance: a longer sentence about the goal still goes to the model.
        Assert.Null(router.MatchToolCommand("run a community goal search for gold instead"));
    }
}
