using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// The horizon a commodity search actually examines, and the radius it is allowed to speak for
/// (#156).
/// <para>
/// Reported 2026-08-28. Asked for the closest place to buy 200 Landmines near Eurybia — the Liz
/// Ryder tribute — and told, twice, with rising confidence: <i>"No stock of Land Mines for 200
/// tonnes within 150 ly of Eurybia…"</i> and then <i>"No buyer for Land Mines in that lot size out
/// to 250 ly of Eurybia."</i> Measured against d47's own data source the same hour, Coleman Relay
/// in Enayex had 5,229 units 11 light years away. <b>The answer was wrong by a factor of twenty.</b>
/// </para>
/// <para>
/// <b>The mechanism: the radius answered was not the radius searched.</b> The sweep fetched the
/// nearest 150 stations by distance and nothing else — three pages of fifty, sorted ascending, no
/// commodity filter — and the ranking then ran locally over whatever those 150 happened to stock.
/// Near a bubble system the nearest 150 markets span a few light years, so a commodity none of
/// them carried was reported absent from the entire 250 light year radius.
/// </para>
/// <para>
/// <b>Measured against the live service on 2026-08-28</b>, at a radius small enough to be under
/// the endpoint's 10,000 result cap, because the cap is what made the first reading of this
/// ambiguous. Within 15 light years of Eurybia: 449 stations unfiltered, 26 carrying a Landmines
/// row at all, <b>8 with supply of at least one</b> and <b>12 with demand of at least one</b>. So
/// the name alone <em>is</em> honoured — the issue's reading of it as silently ignored came from
/// a count pinned at the cap — and the bound is what separates a station that stocks the thing
/// from one that merely lists it. <b>And demand bounds are honoured here</b>, which is the sell
/// side's open question answered: the note on <c>CommodityMarketSearch</c> recording them as
/// accepted-and-ignored was measured on the trade endpoint, which is a different one.
/// </para>
/// </summary>
public class TheCommoditySearchAsksForTheCommodityTests
{
    private sealed class Recorder(params (HttpStatusCode Status, string Body)[] answers) : HttpMessageHandler
    {
        private int _next;

        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                Requests.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            var (status, body) = answers[Math.Min(_next++, answers.Length - 1)];

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static SpanshTradePlanService Service(Recorder recorder) =>
        new(
            NullLogger<SpanshTradePlanService>.Instance,
            book: null,
            new HttpClient(recorder) { BaseAddress = new Uri("https://spansh.co.uk/") },
            () => new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));

    /// <summary>The day's own answer, as the live search returned it.</summary>
    private const string ColemanRelay =
        """
        {"count":6813,
         "reference":{"id64":1458309141194,"name":"Eurybia","x":51.40625,"y":-54.40625,"z":-30.5},
         "results":[
          {"name":"Coleman Relay","system_name":"Enayex","system_x":51.40625,"system_y":-54.40625,"system_z":-19.5,
           "distance_to_arrival":210,"has_large_pad":true,"type":"Coriolis Starport",
           "market_updated_at":"2026-08-28T09:00:00Z",
           "market":[{"commodity":"Landmines","buy_price":444,"sell_price":430,"supply":5229,"demand":1,
                      "is_rare":false}]}
        ]}
        """;

    private static CommoditySearch Buying(int tonnes = 200, double radius = 250) =>
        new(
            "Eurybia",
            Station: null,
            new CommodityQuery("Landmines", TradeSide.Buying, tonnes, radius));

    private static JsonElement Body(string request) => JsonDocument.Parse(request).RootElement;

    /// <summary>
    /// The reported question, and the answer it should always have had.
    /// </summary>
    [Fact]
    public async Task TheReportedQuestionAnswersColemanRelay()
    {
        var recorder = new Recorder((HttpStatusCode.OK, ColemanRelay));
        using var service = Service(recorder);

        var answer = await service.FindCommodityAsync(Buying(), TestContext.Current.CancellationToken);

        var best = Assert.Single(answer.Offers);

        Assert.Equal("Coleman Relay", best.Market.Station);
        Assert.Equal("Enayex", best.Market.System);
        Assert.Equal(200, best.Tonnes);
        Assert.Equal(11, best.Distance, 1);

        // And the distance is real, which it would not be if the origin had been looked for only
        // among the results: a commodity-filtered sweep has no reason to return Eurybia's own
        // stations, and the ranking silently falls back to price when it cannot place the origin.
        Assert.True(answer.OriginKnown);
    }

    /// <summary>
    /// The request itself: the commodity is named and a bound is attached to it.
    /// </summary>
    [Fact]
    public async Task ANamedCommoditySearchSendsTheMarketFilter()
    {
        var recorder = new Recorder((HttpStatusCode.OK, ColemanRelay));
        using var service = Service(recorder);

        await service.FindCommodityAsync(Buying(), TestContext.Current.CancellationToken);

        var market = Body(recorder.Requests[0]).GetProperty("filters").GetProperty("market");
        var wanted = market[0];

        Assert.Equal("Landmines", wanted.GetProperty("name").GetString());
        Assert.Equal("1", wanted.GetProperty("supply").GetProperty("value")[0].GetString());
        Assert.Equal("<=>", wanted.GetProperty("supply").GetProperty("comparison").GetString());
    }

    /// <summary>
    /// <b>Never the name-only shape.</b> It filters — that was measured — but it matches every
    /// station <em>listing</em> the commodity, supply and demand both zero included, which is most
    /// of them: 26 within 15 light years of Eurybia against 8 that actually had any. Spending the
    /// 150-station budget on shelves that are empty is the same defect with a smaller factor on it.
    /// </summary>
    [Fact]
    public async Task TheNameOnlyShapeIsNeverSent()
    {
        var recorder = new Recorder((HttpStatusCode.OK, ColemanRelay));
        using var service = Service(recorder);

        await service.FindCommodityAsync(Buying(), TestContext.Current.CancellationToken);

        foreach (var request in recorder.Requests)
        {
            var body = Body(request);

            if (!body.GetProperty("filters").TryGetProperty("market", out var market))
            {
                continue;
            }

            foreach (var wanted in market.EnumerateArray())
            {
                Assert.True(
                    wanted.TryGetProperty("supply", out _) || wanted.TryGetProperty("demand", out _),
                    $"A market filter with no bound on it matches empty shelves: {request}");
            }
        }
    }

    /// <summary>
    /// The sell side, wired the same way because the endpoint honours it — probed rather than
    /// assumed, since the note this contradicts was measured on a different endpoint.
    /// </summary>
    [Fact]
    public async Task SellingBoundsTheDemandInstead()
    {
        var recorder = new Recorder((HttpStatusCode.OK, ColemanRelay));
        using var service = Service(recorder);

        await service.FindCommodityAsync(
            new CommoditySearch("Eurybia", null, new CommodityQuery("Landmines", TradeSide.Selling, 200)),
            TestContext.Current.CancellationToken);

        var wanted = Body(recorder.Requests[0]).GetProperty("filters").GetProperty("market")[0];

        Assert.True(wanted.TryGetProperty("demand", out var demand));
        Assert.False(wanted.TryGetProperty("supply", out _));
        Assert.Equal("1", demand.GetProperty("value")[0].GetString());
    }

    /// <summary>
    /// <b>And the two sweeps that had no commodity to filter by are untouched.</b> The general
    /// sweep is right to fetch the nearest markets and rank locally — trade planning compares
    /// every commodity against every other, and a build asks about twenty at once, so neither has
    /// one name to narrow by. This is the assertion that stops a later tidy-up applying the fix
    /// where it does not belong.
    /// </summary>
    [Fact]
    public async Task ThePlannerAndColonisationSweepsSendNoMarketFilter()
    {
        var recorder = new Recorder((HttpStatusCode.OK, ColemanRelay));
        using var service = Service(recorder);

        Assert.True(TradeQuery.TryParse(
            "Eurybia", "Coleman Relay", 50_000_000, 384, 1, 40, 1_000, false, 720, false, out var trade, out var why), why);

        await service.PlanAsync(trade, TestContext.Current.CancellationToken);

        await service.SourceConstructionAsync(
            new SourcingSearch(
                "Eurybia",
                null,
                [new D47.Core.Journal.ConstructionResource("Land Mines", 200, 0)]),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(recorder.Requests);

        foreach (var request in recorder.Requests)
        {
            Assert.False(
                Body(request).GetProperty("filters").TryGetProperty("market", out _),
                $"The general sweep must stay general: {request}");
        }
    }

    /// <summary>
    /// A filtered sweep and a general one are different questions, and neither cache may answer
    /// the other's — which is #156 arriving by the back door.
    /// </summary>
    [Fact]
    public async Task AGeneralSweepsCacheDoesNotAnswerANamedCommoditySearch()
    {
        var recorder = new Recorder((HttpStatusCode.OK, ColemanRelay));
        using var service = Service(recorder);

        Assert.True(TradeQuery.TryParse(
            "Eurybia", "Coleman Relay", 50_000_000, 384, 1, 250, 1_000, false, 720, false, out var trade, out var why), why);

        await service.PlanAsync(trade, TestContext.Current.CancellationToken);

        var afterPlanning = recorder.Requests.Count;

        await service.FindCommodityAsync(Buying(), TestContext.Current.CancellationToken);

        Assert.True(
            recorder.Requests.Count > afterPlanning,
            "The commodity search reused the planner's unfiltered sweep, which is the defect.");

        Assert.True(Body(recorder.Requests[^1]).GetProperty("filters").TryGetProperty("market", out _));
    }

    /// <summary>
    /// <b>A negative answer says the horizon it actually examined.</b> When the page budget runs
    /// out before the radius does, "nothing within 250 light years" is a claim about ground that
    /// was never covered — which is the sentence the Commander was given.
    /// </summary>
    [Fact]
    public async Task ASweepThatRanOutOfBudgetReportsHowFarItReached()
    {
        // A full page every time, so the sweep spends its whole budget and never sees the end of
        // the radius. Nothing here stocks what was asked for.
        var page = Full(50);

        var recorder = new Recorder((HttpStatusCode.OK, page));
        using var service = Service(recorder);

        var answer = await service.FindCommodityAsync(
            new CommoditySearch("Eurybia", null, new CommodityQuery("Landmines", TradeSide.Buying, 200, 250)),
            TestContext.Current.CancellationToken);

        Assert.Empty(answer.Offers);
        Assert.NotNull(answer.Horizon);
        Assert.Equal(14, answer.Horizon!.Value, 1);
    }

    /// <summary>
    /// And a search that reached the end of its radius says so by saying nothing: the radius is
    /// what it can speak for, and that is the common case.
    /// </summary>
    [Fact]
    public async Task ASweepThatReachedTheEndOfTheRadiusClaimsTheRadius()
    {
        var recorder = new Recorder((HttpStatusCode.OK, ColemanRelay));
        using var service = Service(recorder);

        var answer = await service.FindCommodityAsync(Buying(), TestContext.Current.CancellationToken);

        Assert.Null(answer.Horizon);
    }

    /// <summary>Fifty stations in a line out to fourteen light years, none stocking Landmines.</summary>
    private static string Full(int count)
    {
        var results = string.Join(
            ",",
            Enumerable.Range(0, count).Select(i =>
                $$"""
                  {"name":"Station {{i}}","system_name":"System {{i}}",
                   "system_x":51.40625,"system_y":-54.40625,"system_z":{{(-30.5 + (14.0 * i / (count - 1))).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}},
                   "has_large_pad":true,"type":"Outpost","market_updated_at":"2026-08-28T09:00:00Z",
                   "market":[{"commodity":"Gold","buy_price":9400,"sell_price":9300,"supply":100,"demand":0,"is_rare":false}]}
                  """));

        return $$"""
                 {"count":10000,
                  "reference":{"id64":1458309141194,"name":"Eurybia","x":51.40625,"y":-54.40625,"z":-30.5},
                  "results":[{{results}}]}
                 """;
    }
}
