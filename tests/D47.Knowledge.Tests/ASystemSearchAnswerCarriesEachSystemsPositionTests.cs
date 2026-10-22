using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Journal;
using D47.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>A system search answer gives each system's position, which the galaxy map draws unvisited stars from (#1409).</summary>
public class ASystemSearchAnswerCarriesEachSystemsPositionTests
{
    [Fact]
    public void ASystemWithXYAndZHasAPosition()
    {
        using var document = JsonDocument.Parse("""
            {"count":2,"reference":{"name":"Sol","x":0,"y":0,"z":0},"results":[
              {"id64":1,"name":"Alpha Centauri","distance":4.38,"x":3.03125,"y":-0.09375,"z":3.15625},
              {"id64":2,"name":"Barnard's Star","distance":5.95,"x":-3.03125,"y":1.375,"z":4.9375}]}
            """);

        var systems = SpanshResponse.ReadSearch(document).Systems;

        Assert.Equal(new StarPosition(3.03125, -0.09375, 3.15625), systems[0].Position);
        Assert.Equal(new StarPosition(-3.03125, 1.375, 4.9375), systems[1].Position);
    }

    [Fact]
    public void ASystemWithoutAllThreeHasNone()
    {
        using var document = JsonDocument.Parse("""
            {"count":2,"results":[{"name":"Nowhere"},{"name":"Halfway","x":1,"y":2}]}
            """);

        Assert.All(SpanshResponse.ReadSearch(document).Systems, system => Assert.Null(system.Position));
    }

    [Fact]
    public void ARecordedNearbySearchHasAPositionForEverySystem()
    {
        using var document = Fixture.Json("spansh-powerplay-near-ltt-7786.json");

        Assert.All(SpanshResponse.ReadSearch(document).Systems, system => Assert.NotNull(system.Position));
    }

    private sealed class Answer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    [Fact]
    public async Task APositionIsTheReferenceTheServiceResolved()
    {
        using var service = new SpanshGalaxyService(
            NullLogger<SpanshGalaxyService>.Instance,
            new HttpClient(new Answer(HttpStatusCode.OK, """{"count":1,"reference":{"name":"LTT 7786","x":7.96875,"y":-41.90625,"z":77.875},"results":[]}""")));

        Assert.Equal(new StarPosition(7.96875, -41.90625, 77.875), await service.PositionAsync("LTT 7786", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ASystemTheServiceDoesNotKnowHasNoPosition()
    {
        using var service = new SpanshGalaxyService(
            NullLogger<SpanshGalaxyService>.Instance,
            new HttpClient(new Answer(HttpStatusCode.BadRequest, """{"error":"Could not find reference system"}""")));

        Assert.Null(await service.PositionAsync("Nowhere Known", TestContext.Current.CancellationToken));
    }
}
