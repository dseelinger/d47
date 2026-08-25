using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// Where to buy it, and what it costs there (list.md Phase 49).
/// <para>
/// The ranking is the whole of this feature and it is pure, so all of it is assertable without a
/// socket. The case that matters most is the one the checklist names: the same two stations, and
/// the tonnage deciding which is the right answer.
/// </para>
/// </summary>
public class WhereToBuyItTests
{
    private static MarketSnapshot Market(
        string station,
        string system,
        double x,
        int buy = 0,
        int supply = 0,
        int sell = 0,
        int demand = 0,
        string commodity = "Tritium",
        string? type = "Coriolis Starport",
        bool largePad = true,
        PriceSource source = PriceSource.Reported,
        DateTimeOffset? updated = null) => new()
        {
            Station = station,
            System = system,
            X = x,
            Type = type,
            HasLargePad = largePad,
            Source = source,
            UpdatedAt = updated ?? DateTimeOffset.UnixEpoch,
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                [commodity] = new(commodity)
                {
                    BuyPrice = buy,
                    Supply = supply,
                    SellPrice = sell,
                    Demand = demand,
                },
            },
        };

    private static MarketSnapshot Origin() => Market("Home", "Sol", 0);

    /// <summary>
    /// The checklist's own example, both ways. Near is 500 a tonne; far is 40 ly out at 300. For
    /// eight tonnes the saving is 1,600 credits and not worth the trip; for seven hundred and
    /// eighty it is 156,000 and plainly is.
    /// </summary>
    [Theory]
    [InlineData(8, "Near")]
    [InlineData(780, "Far")]
    public void TheTonnageDecidesWhichStationIsTheRightAnswer(int tonnes, string expected)
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying, Tonnes: tonnes),
            [
                Market("Near", "Sol", 0, buy: 500, supply: 1000),
                Market("Far", "Elsewhere", 40, buy: 300, supply: 1000),
            ],
            Origin());

        Assert.Equal(expected, offers[0].Market.Station);
    }

    /// <summary>
    /// Without a tonnage there is nothing to weigh a detour against, so price decides and the
    /// distance is reported rather than priced. A Commander who did not say how much is asking
    /// what a thing is worth.
    /// </summary>
    [Fact]
    public void WithNoTonnageThePriceDecidesAndTheDistanceIsJustReported()
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying),
            [
                Market("Near", "Sol", 0, buy: 500, supply: 1000),
                Market("Far", "Elsewhere", 40, buy: 300, supply: 1000),
            ],
            Origin());

        Assert.Equal("Far", offers[0].Market.Station);
        Assert.Equal(40, offers[0].Distance);
    }

    /// <summary>
    /// Supply is a filter and not a footnote. The cheapest steel in the bubble held nine tonnes
    /// deep is not an answer to "where do I buy seven hundred".
    /// </summary>
    [Fact]
    public void AStationThatCannotFillTheLoadIsNotAnAnswer()
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying, Tonnes: 700),
            [
                Market("Cheap", "Sol", 0, buy: 100, supply: 9),
                Market("Enough", "Sol", 0, buy: 500, supply: 700),
            ],
            Origin());

        Assert.Equal("Enough", Assert.Single(offers).Market.Station);
    }

    /// <summary>The same question read the other way round, which is the sell half.</summary>
    [Fact]
    public void SellingRanksTheHighestPayerFirstAndNeedsDemandRatherThanSupply()
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Selling, Tonnes: 700),
            [
                Market("Pays well", "Sol", 0, sell: 9000, demand: 700),
                Market("Pays badly", "Sol", 0, sell: 3000, demand: 700),

                // Sells it cheaply, buys none of it. Invisible to the sell side.
                Market("Sells only", "Sol", 0, buy: 100, supply: 5000),
            ],
            Origin());

        Assert.Equal(2, offers.Count);
        Assert.Equal("Pays well", offers[0].Market.Station);
    }

    /// <summary>
    /// A carrier's prices are player-set and can be a joke, and the station itself may be a
    /// hundred light years away by the time the Commander arrives. Out unless asked for.
    /// </summary>
    [Fact]
    public void AFleetCarrierIsExcludedUnlessItIsAskedFor()
    {
        MarketSnapshot[] markets =
        [
            Market("Station", "Sol", 0, buy: 50_000, supply: 1000),
            Market("Carrier", "Sol", 0, buy: 100, supply: 1000, type: "Drake-Class Carrier"),
        ];

        var without = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying), markets, Origin());

        var with = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying, IncludeCarriers: true), markets, Origin());

        Assert.Equal("Station", Assert.Single(without).Market.Station);
        Assert.Equal("Carrier", with[0].Market.Station);
    }

    [Fact]
    public void ALargePadFilterDropsTheOutposts()
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying, LargePadOnly: true),
            [
                Market("Outpost", "Sol", 0, buy: 100, supply: 1000, largePad: false),
                Market("Starport", "Sol", 0, buy: 500, supply: 1000),
            ],
            Origin());

        Assert.Equal("Starport", Assert.Single(offers).Market.Station);
    }

    [Fact]
    public void AStationBeyondTheRadiusIsNotAnAnswer()
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying, MaxDistance: 20),
            [
                Market("Inside", "Sol", 10, buy: 500, supply: 1000),
                Market("Outside", "Far", 400, buy: 100, supply: 1000),
            ],
            Origin());

        Assert.Equal("Inside", Assert.Single(offers).Market.Station);
    }

    /// <summary>
    /// A station that neither sells nor stocks it is not an answer, and a zero price is not a
    /// bargain. Elite writes zero for "does not trade this", so a ranker that took the cheapest
    /// number would put every station that has never heard of the commodity at the top.
    /// </summary>
    [Fact]
    public void AZeroPriceMeansTheStationDoesNotTradeItRatherThanThatItIsFree()
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying),
            [
                Market("Does not stock it", "Sol", 0, buy: 0, supply: 0),
                Market("Stocks it", "Sol", 0, buy: 500, supply: 1000),
            ],
            Origin());

        Assert.Equal("Stocks it", Assert.Single(offers).Market.Station);
    }

    /// <summary>The Commander's own eyes are labelled, because that is the one figure with no caveat.</summary>
    [Fact]
    public void AMarketTheCommanderStoodInIsMarkedAsTheirs()
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying),
            [Market("Theirs", "Sol", 0, buy: 500, supply: 1000, source: PriceSource.Seen)],
            Origin());

        Assert.True(Assert.Single(offers).IsTheirs);
    }

    /// <summary>
    /// The commodity is the join key between a market and everything else, and it is folded rather
    /// than re-spelled. "low temperature diamonds" has to find "Low Temperature Diamonds".
    /// </summary>
    [Fact]
    public void TheCommodityNameIsMatchedWithoutRegardToCase()
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("low temperature diamonds", TradeSide.Buying),
            [Market("Station", "Sol", 0, buy: 500, supply: 1000, commodity: "Low Temperature Diamonds")],
            Origin());

        Assert.Single(offers);
    }

    [Fact]
    public void TheLimitIsHonoured()
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying, Limit: 2),
            [
                Market("A", "Sol", 0, buy: 100, supply: 1000),
                Market("B", "Sol", 0, buy: 200, supply: 1000),
                Market("C", "Sol", 0, buy: 300, supply: 1000),
            ],
            Origin());

        Assert.Equal(2, offers.Count);
    }

    /// <summary>
    /// The load is capped at what the station can do, so a quoted total is one the Commander could
    /// actually pay rather than one for tonnage that is not there.
    /// </summary>
    [Fact]
    public void TheQuotedTotalIsForTonnageTheStationActuallyHas()
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying),
            [Market("Station", "Sol", 0, buy: 500, supply: 40)],
            Origin());

        var offer = Assert.Single(offers);

        Assert.Equal(40, offer.Tonnes);
        Assert.Equal(20_000, offer.Total);
    }

    /// <summary>
    /// With no origin nothing can be measured, so the radius cannot be applied and price alone
    /// decides. Answering with the nearest of a set of unknown distances would be a guess wearing
    /// a number.
    /// </summary>
    [Fact]
    public void WithNoOriginTheDistancesAreUnknownAndPriceAloneDecides()
    {
        var offers = CommodityMarketSearch.Rank(
            new CommodityQuery("Tritium", TradeSide.Buying, MaxDistance: 1),
            [
                Market("Far but cheap", "Far", 400, buy: 100, supply: 1000),
                Market("Near but dear", "Sol", 0, buy: 900, supply: 1000),
            ],
            origin: null);

        Assert.Equal(2, offers.Count);
        Assert.Equal("Far but cheap", offers[0].Market.Station);
    }
}
