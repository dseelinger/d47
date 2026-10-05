using System.Net;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>Against <c>api/search/systems</c> answers captured from the live service on 2026-10-05.</summary>
public class ATypedSystemNameResolvesThroughSpanshTests
{
    private const string LttAnswer =
        """
        {"count":213,"query":"ltt 7786","results":[
        {"id64":633608311522,"name":"LTT 7786","x":7.96875,"y":-41.90625,"z":77.875},
        {"id64":83852497626,"name":"LTT 7876","x":-9.5625,"y":-28.5625,"z":54.3125},
        {"id64":1733254124250,"name":"NLTT 7789","x":76.75,"y":-97.15625,"z":24.28125},
        {"id64":33441745692908,"name":"Kyloarph TT-R e4-7786","x":-6830.65625,"y":-679.6875,"z":21555.78125},
        {"id64":33442081303084,"name":"Phipae TT-R e4-7786","x":5935.09375,"y":542.03125,"z":27973.03125},
        {"id64":267534678806715,"name":"Mylaifa LT-P d6-7786","x":-3498.90625,"y":-407.4375,"z":18728.90625},
        {"id64":267535618593211,"name":"Shrogea TT-P d6-7786","x":975.3125,"y":866.1875,"z":21277.0625}]}
        """;

    private sealed class Answer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        public List<string> UserAgents { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            UserAgents.Add(request.Headers.UserAgent.ToString());

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static SpanshStarSystemService Service(Answer answer) =>
        new(NullLogger<SpanshStarSystemService>.Instance, new HttpClient(answer));

    [Fact]
    public async Task TheExactMatchComesFirstAndAtMostFiveAreReturned()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, LttAnswer));

        var matches = await service.MatchNamesAsync("ltt 7786", TestContext.Current.CancellationToken);

        Assert.Equal(
            new SystemNameMatch("LTT 7786", 633608311522, new StarPosition(7.96875, -41.90625, 77.875)),
            matches[0]);
        Assert.Equal(
            ["LTT 7786", "LTT 7876", "NLTT 7789", "Kyloarph TT-R e4-7786", "Phipae TT-R e4-7786"],
            matches.Select(m => m.Name));
    }

    [Fact]
    public async Task NoMatchIsAnEmptyList()
    {
        using var service = Service(new Answer(HttpStatusCode.OK, """{"count":0,"query":"zzqqxxv","results":[]}"""));

        Assert.Empty(await service.MatchNamesAsync("zzqqxxv", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheSearchIsAskedOfSpanshWithD47sUserAgent()
    {
        var answer = new Answer(HttpStatusCode.OK, LttAnswer);
        using var service = Service(answer);

        await service.MatchNamesAsync(" ltt 7786 ", TestContext.Current.CancellationToken);

        var request = Assert.Single(answer.Requests);
        Assert.Equal("spansh.co.uk", request.Host);
        Assert.Equal("/api/search/systems", request.AbsolutePath);
        Assert.Equal("?q=ltt%207786", request.Query);
        Assert.StartsWith("d47/", Assert.Single(answer.UserAgents), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusalIsTheServiceBeingUnavailable()
    {
        using var service = Service(new Answer(HttpStatusCode.TooManyRequests, "{}"));

        await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.MatchNamesAsync("ltt 7786", TestContext.Current.CancellationToken));
    }
}
