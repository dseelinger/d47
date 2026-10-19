using System.Net;
using System.Text;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public class ASystemsBodiesComeFromSpanshTests
{
    private const string Dump =
        """
        {"system":{"id64":2869440816561,"name":"LHS 3593","bodies":[
          {"bodyId":0,"id64":2869440816561,"name":"LHS 3593","type":"Star","subType":"M (Red dwarf) Star","distanceToArrival":0},
          {"bodyId":9,"id64":324262042611492273,"name":"LHS 3593 1","type":"Planet","subType":"Class I gas giant",
           "distanceToArrival":444.7,"isLandable":false,"terraformingState":"Not terraformable","reserveLevel":"Common",
           "rings":[{"name":"LHS 3593 1 A Ring","type":"Icy",
                     "signals":{"signals":{"Alexandrite":1,"Bromellite":2},"updateTime":"2026-10-16T22:48:39Z"}}],
           "signals":{"signals":{"$SAA_SignalType_Human;":3},"updateTime":"2020-12-11T20:26:03Z"}},
          {"bodyId":10,"id64":396319636649420209,"name":"LHS 3593 2","type":"Planet","subType":"High metal content world",
           "distanceToArrival":900.1,"isLandable":true,"terraformingState":"Terraformable"}
        ]}}
        """;

    private const string System =
        """
        {"record":{"id64":2869440816561,"bodies":[
          {"id64":324262042611492273,"name":"LHS 3593 1","estimated_mapping_value":11756},
          {"id64":396319636649420209,"name":"LHS 3593 2","estimated_mapping_value":2221}
        ]}}
        """;

    private sealed class Answer(Func<string, (HttpStatusCode Status, string Body)> reply) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            var (status, body) = reply(request.RequestUri.AbsolutePath);

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static SpanshGalaxyService Service(Answer answer) =>
        new(
            NullLogger<SpanshGalaxyService>.Instance,
            new HttpClient(answer) { BaseAddress = new Uri("https://spansh.co.uk/") });

    [Fact]
    public async Task EachBodyCarriesItsRingsSignalsSubtypeTerraformingAndMappingValue()
    {
        var answer = new Answer(path => (HttpStatusCode.OK, path.StartsWith("/api/dump/", StringComparison.Ordinal) ? Dump : System));
        using var service = Service(answer);

        var bodies = await service.SystemBodiesAsync(2869440816561, TestContext.Current.CancellationToken);

        Assert.Equal(["/api/dump/2869440816561", "/api/system/2869440816561"], answer.Paths);
        Assert.Equal(3, bodies.Count);

        var giant = bodies[1];
        Assert.Equal("LHS 3593 1", giant.Name);
        Assert.Equal("LHS 3593", giant.SystemName);
        Assert.Equal(9, giant.BodyId);
        Assert.Equal("Class I gas giant", giant.Subtype);
        Assert.False(giant.IsLandable);
        Assert.Equal("Not terraformable", giant.TerraformingState);
        Assert.Equal(11756, giant.MappingValue);
        Assert.Equal([("Human", 3)], giant.Signals);

        var ring = Assert.Single(giant.Rings);
        Assert.Equal("Icy", ring.Type);
        Assert.Equal([("Alexandrite", 1), ("Bromellite", 2)], ring.Hotspots);

        var rock = bodies[2];
        Assert.True(rock.IsLandable);
        Assert.Equal("Terraformable", rock.TerraformingState);
        Assert.Equal(2221, rock.MappingValue);
        Assert.Empty(rock.Rings);
    }

    [Fact]
    public async Task ASystemWithoutAMappingRecordStillGivesItsBodies()
    {
        using var service = Service(new Answer(path => path.StartsWith("/api/dump/", StringComparison.Ordinal)
            ? (HttpStatusCode.OK, Dump)
            : (HttpStatusCode.NotFound, """{"error":"none"}""")));

        var bodies = await service.SystemBodiesAsync(2869440816561, TestContext.Current.CancellationToken);

        Assert.Equal(3, bodies.Count);
        Assert.All(bodies, body => Assert.Null(body.MappingValue));
    }

    [Fact]
    public async Task ASystemSpanshDoesNotKnowGivesNoBodies()
    {
        using var service = Service(new Answer(_ => (HttpStatusCode.NotFound, """{"error":"none"}""")));

        Assert.Empty(await service.SystemBodiesAsync(1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AServerErrorIsTheServiceBeingUnavailable()
    {
        using var service = Service(new Answer(_ => (HttpStatusCode.BadGateway, "{}")));

        await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.SystemBodiesAsync(1, TestContext.Current.CancellationToken));
    }
}
