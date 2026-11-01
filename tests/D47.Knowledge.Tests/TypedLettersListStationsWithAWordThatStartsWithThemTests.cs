using System.Net;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public class TypedLettersListStationsWithAWordThatStartsWithThemTests
{
    private const string WheelAnswer =
        """
        {"min_max":[{"name":"Led's Wheel","market_id":3228345600,"system_id64":3238296097059,"system_x":-9.5,"system_y":-39.0,"system_z":126.25},
        {"name":"No Market","system_id64":1,"system_x":0.0,"system_y":0.0,"system_z":0.0},
        {"name":"The Wheel","market_id":128666762,"system_id64":2,"system_x":1.0,"system_y":2.0,"system_z":3.0}]}
        """;

    private sealed class Answer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add(request.RequestUri!);
            }

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static SpanshStarSystemService Service(Answer answer) =>
        new(NullLogger<SpanshStarSystemService>.Instance, new HttpClient(answer));

    [Fact]
    public async Task EveryStationIsReadWithItsMarketSystemAndPositionInSpanshsOrder()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, WheelAnswer));

        var matches = await service.StationNamesAsync("wheel", TestContext.Current.CancellationToken);

        Assert.Equal(2, matches.Count);
        Assert.Equal(
            new StationNameMatch("Led's Wheel", 3228345600, 3238296097059, new StarPosition(-9.5, -39.0, 126.25)),
            matches[0]);
        Assert.Equal("The Wheel", matches[1].Name);
    }

    [Fact]
    public async Task TheTextIsAskedOfSpanshsStationNameLookup()
    {
        var answer = new Answer(HttpStatusCode.OK, WheelAnswer);
        using var service = Service(answer);

        await service.StationNamesAsync(" Dark Wheel ", TestContext.Current.CancellationToken);

        var request = Assert.Single(answer.Requests);
        Assert.Equal("/api/stations/field_values/name", request.AbsolutePath);
        Assert.Equal("?q=Dark%20Wheel", request.Query);
    }

    [Fact]
    public async Task TheSameTextInAnyCaseIsAskedOnce()
    {
        var answer = new Answer(HttpStatusCode.OK, WheelAnswer);
        using var service = Service(answer);
        var token = TestContext.Current.CancellationToken;

        await service.StationNamesAsync("Wheel", token);
        await service.StationNamesAsync("wheel", token);

        Assert.Single(answer.Requests);
    }

    [Fact]
    public async Task BlankTextAsksNothing()
    {
        var answer = new Answer(HttpStatusCode.OK, WheelAnswer);
        using var service = Service(answer);

        Assert.Empty(await service.StationNamesAsync("   ", TestContext.Current.CancellationToken));
        Assert.Empty(answer.Requests);
    }

    [Fact]
    public async Task ARefusalIsNotKeptAndSurfacesAsUnavailable()
    {
        var answer = new Answer(HttpStatusCode.TooManyRequests, WheelAnswer);
        using var service = Service(answer);
        var token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<GalaxyUnavailableException>(() => service.StationNamesAsync("wheel", token));
        await Assert.ThrowsAsync<GalaxyUnavailableException>(() => service.StationNamesAsync("wheel", token));

        Assert.Equal(2, answer.Requests.Count);
    }
}
