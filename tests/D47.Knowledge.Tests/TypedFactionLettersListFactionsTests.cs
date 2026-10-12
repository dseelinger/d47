using System.Net;
using System.Text;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>Against an <c>api/systems/field_values/minor_faction_presences</c> answer captured from the live service on 2026-10-12.</summary>
public class TypedFactionLettersListFactionsTests
{
    private const string MotherAnswer =
        """
        {"min_max":{"influence":{"max":1.0,"min":0.0}},"values":{"allegiance":["Independent","Empire"],"influence":[],"name":["Mother Gaia","Independent Fleet of Mothership"],"state":["None","Boom"]}}
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
    public async Task OnlyTheLongestWordIsAskedAndTheAnswerIsFilteredHere()
    {
        var answer = new Answer(HttpStatusCode.OK, MotherAnswer);
        using var service = Service(answer);

        var names = await service.FactionNamesAsync(" Mother G ", TestContext.Current.CancellationToken);

        Assert.Equal(["Mother Gaia"], names);

        var request = Assert.Single(answer.Requests);
        Assert.Equal("spansh.co.uk", request.Host);
        Assert.Equal("/api/systems/field_values/minor_faction_presences", request.AbsolutePath);
        Assert.Equal("?q=Mother", request.Query);
    }

    [Fact]
    public async Task TheSameWordInAnyCaseIsAskedOnce()
    {
        var answer = new Answer(HttpStatusCode.OK, MotherAnswer);
        using var service = Service(answer);
        var token = TestContext.Current.CancellationToken;

        await service.FactionNamesAsync("Mother G", token);
        var again = await service.FactionNamesAsync("mother", token);

        Assert.Single(answer.Requests);
        Assert.Equal(["Mother Gaia", "Independent Fleet of Mothership"], again);
    }

    [Fact]
    public async Task ARefusalIsNotKept()
    {
        using var service = Service(new Answer(HttpStatusCode.TooManyRequests, MotherAnswer));

        await Assert.ThrowsAsync<GalaxyUnavailableException>(
            () => service.FactionNamesAsync("Mother", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NothingTypedAsksNothing()
    {
        var answer = new Answer(HttpStatusCode.OK, MotherAnswer);
        using var service = Service(answer);

        Assert.Empty(await service.FactionNamesAsync("  ", TestContext.Current.CancellationToken));
        Assert.Empty(answer.Requests);
    }
}
