using System.Net;
using System.Text;
using System.Text.Json;
using D47.Core.Knowledge;
using D47.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

/// <summary>RavenColonialPlanWriter against a fake handler; no network.</summary>
public class TheColonisationWriterReportsEachOutcomeTests
{
    private const string Key = "sekrit-key-123";

    private sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Key, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.Headers.TryGetValues("rcc-key", out var values) ? values.Single() : null;
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, key, body));

            return answer(request);
        }
    }

    private static HttpResponseMessage Status(HttpStatusCode code, string body = "") =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (RavenColonialPlanWriter Writer, Fake Fake) Make(
        Func<HttpRequestMessage, HttpResponseMessage>? answer = null, string? key = Key)
    {
        var fake = new Fake(answer ?? (_ => Status(HttpStatusCode.OK)));

        return (new RavenColonialPlanWriter(() => key, new HttpClient(fake)), fake);
    }

    private static readonly Dictionary<string, int> Tonnes = new() { ["$Steel_Name;"] = 5, ["Water"] = 2 };

    [Fact]
    public async Task EachCallUsesItsMethodPathAndKey()
    {
        var (writer, fake) = Make();
        var t = CancellationToken.None;

        await writer.CommanderAsync(t);
        await writer.NeedsAsync("b 1", Tonnes, 10, t);
        await writer.ContributeAsync("b 1", "CMDR X", Tonnes, t);
        await writer.CompleteAsync("b 1", t);
        await writer.CarrierRegisteredAsync(42, t);
        await writer.CarrierMovedAsync(42, Tonnes, t);

        Assert.Equal(
            [
                (HttpMethod.Get, "/api/cmdr/"),
                (HttpMethod.Post, "/api/project/b%201"),
                (HttpMethod.Post, "/api/project/b%201/contribute/CMDR%20X"),
                (HttpMethod.Post, "/api/project/b%201/complete"),
                (HttpMethod.Get, "/api/FC/42"),
                (HttpMethod.Patch, "/api/FC/42/cargo"),
            ],
            fake.Requests.Select(r => (r.Method, r.Path)));
        Assert.All(fake.Requests, r => Assert.Equal(Key, r.Key));
    }

    [Fact]
    public async Task ACommanderNameWithASpaceAndAnApostropheIsEscaped()
    {
        var (writer, fake) = Make();

        await writer.ContributeAsync("b1", "O'Neil Smith", Tonnes, CancellationToken.None);

        Assert.Equal("/api/project/b1/contribute/O%27Neil%20Smith", fake.Requests.Single().Path);
    }

    [Fact]
    public async Task TheNeedsBodyHasExactlyThreeProperties()
    {
        var (writer, fake) = Make();

        await writer.NeedsAsync("b1", Tonnes, 10, CancellationToken.None);

        using var body = JsonDocument.Parse(fake.Requests.Single().Body!);
        Assert.Equal(["buildId", "commodities", "maxNeed"], body.RootElement.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(5, body.RootElement.GetProperty("commodities").GetProperty("steel").GetInt32());
        Assert.Equal(10, body.RootElement.GetProperty("maxNeed").GetInt32());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, PlanWriteOutcome.Sent)]
    [InlineData(HttpStatusCode.NoContent, PlanWriteOutcome.Sent)]
    [InlineData(HttpStatusCode.NotFound, PlanWriteOutcome.NotOnPlanner)]
    [InlineData(HttpStatusCode.Unauthorized, PlanWriteOutcome.KeyRefused)]
    [InlineData(HttpStatusCode.Forbidden, PlanWriteOutcome.KeyRefused)]
    [InlineData(HttpStatusCode.BadRequest, PlanWriteOutcome.Failed)]
    [InlineData(HttpStatusCode.InternalServerError, PlanWriteOutcome.Failed)]
    public async Task EachStatusMapsToItsOutcome(HttpStatusCode code, PlanWriteOutcome expected)
    {
        var (writer, _) = Make(_ => Status(code));

        var result = await writer.CompleteAsync("b1", CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task ANetworkErrorIsFailed()
    {
        var (writer, _) = Make(_ => throw new HttpRequestException("down"));

        Assert.Equal(PlanWriteOutcome.Failed, (await writer.CompleteAsync("b1", CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task ATimeoutIsFailed()
    {
        var (writer, _) = Make(_ => throw new TaskCanceledException("slow"));

        Assert.Equal(PlanWriteOutcome.Failed, (await writer.CompleteAsync("b1", CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task NoKeyMakesNoRequestAndIsRefused()
    {
        var (writer, fake) = Make(key: null);

        var result = await writer.CompleteAsync("b1", CancellationToken.None);
        var commander = await writer.CommanderAsync(CancellationToken.None);

        Assert.Equal(PlanWriteOutcome.KeyRefused, result.Outcome);
        Assert.Equal("No Raven Colonial key is set.", result.Message);
        Assert.Equal(PlanWriteOutcome.KeyRefused, commander.Outcome);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task NoMessageHoldsTheKeyOrTheResponseBody()
    {
        var (writer, _) = Make(_ => Status(HttpStatusCode.BadRequest, $"echo {Key} secret-body"));

        var result = await writer.NeedsAsync("b1", Tonnes, 1, CancellationToken.None);

        Assert.Equal("400 Bad Request", result.Message);
        Assert.DoesNotContain(Key, result.Message);
        Assert.DoesNotContain("secret-body", result.Message);
    }

    [Theory]
    [InlineData("\"CMDR One\"")]
    [InlineData("{\"cmdr\":\"CMDR One\"}")]
    public async Task TheCommanderIsReadFromTheAnswer(string body)
    {
        var (writer, _) = Make(_ => Status(HttpStatusCode.OK, body));

        var commander = await writer.CommanderAsync(CancellationToken.None);

        Assert.Equal(new PlanCommander(PlanWriteOutcome.Sent, "CMDR One"), commander);
    }

    [Fact]
    public async Task AnUnreadableCommanderAnswerIsFailed()
    {
        var (writer, _) = Make(_ => Status(HttpStatusCode.OK, "<html>"));

        Assert.Equal(PlanWriteOutcome.Failed, (await writer.CommanderAsync(CancellationToken.None)).Outcome);
    }
}
