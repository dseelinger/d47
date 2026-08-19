using System.Diagnostics;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// d47's own trade arithmetic (list.md Phase 36). Every market here is written out by hand, which
/// is the property that makes the phase testable at all: the planner reads no clock, opens no
/// socket and has never heard of Spansh.
/// </summary>
public class TradePlannerTests
{
    private static MarketSnapshot Market(
        string station,
        double x,
        params (string Commodity, int Buy, int Sell, int Supply, int Demand)[] quotes) =>
        new()
        {
            Station = station,
            System = station + " System",
            X = x,
            UpdatedAt = new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero),
            HasLargePad = true,
            Type = "Coriolis Starport",
            Quotes = quotes.ToDictionary(
                quote => quote.Commodity,
                quote => new MarketQuote(quote.Commodity)
                {
                    BuyPrice = quote.Buy,
                    SellPrice = quote.Sell,
                    Supply = quote.Supply,
                    Demand = quote.Demand,
                },
                StringComparer.OrdinalIgnoreCase),
        };

    private static TradeQuery Query(
        int hops = 3,
        long capital = 1_000_000,
        int hold = 100,
        bool loop = false,
        double hopDistance = 15)
    {
        Assert.True(TradeQuery.TryParse(
            "Origin System",
            "Origin",
            capital,
            hold,
            hops,
            hopDistance,
            1_000,
            false,
            720,
            loop,
            out var query,
            out _));

        return query;
    }

    [Fact]
    public void HoldingCargoPastAPoorBuyerBeatsSellingIntoIt()
    {
        // The twist, and the one thing no other planner does. Selling the gold at the station in
        // the middle takes 20,000 credits; flying it one more hop takes 400,000.
        var markets = new[]
        {
            Market("Origin", 0, ("Gold", 1_000, 0, 1_000, 0)),
            Market("Middle", 10, ("Gold", 0, 1_200, 0, 1_000)),
            Market("Far", 20, ("Gold", 0, 5_000, 0, 1_000)),
        };

        var route = TradePlanner.Plan(Query(), markets);

        Assert.NotNull(route);
        Assert.Equal(3, route.Stops.Count);
        Assert.Equal(400_000, route.TotalProfit);

        var middle = route.Stops[1];

        Assert.Empty(middle.Sell);

        var held = Assert.Single(middle.Hold);

        Assert.Equal("Gold", held.Commodity);
        Assert.Equal(100, held.Amount);

        // Priced at what this station would have paid, so the Commander can see what flying past
        // it is worth rather than being told to fly past it.
        Assert.Equal(1_200, held.UnitPrice);
    }

    [Fact]
    public void ARoundTripEndsWhereItStarted()
    {
        var markets = new[]
        {
            Market("Origin", 0, ("Gold", 1_000, 0, 1_000, 0), ("Silver", 0, 900, 0, 1_000)),
            Market("Middle", 10, ("Gold", 0, 3_000, 0, 1_000), ("Silver", 500, 0, 1_000, 0)),
        };

        var route = TradePlanner.Plan(Query(hops: 2, loop: true), markets);

        Assert.NotNull(route);
        Assert.True(route.Loop);
        Assert.Equal("Origin", route.Stops[^1].Station);

        // Out with gold, back with silver: 200,000 on the way out and 40,000 on the way home.
        Assert.Equal(240_000, route.TotalProfit);
    }

    [Fact]
    public void NoLegSellsMoreThanTheStationAskedFor()
    {
        // The whole of d47's saturation answer, and deliberately the conservative half of it. The
        // factor by which dumping past demand drops the price is not known and is not invented.
        var markets = new[]
        {
            Market("Origin", 0, ("Gold", 1_000, 0, 1_000, 0)),
            Market("Middle", 10, ("Gold", 0, 5_000, 0, 10)),
        };

        var route = TradePlanner.Plan(Query(hops: 1), markets);

        Assert.NotNull(route);

        var bought = Assert.Single(route.Stops[0].Buy);

        Assert.Equal(10, bought.Amount);
        Assert.True(route.Stops[0].CappedByDemand);
        Assert.Equal(40_000, route.TotalProfit);
    }

    [Fact]
    public void FleetCarriersAreLeftOutAndSaidToHaveBeen()
    {
        // Measured 2026-08-19: the best Gold price within 50 light years of Sol was 4,760,900
        // credits at a carrier, against 52,282 at the best station. A planner that ranks on price
        // and does not know what a carrier is builds every plan around one, and half of them have
        // jumped by the time the Commander gets there.
        var carrier = Market("K7Q-BQL", 5, ("Gold", 0, 4_760_900, 0, 1_000)) with
        {
            Type = "Drake-Class Carrier",
        };

        var markets = new[]
        {
            Market("Origin", 0, ("Gold", 1_000, 0, 1_000, 0)),
            Market("Middle", 10, ("Gold", 0, 5_000, 0, 1_000)),
            carrier,
        };

        var route = TradePlanner.Plan(Query(hops: 1), markets);

        Assert.NotNull(route);
        Assert.Equal(1, route.CarriersIgnored);
        Assert.DoesNotContain(route.Stops, stop => stop.Station == "K7Q-BQL");
        Assert.Equal(2, route.MarketsConsidered);
    }

    [Fact]
    public void RaresAreLeftOutRatherThanMispriced()
    {
        // Priced per station, capped at a handful of tonnes by the game itself, and worth less the
        // closer they are sold to home. None of that is modelled, so they are not planned with.
        var origin = Market("Origin", 0);

        var markets = new[]
        {
            origin with
            {
                Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Lavian Brandy"] = new("Lavian Brandy") { BuyPrice = 1_000, Supply = 1_000, IsRare = true },
                },
            },
            Market("Middle", 10, ("Lavian Brandy", 0, 90_000, 0, 1_000)),
        };

        var route = TradePlanner.Plan(Query(hops: 1), markets);

        Assert.NotNull(route);
        Assert.Empty(route.Stops);
    }

    [Fact]
    public void AMarketTheOriginItselfIsMissingFromIsADifferentAnswerFromNoRoute()
    {
        // Null is "I cannot see the board you are standing in front of". An empty route is
        // "nothing near here pays". The capability words them differently because they are.
        var route = TradePlanner.Plan(Query(), [Market("Elsewhere", 10, ("Gold", 1_000, 0, 500, 0))]);

        Assert.Null(route);
    }

    [Fact]
    public void TheOriginIsPlannedFromEvenWhereItFailsThePadRule()
    {
        // The Commander is already docked there. A pad rule has nothing left to protect them from,
        // and dropping the origin would answer "I cannot see your market" about the market they
        // are standing in.
        var markets = new[]
        {
            Market("Origin", 0, ("Gold", 1_000, 0, 1_000, 0)) with { HasLargePad = false },
            Market("Middle", 10, ("Gold", 0, 5_000, 0, 1_000)),
        };

        Assert.True(TradeQuery.TryParse(
            "Origin System", "Origin", 1_000_000, 100, 1, 15, 1_000, true, 720, false, out var query, out _));

        var route = TradePlanner.Plan(query, markets);

        Assert.NotNull(route);
        Assert.Equal(400_000, route.TotalProfit);
    }

    [Fact]
    public void TenHopsOverAGalaxysWorthOfMarketsFinishInATimeSomebodyWillWait()
    {
        // The anchor is what the borrowed planner cost: Spansh answered a four-hop trade route in
        // forty-eight seconds, measured 2026-08-14. Ten hops in appreciably less than that is a
        // good result and anything in minutes is not. The shape here is the one the phase measured
        // against — 150 markets, 30 commodities, a beam of 200.
        var random = new Random(47);
        var commodities = Enumerable.Range(0, 30).Select(index => $"Commodity {index}").ToArray();
        var markets = new List<MarketSnapshot>();

        for (var station = 0; station < 150; station++)
        {
            var quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase);

            foreach (var commodity in commodities)
            {
                var mean = random.Next(500, 50_000);

                quotes[commodity] = new MarketQuote(commodity)
                {
                    BuyPrice = random.Next(2) == 0 ? mean : 0,
                    SellPrice = random.Next(2) == 0 ? (int)(mean * 1.2) : 0,
                    Supply = random.Next(0, 5_000),
                    Demand = random.Next(0, 5_000),
                };
            }

            markets.Add(new MarketSnapshot
            {
                Station = station == 0 ? "Origin" : $"Station {station}",
                System = station == 0 ? "Origin System" : $"System {station}",
                X = random.NextDouble() * 40,
                Y = random.NextDouble() * 40,
                Z = random.NextDouble() * 40,
                HasLargePad = true,
                UpdatedAt = new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero),
                Quotes = quotes,
            });
        }

        var stopwatch = Stopwatch.StartNew();
        var route = TradePlanner.Plan(Query(hops: 10, capital: 500_000_000, hold: 720, hopDistance: 40), markets);

        stopwatch.Stop();

        Assert.NotNull(route);
        Assert.True(route.Stops.Count > 1, "Ten hops over 150 markets found no route at all.");

        // Generous by a wide margin against the tens of milliseconds this measures at, because a
        // build agent under load is not a workstation. It is still five times inside the wait the
        // borrowed planner charged for four hops.
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Ten hops over 150 markets took {stopwatch.Elapsed.TotalSeconds:N1} seconds.");
    }
}
