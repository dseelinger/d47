using System.Net;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>The best-cargo answer ranks only the stations in the destination system.</summary>
public class BestCargoRanksOnlyTheDestinationSystemTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static string Row(string station, string system, double x, string commodity, int price) =>
        $$"""
          {"name":"{{station}}","system_name":"{{system}}","system_x":{{x}},"system_y":0,"system_z":0,
           "distance_to_arrival":100,"has_large_pad":true,"type":"Ocellus Starport",
           "market_updated_at":"2026-11-10T10:00:00Z","market":[{"commodity":"{{commodity}}","buy_price":0,"sell_price":{{price}},"supply":0,"demand":100000,"is_rare":false}]}
          """;

    private sealed class Held(string sweep) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sweep, Encoding.UTF8, "application/json"),
            });
    }

    private static async Task<BestCargoAnswer?> AskAsync(params string[] rows)
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
            },
        });

        var sweep = $"{{\"count\":{rows.Length},\"results\":[{string.Join(",", rows)}]}}";
        var handler = new Held(sweep);

        using var service = new SpanshTradePlanService(
            NullLogger<SpanshTradePlanService>.Instance,
            new ArdentCommodityService(
                NullLogger<ArdentCommodityService>.Instance,
                new HttpClient(handler) { BaseAddress = new Uri($"https://{ArdentCommodityService.Host}/") }),
            book,
            new HttpClient(handler) { BaseAddress = new Uri("https://spansh.co.uk/") },
            () => Now,
            () => null);

        return await service.BestCargoAsync(
            new BestCargoSearch
            {
                System = "Sol",
                Station = "Abraham Lincoln",
                Destination = "Destination",
                Hold = 100,
                MaxPriceAge = 0,
                Planetary = true,
            },
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ABetterPayingStationInAnotherSystemIsNotPicked()
    {
        var answer = await AskAsync(
            Row("Near Port", "destination", 1, "Gold", 20_000),
            Row("Far Port", "Neighbour", 2, "Gold", 60_000));

        Assert.NotNull(answer);
        var pick = Assert.Single(answer.Picks);
        Assert.Equal("Near Port", pick.Station);
    }

    [Fact]
    public async Task WhenOnlyOtherSystemsBuyThereAreNoPicks()
    {
        var answer = await AskAsync(Row("Far Port", "Neighbour", 2, "Gold", 60_000));

        Assert.NotNull(answer);
        Assert.Empty(answer.Picks);
    }
}
