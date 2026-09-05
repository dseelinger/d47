using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// The Commander's supply floor reaches the station search (#296), which honours it: measured
/// 2026-09-05 within 40 ly of Ega, Palladium at supply ≥ 1 returned 347 stations, ≥ 1,000 returned
/// 80, ≥ 10,000 returned 34 and ≥ 50,000 returned 4, against 7,597 unfiltered.
/// </summary>
public class TheSupplyFloorGoesIntoTheRequestTests
{
    private sealed class Recorder : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"count":0,"reference":{"id64":1,"name":"Ega","x":0,"y":0,"z":0},"results":[]}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }

    private static SpanshTradePlanService Service(Recorder recorder) =>
        new(
            NullLogger<SpanshTradePlanService>.Instance,
            book: null,
            new HttpClient(recorder) { BaseAddress = new Uri("https://spansh.co.uk/") },
            () => new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));

    private static string Bound(string request, string side)
    {
        var market = JsonDocument.Parse(request).RootElement
            .GetProperty("filters").GetProperty("market")[0];

        return market.GetProperty(side).GetProperty("value")[0].GetString()!;
    }

    [Fact]
    public async Task TheFloorIsTheLowerBoundOfTheSupplyRange()
    {
        var recorder = new Recorder();
        using var service = Service(recorder);

        await service.FindCommodityAsync(
            new CommoditySearch("Ega", null, new CommodityQuery("Palladium", MinAvailable: 10_000)),
            TestContext.Current.CancellationToken);

        Assert.Equal("10000", Bound(recorder.Requests[0], "supply"));
    }

    [Fact]
    public async Task SellingPutsTheFloorOnDemand()
    {
        var recorder = new Recorder();
        using var service = Service(recorder);

        await service.FindCommodityAsync(
            new CommoditySearch("Ega", null, new CommodityQuery("Palladium", TradeSide.Selling, MinAvailable: 500)),
            TestContext.Current.CancellationToken);

        Assert.Equal("500", Bound(recorder.Requests[0], "demand"));
    }

    [Fact]
    public async Task WithoutAFloorTheBoundIsOneAsItAlwaysWas()
    {
        var recorder = new Recorder();
        using var service = Service(recorder);

        await service.FindCommodityAsync(
            new CommoditySearch("Ega", null, new CommodityQuery("Palladium")),
            TestContext.Current.CancellationToken);

        Assert.Equal("1", Bound(recorder.Requests[0], "supply"));
    }

    [Fact]
    public async Task AHigherFloorIsNotAnsweredFromALowerFloorsCache()
    {
        var recorder = new Recorder();
        using var service = Service(recorder);

        await service.FindCommodityAsync(
            new CommoditySearch("Ega", null, new CommodityQuery("Palladium", MinAvailable: 10_000)),
            TestContext.Current.CancellationToken);

        // A lower floor is a superset of the sweep just made: no second request.
        await service.FindCommodityAsync(
            new CommoditySearch("Ega", null, new CommodityQuery("Palladium", MinAvailable: 10_000)),
            TestContext.Current.CancellationToken);

        Assert.Single(recorder.Requests);

        // A lower floor asks a wider question, so it goes to the wire.
        await service.FindCommodityAsync(
            new CommoditySearch("Ega", null, new CommodityQuery("Palladium", MinAvailable: 100)),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, recorder.Requests.Count);
        Assert.Equal("100", Bound(recorder.Requests[1], "supply"));
    }
}
