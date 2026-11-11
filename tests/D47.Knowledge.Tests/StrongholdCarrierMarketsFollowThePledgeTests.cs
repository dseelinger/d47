using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A Stronghold Carrier's market is in an answer only when the Commander is pledged to its Power.</summary>
public class StrongholdCarrierMarketsFollowThePledgeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly PowerplayPledge Aisling = new("Aisling Duval");

    private static string SweepRow(string station, string system, double x, string type, string? power, string market) =>
        $$"""
          {"name":"{{station}}","system_name":"{{system}}","system_x":{{x}},"system_y":0,"system_z":0,
           "distance_to_arrival":100,"has_large_pad":true,"type":"{{type}}",
           {{(power is null ? "" : $"\"system_controlling_power\":\"{power}\",\"system_power_state\":\"Stronghold\",")}}
           "market_updated_at":"2026-11-10T10:00:00Z","market":[{{market}}]}
          """;

    private static string Buys(string commodity, int price) =>
        $$"""{"commodity":"{{commodity}}","buy_price":0,"sell_price":{{price}},"supply":0,"demand":100000,"is_rare":false}""";

    private static string Sells(string commodity, int price) =>
        $$"""{"commodity":"{{commodity}}","buy_price":{{price}},"sell_price":{{price - 100}},"supply":100000,"demand":0,"is_rare":false}""";

    // The Li Yong-Rui carrier pays most, then the Aisling Duval carrier, then the outpost.
    private static readonly string Sweep =
        "{\"count\":4,\"results\":["
        + string.Join(",",
            SweepRow("Abraham Lincoln", "Sol", 0, "Ocellus Starport", null,
                string.Join(",", Sells("Gold", 9_400), Sells("Silver", 4_000), Sells("Palladium", 50_000))),
            SweepRow("Stronghold Carrier", "Li Hold", 5, "Planetary Outpost", "Li Yong-Rui", Buys("Gold", 60_000)),
            SweepRow("Stronghold Carrier", "Aisling Hold", 6, "Planetary Outpost", "Aisling Duval", Buys("Silver", 40_000)),
            SweepRow("Quiet Outpost", "Quiet System", 7, "Outpost", null, Buys("Palladium", 60_000)))
        + "]}";

    private const string CarrierList =
        """{"count":1,"results":[{"name":"Stronghold Carrier","system_name":"System A","type":"Planetary Outpost"}]}""";

    private const string Catalogue = """[{"commodityName":"gold"}]""";

    private const string Sol = """{"systemName":"Sol","systemX":0,"systemY":0,"systemZ":0}""";

    private static string ArdentRow(string station, string system, double x, string type) =>
        $$"""
          {"commodityName":"gold","stationName":"{{station}}","stationType":"{{type}}","distanceToArrival":100,
           "maxLandingPadSize":3,"systemName":"{{system}}","systemX":{{x}},"systemY":0,"systemZ":0,
           "buyPrice":9000,"demand":0,"sellPrice":8900,"stock":500,"updatedAt":"2026-11-10T11:00:00.000Z"}
          """;

    private static readonly string Nearby =
        "["
        + string.Join(",",
            ArdentRow("Stronghold Carrier", "System A", 3, "StrongholdCarrier"),
            ArdentRow("Stronghold Carrier", "System B", 4, "StrongholdCarrier"),
            ArdentRow("Quiet Outpost", "Quiet System", 5, "Outpost"))
        + "]";

    /// <summary>Answers Spansh and Ardent from held strings, counting the Spansh carrier list requests.</summary>
    private sealed class Held(bool listFails = false) : HttpMessageHandler
    {
        public int ListRequests { get; private set; }

        public int SpanshRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri!;
            var status = HttpStatusCode.OK;
            string body;

            if (url.Host == "spansh.co.uk")
            {
                SpanshRequests++;

                var sent = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);

                if (sent.Contains("system_controlling_power", StringComparison.Ordinal))
                {
                    ListRequests++;
                    status = listFails ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
                    body = listFails ? "{}" : CarrierList;
                }
                else
                {
                    body = Sweep;
                }
            }
            else
            {
                var path = url.AbsolutePath;

                body = path.Contains("/v2/commodities", StringComparison.Ordinal)
                    ? Catalogue
                    : path.Contains("/nearby/", StringComparison.Ordinal)
                        ? Nearby
                        : Sol;
            }

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static SpanshTradePlanService Service(Held held, PowerplayPledge? pledge, MarketBook? book = null) =>
        new(
            NullLogger<SpanshTradePlanService>.Instance,
            new ArdentCommodityService(
                NullLogger<ArdentCommodityService>.Instance,
                new HttpClient(held) { BaseAddress = new Uri($"https://{ArdentCommodityService.Host}/") }),
            book,
            new HttpClient(held) { BaseAddress = new Uri("https://spansh.co.uk/") },
            () => Now,
            () => pledge);

    private static TradeQuery Query()
    {
        Assert.True(TradeQuery.TryParse(
            "Sol", "Abraham Lincoln", 50_000_000, 384, 1, 2, 20, 1_000, false, 720, false, true, true,
            out var query, out _));

        return query;
    }

    private static Task<CommodityAnswer> FindGoldAsync(SpanshTradePlanService service) =>
        service.FindCommodityAsync(
            new CommoditySearch("Sol", null, new CommodityQuery("Gold"), MaxPriceAge: 0),
            TestContext.Current.CancellationToken);

    private static IEnumerable<string> Systems(CommodityAnswer answer) =>
        answer.Offers.Select(offer => offer.Market.System).Order(StringComparer.Ordinal);

    [Fact]
    public void ACarrierRowInTheMarketSweepCarriesItsPowerAndIsNoFleetCarrier()
    {
        using var document = JsonDocument.Parse(Sweep);

        var carrier = SpanshResponse.ReadMarkets(document).Single(market => market.System == "Aisling Hold");

        Assert.Equal("Aisling Duval", carrier.ControllingPower);
        Assert.Equal("Stronghold", carrier.PowerState);
        Assert.True(carrier.IsStrongholdCarrier);
        Assert.False(carrier.IsCarrier);
    }

    [Theory]
    [InlineData(null, "Quiet System")]
    [InlineData("Aisling Duval", "Aisling Hold")]
    public async Task ATradeRouteStopsOnlyAtTheCarriersOfThePledgedPower(string? power, string destination)
    {
        var held = new Held();
        using var service = Service(held, power is null ? null : new PowerplayPledge(power));

        var route = await service.PlanAsync(Query(), TestContext.Current.CancellationToken);

        Assert.NotNull(route);
        Assert.Equal(destination, route.Stops[^1].System);
    }

    [Theory]
    [InlineData(null, new[] { "Palladium" })]
    [InlineData("Aisling Duval", new[] { "Palladium", "Silver" })]
    public async Task BestCargoSellsOnlyToTheCarriersOfThePledgedPower(string? power, string[] commodities)
    {
        var book = new MarketBook(Path.Combine(@"C:\d47-test", "markets.json"), new MemoryFileSystem(), NullLogger.Instance);

        book.Remember(new MarketSnapshot
        {
            Station = "Abraham Lincoln",
            System = "Sol",
            UpdatedAt = Now,
            Source = PriceSource.Seen,
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                ["Gold"] = new("Gold") { BuyPrice = 9_400, Supply = 100_000 },
                ["Silver"] = new("Silver") { BuyPrice = 4_000, Supply = 100_000 },
                ["Palladium"] = new("Palladium") { BuyPrice = 50_000, Supply = 100_000 },
            },
        });

        var held = new Held();
        using var service = Service(held, power is null ? null : new PowerplayPledge(power), book);

        var answer = await service.BestCargoAsync(
            new BestCargoSearch
            {
                System = "Sol",
                Station = "Abraham Lincoln",
                Destination = "Anywhere",
                Hold = 100,
                MaxPriceAge = 0,
                Planetary = true,
            },
            TestContext.Current.CancellationToken);

        Assert.NotNull(answer);
        Assert.Equal(commodities, answer.Picks.Select(pick => pick.Commodity).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task APledgedCommanderKeepsTheCarriersOnTheirPowersList()
    {
        var held = new Held();
        using var service = Service(held, Aisling);

        var answer = await FindGoldAsync(service);

        Assert.Equal(["Quiet System", "System A"], Systems(answer));
        Assert.Equal(1, held.ListRequests);
        Assert.Equal(1, answer.LeftOut);
        Assert.True(answer.Pledged);
    }

    [Fact]
    public async Task AnUnpledgedCommanderGetsNoCarrierAndSendsNoLookup()
    {
        var held = new Held();
        using var service = Service(held, pledge: null);

        var answer = await FindGoldAsync(service);

        Assert.Equal(["Quiet System"], Systems(answer));
        Assert.Equal(0, held.SpanshRequests);
        Assert.Equal(2, answer.LeftOut);
        Assert.False(answer.Pledged);
    }

    [Fact]
    public async Task AFailedLookupDropsEveryCarrierAndCountsNone()
    {
        var held = new Held(listFails: true);
        using var service = Service(held, Aisling);

        var answer = await FindGoldAsync(service);

        Assert.Equal(["Quiet System"], Systems(answer));
        Assert.Equal(1, held.ListRequests);
        Assert.Equal(0, answer.LeftOut);
    }

    [Fact]
    public async Task TwoSearchesInARowSendTheLookupOnce()
    {
        var held = new Held();
        using var service = Service(held, Aisling);

        await FindGoldAsync(service);
        var second = await FindGoldAsync(service);

        Assert.Equal(["Quiet System", "System A"], Systems(second));
        Assert.Equal(1, held.ListRequests);
    }

    [Fact]
    public void TheLookupAsksForThePowersStrongholdCarriersByName()
    {
        using var document = JsonDocument.Parse(SpanshRequest.StrongholdCarriers("Aisling Duval"));
        var filters = document.RootElement.GetProperty("filters");

        Assert.Equal("Stronghold Carrier", filters.GetProperty("name").GetProperty("value")[0].GetString());
        Assert.Equal("Stronghold", filters.GetProperty("system_power_state").GetProperty("value")[0].GetString());
        Assert.Equal("Aisling Duval", filters.GetProperty("system_controlling_power").GetProperty("value")[0].GetString());
    }
}
