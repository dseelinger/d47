using System.Net;
using System.Text;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>
/// A rare good is sold at one station, usually far outside the radius a nearby sweep covers, and the
/// quantity on offer per visit moves with the system's economic state. That quantity is read from
/// the one market's last report rather than held in a table (#116).
/// </summary>
public class ARaresQuantityIsTheStationsLastReportTests
{
    /// <summary>Lavian Brandy at Ega's own station, as the index gave it 2026-09-10.</summary>
    private const string LavianBrandy =
        """
        {"commodityName":"lavianbrandy","marketId":128106744,"buyPrice":3500,"demand":1,
         "demandBracket":0,"meanPrice":10365,"sellPrice":3500,"stock":24,"stockBracket":3,
         "updatedAt":"2026-09-11T00:23:26.000Z"}
        """;

    /// <summary>What the index answers for a market and commodity pair it has no report for.</summary>
    private const string NotFound =
        """{"error":"Not Found","message":"Market and/or commodity not found"}""";

    /// <summary>
    /// Enough of the catalogue to resolve a name, the one whose spelling is the trap among them: Elite
    /// writes Low Temperature Diamonds into the Commander's own <c>Market.json</c> and the index lists
    /// it singular.
    /// </summary>
    private const string Catalogue =
        """
        [{"commodityName":"gold"},{"commodityName":"lavianbrandy"},
         {"commodityName":"low temp. diamonds"},{"commodityName":"lowtemperaturediamond"},
         {"commodityName":"advanced catalysers"}]
        """;

    /// <summary>Answers the catalogue, then every market call the same way, remembering every URL.</summary>
    private sealed class Index(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.PathAndQuery;

            Urls.Add(url);

            var known = url.Contains("/v2/commodities", StringComparison.Ordinal);

            return Task.FromResult(new HttpResponseMessage(known ? HttpStatusCode.OK : status)
            {
                Content = new StringContent(
                    known ? Catalogue : body, Encoding.UTF8, "application/json"),
            });
        }
    }

    /// <summary>A body whose length the reply does not state, as a chunked or HTTP/2 reply does not.</summary>
    private sealed class Unmeasured(string body) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var bytes = Encoding.UTF8.GetBytes(body);

            await stream.WriteAsync(bytes);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;

            return false;
        }
    }

    /// <summary>The index answering the market call with a body it states no length for.</summary>
    private sealed class Unstated(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content =
                    request.RequestUri!.PathAndQuery.Contains("/v2/commodities", StringComparison.Ordinal)
                        ? new StringContent(Catalogue, Encoding.UTF8, "application/json")
                        : new Unmeasured(body),
            });
    }

    /// <summary>The index taking longer than the call's own budget, as HttpClient reports it.</summary>
    private sealed class TooSlow : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new TaskCanceledException("the request timed out");
    }

    private static ArdentCommodityService Commodities(HttpMessageHandler handler) =>
        new(
            NullLogger<ArdentCommodityService>.Instance,
            new HttpClient(handler) { BaseAddress = new Uri($"https://{ArdentCommodityService.Host}/") });

    [Fact]
    public async Task TheStationsOwnReportCarriesTheStockAndWhenItWasTaken()
    {
        var index = new Index(HttpStatusCode.OK, LavianBrandy);
        using var commodities = Commodities(index);

        var quote = await commodities.QuoteAsync(
            128106744, "Lavian Brandy", TestContext.Current.CancellationToken);

        Assert.NotNull(quote);
        Assert.Equal(24, quote.Stock);
        Assert.Equal(3, quote.StockBracket);
        Assert.Equal(3500, quote.BuyPrice);
        Assert.Equal(3500, quote.SellPrice);
        Assert.Equal(1, quote.Demand);
        Assert.Equal(new DateTimeOffset(2026, 9, 11, 0, 23, 26, TimeSpan.Zero), quote.UpdatedAt);
    }

    /// <summary>
    /// Elite writes Low Temperature Diamonds and the index lists lowtemperaturediamond. Folding the
    /// spelling the Commander used and asking for that asks about a commodity the index answers 404
    /// for, which reads back as a station with nothing on the board.
    /// </summary>
    [Fact]
    public async Task ASpokenPluralReachesTheSpellingTheIndexKeeps()
    {
        var index = new Index(HttpStatusCode.OK, LavianBrandy);
        using var commodities = Commodities(index);

        await commodities.QuoteAsync(
            4308700419, "Low Temperature Diamonds", TestContext.Current.CancellationToken);

        Assert.Equal("/v2/market/4308700419/commodity/name/lowtemperaturediamond", index.Urls[^1]);
    }

    /// <summary>
    /// This endpoint matches on letters and digits alone, so the catalogue's answer is folded before it
    /// is sent rather than escaped as the nearby search escapes it.
    /// </summary>
    [Fact]
    public async Task ACatalogueNameWithSpacesInItIsFoldedBeforeItIsSent()
    {
        var index = new Index(HttpStatusCode.OK, LavianBrandy);
        using var commodities = Commodities(index);

        await commodities.QuoteAsync(
            4308700419, "Advanced Catalysers", TestContext.Current.CancellationToken);

        Assert.Equal("/v2/market/4308700419/commodity/name/advancedcatalysers", index.Urls[^1]);
    }

    /// <summary>
    /// A commodity the index has never heard of is the caller's mistake, not a station with nothing on
    /// the board, and the two must not arrive as the same answer.
    /// </summary>
    [Fact]
    public async Task ACommodityTheIndexDoesNotListSaysSoRatherThanReadingAsAnEmptyMarket()
    {
        using var commodities = Commodities(new Index(HttpStatusCode.OK, LavianBrandy));

        var failure = await Assert.ThrowsAsync<GalaxyUnavailableException>(() =>
            commodities.QuoteAsync(128106744, "Unobtainium", TestContext.Current.CancellationToken));

        Assert.Equal("The market index has no commodity called \"Unobtainium\".", failure.Message);
    }

    [Fact]
    public async Task AMarketTheIndexHasNoReportForIsAnAbsentQuoteRatherThanAFailure()
    {
        using var commodities = Commodities(new Index(HttpStatusCode.NotFound, NotFound));

        Assert.Null(await commodities.QuoteAsync(1, "gold", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An empty body is an absent quote however the reply is framed. The emptiness is read off
    /// Content-Length, which holds for a reply that states no length — chunked, or HTTP/2 — because
    /// the default completion reads the body whole before the length is asked for, and the buffer's
    /// own length answers.
    /// </summary>
    [Fact]
    public async Task AnEmptyBodyIsAnAbsentQuoteEvenWhereTheReplyStatesNoLength()
    {
        using var commodities = Commodities(new Unstated(string.Empty));

        Assert.Null(await commodities.QuoteAsync(
            128106744, "lavianbrandy", TestContext.Current.CancellationToken));
    }

    /// <summary>And a reply that states no length but does carry figures is read as it always was.</summary>
    [Fact]
    public async Task AReplyThatStatesNoLengthIsStillRead()
    {
        using var commodities = Commodities(new Unstated(LavianBrandy));

        var quote = await commodities.QuoteAsync(
            128106744, "lavianbrandy", TestContext.Current.CancellationToken);

        Assert.Equal(24, quote?.Stock);
    }

    /// <summary>A failure to reach the index is a failure, not a report that the market is empty.</summary>
    [Fact]
    public async Task AnIndexThatDoesNotAnswerInTimeSaysSoRatherThanAnsweringNothing()
    {
        using var commodities = Commodities(new TooSlow());

        var failure = await Assert.ThrowsAsync<GalaxyUnavailableException>(() =>
            commodities.QuoteAsync(128106744, "lavianbrandy", TestContext.Current.CancellationToken));

        Assert.Equal("The market search took too long to answer.", failure.Message);
    }

    [Fact]
    public async Task ACommodityWithNoLettersInItReachesNoMarketAtAll()
    {
        var index = new Index(HttpStatusCode.OK, LavianBrandy);
        using var commodities = Commodities(index);

        await Assert.ThrowsAsync<GalaxyUnavailableException>(() =>
            commodities.QuoteAsync(128106744, "  ", TestContext.Current.CancellationToken));

        Assert.Empty(index.Urls);
    }
}
