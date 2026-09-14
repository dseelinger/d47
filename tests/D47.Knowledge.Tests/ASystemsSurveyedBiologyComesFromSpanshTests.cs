using System.Net;
using System.Text;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>Against an <c>api/system</c> response captured from the live service on 2026-09-14.</summary>
public class ASystemsSurveyedBiologyComesFromSpanshTests
{
    private const long BluaEaec = 77862488579746;

    private sealed class Answer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);

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

    private static string Captured() =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "spansh-system-blua-eaec-je-u-c18-283.json"));

    [Fact]
    public async Task TheCapturedSystemGivesItsTwoSurveyedBodiesWithTheirSpecies()
    {
        var answer = new Answer(HttpStatusCode.OK, Captured());
        using var service = Service(answer);

        var biology = await service.SystemBiologyAsync(BluaEaec, TestContext.Current.CancellationToken);

        Assert.Equal(["/api/system/77862488579746"], answer.Paths);
        Assert.Equal(BluaEaec, biology.SystemAddress);
        Assert.Collection(
            biology.Bodies,
            b =>
            {
                Assert.Equal("Blua Eaec JE-U c18-283 3 b", b.Name);
                Assert.Equal(48, b.BodyId);
                Assert.Equal(15_214_300, b.LandmarkValue);
                Assert.Equal(
                    [
                        new ExobiologySpecies("Cactoida", "Cactoida Lapis", 1, 2_483_600),
                        new ExobiologySpecies("Osseus", "Osseus Spiralis", 1, 2_404_700),
                        new ExobiologySpecies("Frutexa", "Frutexa Flammasis", 1, 10_326_000),
                    ],
                    b.Species);
            },
            b =>
            {
                Assert.Equal("Blua Eaec JE-U c18-283 3 c", b.Name);
                Assert.Equal(49, b.BodyId);
                Assert.Equal(12_092_600, b.LandmarkValue);
                Assert.Equal(
                    [
                        new ExobiologySpecies("Tussock", "Tussock Catena", 1, 1_766_600),
                        new ExobiologySpecies("Frutexa", "Frutexa Flammasis", 2, 10_326_000),
                    ],
                    b.Species);
            });
    }

    [Fact]
    public async Task BodiesWithNoLandmarksOrAZeroLandmarkValueAreAbsent()
    {
        const string Body =
            """
            {"record":{"id64":1,"bodies":[
              {"id64":1,"name":"A","landmark_value":null},
              {"id64":2,"name":"B","landmark_value":0,
               "landmarks":[{"type":"Stratum","subtype":"Stratum Tectonicas","count":1,"value":19010800}]},
              {"id64":3,"name":"C","landmark_value":5000000,"landmarks":[]},
              {"id64":4,"name":"D"}
            ]}}
            """;

        using var service = Service(new Answer(HttpStatusCode.OK, Body));

        var biology = await service.SystemBiologyAsync(1, TestContext.Current.CancellationToken);

        Assert.Empty(biology.Bodies);
    }

    [Fact]
    public async Task ASystemSpanshDoesNotKnowGivesAnEmptyResult()
    {
        using var service = Service(new Answer(
            HttpStatusCode.NotFound, """{"error":"Could not find record with id64 1"}"""));

        var biology = await service.SystemBiologyAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(1, biology.SystemAddress);
        Assert.Empty(biology.Bodies);
    }

    [Fact]
    public async Task AServerErrorIsTheServiceBeingUnavailable()
    {
        using var service = Service(new Answer(HttpStatusCode.BadGateway, "{}"));

        await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.SystemBiologyAsync(BluaEaec, TestContext.Current.CancellationToken));
    }
}
