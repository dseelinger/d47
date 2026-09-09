using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The four filters INARA has and the search lacked, the nearest-first ordering, and the saved search
/// that runs them by voice.
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

    /// <summary>
    /// The goal from #331, as Elite wrote it: the run the Commander was flying when the search asked
    /// its question from 55.65 light years away.
    /// </summary>
    private static CommunityGoalBoard Goal(
        string system = "Ega",
        string expiry = "2026-09-10T10:00:00Z",
        long contribution = 4_200,
        int id = 857,
        string title = "Wreaken Calls for Mining Support to Test New Rig")
    {
        var line = $$"""
                     { "timestamp":"2026-09-05T18:00:00Z", "event":"CommunityGoal", "CurrentGoals":[
                     { "CGID":{{id}}, "Title":"{{title}}",
                     "SystemName":"{{system}}", "MarketName":"Metz Enterprise", "Expiry":"{{expiry}}",
                     "IsComplete":false, "CurrentTotal":900000, "PlayerContribution":{{contribution}},
                     "NumContributors":4000, "TopTier":{ "Name":"Tier 5", "Bonus":"" }, "TopRankSize":10,
                     "PlayerInTopRank":false, "TierReached":"Tier 3", "PlayerPercentileBand":50 } ] }
                     """;

        Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

        return CommunityGoalBoard.Empty.Apply(parsed!);
    }

    /// <summary>Before the goal expires.</summary>
    private static DateTimeOffset Flying => DateTimeOffset.Parse("2026-09-05T18:30:00Z");

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

        // With nothing live on the board there is no goal to measure from, so it falls back to the ship — and
 // says which it did, through the tag the posting carries.
        Assert.False(command.Arguments.ContainsKey("near"));
        Assert.Equal(CommunityGoalSearch.ShipTag, command.Arguments["tag"]);
    }

    [Fact]
    public void ALiveGoalIsWhatTheSearchMeasuresFrom()
    {
        var search = new CommunityGoalSearch { Board = () => Goal(), Now = () => Flying };

        Assert.Equal("Ega", search.GoalSystem);

        var command = Assert.Single(search.Phrases(), candidate => candidate.Phrase == "cg search");

        Assert.Equal("Ega", command.Arguments["near"]);
        Assert.Equal(CommunityGoalSearch.Tag, command.Arguments["tag"]);
        Assert.Equal("the goal's system", CommunityGoalSearch.Whose(command.Arguments["tag"]));
    }

    [Theory]
    [InlineData("2026-09-05T17:00:00Z")]
    [InlineData("2026-09-01T10:00:00Z")]
    public void AnExpiredGoalIsNotTheOrigin(string expiry)
    {
        var search = new CommunityGoalSearch { Board = () => Goal(expiry: expiry), Now = () => Flying };

        Assert.Null(search.GoalSystem);

        var command = Assert.Single(search.Phrases(), candidate => candidate.Phrase == "cg search");

        Assert.False(command.Arguments.ContainsKey("near"));
        Assert.Equal(CommunityGoalSearch.ShipTag, command.Arguments["tag"]);
    }

    [Fact]
    public void AGoalThatNamesNoSystemCannotBeTheOrigin()
    {
        Assert.Null(CommunityGoalSearch.OriginOf(Goal(system: "   "), Flying, "Palladium"));
        Assert.Null(CommunityGoalSearch.OriginOf(CommunityGoalBoard.Empty, Flying, "Palladium"));
        Assert.Null(CommunityGoalSearch.OriginOf(null, Flying, "Palladium"));
    }

    [Fact]
    public void TheGoalTheCommanderIsRunningWinsOverTheOneSeenMoreRecently()
    {
        // CurrentGoals is one station's noticeboard, and the board merges by id rather than replacing, so two
        // live goals is the ordinary case once a Commander has docked twice.
        var board = Goal(system: "Ega", contribution: 4_200);

        foreach (var goal in Goal(system: "Kaushpoos", contribution: 0, id: 901).Goals)
        {
            board = board with { Goals = [.. board.Goals, goal with { SeenAt = Flying }] };
        }

        Assert.Equal("Ega", CommunityGoalSearch.OriginOf(board, Flying, "Palladium"));
    }

    /// <summary>Elite runs several goals at once and the board merges rather than replaces, so choosing on recency alone measures the Palladium run from the other goal's system while the page says "the goal's system".</summary>
    [Fact]
    public void ATitleThatNamesTheCommodityBreaksATieBetweenTwoGoalsBeingRun()
    {
        var board = Goal(system: "Ega", title: "Wreaken Calls for Palladium");

        foreach (var goal in Goal(system: "Kaushpoos", id: 901, title: "Alliance Research Initiative").Goals)
        {
            board = board with { Goals = [.. board.Goals, goal with { SeenAt = Flying }] };
        }

        Assert.Equal("Ega", CommunityGoalSearch.OriginOf(board, Flying, "Palladium"));

        // A tie-break rather than a filter: with no title naming what is being searched for, the most
        // recently reported goal still answers.
        Assert.Equal("Kaushpoos", CommunityGoalSearch.OriginOf(board, Flying, "Bertrandite"));
    }

    [Fact]
    public void AskingFromHereMeasuresFromTheShipEvenWithAGoalRunning()
    {
        var search = new CommunityGoalSearch { Board = () => Goal(), Now = () => Flying };

        var here = Assert.Single(search.Phrases(), candidate => candidate.Phrase == "cg search from here");

        Assert.False(here.Arguments.ContainsKey("near"));
        Assert.Equal(CommunityGoalSearch.ShipTag, here.Arguments["tag"]);
        Assert.Equal("your ship's system", CommunityGoalSearch.Whose(here.Arguments["tag"]));

        // Same question otherwise: only the origin moves.
        var goal = Assert.Single(search.Phrases(), candidate => candidate.Phrase == "cg search");

        foreach (var (key, value) in here.Arguments.Where(pair => pair.Key is not ("tag" or "near")))
        {
            Assert.Equal(value, goal.Arguments[key]);
        }
    }

    [Fact]
    public void ThePageAndTheEarKnowBothTagsAndNothingElse()
    {
        Assert.True(CommunityGoalSearch.Owns(CommunityGoalSearch.Tag));
        Assert.True(CommunityGoalSearch.Owns(CommunityGoalSearch.ShipTag));
        Assert.False(CommunityGoalSearch.Owns("market-page"));
        Assert.False(CommunityGoalSearch.Owns(null));
        Assert.Null(CommunityGoalSearch.Whose("market-page"));
        Assert.Null(CommunityGoalSearch.Whose(null));
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

 // Its from-the-ship twin comes and goes with it.
        var here = Assert.Single(search.Phrases(), command => command.Phrase == "refresh from here");

        Assert.Equal(CommunityGoalSearch.ShipTag, here.Arguments["tag"]);

        showing = false;

        Assert.DoesNotContain(search.Phrases(), command => command.Phrase == "refresh from here");
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

 // "from here" is its own whole phrase, so it is not swallowed by the shorter one.
        var here = router.MatchToolCommand("Cg search from here.");

        Assert.NotNull(here);
        Assert.True(here.Arguments.TryGetString("tag", out var tag));
        Assert.Equal(CommunityGoalSearch.ShipTag, tag);
    }
}
