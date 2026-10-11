using System.Net;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>Against an <c>api/systems/field_values/system_names</c> answer captured from the live service on 2026-10-11.</summary>
public class TypedLettersListSystemsThatStartWithThemTests
{
    private const string DiaAnswer =
        """
        {"min_max":[{"id64":670417429889,"name":"Diaguandri","x":-41.0625,"y":-62.15625,"z":-103.25},
        {"id64":7268560741769,"name":"Dian","x":48.375,"y":-105.34375,"z":-80.65625},
        {"id64":5068732573049,"name":"Diaguthivar","x":-8.6875,"y":2.0625,"z":-108.375},
        {"id64":3240259225955,"name":"Diabozo","x":-159.34375,"y":28.9375,"z":-89.03125},
        {"id64":3446467955067,"name":"Diabingo","x":15.3125,"y":-142.875,"z":152.125},
        {"id64":40561134160672,"name":"Diabanda","x":91.21875,"y":-25.96875,"z":-57.53125},
        {"id64":1247427971435,"name":"Diadem","x":5.5,"y":57.0625,"z":9.59375},
        {"id64":669343819153,"name":"Diaguan","x":-122.9375,"y":-24.46875,"z":-60.78125},
        {"id64":2558022062802,"name":"Diaba","x":164.0,"y":-99.6875,"z":6.96875},
        {"id64":4717778307435,"name":"Diabak","x":35.84375,"y":-22.0,"z":-24.15625},
        {"id64":5068732835201,"name":"Diakas","x":-20.96875,"y":76.0,"z":-97.625},
        {"id64":4481899041514,"name":"Dian Cecht","x":-9.3125,"y":1.03125,"z":132.46875}],
        "values":["Diaguandri","Dian","Diaguthivar","Diabozo","Diabingo","Diabanda","Diadem","Diaguan","Diaba","Diabak","Diakas","Dian Cecht"]}
        """;

    private sealed class Answer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        public HttpStatusCode Status { get; set; } = status;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            lock (Requests)
            {
                Requests.Add(request.RequestUri!);
            }

            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static SpanshStarSystemService Service(Answer answer) =>
        new(NullLogger<SpanshStarSystemService>.Instance, new HttpClient(answer));

    [Fact]
    public async Task EveryNameIsReadWithItsAddressAndPositionInSpanshsOrder()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, DiaAnswer));

        var matches = await service.PrefixNamesAsync("Dia", TestContext.Current.CancellationToken);

        Assert.Equal(12, matches.Count);
        Assert.Equal(
            new SystemNameMatch("Diaguandri", 670417429889, new StarPosition(-41.0625, -62.15625, -103.25)),
            matches[0]);
        Assert.Equal("Dian", matches[1].Name);
        Assert.Equal("Dian Cecht", matches[^1].Name);
    }

    [Fact]
    public async Task TheLettersAreAskedOfSpanshsPrefixLookup()
    {
        var answer = new Answer(HttpStatusCode.OK, DiaAnswer);
        using var service = Service(answer);

        await service.PrefixNamesAsync(" Dia g ", TestContext.Current.CancellationToken);

        var request = Assert.Single(answer.Requests);
        Assert.Equal("spansh.co.uk", request.Host);
        Assert.Equal("/api/systems/field_values/system_names", request.AbsolutePath);
        Assert.Equal("?q=Dia%20g", request.Query);
    }

    [Fact]
    public async Task TheSameLettersInAnyCaseAreAskedOnce()
    {
        var answer = new Answer(HttpStatusCode.OK, DiaAnswer);
        using var service = Service(answer);
        var token = TestContext.Current.CancellationToken;

        var first = await service.PrefixNamesAsync("Dia", token);
        await service.PrefixNamesAsync("Di", token);
        var again = await service.PrefixNamesAsync("dia", token);

        Assert.Equal(2, answer.Requests.Count);
        Assert.Same(first, again);
    }

    [Fact]
    public async Task ARefusalIsNotKept()
    {
        var answer = new Answer(HttpStatusCode.TooManyRequests, DiaAnswer);
        using var service = Service(answer);
        var token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<GalaxyUnavailableException>(() => service.PrefixNamesAsync("Dia", token));

        answer.Status = HttpStatusCode.OK;

        Assert.Equal("Diaguandri", (await service.PrefixNamesAsync("Dia", token))[0].Name);
        Assert.Equal(2, answer.Requests.Count);
    }

    [Fact]
    public async Task NothingTypedAsksNothing()
    {
        var answer = new Answer(HttpStatusCode.OK, DiaAnswer);
        using var service = Service(answer);

        Assert.Empty(await service.PrefixNamesAsync("  ", TestContext.Current.CancellationToken));
        Assert.Empty(answer.Requests);
    }
}
