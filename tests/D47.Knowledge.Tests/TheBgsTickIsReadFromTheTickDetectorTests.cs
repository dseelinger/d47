using System.Net;
using System.Net.Http.Headers;
using System.Text;
using D47.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public class TheBgsTickIsReadFromTheTickDetectorTests
{
    private const string Answer = """{"lastGalaxyTick":"2026-10-15T16:14:56.000Z"}""";

    private static readonly DateTimeOffset Tick = new(2026, 10, 15, 16, 14, 56, TimeSpan.Zero);

    private sealed class Detector(params Func<HttpRequestMessage, HttpResponseMessage>[] answers) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(answers[Requests.Count - 1](request));
        }
    }

    private static HttpResponseMessage Ok(string body, string? etag = "W/\"1a\"")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (etag is not null)
        {
            response.Headers.ETag = EntityTagHeaderValue.Parse(etag);
        }

        return response;
    }

    private static InfomancerBgsTickSource Source(Detector detector) =>
        new(NullLogger<InfomancerBgsTickSource>.Instance, detector);

    [Fact]
    public async Task ItAsksTheOneDisclosedHostOverPlainHttpAsD47()
    {
        var detector = new Detector(_ => Ok(Answer));
        using var source = Source(detector);

        Assert.Equal(Tick, await source.LastTickAsync(CancellationToken.None));

        var request = Assert.Single(detector.Requests);
        Assert.Equal(new Uri("http://tick.infomancer.uk/galtick.json"), request.RequestUri);
        Assert.Equal(EgressDisclosure.BgsTickHost, InfomancerBgsTickSource.Host);
        Assert.Equal("d47", request.Headers.UserAgent.ToString());
        Assert.Empty(request.Headers.IfNoneMatch);
    }

    [Fact]
    public async Task TheSecondRequestSendsTheETagAndANotModifiedKeepsTheTime()
    {
        var detector = new Detector(_ => Ok(Answer), _ => new HttpResponseMessage(HttpStatusCode.NotModified));
        using var source = Source(detector);

        await source.LastTickAsync(CancellationToken.None);
        var second = await source.LastTickAsync(CancellationToken.None);

        Assert.Equal(Tick, second);
        Assert.Equal("W/\"1a\"", Assert.Single(detector.Requests[1].Headers.IfNoneMatch).ToString());
    }

    [Fact]
    public async Task AnUnusableAnswerKeepsNoETag()
    {
        var detector = new Detector(_ => Ok("""{"lastGalaxyTick":"soon"}"""), _ => Ok(Answer));
        using var source = Source(detector);

        Assert.Null(await source.LastTickAsync(CancellationToken.None));
        await source.LastTickAsync(CancellationToken.None);

        Assert.Empty(detector.Requests[1].Headers.IfNoneMatch);
    }

    [Fact]
    public async Task AnErrorStatusIsNoTime()
    {
        var detector = new Detector(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        using var source = Source(detector);

        Assert.Null(await source.LastTickAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AnAnswerOverFourKilobytesIsNoTime()
    {
        var padded = $$"""{"lastGalaxyTick":"2026-10-15T16:14:56.000Z","pad":"{{new string('x', 4200)}}"}""";
        var detector = new Detector(_ => Ok(padded));
        using var source = Source(detector);

        Assert.Null(await source.LastTickAsync(CancellationToken.None));
    }

    [Fact]
    public void TheRequestFollowsNoRedirectAndCarriesNoCookie()
    {
        using var handler = InfomancerBgsTickSource.Handler();

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
    }

    [Fact]
    public async Task ARedirectIsNoTime()
    {
        var detector = new Detector(_ => new HttpResponseMessage(HttpStatusCode.Found)
        {
            Headers = { Location = new Uri("http://192.168.0.1/") },
        });
        using var source = Source(detector);

        Assert.Null(await source.LastTickAsync(CancellationToken.None));
        Assert.Single(detector.Requests);
    }

    [Theory]
    [InlineData("""{"lastGalaxyTick":"2026-10-15T16:14:56.000Z"}""")]
    [InlineData("""{"lastGalaxyTick":"2026-10-15T16:14:56Z"}""")]
    [InlineData("""{"lastGalaxyTick":"2026-10-15T16:14:56+00:00"}""")]
    public void AUtcTimeIsRead(string body) =>
        Assert.Equal(Tick, InfomancerBgsTickSource.Read(Encoding.UTF8.GetBytes(body)));

    [Theory]
    [InlineData("<html>not json</html>")]
    [InlineData("""["2026-10-15T16:14:56.000Z"]""")]
    [InlineData("""{"tick":"2026-10-15T16:14:56.000Z"}""")]
    [InlineData("""{"lastGalaxyTick":1760544896}""")]
    [InlineData("""{"lastGalaxyTick":"2026-10-15T16:14:56"}""")]
    [InlineData("""{"lastGalaxyTick":"2026-10-15T18:14:56+02:00"}""")]
    [InlineData("""{"lastGalaxyTick":"15 October 2026 16:14"}""")]
    public void AnythingElseIsNoTime(string body) =>
        Assert.Null(InfomancerBgsTickSource.Read(Encoding.UTF8.GetBytes(body)));
}
