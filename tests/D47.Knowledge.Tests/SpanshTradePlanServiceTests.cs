using System.Net;
using System.Text;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// The half of Phase 36 that is not arithmetic: asking for markets, asking for them as few times
/// as possible, and preferring the Commander's own prices where they are newer than the report.
/// </summary>
public class SpanshTradePlanServiceTests
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

    private static SpanshTradePlanService Service(Recorder recorder, MarketBook? book = null) =>
        new(
            NullLogger<SpanshTradePlanService>.Instance,
            book,
            new HttpClient(recorder) { BaseAddress = new Uri("https://spansh.co.uk/") },
            () => new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// Two stations twenty light years apart, both carrying their whole market — which is the
    /// measured fact the phase rests on.
    /// </summary>
    private const string TwoMarkets =
        """
        {"count":2,"results":[
          {"name":"Abraham Lincoln","system_name":"Sol","system_x":0,"system_y":0,"system_z":0,
           "distance_to_arrival":499,"has_large_pad":true,"type":"Ocellus Starport",
           "market_updated_at":"2026-08-18T10:00:00Z",
           "market":[{"commodity":"Gold","buy_price":9400,"sell_price":9300,"supply":13148,"demand":0,
                      "is_rare":false}]},
          {"name":"Diaz Chemical Holdings","system_name":"RR Caeli","system_x":20,"system_y":0,"system_z":0,
           "distance_to_arrival":120,"has_large_pad":true,"type":"Coriolis Starport",
           "market_updated_at":"2026-08-19T09:00:00Z",
           "market":[{"commodity":"Gold","buy_price":0,"sell_price":52282,"supply":0,"demand":1200,
                      "is_rare":false}]}
        ]}
        """;

    private static TradeQuery Query(long capital = 50_000_000, int hold = 384)
    {
        Assert.True(TradeQuery.TryParse(
            "Sol", "Abraham Lincoln", capital, hold, 1, 40, 1_000, false, 720, false, out var query, out _));

        return query;
    }

    [Fact]
    public async Task TheMarketsComeBackWithTheStationsAndAreRoutedOverHere()
    {
        var recorder = new Recorder((HttpStatusCode.OK, TwoMarkets));
        using var service = Service(recorder);

        var route = await service.PlanAsync(Query(), TestContext.Current.CancellationToken);

        Assert.NotNull(route);
        Assert.Equal(2, route.Stops.Count);
        Assert.Equal("Diaz Chemical Holdings", route.Stops[^1].Station);

        // 384 tonnes at 9,400 out and 52,282 back. The arithmetic is d47's; only the prices came
        // from anybody else.
        Assert.Equal(384 * (52_282L - 9_400L), route.TotalProfit);

        // A short page is the last page. Asking for the next one costs another second and returns
        // nothing, so two results against a page of fifty stop the paging.
        Assert.Single(recorder.Requests);
        Assert.Contains("\"reference_system\":\"Sol\"", recorder.Requests[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsecondPlanAMinuteLaterDoesNotAskAgain()
    {
        // Measured 2026-08-19: a station search answers in 1.1 to 1.3 seconds whatever it returns,
        // so the bill is the number of requests. A planner that re-fetches what it already holds
        // is the only way this gets slow.
        var recorder = new Recorder((HttpStatusCode.OK, TwoMarkets));
        using var service = Service(recorder);

        await service.PlanAsync(Query(), TestContext.Current.CancellationToken);
        await service.PlanAsync(Query(), TestContext.Current.CancellationToken);

        Assert.Single(recorder.Requests);
    }

    [Fact]
    public async Task ThePricesTheCommanderReadThemselvesWinWhenTheyAreNewer()
    {
        // Local, exact and free, against a report from somebody who docked there yesterday. The
        // rule is newer-wins rather than mine-wins: a report from this morning beats what they saw
        // a month ago.
        using var install = new TempFile();
        var book = new MarketBook(install.Path, NullLogger.Instance);

        book.Remember(new MarketSnapshot
        {
            Station = "Abraham Lincoln",
            System = "Sol",
            UpdatedAt = new DateTimeOffset(2026, 8, 19, 11, 0, 0, TimeSpan.Zero),
            Source = PriceSource.Seen,
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                ["Gold"] = new("Gold") { BuyPrice = 5_000, Supply = 13_148 },
            },
        });

        var recorder = new Recorder((HttpStatusCode.OK, TwoMarkets));
        using var service = Service(recorder, book);

        var route = await service.PlanAsync(Query(), TestContext.Current.CancellationToken);

        Assert.NotNull(route);
        Assert.True(route.Stops[0].PricesAreYours);
        Assert.Equal(5_000, Assert.Single(route.Stops[0].Buy).UnitPrice);
        Assert.Equal(1, route.MarketsSeenInPerson);

        // The report's coordinates are kept even when its prices are not: Market.json carries no
        // position, and a market that cannot be placed cannot be routed to.
        Assert.Equal(384 * (52_282L - 5_000L), route.TotalProfit);
    }

    [Fact]
    public async Task APriceOlderThanTheCommanderAskedForIsNotPlannedWith()
    {
        var stale = TwoMarkets.Replace("2026-08-19T09:00:00Z", "2019-01-01T00:00:00Z", StringComparison.Ordinal);

        var recorder = new Recorder((HttpStatusCode.OK, stale));
        using var service = Service(recorder);

        var route = await service.PlanAsync(Query(), TestContext.Current.CancellationToken);

        // The destination aged out, so there is nowhere to sell and no route — which is an answer
        // rather than a failure.
        Assert.NotNull(route);
        Assert.Empty(route.Stops);
        Assert.Equal(1, route.MarketsConsidered);
    }

    [Fact]
    public async Task AnOutageIsASentenceRatherThanAnException()
    {
        var recorder = new Recorder((HttpStatusCode.TooManyRequests, "{}"));
        using var service = Service(recorder);

        var failure = await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.PlanAsync(Query(), TestContext.Current.CancellationToken));

        Assert.Contains("rate limiting", failure.Message, StringComparison.Ordinal);
    }

    private sealed class TempFile : IDisposable
    {
        public TempFile()
        {
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d47-tests");

            Directory.CreateDirectory(folder);

            Path = System.IO.Path.Combine(folder, Guid.NewGuid().ToString("N") + ".json");
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                File.Delete(Path);
            }
            catch (IOException)
            {
                // A leftover temp file is not worth failing a test over.
            }
        }
    }
}
