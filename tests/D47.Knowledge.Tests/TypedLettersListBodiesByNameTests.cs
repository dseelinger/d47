using System.Net;
using System.Text;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public class TypedLettersListBodiesByNameTests
{
    private const string BodyAnswer =
        """
        {"min_max":[{"id64":3238296097059,"name":"Sol 3","system_id64":10477373803,"system_x":0.0,"system_y":0.0,"system_z":0.0},
        {"id64":5,"name":"No System","system_x":0.0,"system_y":0.0,"system_z":0.0},
        {"id64":7,"name":"Sol 4 a","system_id64":2,"system_x":1.0,"system_y":2.0,"system_z":3.0}]}
        """;

    private sealed class Answer(string body) : HttpMessageHandler
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

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static SpanshStarSystemService Service(Answer answer) =>
        new(NullLogger<SpanshStarSystemService>.Instance, new HttpClient(answer));

    [Fact]
    public async Task EveryBodyIsReadWithItsAddressSystemAndPositionAndAnIncompleteEntryIsSkipped()
    {
        using var service = Service(new Answer(BodyAnswer));

        var matches = await service.BodyNamesAsync("sol", TestContext.Current.CancellationToken);

        Assert.Equal(2, matches.Count);
        Assert.Equal(new BodyNameMatch("Sol 3", 3238296097059, 10477373803, new StarPosition(0, 0, 0)), matches[0]);
        Assert.Equal("Sol 4 a", matches[1].Name);
    }

    [Fact]
    public async Task TheTextIsAskedOfSpanshsBodyNameLookup()
    {
        var answer = new Answer(BodyAnswer);
        using var service = Service(answer);

        await service.BodyNamesAsync(" Sol 3 ", TestContext.Current.CancellationToken);

        var request = Assert.Single(answer.Requests);
        Assert.Equal("/api/bodies/field_values/name", request.AbsolutePath);
        Assert.Equal("?q=Sol%203", request.Query);
    }

    [Fact]
    public async Task TheSameTextInAnyCaseIsAskedOnce()
    {
        var answer = new Answer(BodyAnswer);
        using var service = Service(answer);
        var token = TestContext.Current.CancellationToken;

        await service.BodyNamesAsync("Sol", token);
        await service.BodyNamesAsync("sol", token);

        Assert.Single(answer.Requests);
    }

    [Fact]
    public async Task BlankTextAsksNothing()
    {
        var answer = new Answer(BodyAnswer);
        using var service = Service(answer);

        Assert.Empty(await service.BodyNamesAsync("   ", TestContext.Current.CancellationToken));
        Assert.Empty(answer.Requests);
    }
}
