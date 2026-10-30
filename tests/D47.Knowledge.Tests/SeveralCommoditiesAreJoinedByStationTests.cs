using System.Collections.Concurrent;
using System.Net;
using System.Text;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>One market-index call per commodity, the replies joined by station.</summary>
public class SeveralCommoditiesAreJoinedByStationTests
{
    private const string Sol =
        """{"systemName":"Sol","systemX":0,"systemY":0,"systemZ":0}""";

    private const string Catalogue =
        """[{"commodityName":"gold"},{"commodityName":"painite"}]""";

    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Answers each commodity's nearby call with its own rows; safe under parallel calls.</summary>
    private sealed class Index(IReadOnlyDictionary<string, string> rows) : HttpMessageHandler
    {
        public ConcurrentQueue<string> Urls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.PathAndQuery;

            Urls.Enqueue(url);

            var body = url.Contains("/v2/commodities", StringComparison.Ordinal)
                ? Catalogue
                : url.Contains("/nearby/", StringComparison.Ordinal)
                    ? rows.First(pair => url.Contains($"/commodity/name/{pair.Key}/", StringComparison.Ordinal)).Value
                    : Sol;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static string Row(string commodity, string station, double x, int buy, int stock, string updated) =>
        $$"""
          {"commodityName":"{{commodity}}","stationName":"{{station}}","stationType":"Coriolis",
           "distanceToArrival":100,"maxLandingPadSize":3,"systemName":"{{station}} System",
           "systemX":{{x}},"systemY":0,"systemZ":0,"buyPrice":{{buy}},"demand":0,"sellPrice":{{buy - 100}},
           "stock":{{stock}},"updatedAt":"{{updated}}"}
          """;

    private static string Rows(params string[] rows) => "[" + string.Join(",", rows) + "]";

    private static async Task<CommodityAnswer> AskAsync(Index index)
    {
        using var commodities = new ArdentCommodityService(
            NullLogger<ArdentCommodityService>.Instance,
            new HttpClient(index) { BaseAddress = new Uri($"https://{ArdentCommodityService.Host}/") });

        using var service = new SpanshTradePlanService(
            NullLogger<SpanshTradePlanService>.Instance,
            commodities,
            book: null,
            new HttpClient(index) { BaseAddress = new Uri("https://spansh.co.uk/") },
            () => Now);

        return await service.FindCommodityAsync(
            new CommoditySearch("Sol", null, new CommodityQuery("Gold", More: [new("Painite", null)]), MaxPriceAge: 0),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnlyAStationInEveryReplyAnswersWithBothQuotesAndTheOlderDate()
    {
        var index = new Index(new Dictionary<string, string>
        {
            ["gold"] = Rows(
                Row("gold", "Both", 5, 9_000, 100, "2026-10-30T11:00:00.000Z"),
                Row("gold", "Gold only", 3, 8_000, 100, "2026-10-30T11:00:00.000Z")),
            ["painite"] = Rows(
                Row("painite", "Both", 5, 50_000, 100, "2026-10-29T11:00:00.000Z")),
        });

        var answer = await AskAsync(index);

        var offer = Assert.Single(answer.Offers);

        Assert.Equal("Both", offer.Market.Station);
        Assert.Equal([new CommodityLot("Gold", 9_000, 1), new CommodityLot("Painite", 50_000, 1)], offer.Lots);
        Assert.Equal(new DateTimeOffset(2026, 10, 29, 11, 0, 0, TimeSpan.Zero), offer.Market.UpdatedAt);
        Assert.Equal([("Gold", 2), ("Painite", 1)], answer.Stocked);
        Assert.True(answer.Complete);
        Assert.Contains(index.Urls, url => url.Contains("/commodity/name/gold/nearby/", StringComparison.Ordinal));
        Assert.Contains(index.Urls, url => url.Contains("/commodity/name/painite/nearby/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OneTruncatedReplyMakesTheJoinedAnswerIncomplete()
    {
        var gold = Rows([.. Enumerable.Range(0, ArdentCommodityService.RowCeiling)
            .Select(row => Row("gold", $"Station {row}", 1, 9_000, 100, "2026-10-30T11:00:00.000Z"))]);

        var index = new Index(new Dictionary<string, string>
        {
            ["gold"] = gold,
            ["painite"] = Rows(Row("painite", "Station 0", 1, 50_000, 100, "2026-10-30T11:00:00.000Z")),
        });

        var answer = await AskAsync(index);

        Assert.False(answer.Complete);
        Assert.Equal("Station 0", Assert.Single(answer.Offers).Market.Station);
    }
}
